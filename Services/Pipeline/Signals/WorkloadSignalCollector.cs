using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZeroTrustMigrationAddin.Models;

namespace ZeroTrustMigrationAddin.Services.Pipeline.Signals
{
    /// <summary>
    /// Collects co-management workload state from Graph API and historical trend data.
    /// Wraps existing GraphDataService and WorkloadTrendService.
    /// </summary>
    public class WorkloadSignalCollector : SignalCollectorBase<WorkloadSignal>
    {
        private readonly GraphDataService _graphDataService;
        private readonly WorkloadTrendService _trendService;

        public override string Name => "WorkloadSignalCollector";
        protected override TimeSpan CacheTtl => TimeSpan.FromMinutes(5);

        public WorkloadSignalCollector(GraphDataService graphDataService)
        {
            _graphDataService = graphDataService ?? throw new ArgumentNullException(nameof(graphDataService));
            _trendService = new WorkloadTrendService();
        }

        protected override async Task<WorkloadSignal?> CollectCoreAsync(CancellationToken ct)
        {
            // Get enrollment data for total device counts
            var enrollment = await _graphDataService.GetDeviceEnrollmentAsync();
            if (enrollment == null)
                return null;

            ct.ThrowIfCancellationRequested();

            // Get workload authority data from Graph
            WorkloadAuthoritySummary? workloadAuthority = null;
            try
            {
                workloadAuthority = await _graphDataService.GetCoManagedWorkloadAuthorityAsync();
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Log(FileLogger.LogLevel.Warning,
                    $"[PIPELINE] WorkloadSignalCollector: Failed to get workload authority - {ex.Message}");
            }

            ct.ThrowIfCancellationRequested();

            // Get historical trends for velocity calculation
            var trendHistory = await _trendService.GetWorkloadTrendsAsync(90);

            // Build workload states from enrollment data (which includes workloads)
            var signal = new WorkloadSignal
            {
                TotalDevices = enrollment.TotalDevices,
                TotalCoManagedDevices = enrollment.CoManagedDevices,
                IsLiveData = !enrollment.IsMockData,
                CollectedAt = DateTime.UtcNow
            };

            // Map workloads from enrollment data enriched with authority data
            signal.Workloads = BuildWorkloadStates(enrollment, workloadAuthority, trendHistory);

            // Calculate days since any workload changed
            signal.DaysSinceAnyWorkloadChange = CalculateDaysSinceAnyChange(signal.Workloads);

            // Count near-complete devices (5+ of 7 workloads on Intune)
            if (workloadAuthority?.Devices != null)
            {
                signal.NearCompleteDevices = workloadAuthority.Devices
                    .Count(d => CountIntuneWorkloads(d) >= 5 && CountIntuneWorkloads(d) < 7);
            }

            return signal;
        }

        private List<WorkloadState> BuildWorkloadStates(
            DeviceEnrollment enrollment,
            WorkloadAuthoritySummary? authority,
            Dictionary<string, List<WorkloadProgressEntry>> trendHistory)
        {
            var states = new List<WorkloadState>();

            // Standard co-management workloads
            var workloadNames = new[]
            {
                "Compliance Policies",
                "Device Configuration",
                "Endpoint Protection",
                "Resource Access Policies",
                "Windows Update Policies",
                "Office Click-to-Run Apps",
                "Client Apps"
            };

            for (int i = 0; i < workloadNames.Length; i++)
            {
                var name = workloadNames[i];
                var state = new WorkloadState
                {
                    Name = name,
                    Order = i + 1
                };

                // Enrich with authority data if available
                if (authority?.WorkloadIntuneAdoptionCounts != null)
                {
                    var matchKey = authority.WorkloadIntuneAdoptionCounts.Keys
                        .FirstOrDefault(k => k.Contains(name, StringComparison.OrdinalIgnoreCase)
                            || name.Contains(k, StringComparison.OrdinalIgnoreCase));

                    if (matchKey != null)
                    {
                        state.IntuneDeviceCount = authority.WorkloadIntuneAdoptionCounts[matchKey];
                        state.ConfigMgrDeviceCount = authority.TotalCoManagedDevices - state.IntuneDeviceCount;
                        state.IntuneAdoptionPercentage = authority.TotalCoManagedDevices > 0
                            ? (double)state.IntuneDeviceCount / authority.TotalCoManagedDevices * 100
                            : 0;
                        state.HasRealData = true;
                    }
                }

                // Derive status from adoption percentage
                state.Status = state.IntuneAdoptionPercentage switch
                {
                    >= 95 => WorkloadStatus.Completed,
                    > 0 => WorkloadStatus.InProgress,
                    _ => WorkloadStatus.NotStarted
                };

                // Enrich with velocity from trend history
                if (trendHistory.TryGetValue(name, out var history) && history.Count >= 2)
                {
                    var sorted = history.OrderBy(h => h.Date).ToList();
                    var recent = sorted.Last();
                    var oldest = sorted.First();
                    double daySpan = (recent.Date - oldest.Date).TotalDays;

                    if (daySpan > 0)
                    {
                        double pctChange = recent.PercentageComplete - oldest.PercentageComplete;
                        state.VelocityPerWeek = pctChange / daySpan * 7;
                    }

                    // Days since last change
                    state.DaysSinceChange = CalculateDaysSinceChange(sorted);
                }
                else
                {
                    state.DaysSinceChange = 0; // Unknown — insufficient history
                }

                states.Add(state);
            }

            return states;
        }

        private static int CalculateDaysSinceChange(List<WorkloadProgressEntry> sortedHistory)
        {
            if (sortedHistory.Count < 2) return 0;

            // Walk backwards to find the last day the percentage actually changed
            for (int i = sortedHistory.Count - 1; i > 0; i--)
            {
                if (Math.Abs(sortedHistory[i].PercentageComplete - sortedHistory[i - 1].PercentageComplete) > 0.1)
                {
                    return (int)(DateTime.Now - sortedHistory[i].Date).TotalDays;
                }
            }

            // No change detected in entire history
            return (int)(DateTime.Now - sortedHistory.First().Date).TotalDays;
        }

        private static int CalculateDaysSinceAnyChange(List<WorkloadState> workloads)
        {
            if (!workloads.Any()) return 0;

            var minDays = workloads
                .Where(w => w.DaysSinceChange > 0)
                .Select(w => w.DaysSinceChange)
                .DefaultIfEmpty(0)
                .Min();

            return minDays;
        }

        private static int CountIntuneWorkloads(DeviceWorkloadAuthority device)
        {
            return device.WorkloadsManagedByIntuneCount;
        }
    }
}
