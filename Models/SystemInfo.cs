using System.Collections.Generic;

namespace atp_enterprise_app_wpf.Models
{
    public class SystemInfo
    {
        // 1. System Info
        public string ComputerManufacturer { get; set; } = "Not Available";
        public string ComputerModel { get; set; } = "Not Available";
        public string ProductSpecification { get; set; } = "Not Available";
        public string SystemSerialNumber { get; set; } = "Not Available";
        public string SystemUuid { get; set; } = "Not Available";
        public string ChassisType { get; set; } = "Not Available";
        public string AssetTag { get; set; } = "Not Available";
        public string MachineGuid { get; set; } = "Not Available";

        // 2. BIOS
        public string BiosVendor { get; set; } = "Not Available";
        public string BiosVersion { get; set; } = "Not Available";
        public string BiosReleaseDate { get; set; } = "Not Available";
        public string BiosSerialNumber { get; set; } = "Not Available";
        public string BootMode { get; set; } = "Not Available"; // UEFI / Legacy
        public string SecureBootStatus { get; set; } = "Not Available"; // Enabled / Disabled

        // 3. Motherboard
        public string BoardManufacturer { get; set; } = "Not Available";
        public string BoardProduct { get; set; } = "Not Available";
        public string BoardRevision { get; set; } = "Not Available";
        public string BoardVersion { get; set; } = "Not Available";
        public string BoardSerialNumber { get; set; } = "Not Available";
        public string ChipsetInfo { get; set; } = "Not Available";

        // 4. Processor
        public string CpuModel { get; set; } = "Not Available";
        public string CpuManufacturer { get; set; } = "Not Available";
        public string CpuArchitecture { get; set; } = "Not Available";
        public string CpuSocket { get; set; } = "Not Available";
        public int CpuPhysicalCores { get; set; }
        public int CpuLogicalProcessors { get; set; }
        public double CpuBaseFrequencyGhz { get; set; }
        public double CpuMaxFrequencyGhz { get; set; }
        public string L2CacheSize { get; set; } = "Not Available";
        public string L3CacheSize { get; set; } = "Not Available";
        public string VirtualizationSupport { get; set; } = "Not Available";

        // 5. Memory Modules
        public string TotalRamInstalled { get; set; } = "Not Available";
        public List<MemoryModuleDetail> MemoryModules { get; set; } = new();

        // 6. Storage Discovery
        public List<StorageDetail> Disks { get; set; } = new();

        // 7. Graphics Discovery
        public List<GpuDetail> Gpus { get; set; } = new();

        // 8. Network Discovery
        public List<NetworkDetail> NetworkAdapters { get; set; } = new();

        // 9. USB Discovery
        public List<UsbControllerDetail> UsbControllers { get; set; } = new();
        public List<UsbHubDetail> UsbDevices { get; set; } = new();

        // 10. Serial Communication Discovery
        public List<SerialPortDetail> SerialPorts { get; set; } = new();

        // 11. PCI/PCIe Device Discovery
        public List<PciDeviceDetail> PciDevices { get; set; } = new();

        // 12. Display Discovery
        public List<MonitorDetail> Monitors { get; set; } = new();

        // Compatibility Matrix
        public CompatibilityMatrix Compatibility { get; set; } = new();

        // Backward compatibility mappings for older views and services
        public string CpuName => CpuModel;
        public string RamInstalled => TotalRamInstalled;
        public string OsCaption => "Windows Native (Direct OS API)";
        public string OsVersion => "Direct WMI Subsystem";
        public string Hostname => System.Environment.MachineName;
        public string MotherboardManufacturer => BoardManufacturer;
        public string MotherboardProduct => BoardProduct;
        public string MotherboardSerialNumber => BoardSerialNumber;
        public string BiosManufacturer => BiosVendor;
    }

    public class MemoryModuleDetail
    {
        public string SlotIdentifier { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string PartNumber { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string CapacityGb { get; set; } = string.Empty;
        public string ConfiguredSpeed { get; set; } = string.Empty;
        public string FormFactor { get; set; } = string.Empty;
        public string MemoryTypeStr { get; set; } = string.Empty;
        public string EccStatus { get; set; } = "Not Supported";
    }

    public class StorageDetail
    {
        public string DeviceId { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string FirmwareVersion { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string CapacityGb { get; set; } = string.Empty;
        public string InterfaceType { get; set; } = string.Empty; // NVMe, SATA, USB
        public string PartitionList { get; set; } = string.Empty;
        public string FileSystemType { get; set; } = string.Empty;
        public string SmartSupport { get; set; } = "Exposed";
        public string HealthStatus { get; set; } = "Healthy";
        public string TempSensorSupport { get; set; } = "Sensor Supported";
    }

    public class GpuDetail
    {
        public string Model { get; set; } = string.Empty;
        public string DriverVersion { get; set; } = string.Empty;
        public string DriverDate { get; set; } = string.Empty;
        public string Vendor { get; set; } = string.Empty;
        public string DedicatedMemoryMb { get; set; } = string.Empty;
    }

    public class NetworkDetail
    {
        public string AdapterName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string InterfaceType { get; set; } = string.Empty;
        public string DriverVersion { get; set; } = string.Empty;
        public string DriverDate { get; set; } = string.Empty;
        public string LinkStatus { get; set; } = string.Empty;
        public string NegotiatedSpeed { get; set; } = string.Empty;
        public string DuplexMode { get; set; } = "Full Duplex";
        public string PciBusLocation { get; set; } = "Bus 0";
        public bool IsTenG { get; set; }

        public string InterfaceDescription => Description;
        public string LinkSpeed => NegotiatedSpeed;
        public string Status => LinkStatus == "Up" ? "Active" : "Down";
    }

    public class UsbControllerDetail
    {
        public string Name { get; set; } = string.Empty;
        public string DeviceID { get; set; } = string.Empty;
    }

    public class UsbHubDetail
    {
        public string FriendlyName { get; set; } = string.Empty;
        public string DeviceClass { get; set; } = string.Empty;
        public string VendorID { get; set; } = string.Empty;
        public string ProductID { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
    }

    public class SerialPortDetail
    {
        public string PortName { get; set; } = string.Empty;
        public string DeviceDescription { get; set; } = string.Empty;
        public string DriverInfo { get; set; } = string.Empty;
    }

    public class PciDeviceDetail
    {
        public string DeviceName { get; set; } = string.Empty;
        public string Vendor { get; set; } = string.Empty;
        public string DeviceID { get; set; } = string.Empty;
        public string DriverName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class MonitorDetail
    {
        public string DeviceId { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
        public string RefreshRate { get; set; } = string.Empty;
        public string Orientation { get; set; } = "Horizontal";
        public string ConnectionType { get; set; } = "DisplayPort";
    }

    public class CompatibilityMatrix
    {
        public bool HasBatteryBackup { get; set; }
        public bool HasGpsReceiver { get; set; }
        public bool HasTenGigOptical { get; set; }
        public int DetectedLanPorts { get; set; }
        public bool HasOpticalLoopbackCapable { get; set; }
    }
}
