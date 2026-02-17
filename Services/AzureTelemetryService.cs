using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly System.Timers.Timer? _flushTimer;
        private int _eventsSinceLastFlush = 0;

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

                // Start periodic flush timer (every 2 minutes) to ensure telemetry is sent
                // even if app crashes or is force-closed
                _flushTimer = new System.Timers.Timer(120000); // 2 minutes
                _flushTimer.Elapsed += (s, e) => FlushIfNeeded();
                _flushTimer.AutoReset = true;
                _flushTimer.Start();

                FileLogger.Instance.Info("[TELEMETRY] Azure Application Insights initialized successfully");
                FileLogger.Instance.Info($"[TELEMETRY] Anonymous User ID: {_anonymousUserId}");
                FileLogger.Instance.Info($"[TELEMETRY] Session ID: {_telemetryClient.Context.Session.Id}");
                FileLogger.Instance.Info("[TELEMETRY] Auto-flush timer started (every 2 minutes)");
            }
            catch (Exception ex)
            {
                _isEnabled = false;
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to initialize: {ex.Message}");
                FileLogger.Instance.Info("[TELEMETRY] Application will continue without telemetry");
            }
        }

        /// <summary>
        /// Flush telemetry if there are pending events.
        /// </summary>
        private void FlushIfNeeded()
        {
            if (_eventsSinceLastFlush > 0)
            {
                FileLogger.Instance.Debug($"[TELEMETRY] Auto-flushing {_eventsSinceLastFlush} events...");
                _telemetryClient?.Flush();
                _eventsSinceLastFlush = 0;
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
                _eventsSinceLastFlush++;
                
                // Immediately flush important events to ensure they're sent
                if (eventName == "AppStarted" || eventName == "AppExited" || eventName == "EstateSnapshot")
                {
                    _telemetryClient.Flush();
                    _eventsSinceLastFlush = 0;
                    FileLogger.Instance.Debug($"[TELEMETRY] Immediate flush for {eventName}");
                }
                
                FileLogger.Instance.Debug($"[TELEMETRY] Event: {eventName} (pending: {_eventsSinceLastFlush})");
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
        /// Track Application Readiness assessment results.
        /// Provides insight into ConfigMgr app portfolio migration complexity.
        /// v3.17.101 - Application Readiness telemetry
        /// </summary>
        public void TrackApplicationReadinessAssessed(
            int totalApps, 
            int deployedApps,
            int easyApps, 
            int moderateApps, 
            int needsReviewApps, 
            int complexApps,
            int unknownApps,
            double readinessPercentage,
            Dictionary<string, int>? technologyBreakdown = null)
        {
            if (!_isEnabled || _telemetryClient == null) return;

            try
            {
                var properties = new Dictionary<string, string>
                {
                    ["TotalAppsBand"] = GetAppCountBand(totalApps),
                    ["DeployedAppsBand"] = GetAppCountBand(deployedApps),
                    ["ReadinessBand"] = GetPercentageBand(readinessPercentage),
                    ["HasAppV"] = (complexApps > 0).ToString(),
                    ["HasScriptInstallers"] = (needsReviewApps > 0).ToString(),
                    ["AppVersion"] = GetAppVersion()
                };

                // Add technology breakdown if provided
                if (technologyBreakdown != null)
                {
                    var topTechnologies = technologyBreakdown
                        .OrderByDescending(kv => kv.Value)
                        .Take(5)
                        .Select(kv => $"{kv.Key}:{kv.Value}")
                        .ToList();
                    properties["TopTechnologies"] = string.Join(",", topTechnologies);
                }

                var metrics = new Dictionary<string, double>
                {
                    ["TotalApps"] = totalApps,
                    ["DeployedApps"] = deployedApps,
                    ["EasyApps"] = easyApps,
                    ["ModerateApps"] = moderateApps,
                    ["NeedsReviewApps"] = needsReviewApps,
                    ["ComplexApps"] = complexApps,
                    ["UnknownApps"] = unknownApps,
                    ["ReadinessPercentage"] = readinessPercentage,
                    ["ReadyApps"] = easyApps + moderateApps,
                    ["BlockedApps"] = needsReviewApps + complexApps + unknownApps
                };

                _telemetryClient.TrackEvent("ApplicationReadinessAssessed", properties, metrics);
                
                FileLogger.Instance.Info($"[TELEMETRY] ApplicationReadinessAssessed: {deployedApps} deployed apps, {readinessPercentage:F1}% ready (Easy:{easyApps}, Moderate:{moderateApps}, Review:{needsReviewApps}, Complex:{complexApps})");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track application readiness: {ex.Message}");
            }
        }

        #endregion

        #region VP-Level Strategic Insights (Cross-Platform Unique Data)

        /// <summary>
        /// Track migration blockers - what's preventing devices from being fully cloud-managed.
        /// This is UNIQUE data only this tool can provide (sees both CM and Intune simultaneously).
        /// </summary>
        public void TrackMigrationBlockers(
            int noAADDeviceId,
            int staleDevices14Days,
            int hardwareIssues,
            int notInAutopilot,
            int configMgrOnlyNoIntune,
            int intotalDevices)
        {
            if (!_isEnabled || _telemetryClient == null) return;

            try
            {
                var estateBand = GetEstateSizeBand(intotalDevices);
                
                var properties = new Dictionary<string, string>
                {
                    ["EstateSizeBand"] = estateBand,
                    ["AppVersion"] = GetAppVersion()
                };

                var metrics = new Dictionary<string, double>
                {
                    ["NoAADDeviceId"] = noAADDeviceId,
                    ["StaleDevices14Days"] = staleDevices14Days,
                    ["HardwareIssues"] = hardwareIssues,
                    ["NotInAutopilot"] = notInAutopilot,
                    ["ConfigMgrOnlyNoIntune"] = configMgrOnlyNoIntune,
                    ["TotalDevices"] = intotalDevices,
                    // Percentages for normalization across estates
                    ["NoAADDeviceIdPct"] = intotalDevices > 0 ? Math.Round((double)noAADDeviceId / intotalDevices * 100, 1) : 0,
                    ["StalePct"] = intotalDevices > 0 ? Math.Round((double)staleDevices14Days / intotalDevices * 100, 1) : 0,
                    ["NotInAutopilotPct"] = intotalDevices > 0 ? Math.Round((double)notInAutopilot / intotalDevices * 100, 1) : 0
                };

                _telemetryClient.TrackEvent("MigrationBlockers", properties, metrics);
                _eventsSinceLastFlush++;
                
                FileLogger.Instance.Info($"[TELEMETRY] MigrationBlockers: NoAADDeviceId={noAADDeviceId}, Stale={staleDevices14Days}, HardwareIssues={hardwareIssues}, NotAutopilot={notInAutopilot}");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track migration blockers: {ex.Message}");
            }
        }

        /// <summary>
        /// Track security posture comparison - the delta between CM-managed and cloud-managed devices.
        /// KEY INSIGHT: Shows security improvement potential from migration.
        /// </summary>
        public void TrackSecurityPostureComparison(
            double intuneCompliancePct,
            double configMgrCompliancePct,
            double intuneCAReadyPct,
            double configMgrCAReadyPct,
            double intuneEncryptedPct,
            double configMgrEncryptedPct,
            int totalIntuneDevices,
            int totalConfigMgrDevices)
        {
            if (!_isEnabled || _telemetryClient == null) return;

            try
            {
                var properties = new Dictionary<string, string>
                {
                    ["IntuneEstateBand"] = GetEstateSizeBand(totalIntuneDevices),
                    ["ConfigMgrEstateBand"] = GetEstateSizeBand(totalConfigMgrDevices),
                    ["AppVersion"] = GetAppVersion()
                };

                var metrics = new Dictionary<string, double>
                {
                    ["IntuneCompliancePct"] = intuneCompliancePct,
                    ["ConfigMgrCompliancePct"] = configMgrCompliancePct,
                    ["ComplianceDelta"] = intuneCompliancePct - configMgrCompliancePct,
                    ["IntuneCAReadyPct"] = intuneCAReadyPct,
                    ["ConfigMgrCAReadyPct"] = configMgrCAReadyPct,
                    ["CAReadyDelta"] = intuneCAReadyPct - configMgrCAReadyPct,
                    ["IntuneEncryptedPct"] = intuneEncryptedPct,
                    ["ConfigMgrEncryptedPct"] = configMgrEncryptedPct,
                    ["EncryptionDelta"] = intuneEncryptedPct - configMgrEncryptedPct
                };

                _telemetryClient.TrackEvent("SecurityPostureComparison", properties, metrics);
                _eventsSinceLastFlush++;
                
                FileLogger.Instance.Info($"[TELEMETRY] SecurityPostureComparison: Compliance Delta={intuneCompliancePct - configMgrCompliancePct:+0.0;-0.0}%, CA Delta={intuneCAReadyPct - configMgrCAReadyPct:+0.0;-0.0}%");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track security posture: {ex.Message}");
            }
        }

        /// <summary>
        /// Track device orphan analysis - devices in one system but not the other.
        /// UNIQUE INSIGHT: Only this tool can see cross-platform device mismatches.
        /// </summary>
        public void TrackDeviceOrphans(
            int inConfigMgrNotIntune,
            int inIntuneNotConfigMgr,
            int coManagedDevices,
            int cloudNativeDevices,
            int totalDevices)
        {
            if (!_isEnabled || _telemetryClient == null) return;

            try
            {
                var properties = new Dictionary<string, string>
                {
                    ["EstateSizeBand"] = GetEstateSizeBand(totalDevices),
                    ["AppVersion"] = GetAppVersion()
                };

                var metrics = new Dictionary<string, double>
                {
                    ["InConfigMgrNotIntune"] = inConfigMgrNotIntune,
                    ["InIntuneNotConfigMgr"] = inIntuneNotConfigMgr,
                    ["CoManagedDevices"] = coManagedDevices,
                    ["CloudNativeDevices"] = cloudNativeDevices,
                    ["TotalDevices"] = totalDevices,
                    // Percentages
                    ["OrphanedInCMPct"] = totalDevices > 0 ? Math.Round((double)inConfigMgrNotIntune / totalDevices * 100, 1) : 0,
                    ["CloudNativePct"] = totalDevices > 0 ? Math.Round((double)cloudNativeDevices / totalDevices * 100, 1) : 0,
                    ["CoManagedPct"] = totalDevices > 0 ? Math.Round((double)coManagedDevices / totalDevices * 100, 1) : 0
                };

                _telemetryClient.TrackEvent("DeviceOrphans", properties, metrics);
                _eventsSinceLastFlush++;
                
                FileLogger.Instance.Info($"[TELEMETRY] DeviceOrphans: CM-only={inConfigMgrNotIntune}, Intune-only={inIntuneNotConfigMgr}, Co-managed={coManagedDevices}, CloudNative={cloudNativeDevices}");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track device orphans: {ex.Message}");
            }
        }

        /// <summary>
        /// Track Autopilot readiness funnel - how many devices are ready for cloud provisioning.
        /// </summary>
        public void TrackAutopilotReadiness(
            int totalDevices,
            int registeredInAutopilot,
            int hasAADDeviceId,
            int hasTpm20,
            int supportsSecureBoot)
        {
            if (!_isEnabled || _telemetryClient == null) return;

            try
            {
                var properties = new Dictionary<string, string>
                {
                    ["EstateSizeBand"] = GetEstateSizeBand(totalDevices),
                    ["AppVersion"] = GetAppVersion()
                };

                var metrics = new Dictionary<string, double>
                {
                    ["TotalDevices"] = totalDevices,
                    ["RegisteredInAutopilot"] = registeredInAutopilot,
                    ["HasAADDeviceId"] = hasAADDeviceId,
                    ["HasTpm20"] = hasTpm20,
                    ["SupportsSecureBoot"] = supportsSecureBoot,
                    // Funnel percentages
                    ["AutopilotRegisteredPct"] = totalDevices > 0 ? Math.Round((double)registeredInAutopilot / totalDevices * 100, 1) : 0,
                    ["HybridJoinedPct"] = totalDevices > 0 ? Math.Round((double)hasAADDeviceId / totalDevices * 100, 1) : 0,
                    ["Tpm20Pct"] = totalDevices > 0 ? Math.Round((double)hasTpm20 / totalDevices * 100, 1) : 0
                };

                _telemetryClient.TrackEvent("AutopilotReadiness", properties, metrics);
                _eventsSinceLastFlush++;
                
                FileLogger.Instance.Info($"[TELEMETRY] AutopilotReadiness: Registered={registeredInAutopilot}/{totalDevices}, HybridJoined={hasAADDeviceId}, TPM2.0={hasTpm20}");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track Autopilot readiness: {ex.Message}");
            }
        }

        /// <summary>
        /// Track workload authority snapshot - per-workload Intune adoption rates.
        /// KEY INSIGHT: Shows which workloads are ready to move and which need work.
        /// </summary>
        public void TrackWorkloadAuthoritySnapshot(
            int totalCoManagedDevices,
            int devicesReadyForCloudNative,
            Dictionary<string, int> workloadIntuneAdoptionCounts)
        {
            if (!_isEnabled || _telemetryClient == null) return;

            try
            {
                var properties = new Dictionary<string, string>
                {
                    ["EstateSizeBand"] = GetEstateSizeBand(totalCoManagedDevices),
                    ["AppVersion"] = GetAppVersion()
                };

                var metrics = new Dictionary<string, double>
                {
                    ["TotalCoManagedDevices"] = totalCoManagedDevices,
                    ["DevicesReadyForCloudNative"] = devicesReadyForCloudNative,
                    ["CloudNativeReadyPct"] = totalCoManagedDevices > 0 
                        ? Math.Round((double)devicesReadyForCloudNative / totalCoManagedDevices * 100, 1) 
                        : 0
                };

                // Add per-workload adoption counts and percentages
                foreach (var workload in workloadIntuneAdoptionCounts)
                {
                    var safeName = workload.Key.Replace(" ", "");
                    metrics[$"{safeName}Count"] = workload.Value;
                    metrics[$"{safeName}Pct"] = totalCoManagedDevices > 0 
                        ? Math.Round((double)workload.Value / totalCoManagedDevices * 100, 1) 
                        : 0;
                }

                _telemetryClient.TrackEvent("WorkloadAuthoritySnapshot", properties, metrics);
                _eventsSinceLastFlush++;

                FileLogger.Instance.Info($"[TELEMETRY] WorkloadAuthoritySnapshot: CoManaged={totalCoManagedDevices}, CloudNativeReady={devicesReadyForCloudNative}, Workloads={workloadIntuneAdoptionCounts.Count}");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track workload authority: {ex.Message}");
            }
        }

        #endregion

        /// <summary>
        /// Convert app count to band for privacy (e.g., "1-25", "26-50").
        /// </summary>
        private string GetAppCountBand(int appCount)
        {
            return appCount switch
            {
                < 25 => "1-24",
                < 50 => "25-49",
                < 100 => "50-99",
                < 250 => "100-249",
                < 500 => "250-499",
                < 1000 => "500-999",
                _ => "1000+"
            };
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
            // Stop the auto-flush timer
            _flushTimer?.Stop();
            _flushTimer?.Dispose();
            
            // Flush any remaining telemetry
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

        #region Infrastructure Telemetry

        /// <summary>
        /// Track ConfigMgr connection details for debugging version-specific issues.
        /// </summary>
        public void TrackConfigMgrConnected(string? siteCode, string? siteVersion, string? siteBuild, string connectionMethod)
        {
            if (!_isEnabled) return;

            try
            {
                var properties = new Dictionary<string, string>
                {
                    ["SiteCode"] = SanitizeString(siteCode ?? "unknown"),
                    ["SiteVersion"] = siteVersion ?? "unknown",
                    ["SiteBuild"] = siteBuild ?? "unknown",
                    ["ConnectionMethod"] = connectionMethod,
                    ["AppVersion"] = GetAppVersion()
                };

                _telemetryClient.TrackEvent("ConfigMgrConnected", properties);
                
                FileLogger.Instance.Info($"[TELEMETRY] ConfigMgrConnected: Version={siteVersion}, Method={connectionMethod}");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track ConfigMgr connection: {ex.Message}");
            }
        }

        /// <summary>
        /// Track API query results for debugging empty result issues.
        /// </summary>
        public void TrackApiQueryResult(string queryType, int statusCode, int resultCount, string? siteVersion, bool usedFallback)
        {
            if (!_isEnabled) return;

            try
            {
                var properties = new Dictionary<string, string>
                {
                    ["QueryType"] = queryType,
                    ["SiteVersion"] = siteVersion ?? "unknown",
                    ["UsedFallback"] = usedFallback.ToString(),
                    ["AppVersion"] = GetAppVersion()
                };

                var metrics = new Dictionary<string, double>
                {
                    ["StatusCode"] = statusCode,
                    ["ResultCount"] = resultCount
                };

                _telemetryClient.TrackEvent("ApiQueryResult", properties, metrics);
                
                // Only log non-success or empty results to avoid log spam
                if (statusCode != 200 || resultCount == 0)
                {
                    FileLogger.Instance.Info($"[TELEMETRY] ApiQueryResult: {queryType} Status={statusCode}, Count={resultCount}, Version={siteVersion}");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track API query result: {ex.Message}");
            }
        }

        /// <summary>
        /// Track Admin Service connection failures for WMI removal planning.
        /// Captures why connections fail (HTTP error, timeout, etc.) without PII.
        /// </summary>
        public void TrackAdminServiceConnectionFailed(string failureReason, int? httpStatusCode, string? exceptionType, string? siteVersion)
        {
            if (!_isEnabled) return;

            try
            {
                var properties = new Dictionary<string, string>
                {
                    ["FailureReason"] = failureReason,
                    ["ExceptionType"] = exceptionType ?? "Unknown",
                    ["SiteVersion"] = siteVersion ?? "unknown",
                    ["AppVersion"] = GetAppVersion()
                };

                var metrics = new Dictionary<string, double>();
                if (httpStatusCode.HasValue)
                {
                    metrics["HttpStatusCode"] = httpStatusCode.Value;
                }

                _telemetryClient.TrackEvent("AdminServiceConnectionFailed", properties, metrics);
                _telemetryClient.Flush(); // Important event - flush immediately
                
                FileLogger.Instance.Warning($"[TELEMETRY] AdminServiceConnectionFailed: {failureReason}, HTTP={httpStatusCode}, Exception={exceptionType}");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track connection failure: {ex.Message}");
            }
        }

        /// <summary>
        /// Track individual Admin Service query failures.
        /// </summary>
        public void TrackAdminServiceQueryFailed(string queryName, string failureReason, int? httpStatusCode, double? durationMs)
        {
            if (!_isEnabled) return;

            try
            {
                var properties = new Dictionary<string, string>
                {
                    ["QueryName"] = queryName,
                    ["FailureReason"] = failureReason,
                    ["AppVersion"] = GetAppVersion()
                };

                var metrics = new Dictionary<string, double>();
                if (httpStatusCode.HasValue) metrics["HttpStatusCode"] = httpStatusCode.Value;
                if (durationMs.HasValue) metrics["DurationMs"] = durationMs.Value;

                _telemetryClient.TrackEvent("AdminServiceQueryFailed", properties, metrics);
                
                FileLogger.Instance.Warning($"[TELEMETRY] AdminServiceQueryFailed: {queryName} - {failureReason}");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track query failure: {ex.Message}");
            }
        }

        /// <summary>
        /// Track successful Admin Service queries with timing for performance analysis.
        /// </summary>
        public void TrackAdminServiceQuerySucceeded(string queryName, int recordCount, double durationMs)
        {
            if (!_isEnabled) return;

            try
            {
                var properties = new Dictionary<string, string>
                {
                    ["QueryName"] = queryName,
                    ["AppVersion"] = GetAppVersion()
                };

                var metrics = new Dictionary<string, double>
                {
                    ["RecordCount"] = recordCount,
                    ["DurationMs"] = durationMs
                };

                _telemetryClient.TrackEvent("AdminServiceQuerySucceeded", properties, metrics);
                
                // Only log slow queries (> 5 seconds) to avoid spam
                if (durationMs > 5000)
                {
                    FileLogger.Instance.Info($"[TELEMETRY] Slow query: {queryName} took {durationMs:F0}ms");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track query success: {ex.Message}");
            }
        }

        #endregion

        #region Comparison Tile Telemetry

        /// <summary>
        /// Track Cloud Value Comparison tile metrics for debugging display issues.
        /// Captures the values shown to users to understand confusing displays.
        /// </summary>
        public void TrackComparisonTileViewed(string tileName, double? intuneValue, double? configMgrValue, string winner, string comparisonSummary)
        {
            if (!_isEnabled) return;

            try
            {
                var properties = new Dictionary<string, string>
                {
                    ["TileName"] = tileName,
                    ["Winner"] = winner,
                    ["ComparisonSummary"] = SanitizeString(comparisonSummary),
                    ["AppVersion"] = GetAppVersion()
                };

                var metrics = new Dictionary<string, double>();
                if (intuneValue.HasValue) metrics["IntuneValue"] = intuneValue.Value;
                if (configMgrValue.HasValue) metrics["ConfigMgrValue"] = configMgrValue.Value;

                _telemetryClient.TrackEvent("ComparisonTileViewed", properties, metrics);
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track comparison tile: {ex.Message}");
            }
        }

        /// <summary>
        /// Track stale device metrics for Security Blind Spots analysis.
        /// </summary>
        public void TrackStaleDeviceMetrics(string source, int totalDevices, int staleCount, double stalePercent, 
            int noDataCount, int bucket14to30, int bucket30to90, int bucket90plus)
        {
            if (!_isEnabled) return;

            try
            {
                var properties = new Dictionary<string, string>
                {
                    ["Source"] = source, // "Intune" or "ConfigMgr"
                    ["AppVersion"] = GetAppVersion()
                };

                var metrics = new Dictionary<string, double>
                {
                    ["TotalDevices"] = totalDevices,
                    ["StaleCount"] = staleCount,
                    ["StalePercent"] = stalePercent,
                    ["NoDataCount"] = noDataCount,
                    ["Bucket14to30"] = bucket14to30,
                    ["Bucket30to90"] = bucket30to90,
                    ["Bucket90plus"] = bucket90plus
                };

                _telemetryClient.TrackEvent("StaleDeviceMetrics", properties, metrics);
                
                // Log warning if stale percent is high
                if (stalePercent > 25)
                {
                    FileLogger.Instance.Info($"[TELEMETRY] High stale rate: {source} has {stalePercent:F1}% stale devices");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track stale metrics: {ex.Message}");
            }
        }

        /// <summary>
        /// Track sync freshness metrics for Response Time analysis.
        /// </summary>
        public void TrackSyncFreshnessMetrics(string source, double avgDays, int devicesScannedToday,
            int bucket0to1d, int bucket1to7d, int bucket7to14d, int bucket14to30d, int bucket30plus)
        {
            if (!_isEnabled) return;

            try
            {
                var properties = new Dictionary<string, string>
                {
                    ["Source"] = source, // "Intune" or "ConfigMgr"
                    ["AppVersion"] = GetAppVersion()
                };

                var metrics = new Dictionary<string, double>
                {
                    ["AvgDays"] = avgDays,
                    ["DevicesScannedToday"] = devicesScannedToday,
                    ["Bucket0to1d"] = bucket0to1d,
                    ["Bucket1to7d"] = bucket1to7d,
                    ["Bucket7to14d"] = bucket7to14d,
                    ["Bucket14to30d"] = bucket14to30d,
                    ["Bucket30plus"] = bucket30plus
                };

                _telemetryClient.TrackEvent("SyncFreshnessMetrics", properties, metrics);
                
                // Log warning if average is high (like the 170 day issue)
                if (avgDays > 30)
                {
                    FileLogger.Instance.Info($"[TELEMETRY] High avg sync age: {source} avg={avgDays:F1} days");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track sync freshness: {ex.Message}");
            }
        }

        /// <summary>
        /// Track data quality issues in comparison tiles (e.g., division by zero, missing data).
        /// </summary>
        public void TrackComparisonDataQuality(string tileName, string issue, string details)
        {
            if (!_isEnabled) return;

            try
            {
                var properties = new Dictionary<string, string>
                {
                    ["TileName"] = tileName,
                    ["Issue"] = issue, // e.g., "DivisionByZero", "NoConfigMgrData", "ExtremeDifference"
                    ["Details"] = SanitizeString(details),
                    ["AppVersion"] = GetAppVersion()
                };

                _telemetryClient.TrackEvent("ComparisonDataQuality", properties);
                
                FileLogger.Instance.Info($"[TELEMETRY] Data quality issue: {tileName} - {issue}");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track data quality: {ex.Message}");
            }
        }

        /// <summary>
        /// Track comprehensive comparison tile data for diagnosing 0% ConfigMgr values.
        /// Tracks device counts, values, data source, and quality issues.
        /// </summary>
        public void TrackComparisonTileData(
            string tileName,
            int intuneDeviceCount,
            int intuneValue,
            int configMgrDeviceCount,
            int configMgrValue,
            string configMgrDataSource,
            string dataQualityIssues)
        {
            if (!_isEnabled) return;

            try
            {
                var properties = new Dictionary<string, string>
                {
                    ["TileName"] = tileName,
                    ["ConfigMgrDataSource"] = configMgrDataSource, // "AdminService", "WMI", "Mock", "None"
                    ["DataQualityIssues"] = SanitizeString(dataQualityIssues),
                    ["AppVersion"] = GetAppVersion()
                };

                var metrics = new Dictionary<string, double>
                {
                    ["IntuneDeviceCount"] = intuneDeviceCount,
                    ["IntuneValue"] = intuneValue,
                    ["ConfigMgrDeviceCount"] = configMgrDeviceCount,
                    ["ConfigMgrValue"] = configMgrValue,
                    ["IntunePercentage"] = intuneDeviceCount > 0 ? (double)intuneValue / intuneDeviceCount * 100 : 0,
                    ["ConfigMgrPercentage"] = configMgrDeviceCount > 0 ? (double)configMgrValue / configMgrDeviceCount * 100 : 0
                };

                _telemetryClient.TrackEvent("ComparisonTileData", properties, metrics);

                // Log warning if ConfigMgr shows 0% when it has devices
                if (configMgrDeviceCount > 0 && configMgrValue == 0)
                {
                    FileLogger.Instance.Info($"[TELEMETRY] ConfigMgr 0% alert: {tileName} has {configMgrDeviceCount} devices but value=0. Source: {configMgrDataSource}. Issues: {dataQualityIssues}");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track comparison tile data: {ex.Message}");
            }
        }

        /// <summary>
        /// Track ConfigMgr Admin Service query mode for diagnosing connection issues.
        /// </summary>
        public void TrackConfigMgrQueryMode(string queryType, string queryMode, int recordCount, string nullFields)
        {
            if (!_isEnabled) return;

            try
            {
                var properties = new Dictionary<string, string>
                {
                    ["QueryType"] = queryType, // e.g., "GetWindows1011Devices", "GetBitLockerStatus"
                    ["QueryMode"] = queryMode, // "NoSelect", "WithSelect", "NoFilter", "WMI"
                    ["NullFields"] = SanitizeString(nullFields), // e.g., "LastActiveTime:85%,LastPolicyRequest:100%"
                    ["AppVersion"] = GetAppVersion()
                };

                var metrics = new Dictionary<string, double>
                {
                    ["RecordCount"] = recordCount
                };

                _telemetryClient.TrackEvent("ConfigMgrQueryMode", properties, metrics);
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"[TELEMETRY] Failed to track ConfigMgr query mode: {ex.Message}");
            }
        }

        #endregion

        public bool IsEnabled => _isEnabled;
    }
}
