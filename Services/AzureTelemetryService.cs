using System;
using System.Collections.Generic;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace ZeroTrustMigrationAddin.Services
{
    /// <summary>
    /// Azure Application Insights telemetry service for privacy-safe usage analytics.
    /// Only tracks feature usage, performance metrics, and error rates.
    /// NO PII: Device names, usernames, tenant IDs are NEVER sent to Azure.
    /// </summary>
    public sealed class AzureTelemetryService : IDisposable
    {
        private static readonly Lazy<AzureTelemetryService> _instance = 
            new Lazy<AzureTelemetryService>(() => new AzureTelemetryService());
        
        private readonly TelemetryClient? _telemetryClient;
        private readonly string _anonymousUserId;
        private readonly bool _isEnabled;

        private const string ConnectionString = 
            "InstrumentationKey=30d5a38c-0d53-44f8-b26b-8b83d89b57b3;" +
            "IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;" +
            "LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;" +
            "ApplicationId=2aef4b56-7293-40e1-aaa5-445d736beb1c";

        public static AzureTelemetryService Instance => _instance.Value;

        private AzureTelemetryService()
        {
            try
            {
                // Generate anonymous user ID from machine GUID
                _anonymousUserId = GetAnonymousUserId();

                // Initialize telemetry configuration
                var config = TelemetryConfiguration.CreateDefault();
                config.ConnectionString = ConnectionString;

                _telemetryClient = new TelemetryClient(config);
                _telemetryClient.Context.User.Id = _anonymousUserId;
                _telemetryClient.Context.Session.Id = Guid.NewGuid().ToString();
                _telemetryClient.Context.Device.OperatingSystem = Environment.OSVersion.ToString();
                _telemetryClient.Context.Component.Version = GetAppVersion();
                
                _isEnabled = true;

                FileLogger.Instance.Info("[TELEMETRY] Azure Application Insights initialized successfully");
                FileLogger.Instance.Info($"[TELEMETRY] Anonymous User ID: {_anonymousUserId}");
                FileLogger.Instance.Info($"[TELEMETRY] Session ID: {_telemetryClient.Context.Session.Id}");
            }
            catch (Exception ex)
            {
                _isEnabled = false;
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to initialize: {ex.Message}");
                FileLogger.Instance.Info("[TELEMETRY] Application will continue without telemetry");
            }
        }

        /// <summary>
        /// Track a feature usage event (e.g., button click, menu action).
        /// </summary>
        public void TrackEvent(string eventName, Dictionary<string, string>? properties = null, Dictionary<string, double>? metrics = null)
        {
            if (!_isEnabled || _telemetryClient == null) return;

            try
            {
                var sanitizedProperties = properties != null 
                    ? SanitizeProperties(properties) 
                    : new Dictionary<string, string>();

                // Add common context
                sanitizedProperties["AppVersion"] = GetAppVersion();
                sanitizedProperties["OSVersion"] = Environment.OSVersion.VersionString;

                _telemetryClient.TrackEvent(eventName, sanitizedProperties, metrics);
                
                FileLogger.Instance.Debug($"[TELEMETRY] Event: {eventName}");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track event '{eventName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Track a numeric metric (e.g., device count, API latency).
        /// </summary>
        public void TrackMetric(string metricName, double value, Dictionary<string, string>? properties = null)
        {
            if (!_isEnabled || _telemetryClient == null) return;

            try
            {
                var sanitizedProperties = properties != null 
                    ? SanitizeProperties(properties) 
                    : null;

                _telemetryClient.TrackMetric(metricName, value, sanitizedProperties);
                
                FileLogger.Instance.Debug($"[TELEMETRY] Metric: {metricName} = {value}");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track metric '{metricName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Track an exception with sanitized message to remove PII.
        /// </summary>
        public void TrackException(Exception exception, Dictionary<string, string>? properties = null, Dictionary<string, double>? metrics = null)
        {
            if (!_isEnabled || _telemetryClient == null) return;

            try
            {
                // Sanitize exception message and stack trace
                var sanitizedException = SanitizeException(exception);
                
                var sanitizedProperties = properties != null 
                    ? SanitizeProperties(properties) 
                    : new Dictionary<string, string>();

                sanitizedProperties["ExceptionType"] = exception.GetType().Name;
                sanitizedProperties["AppVersion"] = GetAppVersion();

                var telemetry = new ExceptionTelemetry(sanitizedException)
                {
                    SeverityLevel = SeverityLevel.Error
                };

                foreach (var prop in sanitizedProperties)
                {
                    telemetry.Properties[prop.Key] = prop.Value;
                }

                if (metrics != null)
                {
                    foreach (var metric in metrics)
                    {
                        telemetry.Metrics[metric.Key] = metric.Value;
                    }
                }

                _telemetryClient.TrackException(telemetry);
                
                FileLogger.Instance.Debug($"[TELEMETRY] Exception: {exception.GetType().Name}");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Track an API call or external dependency with timing.
        /// </summary>
        public void TrackDependency(string dependencyTypeName, string dependencyName, string data, DateTimeOffset startTime, TimeSpan duration, bool success)
        {
            if (!_isEnabled || _telemetryClient == null) return;

            try
            {
                var sanitizedData = SanitizeString(data);
                
                _telemetryClient.TrackDependency(dependencyTypeName, dependencyName, sanitizedData, startTime, duration, success);
                
                FileLogger.Instance.Debug($"[TELEMETRY] Dependency: {dependencyTypeName}/{dependencyName} - {duration.TotalMilliseconds}ms, success={success}");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track dependency '{dependencyName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Track a page view or window open event.
        /// </summary>
        public void TrackPageView(string pageName, Dictionary<string, string>? properties = null, Dictionary<string, double>? metrics = null)
        {
            if (!_isEnabled || _telemetryClient == null) return;

            try
            {
                var sanitizedProperties = properties != null 
                    ? SanitizeProperties(properties) 
                    : null;

                var telemetry = new PageViewTelemetry(pageName);
                
                if (sanitizedProperties != null)
                {
                    foreach (var prop in sanitizedProperties)
                    {
                        telemetry.Properties[prop.Key] = prop.Value;
                    }
                }

                if (metrics != null)
                {
                    foreach (var metric in metrics)
                    {
                        telemetry.Metrics[metric.Key] = metric.Value;
                    }
                }

                _telemetryClient.TrackPageView(telemetry);
                
                FileLogger.Instance.Debug($"[TELEMETRY] PageView: {pageName}");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track page view '{pageName}': {ex.Message}");
            }
        }

        #region Strategic Telemetry for Leadership Dashboards

        /// <summary>
        /// Track strategic migration metrics for leadership dashboards.
        /// Sends estate size bands (not exact counts) and migration progress percentages.
        /// </summary>
        public void TrackStrategicMetrics(
            int totalDevices,
            int cloudManagedDevices,
            int configMgrOnlyDevices,
            int cloudNativeDevices,
            double enrollmentPercentage,
            double dailyVelocity,
            string trendDirection)
        {
            if (!_isEnabled || _telemetryClient == null) return;

            try
            {
                // Convert exact counts to size bands for privacy
                var estateSizeBand = GetEstateSizeBand(totalDevices);
                
                var properties = new Dictionary<string, string>
                {
                    ["EstateSizeBand"] = estateSizeBand,
                    ["TrendDirection"] = trendDirection, // "Accelerating", "Steady", "Slowing", "Stalled"
                    ["EnrollmentBand"] = GetPercentageBand(enrollmentPercentage),
                    ["AppVersion"] = GetAppVersion()
                };

                var metrics = new Dictionary<string, double>
                {
                    ["EnrollmentPercentage"] = Math.Round(enrollmentPercentage, 1),
                    ["CloudManagedPercentage"] = totalDevices > 0 ? Math.Round((double)cloudManagedDevices / totalDevices * 100, 1) : 0,
                    ["CloudNativePercentage"] = totalDevices > 0 ? Math.Round((double)cloudNativeDevices / totalDevices * 100, 1) : 0,
                    ["ConfigMgrOnlyPercentage"] = totalDevices > 0 ? Math.Round((double)configMgrOnlyDevices / totalDevices * 100, 1) : 0,
                    ["DailyVelocity"] = Math.Round(dailyVelocity, 2)
                };

                _telemetryClient.TrackEvent("StrategicMetrics", properties, metrics);
                
                FileLogger.Instance.Info($"[TELEMETRY] StrategicMetrics: Estate={estateSizeBand}, Enrollment={enrollmentPercentage:F1}%, Velocity={dailyVelocity:F2}/day, Trend={trendDirection}");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track strategic metrics: {ex.Message}");
            }
        }

        /// <summary>
        /// Track estate snapshot for global aggregation (anonymized device counts).
        /// Used for understanding overall migration progress across all customers.
        /// </summary>
        public void TrackEstateSnapshot(
            int totalDevices,
            int cloudManagedDevices,
            int configMgrOnlyDevices,
            int cloudNativeDevices)
        {
            if (!_isEnabled || _telemetryClient == null) return;

            try
            {
                var estateSizeBand = GetEstateSizeBand(totalDevices);
                
                var properties = new Dictionary<string, string>
                {
                    ["EstateSizeBand"] = estateSizeBand,
                    ["AppVersion"] = GetAppVersion()
                };

                var metrics = new Dictionary<string, double>
                {
                    ["TotalDevices"] = totalDevices,
                    ["CloudManagedDevices"] = cloudManagedDevices,
                    ["ConfigMgrOnlyDevices"] = configMgrOnlyDevices,
                    ["CloudNativeDevices"] = cloudNativeDevices
                };

                _telemetryClient.TrackEvent("EstateSnapshot", properties, metrics);
                
                FileLogger.Instance.Debug($"[TELEMETRY] EstateSnapshot: Total={totalDevices}, CloudManaged={cloudManagedDevices}, ConfigMgrOnly={configMgrOnlyDevices}, CloudNative={cloudNativeDevices}");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track estate snapshot: {ex.Message}");
            }
        }

        /// <summary>
        /// Track migration milestone achievements (10%, 25%, 50%, 75%, 90%, 100%).
        /// </summary>
        public void TrackMigrationMilestone(int milestonePercentage, int totalDevices, int cloudManagedDevices)
        {
            if (!_isEnabled || _telemetryClient == null) return;

            try
            {
                var properties = new Dictionary<string, string>
                {
                    ["Milestone"] = $"{milestonePercentage}%",
                    ["EstateSizeBand"] = GetEstateSizeBand(totalDevices),
                    ["AppVersion"] = GetAppVersion()
                };

                var metrics = new Dictionary<string, double>
                {
                    ["MilestonePercentage"] = milestonePercentage,
                    ["TotalDevices"] = totalDevices,
                    ["CloudManagedDevices"] = cloudManagedDevices
                };

                _telemetryClient.TrackEvent("MigrationMilestone", properties, metrics);
                
                FileLogger.Instance.Info($"[TELEMETRY] 🎉 MigrationMilestone: {milestonePercentage}% reached! ({cloudManagedDevices}/{totalDevices} devices)");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track migration milestone: {ex.Message}");
            }
        }

        /// <summary>
        /// Track blocker resolution events.
        /// </summary>
        public void TrackBlockerResolution(string blockerType, string resolution, int affectedDevices)
        {
            if (!_isEnabled || _telemetryClient == null) return;

            try
            {
                var properties = new Dictionary<string, string>
                {
                    ["BlockerType"] = blockerType,
                    ["Resolution"] = resolution,
                    ["AppVersion"] = GetAppVersion()
                };

                var metrics = new Dictionary<string, double>
                {
                    ["AffectedDevices"] = affectedDevices
                };

                _telemetryClient.TrackEvent("BlockerResolution", properties, metrics);
                
                FileLogger.Instance.Info($"[TELEMETRY] BlockerResolution: {blockerType} resolved via {resolution}, {affectedDevices} devices unblocked");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track blocker resolution: {ex.Message}");
            }
        }

        /// <summary>
        /// Track workload transition events (e.g., workload moved from ConfigMgr to Intune).
        /// </summary>
        public void TrackWorkloadTransition(string workloadName, string fromState, string toState, int affectedDevices)
        {
            if (!_isEnabled || _telemetryClient == null) return;

            try
            {
                var properties = new Dictionary<string, string>
                {
                    ["WorkloadName"] = workloadName,
                    ["FromState"] = fromState,
                    ["ToState"] = toState,
                    ["AppVersion"] = GetAppVersion()
                };

                var metrics = new Dictionary<string, double>
                {
                    ["AffectedDevices"] = affectedDevices
                };

                _telemetryClient.TrackEvent("WorkloadTransition", properties, metrics);
                
                FileLogger.Instance.Info($"[TELEMETRY] WorkloadTransition: {workloadName} changed from {fromState} to {toState}, {affectedDevices} devices affected");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track workload transition: {ex.Message}");
            }
        }

        /// <summary>
        /// Track session summary when user closes the app.
        /// </summary>
        public void TrackSessionSummary(TimeSpan sessionDuration, List<string> tabsViewed, int actionsPerformed)
        {
            if (!_isEnabled || _telemetryClient == null) return;

            try
            {
                var properties = new Dictionary<string, string>
                {
                    ["TabsViewed"] = string.Join(",", tabsViewed),
                    ["SessionDurationBand"] = GetSessionDurationBand(sessionDuration),
                    ["AppVersion"] = GetAppVersion()
                };

                var metrics = new Dictionary<string, double>
                {
                    ["SessionDurationMinutes"] = sessionDuration.TotalMinutes,
                    ["TabsViewedCount"] = tabsViewed.Count,
                    ["ActionsPerformed"] = actionsPerformed
                };

                _telemetryClient.TrackEvent("SessionSummary", properties, metrics);
                
                FileLogger.Instance.Info($"[TELEMETRY] SessionSummary: Duration={sessionDuration.TotalMinutes:F1}min, Tabs={tabsViewed.Count}, Actions={actionsPerformed}");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track session summary: {ex.Message}");
            }
        }

        /// <summary>
        /// Convert device count to size band for privacy (e.g., "100-500", "500-1000").
        /// </summary>
        private string GetEstateSizeBand(int deviceCount)
        {
            return deviceCount switch
            {
                < 50 => "1-49",
                < 100 => "50-99",
                < 250 => "100-249",
                < 500 => "250-499",
                < 1000 => "500-999",
                < 2500 => "1000-2499",
                < 5000 => "2500-4999",
                < 10000 => "5000-9999",
                < 25000 => "10000-24999",
                < 50000 => "25000-49999",
                < 100000 => "50000-99999",
                _ => "100000+"
            };
        }

        /// <summary>
        /// Convert percentage to band for grouping (e.g., "0-10%", "10-25%").
        /// </summary>
        private string GetPercentageBand(double percentage)
        {
            return percentage switch
            {
                < 10 => "0-10%",
                < 25 => "10-25%",
                < 50 => "25-50%",
                < 75 => "50-75%",
                < 90 => "75-90%",
                < 100 => "90-99%",
                _ => "100%"
            };
        }

        /// <summary>
        /// Convert session duration to band for grouping.
        /// </summary>
        private string GetSessionDurationBand(TimeSpan duration)
        {
            return duration.TotalMinutes switch
            {
                < 1 => "Under 1 min",
                < 5 => "1-5 min",
                < 15 => "5-15 min",
                < 30 => "15-30 min",
                < 60 => "30-60 min",
                _ => "Over 1 hour"
            };
        }

        #endregion

        /// <summary>
        /// Flush all pending telemetry immediately. Call before app shutdown.
        /// </summary>
        public void Flush()
        {
            if (!_isEnabled || _telemetryClient == null) return;

            try
            {
                _telemetryClient.Flush();
                System.Threading.Thread.Sleep(1000); // Wait for flush to complete
                
                FileLogger.Instance.Info("[TELEMETRY] Telemetry flushed to Azure");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to flush telemetry: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Flush();
        }

        #region Privacy and Sanitization

        /// <summary>
        /// Generate anonymous user ID from machine GUID hash (SHA256).
        /// </summary>
        private string GetAnonymousUserId()
        {
            try
            {
                string machineGuid = GetMachineGuid();
                using (var sha256 = SHA256.Create())
                {
                    var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(machineGuid));
                    return Convert.ToBase64String(hash).Substring(0, 22);
                }
            }
            catch
            {
                return Guid.NewGuid().ToString();
            }
        }

        /// <summary>
        /// Get machine GUID from Windows registry.
        /// </summary>
        private string GetMachineGuid()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                {
                    var guid = key?.GetValue("MachineGuid")?.ToString();
                    if (!string.IsNullOrEmpty(guid))
                    {
                        return guid;
                    }
                }
            }
            catch
            {
                // Fallback to machine name
            }

            return Environment.MachineName;
        }

        private string GetAppVersion()
        {
            try
            {
                return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Sanitize properties dictionary to remove PII.
        /// </summary>
        private Dictionary<string, string> SanitizeProperties(Dictionary<string, string> properties)
        {
            var sanitized = new Dictionary<string, string>();
            
            foreach (var kvp in properties)
            {
                sanitized[kvp.Key] = SanitizeString(kvp.Value);
            }

            return sanitized;
        }

        /// <summary>
        /// Sanitize a string to remove PII:
        /// - UNC paths (\\server\share)
        /// - Local paths with usernames (C:\Users\username)
        /// - Email addresses
        /// - IP addresses (but NOT version numbers like 3.17.36.0)
        /// - GUIDs (tenant IDs, device IDs)
        /// - Domain\username format
        /// </summary>
        private string SanitizeString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var sanitized = input;

            // Remove UNC paths
            sanitized = Regex.Replace(sanitized, @"\\\\[\w\-\.]+\\[\w\-\.\$]+", "[UNC_PATH]", RegexOptions.IgnoreCase);

            // Remove local paths with usernames
            sanitized = Regex.Replace(sanitized, @"[A-Z]:\\Users\\[\w\-\.]+", "C:\\Users\\[USER]", RegexOptions.IgnoreCase);

            // Remove email addresses
            sanitized = Regex.Replace(sanitized, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", "[EMAIL]");

            // Remove IP addresses - only match patterns that look like real IPs (first octet >= 10)
            // This avoids matching version numbers like 3.17.36.0 which start with small numbers
            // Matches: 10.x.x.x, 172.x.x.x, 192.x.x.x and other IPs starting with 10-255
            sanitized = Regex.Replace(sanitized, @"\b(?:1\d{2}|2[0-4]\d|25[0-5]|[1-9]\d)\.\d{1,3}\.\d{1,3}\.\d{1,3}\b", "[IP]");

            // Remove GUIDs
            sanitized = Regex.Replace(sanitized, @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b", "[GUID]");

            // Remove domain\username format
            sanitized = Regex.Replace(sanitized, @"\b[A-Z0-9\-]+\\[\w\-\.]+\b", "[DOMAIN\\USER]", RegexOptions.IgnoreCase);

            return sanitized;
        }

        /// <summary>
        /// Create a sanitized copy of an exception.
        /// </summary>
        private Exception SanitizeException(Exception exception)
        {
            var sanitizedMessage = SanitizeString(exception.Message);
            var sanitizedException = new Exception(sanitizedMessage, exception.InnerException);
            return sanitizedException;
        }

        #endregion

        public bool IsEnabled => _isEnabled;
    }
}
