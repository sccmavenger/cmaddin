using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroTrustMigrationAddin.Services.Pipeline
{
    /// <summary>
    /// Central orchestrator that runs Signal → Analyzer → Recommendation chains.
    /// Discovers registered analyzers via DI and runs them in priority order.
    /// Supports on-demand execution and background scheduled runs.
    /// </summary>
    public class AnalysisPipelineOrchestrator
    {
        private readonly FileLogger _logger = FileLogger.Instance;
        private readonly IServiceProvider _serviceProvider;
        private readonly List<IAnalyzerRegistration> _registrations = new();
        private Timer? _backgroundTimer;
        private AnalysisPipelineResult? _lastResult;
        private readonly SemaphoreSlim _runLock = new(1, 1);
        private readonly string _resultsCachePath;

        /// <summary>Fires when a pipeline run completes.</summary>
        public event EventHandler<AnalysisPipelineResult>? PipelineCompleted;

        /// <summary>Fires when overall severity changes from the last run.</summary>
        public event EventHandler<SeverityLevel>? SeverityChanged;

        /// <summary>Last pipeline result, or null if never run.</summary>
        public AnalysisPipelineResult? LastResult => _lastResult;

        public AnalysisPipelineOrchestrator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;

            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ZeroTrustMigrationAddin");
            Directory.CreateDirectory(appDataFolder);
            _resultsCachePath = Path.Combine(appDataFolder, "pipeline_results.json");

            LoadCachedResults();
        }

        /// <summary>
        /// Registers an analyzer chain (signal collector → analyzer → recommendation provider).
        /// Call this during DI setup.
        /// </summary>
        public void RegisterAnalyzer<TSignal, TAssessment>(
            ISignalCollector<TSignal> collector,
            IAnalyzer<TSignal, TAssessment> analyzer,
            IRecommendationProvider<TAssessment> recommendationProvider)
            where TSignal : class
            where TAssessment : class
        {
            _registrations.Add(new AnalyzerRegistration<TSignal, TAssessment>(
                collector, analyzer, recommendationProvider));
            _logger.Log(FileLogger.LogLevel.Info,
                $"[PIPELINE] Registered analyzer: {analyzer.Name} (priority: {analyzer.Priority})");
        }

        /// <summary>
        /// Runs the full analysis pipeline on-demand.
        /// Collects signals, runs analyzers, generates recommendations.
        /// </summary>
        public async Task<AnalysisPipelineResult> RunAsync(CancellationToken ct = default)
        {
            await _runLock.WaitAsync(ct);
            try
            {
                _logger.Log(FileLogger.LogLevel.Info,
                    $"[PIPELINE] Starting pipeline run with {_registrations.Count} analyzer(s)...");
                var sw = Stopwatch.StartNew();

                var result = new AnalysisPipelineResult();

                // Sort by priority (lower runs first)
                _registrations.Sort((a, b) => a.Priority.CompareTo(b.Priority));

                foreach (var registration in _registrations)
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        var analyzerResult = await registration.ExecuteAsync(ct);
                        result.AnalyzerResults.Add(analyzerResult);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        _logger.Log(FileLogger.LogLevel.Error,
                            $"[PIPELINE] Analyzer '{registration.AnalyzerName}' failed: {ex.Message}");
                        result.AnalyzerResults.Add(new AnalyzerResult
                        {
                            AnalyzerName = registration.AnalyzerName,
                            Severity = SeverityLevel.None
                        });
                    }
                }

                sw.Stop();
                result.Duration = sw.Elapsed;
                result.IsComplete = true;

                _logger.Log(FileLogger.LogLevel.Info,
                    $"[PIPELINE] Pipeline run complete in {sw.ElapsedMilliseconds}ms — " +
                    $"severity: {result.OverallSeverity}, " +
                    $"recommendations: {result.AllRecommendations.Count}");

                // Detect severity changes
                var previousSeverity = _lastResult?.OverallSeverity ?? SeverityLevel.None;
                _lastResult = result;

                if (result.OverallSeverity != previousSeverity)
                {
                    SeverityChanged?.Invoke(this, result.OverallSeverity);
                }

                PipelineCompleted?.Invoke(this, result);
                SaveCachedResults(result);

                return result;
            }
            finally
            {
                _runLock.Release();
            }
        }

        /// <summary>
        /// Starts background scheduled analysis at the specified interval.
        /// </summary>
        public void StartBackgroundSchedule(TimeSpan interval)
        {
            StopBackgroundSchedule();
            _logger.Log(FileLogger.LogLevel.Info,
                $"[PIPELINE] Starting background schedule (interval: {interval.TotalMinutes:F0} min)");
            _backgroundTimer = new Timer(
                async _ => await BackgroundRunAsync(),
                null,
                interval, // First run after one interval
                interval);
        }

        /// <summary>
        /// Stops background scheduled analysis.
        /// </summary>
        public void StopBackgroundSchedule()
        {
            if (_backgroundTimer != null)
            {
                _backgroundTimer.Dispose();
                _backgroundTimer = null;
                _logger.Log(FileLogger.LogLevel.Info, "[PIPELINE] Background schedule stopped");
            }
        }

        private async Task BackgroundRunAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                await RunAsync(cts.Token);
            }
            catch (Exception ex)
            {
                _logger.Log(FileLogger.LogLevel.Error,
                    $"[PIPELINE] Background run failed: {ex.Message}");
            }
        }

        private void SaveCachedResults(AnalysisPipelineResult result)
        {
            try
            {
                // Save a lightweight summary (not full device lists) for fast reload
                var summary = new PipelineResultSummary
                {
                    Timestamp = result.Timestamp,
                    OverallSeverity = result.OverallSeverity,
                    AnalyzerCount = result.AnalyzerResults.Count,
                    RecommendationCount = result.AllRecommendations.Count,
                    DurationMs = (int)result.Duration.TotalMilliseconds
                };

                var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_resultsCachePath, json);
            }
            catch (Exception ex)
            {
                _logger.Log(FileLogger.LogLevel.Warning,
                    $"[PIPELINE] Failed to cache results: {ex.Message}");
            }
        }

        private void LoadCachedResults()
        {
            try
            {
                if (File.Exists(_resultsCachePath))
                {
                    var json = File.ReadAllText(_resultsCachePath);
                    var summary = JsonSerializer.Deserialize<PipelineResultSummary>(json);
                    if (summary != null)
                    {
                        _logger.Log(FileLogger.LogLevel.Info,
                            $"[PIPELINE] Loaded cached result summary from {summary.Timestamp:u} " +
                            $"(severity: {summary.OverallSeverity})");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log(FileLogger.LogLevel.Warning,
                    $"[PIPELINE] Failed to load cached results: {ex.Message}");
            }
        }
    }

    #region Internal Registration Plumbing

    /// <summary>Non-generic interface for polymorphic storage of typed registrations.</summary>
    internal interface IAnalyzerRegistration
    {
        string AnalyzerName { get; }
        int Priority { get; }
        Task<AnalyzerResult> ExecuteAsync(CancellationToken ct);
    }

    /// <summary>
    /// Typed registration that binds a signal collector, analyzer, and recommendation provider.
    /// </summary>
    internal class AnalyzerRegistration<TSignal, TAssessment> : IAnalyzerRegistration
        where TSignal : class
        where TAssessment : class
    {
        private readonly ISignalCollector<TSignal> _collector;
        private readonly IAnalyzer<TSignal, TAssessment> _analyzer;
        private readonly IRecommendationProvider<TAssessment> _recommendationProvider;

        public string AnalyzerName => _analyzer.Name;
        public int Priority => _analyzer.Priority;

        public AnalyzerRegistration(
            ISignalCollector<TSignal> collector,
            IAnalyzer<TSignal, TAssessment> analyzer,
            IRecommendationProvider<TAssessment> recommendationProvider)
        {
            _collector = collector;
            _analyzer = analyzer;
            _recommendationProvider = recommendationProvider;
        }

        public async Task<AnalyzerResult> ExecuteAsync(CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();

            // Step 1: Collect signal
            var signal = await _collector.CollectAsync(ct);
            if (signal == null)
            {
                return new AnalyzerResult
                {
                    AnalyzerName = _analyzer.Name,
                    Severity = SeverityLevel.None
                };
            }

            // Step 2: Analyze
            var assessment = await _analyzer.AnalyzeAsync(signal, ct);

            // Step 3: Generate recommendations
            var recommendations = await _recommendationProvider.GetRecommendationsAsync(assessment, ct);
            foreach (var rec in recommendations)
                rec.SourceAnalyzer = _analyzer.Name;

            sw.Stop();

            return new AnalyzerResult
            {
                AnalyzerName = _analyzer.Name,
                Severity = GetSeverityFromAssessment(assessment),
                Recommendations = recommendations,
                Assessment = assessment,
                Duration = sw.Elapsed
            };
        }

        private static SeverityLevel GetSeverityFromAssessment(TAssessment assessment)
        {
            // Use reflection-free approach: check known assessment types
            if (assessment is EnrollmentStallAssessment enrollmentAssessment)
                return enrollmentAssessment.Severity;
            if (assessment is WorkloadStallAssessment workloadAssessment)
                return workloadAssessment.Severity;
            return SeverityLevel.None;
        }
    }

    /// <summary>Lightweight summary for disk caching between sessions.</summary>
    internal class PipelineResultSummary
    {
        public DateTime Timestamp { get; set; }
        public SeverityLevel OverallSeverity { get; set; }
        public int AnalyzerCount { get; set; }
        public int RecommendationCount { get; set; }
        public int DurationMs { get; set; }
    }

    #endregion
}
