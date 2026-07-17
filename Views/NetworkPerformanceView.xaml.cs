using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using atp_enterprise_app_wpf.Services;
using LiveCharts;
using LiveCharts.Defaults;

namespace atp_enterprise_app_wpf.Views
{
    public partial class NetworkPerformanceView : UserControl, INotifyPropertyChanged
    {
        private NetPerfEngine _engine;
        private ChartValues<ObservablePoint> _chartValues;
        private double _sumMbps = 0;
        private int _countMbps = 0;
        private double _peakMbps = 0;

        public event PropertyChangedEventHandler PropertyChanged;

        public ChartValues<ObservablePoint> ChartValues
        {
            get => _chartValues;
            set
            {
                _chartValues = value;
                OnPropertyChanged();
            }
        }

        public NetworkPerformanceView()
        {
            InitializeComponent();
            ChartValues = new ChartValues<ObservablePoint>();
            DataContext = this;

            _engine = new NetPerfEngine();
            _engine.OnLog += Engine_OnLog;
            _engine.OnMetricsUpdated += Engine_OnMetricsUpdated;
            _engine.OnTestStopped += Engine_OnTestStopped;
        }

        private void Engine_OnLog(object sender, NetPerfLogEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                ListLogs.Items.Add(e.Message);
                if (ListLogs.Items.Count > 0)
                {
                    ListLogs.ScrollIntoView(ListLogs.Items[ListLogs.Items.Count - 1]);
                }
            });
        }

        private void Engine_OnMetricsUpdated(object sender, NetPerfMetricsEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Update stats
                _sumMbps += e.Mbps;
                _countMbps++;
                if (e.Mbps > _peakMbps) _peakMbps = e.Mbps;

                double avgMbps = _sumMbps / _countMbps;

                TxtCurrent.Text = e.Mbps.ToString("F2");
                TxtAverage.Text = avgMbps.ToString("F2");
                TxtPeak.Text = _peakMbps.ToString("F2");
                TxtTransferred.Text = FormatBytes(e.TotalBytes);

                // Update chart (keep last 100 points)
                ChartValues.Add(new ObservablePoint(e.TimeSeconds, e.Mbps));
                if (ChartValues.Count > 100)
                {
                    ChartValues.RemoveAt(0);
                }
            });
        }

        private void Engine_OnTestStopped(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                TxtStatus.Text = "Ready";
                BtnStart.IsEnabled = true;
                BtnStop.IsEnabled = false;
                EnableConfig(true);
            });
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            // Reset state
            ChartValues.Clear();
            _sumMbps = 0;
            _countMbps = 0;
            _peakMbps = 0;
            ListLogs.Items.Clear();

            TxtCurrent.Text = "0.00";
            TxtAverage.Text = "0.00";
            TxtPeak.Text = "0.00";
            TxtTransferred.Text = "0.00 B";

            TxtStatus.Text = "Running...";
            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
            EnableConfig(false);

            bool isServer = RbServer.IsChecked == true;
            string protocol = ((ComboBoxItem)CmbProtocol.SelectedItem).Content.ToString();
            int port = int.Parse(TxtPort.Text);

            if (isServer)
            {
                _engine.StartServer(protocol, port);
            }
            else
            {
                string host = TxtHost.Text;
                string direction = ((ComboBoxItem)CmbDirection.SelectedItem).Content.ToString();
                int duration = int.Parse(TxtDuration.Text);
                int streams = int.Parse(TxtStreams.Text);
                int bufferSize = int.Parse(TxtBufferSize.Text);

                _engine.StartClient(protocol, host, port, direction, duration, streams, bufferSize);
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _engine.StopTest();
        }

        private void EnableConfig(bool enable)
        {
            RbClient.IsEnabled = enable;
            RbServer.IsEnabled = enable;
            TxtHost.IsEnabled = enable && RbClient.IsChecked == true;
            TxtPort.IsEnabled = enable;
            CmbProtocol.IsEnabled = enable;
            CmbDirection.IsEnabled = enable && RbClient.IsChecked == true;
            TxtDuration.IsEnabled = enable && RbClient.IsChecked == true;
            TxtStreams.IsEnabled = enable && RbClient.IsChecked == true;
            TxtBufferSize.IsEnabled = enable;
        }

        private string FormatBytes(long bytes)
        {
            if (bytes == 0) return "0 B";
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
