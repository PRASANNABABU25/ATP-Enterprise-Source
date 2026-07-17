using System;

namespace atp_enterprise_app_wpf.Models
{
    public class TestLog
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string MeasuredValue { get; set; } = string.Empty;
        public string Specification { get; set; } = string.Empty;
        public string Tolerance { get; set; } = string.Empty;
        public string Status { get; set; } = "PASS"; // PASS / FAIL
        public string Timestamp { get; set; } = DateTime.Now.ToString("HH:mm:ss");
    }
}
