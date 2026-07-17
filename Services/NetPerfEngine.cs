using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace atp_enterprise_app_wpf.Services
{
    public class NetPerfMetricsEventArgs : EventArgs
    {
        public double TimeSeconds { get; set; }
        public double Mbps { get; set; }
        public long TotalBytes { get; set; }
    }

    public class NetPerfLogEventArgs : EventArgs
    {
        public string Message { get; set; }
    }

    public class NetPerfEngine
    {
        public event EventHandler<NetPerfMetricsEventArgs> OnMetricsUpdated;
        public event EventHandler<NetPerfLogEventArgs> OnLog;
        public event EventHandler OnTestStopped;

        private CancellationTokenSource _cts;
        private List<Task> _activeTasks;
        private long _totalBytesTransferred;
        private long _lastBytesTransferred;
        private Stopwatch _testTimer;
        private Timer _reportTimer;

        public bool IsRunning { get; private set; }

        public void StartServer(string protocol, int port)
        {
            if (IsRunning) return;
            IsRunning = true;
            _cts = new CancellationTokenSource();
            _activeTasks = new List<Task>();
            _totalBytesTransferred = 0;
            _lastBytesTransferred = 0;
            _testTimer = Stopwatch.StartNew();

            Log($"Starting {protocol} Server on port {port}...");

            if (protocol == "TCP")
            {
                var task = Task.Run(() => RunTcpServer(port, _cts.Token));
                _activeTasks.Add(task);
            }
            else
            {
                var task = Task.Run(() => RunUdpServer(port, _cts.Token));
                _activeTasks.Add(task);
            }

            StartReportingTimer();
        }

        public void StartClient(string protocol, string host, int port, string direction, int durationSec, int streams, int bufferSizeKb)
        {
            if (IsRunning) return;
            IsRunning = true;
            _cts = new CancellationTokenSource();
            _activeTasks = new List<Task>();
            _totalBytesTransferred = 0;
            _lastBytesTransferred = 0;
            _testTimer = Stopwatch.StartNew();

            Log($"Starting {protocol} Client -> {host}:{port} ({direction}) | {streams} Streams | {bufferSizeKb}KB Buffer");

            for (int i = 0; i < streams; i++)
            {
                int streamId = i;
                if (protocol == "TCP")
                {
                    var task = Task.Run(() => RunTcpClient(host, port, direction, bufferSizeKb, streamId, _cts.Token));
                    _activeTasks.Add(task);
                }
                else
                {
                    var task = Task.Run(() => RunUdpClient(host, port, direction, bufferSizeKb, streamId, _cts.Token));
                    _activeTasks.Add(task);
                }
            }

            // Auto-stop after duration
            Task.Run(async () =>
            {
                try { await Task.Delay(durationSec * 1000, _cts.Token); } catch { }
                if (IsRunning)
                {
                    Log($"Test duration ({durationSec}s) reached. Stopping.");
                    StopTest();
                }
            });

            StartReportingTimer();
        }

        public void StopTest()
        {
            if (!IsRunning) return;
            IsRunning = false;
            
            _cts?.Cancel();
            _reportTimer?.Dispose();
            _testTimer?.Stop();

            Log("Test stopped.");
            OnTestStopped?.Invoke(this, EventArgs.Empty);
        }

        private void StartReportingTimer()
        {
            _reportTimer = new Timer(state =>
            {
                if (!IsRunning) return;

                long currentBytes = Interlocked.Read(ref _totalBytesTransferred);
                long bytesSinceLast = currentBytes - _lastBytesTransferred;
                _lastBytesTransferred = currentBytes;

                double timeSec = _testTimer.Elapsed.TotalSeconds;
                
                // Reporting every 250ms, so multiply by 4 to get bytes per sec, then convert to Mbps
                double mbps = (bytesSinceLast * 4.0 * 8.0) / (1024.0 * 1024.0);

                OnMetricsUpdated?.Invoke(this, new NetPerfMetricsEventArgs
                {
                    TimeSeconds = timeSec,
                    Mbps = mbps,
                    TotalBytes = currentBytes
                });

            }, null, 250, 250);
        }

        private void Log(string msg)
        {
            OnLog?.Invoke(this, new NetPerfLogEventArgs { Message = $"[{DateTime.Now:HH:mm:ss}] {msg}" });
        }

        private void RunTcpServer(int port, CancellationToken token)
        {
            TcpListener listener = null;
            try
            {
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                Log($"TCP Server listening on {port}");

                using (token.Register(() => listener.Stop()))
                {
                    while (!token.IsCancellationRequested)
                    {
                        var client = listener.AcceptTcpClient();
                        Log($"Client connected: {client.Client.RemoteEndPoint}");
                        Task.Run(() => HandleTcpServerConnection(client, token));
                    }
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested) Log($"TCP Server Error: {ex.Message}");
            }
            finally
            {
                listener?.Stop();
            }
        }

        private void HandleTcpServerConnection(TcpClient client, CancellationToken token)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    // Read header logic
                    byte[] headerLengthBuf = new byte[4];
                    int read = stream.Read(headerLengthBuf, 0, 4);
                    if (read != 4) return;
                    
                    int headerLen = BitConverter.ToInt32(headerLengthBuf, 0);
                    byte[] headerBuf = new byte[headerLen];
                    read = stream.Read(headerBuf, 0, headerLen);
                    string headerJson = Encoding.UTF8.GetString(headerBuf);
                    
                    bool isDownload = headerJson.Contains("\"Direction\":\"Download\"");
                    byte[] buffer = new byte[128 * 1024];

                    if (!isDownload)
                    {
                        // Upload mode: client sends to us, we read and discard
                        while (!token.IsCancellationRequested && client.Connected)
                        {
                            int bytesRead = stream.Read(buffer, 0, buffer.Length);
                            if (bytesRead == 0) break;
                            Interlocked.Add(ref _totalBytesTransferred, bytesRead);
                        }
                    }
                    else
                    {
                        // Download mode: server blasts to client
                        while (!token.IsCancellationRequested && client.Connected)
                        {
                            stream.Write(buffer, 0, buffer.Length);
                            Interlocked.Add(ref _totalBytesTransferred, buffer.Length);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Disconnected
            }
        }

        private void RunTcpClient(string host, int port, string direction, int bufferSizeKb, int streamId, CancellationToken token)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    client.Connect(host, port);
                    if (streamId == 0) Log($"TCP Client connected to {host}:{port}");

                    using (var stream = client.GetStream())
                    {
                        // Send header
                        string headerJson = $"{{\"StreamId\":{streamId},\"Direction\":\"{direction}\"}}";
                        byte[] headerBuf = Encoding.UTF8.GetBytes(headerJson);
                        byte[] headerLenBuf = BitConverter.GetBytes(headerBuf.Length);
                        
                        stream.Write(headerLenBuf, 0, 4);
                        stream.Write(headerBuf, 0, headerBuf.Length);

                        byte[] buffer = new byte[bufferSizeKb * 1024];

                        if (direction == "Upload")
                        {
                            // Client blasts
                            while (!token.IsCancellationRequested && client.Connected)
                            {
                                stream.Write(buffer, 0, buffer.Length);
                                Interlocked.Add(ref _totalBytesTransferred, buffer.Length);
                            }
                        }
                        else
                        {
                            // Client reads
                            while (!token.IsCancellationRequested && client.Connected)
                            {
                                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                                if (bytesRead == 0) break;
                                Interlocked.Add(ref _totalBytesTransferred, bytesRead);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested && streamId == 0) Log($"TCP Client Error: {ex.Message}");
            }
        }

        private void RunUdpServer(int port, CancellationToken token)
        {
            try
            {
                using (var udpServer = new UdpClient(port))
                {
                    Log($"UDP Server listening on {port}");
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, port);

                    while (!token.IsCancellationRequested)
                    {
                        if (udpServer.Available > 0)
                        {
                            byte[] data = udpServer.Receive(ref remoteEP);
                            Interlocked.Add(ref _totalBytesTransferred, data.Length);
                        }
                        else
                        {
                            Thread.Sleep(1);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested) Log($"UDP Server Error: {ex.Message}");
            }
        }

        private void RunUdpClient(string host, int port, string direction, int bufferSizeKb, int streamId, CancellationToken token)
        {
            try
            {
                using (var udpClient = new UdpClient())
                {
                    udpClient.Connect(host, port);
                    if (streamId == 0) Log($"UDP Client targeting {host}:{port}");
                    
                    // Note: Basic UDP implementation limits buffer size
                    if (bufferSizeKb > 60) bufferSizeKb = 60; // Max UDP payload ~65k
                    byte[] buffer = new byte[bufferSizeKb * 1024];

                    while (!token.IsCancellationRequested)
                    {
                        udpClient.Send(buffer, buffer.Length);
                        Interlocked.Add(ref _totalBytesTransferred, buffer.Length);
                        // Brief spin to prevent totally freezing the loopback stack
                        Thread.SpinWait(100); 
                    }
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested && streamId == 0) Log($"UDP Client Error: {ex.Message}");
            }
        }
    }
}
