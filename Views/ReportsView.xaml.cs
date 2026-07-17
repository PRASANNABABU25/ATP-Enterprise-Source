using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Collections.Generic;
using Microsoft.Win32;
using atp_enterprise_app_wpf.Models;
using atp_enterprise_app_wpf.Services;
using atp_enterprise_app_wpf.Services.Reporting;

namespace atp_enterprise_app_wpf.Views
{
    public partial class ReportsView : UserControl
    {
        private List<AtpSession> _allSessions = new();
        private AtpReportModel? _currentReportData;
        private IReportGenerator _reportGenerator = new AtpFullReportGenerator();

        // Used by MainWindow temporarily if needed to push data to reports (legacy support)
        public string OperatorId { get; set; } = "OPERATOR-01";
        public string ProjectId { get; set; } = "PRJ-RUGGED-2026";
        public string SerialNumber { get; set; } = "";
        public string CustomerName { get; set; } = "US-Aviation-QA";

        public FlowDocument? CurrentPreviewDocument
        {
            get => (FlowDocument)GetValue(CurrentPreviewDocumentProperty);
            set => SetValue(CurrentPreviewDocumentProperty, value);
        }
        public static readonly DependencyProperty CurrentPreviewDocumentProperty =
            DependencyProperty.Register("CurrentPreviewDocument", typeof(FlowDocument), typeof(ReportsView), new PropertyMetadata(null));

        public ReportsView()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSessions();
        }

        private void LoadSessions()
        {
            _allSessions = TraceabilityDatabase.Instance.GetSessions().OrderByDescending(s => s.StartTime).ToList();
            LstSessions.ItemsSource = _allSessions;
            if (_allSessions.Any())
                LstSessions.SelectedIndex = 0;
        }

        private void TxtSearchSession_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = TxtSearchSession.Text.ToLower();
            if (string.IsNullOrWhiteSpace(query))
            {
                LstSessions.ItemsSource = _allSessions;
            }
            else
            {
                LstSessions.ItemsSource = _allSessions.Where(s => 
                    (s.UnitSerialNumber?.ToLower().Contains(query) ?? false) || 
                    (s.SessionId?.ToLower().Contains(query) ?? false)
                ).ToList();
            }
        }

        private void LstSessions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Auto generate preview when selection changes
            GeneratePreview_Click(this, new RoutedEventArgs());
        }

        private void GeneratePreview_Click(object sender, RoutedEventArgs e)
        {
            if (LstSessions.SelectedItem is AtpSession session)
            {
                try
                {
                    _currentReportData = ReportDataService.Instance.GetReportData(session.SessionId, OperatorId);
                    CurrentPreviewDocument = _reportGenerator.GeneratePreview(_currentReportData);
                    PreviewViewer.Document = CurrentPreviewDocument;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to generate report preview:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_currentReportData == null) return;
            
            var sfd = new SaveFileDialog { Filter = "PDF Document (*.pdf)|*.pdf", FileName = $"ATP_Report_{_currentReportData.Session.UnitSerialNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf" };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var bytes = _reportGenerator.GeneratePdf(_currentReportData);
                    File.WriteAllBytes(sfd.FileName, bytes);
                    MessageBox.Show("PDF Generated Successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        private void ExportDocx_Click(object sender, RoutedEventArgs e)
        {
            if (_currentReportData == null) return;
            
            var sfd = new SaveFileDialog { Filter = "Word Document (*.docx)|*.docx", FileName = $"ATP_Report_{_currentReportData.Session.UnitSerialNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.docx" };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var bytes = _reportGenerator.GenerateDocx(_currentReportData);
                    File.WriteAllBytes(sfd.FileName, bytes);
                    MessageBox.Show("DOCX Generated Successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        private void ExportXlsx_Click(object sender, RoutedEventArgs e)
        {
            if (_currentReportData == null) return;
            
            var sfd = new SaveFileDialog { Filter = "Excel Document (*.xlsx)|*.xlsx", FileName = $"ATP_Report_{_currentReportData.Session.UnitSerialNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx" };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var bytes = _reportGenerator.GenerateXlsx(_currentReportData);
                    File.WriteAllBytes(sfd.FileName, bytes);
                    MessageBox.Show("XLSX Generated Successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_currentReportData == null) return;
            
            var sfd = new SaveFileDialog { Filter = "CSV File (*.csv)|*.csv", FileName = $"ATP_Report_{_currentReportData.Session.UnitSerialNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.csv" };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var csv = _reportGenerator.GenerateCsv(_currentReportData);
                    File.WriteAllText(sfd.FileName, csv);
                    MessageBox.Show("CSV Generated Successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        // Legacy compatibility for MainWindow.xaml.cs
        public void PopulateFromInventory(SystemInfo info)
        {
            if (info != null && !string.IsNullOrEmpty(info.SystemSerialNumber))
            {
                SerialNumber = info.SystemSerialNumber;
            }
        }
        
        public Action<string, string, string, string>? OnGeneratePDF { get; set; }
    }
}
