using System;
using System.Collections.Generic;

namespace ZeroTrustMigrationAddin.Models
{
    /// <summary>
    /// Tracks real-time progress of autonomous enrollment execution
    /// </summary>
    public class EnrollmentProgress
    {
        /// <summary>
        /// Associated plan ID
        /// </summary>
        public string PlanId { get; set; } = string.Empty;

        /// <summary>
        /// Total devices in the plan
        /// </summary>
        public int TotalDevices { get; set; }

        /// <summary>
        /// Devices successfully enrolled
        /// </summary>
        public int DevicesEnrolled { get; set; }

        /// <summary>
        /// Devices that failed enrollment
        /// </summary>
        public int DevicesFailed { get; set; }

        /// <summary>
        /// Devices not yet attempted
        /// </summary>
        public int DevicesPending { get; set; }

        /// <summary>
        /// Success rate percentage (0-100)
        /// </summary>
        public double SuccessRate
        {
            get
            {
                int attempted = DevicesEnrolled + DevicesFailed;
                if (attempted == 0) return 0;
                return (double)DevicesEnrolled / attempted * 100.0;
            }
        }

        /// <summary>
        /// Overall progress percentage (0-100)
        /// </summary>
        public double ProgressPercentage
        {
            get
            {
                if (TotalDevices == 0) return 0;
                return (double)(DevicesEnrolled + DevicesFailed) / TotalDevices * 100.0;
            }
        }

        /// <summary>
        /// Current batch being executed (1-based)
        /// </summary>
        public int CurrentBatch { get; set; }

        /// <summary>
        /// Total number of batches
        /// </summary>
        public int TotalBatches { get; set; }

        /// <summary>
        /// When execution started
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Estimated completion time
        /// </summary>
        public DateTime? EstimatedCompletion { get; set; }

        /// <summary>
        /// Recent enrollment results (last 10)
        /// </summary>
        public List<EnrollmentResult> RecentResults { get; set; } = new List<EnrollmentResult>();

        /// <summary>
        /// Current status message
        /// </summary>
        public string StatusMessage { get; set; } = string.Empty;

        /// <summary>
        /// Whether execution is currently paused
        /// </summary>
        public bool IsPaused { get; set; }

        /// <summary>
        /// Reason for pause (if paused)
        /// </summary>
        public string? PauseReason { get; set; }

        /// <summary>
        /// Last update timestamp
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Result of a single device enrollment attempt
    /// </summary>
    public class EnrollmentResult
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Device name
        /// </summary>
        public string DeviceName { get; set; } = string.Empty;

        /// <summary>
        /// When enrollment was attempted
        /// </summary>
        public DateTime AttemptTime { get; set; } = DateTime.Now;

        /// <summary>
        /// Whether enrollment succeeded
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// How long the enrollment took
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Batch number this enrollment was part of
        /// </summary>
        public int BatchNumber { get; set; }

        /// <summary>
        /// Number of retry attempts (0 = first attempt)
        /// </summary>
        public int RetryCount { get; set; }
    }
}
