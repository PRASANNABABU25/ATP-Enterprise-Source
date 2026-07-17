using System;
using System.Diagnostics;
using System.Threading;

namespace atp_enterprise_app_wpf.Services
{
    public class OpticalStatisticsSnapshot
    {
        public long TotalBytesSent { get; set; }
        public long TotalBytesReceived { get; set; }
        public long PacketsSent { get; set; }
        public long PacketsReceived { get; set; }
        public long DroppedPackets { get; set; }
        public long CorruptedPackets { get; set; }
        public double CurrentThroughputMbps { get; set; }
        public double AverageThroughputMbps { get; set; }
        public TimeSpan Elapsed { get; set; }
    }

    public class OpticalStatisticsEngine
    {
        private long _bytesSent;
        private long _bytesReceived;
        private long _packetsSent;
        private long _packetsReceived;
        private long _droppedPackets;
        private long _corruptedPackets;
        
        private long _lastBytesSent;
        private long _lastBytesReceived;
        private Stopwatch _stopwatch = new();
        private Stopwatch _intervalStopwatch = new();

        public event Action<OpticalStatisticsSnapshot>? OnStatisticsUpdated;

        private CancellationTokenSource? _cts;

        public void Start()
        {
            Reset();
            _cts = new CancellationTokenSource();
            _stopwatch.Start();
            _intervalStopwatch.Start();

            var thread = new Thread(MonitorLoop) { IsBackground = true, Name = "OpticalStatsThread" };
            thread.Start();
        }

        public void Stop()
        {
            _cts?.Cancel();
            _stopwatch.Stop();
            _intervalStopwatch.Stop();
        }

        public void Reset()
        {
            Interlocked.Exchange(ref _bytesSent, 0);
            Interlocked.Exchange(ref _bytesReceived, 0);
            Interlocked.Exchange(ref _packetsSent, 0);
            Interlocked.Exchange(ref _packetsReceived, 0);
            Interlocked.Exchange(ref _droppedPackets, 0);
            Interlocked.Exchange(ref _corruptedPackets, 0);
            _lastBytesSent = 0;
            _lastBytesReceived = 0;
            _stopwatch.Reset();
            _intervalStopwatch.Reset();
        }

        public void AddBytesSent(long bytes)
        {
            Interlocked.Add(ref _bytesSent, bytes);
            Interlocked.Increment(ref _packetsSent);
        }

        public void AddBytesReceived(long bytes)
        {
            Interlocked.Add(ref _bytesReceived, bytes);
            Interlocked.Increment(ref _packetsReceived);
        }

        public void AddDroppedPacket() => Interlocked.Increment(ref _droppedPackets);
        public void AddCorruptedPacket() => Interlocked.Increment(ref _corruptedPackets);

        public OpticalStatisticsSnapshot GetSnapshot()
        {
            long currentSent = Interlocked.Read(ref _bytesSent);
            long currentReceived = Interlocked.Read(ref _bytesReceived);
            double totalSeconds = _stopwatch.Elapsed.TotalSeconds;

            double avgMbps = 0;
            if (totalSeconds > 0)
            {
                // Convert bytes to Megabits
                avgMbps = ((currentSent + currentReceived) * 8.0) / 1_000_000.0 / totalSeconds;
            }

            return new OpticalStatisticsSnapshot
            {
                TotalBytesSent = currentSent,
                TotalBytesReceived = currentReceived,
                PacketsSent = Interlocked.Read(ref _packetsSent),
                PacketsReceived = Interlocked.Read(ref _packetsReceived),
                DroppedPackets = Interlocked.Read(ref _droppedPackets),
                CorruptedPackets = Interlocked.Read(ref _corruptedPackets),
                AverageThroughputMbps = avgMbps,
                Elapsed = _stopwatch.Elapsed
            };
        }

        private void MonitorLoop()
        {
            try
            {
                while (_cts != null && !_cts.IsCancellationRequested)
                {
                    Thread.Sleep(1000); // 1 second update interval

                    if (_cts.IsCancellationRequested) break;

                    long currentSent = Interlocked.Read(ref _bytesSent);
                    long currentReceived = Interlocked.Read(ref _bytesReceived);
                    
                    long deltaSent = currentSent - _lastBytesSent;
                    long deltaReceived = currentReceived - _lastBytesReceived;
                    double deltaSeconds = _intervalStopwatch.Elapsed.TotalSeconds;
                    
                    _intervalStopwatch.Restart();
                    _lastBytesSent = currentSent;
                    _lastBytesReceived = currentReceived;

                    double currentMbps = 0;
                    if (deltaSeconds > 0)
                    {
                        currentMbps = ((deltaSent + deltaReceived) * 8.0) / 1_000_000.0 / deltaSeconds;
                    }

                    var snapshot = GetSnapshot();
                    snapshot.CurrentThroughputMbps = currentMbps;

                    OnStatisticsUpdated?.Invoke(snapshot);
                }
            }
            catch (Exception) { /* Background thread exit */ }
        }
    }
}
