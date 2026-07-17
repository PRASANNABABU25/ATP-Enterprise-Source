using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using atp_enterprise_app_wpf.Models;
using atp_enterprise_app_wpf.Services;

namespace atp_enterprise_app_wpf.Views
{
    public partial class SystemInfoView : UserControl
    {
        private readonly WmiMonitorService _wmiService = new();
        public event Action<SystemInfo>? HardwareScanCompleted;

        public SystemInfoView()
        {
            InitializeComponent();
        }

        private async void ScanHardware_Click(object sender, RoutedEventArgs e)
        {
            BtnScan.IsEnabled = false;
            TxtProgressStatus.Text = "Initializing Discovery Service...";
            ProgressScanBar.Value = 0;
            TxtPercent.Text = "0%";

            SystemInfo scannedInfo = null;

            try
            {
                await Task.Run(() =>
                {
                    scannedInfo = _wmiService.RunFullHardwareDiscovery((subsystem, pct) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            TxtProgressStatus.Text = subsystem;
                            ProgressScanBar.Value = pct;
                            TxtPercent.Text = $"{pct:F0}%";
                        });
                    });
                });
            }
            catch (Exception ex)
            {
                TxtProgressStatus.Text = $"Scan failed: {ex.Message}";
                BtnScan.IsEnabled = true;
                return;
            }

            if (scannedInfo != null)
            {
                PopulateUI(scannedInfo);
                HardwareScanCompleted?.Invoke(scannedInfo);
            }

            BtnScan.IsEnabled = true;
        }

        public void PopulateUI(SystemInfo info)
        {
            if (info == null) return;

            // 1. System Info
            SysManufacturer.Text = info.ComputerManufacturer;
            SysModel.Text = info.ComputerModel;
            SysProduct.Text = info.ProductSpecification;
            SysSerial.Text = info.SystemSerialNumber;
            SysChassis.Text = info.ChassisType;
            SysUuid.Text = info.SystemUuid;
            SysAsset.Text = info.AssetTag;
            SysGuid.Text = info.MachineGuid;

            // 2. BIOS
            BiosVendor.Text = info.BiosVendor;
            BiosVersion.Text = info.BiosVersion;
            BiosDate.Text = info.BiosReleaseDate;
            BiosSerial.Text = info.BiosSerialNumber;
            BiosBoot.Text = info.BootMode;
            BiosSecure.Text = info.SecureBootStatus;

            // 3. Motherboard
            MoboVendor.Text = info.BoardManufacturer;
            MoboProduct.Text = info.BoardProduct;
            MoboSerial.Text = info.BoardSerialNumber;
            MoboChipset.Text = info.ChipsetInfo;

            // 4. Processor
            CpuModel.Text = info.CpuModel;
            CpuArch.Text = info.CpuArchitecture;
            CpuCores.Text = $"{info.CpuPhysicalCores} Physical Cores";
            CpuThreads.Text = $"{info.CpuLogicalProcessors} Logical Threads";
            CpuClock.Text = $"{info.CpuBaseFrequencyGhz} GHz Base";
            CpuCache.Text = info.L3CacheSize;

            // 5. Memory Slots
            TxtTotalMemory.Text = $"Total Memory Modules Installed: {info.TotalRamInstalled}";
            GridMemory.ItemsSource = info.MemoryModules;

            // 6. Storage
            GridStorage.ItemsSource = info.Disks;

            // 7. Graphics
            GridGpu.ItemsSource = info.Gpus;

            // 8. Network
            GridNics.ItemsSource = info.NetworkAdapters;

            // 9. USB
            GridUsb.ItemsSource = info.UsbDevices;

            // 10. Serial Ports
            GridSerial.ItemsSource = info.SerialPorts;

            // 11. PCI Devices
            GridPci.ItemsSource = info.PciDevices;

            // 12. Displays
            GridMonitors.ItemsSource = info.Monitors;

            // Update Compatibility Matrix Badges
            var brushGreen = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#237804"));
            var brushRed = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#591d1d"));
            var brushWhite = new SolidColorBrush(Colors.White);

            // Battery
            if (info.Compatibility.HasBatteryBackup)
            {
                MatrixBattery.Background = brushGreen;
                TxtMatrixBattery.Text = "UNLOCKED / DETECTED";
                TxtMatrixBattery.Foreground = brushWhite;
            }
            else
            {
                MatrixBattery.Background = brushRed;
                TxtMatrixBattery.Text = "LOCKED / NO BATTERY";
                TxtMatrixBattery.Foreground = brushWhite;
            }

            // GPS
            if (info.Compatibility.HasGpsReceiver)
            {
                MatrixGps.Background = brushGreen;
                TxtMatrixGps.Text = "UNLOCKED / DETECTED";
                TxtMatrixGps.Foreground = brushWhite;
            }
            else
            {
                MatrixGps.Background = brushRed;
                TxtMatrixGps.Text = "LOCKED / NO GPS DETECTED";
                TxtMatrixGps.Foreground = brushWhite;
            }

            // 10G Optical
            if (info.Compatibility.HasTenGigOptical)
            {
                MatrixOptical.Background = brushGreen;
                TxtMatrixOptical.Text = "UNLOCKED / DETECTED";
                TxtMatrixOptical.Foreground = brushWhite;
            }
            else
            {
                MatrixOptical.Background = brushRed;
                TxtMatrixOptical.Text = "LOCKED / NO 10G NIC";
                TxtMatrixOptical.Foreground = brushWhite;
            }

            // LAN Interfaces detected count
            TxtMatrixLan.Text = $"{info.Compatibility.DetectedLanPorts} PORTS DETECTED";
        }
    }
}
