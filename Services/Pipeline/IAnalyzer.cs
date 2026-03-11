using System;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroTrustMigrationAddin.Services.Pipeline
{
    /// <summary>
    /// Analyzes a signal and produces a typed assessment.
    /// Analyzers are stateless — all inputs come from the signal.
    /// </summary>
    public interface IAnalyzer<TSignal, TAssessment>
        where TSignal : class
        where TAssessment : class
    {
        /// <summary>Display name for logging and diagnostics.</summary>
        string Name { get; }

        /// <summary>Analyzer priority for ordering. Lower runs first.</summary>
        int Priority { get; }

        /// <summary>Analyzes the signal and produces an assessment.</summary>
        Task<TAssessment> AnalyzeAsync(TSignal signal, CancellationToken ct = default);
    }

    /// <summary>
    /// Base class for analyzers with built-in logging, timing, and error handling.
    /// Subclasses implement AnalyzeCoreAsync with the actual detection logic.
    /// </summary>
    public abstract class AnalyzerBase<TSignal, TAssessment> : IAnalyzer<TSignal, TAssessment>
        where TSignal : class
        where TAssessment : class, new()
    {
        private readonly FileLogger _logger = FileLogger.Instance;

        public abstract string Name { get; }
        public virtual int Priority => 100;

        public async Task<TAssessment> AnalyzeAsync(TSignal signal, CancellationToken ct = default)
        {
            try
            {
                _logger.Log(FileLogger.LogLevel.Info, $"[PIPELINE] {Name}: analyzing...");
                var sw = System.Diagnostics.Stopwatch.StartNew();

                var assessment = await AnalyzeCoreAsync(signal, ct);

                sw.Stop();
                _logger.Log(FileLogger.LogLevel.Info, $"[PIPELINE] {Name}: analysis completed in {sw.ElapsedMilliseconds}ms");

                return assessment;
            }
            catch (OperationCanceledException)
            {
                _logger.Log(FileLogger.LogLevel.Warning, $"[PIPELINE] {Name}: analysis cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Log(FileLogger.LogLevel.Error, $"[PIPELINE] {Name}: analysis failed - {ex.Message}");
                return new TAssessment(); // Return default assessment on error
            }
        }

        /// <summary>
        /// Implement this with the actual analysis/detection logic.
        /// </summary>
        protected abstract Task<TAssessment> AnalyzeCoreAsync(TSignal signal, CancellationToken ct);
    }
}
