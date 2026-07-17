using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using atp_enterprise_app_wpf.Models;

namespace atp_enterprise_app_wpf.Services
{
    public class TestExecutionEngine
    {
        private static TestExecutionEngine? _instance;
        public static TestExecutionEngine Instance => _instance ??= new TestExecutionEngine();

        // UI Events
        public event Action<TestDefinition, TestExecutionState, TestRunResult?>? OnTestStateChanged;
        public event Action<string, int, TimeSpan>? OnTestProgressUpdated;
        public event Action? OnSessionCompleted;

        private List<TestDefinition> _testQueue = new();
        private Dictionary<string, ITestModule> _testModules = new();
        private Dictionary<string, TestRunResult> _results = new();
        
        private CancellationTokenSource? _cts;
        private ManualResetEventSlim _pauseEvent = new(true); // true = running, false = paused

        private bool _isRunning = false;
        private bool _isPaused = false;

        // Current active test state for telemetry
        private TestDefinition? _activeTest;
        private List<string> _activeHealthEvents = new();
        private readonly object _healthLock = new();

        private TestExecutionEngine()
        {
            // Subscribe to monitoring engine to log health events during testing
            MonitoringEngine.Instance.OnTelemetryUpdated += MonitoringEngine_OnTelemetryUpdated;
        }

        public AtpProcedure? ActiveProcedure { get; private set; }

        public void RegisterTestModule(string category, ITestModule module)
        {
            _testModules[category] = module;
        }

        public void LoadProcedure(AtpProcedure procedure)
        {
            if (_isRunning) throw new InvalidOperationException("Cannot load procedure while running.");
            ActiveProcedure = procedure;
            
            // Evaluate conditionals and enabled status
            _testQueue = new List<TestDefinition>();
            foreach (var test in procedure.TestSequence)
            {
                if (!test.IsEnabled && !test.IsMandatory) continue;

                if (test.ConditionalExecutionRule != "None")
                {
                    // Basic placeholder logic for conditionals - in a real scenario this might check WMI or prior results
                    // Currently we assume conditionals are met for simplicity, or we could skip based on rule text.
                }

                _testQueue.Add(test);
            }

            _results.Clear();

            // Set all to NotStarted
            foreach (var test in _testQueue)
            {
                OnTestStateChanged?.Invoke(test, TestExecutionState.NotStarted, null);
            }
        }

        public string CurrentSessionId { get; private set; } = string.Empty;

        public async Task StartSessionAsync(string operatorId = "AutoOp")
        {
            if (_isRunning) return;
            if (ActiveProcedure == null) return;
            
            _isRunning = true;
            _isPaused = false;
            _pauseEvent.Set();
            _cts = new CancellationTokenSource();

            // Create Traceability Session
            var session = new AtpSession
            {
                ProcedureId = ActiveProcedure.ProcedureId,
                ProcedureRevision = ActiveProcedure.Revision,
                ProductFamily = ActiveProcedure.ProductFamily,
                OperatorId = operatorId,
                StartTime = DateTime.Now
            };
            TraceabilityDatabase.Instance.CreateSession(session);
            CurrentSessionId = session.SessionId;
            
            TraceabilityDatabase.Instance.LogAuditEvent(CurrentSessionId, "SessionStarted", operatorId, $"Started procedure {ActiveProcedure.ProcedureId} (Rev {ActiveProcedure.Revision})");

            try
            {
                foreach (var test in _testQueue)
                {
                    if (_cts.Token.IsCancellationRequested) break;

                    // Pause logic
                    if (_isPaused)
                    {
                        OnTestStateChanged?.Invoke(test, TestExecutionState.Queued, null);
                        await Task.Run(() => _pauseEvent.Wait(_cts.Token)); // Wait until resumed
                    }
                    if (_cts.Token.IsCancellationRequested) break;

                    await ExecuteTestAsync(test, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Session stopped
            }
            finally
            {
                _isRunning = false;
                _isPaused = false;
                _activeTest = null;
                
                var finalOutcome = _results.Values.Any(r => r.Outcome == TestOutcome.Fail || r.Outcome == TestOutcome.Error) ? "FAIL" : "PASS";
                if (_cts.Token.IsCancellationRequested) finalOutcome = "ABORTED";
                
                TraceabilityDatabase.Instance.FinalizeSession(CurrentSessionId, finalOutcome);
                TraceabilityDatabase.Instance.LogAuditEvent(CurrentSessionId, "SessionFinalized", operatorId, $"Session finalized with result: {finalOutcome}");
                
                OnSessionCompleted?.Invoke();
            }
        }

        public void PauseSession()
        {
            if (_isRunning && !_isPaused)
            {
                _isPaused = true;
                _pauseEvent.Reset();
            }
        }

        public void ResumeSession()
        {
            if (_isRunning && _isPaused)
            {
                _isPaused = false;
                _pauseEvent.Set();
            }
        }

        public void StopSession()
        {
            _cts?.Cancel();
            _pauseEvent.Set(); // Unblock if paused
        }

        private async Task ExecuteTestAsync(TestDefinition test, CancellationToken token)
        {
            OnTestStateChanged?.Invoke(test, TestExecutionState.Initializing, null);

            // Check prerequisites
            if (test.Prerequisites != null && test.Prerequisites.Any())
            {
                foreach (var prereq in test.Prerequisites)
                {
                    if (!_results.ContainsKey(prereq) || _results[prereq].Outcome != TestOutcome.Pass)
                    {
                        var skippedResult = new TestRunResult
                        {
                            Outcome = TestOutcome.Skipped,
                            ErrorDetails = $"Prerequisite '{prereq}' was not met."
                        };
                        _results[test.TestId] = skippedResult;
                        OnTestStateChanged?.Invoke(test, TestExecutionState.Completed, skippedResult);
                        return;
                    }
                }
            }

            // Prepare health tracking
            lock (_healthLock)
            {
                _activeTest = test;
                _activeHealthEvents.Clear();
            }

            OnTestStateChanged?.Invoke(test, TestExecutionState.Running, null);
            var startTime = DateTime.Now;

            TestRunResult result;
            try
            {
                if (_testModules.TryGetValue(test.TestId, out var module) || _testModules.TryGetValue(test.Category, out module))
                {
                    // Create a linked token source for timeout
                    using var timeoutCts = new CancellationTokenSource(test.TimeoutMs);
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

                    OnTestProgressUpdated?.Invoke("Running test module...", 50, TimeSpan.Zero);

                    result = await module.ExecuteAsync(test, linkedCts.Token);
                }
                else
                {
                    // No module found for this test
                    result = new TestRunResult
                    {
                        Outcome = TestOutcome.Error,
                        ErrorDetails = $"No test module registered for '{test.TestId}' (category: '{test.Category}')."
                    };
                }
            }
            catch (OperationCanceledException)
            {
                if (token.IsCancellationRequested)
                {
                    result = new TestRunResult { Outcome = TestOutcome.Skipped, ErrorDetails = "Session stopped by operator." };
                }
                else
                {
                    result = new TestRunResult { Outcome = TestOutcome.Error, ErrorDetails = $"Test timed out after {test.TimeoutMs} ms." };
                }
            }
            catch (Exception ex)
            {
                result = new TestRunResult { Outcome = TestOutcome.Error, ErrorDetails = ex.Message };
            }

            result.StartTime = startTime;
            result.EndTime = DateTime.Now;

            // Attach any health events
            lock (_healthLock)
            {
                result.HealthEvents = _activeHealthEvents.Distinct().ToList();
                _activeTest = null;
            }

            _results[test.TestId] = result;
            OnTestStateChanged?.Invoke(test, TestExecutionState.Evaluating, result);
            
            // Persist to traceability database
            var record = new SessionTestRecord
            {
                SessionId = CurrentSessionId,
                TestId = test.TestId,
                TestName = test.Name,
                Category = test.Category,
                Specification = test.ExpectedSpecification,
                MeasuredValue = result.MeasuredValue,
                Outcome = result.Outcome.ToString(),
                StartTime = result.StartTime ?? DateTime.Now,
                EndTime = result.EndTime ?? DateTime.Now,
                ErrorDetails = result.ErrorDetails,
                HealthEventsJson = System.Text.Json.JsonSerializer.Serialize(result.HealthEvents)
            };
            TraceabilityDatabase.Instance.RecordTestResult(record);
            TraceabilityDatabase.Instance.LogAuditEvent(CurrentSessionId, "TestCompleted", "AutoOp", $"Completed test {test.TestId} with outcome: {result.Outcome}");

            // Brief pause for UI transition
            await Task.Delay(500);
            OnTestStateChanged?.Invoke(test, TestExecutionState.Completed, result);
        }

        private void MonitoringEngine_OnTelemetryUpdated(RealtimeMetrics metrics, List<SensorReading> sensors)
        {
            lock (_healthLock)
            {
                if (_activeTest != null)
                {
                    foreach (var sensor in sensors)
                    {
                        if (sensor.HealthState == "Warning" || sensor.HealthState == "Critical")
                        {
                            string eventMsg = $"[{sensor.HealthState}] {sensor.Name} = {sensor.Value} {sensor.Unit}";
                            if (!_activeHealthEvents.Contains(eventMsg))
                            {
                                _activeHealthEvents.Add(eventMsg);
                            }
                        }
                    }
                }
            }
        }
    }
}
