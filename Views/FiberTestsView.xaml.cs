using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using atp_enterprise_app_wpf.Services;

namespace atp_enterprise_app_wpf.Views
{
    public partial class FiberTestsView : UserControl
    {
        private readonly ThroughputTestService _throughputService = new();
        private readonly DispatcherTimer _timer = new();

        public FiberTestsView()
        {
            InitializeComponent();
            
            // Stats polling timer (runs every 250ms)
            _timer.Interval = TimeSpan.FromMilliseconds(250);
            _timer.Tick += Timer_Tick;
        }

        private void StartTest_Click(object sender, RoutedEventArgs e)
        {
            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;

            // Start UDP server first, then local client loops
            _throughputService.StartServer(5002);
            _throughputService.StartClient("127.0.0.1", 5002, 10, 1400);

            _timer.Start();
        }

        private void StopTest_Click(object sender, RoutedEventArgs e)
        {
            StopSocketTest();
        }

        public void StopSocketTest()
        {
            _timer.Stop();
            _throughputService.StopClient();
            _throughputService.StopServer();

            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            var stats = _throughputService.GetStats();

            TxtTxPackets.Text = stats.PacketsSent.ToString("N0");
            TxtRxPackets.Text = stats.PacketsReceived.ToString("N0");
            TxtCrcErrors.Text = stats.CrcErrors.ToString("N0");
            TxtAverageMbps.Text = $"{stats.AverageMbps:F2} Mbps";
            TxtInstantSpeed.Text = $"{stats.CurrentMbps:F2} Mbps";

            UpdateChart(_throughputService.ThroughputHistory);

            if (!stats.IsRunning)
            {
                StopSocketTest();
            }
        }

        private void UpdateChart(List<double> history)
        {
            ChartCanvas.Children.Clear();
            if (history == null || history.Count < 2) return;

            // Limit array bounds for scrolling effect (last 45 points)
            int itemsCount = history.Count;
            int startIdx = Math.Max(0, itemsCount - 45);
            var renderList = history.GetRange(startIdx, itemsCount - startIdx);

            double canvasW = ChartCanvas.ActualWidth;
            double canvasH = ChartCanvas.ActualHeight;
            if (canvasW <= 0 || canvasH <= 0) return;

            // Draw area gradient fill
            var polygon = new Polygon
            {
                Fill = new LinearGradientBrush(
                    Color.FromArgb(28, 37, 118, 217),
                    Color.FromArgb(0, 37, 118, 217),
                    new Point(0.5, 0),
                    new Point(0.5, 1)
                )
            };

            // Draw line curve
            var polyline = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromRgb(37, 118, 217)),
                StrokeThickness = 2.0
            };

            double maxVal = 10.0;
            foreach (double val in renderList)
            {
                if (val > maxVal) maxVal = val;
            }
            maxVal *= 1.15; // Give headroom

            // Add bottom-left starting coordinate for polygon fill
            polygon.Points.Add(new Point(0, canvasH));

            for (int i = 0; i < renderList.Count; i++)
            {
                double x = (double)i / (renderList.Count - 1) * canvasW;
                double y = canvasH - (renderList[i] / maxVal) * canvasH;
                
                var pt = new Point(x, y);
                polyline.Points.Add(pt);
                polygon.Points.Add(pt);
            }

            // Add bottom-right end coordinate for polygon fill
            polygon.Points.Add(new Point(canvasW, canvasH));

            ChartCanvas.Children.Add(polygon);
            ChartCanvas.Children.Add(polyline);
        }

        private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateChart(_throughputService.ThroughputHistory);
        }
    }
}
