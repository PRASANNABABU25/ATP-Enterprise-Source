using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace atp_enterprise_app_wpf.Views
{
    public partial class DashboardView : UserControl
    {
        private const double Circumference = 314.16; // 2 * pi * r (r=50)

        public DashboardView()
        {
            InitializeComponent();
        }

        public void UpdateCpu(double pct)
        {
            double offset = Circumference - (pct / 100.0) * Circumference;
            CpuRing.StrokeDashOffset = Math.Max(0, Math.Min(Circumference, offset));
            CpuText.Text = $"{Math.Round(pct)}%";
        }

        public void UpdateRam(double pct)
        {
            double offset = Circumference - (pct / 100.0) * Circumference;
            RamRing.StrokeDashOffset = Math.Max(0, Math.Min(Circumference, offset));
            RamText.Text = $"{Math.Round(pct)}%";
        }

        public void UpdateTemp(double? temp)
        {
            if (temp.HasValue)
            {
                double val = temp.Value;
                // Max scaling temp = 100C
                double pct = (val / 100.0) * 100;
                double offset = Circumference - (pct / 100.0) * Circumference;
                TempRing.StrokeDashOffset = Math.Max(0, Math.Min(Circumference, offset));
                TempText.Text = $"{Math.Round(val)}°C";

                // Dynamic color based on thermals
                if (val > 75)
                    TempRing.Stroke = new SolidColorBrush(Color.FromRgb(184, 37, 37)); // Red
                else if (val > 60)
                    TempRing.Stroke = new SolidColorBrush(Color.FromRgb(194, 122, 10)); // Yellow
                else
                    TempRing.Stroke = new SolidColorBrush(Color.FromRgb(82, 196, 26)); // Green
            }
            else
            {
                TempRing.StrokeDashOffset = Circumference;
                TempText.Text = "N/A";
            }
        }

        public void UpdateProgress(string testName, int percentage, int elapsedSec, string overallStatus)
        {
            ActiveTestLabel.Text = testName;
            SuiteProgressBar.Value = percentage;
            
            int minutes = elapsedSec / 60;
            int seconds = elapsedSec % 60;
            TimeLabel.Text = $"Elapsed Time: {minutes:D2}:{seconds:D2}";

            // Color status badge
            if (overallStatus == "PASS")
            {
                StatusBadge.Background = new SolidColorBrush(Color.FromRgb(26, 128, 56)); // Solid Green
                StatusBadgeText.Text = "PASS";
                StatusBadgeText.Foreground = new SolidColorBrush(Colors.White);
            }
            else if (overallStatus == "FAIL")
            {
                StatusBadge.Background = new SolidColorBrush(Color.FromRgb(184, 37, 37)); // Solid Red
                StatusBadgeText.Text = "FAIL";
                StatusBadgeText.Foreground = new SolidColorBrush(Colors.White);
            }
            else if (overallStatus == "TESTING")
            {
                StatusBadge.Background = new SolidColorBrush(Color.FromRgb(37, 118, 217)); // Accent Blue
                StatusBadgeText.Text = "RUNNING";
                StatusBadgeText.Foreground = new SolidColorBrush(Colors.White);
            }
            else
            {
                StatusBadge.Background = new SolidColorBrush(Color.FromRgb(28, 33, 48)); // Dark Card
                StatusBadgeText.Text = "AWAITING";
                StatusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(140, 156, 176));
            }
        }
    }
}
