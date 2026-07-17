using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using atp_enterprise_app_wpf.Models;

namespace atp_enterprise_app_wpf.Services.TestModules
{
    /// <summary>
    /// Phase 2 – USB: Enumerates USB host controllers via WMI.
    /// </summary>
    public class UsbTestModule : ITestModule
    {
        public Task<TestRunResult> ExecuteAsync(TestDefinition test, CancellationToken token)
        {
            var result = new TestRunResult();
            try
            {
                var scope = new ManagementScope(@"\\.\root\cimv2");
                scope.Connect();
                using var searcher = new ManagementObjectSearcher(scope, new SelectQuery("SELECT Name FROM Win32_USBController"));
                using var collection = searcher.Get();

                int controllerCount = 0;
                string names = "";
                foreach (ManagementObject mObj in collection)
                {
                    names += $"[{mObj["Name"]?.ToString()?.Trim()}] ";
                    controllerCount++;
                }

                result.MeasuredValue = controllerCount > 0
                    ? $"{controllerCount} controller(s): {names.Trim()}"
                    : "No USB controllers detected";
                result.Outcome = controllerCount >= 1 ? TestOutcome.Pass : TestOutcome.Fail;
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
    /// Phase 2 – Ethernet: Verifies Ethernet adapter link status and negotiated speed.
    /// </summary>
    public class EthernetLinkModule : ITestModule
    {
        public Task<TestRunResult> ExecuteAsync(TestDefinition test, CancellationToken token)
        {
            var result = new TestRunResult();
            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                             || n.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet)
                    .ToList();

                if (nics.Count == 0)
                {
                    result.MeasuredValue = "No Ethernet adapters detected";
                    result.Outcome = TestOutcome.Skipped;
                    result.ErrorDetails = "Hardware not present.";
                    return Task.FromResult(result);
                }

                string details = "";
                bool anyUp = false;
                foreach (var nic in nics)
                {
                    string status = nic.OperationalStatus.ToString();
                    long speedMbps = nic.Speed / 1_000_000;
                    details += $"[{nic.Name}: {status}, {speedMbps} Mbps, MAC={nic.GetPhysicalAddress()}] ";
                    if (nic.OperationalStatus == OperationalStatus.Up) anyUp = true;
                }

                result.MeasuredValue = $"{nics.Count} adapter(s): {details.Trim()}";
                result.Outcome = anyUp ? TestOutcome.Pass : TestOutcome.Fail;
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
    /// Phase 2 – Serial: Enumerates serial COM ports from WMI.
    /// </summary>
    public class SerialPortModule : ITestModule
    {
        public Task<TestRunResult> ExecuteAsync(TestDefinition test, CancellationToken token)
        {
            var result = new TestRunResult();
            try
            {
                var scope = new ManagementScope(@"\\.\root\cimv2");
                scope.Connect();
                var searcher = new ManagementObjectSearcher(scope, new SelectQuery("SELECT DeviceID, Name FROM Win32_SerialPort"));
                var collection = searcher.Get();

                int portCount = 0;
                string portList = "";
                foreach (ManagementObject mObj in collection)
                {
                    string devId = mObj["DeviceID"]?.ToString() ?? "?";
                    string name = mObj["Name"]?.ToString()?.Trim() ?? "Unknown";
                    portList += $"{devId} ";
                    portCount++;
                }
                collection.Dispose(); searcher.Dispose();

                if (portCount == 0)
                {
                    result.MeasuredValue = "No serial COM ports detected";
                    result.Outcome = TestOutcome.Skipped;
                    result.ErrorDetails = "Hardware not present.";
                    return Task.FromResult(result);
                }

                result.MeasuredValue = $"{portCount} port(s): {portList.Trim()}";
                result.Outcome = TestOutcome.Pass;
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
    /// Phase 2 – Display Output: Verifies display adapters and active video controllers.
    /// </summary>
    public class DisplayOutputModule : ITestModule
    {
        public Task<TestRunResult> ExecuteAsync(TestDefinition test, CancellationToken token)
        {
            var result = new TestRunResult();
            try
            {
                var scope = new ManagementScope(@"\\.\root\cimv2");
                scope.Connect();
                using var searcher = new ManagementObjectSearcher(scope, new SelectQuery("SELECT Name, CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate, AdapterRAM FROM Win32_VideoController"));
                using var collection = searcher.Get();

                int count = 0;
                string details = "";
                foreach (ManagementObject mObj in collection)
                {
                    string name = mObj["Name"]?.ToString()?.Trim() ?? "Unknown";
                    string hRes = mObj["CurrentHorizontalResolution"]?.ToString() ?? "?";
                    string vRes = mObj["CurrentVerticalResolution"]?.ToString() ?? "?";
                    string refresh = mObj["CurrentRefreshRate"]?.ToString() ?? "?";
                    ulong vram = 0;
                    ulong.TryParse(mObj["AdapterRAM"]?.ToString(), out vram);
                    double vramMb = vram / 1048576.0;

                    details += $"[{name} {hRes}x{vRes}@{refresh}Hz VRAM={vramMb:F0}MB] ";
                    count++;
                }

                result.MeasuredValue = count > 0 ? $"{count} GPU(s): {details.Trim()}" : "No video controllers detected";
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
}
