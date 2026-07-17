using System;

namespace atp_enterprise_app_wpf.Models
{
    public class SessionEquipment
    {
        public int Id { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string EquipmentId { get; set; } = string.Empty;
        public string UsageNote { get; set; } = string.Empty; // e.g. "Used for test STG_002"
    }

    public class AuditEvent
    {
        public int Id { get; set; }
        public string? SessionId { get; set; } // Nullable, as some events happen outside a session
        public string EventType { get; set; } = string.Empty; // e.g. SessionStarted, ProcedureSelected, TestCompleted
        public string Actor { get; set; } = string.Empty; // OperatorId or "System"
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
