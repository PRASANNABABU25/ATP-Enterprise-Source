using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using atp_enterprise_app_wpf.Models;
using System.IO;

namespace atp_enterprise_app_wpf.Services
{
    public class EquipmentRegistryService
    {
        private static readonly Lazy<EquipmentRegistryService> _instance = new(() => new EquipmentRegistryService());
        public static EquipmentRegistryService Instance => _instance.Value;

        private readonly string _dbPath;

        private EquipmentRegistryService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appData, "ATP_Enterprise", "Database");
            _dbPath = Path.Combine(appFolder, "traceability.db");
        }

        public void AddEquipment(EquipmentRecord equipment)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Equipment (
                    EquipmentId, Description, Manufacturer, Model, SerialNumber, 
                    CalibrationCertNumber, CalibrationDueDate, Status, Notes
                ) VALUES (
                    @id, @desc, @mfg, @model, @serial, @cert, @due, @status, @notes
                )";
            cmd.Parameters.AddWithValue("@id", equipment.EquipmentId);
            cmd.Parameters.AddWithValue("@desc", equipment.Description);
            cmd.Parameters.AddWithValue("@mfg", equipment.Manufacturer);
            cmd.Parameters.AddWithValue("@model", equipment.Model);
            cmd.Parameters.AddWithValue("@serial", equipment.SerialNumber);
            cmd.Parameters.AddWithValue("@cert", equipment.CalibrationCertNumber);
            cmd.Parameters.AddWithValue("@due", equipment.CalibrationDueDate?.ToString("o") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@status", equipment.Status);
            cmd.Parameters.AddWithValue("@notes", equipment.Notes);
            
            cmd.ExecuteNonQuery();
            
            TraceabilityDatabase.Instance.LogAuditEvent(null, "EquipmentAdded", "System", $"Added equipment {equipment.EquipmentId}");
        }

        public List<EquipmentRecord> GetAllEquipment()
        {
            var list = new List<EquipmentRecord>();
            if (!File.Exists(_dbPath)) return list;

            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM Equipment ORDER BY EquipmentId ASC";
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var record = new EquipmentRecord
                {
                    EquipmentId = reader.GetString(reader.GetOrdinal("EquipmentId")),
                    Description = reader.GetString(reader.GetOrdinal("Description")),
                    Manufacturer = reader.GetString(reader.GetOrdinal("Manufacturer")),
                    Model = reader.GetString(reader.GetOrdinal("Model")),
                    SerialNumber = reader.GetString(reader.GetOrdinal("SerialNumber")),
                    CalibrationCertNumber = reader.IsDBNull(reader.GetOrdinal("CalibrationCertNumber")) ? "" : reader.GetString(reader.GetOrdinal("CalibrationCertNumber")),
                    Status = reader.GetString(reader.GetOrdinal("Status")),
                    Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? "" : reader.GetString(reader.GetOrdinal("Notes"))
                };

                if (!reader.IsDBNull(reader.GetOrdinal("CalibrationDueDate")))
                {
                    record.CalibrationDueDate = DateTime.Parse(reader.GetString(reader.GetOrdinal("CalibrationDueDate")));
                }

                list.Add(record);
            }
            return list;
        }

        public void DeleteEquipment(string equipmentId)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Equipment WHERE EquipmentId = @id";
            cmd.Parameters.AddWithValue("@id", equipmentId);
            cmd.ExecuteNonQuery();
            
            TraceabilityDatabase.Instance.LogAuditEvent(null, "EquipmentDeleted", "System", $"Deleted equipment {equipmentId}");
        }
    }
}
