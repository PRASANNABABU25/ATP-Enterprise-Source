using System;
using System.Collections.Generic;

namespace atp_enterprise_app_wpf.Models
{
    public class AtpSession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public string ATPNumber { get; set; } = string.Empty;
        public string ProjectNumber { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string ProductFamily { get; set; } = string.Empty;
        public string ProductModel { get; set; } = string.Empty;
        public string UnitSerialNumber { get; set; } = string.Empty;
        public string ProcedureId { get; set; } = string.Empty;
        public string ProcedureRevision { get; set; } = string.Empty;
        public string OperatorId { get; set; } = string.Empty;
        
        public DateTime StartTime { get; set; } = DateTime.Now;
        public DateTime? EndTime { get; set; }
        
        public string OverallResult { get; set; } = "PENDING"; // PASS, FAIL, PENDING, ABORTED
        
        // Relationships loaded by TraceabilityDatabase
        public List<SessionTestRecord> Tests { get; set; } = new();
        public List<SessionEquipment> EquipmentUsed { get; set; } = new();
        public List<AuditEvent> AuditEvents { get; set; } = new();
        
        // Serialized JSON
        public string HardwareSnapshotJson { get; set; } = string.Empty;
        public string MonitoringSummaryJson { get; set; } = string.Empty;
    }
}
