using System;
using System.Collections.Generic;
using ZeroTrustMigrationAddin.Models;

namespace ZeroTrustMigrationAddin.Services.Pipeline
{
    #region Pipeline Result

    /// <summary>
    /// Aggregated result from running the full analysis pipeline.
    /// Contains all analyzer results and recommendations.
    /// </summary>
    public class AnalysisPipelineResult
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public TimeSpan Duration { get; set; }
        public bool IsComplete { get; set; }
        public List<AnalyzerResult> AnalyzerResults { get; set; } = new();

        /// <summary>Highest severity across all analyzers.</summary>
        public SeverityLevel OverallSeverity
        {
            get
            {
                var max = SeverityLevel.None;
                foreach (var r in AnalyzerResults)
                    if (r.Severity > max) max = r.Severity;
                return max;
            }
        }

        /// <summary>All recommendations from all analyzers, ordered by priority.</summary>
        public List<PipelineRecommendation> AllRecommendations
        {
            get
            {
                var all = new List<PipelineRecommendation>();
                foreach (var r in AnalyzerResults)
                    all.AddRange(r.Recommendations);
                all.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                return all;
            }
        }
    }

    /// <summary>
    /// Result from a single analyzer execution.
    /// </summary>
    public class AnalyzerResult
    {
        public string AnalyzerName { get; set; } = string.Empty;
        public SeverityLevel Severity { get; set; }
        public StallClassification Classification { get; set; }
        public List<string> ContributingFactors { get; set; } = new();
        public List<AffectedEntity> AffectedEntities { get; set; } = new();
        public List<PipelineRecommendation> Recommendations { get; set; } = new();
        public object? Assessment { get; set; }
        public TimeSpan Duration { get; set; }
    }

    #endregion

    #region Enums

    public enum SeverityLevel
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    public enum StallClassification
    {
        /// <summary>No stall detected.</summary>
        None,
        /// <summary>Enrollment failures, ESP blocking, infrastructure issues.</summary>
        Technical,
        /// <summary>Workloads not moving, slider stagnation, resource constraints.</summary>
        Operational,
        /// <summary>Agent removal hesitation, rollback fears, organizational resistance.</summary>
        ConfidenceBased,
        /// <summary>Dedicated resources not available, competing priorities.</summary>
        ResourceConstrained
    }

    #endregion

    #region Recommendation Model

    /// <summary>
    /// A scoped, named, bounded action recommendation.
    /// More specific than AIRecommendation — includes target devices, blast radius, and risk quantification.
    /// </summary>
    public class PipelineRecommendation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Rationale { get; set; } = string.Empty;
        public List<string> ActionSteps { get; set; } = new();
        public RecommendationPriority Priority { get; set; }
        public RecommendationCategory Category { get; set; }

        /// <summary>Specific devices this recommendation targets. Empty = org-wide.</summary>
        public List<string> TargetDeviceNames { get; set; } = new();
        public int TargetDeviceCount { get; set; }

        /// <summary>How many devices/users are impacted if this action is taken.</summary>
        public int BlastRadiusDevices { get; set; }
        public int BlastRadiusUsers { get; set; }

        /// <summary>Risk of taking this action (Low/Medium/High).</summary>
        public string RiskLevel { get; set; } = "Low";

        /// <summary>What happens if no action is taken.</summary>
        public string CostOfInaction { get; set; } = string.Empty;

        /// <summary>Estimated effort to execute.</summary>
        public string EstimatedEffort { get; set; } = string.Empty;

        /// <summary>Impact score 0-100.</summary>
        public int ImpactScore { get; set; }

        public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;

        /// <summary>Source analyzer that generated this recommendation.</summary>
        public string SourceAnalyzer { get; set; } = string.Empty;
    }

    #endregion

    #region Affected Entity

    /// <summary>
    /// An entity (device, workload, policy) affected by a stall or blocker.
    /// </summary>
    public class AffectedEntity
    {
        public string EntityType { get; set; } = string.Empty; // "Device", "Workload", "Policy"
        public string Name { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public SeverityLevel Severity { get; set; }
    }

    #endregion

    #region Signal Models

    /// <summary>
    /// Enrollment signal — aggregated enrollment state for analysis.
    /// </summary>
    public class EnrollmentSignal
    {
        public int TotalDevices { get; set; }
        public int EnrolledDevices { get; set; }
        public double EnrolledPercentage => TotalDevices > 0 ? (double)EnrolledDevices / TotalDevices * 100 : 0;
        public int CoManagedDevices { get; set; }
        public int ConfigMgrOnlyDevices { get; set; }
        public int CloudNativeDevices { get; set; }

        /// <summary>Velocity metrics from trend analysis.</summary>
        public double Velocity7Day { get; set; }
        public double Velocity30Day { get; set; }
        public double Velocity60Day { get; set; }
        public double Velocity90Day { get; set; }
        public double WeekOverWeekChange { get; set; }
        public TrendState TrendState { get; set; }

        /// <summary>Days since the last device was enrolled.</summary>
        public int DaysSinceLastEnrollment { get; set; }

        /// <summary>Historical snapshots for deeper analysis.</summary>
        public List<EnrollmentSnapshot> Snapshots { get; set; } = new();

        /// <summary>Device readiness breakdown (Excellent/Good/Fair/Poor).</summary>
        public int ExcellentDevices { get; set; }
        public int GoodDevices { get; set; }
        public int FairDevices { get; set; }
        public int PoorDevices { get; set; }

        /// <summary>Confidence score from existing analytics.</summary>
        public int ConfidenceScore { get; set; }

        /// <summary>Whether data comes from live sources or mock.</summary>
        public bool IsLiveData { get; set; }
        public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Workload signal — aggregated co-management workload state for analysis.
    /// </summary>
    public class WorkloadSignal
    {
        public List<WorkloadState> Workloads { get; set; } = new();
        public int TotalCoManagedDevices { get; set; }
        public int TotalDevices { get; set; }

        /// <summary>Number of workloads fully transitioned to Intune.</summary>
        public int CompletedWorkloadCount => Workloads.FindAll(w => w.Status == WorkloadStatus.Completed).Count;

        /// <summary>Number of workloads in progress.</summary>
        public int InProgressWorkloadCount => Workloads.FindAll(w => w.Status == WorkloadStatus.InProgress).Count;

        /// <summary>Number of workloads not started.</summary>
        public int NotStartedWorkloadCount => Workloads.FindAll(w => w.Status == WorkloadStatus.NotStarted).Count;

        /// <summary>Days since ANY workload slider changed authority.</summary>
        public int DaysSinceAnyWorkloadChange { get; set; }

        /// <summary>Devices with 5+ workloads on Intune but not fully transitioned.</summary>
        public int NearCompleteDevices { get; set; }

        /// <summary>Whether data comes from live sources or mock.</summary>
        public bool IsLiveData { get; set; }
        public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Per-workload state snapshot for signal aggregation.
    /// </summary>
    public class WorkloadState
    {
        public string Name { get; set; } = string.Empty;
        public WorkloadStatus Status { get; set; }
        public double IntuneAdoptionPercentage { get; set; }
        public int IntuneDeviceCount { get; set; }
        public int ConfigMgrDeviceCount { get; set; }
        public int Order { get; set; }
        public bool IsBlocked { get; set; }
        public string BlockReason { get; set; } = string.Empty;
        public List<string> DependsOn { get; set; } = new();

        /// <summary>Days since this specific workload's adoption percentage changed.</summary>
        public int DaysSinceChange { get; set; }

        /// <summary>Velocity: percentage points change per week.</summary>
        public double VelocityPerWeek { get; set; }

        /// <summary>Whether this workload has real Graph API data.</summary>
        public bool HasRealData { get; set; }
    }

    #endregion

    #region Assessment Models

    /// <summary>
    /// Assessment result from enrollment stall analysis.
    /// </summary>
    public class EnrollmentStallAssessment
    {
        public bool IsStalled { get; set; }
        public SeverityLevel Severity { get; set; }
        public StallClassification Classification { get; set; }

        /// <summary>True if in the Trust Trough zone (50-60% with declining velocity).</summary>
        public bool IsTrustTroughRisk { get; set; }

        /// <summary>Days the stall has persisted.</summary>
        public int StallDurationDays { get; set; }

        /// <summary>Enrollment percentage when the stall began.</summary>
        public double EnrollmentPercentageAtStall { get; set; }

        /// <summary>Current enrollment percentage.</summary>
        public double CurrentEnrollmentPercentage { get; set; }

        /// <summary>Factors contributing to the stall.</summary>
        public List<string> ContributingFactors { get; set; } = new();

        /// <summary>Devices that are candidates for a Trust Reset Batch.</summary>
        public List<string> TrustResetCandidateDevices { get; set; } = new();
        public int TrustResetBatchSize { get; set; }

        /// <summary>Quantified cost of inaction.</summary>
        public string PatchLatencyImpact { get; set; } = string.Empty;
        public string ZeroTrustGapDescription { get; set; } = string.Empty;
        public int DevicesWithExtendedPatchLatency { get; set; }
    }

    /// <summary>
    /// Assessment result from workload stall analysis.
    /// </summary>
    public class WorkloadStallAssessment
    {
        public bool IsStalled { get; set; }
        public SeverityLevel Severity { get; set; }
        public StallClassification Classification { get; set; }

        /// <summary>True if in the Workload Trust Trough (4-5 of 7 workloads transitioned, then stalled).</summary>
        public bool IsWorkloadTrustTrough { get; set; }

        /// <summary>Workloads that are individually stalled.</summary>
        public List<StalledWorkload> StalledWorkloads { get; set; } = new();

        /// <summary>The workload(s) that are the last holdout(s) on the most devices.</summary>
        public List<LastHoldoutWorkload> LastHoldouts { get; set; } = new();

        /// <summary>Devices that have 5-6 workloads done but are blocked by the last 1-2.</summary>
        public int NearCompleteDeviceCount { get; set; }

        /// <summary>Days since any workload made progress.</summary>
        public int DaysSinceAnyProgress { get; set; }

        /// <summary>Overall workload velocity (percentage points/week across all workloads).</summary>
        public double OverallVelocity { get; set; }

        /// <summary>Factors contributing to the stall.</summary>
        public List<string> ContributingFactors { get; set; } = new();
    }

    /// <summary>
    /// Details about a specific workload that has stalled.
    /// </summary>
    public class StalledWorkload
    {
        public string Name { get; set; } = string.Empty;
        public double CurrentAdoptionPercentage { get; set; }
        public int DaysSinceChange { get; set; }
        public int DevicesBlocked { get; set; }
        public string BlockReason { get; set; } = string.Empty;
        public StallClassification WhyStalled { get; set; }
    }

    #endregion
}
