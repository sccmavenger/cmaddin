using System;
using System.Collections.Generic;

namespace ZeroTrustMigrationAddin.Models
{
    /// <summary>
    /// Defines the configuration and goals for autonomous enrollment operations
    /// </summary>
    public class EnrollmentGoals
    {
        /// <summary>
        /// Target date to complete all enrollments
        /// </summary>
        public DateTime TargetCompletionDate { get; set; }

        /// <summary>
        /// Maximum devices to enroll per day (null = let AI decide)
        /// </summary>
        public int? MaxDevicesPerDay { get; set; }

        /// <summary>
        /// Preferred batch size for enrollments (null = let AI decide)
        /// </summary>
        public int? PreferredBatchSize { get; set; }

        /// <summary>
        /// Risk tolerance level for autonomous operations
        /// </summary>
        public RiskTolerance RiskLevel { get; set; } = RiskTolerance.Conservative;

        /// <summary>
        /// Operating hours schedule for enrollments
        /// </summary>
        public OperatingHours Schedule { get; set; } = OperatingHours.BusinessHours;

        /// <summary>
        /// Require human approval for all plans (Phase 1: always true)
        /// </summary>
        public bool RequireApprovalForAllPlans { get; set; } = true;

        /// <summary>
        /// Pause if failure rate exceeds this percentage (default: 15%)
        /// </summary>
        public double FailureThresholdPercent { get; set; } = 15.0;

        /// <summary>
        /// Device IDs to exclude from autonomous enrollment
        /// </summary>
        public List<string> ExcludedDeviceIds { get; set; } = new List<string>();

        /// <summary>
        /// Device IDs to prioritize (enroll first)
        /// </summary>
        public List<string> PriorityDeviceIds { get; set; } = new List<string>();

        /// <summary>
        /// Minimum readiness score required for enrollment (0-100)
        /// </summary>
        public double MinimumReadinessScore
        {
            get
            {
                return RiskLevel switch
                {
                    RiskTolerance.Conservative => 60.0, // Good/Excellent only
                    RiskTolerance.Balanced => 50.0,     // Fair or better
                    RiskTolerance.Aggressive => 0.0,    // AI decides
                    _ => 60.0
                };
            }
        }

        /// <summary>
        /// Maximum batch size based on risk tolerance
        /// </summary>
        public int MaxBatchSize
        {
            get
            {
                if (PreferredBatchSize.HasValue)
                    return PreferredBatchSize.Value;

                return RiskLevel switch
                {
                    RiskTolerance.Conservative => 25,
                    RiskTolerance.Balanced => 50,
                    RiskTolerance.Aggressive => 100,
                    _ => 25
                };
            }
        }
    }

    /// <summary>
    /// Risk tolerance levels for autonomous enrollment
    /// </summary>
    public enum RiskTolerance
    {
        /// <summary>
        /// Conservative: Only high-quality devices, small batches, require approval
        /// Phase 1 default
        /// </summary>
        Conservative,

        /// <summary>
        /// Balanced: Mix of auto and manual approval based on risk
        /// Phase 2 feature
        /// </summary>
        Balanced,

        /// <summary>
        /// Aggressive: Full autonomy, AI decides everything
        /// Phase 3 feature
        /// </summary>
        Aggressive
    }

    /// <summary>
    /// Operating hours schedule for enrollment execution
    /// </summary>
    public enum OperatingHours
    {
        /// <summary>
        /// Business hours only (8 AM - 6 PM, Monday-Friday)
        /// </summary>
        BusinessHours,

        /// <summary>
        /// Extended hours (6 AM - 10 PM, Monday-Saturday)
        /// </summary>
        Extended,

        /// <summary>
        /// 24/7 operation (any time)
        /// </summary>
        Always
    }
}
