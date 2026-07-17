using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

namespace atp_enterprise_app_wpf.Services
{
    public class ThroughputStats
    {
        public long BytesSent { get; set; }
        public long PacketsSent { get; set; }
        public long BytesReceived { get; set; }
        public long PacketsReceived { get; set; }
        public long CrcErrors { get; set; }
        public long DroppedPackets { get; set; }
        public double CurrentMbps { get; set; }
        public double AverageMbps { get; set; }
        public double MaxMbps { get; set; }
        public double ElapsedTimeSec { get; set; }
        public bool IsRunning { get; set; }
    }

    public class ThroughputTestService
    {
        private UdpClient? _serverSocket;
        private UdpClient? _clientSocket;
        private Thread? _serverThread;
        private Thread? _clientThread;
        private bool _serverRunning;
        private bool _clientRunning;

        private readonly object _statsLock = new();
        private long _bytesSent;
        private long _packetsSent;
        private long _bytesReceived;
        private long _packetsReceived;
        private long _crcErrors;
        private long _droppedPackets;
        private long _lastSeqNum = -1;

        private double _currentMbps;
        private double _averageMbps;
        private double _maxMbps;
        private double _elapsedTimeSec;

        public List<double> ThroughputHistory { get; } = new();

        private static uint ComputeAdler32(byte[] data)
        {
            uint a = 1;
            uint b = 0;
            for (int i = 0; i < data.Length; i++)
            {
                a = (a + data[i]) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }

        public void StartServer(int port = 5002)
        {
            if (_serverRunning) return;

            lock (_statsLock)
            {
                _bytesReceived = 0;
                _packetsReceived = 0;
                _crcErrors = 0;
                _droppedPackets = 0;
                _lastSeqNum = -1;
            }

            _serverRunning = true;
            _serverThread = new Thread(() =>
            {
                try
                {
                    _serverSocket = new UdpClient(port);
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

                    while (_serverRunning)
                    {
                        if (_serverSocket.Available > 0)
                        {
                            byte[] data = _serverSocket.Receive(ref remoteEP);
                            ProcessReceivedPacket(data);
                        }
                        else
                        {
                            Thread.Sleep(1);
                        }
                    }
                }
                catch
                {
                    StopServer();
                }
            })
            {
                IsBackground = true
            };
            _serverThread.Start();
        }

        public void StopServer()
        {
            _serverRunning = false;
            if (_serverSocket != null)
            {
                _serverSocket.Close();
                _serverSocket = null;
            }
            _serverThread = null;
        }

        private void ProcessReceivedPacket(byte[] data)
        {
            if (data.Length < 12)
            {
                lock (_statsLock) { _crcErrors++; }
                return;
            }

            uint seqNum = ((uint)data[0] << 24) | ((uint)data[1] << 16) | ((uint)data[2] << 8) | data[3];
            uint packetChecksum = ((uint)data[4] << 24) | ((uint)data[5] << 16) | ((uint)data[6] << 8) | data[7];
            uint payloadSize = ((uint)data[8] << 24) | ((uint)data[9] << 16) | ((uint)data[10] << 8) | data[11];

            if (data.Length - 12 != payloadSize)
            {
                lock (_statsLock) { _crcErrors++; }
                return;
            }

            byte[] payload = new byte[payloadSize];
            Buffer.BlockCopy(data, 12, payload, 0, (int)payloadSize);

            uint calculatedChecksum = ComputeAdler32(payload);
            if (calculatedChecksum != packetChecksum)
            {
                lock (_statsLock) { _crcErrors++; }
                return;
            }

            lock (_statsLock)
            {
                _packetsReceived++;
                _bytesReceived += data.Length;

                if (_lastSeqNum != -1)
                {
                    if (seqNum > _lastSeqNum + 1)
                    {
                        _droppedPackets += (seqNum - _lastSeqNum - 1);
                    }
                }
                _lastSeqNum = seqNum;
            }
        }

        public void StartClient(string targetIp = "127.0.0.1", int targetPort = 5002, int durationSec = 10, int packetSize = 1400)
        {
            if (_clientRunning) return;

            lock (_statsLock)
            {
                _bytesSent = 0;
                _packetsSent = 0;
                _currentMbps = 0.0;
                _averageMbps = 0.0;
                _maxMbps = 0.0;
                _elapsedTimeSec = 0.0;
            }
            ThroughputHistory.Clear();

            _clientRunning = true;
            _clientThread = new Thread(() =>
            {
                try
                {
                    _clientSocket = new UdpClient();
                    _clientSocket.Connect(targetIp, targetPort);

                    int payloadSize = packetSize - 12;
                    byte[] payload = new byte[payloadSize];
                    for (int i = 0; i < payload.Length; i++) payload[i] = 84; 

                    uint checksum = ComputeAdler32(payload);
                    uint seqNum = 0;

                    Stopwatch sw = Stopwatch.StartNew();
                    Stopwatch intervalSw = Stopwatch.StartNew();
                    long intervalBytesSent = 0;

                    while (_clientRunning && sw.Elapsed.TotalSeconds < durationSec)
                    {
                        byte[] packet = new byte[packetSize];
                        
                        packet[0] = (byte)(seqNum >> 24);
                        packet[1] = (byte)(seqNum >> 16);
                        packet[2] = (byte)(seqNum >> 8);
                        packet[3] = (byte)seqNum;

                        packet[4] = (byte)(checksum >> 24);
                        packet[5] = (byte)(checksum >> 16);
                        packet[6] = (byte)(checksum >> 8);
                        packet[7] = (byte)checksum;

                        packet[8] = (byte)(payloadSize >> 24);
                        packet[9] = (byte)(payloadSize >> 16);
                        packet[10] = (byte)(payloadSize >> 8);
                        packet[11] = (byte)payloadSize;

                        Buffer.BlockCopy(payload, 0, packet, 12, payloadSize);

                        _clientSocket.Send(packet, packet.Length);
                        seqNum++;

                        lock (_statsLock)
                        {
                            _packetsSent++;
                            _bytesSent += packet.Length;
                        }
                        intervalBytesSent += packet.Length;

                        if (intervalSw.ElapsedMilliseconds >= 250)
                        {
                            double elapsedSeconds = intervalSw.Elapsed.TotalSeconds;
                            double mbps = (intervalBytesSent * 8.0) / elapsedSeconds / 1000000.0;
                            
                            lock (_statsLock)
                            {
                                _currentMbps = Math.Round(mbps, 2);
                                _averageMbps = Math.Round((_bytesSent * 8.0) / sw.Elapsed.TotalSeconds / 1000000.0, 2);
                                if (_currentMbps > _maxMbps) _maxMbps = _currentMbps;
                                _elapsedTimeSec = sw.Elapsed.TotalSeconds;
                                ThroughputHistory.Add(_currentMbps);
                            }

                            intervalBytesSent = 0;
                            intervalSw.Restart();
                        }
                    }
                    sw.Stop();
                }
                catch
                {
                }
                finally
                {
                    StopClient();
                }
            })
            {
                IsBackground = true
            };
            _clientThread.Start();
        }

        public void StopClient()
        {
            _clientRunning = false;
            if (_clientSocket != null)
            {
                _clientSocket.Close();
                _clientSocket = null;
            }
            _clientThread = null;
        }

        public ThroughputStats GetStats()
        {
            lock (_statsLock)
            {
                return new ThroughputStats
                {
                    BytesSent = _bytesSent,
                    PacketsSent = _packetsSent,
                    BytesReceived = _bytesReceived,
                    PacketsReceived = _packetsReceived,
                    CrcErrors = _crcErrors,
                    DroppedPackets = _droppedPackets,
                    CurrentMbps = _currentMbps,
                    AverageMbps = _averageMbps,
                    MaxMbps = _maxMbps,
                    ElapsedTimeSec = _elapsedTimeSec,
                    IsRunning = _clientRunning
                };
            }
        }
    }
}
