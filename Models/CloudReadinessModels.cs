using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using ZeroTrustMigrationAddin.Services;

namespace ZeroTrustMigrationAddin.Models
{
    /// <summary>
    /// Cloud Readiness Signal - represents an assessment for a specific cloud migration workload.
    /// v3.17.0 - Cloud Readiness Signals feature
    /// </summary>
    public class CloudReadinessSignal
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = "☁️";
        
        // Readiness metrics
        public int TotalDevices { get; set; }
        public int ReadyDevices { get; set; }
        public int NotReadyDevices => Math.Max(0, TotalDevices - ReadyDevices);
        
        // Cap percentage at 100% to handle data inconsistencies where ReadyDevices > TotalDevices
        public double ReadinessPercentage => TotalDevices > 0 
            ? Math.Min(100, Math.Round((double)ReadyDevices / TotalDevices * 100, 1)) 
            : 0;
        
        /// <summary>
        /// Returns true if this is the Cloud-Native Readiness signal.
        /// Used for UI to show specific criteria text.
        /// </summary>
        public bool IsCloudNativeSignal => Id == "cloud-native";
        
        // Visual properties
        public string ReadinessLevel => ReadinessPercentage switch
        {
            >= 80 => "Excellent",
            >= 60 => "Good",
            >= 40 => "Fair",
            _ => "Needs Work"
        };
        
        public string StatusColor => ReadinessPercentage switch
        {
            >= 80 => "#107C10", // Green
            >= 60 => "#0078D4", // Blue
            >= 40 => "#FFB900", // Yellow
            _ => "#D13438"      // Red
        };
        
        public string StatusIcon => ReadinessPercentage switch
        {
            >= 80 => "✅",
            >= 60 => "🔵",
            >= 40 => "🟡",
            _ => "🔴"
        };
        
        // Blockers and recommendations
        public List<ReadinessBlocker> TopBlockers { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        
        // Related workload for transition
        public string RelatedWorkload { get; set; } = string.Empty;
        public string LearnMoreUrl { get; set; } = string.Empty;
        
        // Assessment timestamp
        public DateTime LastAssessedTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Represents a blocker preventing device readiness.
    /// </summary>
    public class ReadinessBlocker
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int AffectedDeviceCount { get; set; }
        public double PercentageAffected { get; set; }
        public BlockerSeverity Severity { get; set; } = BlockerSeverity.Medium;
        public string RemediationAction { get; set; } = string.Empty;
        public string RemediationUrl { get; set; } = string.Empty;
        
        /// <summary>
        /// Full ConfigMgr device objects affected by this blocker.
        /// Contains OS info, last active time, etc. for rich display in drill-down.
        /// </summary>
        public List<ConfigMgrDevice> AffectedDevices { get; set; } = new();
        
        /// <summary>
        /// Device names affected by this blocker (computed from AffectedDevices for backward compatibility).
        /// </summary>
        public List<string> AffectedDeviceNames => AffectedDevices.Select(d => d.Name).Where(n => !string.IsNullOrEmpty(n)).ToList();
        
        public string SeverityIcon => Severity switch
        {
            BlockerSeverity.Critical => "🔴",
            BlockerSeverity.High => "🟠",
            BlockerSeverity.Medium => "🟡",
            BlockerSeverity.Low => "🔵",
            _ => "⚪"
        };
    }

    /// <summary>
    /// Dashboard summary for Cloud Readiness Signals tab.
    /// </summary>
    public class CloudReadinessDashboard
    {
        public List<CloudReadinessSignal> Signals { get; set; } = new();
        
        // Summary metrics - cap at 100% for safety
        public double OverallReadiness => Signals.Any() 
            ? Math.Min(100, Math.Round(Signals.Average(s => s.ReadinessPercentage), 1)) 
            : 0;
        
        public int TotalAssessedDevices => Signals.Any() ? Signals.Max(s => s.TotalDevices) : 0;
        
        public int TotalBlockersIdentified => Signals.Sum(s => s.TopBlockers.Count);
        
        public string OverallStatus => OverallReadiness switch
        {
            >= 80 => "Ready for Cloud Migration",
            >= 60 => "Good Progress",
            >= 40 => "Some Work Needed",
            _ => "Significant Gaps"
        };
        
        public string OverallStatusColor => OverallReadiness switch
        {
            >= 80 => "#107C10",
            >= 60 => "#0078D4",
            >= 40 => "#FFB900",
            _ => "#D13438"
        };
        
        // Top blockers across all signals
        public List<ReadinessBlocker> TopOverallBlockers => Signals
            .SelectMany(s => s.TopBlockers)
            .OrderByDescending(b => b.AffectedDeviceCount)
            .Take(5)
            .ToList();
        
        public DateTime LastRefreshed { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Autopilot-specific readiness details.
    /// </summary>
    public class AutopilotReadinessDetails
    {
        public int TotalDevices { get; set; }
        
        // Autopilot requirements
        public int HasTpm20 { get; set; }
        public int HasUefi { get; set; }
        public int HasSecureBoot { get; set; }
        public int HasSupportedOs { get; set; } // Windows 10 1809+, Windows 11
        public int IsAadJoinedOrHybrid { get; set; }
        
        // Calculated readiness
        public int FullyReady => Math.Min(Math.Min(Math.Min(Math.Min(
            HasTpm20, HasUefi), HasSecureBoot), HasSupportedOs), IsAadJoinedOrHybrid);
        
        public double ReadinessPercentage => TotalDevices > 0 
            ? Math.Min(100, Math.Round((double)FullyReady / TotalDevices * 100, 1)) 
            : 0;
        
        // Blockers
        public List<ReadinessBlocker> Blockers { get; set; } = new();
    }

    /// <summary>
    /// Windows 11 upgrade readiness details.
    /// </summary>
    public class Windows11ReadinessDetails
    {
        public int TotalDevices { get; set; }
        
        // Windows 11 hardware requirements
        public int HasTpm20 { get; set; }
        public int HasUefi { get; set; }
        public int HasSecureBoot { get; set; }
        public int Has4GbRam { get; set; }
        public int Has64GbStorage { get; set; }
        public int HasCompatibleCpu { get; set; }
        
        // Calculated readiness
        public int FullyReady { get; set; }
        
        public double ReadinessPercentage => TotalDevices > 0 
            ? Math.Min(100, Math.Round((double)FullyReady / TotalDevices * 100, 1)) 
            : 0;
        
        // Blockers
        public List<ReadinessBlocker> Blockers { get; set; } = new();
    }

    /// <summary>
    /// Cloud-native (Entra join + Intune only) readiness details.
    /// </summary>
    public class CloudNativeReadinessDetails
    {
        public int TotalDevices { get; set; }
        
        // Cloud-native requirements
        public int AlreadyCloudNative { get; set; }
        public int HasModernAuth { get; set; }
        public int NoOnPremDependencies { get; set; }
        public int HasIntuneReadyApps { get; set; }
        
        // Calculated readiness
        public int FullyReady { get; set; }
        
        public double ReadinessPercentage => TotalDevices > 0 
            ? Math.Min(100, Math.Round((double)FullyReady / TotalDevices * 100, 1)) 
            : 0;
        
        // Blockers
        public List<ReadinessBlocker> Blockers { get; set; } = new();
    }

    /// <summary>
    /// WSUS to Windows Update for Business readiness details.
    /// </summary>
    public class WufbReadinessDetails
    {
        public int TotalDevices { get; set; }
        
        // WUfB requirements
        public int HasInternetConnectivity { get; set; }
        public int IsWindows10Plus { get; set; }
        public int HasDeliveryOptimization { get; set; }
        public int NoWsusConflicts { get; set; }
        
        // Calculated readiness
        public int FullyReady { get; set; }
        
        public double ReadinessPercentage => TotalDevices > 0 
            ? Math.Min(100, Math.Round((double)FullyReady / TotalDevices * 100, 1)) 
            : 0;
        
        // Blockers
        public List<ReadinessBlocker> Blockers { get; set; } = new();
    }

    /// <summary>
    /// App deployment (SCCM to Intune) readiness details.
    /// </summary>
    public class AppDeploymentReadinessDetails
    {
        public int TotalApps { get; set; }
        
        // App migration readiness
        public int Win32AppsReady { get; set; }
        public int MsiAppsReady { get; set; }
        public int ScriptBasedApps { get; set; }
        public int ComplexTaskSequenceApps { get; set; }
        
        // Calculated readiness
        public int FullyReady => Win32AppsReady + MsiAppsReady;
        
        public double ReadinessPercentage => TotalApps > 0 
            ? Math.Min(100, Math.Round((double)FullyReady / TotalApps * 100, 1)) 
            : 0;
        
        // Blockers
        public List<ReadinessBlocker> Blockers { get; set; } = new();
    }

    /// <summary>
    /// Identity (on-prem AD to Entra) readiness details.
    /// </summary>
    public class IdentityReadinessDetails
    {
        public int TotalDevices { get; set; }
        
        // Identity readiness
        public int EntraJoined { get; set; }
        public int HybridJoined { get; set; }
        public int OnPremOnlyJoined { get; set; }
        public int WorkgroupDevices { get; set; }
        
        // Calculated - Entra Joined or Hybrid are ready
        public int CloudIdentityReady => EntraJoined + HybridJoined;
        
        public double ReadinessPercentage => TotalDevices > 0 
            ? Math.Min(100, Math.Round((double)CloudIdentityReady / TotalDevices * 100, 1)) 
            : 0;
        
        // Blockers
        public List<ReadinessBlocker> Blockers { get; set; } = new();
    }

    /// <summary>
    /// Endpoint Security (ConfigMgr EP to MDE) readiness details.
    /// </summary>
    public class EndpointSecurityReadinessDetails
    {
        public int TotalDevices { get; set; }
        
        // Endpoint security readiness
        public int HasMdeOnboarded { get; set; }
        public int HasDefenderAv { get; set; }
        public int HasCloudProtection { get; set; }
        public int NoThirdPartyConflicts { get; set; }
        
        // Calculated readiness
        public int FullyReady { get; set; }
        
        public double ReadinessPercentage => TotalDevices > 0 
            ? Math.Min(100, Math.Round((double)FullyReady / TotalDevices * 100, 1)) 
            : 0;
        
        // Blockers
        public List<ReadinessBlocker> Blockers { get; set; } = new();
    }

    /// <summary>
    /// Per-device co-management workload authority from Microsoft Graph API.
    /// Source: managedDevice.configurationManagerClientEnabledFeatures
    /// Docs: https://learn.microsoft.com/graph/api/resources/intune-devices-configurationmanagerclientenabledfeatures
    /// 
    /// Note: In the Graph API configurationManagerClientEnabledFeatures:
    ///   TRUE = Intune manages the workload
    ///   FALSE = ConfigMgr manages the workload
    /// This model INVERTS that: ManagedByConfigMgr properties are true when ConfigMgr manages.
    /// See: https://learn.microsoft.com/en-us/graph/api/resources/intune-devices-configurationmanagerclientenabledfeatures
    /// </summary>
    public class DeviceWorkloadAuthority
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        
        /// <summary>Modern apps (Win32, LOB) managed by ConfigMgr (true) or Intune (false)</summary>
        public bool ModernAppsManagedByConfigMgr { get; set; }
        
        /// <summary>Resource access (VPN, Wi-Fi, email) managed by ConfigMgr (true) or Intune (false)</summary>
        public bool ResourceAccessManagedByConfigMgr { get; set; }
        
        /// <summary>Device configuration profiles managed by ConfigMgr (true) or Intune (false)</summary>
        public bool DeviceConfigurationManagedByConfigMgr { get; set; }
        
        /// <summary>Compliance policies managed by ConfigMgr (true) or Intune (false)</summary>
        public bool CompliancePolicyManagedByConfigMgr { get; set; }
        
        /// <summary>Windows Update for Business managed by ConfigMgr (true) or Intune (false)</summary>
        public bool WindowsUpdateManagedByConfigMgr { get; set; }
        
        /// <summary>Endpoint Protection managed by ConfigMgr (true) or Intune (false)</summary>
        public bool EndpointProtectionManagedByConfigMgr { get; set; }
        
        /// <summary>Office Click-to-Run apps managed by ConfigMgr (true) or Intune (false)</summary>
        public bool OfficeAppsManagedByConfigMgr { get; set; }

        /// <summary>
        /// Returns true if ALL workloads are managed by Intune (all ConfigMgr flags are false).
        /// This means the device is ready to become cloud-native (remove ConfigMgr client).
        /// </summary>
        public bool AllWorkloadsManagedByIntune =>
            !ModernAppsManagedByConfigMgr &&
            !ResourceAccessManagedByConfigMgr &&
            !DeviceConfigurationManagedByConfigMgr &&
            !CompliancePolicyManagedByConfigMgr &&
            !WindowsUpdateManagedByConfigMgr &&
            !EndpointProtectionManagedByConfigMgr &&
            !OfficeAppsManagedByConfigMgr;

        /// <summary>
        /// Count of workloads currently managed by Intune (ConfigMgr flag = false).
        /// </summary>
        public int WorkloadsManagedByIntuneCount
        {
            get
            {
                int count = 0;
                if (!ModernAppsManagedByConfigMgr) count++;
                if (!ResourceAccessManagedByConfigMgr) count++;
                if (!DeviceConfigurationManagedByConfigMgr) count++;
                if (!CompliancePolicyManagedByConfigMgr) count++;
                if (!WindowsUpdateManagedByConfigMgr) count++;
                if (!EndpointProtectionManagedByConfigMgr) count++;
                if (!OfficeAppsManagedByConfigMgr) count++;
                return count;
            }
        }

        /// <summary>Total workloads (7 co-management workloads)</summary>
        public const int TotalWorkloads = 7;

        /// <summary>
        /// Returns a list of workloads still managed by ConfigMgr (blockers for cloud-native).
        /// </summary>
        public List<string> WorkloadsStillOnConfigMgr
        {
            get
            {
                var blockers = new List<string>();
                if (ModernAppsManagedByConfigMgr) blockers.Add("Modern Apps");
                if (ResourceAccessManagedByConfigMgr) blockers.Add("Resource Access");
                if (DeviceConfigurationManagedByConfigMgr) blockers.Add("Device Configuration");
                if (CompliancePolicyManagedByConfigMgr) blockers.Add("Compliance Policy");
                if (WindowsUpdateManagedByConfigMgr) blockers.Add("Windows Update");
                if (EndpointProtectionManagedByConfigMgr) blockers.Add("Endpoint Protection");
                if (OfficeAppsManagedByConfigMgr) blockers.Add("Office Apps");
                return blockers;
            }
        }
    }

    /// <summary>
    /// Summary of workload authority across all co-managed devices.
    /// </summary>
    public class WorkloadAuthoritySummary
    {
        public int TotalCoManagedDevices { get; set; }
        public int DevicesReadyForCloudNative { get; set; }
        public List<DeviceWorkloadAuthority> Devices { get; set; } = new();

        /// <summary>Percentage of co-managed devices ready for cloud-native</summary>
        public double CloudNativeReadyPercentage => TotalCoManagedDevices > 0
            ? Math.Round((double)DevicesReadyForCloudNative / TotalCoManagedDevices * 100, 1)
            : 0;

        /// <summary>
        /// Breakdown of how many devices have each workload managed by Intune.
        /// </summary>
        public Dictionary<string, int> WorkloadIntuneAdoptionCounts { get; set; } = new();
    }

    /// <summary>
    /// Comparison data for Update Management between Intune WUfB and ConfigMgr WSUS.
    /// Used to demonstrate cloud-native value proposition.
    /// </summary>
    public class UpdateManagementComparison
    {
        // Intune (Cloud Native) metrics
        public int IntuneDeviceCount { get; set; }
        public double IntuneCompliancePercentage { get; set; }
        public double IntuneAvgDaysSinceSync { get; set; }
        
        // ConfigMgr metrics
        public int ConfigMgrDeviceCount { get; set; }
        public double ConfigMgrCompliancePercentage { get; set; }
        public double ConfigMgrAvgDaysSinceScan { get; set; }
        
        // Calculated comparison
        public double ComplianceDifference => IntuneCompliancePercentage - ConfigMgrCompliancePercentage;
        public bool CloudNativeIsBetter => ComplianceDifference > 0;
        
        public string ComparisonSummary => CloudNativeIsBetter 
            ? $"Cloud-native devices are {Math.Abs(ComplianceDifference):F0}% more compliant!"
            : ComplianceDifference < 0 
                ? $"ConfigMgr devices are {Math.Abs(ComplianceDifference):F0}% more compliant"
                : "Compliance rates are equal";
        
        public string ComparisonIcon => CloudNativeIsBetter ? "📈" : ComplianceDifference < 0 ? "📉" : "➡️";
    }

    /// <summary>
    /// OS Version distribution for currency comparison between cloud-native and ConfigMgr devices.
    /// </summary>
    public class OSCurrencyComparison
    {
        public List<OSVersionGroup> IntuneDistribution { get; set; } = new();
        public List<OSVersionGroup> ConfigMgrDistribution { get; set; } = new();
        
        // Summary metrics
        public int IntuneDeviceCount { get; set; }
        public int ConfigMgrDeviceCount { get; set; }
        
        public double IntuneWindows11Percentage { get; set; }
        public double ConfigMgrWindows11Percentage { get; set; }
        
        public double IntuneLatestBuildPercentage { get; set; }
        public double ConfigMgrLatestBuildPercentage { get; set; }
        
        public double Windows11Difference => IntuneWindows11Percentage - ConfigMgrWindows11Percentage;
        public bool CloudNativeMoreCurrent => Windows11Difference > 0;
        
        public string ComparisonSummary => CloudNativeMoreCurrent 
            ? $"Cloud-native devices have {Math.Abs(Windows11Difference):F0}% higher Windows 11 adoption!"
            : Windows11Difference < 0 
                ? $"ConfigMgr devices have {Math.Abs(Windows11Difference):F0}% higher Windows 11 adoption"
                : "Windows 11 adoption is equal";
        
        public string ComparisonIcon => CloudNativeMoreCurrent ? "🚀" : Windows11Difference < 0 ? "📉" : "➡️";
    }

    /// <summary>
    /// Represents a group of devices by OS version.
    /// </summary>
    public class OSVersionGroup
    {
        public string OSVersion { get; set; } = string.Empty;
        public string FriendlyName { get; set; } = string.Empty; // e.g., "Windows 11 24H2"
        public int DeviceCount { get; set; }
        public double Percentage { get; set; }
        
        public string DisplayColor => FriendlyName.Contains("11 24H2") ? "#107C10" : 
                                       FriendlyName.Contains("11 23H2") ? "#0078D4" :
                                       FriendlyName.Contains("11") ? "#00BCF2" :
                                       FriendlyName.Contains("10") ? "#FFB900" : "#888888";
    }

    /// <summary>
    /// Device Sync Freshness comparison - how quickly can devices receive policy updates?
    /// Shows cloud-native responsiveness advantage.
    /// </summary>
    public class SyncFreshnessComparison
    {
        // Intune metrics
        public int IntuneDeviceCount { get; set; }
        public double IntuneAvgDaysSinceSync { get; set; }
        public int IntuneSyncedToday { get; set; }
        public double IntuneSyncedTodayPercentage { get; set; }
        
        // ConfigMgr metrics
        public int ConfigMgrDeviceCount { get; set; }
        public double ConfigMgrAvgDaysSinceScan { get; set; }
        public int ConfigMgrScannedToday { get; set; }
        public double ConfigMgrScannedTodayPercentage { get; set; }
        
        // Data availability flags (0 days is valid - means synced today)
        public bool HasIntuneData => IntuneDeviceCount > 0;
        // ConfigMgr has data only if we have devices AND either some scanned today OR non-zero average
        public bool HasConfigMgrData => ConfigMgrDeviceCount > 0 && 
            (ConfigMgrScannedToday > 0 || ConfigMgrAvgDaysSinceScan > 0);
        
        // Detect when ConfigMgr shows 0.0 days but 0% scanned - likely no real data
        public bool ConfigMgrDataSuspect => ConfigMgrDeviceCount > 0 && 
            ConfigMgrAvgDaysSinceScan == 0 && ConfigMgrScannedTodayPercentage == 0;
        
        // Comparison
        public double SpeedMultiplier => HasConfigMgrData && HasIntuneData && IntuneAvgDaysSinceSync > 0
            ? Math.Round(ConfigMgrAvgDaysSinceScan / IntuneAvgDaysSinceSync, 1) 
            : 0;
        
        public bool CloudNativeIsFaster => HasIntuneData && HasConfigMgrData && IntuneAvgDaysSinceSync < ConfigMgrAvgDaysSinceScan;
        public bool ConfigMgrIsFaster => HasIntuneData && HasConfigMgrData && ConfigMgrAvgDaysSinceScan < IntuneAvgDaysSinceSync;
        
        public string ComparisonSummary
        {
            get
            {
                // Handle missing data cases
                if (!HasConfigMgrData && !HasIntuneData)
                    return "No sync data available from either source";
                if (!HasConfigMgrData || ConfigMgrDataSuspect)
                    return "ConfigMgr scan data not available";
                if (!HasIntuneData)
                    return "No Intune sync data available for comparison";
                
                // Compare average days since sync (lower is better)
                var intuneDays = IntuneAvgDaysSinceSync;
                var configMgrDays = ConfigMgrAvgDaysSinceScan;
                
                // If both are very recent (< 1 day), they're both good
                if (intuneDays < 1 && configMgrDays < 1)
                    return "Both platforms have excellent response times";
                
                // If ConfigMgr is significantly better (lower days)
                if (configMgrDays < intuneDays && intuneDays > 1)
                {
                    if (configMgrDays < 1)
                        return $"ConfigMgr avg today, Intune avg {intuneDays:F0} days";
                    var ratio = Math.Round(intuneDays / configMgrDays, 1);
                    return $"ConfigMgr {ratio:F0}x faster ({configMgrDays:F0}d vs {intuneDays:F0}d)";
                }
                
                // If Intune is significantly better
                if (intuneDays < configMgrDays && configMgrDays > 1)
                {
                    if (intuneDays < 1)
                        return $"Intune avg today, ConfigMgr avg {configMgrDays:F0} days";
                    var ratio = Math.Round(configMgrDays / intuneDays, 1);
                    return $"Intune {ratio:F0}x faster ({intuneDays:F0}d vs {configMgrDays:F0}d)";
                }
                
                return "Response times are comparable";
            }
        }
        
        // Icon: ⚡ = Cloud faster, ➖ = ConfigMgr faster, ➡️ = comparable, ❓ = no data
        public string ComparisonIcon => !HasConfigMgrData || !HasIntuneData || ConfigMgrDataSuspect ? "❓" : 
            (ConfigMgrIsFaster ? "➖" : (CloudNativeIsFaster ? "⚡" : "➡️"));
    }

    /// <summary>
    /// Stale Device Rate comparison - security blind spots from unmanaged devices.
    /// Stale = no check-in for 14+ days.
    /// </summary>
    public class StaleDeviceComparison
    {
        public const int StaleThresholdDays = 14;
        
        // Intune metrics
        public int IntuneDeviceCount { get; set; }
        public int IntuneStaleCount { get; set; }
        public double IntuneStalePercentage { get; set; }
        
        // ConfigMgr metrics
        public int ConfigMgrDeviceCount { get; set; }
        public int ConfigMgrStaleCount { get; set; }
        public double ConfigMgrStalePercentage { get; set; }
        
        // Track devices with missing data (counted as stale but actually unknown)
        public int ConfigMgrDevicesWithNoLastActiveTime { get; set; }
        public bool ConfigMgrAllMissingData => ConfigMgrDeviceCount > 0 && 
            ConfigMgrDevicesWithNoLastActiveTime == ConfigMgrDeviceCount;
        
        // Comparison
        public double StaleRatioMultiplier => IntuneStalePercentage > 0 
            ? Math.Round(ConfigMgrStalePercentage / IntuneStalePercentage, 1) 
            : 0;
        
        public bool CloudNativeHasFewerStale => IntuneStalePercentage < ConfigMgrStalePercentage;
        
        /// <summary>
        /// Security impact explanation for the stale devices
        /// </summary>
        public string SecurityImpact => IntuneStaleCount > 0
            ? $"{IntuneStaleCount} devices may have outdated security policies - only visible via cloud"
            : "All devices are receiving current security policies";
        
        public string ComparisonSummary
        {
            get
            {
                // If all ConfigMgr devices have no data, show simpler message
                if (ConfigMgrAllMissingData)
                    return "ConfigMgr activity data not available";
                
                // Handle zero stale percentages
                if (ConfigMgrStalePercentage == 0 && IntuneStalePercentage == 0)
                    return "All devices are actively communicating";
                
                // KEY INSIGHT: Intune detecting stale devices is CLOUD VISIBILITY in action
                // ConfigMgr CANNOT see devices when they're off-network (no VPN, remote workers)
                // Intune sees them because cloud = always connected over internet
                if (ConfigMgrStalePercentage == 0 && IntuneStalePercentage > 0)
                {
                    // Emphasize security impact and cloud advantage
                    if (IntuneStaleCount > 0)
                        return $"{IntuneStaleCount} devices with policy gaps - visible only via cloud";
                    return "Cloud provides visibility ConfigMgr cannot";
                }
                
                if (IntuneStalePercentage == 0 && ConfigMgrStalePercentage > 0)
                    return $"Cloud-native has zero blind spots ({ConfigMgrStaleCount} stale in ConfigMgr)";
                
                // Both have stale devices - compare
                if (CloudNativeHasFewerStale && StaleRatioMultiplier > 1)
                    return $"Cloud-native has {StaleRatioMultiplier:F0}x fewer security blind spots";
                
                if (IntuneStalePercentage == ConfigMgrStalePercentage)
                    return "Similar visibility across both platforms";
                
                // More stale in Intune - emphasize these are VISIBLE because of cloud
                if (IntuneStalePercentage > ConfigMgrStalePercentage)
                    return $"{IntuneStaleCount} off-network devices identified via cloud visibility";
                
                var ratio = Math.Round(ConfigMgrStalePercentage / IntuneStalePercentage, 1);
                return $"Cloud-native has {ratio:F0}x fewer blind spots";
            }
        }
        
        // Icon reflects cloud visibility advantage - cloud is good even when detecting stale devices
        public string ComparisonIcon => ConfigMgrAllMissingData ? "❓" : 
            (IntuneStalePercentage > ConfigMgrStalePercentage ? "☁️" : 
            (CloudNativeHasFewerStale ? "🔍" : "➡️"));
    }

    /// <summary>
    /// Conditional Access Readiness comparison - Zero Trust foundation.
    /// ConfigMgr-only devices CANNOT participate in CA (architectural fact).
    /// </summary>
    public class ConditionalAccessComparison
    {
        // Intune metrics
        public int IntuneDeviceCount { get; set; }
        public int IntuneCAReadyCount { get; set; } // Compliant devices
        public double IntuneCAReadyPercentage { get; set; }
        
        // ConfigMgr-only metrics (these are always 0 - CA requires Intune enrollment)
        public int ConfigMgrOnlyDeviceCount { get; set; }
        public int ConfigMgrOnlyCAReadyCount => 0; // Always 0 - architectural fact
        public double ConfigMgrOnlyCAReadyPercentage => 0; // Always 0%
        
        // Comparison
        public bool HasCAGap => ConfigMgrOnlyDeviceCount > 0;
        
        public string ComparisonSummary => HasCAGap
            ? $"{ConfigMgrOnlyDeviceCount:N0} devices cannot participate in Zero Trust policies"
            : "All devices can participate in Conditional Access";
        
        public string DetailMessage => HasCAGap
            ? "ConfigMgr-only devices are not enrolled in Intune and cannot be evaluated by Conditional Access policies. Co-management with the compliance workload moved to Intune is the bridge."
            : "";
        
        public string ComparisonIcon => HasCAGap ? "🛡️" : "✅";
    }

    /// <summary>
    /// Threat Detection comparison - shows partner reported threat state vs basic AV status.
    /// Cloud-native devices report actionable threat status, not just "enabled".
    /// </summary>
    public class ThreatDetectionComparison
    {
        // Intune metrics (from partnerReportedThreatState)
        public int IntuneDeviceCount { get; set; }
        public int IntuneSecuredCount { get; set; }
        public int IntuneMisconfiguredCount { get; set; }
        public int IntuneCompromisedCount { get; set; }
        public int IntuneUnknownCount { get; set; }
        
        // ConfigMgr metrics (from SMS_G_System_AntimalwareHealthStatus)
        public int ConfigMgrDeviceCount { get; set; }
        public int ConfigMgrProtectionEnabledCount { get; set; }
        public int ConfigMgrProtectionDisabledCount { get; set; }
        
        // License status - set by CloudReadinessService
        public bool IsMDELicensed { get; set; } = true; // Assume licensed unless proven otherwise
        
        // MDE Detection - if all devices have null/Unknown threat state, MDE likely not connected
        public bool IsMDEConnected => IntuneDeviceCount > 0 && 
            (IntuneSecuredCount > 0 || IntuneCompromisedCount > 0 || IntuneMisconfiguredCount > 0);
        
        // Comparison
        public bool CloudHasActionableData => IntuneCompromisedCount > 0 || IntuneMisconfiguredCount > 0;
        
        public string ComparisonSummary
        {
            get
            {
                // Check for unlicensed state first
                if (!IsMDELicensed && IntuneDeviceCount > 0)
                    return $"⚠️ MDE license not detected. Enable Microsoft Defender for Endpoint to see threat visibility for {IntuneDeviceCount:N0} devices.";
                
                // Check for not connected (licensed but no data)
                if (!IsMDEConnected && IntuneDeviceCount > 0)
                    return $"⚠️ MDE not connected - {IntuneDeviceCount:N0} devices have no threat visibility. Configure Defender for Endpoint connector in Intune.";
                
                // Normal cases
                if (CloudHasActionableData)
                    return $"Intune detected {IntuneCompromisedCount} compromised, {IntuneMisconfiguredCount} misconfigured devices";
                
                if (IntuneSecuredCount > 0)
                    return $"{IntuneSecuredCount:N0} devices confirmed secured by Defender";
                
                return "Connect to Graph to see threat detection status";
            }
        }
        
        public string ConfigMgrSummary => ConfigMgrProtectionEnabledCount > 0
            ? $"Protection enabled on {ConfigMgrProtectionEnabledCount:N0} devices"
            : "No visibility into threat status";
        
        public string ComparisonIcon => !IsMDELicensed || !IsMDEConnected ? "⚠️" : CloudHasActionableData ? "🚨" : "🛡️";
        
        // Friendly message for UI when MDE not available
        public string MDEStatusMessage
        {
            get
            {
                if (!IsMDELicensed)
                    return "Microsoft Defender for Endpoint license not detected. Enable MDE P1 or P2 to see real-time threat visibility.";
                if (!IsMDEConnected && IntuneDeviceCount > 0)
                    return "MDE is licensed but not connected to Intune. Configure the Defender for Endpoint connector in Intune > Endpoint Security > Microsoft Defender for Endpoint.";
                return "";
            }
        }
    }

    /// <summary>
    /// Active Malware comparison - only cloud-native can show real-time malware counts.
    /// </summary>
    public class ActiveMalwareComparison
    {
        // Intune metrics (from windowsActiveMalwareCount)
        public int IntuneDeviceCount { get; set; }
        public int TotalActiveMalwareCount { get; set; }
        public int DevicesWithMalwareCount { get; set; }
        public List<string> DevicesWithMalware { get; set; } = new();
        
        // ConfigMgr - no real-time visibility
        public int ConfigMgrDeviceCount { get; set; }
        // ConfigMgr cannot report this - always unknown
        
        // License status - set by CloudReadinessService
        public bool IsMDEP2Licensed { get; set; } = true; // Assume licensed unless proven otherwise
        
        public bool HasActiveMalware => TotalActiveMalwareCount > 0;
        
        public string ComparisonSummary
        {
            get
            {
                // Check for P2 license requirement
                if (!IsMDEP2Licensed && IntuneDeviceCount > 0)
                    return $"⚠️ MDE P2 license not detected. Active malware counts require Microsoft Defender for Endpoint Plan 2.";
                
                if (HasActiveMalware)
                    return $"⚠️ {DevicesWithMalwareCount} devices have {TotalActiveMalwareCount} active threats";
                
                if (IntuneDeviceCount > 0)
                    return "✓ No active malware detected";
                
                return "Connect to Graph for malware visibility";
            }
        }
        
        public string ConfigMgrSummary => "No real-time malware visibility";
        
        public string ComparisonIcon => HasActiveMalware ? "🦠" : "✅";
    }

    /// <summary>
    /// BitLocker encryption comparison - both platforms can report this, but cloud has key escrow advantage.
    /// </summary>
    public class BitLockerComparison
    {
        // Intune metrics (from isEncrypted)
        public int IntuneDeviceCount { get; set; }
        public int IntuneEncryptedCount { get; set; }
        public int IntuneEncryptionUnknownCount { get; set; } // Devices with IsEncrypted=null
        public double IntuneEncryptedPercentage => IntuneDeviceCount > 0 
            ? Math.Round((double)IntuneEncryptedCount / IntuneDeviceCount * 100, 1) : 0;
        
        // ConfigMgr metrics (from SMS_G_System_ENCRYPTABLE_VOLUME)
        public int ConfigMgrDeviceCount { get; set; }
        public int ConfigMgrEncryptedCount { get; set; }
        public double ConfigMgrEncryptedPercentage => ConfigMgrDeviceCount > 0 
            ? Math.Round((double)ConfigMgrEncryptedCount / ConfigMgrDeviceCount * 100, 1) : 0;
        
        // Data availability
        public bool HasIntuneData => IntuneDeviceCount > 0 && IntuneEncryptionUnknownCount < IntuneDeviceCount;
        public bool HasConfigMgrData => ConfigMgrDeviceCount > 0;
        
        // Cloud advantage
        public string CloudBenefit => "Recovery keys in Azure AD - accessible from any browser";
        public string OnPremNote => "Recovery keys in MBAM - requires VPN + console";
        
        public bool CloudHasHigherEncryption => HasIntuneData && IntuneEncryptedPercentage > ConfigMgrEncryptedPercentage;
        
        public string ComparisonSummary
        {
            get
            {
                // Handle missing data - Intune not reporting encryption status
                if (IntuneDeviceCount > 0 && IntuneEncryptionUnknownCount == IntuneDeviceCount)
                    return "Intune not reporting encryption status - check device sync";
                
                // Handle missing ConfigMgr data
                if (!HasConfigMgrData && HasIntuneData)
                    return "No ConfigMgr BitLocker data - SMS_G_System_ENCRYPTABLE_VOLUME not inventoried";
                
                // Both have data - compare
                if (CloudHasHigherEncryption)
                    return $"Cloud-native {IntuneEncryptedPercentage - ConfigMgrEncryptedPercentage:F0}% more encrypted";
                
                if (Math.Abs(IntuneEncryptedPercentage - ConfigMgrEncryptedPercentage) < 5)
                    return "Encryption rates comparable - cloud recovery keys accessible anywhere";
                
                return "Similar encryption - cloud advantage is key recovery accessibility";
            }
        }
        
        public string ComparisonIcon => (!HasIntuneData && IntuneDeviceCount > 0) ? "❓" : "🔑";
    }

    /// <summary>
    /// TPM Health comparison - both can query TPM, but only cloud can attest to Azure AD.
    /// </summary>
    public class TpmHealthComparison
    {
        // Intune metrics
        public int IntuneDeviceCount { get; set; }
        public int IntuneTpmReadyCount { get; set; } // TPM 2.0 enabled and attested
        public double IntuneTpmReadyPercentage => IntuneDeviceCount > 0 
            ? Math.Round((double)IntuneTpmReadyCount / IntuneDeviceCount * 100, 1) : 0;
        
        // ConfigMgr metrics (from SMS_G_System_TPM)
        public int ConfigMgrDeviceCount { get; set; }
        public int ConfigMgrTpmEnabledCount { get; set; }
        public int ConfigMgrTpm20Count { get; set; }
        public double ConfigMgrTpmEnabledPercentage => ConfigMgrDeviceCount > 0 
            ? Math.Round((double)ConfigMgrTpmEnabledCount / ConfigMgrDeviceCount * 100, 1) : 0;
        
        // Cloud advantage
        public string CloudBenefit => "TPM attested to Azure AD for Conditional Access";
        public string OnPremNote => "TPM data local only - cannot prove health remotely";
        
        public string ComparisonSummary => IntuneTpmReadyCount > 0
            ? $"{IntuneTpmReadyCount:N0} devices can attest TPM health to Azure AD"
            : "Connect to Graph for TPM attestation data";
        
        public string ComparisonIcon => "🔐";
    }

    /// <summary>
    /// Device Health Attestation comparison - Zero Trust foundation.
    /// Only cloud-managed devices can prove hardware health to Azure AD.
    /// </summary>
    public class DeviceHealthAttestationComparison
    {
        // Intune metrics (from deviceHealthAttestationState)
        public int IntuneDeviceCount { get; set; }
        public int IntuneSecureBootEnabled { get; set; }
        public int IntuneBitLockerEnabled { get; set; }
        public int IntuneCodeIntegrityEnabled { get; set; }
        public int IntuneFullyAttestedCount { get; set; } // All 4 criteria met
        
        // ConfigMgr cannot attest to Azure AD
        public int ConfigMgrDeviceCount { get; set; }
        // These are always 0 for remote attestation
        public int ConfigMgrAttestedCount => 0;
        
        public string CloudBenefit => "Hardware-verified trust for Zero Trust policies";
        public string OnPremNote => "Local data only - cannot prove health to cloud";
        
        public bool HasAttestationData => IntuneFullyAttestedCount > 0;
        
        public string ComparisonSummary => HasAttestationData
            ? $"{IntuneFullyAttestedCount:N0} devices passed hardware attestation"
            : IntuneDeviceCount > 0 
                ? "Query deviceHealthAttestationState for attestation data"
                : "Connect to Graph for health attestation";
        
        public string ComparisonIcon => "🔒";
    }

    /// <summary>
    /// Remote Actions comparison - what you can do right now from each platform.
    /// </summary>
    public class RemoteActionsComparison
    {
        // Intune remote actions (static list - always available)
        public List<string> IntuneActions { get; set; } = new()
        {
            "Wipe Device", "Retire Device", "Remote Lock", "Restart Device", 
            "Fresh Start", "Locate Device", "Sync Device", "Rotate BitLocker Key",
            "Defender Full Scan", "Defender Quick Scan", "Collect Diagnostics",
            "Rename Device", "Autopilot Reset", "Clear Activation Lock", "Send Notification"
        };
        
        // ConfigMgr remote actions (limited without client connection)
        public List<string> ConfigMgrActions { get; set; } = new()
        {
            "Sync Policy", "Run Script", "Client Notification"
        };
        
        public int IntuneActionCount => IntuneActions.Count;
        public int ConfigMgrActionCount => ConfigMgrActions.Count;
        
        // These are the actions only Intune can do
        public List<string> CloudUniqueActions => IntuneActions
            .Except(new[] { "Sync Policy" })
            .ToList();
        
        public string ComparisonSummary => 
            $"Cloud-native: {IntuneActionCount} actions available anywhere, anytime";
        
        public string OnPremNote => 
            $"On-prem: {ConfigMgrActionCount} actions (requires client check-in)";
        
        public string ComparisonIcon => "🎮";
    }

    /// <summary>
    /// Device Compliance comparison for the value comparison tab.
    /// </summary>
    public class DeviceComplianceComparison
    {
        // Intune metrics
        public int IntuneDeviceCount { get; set; }
        public int IntuneCompliantCount { get; set; }
        public double IntuneCompliancePercentage => IntuneDeviceCount > 0 
            ? Math.Round((double)IntuneCompliantCount / IntuneDeviceCount * 100, 1) : 0;
        
        // ConfigMgr metrics
        public int ConfigMgrDeviceCount { get; set; }
        public int ConfigMgrCompliantCount { get; set; }
        public double ConfigMgrCompliancePercentage => ConfigMgrDeviceCount > 0 
            ? Math.Round((double)ConfigMgrCompliantCount / ConfigMgrDeviceCount * 100, 1) : 0;
        
        // Comparison
        public double ComplianceDifference => IntuneCompliancePercentage - ConfigMgrCompliancePercentage;
        public bool CloudHasBetterCompliance => IntuneCompliancePercentage > ConfigMgrCompliancePercentage;
        
        public string ComparisonSummary => CloudHasBetterCompliance && ComplianceDifference > 0
            ? $"Cloud-native {ComplianceDifference:F0}% more compliant"
            : Math.Abs(ComplianceDifference) < 5 
                ? "Compliance rates comparable"
                : $"Compliance rates within {Math.Abs(ComplianceDifference):F0}%";
        
        public string ComparisonIcon => CloudHasBetterCompliance ? "📈" : "📊";
    }

    /// <summary>
    /// Defender/MDE Integration comparison - shows real-time security visibility.
    /// Intune with MDE provides threat state, ConfigMgr only knows "AV enabled".
    /// </summary>
    public class DefenderIntegrationComparison
    {
        // Intune metrics
        public int IntuneDeviceCount { get; set; }
        public int IntuneMDEOnboardedCount { get; set; }
        public int IntuneRealTimeProtectionCount { get; set; }
        public int IntuneRemediatedMalwareCount { get; set; }
        
        public double IntuneMDEOnboardedPercentage => IntuneDeviceCount > 0 
            ? Math.Round((double)IntuneMDEOnboardedCount / IntuneDeviceCount * 100, 1) : 0;
        
        // ConfigMgr metrics - only basic AV status
        public int ConfigMgrDeviceCount { get; set; }
        public int ConfigMgrProtectionEnabledCount { get; set; }
        
        public double ConfigMgrProtectionPercentage => ConfigMgrDeviceCount > 0 
            ? Math.Round((double)ConfigMgrProtectionEnabledCount / ConfigMgrDeviceCount * 100, 1) : 0;
        
        // License status - set by CloudReadinessService
        public bool IsMDELicensed { get; set; } = true;
        public bool IsMDEP2Licensed { get; set; } = true;
        
        // Comparison
        public bool HasMDEVisibility => IntuneMDEOnboardedCount > 0;
        public bool HasRemediations => IntuneRemediatedMalwareCount > 0;
        
        public string ComparisonSummary
        {
            get
            {
                // Check for license issues first
                if (!IsMDELicensed && IntuneDeviceCount > 0)
                    return $"⚠️ MDE license not detected. Enable Microsoft Defender for Endpoint to see threat visibility for {IntuneDeviceCount:N0} devices.";
                
                if (HasMDEVisibility)
                {
                    if (HasRemediations)
                        return $"{IntuneMDEOnboardedCount:N0} devices with MDE visibility, {IntuneRemediatedMalwareCount} threats auto-remediated";
                    return $"{IntuneMDEOnboardedCount:N0} devices with real-time threat visibility ({IntuneMDEOnboardedPercentage:F0}%)";
                }
                
                if (IntuneDeviceCount > 0)
                    return "⚠️ MDE connector not enabled. Configure Defender for Endpoint integration in Intune to see threat visibility.";
                
                return "No Intune devices available";
            }
        }
        
        public string ConfigMgrSummary => ConfigMgrProtectionEnabledCount > 0
            ? $"AV enabled on {ConfigMgrProtectionEnabledCount:N0} devices - no threat state visibility"
            : "No AV status data available";
        
        public string ComparisonIcon => !IsMDELicensed ? "❌" : HasMDEVisibility ? "🛡️" : "⚠️";
        
        // Friendly message for UI when MDE not available
        public string MDEStatusMessage
        {
            get
            {
                if (!IsMDELicensed)
                    return "Microsoft Defender for Endpoint license not detected. Requires MDE P1 or P2 license.";
                if (!HasMDEVisibility && IntuneDeviceCount > 0)
                    return "MDE is licensed but devices aren't onboarded. Ensure devices have the MDE sensor installed and reporting.";
                if (!IsMDEP2Licensed && HasMDEVisibility)
                    return "MDE P1 detected. Upgrade to MDE P2 for malware counts and auto-remediation tracking.";
                return "";
            }
        }
        
        // Key capabilities only available with Intune + MDE
        public List<string> CloudUniqueCapabilities { get; set; } = new()
        {
            "Real-time threat state",
            "Active malware count",
            "Auto-remediation tracking",
            "Threat severity levels",
            "Cloud-delivered protection"
        };
    }
}

