using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using atp_enterprise_app_wpf.Models;

namespace atp_enterprise_app_wpf.Services
{
    public class MonitoringEngine
    {
        private static MonitoringEngine? _instance;
        public static MonitoringEngine Instance => _instance ??= new MonitoringEngine();

        private readonly ManagementScope _wmiScope;
        private readonly List<SensorReading> _sensorRegistry = new();
        private readonly Queue<RealtimeMetrics> _history = new();
        private readonly List<string> _diskActiveCounters = new();
        
        private Timer? _pollingTimer;
        private readonly object _lock = new();
        private bool _isRunning;
        public bool IsRunning => _isRunning;
        private string _logFilePath = string.Empty;

        [DllImport("kernel32.dll")]
        private static extern bool GetSystemPowerStatus(out SystemPowerStatus lpSystemPowerStatus);

        private struct SystemPowerStatus
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public uint BatteryLifeTime;
            public uint BatteryFullLifeTime;
        }

        // Configurable configurations (synced from SettingsView)
        public int RefreshIntervalMs { get; set; } = 1000;
        public int HistoryMaxSamples { get; set; } = 60;
        public double CpuLimitWarn { get; set; } = 80;
        public double CpuLimitCritical { get; set; } = 95;
        public double RamLimitWarn { get; set; } = 85;
        public double RamLimitCritical { get; set; } = 95;
        public double TempLimitMax { get; set; } = 75;

        // Event for UI notification
        public event Action<RealtimeMetrics, List<SensorReading>>? OnTelemetryUpdated;

        private double _maxCpuFreqObserved = 0.0;
        private double _minCpuFreqObserved = 999.0;

        private MonitoringEngine()
        {
            _wmiScope = new ManagementScope(@"\\.\root\cimv2");
            _wmiScope.Connect();

            // Set up log directory
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string logDir = Path.Combine(appDir, "Logs");
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
            _logFilePath = Path.Combine(logDir, $"TelemetryLog_{DateTime.Now:yyyyMMdd}.txt");

            InitializeSensorRegistry();
        }

        private void InitializeSensorRegistry()
        {
            lock (_lock)
            {
                _sensorRegistry.Clear();
                // 1. Processor group
                _sensorRegistry.Add(new SensorReading { Name = "CPU Workload", Type = "Processor", Unit = "%", Status = "Reporting" });
                _sensorRegistry.Add(new SensorReading { Name = "CPU Current Frequency", Type = "Processor", Unit = "MHz", Status = "Reporting" });
                _sensorRegistry.Add(new SensorReading { Name = "CPU Thermal Throttling", Type = "Processor", Unit = "State", Status = "Reporting" });

                // 2. Memory group
                _sensorRegistry.Add(new SensorReading { Name = "RAM Utilization", Type = "Memory", Unit = "%", Status = "Reporting" });
                _sensorRegistry.Add(new SensorReading { Name = "RAM Free Capacity", Type = "Memory", Unit = "GB", Status = "Reporting" });

                // 3. Thermal group
                _sensorRegistry.Add(new SensorReading { Name = "CPU Core Temperature", Type = "Thermal", Unit = "°C", Status = "Reporting" });
                _sensorRegistry.Add(new SensorReading { Name = "Storage Device Temperature", Type = "Thermal", Unit = "°C", Status = "Reporting" });

                // 4. Power group
                _sensorRegistry.Add(new SensorReading { Name = "Power Source AC/DC", Type = "Power", Unit = "Source", Status = "Reporting" });
                _sensorRegistry.Add(new SensorReading { Name = "Voltage Rails 12V", Type = "Power", Unit = "V", Status = "Not Supported", Value = "Sensor Not Supported" });
                _sensorRegistry.Add(new SensorReading { Name = "Chassis Fan Speed", Type = "Fan", Unit = "RPM", Status = "Not Supported", Value = "Sensor Not Supported" });
            }
        }

        public void Start(int intervalMs, int historyLen, double cpuW, double cpuC, double ramW, double ramC, double tempMax)
        {
            lock (_lock)
            {
                if (_isRunning) return;

                RefreshIntervalMs = intervalMs;
                HistoryMaxSamples = historyLen;
                CpuLimitWarn = cpuW;
                CpuLimitCritical = cpuC;
                RamLimitWarn = ramW;
                RamLimitCritical = ramC;
                TempLimitMax = tempMax;

                _isRunning = true;
                _pollingTimer = new Timer(PollTelemetryCallback, null, 0, RefreshIntervalMs);
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (!_isRunning) return;
                _isRunning = false;
                _pollingTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _pollingTimer?.Dispose();
                _pollingTimer = null;
            }
        }

        public List<SensorReading> GetSensorRegistry()
        {
            lock (_lock)
            {
                return _sensorRegistry.ToList();
            }
        }

        public List<RealtimeMetrics> GetHistory()
        {
            lock (_lock)
            {
                return _history.ToList();
            }
        }

        private void PollTelemetryCallback(object? state)
        {
            try
            {
                var metrics = AcquireMetrics();
                EvaluateHealthAndRegistry(metrics);
                
                lock (_lock)
                {
                    _history.Enqueue(metrics);
                    while (_history.Count > HistoryMaxSamples)
                    {
                        _history.Dequeue();
                    }
                }

                // Write logs to disk
                AppendLogToDisk(metrics);

                // Raise event
                OnTelemetryUpdated?.Invoke(metrics, GetSensorRegistry());
            }
            catch {}
        }

        private RealtimeMetrics AcquireMetrics()
        {
            var metrics = new RealtimeMetrics();

            // CPU load
            double cpuVal = 0.0;
            try
            {
                using var searcher = new ManagementObjectSearcher(_wmiScope, new SelectQuery("SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'"));
                using var collection = searcher.Get();
                foreach (ManagementObject mObj in collection)
                {
                    double.TryParse(mObj["PercentProcessorTime"]?.ToString(), out cpuVal);
                }
            }
            catch {}
            metrics.CpuUsagePercent = cpuVal;

            // Memory load
            try
            {
                using var searcher = new ManagementObjectSearcher(_wmiScope, new SelectQuery("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem"));
                using var collection = searcher.Get();
                foreach (ManagementObject mObj in collection)
                {
                    double totalKb = double.Parse(mObj["TotalVisibleMemorySize"].ToString());
                    double freeKb = double.Parse(mObj["FreePhysicalMemory"].ToString());
                    metrics.RamTotalBytes = totalKb * 1024;
                    metrics.RamFreeBytes = freeKb * 1024;
                    metrics.RamUsagePercent = Math.Round(((totalKb - freeKb) / totalKb) * 100, 1);
                }
            }
            catch {}

            // Thermal zones
            try
            {
                var wmiWmiScope = new ManagementScope(@"\\.\root\wmi");
                wmiWmiScope.Connect();
                using var searcher = new ManagementObjectSearcher(wmiWmiScope, new SelectQuery("SELECT InstanceName, CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"));
                using var collection = searcher.Get();
                foreach (ManagementObject mObj in collection)
                {
                    string name = mObj["InstanceName"]?.ToString() ?? "Thermal Zone";
                    double kelvin10 = double.Parse(mObj["CurrentTemperature"].ToString());
                    double celsius = (kelvin10 / 10.0) - 273.15;
                    metrics.Temperatures.Add(new ThermalZoneReading
                    {
                        Zone = name,
                        TemperatureCelsius = Math.Round(celsius, 2)
                    });
                }
            }
            catch {}

            // Network stats
            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var nic in nics)
                {
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet || nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    {
                        var ipStats = nic.GetIPv4Statistics();
                        metrics.NetworkStats.Add(new NetworkStatsReading
                        {
                            Name = nic.Name,
                            ReceivedBytes = ipStats.BytesReceived,
                            SentBytes = ipStats.BytesSent,
                            ReceivedDiscarded = ipStats.IncomingPacketsDiscarded,
                            ReceivedErrors = ipStats.IncomingPacketsWithErrors,
                            SentDiscarded = ipStats.OutgoingPacketsDiscarded,
                            SentErrors = ipStats.OutgoingPacketsWithErrors
                        });
                    }
                }
            }
            catch {}

            return metrics;
        }

        private void EvaluateHealthAndRegistry(RealtimeMetrics metrics)
        {
            lock (_lock)
            {
                // 1. CPU utilization
                var cpuSensor = _sensorRegistry.FirstOrDefault(s => s.Name == "CPU Workload");
                if (cpuSensor != null)
                {
                    cpuSensor.Value = $"{metrics.CpuUsagePercent:F1}";
                    cpuSensor.Timestamp = DateTime.Now;
                    cpuSensor.HealthState = metrics.CpuUsagePercent >= CpuLimitCritical ? "Critical" : 
                                            (metrics.CpuUsagePercent >= CpuLimitWarn ? "Warning" : "Normal");
                }

                // CPU Frequency (Read base clock)
                double currentFreq = 2600.0;
                try
                {
                    using var searcher = new ManagementObjectSearcher(_wmiScope, new SelectQuery("SELECT CurrentClockSpeed FROM Win32_Processor"));
                    using var collection = searcher.Get();
                    foreach (ManagementObject mObj in collection)
                    {
                        double.TryParse(mObj["CurrentClockSpeed"]?.ToString(), out currentFreq);
                    }
                }
                catch {}
                
                if (currentFreq > _maxCpuFreqObserved) _maxCpuFreqObserved = currentFreq;
                if (currentFreq < _minCpuFreqObserved && currentFreq > 0) _minCpuFreqObserved = currentFreq;

                var freqSensor = _sensorRegistry.FirstOrDefault(s => s.Name == "CPU Current Frequency");
                if (freqSensor != null)
                {
                    freqSensor.Value = $"{currentFreq:F0}";
                    freqSensor.Timestamp = DateTime.Now;
                    freqSensor.HealthState = "Normal";
                }

                // CPU Throttling
                var throttlingSensor = _sensorRegistry.FirstOrDefault(s => s.Name == "CPU Thermal Throttling");
                if (throttlingSensor != null)
                {
                    throttlingSensor.Value = currentFreq < _minCpuFreqObserved + 200 && _minCpuFreqObserved > 0 ? "Active Throttling" : "Inactive";
                    throttlingSensor.Timestamp = DateTime.Now;
                    throttlingSensor.HealthState = throttlingSensor.Value == "Active Throttling" ? "Warning" : "Normal";
                }

                // 2. RAM
                var ramSensor = _sensorRegistry.FirstOrDefault(s => s.Name == "RAM Utilization");
                if (ramSensor != null)
                {
                    ramSensor.Value = $"{metrics.RamUsagePercent:F1}";
                    ramSensor.Timestamp = DateTime.Now;
                    ramSensor.HealthState = metrics.RamUsagePercent >= RamLimitCritical ? "Critical" :
                                            (metrics.RamUsagePercent >= RamLimitWarn ? "Warning" : "Normal");
                }

                var ramFreeSensor = _sensorRegistry.FirstOrDefault(s => s.Name == "RAM Free Capacity");
                if (ramFreeSensor != null)
                {
                    ramFreeSensor.Value = $"{(metrics.RamFreeBytes / 1073741824.0):F2}";
                    ramFreeSensor.Timestamp = DateTime.Now;
                    ramFreeSensor.HealthState = "Normal";
                }

                // 3. CPU Core Temp
                double maxTemp = 0.0;
                var tempSensor = _sensorRegistry.FirstOrDefault(s => s.Name == "CPU Core Temperature");
                if (tempSensor != null)
                {
                    if (metrics.Temperatures.Count > 0)
                    {
                        maxTemp = metrics.Temperatures.Max(t => t.TemperatureCelsius);
                        tempSensor.Value = $"{maxTemp:F1}";
                        tempSensor.Status = "Reporting";
                        tempSensor.HealthState = maxTemp >= TempLimitMax ? "Critical" :
                                                (maxTemp >= TempLimitMax - 10 ? "Warning" : "Normal");
                    }
                    else
                    {
                        tempSensor.Value = "Sensor Not Supported";
                        tempSensor.Status = "Not Supported";
                        tempSensor.HealthState = "Unavailable";
                    }
                    tempSensor.Timestamp = DateTime.Now;
                }

                // 4. Storage Temp
                var diskTempSensor = _sensorRegistry.FirstOrDefault(s => s.Name == "Storage Device Temperature");
                if (diskTempSensor != null)
                {
                    diskTempSensor.Value = "Not Available";
                    diskTempSensor.Status = "Sensor Not Supported";
                    diskTempSensor.HealthState = "Unavailable";
                    diskTempSensor.Timestamp = DateTime.Now;
                }

                // 5. Power AC status
                var powerSensor = _sensorRegistry.FirstOrDefault(s => s.Name == "Power Source AC/DC");
                if (powerSensor != null)
                {
                    try
                    {
                        if (GetSystemPowerStatus(out SystemPowerStatus pStatus))
                        {
                            powerSensor.Value = pStatus.ACLineStatus == 1 ? "AC Grid Power" : (pStatus.ACLineStatus == 0 ? "DC Battery Power" : "Unknown");
                            powerSensor.Status = "Reporting";
                            powerSensor.HealthState = pStatus.ACLineStatus == 1 ? "Normal" : "Warning";
                        }
                        else
                        {
                            powerSensor.Value = "AC Grid Power (Assumed)";
                            powerSensor.Status = "Reporting";
                            powerSensor.HealthState = "Normal";
                        }
                    }
                    catch
                    {
                        powerSensor.Value = "AC Grid Power (Assumed)";
                        powerSensor.Status = "Reporting";
                        powerSensor.HealthState = "Normal";
                    }
                    powerSensor.Timestamp = DateTime.Now;
                }
            }
        }

        private void AppendLogToDisk(RealtimeMetrics metrics)
        {
            try
            {
                lock (_lock)
                {
                    using var writer = new StreamWriter(_logFilePath, true);
                    foreach (var s in _sensorRegistry)
                    {
                        if (s.Status == "Reporting")
                        {
                            writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\t{s.Name}\t{s.Value}\t{s.Unit}\t{s.HealthState}");
                        }
                    }
                }
            }
            catch {}
        }
    }

    public class SensorReading
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Value { get; set; } = "Not Available";
        public string Unit { get; set; } = string.Empty;
        public string HealthState { get; set; } = "Unavailable";
        public string Status { get; set; } = "Not Supported";
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
