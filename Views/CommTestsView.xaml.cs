using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace atp_enterprise_app_wpf.Views
{
    public partial class CommTestsView : UserControl
    {
        public CommTestsView()
        {
            InitializeComponent();
            ScanPorts();
        }

        private void ScanPorts_Click(object sender, RoutedEventArgs e)
        {
            ScanPorts();
            MessageBox.Show("COM Serial Ports hardwares scan completed!", "Comm Diagnostics");
        }

        private void ScanPorts()
        {
            LstComPorts.Items.Clear();
            try
            {
                // Native registry probe for active serial ports (failsafe, no packages required)
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM");
                if (key != null)
                {
                    string[] valNames = key.GetValueNames();
                    foreach (string name in valNames)
                    {
                        var val = key.GetValue(name);
                        if (val != null)
                        {
                            LstComPorts.Items.Add($"{val} (Active - {name.Substring(name.LastIndexOf('\\') + 1)})");
                        }
                    }
                }
            }
            catch
            {
            }

            if (LstComPorts.Items.Count == 0)
            {
                LstComPorts.Items.Add("No Active COM Serial Ports Found");
            }
        }
    }
}
