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
    // IMPLEMENTED FEATURE MODELS (3 deep-analysis features)
    // ================================================================

    /// <summary>
    /// Feature 1: ConfigMgr Client Uninstall Readiness — per-tier device counts with deep drill-down.
    /// </summary>
    public class UninstallReadinessResult
    {
        public int GreenCount { get; set; }
        public int YellowCount { get; set; }
        public int RedCount { get; set; }
        public int TotalDevices { get; set; }
        public List<string> TopBlockers { get; set; } = new();
        public string Summary { get; set; } = string.Empty;

        /// Per-workload gap breakdown showing which workloads block uninstall readiness
        public List<WorkloadGapDetail> WorkloadGaps { get; set; } = new();

        /// Actionable next steps per readiness tier
        public List<string> GreenActions { get; set; } = new();
        public List<string> YellowActions { get; set; } = new();
        public List<string> RedActions { get; set; } = new();

        /// Devices that moved from Yellow→Green if the nearest-to-completion workload finishes
        public int NextWinDeviceCount { get; set; }
        public string NextWinWorkload { get; set; } = string.Empty;

        public double GreenPercent => TotalDevices > 0 ? (double)GreenCount / TotalDevices * 100 : 0;
        public double YellowPercent => TotalDevices > 0 ? (double)YellowCount / TotalDevices * 100 : 0;
        public double RedPercent => TotalDevices > 0 ? (double)RedCount / TotalDevices * 100 : 0;
    }

    /// <summary>
    /// Per-workload detail showing how many devices a workload is blocking from uninstall readiness.
    /// </summary>
    public class WorkloadGapDetail
    {
        public string WorkloadName { get; set; } = string.Empty;
        public double AdoptionPercent { get; set; }
        public int DevicesBlocked { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusIcon => Status switch
        {
            "Complete" => "✅",
            "Almost" => "🟡",
            "Blocking" => "🔴",
            _ => "⚪"
        };
        public string BarColor => AdoptionPercent >= 90 ? "#16A34A"
            : AdoptionPercent >= 50 ? "#D97706"
            : "#DC2626";
    }

    /// <summary>
    /// Feature 2: Security Exposure Gap — Intune vs ConfigMgr cohort comparison with deep drill-down.
    /// </summary>
    public class SecurityExposureResult
    {
        public List<SecurityMetricComparison> Metrics { get; set; } = new();
        public string Verdict { get; set; } = string.Empty;
        public int SecurityDeltaScore { get; set; }

        /// Overall risk severity: Critical, High, Moderate, Low
        public string RiskSeverity { get; set; } = string.Empty;
        public string RiskSeverityIcon => RiskSeverity switch
        {
            "Critical" => "🔴",
            "High" => "🟠",
            "Moderate" => "🟡",
            "Low" => "🟢",
            _ => "⚪"
        };
        public string RiskSeverityColor => RiskSeverity switch
        {
            "Critical" => "#DC2626",
            "High" => "#EA580C",
            "Moderate" => "#D97706",
            "Low" => "#16A34A",
            _ => "#9CA3AF"
        };

        /// Executive risk summary — 1-2 sentence impact statement for leadership
        public string ExecutiveRiskSummary { get; set; } = string.Empty;

        /// Per-workload security impact — what each unmoved workload contributes to the gap
        public List<WorkloadSecurityImpact> WorkloadImpacts { get; set; } = new();

        /// Estimated devices at elevated risk
        public int DevicesAtRisk { get; set; }

        /// Top remediation actions
        public List<string> RemediationActions { get; set; } = new();
    }

    /// <summary>
    /// Per-workload contribution to the overall security exposure gap.
    /// </summary>
    public class WorkloadSecurityImpact
    {
        public string WorkloadName { get; set; } = string.Empty;
        public string RiskContribution { get; set; } = string.Empty;
        public int GapPoints { get; set; }
        public string Icon { get; set; } = string.Empty;
        public string GapColor => GapPoints >= 15 ? "#DC2626" : GapPoints >= 8 ? "#D97706" : "#16A34A";
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
    /// Feature 3: Stale/Orphan detection — 4 categories of device waste with deep drill-down.
    /// </summary>
    public class StaleOrphanResult
    {
        public int StaleCount { get; set; }
        public int OrphanedCount { get; set; }
        public int GhostCount { get; set; }
        public int BlockerCount { get; set; }
        public int TotalWaste => StaleCount + OrphanedCount + GhostCount + BlockerCount;
        public string WasteSummary { get; set; } = string.Empty;

        /// Per-category detailed breakdown with action items
        public List<StaleOrphanCategory> Categories { get; set; } = new();

        /// Estimated annual cost of maintaining waste devices (hours × cost)
        public string EstimatedAnnualWaste { get; set; } = string.Empty;

        /// Cleanup priority actions
        public List<string> CleanupActions { get; set; } = new();

        /// Percentage of fleet that is waste
        public double WastePercent { get; set; }
    }

    /// <summary>
    /// Detailed breakdown for each waste detection category.
    /// </summary>
    public class StaleOrphanCategory
    {
        public string CategoryName { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public int DeviceCount { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Impact { get; set; } = string.Empty;
        public string ActionItem { get; set; } = string.Empty;
        public string SeverityColor { get; set; } = "#9CA3AF";
        public string SeverityBackground { get; set; } = "#F9FAFB";
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
