using System;
using System.Collections.Generic;

namespace atp_enterprise_app_wpf.Models
{
    public class AtpProcedure
    {
        public string ProcedureId { get; set; } = Guid.NewGuid().ToString();
        public string ProcedureTitle { get; set; } = "New ATP Procedure";
        public string Revision { get; set; } = "1.0.0";
        public string ProductFamily { get; set; } = "Generic";
        public string ApplicableHardware { get; set; } = "All";
        public string Customer { get; set; } = "Internal";
        
        public string Author { get; set; } = "Engineering";
        public string Approver { get; set; } = "Pending";
        public DateTime IssueDate { get; set; } = DateTime.Now;
        public string ChangeSummary { get; set; } = "Initial release";

        public List<TestDefinition> TestSequence { get; set; } = new();

        public AtpProcedure Clone()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(this);
            var cloned = System.Text.Json.JsonSerializer.Deserialize<AtpProcedure>(json) ?? new AtpProcedure();
            cloned.ProcedureId = Guid.NewGuid().ToString();
            cloned.Revision = "1.0.0";
            return cloned;
        }
    }
}
