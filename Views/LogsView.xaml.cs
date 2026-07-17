using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using atp_enterprise_app_wpf.Models;

namespace atp_enterprise_app_wpf.Views
{
    public partial class LogsView : UserControl
    {
        public Action? OnClearLogs { get; set; }
        public Action? OnExportCSV { get; set; }

        public LogsView()
        {
            InitializeComponent();
        }

        public void Populate(List<TestLog> logs)
        {
            GridLogs.ItemsSource = null;
            GridLogs.ItemsSource = logs;
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            OnClearLogs?.Invoke();
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            OnExportCSV?.Invoke();
        }
    }
}
