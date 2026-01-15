using System;
using System.Collections.Generic;

namespace ZeroTrustMigrationAddin.Models
{
    #region Core Data Models

    /// <summary>
    /// Point-in-time snapshot of enrollment state for time-series analysis.
    /// </summary>
    public class EnrollmentSnapshot
    {
        public DateTime Date { get; set; }
        public int TotalConfigMgrDevices { get; set; }
        public int TotalIntuneDevices { get; set; }
        public int NewEnrollmentsCount { get; set; }
        
        public double EnrolledPct => TotalConfigMgrDevices > 0 
            ? (double)TotalIntuneDevices / TotalConfigMgrDevices * 100 
            : 0;
        
        public int Gap => TotalConfigMgrDevices - TotalIntuneDevices;
    }

    /// <summary>
    /// Enrollment trend analysis with velocity metrics.
    /// </summary>
    public class EnrollmentTrendAnalysis
    {
        /// <summary>Rolling average velocity over last 30 days (devices/day)</summary>
        public double Velocity30 { get; set; }
        
        /// <summary>Rolling average velocity over last 60 days (devices/day)</summary>
        public double Velocity60 { get; set; }
        
        /// <summary>Rolling average velocity over last 90 days (devices/day)</summary>
        public double Velocity90 { get; set; }
        
        /// <summary>Velocity trend classification</summary>
        public TrendState TrendState { get; set; }
        
        /// <summary>7-day rolling average for recent trend</summary>
        public double Velocity7Day { get; set; }
        
        /// <summary>Week-over-week velocity change percentage</summary>
        public double WeekOverWeekChange { get; set; }
        
        /// <summary>Trend direction description</summary>
        public string TrendDescription => TrendState switch
        {
            TrendState.Accelerating => "📈 Accelerating",
            TrendState.Steady => "➡️ Steady",
            TrendState.Declining => "📉 Declining",
            TrendState.Stalled => "⏸️ Stalled",
            _ => "❓ Unknown"
        };
        
        /// <summary>Devices per week (derived from daily velocity)</summary>
        public double DevicesPerWeek => Velocity7Day * 7;
    }

    /// <summary>
    /// Trend state classification
    /// </summary>
    public enum TrendState
    {
        Unknown,
        Accelerating,
        Steady,
        Declining,
        Stalled
    }

    #endregion

    #region Confidence Score Models

    /// <summary>
    /// Inputs used to calculate enrollment confidence score.
    /// </summary>
    public class ConfidenceInputs
    {
        // Velocity metrics
        public double Velocity30 { get; set; }
        public double Velocity60 { get; set; }
        public double Velocity90 { get; set; }
        
        // Success/Retry metrics
        public double FirstAttemptSuccessRate { get; set; } = 0.85; // Default 85% if unknown
        public int EnrollmentRetryCount { get; set; }
        public int DuplicateDeviceObjectCount { get; set; }
        
        // Enrollment-time dependency complexity
        public int RequiredAppCount { get; set; }
        public int BlockingESPAppCount { get; set; }
        public int DeviceTargetedAssignmentCount { get; set; }
        
        // Conditional Access indicators
        public bool HasBlockingCAPolicy { get; set; }
        public bool RequiresMFA { get; set; }
        public bool RequiresCompliantDevice { get; set; }
        public int RiskySignInBlockCount { get; set; }
        
        // Infrastructure indicators
        public bool HasCMG { get; set; }
        public bool HasCoManagement { get; set; }
        public bool HasAutopilot { get; set; }
        
        // Current state
        public double CurrentEnrollmentPct { get; set; }
        public int DaysSinceLastEnrollment { get; set; }
    }

    /// <summary>
    /// Result of enrollment confidence score calculation.
    /// </summary>
    public class EnrollmentConfidenceResult
    {
        /// <summary>Overall confidence score (0-100)</summary>
        public int Score { get; set; }
        
        /// <summary>Confidence band classification</summary>
        public ConfidenceBand Band { get; set; }
        
        /// <summary>Human-readable explanation of the score</summary>
        public string Explanation { get; set; } = string.Empty;
        
        /// <summary>Top positive drivers (what's helping)</summary>
        public List<ScoreDriver> TopDrivers { get; set; } = new();
        
        /// <summary>Top detractors (what's hurting)</summary>
        public List<ScoreDriver> TopDetractors { get; set; } = new();
        
        /// <summary>Detailed score breakdown by category</summary>
        public ScoreBreakdown Breakdown { get; set; } = new();
        
        /// <summary>Score display with emoji indicator</summary>
        public string ScoreDisplay => Band switch
        {
            ConfidenceBand.High => $"🟢 {Score}/100 High Confidence",
            ConfidenceBand.Medium => $"🟡 {Score}/100 Medium Confidence",
            ConfidenceBand.Low => $"🔴 {Score}/100 Low Confidence",
            _ => $"⚪ {Score}/100"
        };
        
        /// <summary>Color for UI binding</summary>
        public string BandColor => Band switch
        {
            ConfidenceBand.High => "#22C55E",    // Green
            ConfidenceBand.Medium => "#EAB308",  // Yellow
            ConfidenceBand.Low => "#EF4444",     // Red
            _ => "#6B7280"                        // Gray
        };
    }

    /// <summary>
    /// Confidence band classification
    /// </summary>
    public enum ConfidenceBand
    {
        Low,      // 0-49
        Medium,   // 50-74
        High      // 75-100
    }

    /// <summary>
    /// A factor that contributes to or detracts from the confidence score.
    /// </summary>
    public class ScoreDriver
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Impact { get; set; } // Positive or negative points
        public string Category { get; set; } = string.Empty;
        
        public string ImpactDisplay => Impact >= 0 ? $"+{Impact}" : $"{Impact}";
        public string ImpactColor => Impact >= 0 ? "#22C55E" : "#EF4444";
    }

    /// <summary>
    /// Detailed breakdown of confidence score by category.
    /// </summary>
    public class ScoreBreakdown
    {
        public int VelocityScore { get; set; }
        public int SuccessRateScore { get; set; }
        public int ComplexityScore { get; set; }
        public int InfrastructureScore { get; set; }
        public int ConditionalAccessScore { get; set; }
        
        public int VelocityWeight { get; set; } = 30;
        public int SuccessRateWeight { get; set; } = 25;
        public int ComplexityWeight { get; set; } = 20;
        public int InfrastructureWeight { get; set; } = 15;
        public int ConditionalAccessWeight { get; set; } = 10;
    }

    #endregion

    #region Stall Risk Models

    /// <summary>
    /// Stall risk assessment ("Trust Trough" detection)
    /// </summary>
    public class StallRiskAssessment
    {
        public bool IsAtRisk { get; set; }
        public StallRiskLevel RiskLevel { get; set; }
        public string RiskDescription { get; set; } = string.Empty;
        public int DaysAtRisk { get; set; }
        public double EnrollmentPctAtRiskStart { get; set; }
        public List<string> ContributingFactors { get; set; } = new();
        public List<string> RecommendedActions { get; set; } = new();
        
        /// <summary>"Trust Trough" risk: stuck at 50-60% with declining velocity</summary>
        public bool IsTrustTroughRisk { get; set; }
        
        public string RiskLevelDisplay => RiskLevel switch
        {
            StallRiskLevel.Critical => "🔴 Critical",
            StallRiskLevel.High => "🟠 High",
            StallRiskLevel.Medium => "🟡 Medium",
            StallRiskLevel.Low => "🟢 Low",
            StallRiskLevel.None => "✅ None",
            _ => "⚪ Unknown"
        };
    }

    public enum StallRiskLevel
    {
        None,
        Low,
        Medium,
        High,
        Critical
    }

    #endregion

    #region Milestone Models

    /// <summary>
    /// Enrollment milestone definition
    /// </summary>
    public class EnrollmentMilestone
    {
        public int Percentage { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string WhatChanges { get; set; } = string.Empty;
        public bool IsAchieved { get; set; }
        public bool IsNext { get; set; }
        public bool IsCurrent { get; set; }
        public DateTime? AchievedDate { get; set; }
        public string RecommendedPlaybook { get; set; } = string.Empty;
        
        /// <summary>Special flag for "Trust Trough" milestone (50-60%)</summary>
        public bool IsTrustTrough { get; set; }
        
        public string DisplayIcon => IsAchieved ? "✅" : (IsNext ? "🎯" : "⏳");
        
        public string StatusColor => IsAchieved ? "#22C55E" : (IsNext ? "#3B82F6" : "#6B7280");
    }

    #endregion

    #region Playbook Models

    /// <summary>
    /// Enrollment playbook (guided action plan)
    /// </summary>
    public class EnrollmentPlaybook
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public PlaybookType Type { get; set; }
        public PlaybookRiskLevel RiskLevel { get; set; }
        public string EstimatedTime { get; set; } = string.Empty;
        public int ExpectedImpactDevices { get; set; }
        public List<PlaybookStep> Steps { get; set; } = new();
        public List<string> Prerequisites { get; set; } = new();
        public bool IsRecommended { get; set; }
        public string RecommendationReason { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        
        public string RiskLevelDisplay => RiskLevel switch
        {
            PlaybookRiskLevel.Low => "🟢 Low Risk",
            PlaybookRiskLevel.Medium => "🟡 Medium Risk",
            PlaybookRiskLevel.High => "🔴 High Risk",
            _ => "⚪ Unknown"
        };
        
        public string TypeIcon => Type switch
        {
            PlaybookType.RebuildMomentum => "🚀",
            PlaybookType.ReduceDependencies => "🔧",
            PlaybookType.AutopilotHygiene => "✈️",
            PlaybookType.StallRecovery => "🔄",
            PlaybookType.ScaleUp => "📈",
            _ => "📋"
        };
    }

    public enum PlaybookType
    {
        RebuildMomentum,
        ReduceDependencies,
        AutopilotHygiene,
        StallRecovery,
        ScaleUp,
        Custom
    }

    public enum PlaybookRiskLevel
    {
        Low,
        Medium,
        High
    }

    /// <summary>
    /// A single step in an enrollment playbook
    /// </summary>
    public class PlaybookStep
    {
        public int Order { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty; // "Review", "Configure", "Execute", "Verify"
        public bool IsOptional { get; set; }
        public bool RequiresConfirmation { get; set; }
        public string PortalLink { get; set; } = string.Empty;
        public List<string> Checklist { get; set; } = new();
        public string ExpectedOutcome { get; set; } = string.Empty;
        public string RollbackInstructions { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        
        public string ActionTypeIcon => ActionType switch
        {
            "Review" => "👁️",
            "Configure" => "⚙️",
            "Execute" => "▶️",
            "Verify" => "✓",
            _ => "📋"
        };
    }

    /// <summary>
    /// Device batch for enrollment (from playbook)
    /// Note: Named differently from Models.EnrollmentBatch to avoid conflict
    /// </summary>
    public class PlaybookEnrollmentBatch
    {
        public string BatchId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public List<DeviceCandidate> Devices { get; set; } = new();
        public int DeviceCount => Devices.Count;
        public double AverageReadinessScore { get; set; }
        public PlaybookRiskLevel RiskLevel { get; set; }
        public string SelectionCriteria { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Device candidate for enrollment batch
    /// </summary>
    public class DeviceCandidate
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceNameHashed { get; set; } = string.Empty; // For telemetry (privacy)
        public double ReadinessScore { get; set; }
        public bool IsCompliant { get; set; }
        public bool HasRecentCheckIn { get; set; }
        public DateTime? LastCheckIn { get; set; }
        public bool HasBitLockerRecovery { get; set; }
        public string OperatingSystem { get; set; } = string.Empty;
        public List<string> RiskFactors { get; set; } = new();
        public bool IsSelected { get; set; } = true;
        
        public string ReadinessDisplay => ReadinessScore switch
        {
            >= 80 => $"🟢 {ReadinessScore:F0}",
            >= 60 => $"🟡 {ReadinessScore:F0}",
            _ => $"🔴 {ReadinessScore:F0}"
        };
    }

    #endregion

    #region Aggregate Result Models

    /// <summary>
    /// Complete enrollment analytics result
    /// </summary>
    public class EnrollmentAnalyticsResult
    {
        // Core metrics
        public int TotalConfigMgrDevices { get; set; }
        public int TotalIntuneDevices { get; set; }
        public int Gap => TotalConfigMgrDevices - TotalIntuneDevices;
        public double EnrolledPct => TotalConfigMgrDevices > 0 
            ? (double)TotalIntuneDevices / TotalConfigMgrDevices * 100 
            : 0;
        
        // Time-series data
        public List<EnrollmentSnapshot> Snapshots { get; set; } = new();
        
        // Trend analysis
        public EnrollmentTrendAnalysis Trend { get; set; } = new();
        
        // Confidence score
        public EnrollmentConfidenceResult Confidence { get; set; } = new();
        
        // Stall risk
        public StallRiskAssessment StallRisk { get; set; } = new();
        
        // Milestones
        public List<EnrollmentMilestone> Milestones { get; set; } = new();
        public EnrollmentMilestone? NextMilestone { get; set; }
        
        // Playbook recommendations
        public List<EnrollmentPlaybook> RecommendedPlaybooks { get; set; } = new();
        
        // Low-risk batch (pre-computed)
        public PlaybookEnrollmentBatch? LowRiskBatch { get; set; }
        
        // Metadata
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public TimeSpan ComputationTime { get; set; }
        public string DataSource { get; set; } = string.Empty;
    }

    #endregion
}
