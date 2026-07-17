using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using atp_enterprise_app_wpf.Models;
using atp_enterprise_app_wpf.Services;

namespace atp_enterprise_app_wpf.Views
{
    public partial class TraceabilityView : UserControl
    {
        public TraceabilityView()
        {
            InitializeComponent();
            Loaded += TraceabilityView_Loaded;
        }

        private void TraceabilityView_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshAll();
        }

        private void RefreshAll()
        {
            LoadSessions();
            LoadEquipment();
            LoadAuditTrail();
        }

        // --- Tab 1: Sessions ---

        private void BtnRefreshSessions_Click(object sender, RoutedEventArgs e)
        {
            LoadSessions();
        }

        private void LoadSessions()
        {
            try
            {
                var sessions = TraceabilityDatabase.Instance.GetSessions();
                GridSessions.ItemsSource = sessions;
                PanelSessionDetail.Visibility = Visibility.Hidden;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading sessions: {ex.Message}");
            }
        }

        private void GridSessions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridSessions.SelectedItem is AtpSession session)
            {
                PanelSessionDetail.Visibility = Visibility.Visible;
                
                TxtDetId.Text = session.SessionId.Substring(0, 8) + "...";
                TxtDetProc.Text = session.ProcedureId;
                TxtDetOp.Text = session.OperatorId;
                TxtDetProj.Text = session.ProjectNumber;
                TxtDetStart.Text = session.StartTime.ToString("MM/dd/yy HH:mm");
                TxtDetEnd.Text = session.EndTime?.ToString("MM/dd/yy HH:mm") ?? "N/A";

                var tests = TraceabilityDatabase.Instance.GetTestsForSession(session.SessionId);
                GridSessionTests.ItemsSource = tests;
            }
            else
            {
                PanelSessionDetail.Visibility = Visibility.Hidden;
            }
        }

        // --- Tab 2: Equipment ---

        private void LoadEquipment()
        {
            try
            {
                var eq = EquipmentRegistryService.Instance.GetAllEquipment();
                GridEquipment.ItemsSource = eq;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading equipment: {ex.Message}");
            }
        }

        private void BtnAddEquipment_Click(object sender, RoutedEventArgs e)
        {
            // For MVP, just add a dummy piece of equipment to show functionality
            var dummy = new EquipmentRecord
            {
                EquipmentId = $"DMM-{new Random().Next(100, 999)}",
                Description = "Digital Multimeter",
                Manufacturer = "Fluke",
                Model = "87V",
                SerialNumber = Guid.NewGuid().ToString().Substring(0, 8).ToUpper(),
                CalibrationCertNumber = "CERT-" + new Random().Next(1000, 9999),
                CalibrationDueDate = DateTime.Now.AddDays(new Random().Next(-10, 365)), // Mix of valid and expired
                Status = "Active"
            };
            
            EquipmentRegistryService.Instance.AddEquipment(dummy);
            LoadEquipment();
            LoadAuditTrail();
        }

        private void BtnDeleteEquipment_Click(object sender, RoutedEventArgs e)
        {
            if (GridEquipment.SelectedItem is EquipmentRecord eq)
            {
                EquipmentRegistryService.Instance.DeleteEquipment(eq.EquipmentId);
                LoadEquipment();
                LoadAuditTrail();
            }
        }

        // --- Tab 3: Audit Trail ---

        private void BtnRefreshAudit_Click(object sender, RoutedEventArgs e)
        {
            LoadAuditTrail();
        }

        private void LoadAuditTrail()
        {
            try
            {
                var events = TraceabilityDatabase.Instance.GetAuditEvents();
                GridAudit.ItemsSource = events;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading audit trail: {ex.Message}");
            }
        }
    }
}
