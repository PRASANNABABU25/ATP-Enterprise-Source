using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using atp_enterprise_app_wpf.Models;
using atp_enterprise_app_wpf.Services;

namespace atp_enterprise_app_wpf.Views
{
    public partial class HealthMonitorView : UserControl
    {
        public SettingsView? Settings { get; set; }

        public HealthMonitorView()
        {
            InitializeComponent();
            Loaded += HealthMonitorView_Loaded;
            Unloaded += HealthMonitorView_Unloaded;

            UpdateEngineStatusUI();
        }

        private void HealthMonitorView_Loaded(object sender, RoutedEventArgs e)
        {
            // Subscribe to shared telemetry updates
            MonitoringEngine.Instance.OnTelemetryUpdated += TelemetryUpdated_Callback;
            UpdateEngineStatusUI();
        }

        private void HealthMonitorView_Unloaded(object sender, RoutedEventArgs e)
        {
            // Unsubscribe to avoid memory leakage
            MonitoringEngine.Instance.OnTelemetryUpdated -= TelemetryUpdated_Callback;
        }

        private void TelemetryUpdated_Callback(RealtimeMetrics metrics, List<SensorReading> sensors)
        {
            Dispatcher.Invoke(() =>
            {
                TxtLastUpdate.Text = $"Last Scan: {DateTime.Now:HH:mm:ss}";

                // Update text displays
                TxtLiveCpu.Text = $"{metrics.CpuUsagePercent:F1} %";
                TxtLiveRam.Text = $"{metrics.RamUsagePercent:F1} %";

                double? firstTemp = null;
                if (metrics.Temperatures.Count > 0)
                {
                    firstTemp = metrics.Temperatures[0].TemperatureCelsius;
                    TxtLiveTemp.Text = $"{firstTemp.Value:F1} °C";
                }
                else
                {
                    TxtLiveTemp.Text = "Sensor Not Supported";
                }

                // Update details
                TxtDtlCpuLoad.Text = $"{metrics.CpuUsagePercent:F1}%";
                
                var freqSensor = sensors.FirstOrDefault(s => s.Name == "CPU Current Frequency");
                TxtDtlCpuFreq.Text = freqSensor != null ? $"{freqSensor.Value} MHz" : "Calculating...";

                var throttleSensor = sensors.FirstOrDefault(s => s.Name == "CPU Thermal Throttling");
                TxtDtlCpuThrottling.Text = throttleSensor != null ? throttleSensor.Value : "Inactive";
                TxtDtlCpuThrottling.Foreground = TxtDtlCpuThrottling.Text == "Active Throttling" ? Brushes.Red : Brushes.White;

                TxtDtlRamTotal.Text = $"{(metrics.RamTotalBytes / 1073741824.0):F2} GB";
                TxtDtlRamFree.Text = $"{(metrics.RamFreeBytes / 1073741824.0):F2} GB";
                TxtDtlRamPct.Text = $"{metrics.RamUsagePercent:F1}%";

                var powerSensor = sensors.FirstOrDefault(s => s.Name == "Power Source AC/DC");
                TxtDtlPowerSource.Text = powerSensor != null ? powerSensor.Value : "AC Power Source";

                // Refresh registry Grid
                GridSensorRegistry.ItemsSource = sensors;

                // Redraw mini charts using rolling history queues from engine
                var history = MonitoringEngine.Instance.GetHistory();
                if (history.Count >= 2)
                {
                    var cpuHistory = history.Select(h => h.CpuUsagePercent).ToList();
                    var ramHistory = history.Select(h => h.RamUsagePercent).ToList();
                    var tempHistory = history.Select(h => h.Temperatures.Count > 0 ? h.Temperatures[0].TemperatureCelsius : 0.0).ToList();

                    DrawMiniChart(CanvasCpu, cpuHistory, 100.0, Color.FromRgb(37, 118, 217));
                    DrawMiniChart(CanvasRam, ramHistory, 100.0, Color.FromRgb(82, 196, 26));
                    DrawMiniChart(CanvasTemp, tempHistory, 100.0, Color.FromRgb(250, 173, 20));
                }
            });
        }

        private void StartMonitoring_Click(object sender, RoutedEventArgs e)
        {
            // Acquire parameters from settings panel or defaults
            int interval = Settings?.RefreshIntervalMs ?? 1000;
            int length = Settings?.HistoryLength ?? 60;
            double cpuW = Settings?.CpuLimitWarn ?? 80;
            double cpuC = Settings?.CpuLimitCritical ?? 95;
            double ramW = Settings?.RamLimitWarn ?? 85;
            double ramC = Settings?.RamLimitCritical ?? 95;
            double tempMax = Settings?.TempLimitMax ?? 75;

            MonitoringEngine.Instance.Start(interval, length, cpuW, cpuC, ramW, ramC, tempMax);
            UpdateEngineStatusUI();
            TxtLastUpdate.Text = "Monitoring started.";
        }

        private void StopMonitoring_Click(object sender, RoutedEventArgs e)
        {
            MonitoringEngine.Instance.Stop();
            UpdateEngineStatusUI();
            TxtLastUpdate.Text = "Monitoring suspended.";
        }

        private void UpdateEngineStatusUI()
        {
            bool active = MonitoringEngine.Instance.IsRunning;
            if (active)
            {
                TxtStatus.Text = "ACTIVE";
                StatusIndicator.Background = new SolidColorBrush(Color.FromRgb(35, 120, 4));
            }
            else
            {
                TxtStatus.Text = "SUSPENDED";
                StatusIndicator.Background = new SolidColorBrush(Color.FromRgb(128, 40, 40));
            }
        }

        private void DrawMiniChart(Canvas canvas, List<double> history, double maxVal, Color strokeColor)
        {
            canvas.Children.Clear();
            if (history.Count < 2) return;

            double canvasW = canvas.ActualWidth;
            double canvasH = canvas.ActualHeight;
            if (canvasW <= 0 || canvasH <= 0) return;

            // Fill polygon under chart
            var polygon = new Polygon
            {
                Fill = new LinearGradientBrush(
                    Color.FromArgb(20, strokeColor.R, strokeColor.G, strokeColor.B),
                    Color.FromArgb(0, strokeColor.R, strokeColor.G, strokeColor.B),
                    new Point(0.5, 0), new Point(0.5, 1)),
                StrokeThickness = 0
            };

            // Main line path
            var polyline = new Polyline
            {
                Stroke = new SolidColorBrush(strokeColor),
                StrokeThickness = 1.5
            };

            double step = canvasW / (history.Count - 1);
            polygon.Points.Add(new Point(0, canvasH));

            for (int i = 0; i < history.Count; i++)
            {
                double x = i * step;
                double val = history[i];
                if (val > maxVal) val = maxVal;
                double y = canvasH - ((val / maxVal) * canvasH);

                polyline.Points.Add(new Point(x, y));
                polygon.Points.Add(new Point(x, y));
            }

            polygon.Points.Add(new Point(canvasW, canvasH));

            canvas.Children.Add(polygon);
            canvas.Children.Add(polyline);
        }
    }
}
