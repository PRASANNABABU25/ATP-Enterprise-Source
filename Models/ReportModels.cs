using System;
using System.Collections.Generic;
using System.Text.Json;

namespace atp_enterprise_app_wpf.Models
{
    public class AtpReportModel
    {
        public AtpSession Session { get; set; } = new();
        public SystemInfo HardwareSnapshot { get; set; } = new();
        public List<SessionTestRecord> Tests { get; set; } = new();
        public List<EquipmentRecord> Equipment { get; set; } = new();
        public List<AuditEvent> Audits { get; set; } = new();
        
        public string ReportGeneratedBy { get; set; } = string.Empty;
        public DateTime ReportGeneratedAt { get; set; } = DateTime.Now;
        public string ReportVersion { get; set; } = "1.0.0";
    }
}
