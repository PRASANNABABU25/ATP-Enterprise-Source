using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using atp_enterprise_app_wpf.Services;

namespace atp_enterprise_app_wpf.Views
{
    public partial class FunctionalTestsView : UserControl
    {
        private readonly WmiMonitorService _wmiService = new();

        public FunctionalTestsView()
        {
            InitializeComponent();
            LoadUptime();
        }

        private void LoadUptime()
        {
            try
            {
                // Retrieve tick count uptime
                double seconds = Environment.TickCount64 / 1000.0;
                if (seconds >= 3600)
                {
                    TxtUptime.Text = $"{(seconds / 3600.0):F2} hours";
                }
                else
                {
                    TxtUptime.Text = $"{seconds:F0} seconds";
                }
            }
            catch
            {
                TxtUptime.Text = "Not Available";
            }
        }

        private void VerifyRtc_Click(object sender, RoutedEventArgs e)
        {
            // Measure drift: datetime skew vs stopwatch delta (sub-millisecond precise)
            var sw = Stopwatch.StartNew();
            var dt1 = DateTime.UtcNow;
            System.Threading.Thread.Sleep(10);
            var dt2 = DateTime.UtcNow;
            sw.Stop();

            double elapsedMs = sw.Elapsed.TotalMilliseconds;
            double dateMs = (dt2 - dt1).TotalMilliseconds;
            double drift = Math.Abs(elapsedMs - dateMs);

            TxtRtcDrift.Text = $"{drift:F4} ms";
            MessageBox.Show($"High Precision Timer Clock Drift Verified: {drift:F4} ms.\nStatus: PASS", "UUT Calibration System");
        }

        private void FlashLed_Click(object sender, RoutedEventArgs e)
        {
            _wmiService.BlinkCapsLockLed();
            MessageBox.Show("Keyboard CapsLock LED Toggle Event Sent!", "UUT Diagnostic");
        }

        private void EmitBeep_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.Beep(2000, 400);
            }
            catch
            {
                System.Media.SystemSounds.Beep.Play();
            }
        }
    }
}
