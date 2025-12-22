using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CloudJourneyAddin.Models;

namespace CloudJourneyAddin.Services
{
    /// <summary>
    /// Tracks workload progress over time and detects velocity trends.
    /// Persists historical data to local storage for trend analysis.
    /// </summary>
    public class WorkloadTrendService
    {
        private readonly FileLogger _fileLogger;
        private readonly string _historyFilePath;

        public WorkloadTrendService()
        {
            _fileLogger = FileLogger.Instance;
            
            // Store history in same folder as logs
            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CloudJourneyAddin"
            );
            Directory.CreateDirectory(appDataFolder);
            _historyFilePath = Path.Combine(appDataFolder, "workload_history.json");
        }

        /// <summary>
        /// Records current workload status to history
        /// </summary>
        public async Task RecordWorkloadProgressAsync(List<Workload> workloads, int totalDevices, int enrolledDevices)
        {
            try
            {
                var history = await LoadHistoryAsync();
                var today = DateTime.Now.Date;

                // Don't record duplicate entries for same day
                if (history.Any(h => h.Date.Date == today))
                {
                    _fileLogger.Log(FileLogger.LogLevel.Debug, 
                        "Workload progress already recorded today, skipping duplicate");
                    return;
                }

                foreach (var workload in workloads)
                {
                    var entry = new WorkloadProgressEntry
                    {
                        Date = DateTime.Now,
                        WorkloadName = workload.Name,
                        Status = workload.Status.ToString(),
                        DevicesManaged = workload.Status == WorkloadStatus.Completed ? enrolledDevices : 0,
                        PercentageComplete = workload.Status == WorkloadStatus.Completed ? 100 : 
                                            workload.Status == WorkloadStatus.InProgress ? 50 : 0,
                        TotalDevices = totalDevices
                    };

                    history.Add(entry);
                }

                await SaveHistoryAsync(history);
                _fileLogger.Log(FileLogger.LogLevel.Info, 
                    $"Recorded workload progress: {workloads.Count} workloads tracked");
            }
            catch (Exception ex)
            {
                _fileLogger.Log(FileLogger.LogLevel.Error, 
                    $"Failed to record workload progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Analyzes workload velocity and generates trend-based recommendations
        /// </summary>
        public async Task<List<AIRecommendation>> AnalyzeWorkloadVelocityAsync(List<Workload> currentWorkloads)
        {
            var recommendations = new List<AIRecommendation>();

            try
            {
                var history = await LoadHistoryAsync();

                if (history.Count < 2)
                {
                    _fileLogger.Log(FileLogger.LogLevel.Info, 
                        "Insufficient history for trend analysis (need 2+ data points)");
                    return recommendations;
                }

                foreach (var workload in currentWorkloads)
                {
                    var workloadHistory = history
                        .Where(h => h.WorkloadName == workload.Name)
                        .OrderBy(h => h.Date)
                        .ToList();

                    if (workloadHistory.Count < 2)
                        continue;

                    var trend = CalculateWorkloadTrend(workloadHistory);

                    // Detect stalled workloads
                    if (trend.Velocity < 5 && trend.DaysSinceChange > 14 && 
                        workload.Status == WorkloadStatus.InProgress)
                    {
                        recommendations.Add(new AIRecommendation
                        {
                            Title = $"📉 Workload Stalled: {workload.Name}",
                            Description = $"No measurable progress on {workload.Name} in {trend.DaysSinceChange} days. " +
                                          $"Current status: {workload.Status}.",
                            Rationale = "Slow workload velocity indicates technical or resource blockers. " +
                                        "Typical workload transitions complete 10-20% per week.",
                            ActionSteps = new List<string>
                            {
                                "1. Review assignment scope - is it too narrow for testing?",
                                "2. Check for policy conflicts between ConfigMgr and Intune",
                                "3. Verify devices are receiving policies (check Intune reporting)",
                                "4. Expand assignment to next group of 50-100 devices",
                                $"5. Target: Complete {workload.Name} within 4 weeks"
                            },
                            Priority = RecommendationPriority.High,
                            Category = RecommendationCategory.WorkloadTransition,
                            ImpactScore = 85,
                            EstimatedEffort = "1-2 weeks to restart momentum"
                        });
                    }

                    // Recognize fast progress
                    if (trend.Velocity > 15 && workload.Status == WorkloadStatus.InProgress)
                    {
                        recommendations.Add(new AIRecommendation
                        {
                            Title = $"🚀 Excellent Velocity: {workload.Name}",
                            Description = $"{workload.Name} progressing at {trend.Velocity:F1}% per week. " +
                                          "This is exceptional pace!",
                            Rationale = "Your team is moving faster than average. This momentum should be maintained.",
                            ActionSteps = new List<string>
                            {
                                "✅ Keep this pace going - you're ahead of schedule",
                                "📊 Document what's working well for other workloads",
                                "🎯 Consider starting next workload in parallel"
                            },
                            Priority = RecommendationPriority.Low,
                            Category = RecommendationCategory.General,
                            ImpactScore = 60
                        });
                    }
                }

                // Overall velocity analysis
                var overallTrend = CalculateOverallTrend(history);
                
                if (overallTrend.Velocity < 5)
                {
                    recommendations.Add(new AIRecommendation
                    {
                        Title = "⚠️ Overall Migration Velocity: Slow",
                        Description = $"Migration velocity: {overallTrend.Velocity:F1}% per week. " +
                                      "Target: 10-15% per week for on-time completion.",
                        Rationale = "Low velocity across all workloads suggests systemic issues or resource constraints.",
                        ActionSteps = new List<string>
                        {
                            "1. Assess if project has dedicated resources",
                            "2. Identify common blockers across workloads",
                            "3. Consider Microsoft FastTrack engagement",
                            "4. Set weekly standup meetings to maintain focus"
                        },
                        Priority = RecommendationPriority.High,
                        Category = RecommendationCategory.StallPrevention,
                        ImpactScore = 90
                    });
                }

                _fileLogger.Log(FileLogger.LogLevel.Info, 
                    $"Trend analysis complete: {recommendations.Count} velocity-based recommendations");
            }
            catch (Exception ex)
            {
                _fileLogger.Log(FileLogger.LogLevel.Error, 
                    $"Failed to analyze workload velocity: {ex.Message}");
            }

            return recommendations;
        }

        /// <summary>
        /// Gets workload trend data for visualization
        /// </summary>
        public async Task<Dictionary<string, List<WorkloadProgressEntry>>> GetWorkloadTrendsAsync(int daysToInclude = 90)
        {
            var history = await LoadHistoryAsync();
            var cutoffDate = DateTime.Now.AddDays(-daysToInclude);

            return history
                .Where(h => h.Date >= cutoffDate)
                .GroupBy(h => h.WorkloadName)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(h => h.Date).ToList()
                );
        }

        private WorkloadTrend CalculateWorkloadTrend(List<WorkloadProgressEntry> history)
        {
            if (history.Count < 2)
                return new WorkloadTrend();

            var oldest = history.First();
            var newest = history.Last();
            
            var daysSpan = Math.Max(1, (newest.Date - oldest.Date).TotalDays);
            var percentageChange = newest.PercentageComplete - oldest.PercentageComplete;
            var velocity = (percentageChange / daysSpan) * 7; // Per week

            return new WorkloadTrend
            {
                WorkloadName = newest.WorkloadName,
                Velocity = velocity,
                DaysSinceChange = (int)(DateTime.Now - newest.Date).TotalDays,
                TrendDirection = percentageChange > 0 ? "Increasing" : 
                                percentageChange < 0 ? "Decreasing" : "Flat"
            };
        }

        private WorkloadTrend CalculateOverallTrend(List<WorkloadProgressEntry> history)
        {
            var recentHistory = history
                .Where(h => h.Date >= DateTime.Now.AddDays(-30))
                .ToList();

            if (recentHistory.Count < 2)
                return new WorkloadTrend { Velocity = 0 };

            var avgVelocity = recentHistory
                .GroupBy(h => h.WorkloadName)
                .Select(g => CalculateWorkloadTrend(g.OrderBy(h => h.Date).ToList()).Velocity)
                .Average();

            return new WorkloadTrend
            {
                WorkloadName = "Overall",
                Velocity = avgVelocity,
                DaysSinceChange = 0
            };
        }

        private async Task<List<WorkloadProgressEntry>> LoadHistoryAsync()
        {
            try
            {
                if (!File.Exists(_historyFilePath))
                    return new List<WorkloadProgressEntry>();

                var json = await File.ReadAllTextAsync(_historyFilePath);
                var history = JsonSerializer.Deserialize<List<WorkloadProgressEntry>>(json);
                return history ?? new List<WorkloadProgressEntry>();
            }
            catch (Exception ex)
            {
                _fileLogger.Log(FileLogger.LogLevel.Error, 
                    $"Failed to load workload history: {ex.Message}");
                return new List<WorkloadProgressEntry>();
            }
        }

        private async Task SaveHistoryAsync(List<WorkloadProgressEntry> history)
        {
            try
            {
                // Keep only last 365 days
                var cutoff = DateTime.Now.AddDays(-365);
                var filtered = history.Where(h => h.Date >= cutoff).ToList();

                var json = JsonSerializer.Serialize(filtered, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(_historyFilePath, json);
                
                _fileLogger.Log(FileLogger.LogLevel.Debug, 
                    $"Saved workload history: {filtered.Count} entries");
            }
            catch (Exception ex)
            {
                _fileLogger.Log(FileLogger.LogLevel.Error, 
                    $"Failed to save workload history: {ex.Message}");
            }
        }
    }

    #region Models

    public class WorkloadProgressEntry
    {
        public DateTime Date { get; set; }
        public string WorkloadName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int DevicesManaged { get; set; }
        public double PercentageComplete { get; set; }
        public int TotalDevices { get; set; }
    }

    public class WorkloadTrend
    {
        public string WorkloadName { get; set; } = string.Empty;
        public double Velocity { get; set; } // Percentage change per week
        public int DaysSinceChange { get; set; }
        public string TrendDirection { get; set; } = string.Empty;

        public string VelocityDescription
        {
            get
            {
                if (Velocity >= 15) return "Excellent";
                if (Velocity >= 10) return "Good";
                if (Velocity >= 5) return "Moderate";
                return "Slow";
            }
        }
    }

    #endregion
}
