using System;
using System.Windows;
using System.Windows.Controls;

namespace atp_enterprise_app_wpf.Views
{
    public partial class SettingsView : UserControl
    {
        public double TempLimitMax { get; set; } = 75;
        public double DiskReadLimitMin { get; set; } = 150;
        public double DiskWriteLimitMin { get; set; } = 80;

        // Dynamic health bounds
        public double CpuLimitWarn { get; set; } = 80;
        public double CpuLimitCritical { get; set; } = 95;
        public double RamLimitWarn { get; set; } = 85;
        public double RamLimitCritical { get; set; } = 95;
        public int RefreshIntervalMs { get; set; } = 1000;
        public int HistoryLength { get; set; } = 60;

        public Action? OnSettingsSaved { get; set; }

        public SettingsView()
        {
            InitializeComponent();
        }

        private void SaveLimits_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(TxtTempMax.Text, out double temp) &&
                double.TryParse(TxtReadMin.Text, out double rMin) &&
                double.TryParse(TxtWriteMin.Text, out double wMin) &&
                double.TryParse(TxtCpuWarn.Text, out double cpuW) &&
                double.TryParse(TxtCpuCritical.Text, out double cpuC) &&
                double.TryParse(TxtRamWarn.Text, out double ramW) &&
                double.TryParse(TxtRamCritical.Text, out double ramC) &&
                int.TryParse(TxtRefreshInterval.Text, out int interval) &&
                int.TryParse(TxtHistorySize.Text, out int hSize))
            {
                TempLimitMax = temp;
                DiskReadLimitMin = rMin;
                DiskWriteLimitMin = wMin;
                CpuLimitWarn = cpuW;
                CpuLimitCritical = cpuC;
                RamLimitWarn = ramW;
                RamLimitCritical = ramC;
                RefreshIntervalMs = interval;
                HistoryLength = hSize;

                OnSettingsSaved?.Invoke();
                MessageBox.Show("ATP threshold parameter limits updated successfully!", "Limits Calibration");
            }
            else
            {
                MessageBox.Show("Invalid input values. Please enter valid numeric coefficients.", "Calibration Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
