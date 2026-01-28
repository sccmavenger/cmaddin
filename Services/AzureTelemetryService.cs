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

        #region Strategic Telemetry for Leadership

        /// <summary>
        /// Track blocker resolution for leadership dashboards.
        /// Shows which blockers are being resolved and how quickly.
        /// </summary>
        public void TrackBlockerResolution(string blockerType, int devicesAffected, string resolution)
        {
            TrackEvent("BlockerResolution", new Dictionary<string, string>
            {
                ["BlockerType"] = blockerType,
                ["Resolution"] = resolution
            }, new Dictionary<string, double>
            {
                ["DevicesAffected"] = devicesAffected
            });
        }

        /// <summary>
        /// Track workload transition milestone for leadership dashboards.
        /// </summary>
        public void TrackWorkloadTransition(string workloadName, string fromState, string toState, int devicesAffected)
        {
            TrackEvent("WorkloadTransition", new Dictionary<string, string>
            {
                ["WorkloadName"] = workloadName,
                ["FromState"] = fromState,
                ["ToState"] = toState
            }, new Dictionary<string, double>
            {
                ["DevicesAffected"] = devicesAffected
            });
        }

        /// <summary>
        /// Track feature engagement for product development insights.
        /// </summary>
        public void TrackFeatureEngagement(string featureName, string action, Dictionary<string, string>? additionalProperties = null)
        {
            var properties = additionalProperties ?? new Dictionary<string, string>();
            properties["FeatureName"] = featureName;
            properties["Action"] = action;
            
            TrackEvent("FeatureEngagement", properties);
        }

        /// <summary>
        /// Track migration milestone reached (e.g., 25%, 50%, 75%, 100%).
        /// </summary>
        public void TrackMigrationMilestone(double enrollmentPercentage, int totalDevices, int migratedDevices)
        {
            // Determine milestone
            int milestone = enrollmentPercentage switch
            {
                >= 100 => 100,
                >= 90 => 90,
                >= 75 => 75,
                >= 50 => 50,
                >= 25 => 25,
                >= 10 => 10,
                >= 1 => 1,
                _ => 0
            };

            string estateSize = totalDevices switch
            {
                < 100 => "Small",
                < 500 => "Medium",
                < 1000 => "Large",
                < 5000 => "Enterprise",
                _ => "Mega"
            };

            TrackEvent("MigrationMilestone", new Dictionary<string, string>
            {
                ["Milestone"] = $"{milestone}%",
                ["EstateSize"] = estateSize
            }, new Dictionary<string, double>
            {
                ["EnrollmentPercentage"] = Math.Round(enrollmentPercentage, 1),
                ["MilestoneValue"] = milestone
            });
            
            FileLogger.Instance.Info($"[TELEMETRY] Migration milestone tracked: {milestone}% ({enrollmentPercentage:F1}% actual)");
        }

        /// <summary>
        /// Track session summary when user closes the app.
        /// Provides insight into tool usage patterns.
        /// </summary>
        public void TrackSessionSummary(TimeSpan sessionDuration, int tabsViewed, int actionsPerformed, bool dataRefreshed)
        {
            TrackEvent("SessionSummary", new Dictionary<string, string>
            {
                ["DataRefreshed"] = dataRefreshed.ToString()
            }, new Dictionary<string, double>
            {
                ["SessionMinutes"] = Math.Round(sessionDuration.TotalMinutes, 1),
                ["TabsViewed"] = tabsViewed,
                ["ActionsPerformed"] = actionsPerformed
            });
        }

        /// <summary>
        /// Track AI recommendation engagement for understanding AI value.
        /// </summary>
        public void TrackAIRecommendation(string recommendationType, string action, bool wasHelpful)
        {
            TrackEvent("AIRecommendation", new Dictionary<string, string>
            {
                ["RecommendationType"] = recommendationType,
                ["Action"] = action,
                ["WasHelpful"] = wasHelpful.ToString()
            });
        }

        /// <summary>
        /// Track data source connection for understanding deployment scenarios.
        /// </summary>
        public void TrackDataSourceConnection(string dataSource, bool success, string? errorType = null)
        {
            var properties = new Dictionary<string, string>
            {
                ["DataSource"] = dataSource,
                ["Success"] = success.ToString()
            };

            if (errorType != null)
            {
                properties["ErrorType"] = errorType;
            }

            TrackEvent("DataSourceConnection", properties);
        }

        #endregion

        public bool IsEnabled => _isEnabled;
    }
}
