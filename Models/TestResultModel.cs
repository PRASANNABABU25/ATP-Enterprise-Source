using System;
using System.Collections.Generic;

namespace atp_enterprise_app_wpf.Models
{
    public enum TestExecutionState
    {
        NotStarted,
        Queued,
        Initializing,
        Running,
        Evaluating,
        Completed
    }

    public enum TestOutcome
    {
        Pending,
        Pass,
        Fail,
        Skipped,
        Error
    }

    public class TestRunResult
    {
        public string MeasuredValue { get; set; } = string.Empty;
        public TestOutcome Outcome { get; set; } = TestOutcome.Pending;
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan Duration => (EndTime ?? DateTime.Now) - (StartTime ?? DateTime.Now);
        public string ErrorDetails { get; set; } = string.Empty;
        
        // Traceability Extensions
        public string Category { get; set; } = string.Empty;
        public string Specification { get; set; } = string.Empty;
        public int RetryCount { get; set; } = 0;
        
        // List of health events (Warning/Critical) that occurred during the test
        public List<string> HealthEvents { get; set; } = new();
    }
}
