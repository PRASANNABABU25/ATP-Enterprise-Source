using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using atp_enterprise_app_wpf.Services;

namespace atp_enterprise_app_wpf.Views
{
    public partial class StorageTestsView : UserControl
    {
        private readonly DiskSpeedTestService _speedService = new();

        public StorageTestsView()
        {
            InitializeComponent();
            TxtTargetPath.Text = $"Target directory: {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "Temp")}";
        }

        private async void RunBenchmark_Click(object sender, RoutedEventArgs e)
        {
            // Disable button during test
            var btn = sender as Button;
            if (btn != null) btn.IsEnabled = false;

            TxtWriteSpeed.Text = "Testing Write...";
            TxtReadSpeed.Text = "Testing Read...";
            TxtIntegrity.Text = "Verifying...";
            TxtIntegrity.Foreground = new SolidColorBrush(Color.FromRgb(140, 156, 176));

            WriteBar.Width = 0;
            ReadBar.Width = 0;

            string targetFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "Temp");

            // Execute benchmark in background thread to keep UI alive
            var result = await Task.Run(() => _speedService.RunBenchmark(targetFolder));

            TxtWriteSpeed.Text = $"{result.WriteSpeedMBs:F2} MB/s";
            TxtReadSpeed.Text = $"{result.ReadSpeedMBs:F2} MB/s";

            if (result.IntegrityChecked)
            {
                TxtIntegrity.Text = "VERIFIED PASS (Adler-32 Match)";
                TxtIntegrity.Foreground = new SolidColorBrush(Color.FromRgb(82, 196, 26)); // Green
            }
            else
            {
                TxtIntegrity.Text = "INTEGRITY ERROR (Data Mismatch)";
                TxtIntegrity.Foreground = new SolidColorBrush(Color.FromRgb(184, 37, 37)); // Red
            }

            // Animate progress widths
            double parentWidth = 200;
            if (WriteBar.Parent is FrameworkElement borderParent)
            {
                parentWidth = borderParent.ActualWidth - 4; // Subtract padding
            }

            double scaleMax = 1200.0; // scale up to 1.2 GB/s NVMe speeds
            double targetWriteWidth = parentWidth * Math.Min(result.WriteSpeedMBs / scaleMax, 1.0);
            double targetReadWidth = parentWidth * Math.Min(result.ReadSpeedMBs / scaleMax, 1.0);

            // XAML Double Animations on Border.Width
            var writeAnim = new DoubleAnimation(0, targetWriteWidth, TimeSpan.FromMilliseconds(600))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var readAnim = new DoubleAnimation(0, targetReadWidth, TimeSpan.FromMilliseconds(600))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            WriteBar.BeginAnimation(WidthProperty, writeAnim);
            ReadBar.BeginAnimation(WidthProperty, readAnim);

            if (btn != null) btn.IsEnabled = true;
        }
    }
}
