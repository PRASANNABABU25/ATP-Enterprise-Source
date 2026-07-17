using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using atp_enterprise_app_wpf.Models;
using atp_enterprise_app_wpf.Services;
using atp_enterprise_app_wpf.Views;

namespace atp_enterprise_app_wpf
{
    public partial class MainWindow : Window
    {
        // Views
        private readonly DashboardView _dashboardView = new();
        private readonly SystemInfoView _systemInfoView = new();
        private readonly TestExecutionView _testExecutionView = new();
        private readonly OpticalTestView _opticalTestView = new();
        private readonly ProcedureManagerView _procedureManagerView = new();
        private readonly FunctionalTestsView _functionalTestsView = new();
        private readonly DisplayTestsView _displayTestsView = new();
        private readonly StorageTestsView _storageTestsView = new();
        private readonly CommTestsView _commTestsView = new();
        private readonly EthernetTestsView _ethernetTestsView = new();
        private readonly NetworkPerformanceView _networkPerformanceView = new();
        private readonly FiberTestsView _fiberTestsView = new();
        private readonly HealthMonitorView _healthMonitorView = new();
        private readonly LogsView _logsView = new();
        private readonly ReportsView _reportsView = new();
        private readonly SettingsView _settingsView = new();
        private readonly TraceabilityView _traceabilityView = new();

        // Services
        private readonly WmiMonitorService _wmiService = new();
        private readonly DiskSpeedTestService _diskService = new();
        private readonly ReportService _reportService = new();
        private readonly ThroughputTestService _throughputService = new();



        // Data cache
        private SystemInfo? _staticInfo;
        private RealtimeMetrics? _currentMetrics;
        private readonly List<TestLog> _logs = new();
        private string _overallVerdict = "AWAITING TEST";



        public MainWindow()
        {
            InitializeComponent();
            InitializeViewsCallbacks();
            LoadStaticSystemData();

            // Select Dashboard by default
            LstNavigation.SelectedIndex = 0;

            // Start monitoring engine automatically
            MonitoringEngine.Instance.Start(
                _settingsView.RefreshIntervalMs,
                _settingsView.HistoryLength,
                _settingsView.CpuLimitWarn,
                _settingsView.CpuLimitCritical,
                _settingsView.RamLimitWarn,
                _settingsView.RamLimitCritical,
                _settingsView.TempLimitMax
            );

            // Subscribe to unified telemetry events
            MonitoringEngine.Instance.OnTelemetryUpdated += MainWindow_OnTelemetryUpdated;
        }

        private void InitializeViewsCallbacks()
        {
            // Connect logs actions
            _logsView.OnClearLogs = () =>
            {
                _logs.Clear();
                _logsView.Populate(_logs);
                _overallVerdict = "AWAITING TEST";
                UpdateVerdictBar();
            };

            _logsView.OnExportCSV = ExecuteExportCSV;

            // Connect reports actions
            _reportsView.OnGeneratePDF = (serial, project, customer, op) =>
            {
                ExecuteExportPDF(serial, project, customer, op);
            };

            // Connect settings saved callback
            _settingsView.OnSettingsSaved = () =>
            {
                TxtStatusMetadata.Text = $"UUT S/N: {_reportsView.SerialNumber} | Project: {_reportsView.ProjectId}";
                
                // Propagate settings parameters to the active engine if it is running
                if (MonitoringEngine.Instance.IsRunning)
                {
                    MonitoringEngine.Instance.Stop();
                    MonitoringEngine.Instance.Start(
                        _settingsView.RefreshIntervalMs,
                        _settingsView.HistoryLength,
                        _settingsView.CpuLimitWarn,
                        _settingsView.CpuLimitCritical,
                        _settingsView.RamLimitWarn,
                        _settingsView.RamLimitCritical,
                        _settingsView.TempLimitMax
                    );
                }
            };

            // Feed SettingsView instance to HealthMonitorView
            _healthMonitorView.Settings = _settingsView;
        }

        private void LoadStaticSystemData()
        {
            _staticInfo = new SystemInfo();
            _systemInfoView.HardwareScanCompleted += OnHardwareScanCompleted;
            
            TxtStatusMetadata.Text = $"UUT S/N: {_reportsView.SerialNumber} | Project: {_reportsView.ProjectId}";
            TxtOperatorTag.Text = _reportsView.OperatorId;
        }

        private void OnHardwareScanCompleted(SystemInfo info)
        {
            _staticInfo = info;

            // Unlock standard test pages
            NavItemFunctional.IsEnabled = true;
            NavItemDisplay.IsEnabled = true;
            NavItemStorage.IsEnabled = true;
            NavItemComm.IsEnabled = true;
            NavItemEthernet.IsEnabled = true;
            NavItemHealth.IsEnabled = true;

            // Pass down to Test Execution for Traceability snapshot
            _testExecutionView.SystemInfoContext = info;

            // Check 10G optical interface compatibility
            if (info.Compatibility.HasTenGigOptical)
            {
                NavItemFiber.Visibility = Visibility.Visible;
                NavItemFiber.IsEnabled = true;
            }
            else
            {
                NavItemFiber.Visibility = Visibility.Collapsed;
                NavItemFiber.IsEnabled = false;
            }

            // Sync serial info with reports view
            _reportsView.PopulateFromInventory(info);
            TxtStatusMetadata.Text = $"UUT S/N: {info.SystemSerialNumber} | Project: {_reportsView.ProjectId}";
        }

        private void MainWindow_OnTelemetryUpdated(RealtimeMetrics metrics, List<SensorReading> sensors)
        {
            Dispatcher.Invoke(() =>
            {
                _currentMetrics = metrics;

                // Extract temp
                double? firstTemp = null;
                var tempSensor = sensors.FirstOrDefault(s => s.Name == "CPU Core Temperature");
                if (tempSensor != null && tempSensor.Status == "Reporting")
                {
                    if (double.TryParse(tempSensor.Value, out double tVal))
                        firstTemp = tVal;
                }

                // Update gauges on active Dashboard
                _dashboardView.UpdateCpu(metrics.CpuUsagePercent);
                _dashboardView.UpdateRam(metrics.RamUsagePercent);
                _dashboardView.UpdateTemp(firstTemp);

                // Populate other pages if they are showing
                if (_staticInfo != null)
                {
                    _ethernetTestsView.Populate(_staticInfo, metrics);
                }
            });
        }

        private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstNavigation == null || WorkspaceHost == null) return;

            UserControl targetView = LstNavigation.SelectedIndex switch
            {
                0 => _dashboardView,
                1 => _systemInfoView,
                2 => _testExecutionView,
                3 => _functionalTestsView,
                4 => _displayTestsView,
                5 => _storageTestsView,
                6 => _commTestsView,
                7 => _ethernetTestsView,
                8 => _networkPerformanceView,
                9 => _fiberTestsView,
                10 => _healthMonitorView,
                11 => _logsView,
                12 => _reportsView,
                13 => _settingsView,
                14 => _procedureManagerView,
                15 => _traceabilityView,
                _ => _dashboardView
            };

            // Swap view
            WorkspaceHost.Content = targetView;

            // Trigger slide-fade storyboards
            if (Resources["PageTransition"] is Storyboard sb)
            {
                sb.Begin(this);
            }
        }

        // Custom window buttons callbacks
        private void MinWin_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaxWin_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseWin_Click(object sender, RoutedEventArgs e)
        {
            _fiberTestsView.StopSocketTest();
            Application.Current.Shutdown();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        // --- AUTOMATED ATP SEQUENCER (delegated to TestExecutionEngine) ---
        private void RunAll_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to the ATP Execution view
            LstNavigation.SelectedIndex = 2;
            _overallVerdict = "TESTING";
            UpdateVerdictBar();
        }

        private void Abort_Click(object sender, RoutedEventArgs e)
        {
            TestExecutionEngine.Instance.StopSession();
            _overallVerdict = "FAIL";
            UpdateVerdictBar();
        }

        private void UpdateVerdictBar()
        {
            TxtOverallVerdict.Text = _overallVerdict;
            if (_overallVerdict == "PASS")
                TxtOverallVerdict.Foreground = new SolidColorBrush(Color.FromRgb(82, 196, 26)); // Green
            else if (_overallVerdict == "FAIL")
                TxtOverallVerdict.Foreground = new SolidColorBrush(Color.FromRgb(184, 37, 37)); // Red
            else if (_overallVerdict == "TESTING")
                TxtOverallVerdict.Foreground = new SolidColorBrush(Color.FromRgb(37, 118, 217)); // Blue
            else
                TxtOverallVerdict.Foreground = new SolidColorBrush(Color.FromRgb(140, 156, 176)); // Gray
        }

        // Secondary command buttons
        private void BlinkLed_Click(object sender, RoutedEventArgs e)
        {
            _wmiService.BlinkCapsLockLed();
            MessageBox.Show("Sent blink lock toggles event!", "Command Bar");
        }

        private void Nav_Optical_Click(object sender, RoutedEventArgs e)
        {
            WorkspaceHost.Content = _opticalTestView;
        }

        private void Nav_Procedures_Click(object sender, RoutedEventArgs e)
        {
            WorkspaceHost.Content = _procedureManagerView;
        }

        private void QuickDisk_Click(object sender, RoutedEventArgs e)
        {
            LstNavigation.SelectedIndex = 5; // Select Storage Page (shifted +1)
        }

        private void ExportCSV_Click(object sender, RoutedEventArgs e)
        {
            ExecuteExportCSV();
        }

        private void ExportPDF_Click(object sender, RoutedEventArgs e)
        {
            ExecuteExportPDF(
                _reportsView.SerialNumber,
                _reportsView.ProjectId,
                _reportsView.CustomerName,
                _reportsView.OperatorId
            );
        }

        // File Exporters
        private void ExecuteExportCSV()
        {
            if (_staticInfo == null) return;

            var sfd = new SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv",
                FileName = $"atp-report-{_reportsView.SerialNumber}.csv"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    string csvContent = _reportService.GenerateCSVReport(
                        _staticInfo,
                        _logs,
                        _reportsView.SerialNumber,
                        _reportsView.ProjectId,
                        _reportsView.CustomerName,
                        _reportsView.OperatorId,
                        _overallVerdict
                    );
                    File.WriteAllText(sfd.FileName, csvContent);
                    MessageBox.Show("Excel CSV Compliance grid exported successfully!", "Export Completed");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"CSV write failed: {ex.Message}", "Write IO Fault", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteExportPDF(string serial, string project, string customer, string opName)
        {
            if (_staticInfo == null) return;

            var sfd = new SaveFileDialog
            {
                Filter = "PDF Document (*.pdf)|*.pdf",
                FileName = $"atp-report-{serial}.pdf"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    byte[] pdfBytes = _reportService.GeneratePDFReport(
                        _staticInfo,
                        _logs,
                        serial,
                        project,
                        customer,
                        opName,
                        _overallVerdict
                    );
                    File.WriteAllBytes(sfd.FileName, pdfBytes);
                    MessageBox.Show("compliance certificate PDF compiled and signed offline successfully!", "PDF Generation Completed");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"PDF stream compilation failed: {ex.Message}", "Compiler Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}