using System.Collections.Generic;

namespace atp_enterprise_app_wpf.Models
{
    public class TestDefinition
    {
        public string TestId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Prerequisites { get; set; } = new();
        public string ExpectedSpecification { get; set; } = string.Empty;
        
        // Stage 8 additions for dynamic limit parsing
        public double? MinimumNumericValue { get; set; }
        public double? MaximumNumericValue { get; set; }
        public string? ExpectedStringValue { get; set; }
        
        public int TimeoutMs { get; set; } = 30000;
        public int RetryPolicy { get; set; } = 0;
        public string ApplicableHardware { get; set; } = "All";
        public bool IsEnabled { get; set; } = true;
        
        // Stage 8 additions for procedure management
        public bool IsMandatory { get; set; } = false;
        public string ConditionalExecutionRule { get; set; } = "None"; // e.g. "RequiresGPS", "Requires10G"
    }
}
