using System;

namespace atp_enterprise_app_wpf.Models
{
    public class EquipmentRecord
    {
        public string EquipmentId { get; set; } = string.Empty; // e.g. "DMM-001"
        public string Description { get; set; } = string.Empty; // e.g. "Digital Multimeter"
        public string Manufacturer { get; set; } = string.Empty; // e.g. "Fluke"
        public string Model { get; set; } = string.Empty; // e.g. "87V"
        public string SerialNumber { get; set; } = string.Empty;
        
        public string CalibrationCertNumber { get; set; } = string.Empty;
        public DateTime? CalibrationDueDate { get; set; }
        
        public string Status { get; set; } = "Active"; // Active, Out for Cal, Retired
        public string Notes { get; set; } = string.Empty;

        // Helper property
        public bool IsCalibrationValid => CalibrationDueDate.HasValue && CalibrationDueDate.Value > DateTime.Now;
        public bool IsCalibrationExpiring => IsCalibrationValid && (CalibrationDueDate.Value - DateTime.Now).TotalDays <= 30;
    }
}
