using System.Collections.Generic;

namespace atp_enterprise_app_wpf.Models
{
    public class RealtimeMetrics
    {
        public double CpuUsagePercent { get; set; }
        public double RamTotalBytes { get; set; }
        public double RamFreeBytes { get; set; }
        public double RamUsagePercent { get; set; }
        
        public List<ThermalZoneReading> Temperatures { get; set; } = new();
        public List<NetworkStatsReading> NetworkStats { get; set; } = new();
    }

    public class ThermalZoneReading
    {
        public string Zone { get; set; } = string.Empty;
        public double TemperatureCelsius { get; set; }
    }

    public class NetworkStatsReading
    {
        public string Name { get; set; } = string.Empty;
        public long ReceivedBytes { get; set; }
        public long SentBytes { get; set; }
        public long ReceivedDiscarded { get; set; }
        public long ReceivedErrors { get; set; }
        public long SentDiscarded { get; set; }
        public long SentErrors { get; set; }
    }
}
