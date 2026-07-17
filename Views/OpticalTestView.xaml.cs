using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using atp_enterprise_app_wpf.Services;

namespace atp_enterprise_app_wpf.Views
{
    public partial class OpticalTestView : UserControl
    {
        private OpticalStatisticsEngine _statsEngine;
        private OpticalTestProtocol _protocol;

        public OpticalTestView()
        {
            InitializeComponent();
            _statsEngine = new OpticalStatisticsEngine();
            _statsEngine.OnStatisticsUpdated += StatsEngine_OnStatisticsUpdated;
            _protocol = new OpticalTestProtocol(_statsEngine);
        }

        private void StatsEngine_OnStatisticsUpdated(OpticalStatisticsSnapshot snapshot)
        {
            Dispatcher.Invoke(() =>
            {
                TxtCurrentMbps.Text = $"{snapshot.CurrentThroughputMbps:F2} Mbps";
                TxtAvgMbps.Text = $"{snapshot.AverageThroughputMbps:F2} Mbps";
                
                double totalMb = (snapshot.TotalBytesSent + snapshot.TotalBytesReceived) / 1048576.0;
                TxtTotalBytes.Text = $"{totalMb:F2} MB";

                TxtPacketsSent.Text = $"Packets Sent: {snapshot.PacketsSent}";
                TxtPacketsReceived.Text = $"Packets Received: {snapshot.PacketsReceived}";
                TxtDropped.Text = $"Dropped / Missing: {snapshot.DroppedPackets}";
                TxtCorrupted.Text = $"Corrupted / Checksum Fail: {snapshot.CorruptedPackets}";
                
                TxtElapsed.Text = $"Elapsed Time: {snapshot.Elapsed:hh\\:mm\\:ss}";
            });
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            bool isServer = CmbMode.SelectedIndex == 0;
            string targetIp = TxtIpAddress.Text;
            if (!int.TryParse(TxtPort.Text, out int port)) port = 9000;
            if (!int.TryParse(TxtPayload.Text, out int payloadSize)) payloadSize = 65536;
            if (!int.TryParse(TxtDuration.Text, out int duration)) duration = 30;

            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
            CmbMode.IsEnabled = false;
            TxtStatus.Text = isServer ? "Status: LISTENING (SERVER MODE)" : "Status: TRANSMITTING (CLIENT MODE)";

            _statsEngine.Start();

            try
            {
                if (isServer)
                {
                    await _protocol.RunServerAsync(port, CancellationToken.None); // Runs until stopped
                }
                else
                {
                    await _protocol.RunClientAsync(targetIp, port, duration, payloadSize, CancellationToken.None);
                    TxtStatus.Text = "Status: TEST COMPLETED";
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Status: ERROR - {ex.Message}";
                TxtStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
            finally
            {
                ResetUI();
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _protocol.Stop();
            _statsEngine.Stop();
            TxtStatus.Text = "Status: STOPPED";
            ResetUI();
        }

        private void ResetUI()
        {
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
            CmbMode.IsEnabled = true;
        }
    }
}
