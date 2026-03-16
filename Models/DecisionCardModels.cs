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

    /// <summary>
    /// A proposed feature for the roadmap — shown in the Decision Cards tab for brainstorming.
    /// </summary>
    public class FeatureRoadmapItem
    {
        public int Number { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Goal { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Phase { get; set; } = string.Empty;
        public string Effort { get; set; } = string.Empty;
        public string Impact { get; set; } = string.Empty;
        public List<string> DataSources { get; set; } = new();
        public string WhyItDrivesAction { get; set; } = string.Empty;
        public string MicrosoftDocsUrl { get; set; } = string.Empty;
        public bool RequiresNewApiCalls { get; set; }

        // UI helpers
        public string PhaseIcon => Phase switch
        {
            "Phase 1" => "🟢",
            "Phase 2" => "🟡",
            "Phase 3" => "🔵",
            _ => "⚪"
        };

        public string PhaseColor => Phase switch
        {
            "Phase 1" => "#16A34A",
            "Phase 2" => "#D97706",
            "Phase 3" => "#2563EB",
            _ => "#9CA3AF"
        };

        public string PhaseBackground => Phase switch
        {
            "Phase 1" => "#F0FDF4",
            "Phase 2" => "#FFFBEB",
            "Phase 3" => "#EFF6FF",
            _ => "#F9FAFB"
        };

        public string PhaseBorder => Phase switch
        {
            "Phase 1" => "#86EFAC",
            "Phase 2" => "#FCD34D",
            "Phase 3" => "#93C5FD",
            _ => "#D1D5DB"
        };

        public string ImpactColor => Impact switch
        {
            "Very High" => "#DC2626",
            "High" => "#D97706",
            "Medium-High" => "#2563EB",
            "Medium" => "#6B7280",
            _ => "#9CA3AF"
        };

        public string EffortBadgeColor => Effort switch
        {
            "Low" => "#16A34A",
            "Medium" => "#D97706",
            "High" => "#DC2626",
            _ => "#9CA3AF"
        };

        public string ApiCallBadge => RequiresNewApiCalls ? "New API calls needed" : "Uses existing data";
        public string ApiCallBadgeColor => RequiresNewApiCalls ? "#D97706" : "#16A34A";
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
