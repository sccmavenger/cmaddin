using System;
using System.Collections.Generic;

namespace ZeroTrustMigrationAddin.Models
{
    /// <summary>
    /// Type of decision card based on workload state.
    /// </summary>
    public enum DecisionCardType
    {
        ReadyToStart,
        ExpandScope,
        StallRecovery,
        NearComplete,
        Complete
    }

    /// <summary>
    /// A decision-forcing card that answers the 4 key questions from copilot_recommendations.md:
    /// 1. What decision must be made now
    /// 2. Why it matters
    /// 3. What happens if no action is taken (cost of inaction)
    /// 4. What the lowest-risk next step is
    /// </summary>
    public class DecisionCard
    {
        public string WorkloadName { get; set; } = string.Empty;
        public DecisionCardType CardType { get; set; }

        /// <summary>Q1: What decision must be made now</summary>
        public string Decision { get; set; } = string.Empty;

        /// <summary>Q2: Why it matters</summary>
        public string WhyItMatters { get; set; } = string.Empty;

        /// <summary>Q3: What happens if no action is taken</summary>
        public string CostOfInaction { get; set; } = string.Empty;

        /// <summary>Q4: What the lowest-risk next step is</summary>
        public string LowestRiskNextStep { get; set; } = string.Empty;

        // Metadata
        public string RiskLevel { get; set; } = "Low";
        public int DevicesAffected { get; set; }
        public string SafetyScore { get; set; } = string.Empty;
        public double ReadinessScore { get; set; }
        public int Order { get; set; }

        // UI helpers
        public string CardTypeLabel => CardType switch
        {
            DecisionCardType.ReadyToStart => "READY TO START",
            DecisionCardType.ExpandScope => "EXPAND SCOPE",
            DecisionCardType.StallRecovery => "STALL — ACTION NEEDED",
            DecisionCardType.NearComplete => "ALMOST DONE",
            DecisionCardType.Complete => "COMPLETE",
            _ => ""
        };

        public string CardTypeIcon => CardType switch
        {
            DecisionCardType.ReadyToStart => "🚀",
            DecisionCardType.ExpandScope => "📈",
            DecisionCardType.StallRecovery => "⚠️",
            DecisionCardType.NearComplete => "🎯",
            DecisionCardType.Complete => "✅",
            _ => "❓"
        };

        public string CardColor => CardType switch
        {
            DecisionCardType.ReadyToStart => "#2563EB",
            DecisionCardType.ExpandScope => "#0891B2",
            DecisionCardType.StallRecovery => "#DC2626",
            DecisionCardType.NearComplete => "#D97706",
            DecisionCardType.Complete => "#16A34A",
            _ => "#6B7280"
        };

        public string CardBackgroundColor => CardType switch
        {
            DecisionCardType.ReadyToStart => "#EFF6FF",
            DecisionCardType.ExpandScope => "#ECFEFF",
            DecisionCardType.StallRecovery => "#FEF2F2",
            DecisionCardType.NearComplete => "#FFFBEB",
            DecisionCardType.Complete => "#F0FDF4",
            _ => "#F9FAFB"
        };

        public string CardBorderColor => CardType switch
        {
            DecisionCardType.ReadyToStart => "#93C5FD",
            DecisionCardType.ExpandScope => "#67E8F9",
            DecisionCardType.StallRecovery => "#FCA5A5",
            DecisionCardType.NearComplete => "#FCD34D",
            DecisionCardType.Complete => "#86EFAC",
            _ => "#D1D5DB"
        };
    }

    /// <summary>
    /// Shows what completing a workload unlocks downstream.
    /// </summary>
    public class WorkloadUnlockChain
    {
        public string WorkloadName { get; set; } = string.Empty;
        public string UnlockDescription { get; set; } = string.Empty;
        public List<string> EnabledWorkloads { get; set; } = new();
        public int DevicesUnblocked { get; set; }
        public string MultiplierEffect { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }

        public string StatusIcon => IsCompleted ? "✅" : "🔓";
    }

    /// <summary>
    /// Per-workload ConfigMgr vs Intune coverage breakdown.
    /// </summary>
    public class ConfigMgrCoverage
    {
        public string WorkloadName { get; set; } = string.Empty;
        public int IntuneDeviceCount { get; set; }
        public int ConfigMgrDeviceCount { get; set; }
        public double IntunePercentage { get; set; }
        public string StatusSummary { get; set; } = string.Empty;
        public string MigrationNote { get; set; } = string.Empty;
        public bool HasRealData { get; set; }

        public string BarColor => IntunePercentage >= 90 ? "#16A34A"
            : IntunePercentage >= 50 ? "#2563EB"
            : IntunePercentage > 0 ? "#D97706"
            : "#9CA3AF";
    }

    /// <summary>
    /// Per-workload safety score for "Safe to Remove" confidence.
    /// </summary>
    public class WorkloadSafetyScore
    {
        public string WorkloadName { get; set; } = string.Empty;
        public string SafetyLevel { get; set; } = "Unknown";
        public List<string> WhatStopsRunning { get; set; } = new();
        public List<string> IntuneCoverage { get; set; } = new();
        public int PolicyConflictCount { get; set; }

        /// <summary>Data-driven explanation of WHY this safety level was assigned</summary>
        public string WhySafe { get; set; } = string.Empty;

        /// <summary>What the safety level means in plain language</summary>
        public string SafetyLevelExplanation => SafetyLevel switch
        {
            "High" => "High confidence — Intune policies fully cover this workload. Moving the slider carries minimal risk.",
            "Medium" => "Moderate confidence — Most devices are covered by Intune, but some gaps exist. Review before moving.",
            "Low" => "Low confidence — Significant gaps in Intune coverage. Moving the slider could leave devices unmanaged.",
            _ => "Insufficient data to determine safety."
        };

        public string SafetyIcon => SafetyLevel switch
        {
            "High" => "🟢",
            "Medium" => "🟡",
            "Low" => "🔴",
            _ => "⚪"
        };

        public string SafetyColor => SafetyLevel switch
        {
            "High" => "#16A34A",
            "Medium" => "#D97706",
            "Low" => "#DC2626",
            _ => "#9CA3AF"
        };

        public string SafetyBackgroundColor => SafetyLevel switch
        {
            "High" => "#F0FDF4",
            "Medium" => "#FFFBEB",
            "Low" => "#FEF2F2",
            _ => "#F9FAFB"
        };
    }

    // ================================================================
    // IMPLEMENTED FEATURE MODELS (8 deep-analysis features)
    // ================================================================

    /// <summary>
    /// Feature 1: ConfigMgr Client Uninstall Readiness — per-tier device counts.
    /// </summary>
    public class UninstallReadinessResult
    {
        public int GreenCount { get; set; }
        public int YellowCount { get; set; }
        public int RedCount { get; set; }
        public int TotalDevices { get; set; }
        public List<string> TopBlockers { get; set; } = new();
        public string Summary { get; set; } = string.Empty;

        public double GreenPercent => TotalDevices > 0 ? (double)GreenCount / TotalDevices * 100 : 0;
        public double YellowPercent => TotalDevices > 0 ? (double)YellowCount / TotalDevices * 100 : 0;
        public double RedPercent => TotalDevices > 0 ? (double)RedCount / TotalDevices * 100 : 0;
    }

    /// <summary>
    /// Feature 2: Security Exposure Gap — Intune vs ConfigMgr cohort comparison.
    /// </summary>
    public class SecurityExposureResult
    {
        public List<SecurityMetricComparison> Metrics { get; set; } = new();
        public string Verdict { get; set; } = string.Empty;
        public int SecurityDeltaScore { get; set; }
    }

    public class SecurityMetricComparison
    {
        public string MetricName { get; set; } = string.Empty;
        public string MetricIcon { get; set; } = string.Empty;
        public double IntuneValue { get; set; }
        public double ConfigMgrValue { get; set; }
        public string IntuneLabel { get; set; } = string.Empty;
        public string ConfigMgrLabel { get; set; } = string.Empty;
        public bool HigherIsBetter { get; set; } = true;

        public string DeltaLabel
        {
            get
            {
                var diff = IntuneValue - ConfigMgrValue;
                if (HigherIsBetter)
                    return diff > 0 ? $"+{diff:F0}% better with Intune" : $"{diff:F0}% gap";
                else
                    return diff < 0 ? $"{Math.Abs(diff):F1}x fewer with Intune" : $"{diff:F1}x more";
            }
        }

        public string DeltaColor => HigherIsBetter
            ? (IntuneValue > ConfigMgrValue ? "#16A34A" : "#DC2626")
            : (IntuneValue < ConfigMgrValue ? "#16A34A" : "#DC2626");
    }

    /// <summary>
    /// Feature 3: Days to ConfigMgr-Free countdown projection.
    /// </summary>
    public class ConfigMgrFreeCountdown
    {
        public DateTime ProjectedDate { get; set; }
        public DateTime AcceleratedDate { get; set; }
        public int DaysRemaining { get; set; }
        public int DaysWithAcceleration { get; set; }
        public int WeeksSaved { get; set; }
        public double CurrentVelocity { get; set; }
        public double AcceleratedVelocity { get; set; }
        public int DevicesRemaining { get; set; }
        public int WorkloadsRemaining { get; set; }
        public List<string> TopAccelerators { get; set; } = new();

        public string ProjectedDateLabel => ProjectedDate.ToString("MMMM yyyy");
        public string AcceleratedDateLabel => AcceleratedDate.ToString("MMMM yyyy");
    }

    /// <summary>
    /// Feature 4: Pilot Wave — batch of devices for next migration wave.
    /// </summary>
    public class PilotWave
    {
        public int WaveNumber { get; set; }
        public string WaveName { get; set; } = string.Empty;
        public int DeviceCount { get; set; }
        public double ExpectedSuccessRate { get; set; }
        public string RiskProfile { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public string RiskColor => RiskProfile switch
        {
            "Low" => "#16A34A",
            "Medium" => "#D97706",
            "High" => "#DC2626",
            _ => "#9CA3AF"
        };

        public string RiskBackground => RiskProfile switch
        {
            "Low" => "#F0FDF4",
            "Medium" => "#FFFBEB",
            "High" => "#FEF2F2",
            _ => "#F9FAFB"
        };
    }

    /// <summary>
    /// Feature 5: Workload What-If — projected impact of moving a single workload.
    /// </summary>
    public class WorkloadWhatIf
    {
        public string WorkloadName { get; set; } = string.Empty;
        public int DevicesAffected { get; set; }
        public int SecurityDelta { get; set; }
        public int OperationsDelta { get; set; }
        public int ComplianceDelta { get; set; }
        public List<string> WorkloadsUnblocked { get; set; } = new();
        public int NewUninstallReadyDevices { get; set; }
        public string Recommendation { get; set; } = string.Empty;

        public string SecurityDeltaLabel => SecurityDelta >= 0 ? $"+{SecurityDelta}" : $"{SecurityDelta}";
        public string OperationsDeltaLabel => OperationsDelta >= 0 ? $"+{OperationsDelta}" : $"{OperationsDelta}";
        public string ComplianceDeltaLabel => ComplianceDelta >= 0 ? $"+{ComplianceDelta}" : $"{ComplianceDelta}";
        public string SecurityColor => SecurityDelta > 0 ? "#16A34A" : SecurityDelta < 0 ? "#DC2626" : "#9CA3AF";
        public string OperationsColor => OperationsDelta > 0 ? "#16A34A" : OperationsDelta < 0 ? "#DC2626" : "#9CA3AF";
        public string ComplianceColor => ComplianceDelta > 0 ? "#16A34A" : ComplianceDelta < 0 ? "#DC2626" : "#9CA3AF";
    }

    /// <summary>
    /// Feature 6: Stale/Orphan detection — 4 categories of device waste.
    /// </summary>
    public class StaleOrphanResult
    {
        public int StaleCount { get; set; }
        public int OrphanedCount { get; set; }
        public int GhostCount { get; set; }
        public int BlockerCount { get; set; }
        public int TotalWaste => StaleCount + OrphanedCount + GhostCount + BlockerCount;
        public string WasteSummary { get; set; } = string.Empty;
    }

    /// <summary>
    /// Feature 7: Infrastructure Retirement Map — what can be decommissioned per workload.
    /// </summary>
    public class InfraRetirementItem
    {
        public string WorkloadName { get; set; } = string.Empty;
        public string InfrastructureName { get; set; } = string.Empty;
        public string InfrastructureDescription { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusIcon => Status switch
        {
            "Ready to Retire" => "🟢",
            "Partially Retired" => "🟡",
            "Still Needed" => "🔴",
            _ => "⚪"
        };
        public string StatusColor => Status switch
        {
            "Ready to Retire" => "#16A34A",
            "Partially Retired" => "#D97706",
            "Still Needed" => "#DC2626",
            _ => "#9CA3AF"
        };
        public string StatusBackground => Status switch
        {
            "Ready to Retire" => "#F0FDF4",
            "Partially Retired" => "#FFFBEB",
            "Still Needed" => "#FEF2F2",
            _ => "#F9FAFB"
        };
    }

    /// <summary>
    /// Feature 8: Compliance trend snapshot (pre-time-series: current projected impact).
    /// </summary>
    public class ComplianceTrendSnapshot
    {
        public double CurrentComplianceRate { get; set; }
        public double ProjectedComplianceRate { get; set; }
        public double ImprovementPercent => ProjectedComplianceRate - CurrentComplianceRate;
        public List<ComplianceWorkloadImpact> WorkloadImpacts { get; set; } = new();
        public string Insight { get; set; } = string.Empty;
    }

    public class ComplianceWorkloadImpact
    {
        public string WorkloadName { get; set; } = string.Empty;
        public double CurrentCompliance { get; set; }
        public double ProjectedCompliance { get; set; }
        public double Improvement => ProjectedCompliance - CurrentCompliance;
        public string ImprovementLabel => $"+{Improvement:F0}%";
        public string BarColor => Improvement >= 20 ? "#16A34A" : Improvement >= 10 ? "#2563EB" : "#D97706";
    }

    /// <summary>
    /// Last Holdout spotlight — dedicated card for the 6/7 scenario.
    /// </summary>
    public class LastHoldoutSpotlight
    {
        public string WorkloadName { get; set; } = string.Empty;
        public int DevicesBlocked { get; set; }
        public string WhyItMatters { get; set; } = string.Empty;
        public string WhatNeedsToBeDone { get; set; } = string.Empty;
        public int CloudNativeDevicesOnCompletion { get; set; }
        public bool IsVisible { get; set; }
    }
}
