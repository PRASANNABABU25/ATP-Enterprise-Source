using System;

namespace atp_enterprise_app_wpf.Models
{
    public class SessionTestRecord
    {
        public int Id { get; set; }
        public string SessionId { get; set; } = string.Empty;
        
        public string TestId { get; set; } = string.Empty;
        public string TestName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        
        public string Specification { get; set; } = string.Empty;
        public string MeasuredValue { get; set; } = string.Empty;
        public string AcceptanceCriteria { get; set; } = string.Empty; // Description of the limits applied
        
        public string Outcome { get; set; } = string.Empty; // Pass, Fail, Error, Skipped
        
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        
        public int RetryCount { get; set; } = 0;
        public string ErrorDetails { get; set; } = string.Empty;
        public string HealthEventsJson { get; set; } = "[]"; // Serialized list of health events
    }
}
