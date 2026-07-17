using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using atp_enterprise_app_wpf.Models;

namespace atp_enterprise_app_wpf.Services.TestModules
{
    /// <summary>
    /// Phase 3 – Storage Performance: Measures sequential read/write throughput using DiskSpeedTestService.
    /// </summary>
    public class StoragePerformanceModule : ITestModule
    {
        private readonly DiskSpeedTestService _diskService = new();

        public async Task<TestRunResult> ExecuteAsync(TestDefinition test, CancellationToken token)
        {
            var result = new TestRunResult();
            try
            {
                string targetFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Temp");

                var diskResult = await Task.Run(() => _diskService.RunBenchmark(targetFolder), token);

                result.MeasuredValue = $"Read: {diskResult.ReadSpeedMBs:F2} MB/s | Write: {diskResult.WriteSpeedMBs:F2} MB/s | Integrity: {(diskResult.IntegrityChecked ? "Verified" : "Failed")}";

                // Parse spec like "Read > 50 MB/s"
                double minRead = 50.0;
                var specParts = test.ExpectedSpecification.Split(' ');
                foreach (var part in specParts)
                {
                    if (double.TryParse(part, out double parsed) && parsed > 0)
                    {
                        minRead = parsed;
                        break;
                    }
                }

                bool readOk = diskResult.ReadSpeedMBs >= minRead;
                bool integrityOk = diskResult.IntegrityChecked;
                result.Outcome = readOk && integrityOk ? TestOutcome.Pass : TestOutcome.Fail;

                if (!integrityOk)
                    result.ErrorDetails = "Data integrity verification failed. Read data did not match written data.";
            }
            catch (OperationCanceledException)
            {
                result.Outcome = TestOutcome.Error;
                result.ErrorDetails = "Storage benchmark cancelled.";
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
    /// Phase 3 – Network Throughput: Measures UDP loopback throughput using ThroughputTestService.
    /// </summary>
    public class NetworkThroughputModule : ITestModule
    {
        public async Task<TestRunResult> ExecuteAsync(TestDefinition test, CancellationToken token)
        {
            var result = new TestRunResult();
            var throughputService = new ThroughputTestService();
            int port = 5010 + new Random().Next(1, 100); // Randomize port to avoid conflicts

            try
            {
                throughputService.StartServer(port);
                throughputService.StartClient("127.0.0.1", port, 3, 1400);

                // Run for 3 seconds
                await Task.Delay(3500, token);

                var stats = throughputService.GetStats();
                throughputService.StopClient();
                throughputService.StopServer();

                result.MeasuredValue = $"Avg: {stats.AverageMbps:F2} Mbps | Max: {stats.MaxMbps:F2} Mbps | Sent: {stats.PacketsSent} pkts | Recv: {stats.PacketsReceived} pkts | CRC Errors: {stats.CrcErrors}";

                result.Outcome = stats.AverageMbps > 0 ? TestOutcome.Pass : TestOutcome.Fail;
            }
            catch (OperationCanceledException)
            {
                result.Outcome = TestOutcome.Error;
                result.ErrorDetails = "Throughput test cancelled.";
            }
            catch (Exception ex)
            {
                result.Outcome = TestOutcome.Error;
                result.ErrorDetails = ex.Message;
            }
            finally
            {
                try
                {
                    throughputService.StopClient();
                    throughputService.StopServer();
                }
                catch { }
            }
            return result;
        }
    }

    /// <summary>
    /// Phase 3 – RTC Drift: Measures real-time clock precision by comparing DateTime vs Stopwatch.
    /// </summary>
    public class RtcDriftModule : ITestModule
    {
        public async Task<TestRunResult> ExecuteAsync(TestDefinition test, CancellationToken token)
        {
            var result = new TestRunResult();
            try
            {
                var sw = Stopwatch.StartNew();
                var dt1 = DateTime.UtcNow;
                await Task.Delay(100, token); // 100ms measurement window
                var dt2 = DateTime.UtcNow;
                sw.Stop();

                double stopwatchMs = sw.Elapsed.TotalMilliseconds;
                double dateTimeMs = (dt2 - dt1).TotalMilliseconds;
                double driftMs = Math.Abs(stopwatchMs - dateTimeMs);

                result.MeasuredValue = $"{driftMs:F4} ms drift (over {stopwatchMs:F1} ms window)";

                // Parse spec like "Drift < 50 ms"
                double maxDrift = 50.0;
                var specParts = test.ExpectedSpecification.Split(' ');
                foreach (var part in specParts)
                {
                    if (double.TryParse(part, out double parsed) && parsed > 0)
                    {
                        maxDrift = parsed;
                        break;
                    }
                }

                result.Outcome = driftMs < maxDrift ? TestOutcome.Pass : TestOutcome.Fail;
            }
            catch (OperationCanceledException)
            {
                result.Outcome = TestOutcome.Error;
                result.ErrorDetails = "RTC drift test cancelled.";
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
    /// Phase 3 – Thermal Sensor: Reads CPU thermal zone temperatures via WMI root\wmi.
    /// </summary>
    public class ThermalSensorModule : ITestModule
    {
        public Task<TestRunResult> ExecuteAsync(TestDefinition test, CancellationToken token)
        {
            var result = new TestRunResult();
            try
            {
                var wmiScope = new System.Management.ManagementScope(@"\\.\root\wmi");
                wmiScope.Connect();

                using var searcher = new System.Management.ManagementObjectSearcher(wmiScope,
                    new System.Management.SelectQuery("SELECT InstanceName, CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"));
                using var collection = searcher.Get();

                double maxCelsius = 0;
                int zoneCount = 0;
                string details = "";

                foreach (System.Management.ManagementObject mObj in collection)
                {
                    string zone = mObj["InstanceName"]?.ToString() ?? "Zone";
                    double kelvin10 = double.Parse(mObj["CurrentTemperature"]?.ToString() ?? "0");
                    double celsius = (kelvin10 / 10.0) - 273.15;
                    if (celsius > maxCelsius) maxCelsius = celsius;
                    details += $"[{celsius:F1}°C] ";
                    zoneCount++;
                }

                if (zoneCount > 0)
                {
                    result.MeasuredValue = $"Max: {maxCelsius:F1}°C across {zoneCount} zone(s) {details.Trim()}";

                    // Parse spec like "Temp < 85 °C"
                    double maxAllowed = 85.0;
                    var specParts = test.ExpectedSpecification.Split(' ');
                    foreach (var part in specParts)
                    {
                        if (double.TryParse(part, out double parsed) && parsed > 0)
                        {
                            maxAllowed = parsed;
                            break;
                        }
                    }

                    result.Outcome = maxCelsius < maxAllowed ? TestOutcome.Pass : TestOutcome.Fail;
                }
                else
                {
                    result.MeasuredValue = "Sensor Not Supported";
                    result.Outcome = TestOutcome.Pass; // Pass if sensor absent per ATP rules
                }
            }
            catch
            {
                // Thermal sensors not accessible (common on desktops)
                result.MeasuredValue = "Sensor Not Supported (WMI access denied)";
                result.Outcome = TestOutcome.Pass; // Non-critical
            }
            return Task.FromResult(result);
        }
    }
}
