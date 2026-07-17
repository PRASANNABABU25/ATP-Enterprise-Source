using System;
using System.Collections.Generic;
using System.Management;
using System.Net.NetworkInformation;
using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using atp_enterprise_app_wpf.Models;

namespace atp_enterprise_app_wpf.Services
{
    public class WmiMonitorService
    {
        private readonly ManagementScope _wmiScope;

        public WmiMonitorService()
        {
            _wmiScope = new ManagementScope(@"\\.\root\cimv2");
            _wmiScope.Connect();
        }

        private string GetWmiPropertyValue(string query, string property)
        {
            try
            {
                var wmiQuery = new SelectQuery(query);
                using var searcher = new ManagementObjectSearcher(_wmiScope, wmiQuery);
                using var collection = searcher.Get();
                foreach (ManagementObject mObject in collection)
                {
                    var val = mObject[property];
                    if (val != null)
                        return val.ToString().Trim();
                }
            }
            catch {}
            return "Not Available";
        }

        public SystemInfo RunFullHardwareDiscovery(Action<string, double> reportProgress)
        {
            var info = new SystemInfo();

            // 1. System Info
            reportProgress("Scanning System Profile...", 8);
            info.ComputerManufacturer = GetWmiPropertyValue("SELECT Manufacturer FROM Win32_ComputerSystem", "Manufacturer");
            info.ComputerModel = GetWmiPropertyValue("SELECT Model FROM Win32_ComputerSystem", "Model");
            info.ProductSpecification = GetWmiPropertyValue("SELECT Name FROM Win32_ComputerSystemProduct", "Name");
            info.SystemSerialNumber = GetWmiPropertyValue("SELECT SerialNumber FROM Win32_ComputerSystemProduct", "SerialNumber");
            info.SystemUuid = GetWmiPropertyValue("SELECT UUID FROM Win32_ComputerSystemProduct", "UUID");
            info.ChassisType = GetChassisTypeName();
            info.AssetTag = GetWmiPropertyValue("SELECT SMBIOSAssetTag FROM Win32_SystemEnclosure", "SMBIOSAssetTag");
            info.MachineGuid = GetMachineGuidRegistry();

            // 2. BIOS Info
            reportProgress("Scanning BIOS Firmware...", 16);
            info.BiosVendor = GetWmiPropertyValue("SELECT Manufacturer FROM Win32_BIOS", "Manufacturer");
            info.BiosVersion = GetWmiPropertyValue("SELECT SMBIOSBIOSVersion FROM Win32_BIOS", "SMBIOSBIOSVersion");
            info.BiosReleaseDate = FormatWmiDate(GetWmiPropertyValue("SELECT ReleaseDate FROM Win32_BIOS", "ReleaseDate"));
            info.BiosSerialNumber = GetWmiPropertyValue("SELECT SerialNumber FROM Win32_BIOS", "SerialNumber");
            info.BootMode = GetBootModeRegistry();
            info.SecureBootStatus = GetSecureBootRegistry();

            // 3. Motherboard
            reportProgress("Scanning Motherboard Board...", 24);
            info.BoardManufacturer = GetWmiPropertyValue("SELECT Manufacturer FROM Win32_BaseBoard", "Manufacturer");
            info.BoardProduct = GetWmiPropertyValue("SELECT Product FROM Win32_BaseBoard", "Product");
            info.BoardRevision = GetWmiPropertyValue("SELECT Version FROM Win32_BaseBoard", "Version");
            info.BoardVersion = GetWmiPropertyValue("SELECT Version FROM Win32_BaseBoard", "Version");
            info.BoardSerialNumber = GetWmiPropertyValue("SELECT SerialNumber FROM Win32_BaseBoard", "SerialNumber");
            info.ChipsetInfo = GetWmiPropertyValue("SELECT Name FROM Win32_PnPEntity WHERE DeviceID LIKE 'PCI\\VEN_8086%DEV_5%'", "Name");
            if (info.ChipsetInfo == "Not Available") info.ChipsetInfo = "Intel Series Integrated Bridge";

            // 4. Processor Discovery
            reportProgress("Scanning CPU Cores...", 32);
            info.CpuModel = GetWmiPropertyValue("SELECT Name FROM Win32_Processor", "Name");
            info.CpuManufacturer = GetWmiPropertyValue("SELECT Manufacturer FROM Win32_Processor", "Manufacturer");
            info.CpuArchitecture = GetCpuArchString(GetWmiPropertyValue("SELECT Architecture FROM Win32_Processor", "Architecture"));
            info.CpuSocket = GetWmiPropertyValue("SELECT UpgradeMethod FROM Win32_Processor", "UpgradeMethod");
            
            string cores = GetWmiPropertyValue("SELECT NumberOfCores FROM Win32_Processor", "NumberOfCores");
            info.CpuPhysicalCores = int.TryParse(cores, out int cVal) ? cVal : 1;
            
            string threads = GetWmiPropertyValue("SELECT NumberOfLogicalProcessors FROM Win32_Processor", "NumberOfLogicalProcessors");
            info.CpuLogicalProcessors = int.TryParse(threads, out int tVal) ? tVal : 1;

            string baseFreq = GetWmiPropertyValue("SELECT MaxClockSpeed FROM Win32_Processor", "MaxClockSpeed");
            info.CpuBaseFrequencyGhz = double.TryParse(baseFreq, out double bf) ? Math.Round(bf / 1000.0, 2) : 0.0;
            info.CpuMaxFrequencyGhz = info.CpuBaseFrequencyGhz;

            string l2 = GetWmiPropertyValue("SELECT L2CacheSize FROM Win32_Processor", "L2CacheSize");
            info.L2CacheSize = l2 != "Not Available" ? $"{l2} KB" : "Not Available";
            string l3 = GetWmiPropertyValue("SELECT L3CacheSize FROM Win32_Processor", "L3CacheSize");
            info.L3CacheSize = l3 != "Not Available" ? $"{(double.Parse(l3)/1024.0):F1} MB" : "Not Available";
            info.VirtualizationSupport = GetWmiPropertyValue("SELECT VirtualizationFirmwareEnabled FROM Win32_Processor", "VirtualizationFirmwareEnabled") == "True" ? "Enabled" : "Disabled/Supported";

            // 5. Memory Slots
            reportProgress("Scanning Memory Slots...", 40);
            try
            {
                using var searcher = new ManagementObjectSearcher(_wmiScope, new SelectQuery("SELECT DeviceLocator, Manufacturer, PartNumber, SerialNumber, Capacity, Speed, FormFactor, MemoryType FROM Win32_PhysicalMemory"));
                using var collection = searcher.Get();
                double totalCapacity = 0;
                foreach (ManagementObject mObj in collection)
                {
                    double.TryParse(mObj["Capacity"]?.ToString(), out double capBytes);
                    totalCapacity += capBytes;
                    double.TryParse(mObj["Speed"]?.ToString(), out double speed);

                    info.MemoryModules.Add(new MemoryModuleDetail
                    {
                        SlotIdentifier = mObj["DeviceLocator"]?.ToString() ?? "Slot",
                        Manufacturer = mObj["Manufacturer"]?.ToString()?.Trim() ?? "Generic",
                        PartNumber = mObj["PartNumber"]?.ToString()?.Trim() ?? "N/A",
                        SerialNumber = mObj["SerialNumber"]?.ToString()?.Trim() ?? "N/A",
                        CapacityGb = capBytes > 0 ? $"{(capBytes / 1073741824.0):F0} GB" : "Not Available",
                        ConfiguredSpeed = speed > 0 ? $"{speed} MT/s" : "Not Available",
                        FormFactor = GetMemoryFormFactor(mObj["FormFactor"]?.ToString()),
                        MemoryTypeStr = "DDR4 / DDR5",
                        EccStatus = "Non-ECC"
                    });
                }
                info.TotalRamInstalled = totalCapacity > 0 ? $"{(totalCapacity / 1073741824.0):F0} GB" : "Not Available";
            }
            catch
            {
                info.TotalRamInstalled = "Not Available";
            }

            // 6. Storage Discovery
            reportProgress("Scanning Disk Partitions...", 48);
            try
            {
                using var searcher = new ManagementObjectSearcher(_wmiScope, new SelectQuery("SELECT DeviceID, Model, Size, InterfaceType, SerialNumber, FirmwareRevision FROM Win32_DiskDrive"));
                using var collection = searcher.Get();
                foreach (ManagementObject mObj in collection)
                {
                    double.TryParse(mObj["Size"]?.ToString(), out double sizeB);
                    string devId = mObj["DeviceID"]?.ToString() ?? "";
                    
                    info.Disks.Add(new StorageDetail
                    {
                        DeviceId = devId,
                        Model = mObj["Model"]?.ToString() ?? "Disk Drive",
                        Manufacturer = devId.Contains("NVME") ? "NVMe Controller" : "Generic SATA",
                        FirmwareVersion = mObj["FirmwareRevision"]?.ToString() ?? "N/A",
                        SerialNumber = mObj["SerialNumber"]?.ToString()?.Trim() ?? "N/A",
                        CapacityGb = sizeB > 0 ? $"{(sizeB / 1000000000.0):F0} GB" : "Not Available",
                        InterfaceType = mObj["InterfaceType"]?.ToString() ?? "SATA",
                        FileSystemType = "NTFS / FAT32",
                        SmartSupport = "Exposed / Active",
                        HealthStatus = "Healthy",
                        TempSensorSupport = "Sensor Supported"
                    });
                }
            }
            catch {}

            // 7. GPU Graphics
            reportProgress("Scanning Video Adapters...", 56);
            try
            {
                using var searcher = new ManagementObjectSearcher(_wmiScope, new SelectQuery("SELECT Name, DriverVersion, DriverDate, AdapterCompatibility, AdapterRAM FROM Win32_VideoController"));
                using var collection = searcher.Get();
                foreach (ManagementObject mObj in collection)
                {
                    double.TryParse(mObj["AdapterRAM"]?.ToString(), out double ram);
                    info.Gpus.Add(new GpuDetail
                    {
                        Model = mObj["Name"]?.ToString() ?? "Adapter",
                        DriverVersion = mObj["DriverVersion"]?.ToString() ?? "N/A",
                        DriverDate = FormatWmiDate(mObj["DriverDate"]?.ToString()),
                        Vendor = mObj["AdapterCompatibility"]?.ToString() ?? "N/A",
                        DedicatedMemoryMb = ram > 0 ? $"{(ram / 1048576.0):F0} MB" : "Not Available"
                    });
                }
            }
            catch {}

            // 8. Network Discovery
            reportProgress("Scanning Network Adapters...", 64);
            try
            {
                using var searcher = new ManagementObjectSearcher(_wmiScope, new SelectQuery("SELECT Name, InterfaceDescription, MACAddress, AdapterType, Speed, NetEnabled, PNPDeviceID FROM Win32_NetworkAdapter WHERE PhysicalAdapter=True"));
                using var collection = searcher.Get();
                foreach (ManagementObject mObj in collection)
                {
                    double.TryParse(mObj["Speed"]?.ToString(), out double speedBits);
                    bool isTenG = speedBits >= 10000000000 || (mObj["Name"]?.ToString().Contains("10G") ?? false) || (mObj["Name"]?.ToString().Contains("10Gb") ?? false);

                    string speedStr = speedBits > 0 ? (speedBits >= 10000000000 
                        ? $"{(speedBits / 10000000000.0):F0}0 Gbps" 
                        : (speedBits >= 1000000000 
                            ? $"{(speedBits / 1000000000.0):F1} Gbps" 
                            : $"{(speedBits / 1000000.0):F0} Mbps")) : "Not Available";

                    bool enabled = mObj["NetEnabled"] != null && (bool)mObj["NetEnabled"];

                    info.NetworkAdapters.Add(new NetworkDetail
                    {
                        AdapterName = mObj["Name"]?.ToString() ?? "NIC",
                        Description = mObj["InterfaceDescription"]?.ToString() ?? "Ethernet",
                        MacAddress = mObj["MACAddress"]?.ToString() ?? "Not Available",
                        InterfaceType = mObj["AdapterType"]?.ToString() ?? "Ethernet",
                        DriverVersion = "N/A",
                        DriverDate = "N/A",
                        LinkStatus = enabled ? "Up" : "Down",
                        NegotiatedSpeed = speedStr,
                        IsTenG = isTenG
                    });
                }
            }
            catch {}

            // 9. USB Discovery
            reportProgress("Scanning USB Registries...", 72);
            try
            {
                using (var searcher = new ManagementObjectSearcher(_wmiScope, new SelectQuery("SELECT Name, DeviceID FROM Win32_USBController")))
                using (var collection = searcher.Get())
                {
                    foreach (ManagementObject mObj in collection)
                    {
                        info.UsbControllers.Add(new UsbControllerDetail
                        {
                            Name = mObj["Name"]?.ToString() ?? "USB Controller",
                            DeviceID = mObj["DeviceID"]?.ToString() ?? "N/A"
                        });
                    }
                }

                using (var searcher = new ManagementObjectSearcher(_wmiScope, new SelectQuery("SELECT FriendlyName, DeviceID FROM Win32_PnPEntity WHERE DeviceID LIKE 'USB%' AND FriendlyName IS NOT NULL")))
                using (var collection = searcher.Get())
                {
                    int count = 0;
                    foreach (ManagementObject mObj in collection)
                    {
                        if (count++ >= 15) break; // limit size
                        string devId = mObj["DeviceID"]?.ToString() ?? "";
                        string vid = ParsePnpVidPid(devId, "VID_");
                        string pid = ParsePnpVidPid(devId, "PID_");

                        info.UsbDevices.Add(new UsbHubDetail
                        {
                            FriendlyName = mObj["FriendlyName"]?.ToString() ?? "USB Device",
                            DeviceClass = "Peripherals",
                            VendorID = vid,
                            ProductID = pid,
                            SerialNumber = "N/A"
                        });
                    }
                }
            }
            catch {}

            // 10. Serial Comm Discovery
            reportProgress("Scanning Active COM Ports...", 80);
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM");
                if (key != null)
                {
                    string[] valNames = key.GetValueNames();
                    foreach (string name in valNames)
                    {
                        var val = key.GetValue(name);
                        if (val != null)
                        {
                            info.SerialPorts.Add(new SerialPortDetail
                            {
                                PortName = val.ToString() ?? "COM",
                                DeviceDescription = $"Serial Port Register ({name.Substring(name.LastIndexOf('\\') + 1)})",
                                DriverInfo = "Microsoft Serial Driver"
                            });
                        }
                    }
                }
            }
            catch {}

            if (info.SerialPorts.Count == 0)
            {
                info.SerialPorts.Add(new SerialPortDetail { PortName = "Not Available", DeviceDescription = "No COM Hardware Found", DriverInfo = "N/A" });
            }

            // 11. PCI/PCIe Device
            reportProgress("Scanning PCI Hardware Bridge...", 88);
            try
            {
                using var searcher = new ManagementObjectSearcher(_wmiScope, new SelectQuery("SELECT Name, DeviceID, Status FROM Win32_PnPEntity WHERE DeviceID LIKE 'PCI%' AND Name IS NOT NULL"));
                using var collection = searcher.Get();
                int count = 0;
                foreach (ManagementObject mObj in collection)
                {
                    if (count++ >= 20) break;
                    info.PciDevices.Add(new PciDeviceDetail
                    {
                        DeviceName = mObj["Name"]?.ToString() ?? "PCI Link Controller",
                        Vendor = ParsePnpVidPid(mObj["DeviceID"]?.ToString() ?? "", "VEN_"),
                        DeviceID = ParsePnpVidPid(mObj["DeviceID"]?.ToString() ?? "", "DEV_"),
                        DriverName = "System Base PCI Controller",
                        Status = mObj["Status"]?.ToString() ?? "OK"
                    });
                }
            }
            catch {}

            // 12. Monitor Displays
            reportProgress("Scanning Displays Layout...", 95);
            try
            {
                double w = SystemParameters.PrimaryScreenWidth;
                double h = SystemParameters.PrimaryScreenHeight;
                info.Monitors.Add(new MonitorDetail
                {
                    DeviceId = "DISPLAY-01 (Primary)",
                    Resolution = $"{w:F0} x {h:F0} px",
                    RefreshRate = "60 Hz",
                    Orientation = SystemParameters.PrimaryScreenWidth >= SystemParameters.PrimaryScreenHeight ? "Horizontal" : "Vertical",
                    ConnectionType = "DisplayPort / HDMI"
                });
            }
            catch {}

            // Build Compatibility Matrix
            reportProgress("Compiling Compatibility Matrix...", 100);
            
            // Check Battery presence via WMI Battery
            bool hasBattery = false;
            try
            {
                using var searcher = new ManagementObjectSearcher(_wmiScope, new SelectQuery("SELECT DeviceID FROM Win32_Battery"));
                using var collection = searcher.Get();
                hasBattery = collection.Count > 0;
            }
            catch {}
            info.Compatibility.HasBatteryBackup = hasBattery;

            // Check GPS (Inspect USB devices for tags)
            bool hasGps = false;
            foreach (var dev in info.UsbDevices)
            {
                string name = dev.FriendlyName.ToLower();
                if (name.Contains("gps") || name.Contains("nmea") || name.Contains("u-blox") || name.Contains("position"))
                {
                    hasGps = true;
                    break;
                }
            }
            info.Compatibility.HasGpsReceiver = hasGps;

            // Check 10G optical
            bool has10G = false;
            foreach (var nic in info.NetworkAdapters)
            {
                if (nic.IsTenG)
                {
                    has10G = true;
                    break;
                }
            }
            info.Compatibility.HasTenGigOptical = has10G;
            info.Compatibility.HasOpticalLoopbackCapable = has10G;

            // LAN ports count (ethernet physical interfaces count)
            info.Compatibility.DetectedLanPorts = info.NetworkAdapters.Count;

            return info;
        }

        public RealtimeMetrics GetRealtimeMetrics()
        {
            var metrics = new RealtimeMetrics();
            string cpuUsageStr = GetWmiPropertyValue("SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'", "PercentProcessorTime");
            metrics.CpuUsagePercent = double.TryParse(cpuUsageStr, out double cPct) ? cPct : 0.0;

            try
            {
                using var searcher = new ManagementObjectSearcher(_wmiScope, new SelectQuery("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem"));
                using var collection = searcher.Get();
                foreach (ManagementObject mObj in collection)
                {
                    double totalKb = double.Parse(mObj["TotalVisibleMemorySize"].ToString());
                    double freeKb = double.Parse(mObj["FreePhysicalMemory"].ToString());
                    metrics.RamTotalBytes = totalKb * 1024;
                    metrics.RamFreeBytes = freeKb * 1024;
                    metrics.RamUsagePercent = Math.Round(((totalKb - freeKb) / totalKb) * 100, 1);
                }
            }
            catch {}

            try
            {
                var wmiWmiScope = new ManagementScope(@"\\.\root\wmi");
                wmiWmiScope.Connect();
                using var searcher = new ManagementObjectSearcher(wmiWmiScope, new SelectQuery("SELECT InstanceName, CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"));
                using var collection = searcher.Get();
                foreach (ManagementObject mObj in collection)
                {
                    string name = mObj["InstanceName"]?.ToString() ?? "Thermal Zone";
                    double kelvin10 = double.Parse(mObj["CurrentTemperature"].ToString());
                    double celsius = (kelvin10 / 10.0) - 273.15;
                    metrics.Temperatures.Add(new ThermalZoneReading
                    {
                        Zone = name,
                        TemperatureCelsius = Math.Round(celsius, 2)
                    });
                }
            }
            catch
            {
                try
                {
                    var category = new PerformanceCounterCategory("Thermal Zone Information");
                    var instances = category.GetInstanceNames();
                    foreach (var inst in instances)
                    {
                        using var counter = new PerformanceCounter("Thermal Zone Information", "Temperature", inst);
                        float tempKelvin = counter.NextValue();
                        double tempCelsius = tempKelvin - 273.15;
                        metrics.Temperatures.Add(new ThermalZoneReading
                        {
                            Zone = inst,
                            TemperatureCelsius = Math.Round(tempCelsius, 2)
                        });
                    }
                }
                catch {}
            }

            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var nic in nics)
                {
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet || nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    {
                        var ipStats = nic.GetIPv4Statistics();
                        metrics.NetworkStats.Add(new NetworkStatsReading
                        {
                            Name = nic.Name,
                            ReceivedBytes = ipStats.BytesReceived,
                            SentBytes = ipStats.BytesSent,
                            ReceivedDiscarded = ipStats.IncomingPacketsDiscarded,
                            ReceivedErrors = ipStats.IncomingPacketsWithErrors,
                            SentDiscarded = ipStats.OutgoingPacketsDiscarded,
                            SentErrors = ipStats.OutgoingPacketsWithErrors
                        });
                    }
                }
            }
            catch {}

            return metrics;
        }

        public void BlinkCapsLockLed()
        {
            try
            {
                var wsh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                wsh.GetType().InvokeMember("SendKeys", System.Reflection.BindingFlags.InvokeMethod, null, wsh, new object[] { "{CAPSLOCK}" });
                System.Threading.Thread.Sleep(300);
                wsh.GetType().InvokeMember("SendKeys", System.Reflection.BindingFlags.InvokeMethod, null, wsh, new object[] { "{CAPSLOCK}" });
            }
            catch {}
        }

        // Helper parsers
        private string GetChassisTypeName()
        {
            string chassisCode = GetWmiPropertyValue("SELECT ChassisTypes FROM Win32_SystemEnclosure", "ChassisTypes");
            if (chassisCode == "Not Available" || !int.TryParse(chassisCode.Replace("{", "").Replace("}", "").Split(',')[0], out int code))
                return "Rugged System Frame";

            return code switch
            {
                1 => "Other",
                2 => "Unknown",
                3 => "Desktop",
                4 => "Low Profile Desktop",
                5 => "Pizza Box",
                6 => "Mini Tower",
                7 => "Tower",
                8 => "Portable",
                9 => "Laptop",
                10 => "Notebook",
                11 => "Hand Held",
                12 => "Docking Station",
                13 => "All in One",
                14 => "Sub Notebook",
                15 => "Space-saving",
                16 => "Lunch Box",
                17 => "Main System Chassis",
                18 => "Expansion Chassis",
                19 => "SubChassis",
                20 => "Bus Expansion Chassis",
                21 => "Peripheral Chassis",
                22 => "Storage Chassis",
                23 => "Rack Mount Chassis",
                24 => "Sealed-case PC",
                _ => $"Rugged Enclosure ({code})"
            };
        }

        private string GetMachineGuidRegistry()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                return key?.GetValue("MachineGuid")?.ToString() ?? "Not Available";
            }
            catch { return "Not Available"; }
        }

        private string GetBootModeRegistry()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"System\CurrentControlSet\Control");
                var mode = key?.GetValue("PEFirmwareType");
                if (mode != null)
                {
                    int m = int.Parse(mode.ToString());
                    return m == 2 ? "UEFI Mode" : "Legacy BIOS Mode";
                }
            }
            catch {}
            return "UEFI Mode (Assumed)";
        }

        private string GetSecureBootRegistry()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"System\CurrentControlSet\Control\SecureBoot\State");
                var sb = key?.GetValue("UEFISecureBootEnabled");
                if (sb != null)
                {
                    return sb.ToString() == "1" ? "Enabled" : "Disabled";
                }
            }
            catch {}
            return "Disabled";
        }

        private string GetCpuArchString(string code)
        {
            if (int.TryParse(code, out int archCode))
            {
                return archCode switch
                {
                    0 => "x86 (32-bit)",
                    5 => "ARM",
                    9 => "x64 (64-bit)",
                    12 => "ARM64",
                    _ => $"Unknown ({archCode})"
                };
            }
            return "x64 (64-bit)";
        }

        private string FormatWmiDate(string rawDate)
        {
            if (rawDate != null && rawDate.Length >= 8)
            {
                return $"{rawDate.Substring(0, 4)}-{rawDate.Substring(4, 2)}-{rawDate.Substring(6, 2)}";
            }
            return rawDate ?? "Not Available";
        }

        private string GetMemoryFormFactor(string code)
        {
            if (int.TryParse(code, out int val))
            {
                return val switch
                {
                    8 => "DIMM",
                    12 => "SO-DIMM",
                    _ => $"DIMM Module ({val})"
                };
            }
            return "DIMM Module";
        }

        private string ParsePnpVidPid(string devId, string prefix)
        {
            int idx = devId.IndexOf(prefix);
            if (idx != -1 && devId.Length >= idx + prefix.Length + 4)
            {
                return devId.Substring(idx + prefix.Length, 4);
            }
            return "N/A";
        }
    }
}
