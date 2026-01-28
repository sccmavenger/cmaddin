using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZeroTrustMigrationAddin.Models
{
    /// <summary>
    /// Represents a single point-in-time snapshot of enrollment data for historical tracking.
    /// Used to track real historical progress for trend graphs.
    /// Named differently from EnrollmentSnapshot in EnrollmentAnalyticsModels to avoid conflicts.
    /// </summary>
    public class HistoricalEnrollmentDataPoint
    {
        /// <summary>
        /// UTC timestamp when this snapshot was recorded
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Total Windows 10/11 devices in ConfigMgr
        /// </summary>
        public int TotalDevices { get; set; }

        /// <summary>
        /// Devices that are co-managed or Intune-managed
        /// </summary>
        public int CloudManagedDevices { get; set; }

        /// <summary>
        /// Devices in ConfigMgr only (not yet co-managed)
        /// </summary>
        public int ConfigMgrOnlyDevices { get; set; }

        /// <summary>
        /// Cloud-native devices (Entra joined, no ConfigMgr)
        /// </summary>
        public int CloudNativeDevices { get; set; }

        /// <summary>
        /// Enrollment percentage at this point in time
        /// </summary>
        public double EnrollmentPercentage => TotalDevices > 0 
            ? Math.Round((double)CloudManagedDevices / TotalDevices * 100, 1) 
            : 0;

        /// <summary>
        /// Was this data from real queries (true) or mock data (false)
        /// </summary>
        public bool IsRealData { get; set; }

        /// <summary>
        /// Optional: Hash of the data source for change detection
        /// </summary>
        public string? DataSourceHash { get; set; }
    }

    /// <summary>
    /// Container for all historical enrollment data.
    /// Stored at %LOCALAPPDATA%\ZeroTrustMigrationAddin\enrollment-history.json
    /// </summary>
    public class EnrollmentHistory
    {
        /// <summary>
        /// Version of the history file format (for future migrations)
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// When the history file was first created
        /// </summary>
        public DateTime FirstRecordedDate { get; set; }

        /// <summary>
        /// When the history file was last updated
        /// </summary>
        public DateTime LastUpdatedDate { get; set; }

        /// <summary>
        /// Anonymous identifier for this installation (for telemetry correlation)
        /// </summary>
        public string? InstallationId { get; set; }

        /// <summary>
        /// All historical snapshots, ordered by timestamp
        /// </summary>
        public List<HistoricalEnrollmentDataPoint> Snapshots { get; set; } = new();

        /// <summary>
        /// Summary statistics for telemetry
        /// </summary>
        public EnrollmentSummaryStats? SummaryStats { get; set; }

        /// <summary>
        /// Get the number of days we have real historical data
        /// </summary>
        [JsonIgnore]
        public int DaysOfHistory => Snapshots.Count > 1 
            ? (int)(Snapshots[^1].Timestamp - Snapshots[0].Timestamp).TotalDays 
            : 0;

        /// <summary>
        /// Whether we have enough data for meaningful trend analysis (at least 7 days)
        /// </summary>
        [JsonIgnore]
        public bool HasSufficientHistory => DaysOfHistory >= 7;
    }

    /// <summary>
    /// Aggregated summary statistics for telemetry reporting.
    /// These are calculated periodically and sent anonymously.
    /// </summary>
    public class EnrollmentSummaryStats
    {
        /// <summary>
        /// When these stats were last calculated
        /// </summary>
        public DateTime CalculatedAt { get; set; }

        /// <summary>
        /// Peak total devices ever seen
        /// </summary>
        public int PeakTotalDevices { get; set; }

        /// <summary>
        /// Current total devices
        /// </summary>
        public int CurrentTotalDevices { get; set; }

        /// <summary>
        /// Devices migrated in the last 7 days
        /// </summary>
        public int DevicesMigratedLast7Days { get; set; }

        /// <summary>
        /// Devices migrated in the last 30 days
        /// </summary>
        public int DevicesMigratedLast30Days { get; set; }

        /// <summary>
        /// Devices migrated in the last 90 days
        /// </summary>
        public int DevicesMigratedLast90Days { get; set; }

        /// <summary>
        /// Average daily migration velocity (devices/day)
        /// </summary>
        public double AverageDailyVelocity { get; set; }

        /// <summary>
        /// Current enrollment percentage
        /// </summary>
        public double CurrentEnrollmentPercentage { get; set; }

        /// <summary>
        /// Enrollment percentage 7 days ago (for delta calculation)
        /// </summary>
        public double EnrollmentPercentage7DaysAgo { get; set; }

        /// <summary>
        /// Enrollment percentage 30 days ago (for delta calculation)
        /// </summary>
        public double EnrollmentPercentage30DaysAgo { get; set; }

        /// <summary>
        /// Days since first real data was collected
        /// </summary>
        public int DaysSinceFirstData { get; set; }

        /// <summary>
        /// Number of data points collected
        /// </summary>
        public int TotalDataPoints { get; set; }

        /// <summary>
        /// Whether the installation is actively being used (data in last 7 days)
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Trend direction: "Accelerating", "Steady", "Slowing", "Stalled"
        /// </summary>
        public string TrendDirection { get; set; } = "Unknown";

        /// <summary>
        /// Estimated days to reach 100% enrollment at current velocity
        /// </summary>
        public int? EstimatedDaysToCompletion { get; set; }
    }

    /// <summary>
    /// Options for how trend data should be displayed
    /// </summary>
    public class TrendDisplayOptions
    {
        /// <summary>
        /// True if showing projected data (insufficient history)
        /// </summary>
        public bool IsProjected { get; set; }

        /// <summary>
        /// Number of real data points available
        /// </summary>
        public int RealDataPoints { get; set; }

        /// <summary>
        /// Days of actual historical data
        /// </summary>
        public int DaysOfRealData { get; set; }

        /// <summary>
        /// Message to display to user about data quality
        /// </summary>
        public string DataQualityMessage { get; set; } = string.Empty;

        /// <summary>
        /// When first real data was collected
        /// </summary>
        public DateTime? FirstDataDate { get; set; }
    }
}
