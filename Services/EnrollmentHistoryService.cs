using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroTrustMigrationAddin.Models;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Services
{
    /// <summary>
    /// Manages storage and retrieval of historical enrollment data.
    /// Data is stored locally to enable real trend analysis over time.
    /// </summary>
    public class EnrollmentHistoryService
    {
        private static readonly Lazy<EnrollmentHistoryService> _instance = 
            new Lazy<EnrollmentHistoryService>(() => new EnrollmentHistoryService());
        
        public static EnrollmentHistoryService Instance => _instance.Value;

        private readonly string _historyFilePath;
        private readonly string _dataDirectory;
        private EnrollmentHistory? _cachedHistory;
        private readonly object _lockObject = new();
        
        // Minimum hours between snapshots to prevent excessive data
        private const int MinHoursBetweenSnapshots = 12;
        
        // Maximum snapshots to retain (about 2 years of daily data)
        private const int MaxSnapshots = 730;

        private EnrollmentHistoryService()
        {
            _dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ZeroTrustMigrationAddin");
            
            _historyFilePath = Path.Combine(_dataDirectory, "enrollment-history.json");
            
            FileLogger.Instance.Info($"[HISTORY] Enrollment history service initialized");
            FileLogger.Instance.Info($"[HISTORY] Data file: {_historyFilePath}");
        }

        /// <summary>
        /// Load enrollment history from disk, or create new if doesn't exist.
        /// </summary>
        public async Task<EnrollmentHistory> LoadHistoryAsync()
        {
            lock (_lockObject)
            {
                if (_cachedHistory != null)
                {
                    FileLogger.Instance.Debug($"[HISTORY] Returning cached history ({_cachedHistory.Snapshots.Count} snapshots)");
                    return _cachedHistory;
                }
            }

            try
            {
                if (File.Exists(_historyFilePath))
                {
                    var json = await File.ReadAllTextAsync(_historyFilePath);
                    var history = JsonSerializer.Deserialize<EnrollmentHistory>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (history != null)
                    {
                        lock (_lockObject)
                        {
                            _cachedHistory = history;
                        }
                        
                        FileLogger.Instance.Info($"[HISTORY] ✅ Loaded enrollment history:");
                        FileLogger.Instance.Info($"[HISTORY]    First recorded: {history.FirstRecordedDate:yyyy-MM-dd}");
                        FileLogger.Instance.Info($"[HISTORY]    Last updated: {history.LastUpdatedDate:yyyy-MM-dd HH:mm}");
                        FileLogger.Instance.Info($"[HISTORY]    Total snapshots: {history.Snapshots.Count}");
                        FileLogger.Instance.Info($"[HISTORY]    Days of history: {history.DaysOfHistory}");
                        FileLogger.Instance.Info($"[HISTORY]    Has sufficient history: {history.HasSufficientHistory}");
                        
                        return history;
                    }
                }
                
                FileLogger.Instance.Info($"[HISTORY] No existing history file found, creating new history");
                return CreateNewHistory();
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"[HISTORY] ❌ Failed to load history: {ex.Message}");
                FileLogger.Instance.LogException(ex, "EnrollmentHistoryService.LoadHistoryAsync");
                return CreateNewHistory();
            }
        }

        /// <summary>
        /// Record a new enrollment snapshot.
        /// Only records if enough time has passed since last snapshot.
        /// </summary>
        public async Task<bool> RecordSnapshotAsync(
            int totalDevices, 
            int cloudManagedDevices, 
            int configMgrOnlyDevices, 
            int cloudNativeDevices,
            bool isRealData)
        {
            try
            {
                var history = await LoadHistoryAsync();
                
                // Check if we should record (minimum time elapsed)
                if (history.Snapshots.Count > 0)
                {
                    var lastSnapshot = history.Snapshots[^1];
                    var hoursSinceLast = (DateTime.UtcNow - lastSnapshot.Timestamp).TotalHours;
                    
                    if (hoursSinceLast < MinHoursBetweenSnapshots)
                    {
                        FileLogger.Instance.Debug($"[HISTORY] Skipping snapshot - only {hoursSinceLast:F1} hours since last (min: {MinHoursBetweenSnapshots})");
                        
                        // Still update the last snapshot if data has significantly changed
                        if (ShouldUpdateLastSnapshot(lastSnapshot, totalDevices, cloudManagedDevices, isRealData))
                        {
                            FileLogger.Instance.Info($"[HISTORY] Updating last snapshot due to significant data change");
                            lastSnapshot.TotalDevices = totalDevices;
                            lastSnapshot.CloudManagedDevices = cloudManagedDevices;
                            lastSnapshot.ConfigMgrOnlyDevices = configMgrOnlyDevices;
                            lastSnapshot.CloudNativeDevices = cloudNativeDevices;
                            lastSnapshot.IsRealData = isRealData;
                            lastSnapshot.Timestamp = DateTime.UtcNow;
                            
                            await SaveHistoryAsync(history);
                        }
                        
                        return false;
                    }
                }

                // Create new snapshot
                var snapshot = new HistoricalEnrollmentDataPoint
                {
                    Timestamp = DateTime.UtcNow,
                    TotalDevices = totalDevices,
                    CloudManagedDevices = cloudManagedDevices,
                    ConfigMgrOnlyDevices = configMgrOnlyDevices,
                    CloudNativeDevices = cloudNativeDevices,
                    IsRealData = isRealData,
                    DataSourceHash = ComputeDataHash(totalDevices, cloudManagedDevices)
                };

                history.Snapshots.Add(snapshot);
                history.LastUpdatedDate = DateTime.UtcNow;
                
                // Prune old snapshots if needed
                if (history.Snapshots.Count > MaxSnapshots)
                {
                    var removeCount = history.Snapshots.Count - MaxSnapshots;
                    history.Snapshots.RemoveRange(0, removeCount);
                    FileLogger.Instance.Info($"[HISTORY] Pruned {removeCount} old snapshots");
                }

                // Update summary stats
                history.SummaryStats = CalculateSummaryStats(history);

                await SaveHistoryAsync(history);

                FileLogger.Instance.Info($"[HISTORY] ✅ Recorded new snapshot:");
                FileLogger.Instance.Info($"[HISTORY]    Timestamp: {snapshot.Timestamp:yyyy-MM-dd HH:mm} UTC");
                FileLogger.Instance.Info($"[HISTORY]    Total Devices: {totalDevices:N0}");
                FileLogger.Instance.Info($"[HISTORY]    Cloud Managed: {cloudManagedDevices:N0} ({snapshot.EnrollmentPercentage}%)");
                FileLogger.Instance.Info($"[HISTORY]    ConfigMgr Only: {configMgrOnlyDevices:N0}");
                FileLogger.Instance.Info($"[HISTORY]    Cloud Native: {cloudNativeDevices:N0}");
                FileLogger.Instance.Info($"[HISTORY]    Is Real Data: {isRealData}");
                FileLogger.Instance.Info($"[HISTORY]    Total snapshots now: {history.Snapshots.Count}");

                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"[HISTORY] ❌ Failed to record snapshot: {ex.Message}");
                FileLogger.Instance.LogException(ex, "EnrollmentHistoryService.RecordSnapshotAsync");
                return false;
            }
        }

        /// <summary>
        /// Get trend data for the enrollment graph.
        /// Returns real data if available, or indicates projection needed.
        /// </summary>
        public async Task<(EnrollmentTrend[] Trends, TrendDisplayOptions Options)> GetTrendDataAsync(
            int currentTotal, 
            int currentCloudManaged, 
            int currentConfigMgrOnly, 
            int currentCloudNative)
        {
            var history = await LoadHistoryAsync();
            var options = new TrendDisplayOptions();

            // Count real data points
            var realSnapshots = history.Snapshots.Where(s => s.IsRealData).ToList();
            options.RealDataPoints = realSnapshots.Count;
            options.DaysOfRealData = history.DaysOfHistory;
            options.FirstDataDate = history.Snapshots.FirstOrDefault()?.Timestamp;

            FileLogger.Instance.Info($"[HISTORY] === Generating Trend Data ===");
            FileLogger.Instance.Info($"[HISTORY]    Real data points: {options.RealDataPoints}");
            FileLogger.Instance.Info($"[HISTORY]    Days of real data: {options.DaysOfRealData}");
            FileLogger.Instance.Info($"[HISTORY]    Has sufficient history: {history.HasSufficientHistory}");

            // If we have sufficient history (7+ days), use real data
            if (history.HasSufficientHistory && realSnapshots.Count >= 2)
            {
                options.IsProjected = false;
                options.DataQualityMessage = $"📊 Real data from {options.DaysOfRealData} days of tracking ({options.RealDataPoints} data points)";
                
                FileLogger.Instance.Info($"[HISTORY] ✅ Using REAL historical data for trend graph");
                
                var trends = GenerateRealTrendData(history, currentTotal, currentCloudManaged, currentConfigMgrOnly, currentCloudNative);
                return (trends, options);
            }
            else
            {
                // Not enough history - use projected data with clear indicator
                options.IsProjected = true;
                
                if (options.RealDataPoints == 0)
                {
                    options.DataQualityMessage = "📈 Projected trend (no historical data yet - check back in 7 days)";
                }
                else if (options.DaysOfRealData < 7)
                {
                    options.DataQualityMessage = $"📈 Projected trend ({options.DaysOfRealData} days tracked - need 7+ days for real trends)";
                }
                else
                {
                    options.DataQualityMessage = $"📈 Projected trend (only {options.RealDataPoints} data points - need more for accuracy)";
                }

                FileLogger.Instance.Warning($"[HISTORY] ⚠️ Using PROJECTED data for trend graph - insufficient history");
                FileLogger.Instance.Info($"[HISTORY]    Reason: {options.DataQualityMessage}");
                
                var trends = GenerateProjectedTrendData(currentTotal, currentCloudManaged, currentConfigMgrOnly, currentCloudNative);
                return (trends, options);
            }
        }

        /// <summary>
        /// Generate trend data from real historical snapshots.
        /// </summary>
        private EnrollmentTrend[] GenerateRealTrendData(
            EnrollmentHistory history,
            int currentTotal,
            int currentCloudManaged,
            int currentConfigMgrOnly,
            int currentCloudNative)
        {
            var trends = new List<EnrollmentTrend>();
            
            // Get snapshots for the last 6 months, sampled at monthly intervals
            var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
            var relevantSnapshots = history.Snapshots
                .Where(s => s.Timestamp >= sixMonthsAgo)
                .OrderBy(s => s.Timestamp)
                .ToList();

            FileLogger.Instance.Info($"[HISTORY] Generating real trend from {relevantSnapshots.Count} snapshots in last 6 months");

            // Sample at monthly intervals (or use all if few data points)
            var monthlySnapshots = new List<HistoricalEnrollmentDataPoint>();
            
            for (int i = 0; i <= 6; i++)
            {
                var targetDate = DateTime.UtcNow.AddMonths(-6 + i);
                var closest = relevantSnapshots
                    .OrderBy(s => Math.Abs((s.Timestamp - targetDate).TotalDays))
                    .FirstOrDefault();
                
                if (closest != null && !monthlySnapshots.Contains(closest))
                {
                    monthlySnapshots.Add(closest);
                }
            }

            // If we have gaps, fill them with interpolation
            foreach (var snapshot in monthlySnapshots.OrderBy(s => s.Timestamp))
            {
                trends.Add(new EnrollmentTrend
                {
                    Month = snapshot.Timestamp,
                    IntuneDevices = snapshot.CloudManagedDevices,
                    CloudNativeDevices = snapshot.CloudNativeDevices,
                    ConfigMgrDevices = snapshot.ConfigMgrOnlyDevices
                });
                
                FileLogger.Instance.Debug($"[HISTORY]    {snapshot.Timestamp:yyyy-MM-dd}: CM={snapshot.CloudManagedDevices}, Native={snapshot.CloudNativeDevices}, ConfigMgr={snapshot.ConfigMgrOnlyDevices}");
            }

            // Always add current data as the last point
            var lastTrend = trends.LastOrDefault();
            if (lastTrend == null || (DateTime.UtcNow - lastTrend.Month).TotalDays > 1)
            {
                trends.Add(new EnrollmentTrend
                {
                    Month = DateTime.UtcNow,
                    IntuneDevices = currentCloudManaged,
                    CloudNativeDevices = currentCloudNative,
                    ConfigMgrDevices = currentConfigMgrOnly
                });
            }

            FileLogger.Instance.Info($"[HISTORY] Generated {trends.Count} real trend data points");
            
            return trends.ToArray();
        }

        /// <summary>
        /// Generate projected trend data when we don't have enough history.
        /// This is clearly labeled as projected/estimated.
        /// </summary>
        private EnrollmentTrend[] GenerateProjectedTrendData(
            int currentTotal, 
            int currentCloudManaged, 
            int currentConfigMgrOnly, 
            int currentCloudNative)
        {
            FileLogger.Instance.Warning($"[HISTORY] ⚠️ Generating PROJECTED trend data (not real historical data)");
            FileLogger.Instance.Info($"[HISTORY]    Current values: Total={currentTotal}, CloudManaged={currentCloudManaged}, ConfigMgrOnly={currentConfigMgrOnly}, CloudNative={currentCloudNative}");
            
            var trends = new List<EnrollmentTrend>();
            var baseDate = DateTime.Now.AddMonths(-6);

            // Use actual cloud native count if provided, otherwise estimate
            if (currentCloudNative == 0 && currentCloudManaged > 0)
                currentCloudNative = (int)(currentCloudManaged * 0.12);

            for (int i = 0; i <= 6; i++)
            {
                double progress = i / 6.0;

                // Cloud-managed devices grow over time (co-management progress)
                int cloudManagedAtMonth = (int)(currentCloudManaged * progress);

                // Cloud native grows faster (newer devices)
                int cloudNativeAtMonth = (int)(currentCloudNative * (0.3 + progress * 0.7));

                // ConfigMgr-only decreases as devices become co-managed
                int configMgrAtMonth = currentTotal - cloudManagedAtMonth;
                configMgrAtMonth = Math.Max(0, configMgrAtMonth);

                trends.Add(new EnrollmentTrend
                {
                    Month = baseDate.AddMonths(i),
                    IntuneDevices = cloudManagedAtMonth,
                    CloudNativeDevices = cloudNativeAtMonth,
                    ConfigMgrDevices = configMgrAtMonth
                });
                
                FileLogger.Instance.Debug($"[HISTORY]    Month {i} (PROJECTED): CM={cloudManagedAtMonth}, Native={cloudNativeAtMonth}, ConfigMgr={configMgrAtMonth}");
            }

            return trends.ToArray();
        }

        /// <summary>
        /// Calculate summary statistics for telemetry reporting.
        /// </summary>
        public EnrollmentSummaryStats CalculateSummaryStats(EnrollmentHistory history)
        {
            var stats = new EnrollmentSummaryStats
            {
                CalculatedAt = DateTime.UtcNow,
                TotalDataPoints = history.Snapshots.Count
            };

            if (history.Snapshots.Count == 0)
            {
                return stats;
            }

            var latestSnapshot = history.Snapshots[^1];
            var firstSnapshot = history.Snapshots[0];

            stats.CurrentTotalDevices = latestSnapshot.TotalDevices;
            stats.PeakTotalDevices = history.Snapshots.Max(s => s.TotalDevices);
            stats.CurrentEnrollmentPercentage = latestSnapshot.EnrollmentPercentage;
            stats.DaysSinceFirstData = (int)(DateTime.UtcNow - firstSnapshot.Timestamp).TotalDays;
            stats.IsActive = (DateTime.UtcNow - latestSnapshot.Timestamp).TotalDays <= 7;

            // Calculate migration velocity over different periods
            var now = DateTime.UtcNow;
            
            var snapshot7DaysAgo = history.Snapshots
                .Where(s => s.Timestamp <= now.AddDays(-7))
                .OrderByDescending(s => s.Timestamp)
                .FirstOrDefault();
            
            var snapshot30DaysAgo = history.Snapshots
                .Where(s => s.Timestamp <= now.AddDays(-30))
                .OrderByDescending(s => s.Timestamp)
                .FirstOrDefault();
            
            var snapshot90DaysAgo = history.Snapshots
                .Where(s => s.Timestamp <= now.AddDays(-90))
                .OrderByDescending(s => s.Timestamp)
                .FirstOrDefault();

            if (snapshot7DaysAgo != null)
            {
                stats.DevicesMigratedLast7Days = latestSnapshot.CloudManagedDevices - snapshot7DaysAgo.CloudManagedDevices;
                stats.EnrollmentPercentage7DaysAgo = snapshot7DaysAgo.EnrollmentPercentage;
            }

            if (snapshot30DaysAgo != null)
            {
                stats.DevicesMigratedLast30Days = latestSnapshot.CloudManagedDevices - snapshot30DaysAgo.CloudManagedDevices;
                stats.EnrollmentPercentage30DaysAgo = snapshot30DaysAgo.EnrollmentPercentage;
            }

            if (snapshot90DaysAgo != null)
            {
                stats.DevicesMigratedLast90Days = latestSnapshot.CloudManagedDevices - snapshot90DaysAgo.CloudManagedDevices;
            }

            // Calculate average daily velocity
            if (stats.DaysSinceFirstData > 0)
            {
                var totalMigrated = latestSnapshot.CloudManagedDevices - firstSnapshot.CloudManagedDevices;
                stats.AverageDailyVelocity = Math.Round((double)totalMigrated / stats.DaysSinceFirstData, 2);
            }

            // Determine trend direction
            if (stats.EnrollmentPercentage7DaysAgo > 0)
            {
                var weeklyDelta = stats.CurrentEnrollmentPercentage - stats.EnrollmentPercentage7DaysAgo;
                
                if (weeklyDelta > 2)
                    stats.TrendDirection = "Accelerating";
                else if (weeklyDelta > 0.5)
                    stats.TrendDirection = "Steady";
                else if (weeklyDelta > -0.5)
                    stats.TrendDirection = "Stalled";
                else
                    stats.TrendDirection = "Declining";
            }

            // Estimate days to completion
            if (stats.AverageDailyVelocity > 0 && latestSnapshot.ConfigMgrOnlyDevices > 0)
            {
                stats.EstimatedDaysToCompletion = (int)(latestSnapshot.ConfigMgrOnlyDevices / stats.AverageDailyVelocity);
            }

            FileLogger.Instance.Info($"[HISTORY] Summary stats calculated:");
            FileLogger.Instance.Info($"[HISTORY]    Peak devices: {stats.PeakTotalDevices:N0}");
            FileLogger.Instance.Info($"[HISTORY]    Current enrollment: {stats.CurrentEnrollmentPercentage}%");
            FileLogger.Instance.Info($"[HISTORY]    7-day migration: {stats.DevicesMigratedLast7Days:N0} devices");
            FileLogger.Instance.Info($"[HISTORY]    30-day migration: {stats.DevicesMigratedLast30Days:N0} devices");
            FileLogger.Instance.Info($"[HISTORY]    Daily velocity: {stats.AverageDailyVelocity:F1} devices/day");
            FileLogger.Instance.Info($"[HISTORY]    Trend: {stats.TrendDirection}");
            FileLogger.Instance.Info($"[HISTORY]    Est. completion: {stats.EstimatedDaysToCompletion?.ToString() ?? "N/A"} days");

            return stats;
        }

        /// <summary>
        /// Get current summary stats for telemetry.
        /// </summary>
        public async Task<EnrollmentSummaryStats?> GetSummaryStatsAsync()
        {
            var history = await LoadHistoryAsync();
            return history.SummaryStats ?? CalculateSummaryStats(history);
        }

        private EnrollmentHistory CreateNewHistory()
        {
            var history = new EnrollmentHistory
            {
                Version = 1,
                FirstRecordedDate = DateTime.UtcNow,
                LastUpdatedDate = DateTime.UtcNow,
                InstallationId = Guid.NewGuid().ToString("N")[..16],
                Snapshots = new List<HistoricalEnrollmentDataPoint>()
            };

            lock (_lockObject)
            {
                _cachedHistory = history;
            }

            FileLogger.Instance.Info($"[HISTORY] Created new enrollment history with ID: {history.InstallationId}");
            return history;
        }

        private async Task SaveHistoryAsync(EnrollmentHistory history)
        {
            try
            {
                if (!Directory.Exists(_dataDirectory))
                {
                    Directory.CreateDirectory(_dataDirectory);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(history, options);
                await File.WriteAllTextAsync(_historyFilePath, json);

                lock (_lockObject)
                {
                    _cachedHistory = history;
                }

                FileLogger.Instance.Debug($"[HISTORY] Saved history to {_historyFilePath}");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"[HISTORY] ❌ Failed to save history: {ex.Message}");
                throw;
            }
        }

        private bool ShouldUpdateLastSnapshot(HistoricalEnrollmentDataPoint last, int newTotal, int newCloudManaged, bool newIsRealData)
        {
            // Update if going from mock to real data
            if (!last.IsRealData && newIsRealData)
                return true;

            // Update if significant change (>5% difference)
            if (last.TotalDevices > 0)
            {
                var totalChange = Math.Abs(newTotal - last.TotalDevices) / (double)last.TotalDevices;
                var cloudChange = last.CloudManagedDevices > 0 
                    ? Math.Abs(newCloudManaged - last.CloudManagedDevices) / (double)last.CloudManagedDevices 
                    : 1;

                return totalChange > 0.05 || cloudChange > 0.05;
            }

            return false;
        }

        private string ComputeDataHash(int total, int cloudManaged)
        {
            return $"{total}_{cloudManaged}_{DateTime.UtcNow:yyyyMMdd}";
        }

        /// <summary>
        /// Clear all history (for testing or reset)
        /// </summary>
        public async Task ClearHistoryAsync()
        {
            try
            {
                if (File.Exists(_historyFilePath))
                {
                    File.Delete(_historyFilePath);
                }

                lock (_lockObject)
                {
                    _cachedHistory = null;
                }

                FileLogger.Instance.Info($"[HISTORY] History cleared");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"[HISTORY] Failed to clear history: {ex.Message}");
                throw;
            }
        }
    }
}
