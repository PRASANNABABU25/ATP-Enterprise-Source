using System;
using System.Windows.Controls;
using System.Windows.Media;
using atp_enterprise_app_wpf.Models;

namespace atp_enterprise_app_wpf.Views
{
    public partial class EthernetTestsView : UserControl
    {
        public SystemInfo? CurrentInfo { get; set; }

        public EthernetTestsView()
        {
            InitializeComponent();
        }

        public void Populate(SystemInfo info, RealtimeMetrics metrics)
        {
            CurrentInfo = info;

            // Load primary active ethernet interface
            NetworkDetail? activeNic = null;
            foreach (var nic in info.NetworkAdapters)
            {
                if (nic.Status == "Active")
                {
                    activeNic = nic;
                    break;
                }
            }

            // Fallback to first if none active
            if (activeNic == null && info.NetworkAdapters.Count > 0)
            {
                activeNic = info.NetworkAdapters[0];
            }

            if (activeNic != null)
            {
                TxtNicName.Text = activeNic.InterfaceDescription;
                TxtMac.Text = activeNic.MacAddress;
                TxtSpeed.Text = activeNic.LinkSpeed;
                TxtNicStatus.Text = activeNic.Status == "Active" ? "LINK ACTIVE" : "LINK DOWN";
                TxtNicStatus.Foreground = activeNic.Status == "Active" 
                    ? new SolidColorBrush(Color.FromRgb(26, 128, 56)) 
                    : new SolidColorBrush(Color.FromRgb(184, 37, 37));
            }
            else
            {
                TxtNicName.Text = "Device Not Connected";
                TxtMac.Text = "Not Available";
                TxtSpeed.Text = "Sensor Not Supported";
                TxtNicStatus.Text = "DEVICE NOT CONNECTED";
                TxtNicStatus.Foreground = new SolidColorBrush(Color.FromRgb(194, 122, 10)); // Yellow warning
            }

            GridEthStats.ItemsSource = metrics.NetworkStats;
        }
    }
}
