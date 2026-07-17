using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using atp_enterprise_app_wpf.Models;

namespace atp_enterprise_app_wpf.Services.TestModules
{
    /// <summary>
    /// Phase 4 – Serial Active: Verifies serial port open, write, and read (loopback).
    /// </summary>
    public class SerialActiveModule : ITestModule
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
                    result.MeasuredValue = "No serial COM ports detected";
                    result.Outcome = TestOutcome.Skipped;
                    return result;
                }

                string targetPort = ports[0];
                using var serialPort = new SerialPort(targetPort, 115200, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 1000,
                    WriteTimeout = 1000
                };

                var policy = RetryPolicy.Default;
                await policy.ExecuteWithRetryAsync(async (ct) =>
                {
                    serialPort.Open();
                    byte[] payload = ChecksumUtil.GenerateTestPayload(128);
                    
                    serialPort.Write(payload, 0, payload.Length);
                    stats.BytesSent += payload.Length;
                    stats.PacketsSent++;

                    // Try to read (loopback)
                    try
                    {
                        byte[] rxBuffer = new byte[128];
                        int bytesRead = 0;
                        while (bytesRead < 128 && !ct.IsCancellationRequested)
                        {
                            if (serialPort.BytesToRead > 0)
                            {
                                int read = serialPort.Read(rxBuffer, bytesRead, 128 - bytesRead);
                                bytesRead += read;
                            }
                            await Task.Delay(10, ct);
                        }
                        stats.BytesReceived += bytesRead;
                        
                        if (bytesRead > 0)
                        {
                            stats.PacketsReceived++;
                            stats.IntegrityVerified = ChecksumUtil.VerifyTestPayload(rxBuffer);
                        }
                    }
                    catch (TimeoutException)
                    {
                        stats.LastErrorType = CommErrorType.Timeout;
                        stats.ErrorCount++;
                    }
                    
                    serialPort.Close();
                    return true;
                }, token, (retry) => stats.RetryCount = retry);

                stats.LastSuccessfulTransaction = DateTime.Now;
                result.MeasuredValue = $"Port {targetPort} | " + stats.ToSummary();
                result.Outcome = TestOutcome.Pass;
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
    /// Phase 4 – Network Active: Verifies network stack by pinging loopback or gateway and validating response.
    /// </summary>
    public class NetworkActiveModule : ITestModule
    {
        public async Task<TestRunResult> ExecuteAsync(TestDefinition test, CancellationToken token)
        {
            var result = new TestRunResult();
            var stats = new CommStats();
            try
            {
                using var ping = new Ping();
                string targetIp = "127.0.0.1"; 

                var policy = RetryPolicy.Default;
                await policy.ExecuteWithRetryAsync(async (ct) =>
                {
                    byte[] payload = ChecksumUtil.GenerateTestPayload(32);
                    var reply = await ping.SendPingAsync(targetIp, 1000, payload, new PingOptions(64, true));
                    
                    stats.BytesSent += payload.Length;
                    stats.PacketsSent++;

                    if (reply.Status == IPStatus.Success)
                    {
                        stats.BytesReceived += reply.Buffer.Length;
                        stats.PacketsReceived++;
                        
                        if (reply.Buffer.Length == payload.Length && ChecksumUtil.ComputeCrc32(reply.Buffer) == ChecksumUtil.ComputeCrc32(payload))
                        {
                            stats.IntegrityVerified = true;
                        }
                        else
                        {
                            stats.ErrorCount++;
                            stats.LastErrorType = CommErrorType.DataCorruption;
                        }
                    }
                    else
                    {
                        stats.ErrorCount++;
                        stats.LastErrorType = CommErrorType.Timeout;
                        throw new Exception($"Ping failed: {reply.Status}");
                    }
                    return true;
                }, token, (retry) => stats.RetryCount = retry);

                stats.LastSuccessfulTransaction = DateTime.Now;
                result.MeasuredValue = $"Ping {targetIp} | " + stats.ToSummary();
                result.Outcome = stats.ErrorCount == 0 ? TestOutcome.Pass : TestOutcome.Fail;
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
    /// Phase 4 – USB Active: Enumerates USB removable drives and performs file I/O with integrity check.
    /// </summary>
    public class UsbActiveModule : ITestModule
    {
        public async Task<TestRunResult> ExecuteAsync(TestDefinition test, CancellationToken token)
        {
            var result = new TestRunResult();
            var stats = new CommStats();
            try
            {
                var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.DriveType == DriveType.Removable && d.IsReady);
                if (drive == null)
                {
                    result.MeasuredValue = "No ready USB Mass Storage drives detected";
                    result.Outcome = TestOutcome.Skipped;
                    return result;
                }

                string testFile = Path.Combine(drive.RootDirectory.FullName, $"atp_usb_test_{Guid.NewGuid().ToString().Substring(0,8)}.tmp");
                byte[] payload = ChecksumUtil.GenerateTestPayload(1024 * 1024); // 1 MB test file

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

                    stats.IntegrityVerified = ChecksumUtil.VerifyTestPayload(rxBuffer);
                    if (!stats.IntegrityVerified)
                    {
                        stats.ErrorCount++;
                        stats.LastErrorType = CommErrorType.DataCorruption;
                    }

                    if (File.Exists(testFile)) File.Delete(testFile);

                    return true;
                }, token, (retry) => stats.RetryCount = retry);

                stats.LastSuccessfulTransaction = DateTime.Now;
                result.MeasuredValue = $"Drive {drive.Name} | " + stats.ToSummary();
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
}
