using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using atp_enterprise_app_wpf.Models;

namespace atp_enterprise_app_wpf.Services
{
    public class TraceabilityDatabase
    {
        private static readonly Lazy<TraceabilityDatabase> _instance = new(() => new TraceabilityDatabase());
        public static TraceabilityDatabase Instance => _instance.Value;

        private readonly string _dbPath;

        private TraceabilityDatabase()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appData, "ATP_Enterprise", "Database");
            Directory.CreateDirectory(appFolder);
            
            _dbPath = Path.Combine(appFolder, "traceability.db");
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS AtpSessions (
                    SessionId TEXT PRIMARY KEY,
                    ATPNumber TEXT,
                    ProjectNumber TEXT,
                    Customer TEXT,
                    ProductFamily TEXT,
                    ProductModel TEXT,
                    UnitSerialNumber TEXT,
                    ProcedureId TEXT,
                    ProcedureRevision TEXT,
                    OperatorId TEXT,
                    StartTime TEXT,
                    EndTime TEXT,
                    OverallResult TEXT,
                    HardwareSnapshotJson TEXT,
                    MonitoringSummaryJson TEXT
                );

                CREATE TABLE IF NOT EXISTS SessionTests (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SessionId TEXT,
                    TestId TEXT,
                    TestName TEXT,
                    Category TEXT,
                    Specification TEXT,
                    MeasuredValue TEXT,
                    AcceptanceCriteria TEXT,
                    Outcome TEXT,
                    StartTime TEXT,
                    EndTime TEXT,
                    RetryCount INTEGER,
                    ErrorDetails TEXT,
                    HealthEventsJson TEXT,
                    FOREIGN KEY(SessionId) REFERENCES AtpSessions(SessionId)
                );

                CREATE TABLE IF NOT EXISTS Equipment (
                    EquipmentId TEXT PRIMARY KEY,
                    Description TEXT,
                    Manufacturer TEXT,
                    Model TEXT,
                    SerialNumber TEXT,
                    CalibrationCertNumber TEXT,
                    CalibrationDueDate TEXT,
                    Status TEXT,
                    Notes TEXT
                );

                CREATE TABLE IF NOT EXISTS SessionEquipment (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SessionId TEXT,
                    EquipmentId TEXT,
                    UsageNote TEXT,
                    FOREIGN KEY(SessionId) REFERENCES AtpSessions(SessionId),
                    FOREIGN KEY(EquipmentId) REFERENCES Equipment(EquipmentId)
                );

                CREATE TABLE IF NOT EXISTS AuditEvents (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SessionId TEXT,
                    EventType TEXT,
                    Actor TEXT,
                    Description TEXT,
                    Timestamp TEXT DEFAULT CURRENT_TIMESTAMP
                );
            ";
            command.ExecuteNonQuery();
        }

        public void CreateSession(AtpSession session)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            using var transaction = connection.BeginTransaction();
            
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                INSERT INTO AtpSessions (
                    SessionId, ATPNumber, ProjectNumber, Customer, ProductFamily, ProductModel,
                    UnitSerialNumber, ProcedureId, ProcedureRevision, OperatorId, StartTime, OverallResult
                ) VALUES (
                    @id, @atp, @project, @customer, @family, @model,
                    @serial, @procId, @procRev, @operator, @start, @result
                )";
            cmd.Parameters.AddWithValue("@id", session.SessionId);
            cmd.Parameters.AddWithValue("@atp", session.ATPNumber);
            cmd.Parameters.AddWithValue("@project", session.ProjectNumber);
            cmd.Parameters.AddWithValue("@customer", session.Customer);
            cmd.Parameters.AddWithValue("@family", session.ProductFamily);
            cmd.Parameters.AddWithValue("@model", session.ProductModel);
            cmd.Parameters.AddWithValue("@serial", session.UnitSerialNumber);
            cmd.Parameters.AddWithValue("@procId", session.ProcedureId);
            cmd.Parameters.AddWithValue("@procRev", session.ProcedureRevision);
            cmd.Parameters.AddWithValue("@operator", session.OperatorId);
            cmd.Parameters.AddWithValue("@start", session.StartTime.ToString("o"));
            cmd.Parameters.AddWithValue("@result", session.OverallResult);
            
            cmd.ExecuteNonQuery();
            transaction.Commit();
        }

        public void FinalizeSession(string sessionId, string overallResult)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE AtpSessions 
                SET EndTime = @end, OverallResult = @result 
                WHERE SessionId = @id";
            cmd.Parameters.AddWithValue("@id", sessionId);
            cmd.Parameters.AddWithValue("@end", DateTime.Now.ToString("o"));
            cmd.Parameters.AddWithValue("@result", overallResult);
            
            cmd.ExecuteNonQuery();
        }

        public void RecordTestResult(SessionTestRecord record)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO SessionTests (
                    SessionId, TestId, TestName, Category, Specification, MeasuredValue, 
                    AcceptanceCriteria, Outcome, StartTime, EndTime, RetryCount, ErrorDetails, HealthEventsJson
                ) VALUES (
                    @sessionId, @testId, @testName, @cat, @spec, @measured, 
                    @criteria, @outcome, @start, @end, @retry, @err, @health
                )";
            cmd.Parameters.AddWithValue("@sessionId", record.SessionId);
            cmd.Parameters.AddWithValue("@testId", record.TestId);
            cmd.Parameters.AddWithValue("@testName", record.TestName);
            cmd.Parameters.AddWithValue("@cat", record.Category);
            cmd.Parameters.AddWithValue("@spec", record.Specification);
            cmd.Parameters.AddWithValue("@measured", record.MeasuredValue);
            cmd.Parameters.AddWithValue("@criteria", record.AcceptanceCriteria);
            cmd.Parameters.AddWithValue("@outcome", record.Outcome);
            cmd.Parameters.AddWithValue("@start", record.StartTime.ToString("o"));
            cmd.Parameters.AddWithValue("@end", record.EndTime.ToString("o"));
            cmd.Parameters.AddWithValue("@retry", record.RetryCount);
            cmd.Parameters.AddWithValue("@err", record.ErrorDetails);
            cmd.Parameters.AddWithValue("@health", record.HealthEventsJson);
            
            cmd.ExecuteNonQuery();
        }

        public void SaveHardwareSnapshot(string sessionId, SystemInfo info)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            
            string json = JsonSerializer.Serialize(info);
            
            var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE AtpSessions SET HardwareSnapshotJson = @json WHERE SessionId = @id";
            cmd.Parameters.AddWithValue("@id", sessionId);
            cmd.Parameters.AddWithValue("@json", json);
            cmd.ExecuteNonQuery();
        }

        public void SaveMonitoringSummary(string sessionId, string summaryJson)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            
            var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE AtpSessions SET MonitoringSummaryJson = @json WHERE SessionId = @id";
            cmd.Parameters.AddWithValue("@id", sessionId);
            cmd.Parameters.AddWithValue("@json", summaryJson);
            cmd.ExecuteNonQuery();
        }

        public void LogAuditEvent(string? sessionId, string eventType, string actor, string description)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO AuditEvents (SessionId, EventType, Actor, Description, Timestamp) 
                VALUES (@sessionId, @eventType, @actor, @description, @timestamp)";
            cmd.Parameters.AddWithValue("@sessionId", sessionId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@eventType", eventType);
            cmd.Parameters.AddWithValue("@actor", actor);
            cmd.Parameters.AddWithValue("@description", description);
            cmd.Parameters.AddWithValue("@timestamp", DateTime.Now.ToString("o"));
            
            cmd.ExecuteNonQuery();
        }

        public List<AtpSession> GetSessions()
        {
            var sessions = new List<AtpSession>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM AtpSessions ORDER BY StartTime DESC";
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var session = new AtpSession
                {
                    SessionId = reader.GetString(reader.GetOrdinal("SessionId")),
                    ATPNumber = reader.IsDBNull(reader.GetOrdinal("ATPNumber")) ? "" : reader.GetString(reader.GetOrdinal("ATPNumber")),
                    ProjectNumber = reader.IsDBNull(reader.GetOrdinal("ProjectNumber")) ? "" : reader.GetString(reader.GetOrdinal("ProjectNumber")),
                    Customer = reader.IsDBNull(reader.GetOrdinal("Customer")) ? "" : reader.GetString(reader.GetOrdinal("Customer")),
                    UnitSerialNumber = reader.IsDBNull(reader.GetOrdinal("UnitSerialNumber")) ? "" : reader.GetString(reader.GetOrdinal("UnitSerialNumber")),
                    ProcedureId = reader.IsDBNull(reader.GetOrdinal("ProcedureId")) ? "" : reader.GetString(reader.GetOrdinal("ProcedureId")),
                    OperatorId = reader.IsDBNull(reader.GetOrdinal("OperatorId")) ? "" : reader.GetString(reader.GetOrdinal("OperatorId")),
                    StartTime = DateTime.Parse(reader.GetString(reader.GetOrdinal("StartTime"))),
                    OverallResult = reader.IsDBNull(reader.GetOrdinal("OverallResult")) ? "" : reader.GetString(reader.GetOrdinal("OverallResult"))
                };
                
                if (!reader.IsDBNull(reader.GetOrdinal("EndTime")))
                {
                    session.EndTime = DateTime.Parse(reader.GetString(reader.GetOrdinal("EndTime")));
                }
                
                sessions.Add(session);
            }
            return sessions;
        }

        public List<SessionTestRecord> GetTestsForSession(string sessionId)
        {
            var tests = new List<SessionTestRecord>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM SessionTests WHERE SessionId = @id ORDER BY Id ASC";
            cmd.Parameters.AddWithValue("@id", sessionId);
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var test = new SessionTestRecord
                {
                    TestId = reader.GetString(reader.GetOrdinal("TestId")),
                    TestName = reader.IsDBNull(reader.GetOrdinal("TestName")) ? "" : reader.GetString(reader.GetOrdinal("TestName")),
                    Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? "" : reader.GetString(reader.GetOrdinal("Category")),
                    Outcome = reader.GetString(reader.GetOrdinal("Outcome")),
                    MeasuredValue = reader.IsDBNull(reader.GetOrdinal("MeasuredValue")) ? "" : reader.GetString(reader.GetOrdinal("MeasuredValue")),
                    Specification = reader.IsDBNull(reader.GetOrdinal("Specification")) ? "" : reader.GetString(reader.GetOrdinal("Specification")),
                    ErrorDetails = reader.IsDBNull(reader.GetOrdinal("ErrorDetails")) ? "" : reader.GetString(reader.GetOrdinal("ErrorDetails"))
                };
                
                if (!reader.IsDBNull(reader.GetOrdinal("StartTime")))
                    test.StartTime = DateTime.Parse(reader.GetString(reader.GetOrdinal("StartTime")));
                if (!reader.IsDBNull(reader.GetOrdinal("EndTime")))
                    test.EndTime = DateTime.Parse(reader.GetString(reader.GetOrdinal("EndTime")));
                    
                tests.Add(test);
            }
            return tests;
        }

        public List<AuditEvent> GetAuditEvents()
        {
            var events = new List<AuditEvent>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM AuditEvents ORDER BY Timestamp DESC LIMIT 1000";
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                events.Add(new AuditEvent
                {
                    SessionId = reader.IsDBNull(reader.GetOrdinal("SessionId")) ? null : reader.GetString(reader.GetOrdinal("SessionId")),
                    EventType = reader.GetString(reader.GetOrdinal("EventType")),
                    Actor = reader.GetString(reader.GetOrdinal("Actor")),
                    Description = reader.GetString(reader.GetOrdinal("Description")),
                    Timestamp = DateTime.Parse(reader.GetString(reader.GetOrdinal("Timestamp")))
                });
            }
            return events;
        }
    }
}
