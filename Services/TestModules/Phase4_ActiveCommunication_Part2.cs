using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using atp_enterprise_app_wpf.Models;

namespace atp_enterprise_app_wpf.Services.TestModules
{
    /// <summary>
    /// Phase 4 – Storage Active: Verifies storage integrity by writing a block, reading it back, and hashing it.
    /// </summary>
    public class StorageIntegrityModule : ITestModule
    {
        public async Task<TestRunResult> ExecuteAsync(TestDefinition test, CancellationToken token)
        {
            var result = new TestRunResult();
            var stats = new CommStats();
            try
            {
                string targetFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp");
                if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

                string testFile = Path.Combine(targetFolder, $"atp_storage_integrity_{Guid.NewGuid().ToString().Substring(0,8)}.tmp");
                byte[] payload = ChecksumUtil.GenerateTestPayload(4 * 1024 * 1024); // 4 MB block

                var policy = RetryPolicy.Default;
                await policy.ExecuteWithRetryAsync(async (ct) =>
                {
                    using (var fs = new FileStream(testFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                    {
                        await fs.WriteAsync(payload, 0, payload.Length, ct);
                    }
                    stats.BytesSent += payload.Length;
                    stats.PacketsSent++;

                    byte[] rxBuffer = new byte[payload.Length];
                    using (var fs = new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        int bytesRead = await fs.ReadAsync(rxBuffer, 0, rxBuffer.Length, ct);
                        stats.BytesReceived += bytesRead;
                        stats.PacketsReceived++;
                    }

                    stats.IntegrityVerified = ChecksumUtil.ComputeSha256(payload) == ChecksumUtil.ComputeSha256(rxBuffer);
                    if (!stats.IntegrityVerified)
                    {
                        stats.ErrorCount++;
                        stats.LastErrorType = CommErrorType.DataCorruption;
                    }

                    if (File.Exists(testFile)) File.Delete(testFile);
                    return true;
                }, token, (retry) => stats.RetryCount = retry);

                stats.LastSuccessfulTransaction = DateTime.Now;
                result.MeasuredValue = $"Storage C: | " + stats.ToSummary();
                result.Outcome = stats.IntegrityVerified ? TestOutcome.Pass : TestOutcome.Fail;
            }
            catch (Exception ex)
            {
                stats.LastErrorType = CommErrorType.HardwareFailure;
                stats.ErrorCount++;
                stats.LastErrorMessage = ex.Message;
                result.Outcome = TestOutcome.Error;
                result.ErrorDetails = ex.Message;
                result.MeasuredValue = stats.ToSummary();
            }
            return result;
        }
    }

    /// <summary>
    /// Phase 4 – GPS Active: Attempts to read NMEA sentences from available COM ports.
    /// </summary>
    public class GpsActiveModule : ITestModule
    {
        public async Task<TestRunResult> ExecuteAsync(TestDefinition test, CancellationToken token)
        {
            var result = new TestRunResult();
            var stats = new CommStats();
            try
            {
                var ports = SerialPort.GetPortNames();
                if (ports.Length == 0)
                {
                    result.MeasuredValue = "No serial COM ports detected for GPS";
                    result.Outcome = TestOutcome.Skipped;
                    return result;
                }

                // In a real scenario, this would try all ports or specific configured ports.
                // For this module, we will just try the first port as a representation.
                string targetPort = ports.Last(); // GPS is often on higher COM ports
                using var serialPort = new SerialPort(targetPort, 9600, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 2000,
                    NewLine = "\r\n"
                };

                bool nmeaReceived = false;
                string sampleSentence = "";

                try
                {
                    serialPort.Open();
                    
                    // Listen for 3 seconds for any NMEA sentence
                    var endTime = DateTime.Now.AddSeconds(3);
                    while (DateTime.Now < endTime && !token.IsCancellationRequested)
                    {
                        if (serialPort.BytesToRead > 0)
                        {
                            string line = serialPort.ReadLine();
                            stats.BytesReceived += line.Length;
                            stats.PacketsReceived++;

                            if (line.StartsWith("$GP") || line.StartsWith("$GN"))
                            {
                                nmeaReceived = true;
                                sampleSentence = line;
                                stats.IntegrityVerified = true;
                                break;
                            }
                        }
                        await Task.Delay(50, token);
                    }
                }
                catch (TimeoutException)
                {
                    stats.LastErrorType = CommErrorType.Timeout;
                    stats.ErrorCount++;
                }
                catch (Exception ex)
                {
                    stats.LastErrorType = CommErrorType.AccessDenied;
                    stats.ErrorCount++;
                    stats.LastErrorMessage = ex.Message;
                }
                finally
                {
                    if (serialPort.IsOpen) serialPort.Close();
                }

                if (nmeaReceived)
                {
                    stats.LastSuccessfulTransaction = DateTime.Now;
                    result.MeasuredValue = $"GPS on {targetPort} | Sentence: {sampleSentence} | " + stats.ToSummary();
                    result.Outcome = TestOutcome.Pass;
                }
                else
                {
                    result.MeasuredValue = $"No NMEA data on {targetPort}";
                    result.Outcome = TestOutcome.Skipped; // Skip if no GPS hardware responds, per ATP spec
                }
            }
            catch (Exception ex)
            {
                result.Outcome = TestOutcome.Error;
                result.ErrorDetails = ex.Message;
            }
            return result;
        }
    }

    /// <summary>
    /// Phase 4 – Audio Active: Detects playback and recording devices.
    /// </summary>
    public class AudioActiveModule : ITestModule
    {
        public Task<TestRunResult> ExecuteAsync(TestDefinition test, CancellationToken token)
        {
            var result = new TestRunResult();
            try
            {
                var scope = new ManagementScope(@"\\.\root\cimv2");
                scope.Connect();
                var searcher = new ManagementObjectSearcher(scope, new SelectQuery("SELECT Name, Status FROM Win32_SoundDevice"));
                var collection = searcher.Get();

                int deviceCount = 0;
                string names = "";
                foreach (ManagementObject mObj in collection)
                {
                    names += $"[{mObj["Name"]?.ToString()?.Trim()}] ";
                    deviceCount++;
                }

                result.MeasuredValue = deviceCount > 0
                    ? $"{deviceCount} audio device(s): {names.Trim()}"
                    : "No audio devices detected";
                
                result.Outcome = deviceCount > 0 ? TestOutcome.Pass : TestOutcome.Skipped;
                
                collection.Dispose();
                searcher.Dispose();
            }
            catch (Exception ex)
            {
                result.Outcome = TestOutcome.Error;
                result.ErrorDetails = ex.Message;
            }
            return Task.FromResult(result);
        }
    }
}
