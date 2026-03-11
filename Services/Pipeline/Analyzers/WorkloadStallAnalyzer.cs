using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZeroTrustMigrationAddin.Models;

namespace ZeroTrustMigrationAddin.Services.Pipeline.Analyzers
{
    /// <summary>
    /// Detects workload transition stalls including:
    /// - Workload Trust Trough (4-5 of 7 workloads done, then stalled)
    /// - Individual workload stalls (slider hasn't moved in N days)
    /// - Client Apps holdout pattern (most common last blocker)
    /// - Last holdout identification (which workload blocks the most devices)
    /// </summary>
    public class WorkloadStallAnalyzer : AnalyzerBase<WorkloadSignal, WorkloadStallAssessment>
    {
        public override string Name => "WorkloadStallAnalyzer";
        public override int Priority => 20; // Run after enrollment analyzer

        private const int StallThresholdDays = 14;
        private const int SevereStallThresholdDays = 30;
        private const int WorkloadTrustTroughMin = 4;
        private const int WorkloadTrustTroughMax = 5;
        private const int TotalWorkloads = 7;

        protected override Task<WorkloadStallAssessment> AnalyzeCoreAsync(
            WorkloadSignal signal, CancellationToken ct)
        {
            var assessment = new WorkloadStallAssessment
            {
                DaysSinceAnyProgress = signal.DaysSinceAnyWorkloadChange,
                NearCompleteDeviceCount = signal.NearCompleteDevices
            };

            // === Detect individually stalled workloads ===
            foreach (var workload in signal.Workloads)
            {
                if (workload.Status == WorkloadStatus.InProgress
                    && workload.DaysSinceChange >= StallThresholdDays
                    && workload.VelocityPerWeek < 5.0)
                {
                    assessment.StalledWorkloads.Add(new StalledWorkload
                    {
                        Name = workload.Name,
                        CurrentAdoptionPercentage = workload.IntuneAdoptionPercentage,
                        DaysSinceChange = workload.DaysSinceChange,
                        DevicesBlocked = workload.ConfigMgrDeviceCount,
                        BlockReason = workload.IsBlocked ? workload.BlockReason : "No measurable progress",
                        WhyStalled = ClassifyWorkloadStall(workload)
                    });
                }
            }

            // === Detect Workload Trust Trough (4-5 of 7 done, then stuck) ===
            int completedCount = signal.CompletedWorkloadCount;
            if (completedCount >= WorkloadTrustTroughMin
                && completedCount <= WorkloadTrustTroughMax
                && signal.DaysSinceAnyWorkloadChange >= StallThresholdDays)
            {
                assessment.IsWorkloadTrustTrough = true;
                assessment.ContributingFactors.Add(
                    $"{completedCount} of {TotalWorkloads} workloads transitioned, but progress has stalled " +
                    $"for {signal.DaysSinceAnyWorkloadChange} days");

                var remainingWorkloads = signal.Workloads
                    .Where(w => w.Status != WorkloadStatus.Completed)
                    .Select(w => w.Name)
                    .ToList();
                assessment.ContributingFactors.Add(
                    $"Remaining workloads: {string.Join(", ", remainingWorkloads)}");
            }

            // === Detect Client Apps holdout pattern ===
            var clientApps = signal.Workloads.FirstOrDefault(w =>
                w.Name.Contains("Client Apps", System.StringComparison.OrdinalIgnoreCase));

            if (clientApps != null
                && clientApps.IntuneAdoptionPercentage < 10
                && completedCount >= 3)
            {
                assessment.ContributingFactors.Add(
                    $"Client Apps adoption is only {clientApps.IntuneAdoptionPercentage:F0}% " +
                    $"while {completedCount} other workloads are complete — " +
                    "this is the most common 'last holdout' pattern");
            }

            // === Identify last holdout workloads ===
            var holdouts = signal.Workloads
                .Where(w => w.Status != WorkloadStatus.Completed && w.ConfigMgrDeviceCount > 0)
                .OrderByDescending(w => w.ConfigMgrDeviceCount)
                .Select(w => new LastHoldoutWorkload
                {
                    WorkloadName = w.Name,
                    DevicesBlockedCount = w.ConfigMgrDeviceCount,
                    Icon = w.DaysSinceChange >= SevereStallThresholdDays ? "🔴" : "⚠️"
                })
                .ToList();
            assessment.LastHoldouts = holdouts;

            // === Calculate overall workload velocity ===
            var inProgressWorkloads = signal.Workloads
                .Where(w => w.Status == WorkloadStatus.InProgress && w.HasRealData)
                .ToList();
            if (inProgressWorkloads.Any())
            {
                assessment.OverallVelocity = inProgressWorkloads.Average(w => w.VelocityPerWeek);
            }

            // === Determine overall severity ===
            assessment.IsStalled = assessment.StalledWorkloads.Any()
                || assessment.IsWorkloadTrustTrough
                || signal.DaysSinceAnyWorkloadChange >= SevereStallThresholdDays;

            if (assessment.IsStalled)
            {
                assessment.Classification = DetermineClassification(assessment, signal);

                if (signal.DaysSinceAnyWorkloadChange >= SevereStallThresholdDays
                    && assessment.StalledWorkloads.Count >= 2)
                {
                    assessment.Severity = SeverityLevel.Critical;
                }
                else if (assessment.IsWorkloadTrustTrough
                    || signal.DaysSinceAnyWorkloadChange >= SevereStallThresholdDays)
                {
                    assessment.Severity = SeverityLevel.High;
                }
                else if (assessment.StalledWorkloads.Any())
                {
                    assessment.Severity = SeverityLevel.Medium;
                }
                else
                {
                    assessment.Severity = SeverityLevel.Low;
                }
            }

            return Task.FromResult(assessment);
        }

        private static StallClassification ClassifyWorkloadStall(WorkloadState workload)
        {
            if (workload.IsBlocked)
                return StallClassification.Technical;

            // Client Apps / Office Apps stalls are typically confidence-based
            if (workload.Name.Contains("Client Apps", System.StringComparison.OrdinalIgnoreCase)
                || workload.Name.Contains("Office", System.StringComparison.OrdinalIgnoreCase))
                return StallClassification.ConfidenceBased;

            // Workloads with some adoption that stalled are likely operational
            if (workload.IntuneAdoptionPercentage > 0)
                return StallClassification.Operational;

            return StallClassification.ResourceConstrained;
        }

        private static StallClassification DetermineClassification(
            WorkloadStallAssessment assessment, WorkloadSignal signal)
        {
            // If most stalled workloads are confidence-based, overall is confidence
            var classifications = assessment.StalledWorkloads
                .Select(w => w.WhyStalled)
                .GroupBy(c => c)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (classifications != null)
                return classifications.Key;

            // Default: if high completion but stalled, it's confidence; otherwise operational
            return signal.CompletedWorkloadCount >= 4
                ? StallClassification.ConfidenceBased
                : StallClassification.Operational;
        }
    }
}
