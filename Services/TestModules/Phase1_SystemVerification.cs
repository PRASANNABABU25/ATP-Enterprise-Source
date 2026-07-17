using System;
using System.Diagnostics;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using atp_enterprise_app_wpf.Models;

namespace atp_enterprise_app_wpf.Services.TestModules
{
    /// <summary>
    /// Phase 1 – Power: Verifies AC power line status using kernel32 GetSystemPowerStatus.
    /// </summary>
    public class PowerTestModule : ITestModule
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool GetSystemPowerStatus(out SystemPowerStatusStruct lpSystemPowerStatus);

        private struct SystemPowerStatusStruct
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public uint BatteryLifeTime;
            public uint BatteryFullLifeTime;
        }

        public Task<TestRunResult> ExecuteAsync(TestDefinition test, CancellationToken token)
        {
            var result = new TestRunResult();
            try
            {
                if (GetSystemPowerStatus(out var status))
                {
                    string source = status.ACLineStatus == 1 ? "AC Online" : (status.ACLineStatus == 0 ? "DC Battery" : "Unknown");
                    result.MeasuredValue = source;

                    if (test.ExpectedSpecification.Contains("AC"))
                    {
                        result.Outcome = status.ACLineStatus == 1 ? TestOutcome.Pass : TestOutcome.Fail;
                    }
                    else
                    {
                        result.Outcome = TestOutcome.Pass; // Any power source is acceptable
                    }
                }
                else
                {
                    result.MeasuredValue = "GetSystemPowerStatus failed";
                    result.Outcome = TestOutcome.Error;
                    result.ErrorDetails = "Native API call returned false.";
                }
            }
            catch (Exception ex)
            {
                result.Outcome = TestOutcome.Error;
                result.ErrorDetails = ex.Message;
            }
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Phase 1 – System: Verifies the OS has booted with positive uptime.
    /// </summary>
    public class BootTestModule : ITestModule
    {
        public Task<TestRunResult> ExecuteAsync(TestDefinition test, CancellationToken token)
        {
            var result = new TestRunResult();
            try
            {
                double uptimeSec = Environment.TickCount64 / 1000.0;
                result.MeasuredValue = $"{uptimeSec:F0} seconds";
                result.Outcome = uptimeSec > 0 ? TestOutcome.Pass : TestOutcome.Fail;
            }
            catch (Exception ex)
            {
                result.Outcome = TestOutcome.Error;
                result.ErrorDetails = ex.Message;
            }
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Phase 1 – Processor: Reads CPU model, core count, and clock speed via WMI.
    /// </summary>
    public class CpuTestModule : ITestModule
    {
        public Task<TestRunResult> ExecuteAsync(TestDefinition test, CancellationToken token)
        {
            var result = new TestRunResult();
            try
            {
                var scope = new ManagementScope(@"\\.\root\cimv2");
                scope.Connect();
                using var searcher = new ManagementObjectSearcher(scope, new SelectQuery("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor"));
                using var collection = searcher.Get();

                foreach (ManagementObject mObj in collection)
                {
                    string name = mObj["Name"]?.ToString()?.Trim() ?? "Not Available";
                    string cores = mObj["NumberOfCores"]?.ToString() ?? "0";
                    string threads = mObj["NumberOfLogicalProcessors"]?.ToString() ?? "0";
                    string maxClock = mObj["MaxClockSpeed"]?.ToString() ?? "0";

                    result.MeasuredValue = $"{name} | {cores}C/{threads}T @ {maxClock} MHz";
                    result.Outcome = !string.IsNullOrEmpty(name) && name != "Not Available" ? TestOutcome.Pass : TestOutcome.Fail;
                    break;
                }

                if (string.IsNullOrEmpty(result.MeasuredValue))
                {
                    result.MeasuredValue = "No CPU detected via WMI";
                    result.Outcome = TestOutcome.Fail;
                }
            }
            catch (Exception ex)
            {
                result.Outcome = TestOutcome.Error;
                result.ErrorDetails = ex.Message;
            }
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Phase 1 – Memory: Verifies installed RAM capacity via WMI.
    /// </summary>
    public class RamTestModule : ITestModule
    {
        public Task<TestRunResult> ExecuteAsync(TestDefinition test, CancellationToken token)
        {
            var result = new TestRunResult();
            try
            {
                var scope = new ManagementScope(@"\\.\root\cimv2");
                scope.Connect();
                using var searcher = new ManagementObjectSearcher(scope, new SelectQuery("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem"));
                using var collection = searcher.Get();

                foreach (ManagementObject mObj in collection)
                {
                    if (double.TryParse(mObj["TotalVisibleMemorySize"]?.ToString(), out double totalKb))
                    {
                        double totalGb = totalKb / 1048576.0;
                        result.MeasuredValue = $"{totalGb:F2} GB";

                        // Parse spec like "RAM >= 4 GB"
                        double minGb = 4.0;
                        var specParts = test.ExpectedSpecification.Split(' ');
                        foreach (var part in specParts)
                        {
                            if (double.TryParse(part, out double parsed) && parsed > 0)
                            {
                                minGb = parsed;
                                break;
                            }
                        }
                        result.Outcome = totalGb >= minGb ? TestOutcome.Pass : TestOutcome.Fail;
                    }
                    break;
                }
            }
            catch (Exception ex)
            {
                result.Outcome = TestOutcome.Error;
                result.ErrorDetails = ex.Message;
            }
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Phase 1 – Storage: Detects primary storage device model and capacity via WMI.
    /// </summary>
    public class StorageDetectionModule : ITestModule
    {
        public Task<TestRunResult> ExecuteAsync(TestDefinition test, CancellationToken token)
        {
            var result = new TestRunResult();
            try
            {
                var scope = new ManagementScope(@"\\.\root\cimv2");
                scope.Connect();
                using var searcher = new ManagementObjectSearcher(scope, new SelectQuery("SELECT Model, Size, MediaType FROM Win32_DiskDrive"));
                using var collection = searcher.Get();

                int count = 0;
                string details = "";
                foreach (ManagementObject mObj in collection)
                {
                    string model = mObj["Model"]?.ToString()?.Trim() ?? "Unknown";
                    string mediaType = mObj["MediaType"]?.ToString()?.Trim() ?? "Unknown";
                    double sizeGb = 0;
                    if (ulong.TryParse(mObj["Size"]?.ToString(), out ulong sizeBytes))
                    {
                        sizeGb = sizeBytes / 1073741824.0;
                    }
                    details += $"[{model} | {sizeGb:F0} GB | {mediaType}] ";
                    count++;
                }

                result.MeasuredValue = count > 0 ? $"{count} drive(s): {details.Trim()}" : "No drives detected";
                result.Outcome = count > 0 ? TestOutcome.Pass : TestOutcome.Fail;
            }
            catch (Exception ex)
            {
                result.Outcome = TestOutcome.Error;
                result.ErrorDetails = ex.Message;
            }
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Phase 1 – Display: Verifies display resolution and monitor count.
    /// </summary>
    public class DisplayResolutionModule : ITestModule
    {
        public Task<TestRunResult> ExecuteAsync(TestDefinition test, CancellationToken token)
        {
            var result = new TestRunResult();
            try
            {
                double screenW = System.Windows.SystemParameters.PrimaryScreenWidth;
                double screenH = System.Windows.SystemParameters.PrimaryScreenHeight;

                // Count monitors via WMI
                int monitorCount = 1;
                try
                {
                    var scope = new ManagementScope(@"\\.\root\cimv2");
                    scope.Connect();
                    var searcher = new ManagementObjectSearcher(scope, new SelectQuery("SELECT * FROM Win32_DesktopMonitor"));
                    var col = searcher.Get();
                    int wmiCount = 0;
                    foreach (ManagementObject _ in col) wmiCount++;
                    if (wmiCount > 0) monitorCount = wmiCount;
                    col.Dispose(); searcher.Dispose();
                }
                catch { }

                result.MeasuredValue = $"{screenW:F0}x{screenH:F0} ({monitorCount} monitor(s))";
                result.Outcome = screenW > 0 && screenH > 0 ? TestOutcome.Pass : TestOutcome.Fail;
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
