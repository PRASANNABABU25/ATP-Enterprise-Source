using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using atp_enterprise_app_wpf.Models;
using atp_enterprise_app_wpf.Services;
using atp_enterprise_app_wpf.Services.TestModules;
using System.ComponentModel;

namespace atp_enterprise_app_wpf.Views
{
    public class TestQueueItemViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        
        public string TestId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;

        private TestExecutionState _state = TestExecutionState.NotStarted;
        public TestExecutionState State
        {
            get => _state;
            set { _state = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StateString))); }
        }
        public string StateString => State.ToString();

        private TestOutcome _outcome = TestOutcome.Pending;
        public TestOutcome Outcome
        {
            get => _outcome;
            set { _outcome = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OutcomeString))); }
        }
        public string OutcomeString => Outcome.ToString();

        private TimeSpan _duration = TimeSpan.Zero;
        public TimeSpan Duration
        {
            get => _duration;
            set { _duration = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DurationString))); }
        }
        public string DurationString => $"{Duration.TotalMilliseconds:F0} ms";
    }

    public partial class TestExecutionView : UserControl
    {
        private ObservableCollection<TestQueueItemViewModel> _viewModels = new();

        public TestExecutionView()
        {
            InitializeComponent();
            RegisterTestModules();
            
            // Subscribe to engine events
            var engine = TestExecutionEngine.Instance;
            engine.OnTestStateChanged += Engine_OnTestStateChanged;
            engine.OnTestProgressUpdated += Engine_OnTestProgressUpdated;
            engine.OnSessionCompleted += Engine_OnSessionCompleted;

            GridTestQueue.ItemsSource = _viewModels;

            // Load available procedures into ComboBox
            PopulateProceduresComboBox();
        }

        private void PopulateProceduresComboBox()
        {
            var procedures = ProcedureLoaderService.Instance.ListProcedures();
            CmbProcedures.ItemsSource = procedures;
            if (procedures.Any())
            {
                CmbProcedures.SelectedIndex = 0;
            }
        }

        private void CmbProcedures_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbProcedures.SelectedItem is AtpProcedure procedure)
            {
                TestExecutionEngine.Instance.LoadProcedure(procedure);
                RefreshQueueGrid();
            }
        }

        private void RegisterTestModules()
        {
            var engine = TestExecutionEngine.Instance;
            
            // Phase 1 - Basic Diagnostics
            engine.RegisterTestModule("PWR_001", new PowerTestModule());
            engine.RegisterTestModule("BOOT_001", new BootTestModule());
            engine.RegisterTestModule("CPU_001", new CpuTestModule());
            engine.RegisterTestModule("RAM_001", new RamTestModule());
            engine.RegisterTestModule("STG_001", new StorageDetectionModule());
            
            // Phase 2 - Functional Interfaces
            engine.RegisterTestModule("USB_001", new UsbTestModule());
            engine.RegisterTestModule("NET_001", new EthernetLinkModule());
            engine.RegisterTestModule("COM_001", new SerialPortModule());
            engine.RegisterTestModule("DSP_001", new DisplayOutputModule());

            // Phase 3 - Performance & Subsystems
            engine.RegisterTestModule("STG_002", new StoragePerformanceModule());
            engine.RegisterTestModule("NET_002", new NetworkThroughputModule());
            engine.RegisterTestModule("RTC_001", new RtcDriftModule());
            engine.RegisterTestModule("ENV_001", new ThermalSensorModule());

            // Phase 4 - Active Communication
            engine.RegisterTestModule("COM_002", new SerialActiveModule());
            engine.RegisterTestModule("NET_003", new NetworkActiveModule());
            engine.RegisterTestModule("USB_002", new UsbActiveModule());
            engine.RegisterTestModule("STG_003", new StorageIntegrityModule());
            engine.RegisterTestModule("GPS_001", new GpsActiveModule());
            engine.RegisterTestModule("AUD_001", new AudioActiveModule());
            
            // Phase 5 - 10G Optical Tests
            engine.RegisterTestModule("OPT_001", new OpticalAdapterDetectionModule());
            engine.RegisterTestModule("OPT_002", new OpticalTransceiverModule());
            engine.RegisterTestModule("OPT_003", new OpticalLinkModule());
        }

        private void RefreshQueueGrid()
        {
            _viewModels.Clear();
            var engine = TestExecutionEngine.Instance;
            if (engine.ActiveProcedure != null)
            {
                foreach (var test in engine.ActiveProcedure.TestSequence)
                {
                    _viewModels.Add(new TestQueueItemViewModel { TestId = test.TestId, Name = test.Name, Category = test.Category });
                }
            }
            UpdateCounters();
        }

        private void Engine_OnTestStateChanged(TestDefinition test, TestExecutionState state, TestRunResult? result)
        {
            Dispatcher.Invoke(() =>
            {
                var vm = _viewModels.FirstOrDefault(v => v.TestId == test.TestId);
                if (vm != null)
                {
                    vm.State = state;
                    if (result != null)
                    {
                        vm.Outcome = result.Outcome;
                        vm.Duration = result.EndTime - result.StartTime ?? TimeSpan.Zero;
                    }
                }

                if (state == TestExecutionState.Running || state == TestExecutionState.Evaluating)
                {
                    TxtActiveTestName.Text = test.Name;
                    TxtActiveTestDesc.Text = test.Description;
                    TxtActiveTestState.Text = state.ToString().ToUpper();
                    TxtActiveTestSpec.Text = test.ExpectedSpecification;

                    if (state == TestExecutionState.Evaluating)
                    {
                        ProgressBar.IsIndeterminate = true;
                    }
                }
                else if (state == TestExecutionState.Completed)
                {
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = 100;
                    UpdateCounters();
                }
            });
        }

        private void Engine_OnTestProgressUpdated(string message, int percent, TimeSpan elapsed)
        {
            Dispatcher.Invoke(() =>
            {
                TxtActiveTestState.Text = message;
                if (percent >= 0)
                {
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = percent;
                }
                else
                {
                    ProgressBar.IsIndeterminate = true;
                }
            });
        }

        private void Engine_OnSessionCompleted()
        {
            Dispatcher.Invoke(() =>
            {
                TxtActiveTestName.Text = "Sequence Completed";
                TxtActiveTestDesc.Text = "-";
                TxtActiveTestState.Text = "IDLE";
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Value = 0;

                BtnStart.IsEnabled = true;
                BtnPause.IsEnabled = false;
                BtnResume.IsEnabled = false;
                BtnStop.IsEnabled = false;
                CmbProcedures.IsEnabled = true;
            });
        }

        public SystemInfo? SystemInfoContext { get; set; }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            BtnStart.IsEnabled = false;
            BtnPause.IsEnabled = true;
            BtnStop.IsEnabled = true;
            CmbProcedures.IsEnabled = false;

            // Reset UI states
            RefreshQueueGrid();

            await TestExecutionEngine.Instance.StartSessionAsync();
            
            // Capture hardware snapshot
            if (SystemInfoContext != null)
            {
                TraceabilityDatabase.Instance.SaveHardwareSnapshot(TestExecutionEngine.Instance.CurrentSessionId, SystemInfoContext);
            }
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            TestExecutionEngine.Instance.PauseSession();
            BtnPause.IsEnabled = false;
            BtnResume.IsEnabled = true;
            TxtActiveTestState.Text = "PAUSING...";
        }

        private void Resume_Click(object sender, RoutedEventArgs e)
        {
            TestExecutionEngine.Instance.ResumeSession();
            BtnResume.IsEnabled = false;
            BtnPause.IsEnabled = true;
            TxtActiveTestState.Text = "RESUMING...";
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            TestExecutionEngine.Instance.StopSession();
            BtnStop.IsEnabled = false;
            TxtActiveTestState.Text = "ABORTING...";
        }

        private void UpdateCounters()
        {
            int total = _viewModels.Count;
            int pass = _viewModels.Count(v => v.Outcome == TestOutcome.Pass);
            int fail = _viewModels.Count(v => v.Outcome == TestOutcome.Fail || v.Outcome == TestOutcome.Error);
            int skip = _viewModels.Count(v => v.Outcome == TestOutcome.Skipped);

            TxtTotal.Text = total.ToString();
            TxtPass.Text = pass.ToString();
            TxtFail.Text = fail.ToString();
            TxtSkip.Text = skip.ToString();
        }
    }
}
