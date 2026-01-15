using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Services
{
    /// <summary>
    /// Configurable scoring weights and thresholds for enrollment confidence calculations.
    /// Loaded from JSON config file with hot-reload support.
    /// </summary>
    public class EnrollmentScoringOptions
    {
        private static readonly string ConfigFileName = "enrollment-scoring-config.json";
        private static EnrollmentScoringOptions? _current;
        private static DateTime _lastLoadTime = DateTime.MinValue;
        private static readonly object _lock = new();

        #region Weight Configuration (sum should = 100)

        /// <summary>Weight for velocity metrics (30% default)</summary>
        [JsonPropertyName("velocityWeight")]
        public int VelocityWeight { get; set; } = 30;

        /// <summary>Weight for success rate metrics (25% default)</summary>
        [JsonPropertyName("successRateWeight")]
        public int SuccessRateWeight { get; set; } = 25;

        /// <summary>Weight for enrollment complexity (20% default)</summary>
        [JsonPropertyName("complexityWeight")]
        public int ComplexityWeight { get; set; } = 20;

        /// <summary>Weight for infrastructure readiness (15% default)</summary>
        [JsonPropertyName("infrastructureWeight")]
        public int InfrastructureWeight { get; set; } = 15;

        /// <summary>Weight for Conditional Access factors (10% default)</summary>
        [JsonPropertyName("conditionalAccessWeight")]
        public int ConditionalAccessWeight { get; set; } = 10;

        #endregion

        #region Velocity Thresholds

        /// <summary>Minimum devices/day to be considered "good" velocity</summary>
        [JsonPropertyName("goodVelocityThreshold")]
        public double GoodVelocityThreshold { get; set; } = 5.0;

        /// <summary>Minimum devices/day to be considered "excellent" velocity</summary>
        [JsonPropertyName("excellentVelocityThreshold")]
        public double ExcellentVelocityThreshold { get; set; } = 15.0;

        /// <summary>Threshold for "flat" velocity detection (abs change per day)</summary>
        [JsonPropertyName("flatVelocityDeltaThreshold")]
        public double FlatVelocityDeltaThreshold { get; set; } = 0.5;

        /// <summary>Days of flat/declining velocity before flagging stall risk</summary>
        [JsonPropertyName("stallRiskDaysThreshold")]
        public int StallRiskDaysThreshold { get; set; } = 60;

        #endregion

        #region Trust Trough Configuration

        /// <summary>Lower bound of "Trust Trough" zone (%)</summary>
        [JsonPropertyName("trustTroughLowerPct")]
        public double TrustTroughLowerPct { get; set; } = 50.0;

        /// <summary>Upper bound of "Trust Trough" zone (%)</summary>
        [JsonPropertyName("trustTroughUpperPct")]
        public double TrustTroughUpperPct { get; set; } = 60.0;

        #endregion

        #region Complexity Thresholds

        /// <summary>Number of required apps considered "low" complexity</summary>
        [JsonPropertyName("lowComplexityAppCount")]
        public int LowComplexityAppCount { get; set; } = 5;

        /// <summary>Number of required apps considered "high" complexity</summary>
        [JsonPropertyName("highComplexityAppCount")]
        public int HighComplexityAppCount { get; set; } = 15;

        /// <summary>Number of ESP-blocking apps considered problematic</summary>
        [JsonPropertyName("espBlockingAppWarningThreshold")]
        public int ESPBlockingAppWarningThreshold { get; set; } = 3;

        #endregion

        #region Batch Configuration

        /// <summary>Minimum batch size for "Rebuild Momentum" playbook</summary>
        [JsonPropertyName("minLowRiskBatchSize")]
        public int MinLowRiskBatchSize { get; set; } = 20;

        /// <summary>Maximum batch size for "Rebuild Momentum" playbook</summary>
        [JsonPropertyName("maxLowRiskBatchSize")]
        public int MaxLowRiskBatchSize { get; set; } = 50;

        /// <summary>Minimum readiness score for low-risk batch inclusion</summary>
        [JsonPropertyName("lowRiskReadinessThreshold")]
        public double LowRiskReadinessThreshold { get; set; } = 75.0;

        /// <summary>Maximum days since last check-in for batch inclusion</summary>
        [JsonPropertyName("maxDaysSinceCheckIn")]
        public int MaxDaysSinceCheckIn { get; set; } = 7;

        #endregion

        #region Confidence Score Modifiers

        /// <summary>Points added for having CMG deployed</summary>
        [JsonPropertyName("cmgBonus")]
        public int CMGBonus { get; set; } = 10;

        /// <summary>Points added for having co-management enabled</summary>
        [JsonPropertyName("coManagementBonus")]
        public int CoManagementBonus { get; set; } = 8;

        /// <summary>Points added for having Autopilot configured</summary>
        [JsonPropertyName("autopilotBonus")]
        public int AutopilotBonus { get; set; } = 5;

        /// <summary>Points deducted for each ESP-blocking app</summary>
        [JsonPropertyName("espBlockingAppPenalty")]
        public int ESPBlockingAppPenalty { get; set; } = 3;

        /// <summary>Points deducted for blocking CA policy</summary>
        [JsonPropertyName("blockingCAPenalty")]
        public int BlockingCAPenalty { get; set; } = 10;

        /// <summary>Points deducted per day of stalled enrollment (max 20)</summary>
        [JsonPropertyName("stallDayPenalty")]
        public double StallDayPenalty { get; set; } = 0.5;

        #endregion

        #region Static Accessor

        /// <summary>
        /// Gets the current scoring options, loading from config file if needed.
        /// Supports hot-reload (checks file modification time).
        /// </summary>
        public static EnrollmentScoringOptions Current
        {
            get
            {
                lock (_lock)
                {
                    var configPath = GetConfigPath();
                    
                    // Check if we need to reload
                    if (_current == null || ShouldReload(configPath))
                    {
                        _current = LoadFromFile(configPath);
                        _lastLoadTime = DateTime.UtcNow;
                    }
                    
                    return _current;
                }
            }
        }

        private static bool ShouldReload(string configPath)
        {
            if (!File.Exists(configPath)) return false;
            
            var fileModTime = File.GetLastWriteTimeUtc(configPath);
            return fileModTime > _lastLoadTime;
        }

        private static string GetConfigPath()
        {
            // Try app directory first
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var appPath = Path.Combine(appDir, ConfigFileName);
            if (File.Exists(appPath)) return appPath;
            
            // Try local app data
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dataPath = Path.Combine(localAppData, "ZeroTrustMigrationAddin", ConfigFileName);
            if (File.Exists(dataPath)) return dataPath;
            
            // Return app directory path (will create default there)
            return appPath;
        }

        private static EnrollmentScoringOptions LoadFromFile(string configPath)
        {
            try
            {
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var options = JsonSerializer.Deserialize<EnrollmentScoringOptions>(json);
                    
                    if (options != null)
                    {
                        Instance.Info($"[SCORING] Loaded scoring options from {configPath}");
                        LogScoringOptions(options);
                        return options;
                    }
                }
            }
            catch (Exception ex)
            {
                Instance.Warning($"[SCORING] Failed to load config from {configPath}: {ex.Message}");
            }
            
            // Return default options
            var defaults = new EnrollmentScoringOptions();
            Instance.Info("[SCORING] Using default scoring options");
            
            // Save defaults for future editing
            try
            {
                SaveToFile(defaults, configPath);
            }
            catch { /* Ignore save errors */ }
            
            return defaults;
        }

        /// <summary>
        /// Saves the current options to the config file.
        /// </summary>
        public static void SaveToFile(EnrollmentScoringOptions options, string? path = null)
        {
            var configPath = path ?? GetConfigPath();
            
            try
            {
                var directory = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                var json = JsonSerializer.Serialize(options, jsonOptions);
                File.WriteAllText(configPath, json);
                
                Instance.Info($"[SCORING] Saved scoring options to {configPath}");
            }
            catch (Exception ex)
            {
                Instance.Error($"[SCORING] Failed to save config to {configPath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Forces a reload of the configuration from disk.
        /// </summary>
        public static void ForceReload()
        {
            lock (_lock)
            {
                _current = null;
                _lastLoadTime = DateTime.MinValue;
            }
        }

        private static void LogScoringOptions(EnrollmentScoringOptions options)
        {
            Instance.Debug($"[SCORING] Weights: Velocity={options.VelocityWeight}%, " +
                          $"SuccessRate={options.SuccessRateWeight}%, " +
                          $"Complexity={options.ComplexityWeight}%, " +
                          $"Infrastructure={options.InfrastructureWeight}%, " +
                          $"CA={options.ConditionalAccessWeight}%");
            
            Instance.Debug($"[SCORING] Velocity thresholds: Good={options.GoodVelocityThreshold}/day, " +
                          $"Excellent={options.ExcellentVelocityThreshold}/day, " +
                          $"FlatDelta={options.FlatVelocityDeltaThreshold}");
            
            Instance.Debug($"[SCORING] Trust Trough: {options.TrustTroughLowerPct}%-{options.TrustTroughUpperPct}%");
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validates that the weights sum to 100.
        /// </summary>
        public bool ValidateWeights(out string error)
        {
            var totalWeight = VelocityWeight + SuccessRateWeight + ComplexityWeight + 
                             InfrastructureWeight + ConditionalAccessWeight;
            
            if (totalWeight != 100)
            {
                error = $"Weights must sum to 100, but current total is {totalWeight}";
                return false;
            }
            
            error = string.Empty;
            return true;
        }

        #endregion
    }
}
