using System;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroTrustMigrationAddin.Services.Pipeline
{
    /// <summary>
    /// Collects raw data signals from external sources (Graph API, ConfigMgr, local history).
    /// Each collector produces a typed signal model with built-in caching.
    /// </summary>
    public interface ISignalCollector<TSignal> where TSignal : class
    {
        /// <summary>Display name for logging and diagnostics.</summary>
        string Name { get; }

        /// <summary>Collects the signal, using cache if fresh.</summary>
        Task<TSignal?> CollectAsync(CancellationToken ct = default);

        /// <summary>Forces a fresh collection, bypassing cache.</summary>
        Task<TSignal?> CollectFreshAsync(CancellationToken ct = default);

        /// <summary>Returns true if the cached signal is still valid.</summary>
        bool IsCacheFresh { get; }
    }

    /// <summary>
    /// Base class for signal collectors with built-in caching, logging, and error handling.
    /// Subclasses implement CollectCoreAsync with the actual data-fetching logic.
    /// </summary>
    public abstract class SignalCollectorBase<TSignal> : ISignalCollector<TSignal> where TSignal : class
    {
        private readonly FileLogger _logger = FileLogger.Instance;
        private TSignal? _cachedSignal;
        private DateTime _cacheTimestamp;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public abstract string Name { get; }

        /// <summary>Cache duration. Override to customize per collector.</summary>
        protected virtual TimeSpan CacheTtl => TimeSpan.FromMinutes(5);

        public bool IsCacheFresh =>
            _cachedSignal != null && DateTime.UtcNow - _cacheTimestamp < CacheTtl;

        public async Task<TSignal?> CollectAsync(CancellationToken ct = default)
        {
            if (IsCacheFresh)
            {
                _logger.Log(FileLogger.LogLevel.Debug, $"[PIPELINE] {Name}: returning cached signal (age: {(DateTime.UtcNow - _cacheTimestamp).TotalSeconds:F0}s)");
                return _cachedSignal;
            }

            return await CollectFreshAsync(ct);
        }

        public async Task<TSignal?> CollectFreshAsync(CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                _logger.Log(FileLogger.LogLevel.Info, $"[PIPELINE] {Name}: collecting fresh signal...");
                var sw = System.Diagnostics.Stopwatch.StartNew();

                var signal = await CollectCoreAsync(ct);

                sw.Stop();
                if (signal != null)
                {
                    _cachedSignal = signal;
                    _cacheTimestamp = DateTime.UtcNow;
                    _logger.Log(FileLogger.LogLevel.Info, $"[PIPELINE] {Name}: signal collected in {sw.ElapsedMilliseconds}ms");
                }
                else
                {
                    _logger.Log(FileLogger.LogLevel.Warning, $"[PIPELINE] {Name}: collector returned null after {sw.ElapsedMilliseconds}ms");
                }

                return signal;
            }
            catch (OperationCanceledException)
            {
                _logger.Log(FileLogger.LogLevel.Warning, $"[PIPELINE] {Name}: collection cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Log(FileLogger.LogLevel.Error, $"[PIPELINE] {Name}: collection failed - {ex.Message}");
                return _cachedSignal; // Return stale cache on error if available
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Implement this with the actual data-fetching logic.
        /// Called when cache is stale or a fresh collection is requested.
        /// </summary>
        protected abstract Task<TSignal?> CollectCoreAsync(CancellationToken ct);
    }
}
