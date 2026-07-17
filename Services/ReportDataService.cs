using System;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using atp_enterprise_app_wpf.Models;

namespace atp_enterprise_app_wpf.Services
{
    public class ReportDataService
    {
        private static readonly Lazy<ReportDataService> _instance = new(() => new ReportDataService());
        public static ReportDataService Instance => _instance.Value;

        private ReportDataService() { }

        public AtpReportModel GetReportData(string sessionId, string generatedByOperatorId)
        {
            var db = TraceabilityDatabase.Instance;
            
            // 1. Fetch Session
            var sessions = db.GetSessions();
            var session = sessions.FirstOrDefault(s => s.SessionId == sessionId);
            if (session == null)
                throw new ArgumentException($"Session with ID {sessionId} not found.");

            // 2. We need the raw json to deserialize hardware snapshot
            // For now, let's just get tests
            var tests = db.GetTestsForSession(sessionId);

            // 3. We also need to get the actual JSON for HardwareSnapshot.
            // Since TraceabilityDatabase.GetSessions doesn't currently pull the JSON strings (to save memory),
            // I need to fetch it specifically or modify the query.
            var hardwareSnapshotJson = GetSessionJsonField(sessionId, "HardwareSnapshotJson");
            var systemInfo = new SystemInfo();
            if (!string.IsNullOrEmpty(hardwareSnapshotJson))
            {
                try { systemInfo = JsonSerializer.Deserialize<SystemInfo>(hardwareSnapshotJson) ?? new SystemInfo(); }
                catch { }
            }

            // 4. Audits (filter for this session)
            var allAudits = db.GetAuditEvents();
            var sessionAudits = allAudits.Where(a => a.SessionId == sessionId).OrderBy(a => a.Timestamp).ToList();

            // 5. Equipment (for MVP, let's just grab all active equipment if no specific session mapping is populated)
            // Ideally we'd pull from SessionEquipment, but since we didn't wire up specific equipment to specific sessions yet in UI,
            // we will pull the global equipment registry to simulate the "Equipment Used" list.
            var equipment = EquipmentRegistryService.Instance.GetAllEquipment();

            return new AtpReportModel
            {
                Session = session,
                HardwareSnapshot = systemInfo,
                Tests = tests,
                Equipment = equipment,
                Audits = sessionAudits,
                ReportGeneratedBy = generatedByOperatorId,
                ReportGeneratedAt = DateTime.Now
            };
        }

        private string GetSessionJsonField(string sessionId, string field)
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ATP_Enterprise", "Database", "traceability.db")}");
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT {field} FROM AtpSessions WHERE SessionId = @id";
            cmd.Parameters.AddWithValue("@id", sessionId);
            
            var result = cmd.ExecuteScalar();
            return result == DBNull.Value || result == null ? "" : result.ToString();
        }
    }
}
