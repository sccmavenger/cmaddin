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
    /// Per Microsoft Autopilot Requirements (https://learn.microsoft.com/en-us/autopilot/requirements):
    /// - Software: Windows 10 1809+ or Windows 11 (Pro/Enterprise/Education edition)
    /// - Networking: Access to Autopilot deployment service URLs
    /// - Licensing: M365/EMS/Intune license
    /// - Configuration: Device registered in Autopilot, Azure AD joined or Hybrid joined
    /// NOTE: TPM 2.0 is NOT required for basic Autopilot registration (only for Self-Deploying/Pre-Provisioning modes)
    /// </summary>
    public class AutopilotReadinessDetails
    {
        public int TotalDevices { get; set; }
        
        // OS Requirements (Windows 10 1809+ / Windows 11)
        public int HasSupportedOs { get; set; }
        public int HasUnsupportedOs { get; set; }
        
        // OS Edition Requirements (Pro/Enterprise/Education - NOT Home)
        public int HasSupportedEdition { get; set; }
        public int HasUnsupportedEdition { get; set; } // Home edition = BLOCKER
        
        // Azure AD Join Status (must have AAD identity for Autopilot)
        public int IsAadJoinedOrHybrid { get; set; }
        public int NotAadJoined { get; set; } // On-Prem only / Workgroup = BLOCKER
        
        // Join Type Breakdown
        public int HybridJoinedDevices { get; set; }
        public int EntraJoinedDevices { get; set; }
        public int OnPremOnlyDevices { get; set; }
        public int WorkgroupDevices { get; set; }
        
        // Autopilot Registration Status
        public int RegisteredInAutopilot { get; set; }
        public int NotRegisteredInAutopilot { get; set; }
        
        // Licensing (tenant-level)
        public bool TenantHasIntuneLicense { get; set; }
        
        // Legacy fields for backwards compatibility
        public int HasTpm20 { get; set; }
        public int HasUefi { get; set; }
        public int HasSecureBoot { get; set; }
        
        // Calculated readiness: Must meet ALL requirements
        // (OS version + OS edition + AAD joined + registered)
        public int FullyReady => Math.Min(Math.Min(Math.Min(
            HasSupportedOs, HasSupportedEdition), IsAadJoinedOrHybrid), RegisteredInAutopilot);
        
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
    /// Shows cloud-native responsiveness advantage via Push (Intune) vs Poll (ConfigMgr) architecture.
    /// </summary>
    public class SyncFreshnessComparison
    {
        /// <summary>
        /// Threshold for considering a device "abandoned" - devices not synced in this many days
        /// are excluded from the filtered average to provide a fair comparison.
        /// </summary>
        public const int AbandonedThresholdDays = 30;
        /// <summary>Minimum devices required for meaningful comparison</summary>
        public const int MinimumDevicesForComparison = 10;
        
        // Intune metrics (ALL devices - includes abandoned)
        public int IntuneDeviceCount { get; set; }
        public double IntuneAvgDaysSinceSync { get; set; }
        public int IntuneSyncedToday { get; set; }
        public double IntuneSyncedTodayPercentage { get; set; }
        
        // Intune FILTERED metrics (active devices only - synced within 30 days)
        /// <summary>Count of active Intune devices (synced within 30 days)</summary>
        public int IntuneActiveDeviceCount { get; set; }
        /// <summary>Average days since sync for ACTIVE devices only (fair comparison)</summary>
        public double IntuneActiveAvgDaysSinceSync { get; set; }
        /// <summary>Count of abandoned devices (not synced in 30+ days)</summary>
        public int IntuneAbandonedDeviceCount { get; set; }
        /// <summary>Percentage of devices that are abandoned</summary>
        public double IntuneAbandonedPercentage { get; set; }
        
        // ConfigMgr metrics
        public int ConfigMgrDeviceCount { get; set; }
        public double ConfigMgrAvgDaysSinceScan { get; set; }
        public int ConfigMgrScannedToday { get; set; }
        public double ConfigMgrScannedTodayPercentage { get; set; }
        
        // Data availability flags (0 days is valid - means synced today)
        public bool HasIntuneData => IntuneDeviceCount > 0;
        public bool HasIntuneActiveData => IntuneActiveDeviceCount > 0;
        // ConfigMgr has data only if we have devices AND either some scanned today OR non-zero average
        public bool HasConfigMgrData => ConfigMgrDeviceCount > 0 && 
            (ConfigMgrScannedToday > 0 || ConfigMgrAvgDaysSinceScan > 0);
        
        /// <summary>True if ConfigMgr has enough devices for meaningful comparison</summary>
        public bool HasMinimumConfigMgrData => ConfigMgrDeviceCount >= MinimumDevicesForComparison;
        
        /// <summary>True when Intune sees significantly more devices than ConfigMgr</summary>
        public bool CloudSeesMoreDevices => IntuneDeviceCount > ConfigMgrDeviceCount * 2;
        
        // Detect when ConfigMgr shows 0.0 days but 0% scanned - likely no real data
        public bool ConfigMgrDataSuspect => ConfigMgrDeviceCount > 0 && 
            ConfigMgrAvgDaysSinceScan == 0 && ConfigMgrScannedTodayPercentage == 0;
        
        /// <summary>True if there are significant abandoned devices skewing the raw average</summary>
        public bool HasAbandonedDevicesSkewingAverage => IntuneAbandonedDeviceCount > 0 && 
            IntuneAvgDaysSinceSync > IntuneActiveAvgDaysSinceSync + 5;
        
        // Comparison - use FILTERED Intune average for fair comparison
        public double SpeedMultiplier => HasConfigMgrData && HasIntuneActiveData && IntuneActiveAvgDaysSinceSync > 0
            ? Math.Round(ConfigMgrAvgDaysSinceScan / IntuneActiveAvgDaysSinceSync, 1) 
            : 0;
        
        // Use filtered averages for comparison (excludes abandoned devices)
        public bool CloudNativeIsFaster => HasIntuneActiveData && HasConfigMgrData && HasMinimumConfigMgrData &&
            IntuneActiveAvgDaysSinceSync < ConfigMgrAvgDaysSinceScan;
        public bool ConfigMgrIsFaster => HasIntuneActiveData && HasConfigMgrData && HasMinimumConfigMgrData &&
            ConfigMgrAvgDaysSinceScan < IntuneActiveAvgDaysSinceSync;
        
        /// <summary>
        /// Comparison summary emphasizing the architectural advantage:
        /// - Intune: Push-based delivery via WNS (seconds to minutes)
        /// - ConfigMgr: Poll-based, default 60-minute client policy interval
        /// </summary>
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
                
                // ConfigMgr has insufficient data for meaningful comparison
                if (!HasMinimumConfigMgrData)
                {
                    if (CloudSeesMoreDevices)
                        return $"Cloud visibility: {IntuneDeviceCount} devices (ConfigMgr sees {ConfigMgrDeviceCount})";
                    return $"Limited ConfigMgr data ({ConfigMgrDeviceCount} devices)";
                }
                
                // If there are abandoned devices, highlight the cleanup opportunity
                if (HasAbandonedDevicesSkewingAverage)
                {
                    return $"Push delivery (Intune) vs 60-min poll (ConfigMgr) - {IntuneAbandonedDeviceCount} devices need cleanup";
                }
                
                // Use FILTERED average for fair comparison
                var intuneDays = IntuneActiveAvgDaysSinceSync;
                var configMgrDays = ConfigMgrAvgDaysSinceScan;
                
                // Both have excellent response - emphasize the architectural difference
                if (intuneDays < 1 && configMgrDays < 1)
                    return "Push (Intune) vs Poll (ConfigMgr) - both syncing regularly";
                
                // Show the architectural advantage with context
                if (intuneDays <= configMgrDays)
                {
                    return "Intune push delivers in seconds; ConfigMgr polls every 60 min";
                }
                
                // Edge case: ConfigMgr showing better numbers (rare, usually small sample)
                return "Push (seconds) vs Poll (60 min) - verify device counts";
            }
        }
        
        /// <summary>
        /// Secondary display text showing abandoned device cleanup opportunity
        /// </summary>
        public string AbandonedDeviceMessage => IntuneAbandonedDeviceCount > 0
            ? $"{IntuneAbandonedDeviceCount} abandoned devices ({IntuneAbandonedPercentage:F0}%) skewing avg from {IntuneActiveAvgDaysSinceSync:F1}d to {IntuneAvgDaysSinceSync:F1}d"
            : string.Empty;
        
        // Icon: ⚡ = Cloud advantage (push), ☁️ = limited data but cloud visibility, ❓ = no data
        public string ComparisonIcon => !HasConfigMgrData || !HasIntuneData || ConfigMgrDataSuspect ? "❓" : 
            (!HasMinimumConfigMgrData ? "☁️" : "⚡");
    }

    /// <summary>
    /// Stale Device Rate comparison - security blind spots from unmanaged devices.
    /// Stale = no check-in for 14+ days.
    /// </summary>
    public class StaleDeviceComparison
    {
        public const int StaleThresholdDays = 14;
        /// <summary>Minimum devices required for meaningful comparison</summary>
        public const int MinimumDevicesForComparison = 10;
        
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
        
        /// <summary>True if ConfigMgr has enough devices for meaningful comparison</summary>
        public bool HasMinimumConfigMgrData => ConfigMgrDeviceCount >= MinimumDevicesForComparison;
        
        // Comparison
        public double StaleRatioMultiplier => IntuneStalePercentage > 0 
            ? Math.Round(ConfigMgrStalePercentage / IntuneStalePercentage, 1) 
            : 0;
        
        /// <summary>Only claim advantages when we have sufficient data</summary>
        public bool CloudNativeHasFewerStale => HasMinimumConfigMgrData && 
            IntuneStalePercentage < ConfigMgrStalePercentage;
        
        /// <summary>
        /// Devices only visible via cloud (Intune sees more than ConfigMgr)
        /// </summary>
        public int DevicesOnlyVisibleViaCloud => Math.Max(0, IntuneDeviceCount - ConfigMgrDeviceCount);
        
        /// <summary>
        /// Are we comparing different-sized populations? (Cloud sees more)
        /// </summary>
        public bool CloudSeesMoreDevices => IntuneDeviceCount > ConfigMgrDeviceCount;
        
        /// <summary>
        /// Display text for Intune side - explicitly says "stale" with total context
        /// </summary>
        public string IntuneCountDisplay => CloudSeesMoreDevices
            ? $"{IntuneStaleCount} stale (of {IntuneDeviceCount})"
            : $"{IntuneStaleCount:N0} stale";
        
        /// <summary>
        /// Display text for ConfigMgr side - shows the limited scope
        /// </summary>
        public string ConfigMgrCountDisplay => CloudSeesMoreDevices
            ? $"{ConfigMgrStaleCount} stale (of {ConfigMgrDeviceCount})"
            : $"{ConfigMgrStaleCount:N0} stale";
        
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
                
                // ConfigMgr has insufficient data for meaningful comparison
                if (!HasMinimumConfigMgrData && ConfigMgrDeviceCount > 0)
                {
                    if (CloudSeesMoreDevices)
                        return $"Cloud sees {DevicesOnlyVisibleViaCloud} devices ConfigMgr can't track";
                    return $"Limited ConfigMgr data ({ConfigMgrDeviceCount} devices)";
                }
                
                // Handle zero stale percentages
                if (ConfigMgrStalePercentage == 0 && IntuneStalePercentage == 0)
                    return "All devices are actively communicating";
                
                // KEY INSIGHT: Cloud sees MORE devices than ConfigMgr
                // ConfigMgr cannot see these devices - they're remote/cloud-native
                if (CloudSeesMoreDevices && IntuneStaleCount > 0)
                {
                    return $"Cloud visibility: {IntuneStaleCount} stale detected (ConfigMgr sees only {ConfigMgrDeviceCount})";
                }
                
                // Fallback for same-size populations
                if (ConfigMgrStalePercentage == 0 && IntuneStalePercentage > 0)
                {
                    return $"{IntuneStaleCount} devices haven't synced in 14+ days";
                }
                
                if (IntuneStalePercentage == 0 && ConfigMgrStalePercentage > 0)
                    return $"Cloud-native has zero blind spots ({ConfigMgrStaleCount} stale in ConfigMgr)";
                
                // Both have stale devices - compare (only with sufficient data)
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
        public string ComparisonIcon => ConfigMgrAllMissingData || !HasMinimumConfigMgrData ? "☁️" : 
            (CloudSeesMoreDevices || IntuneStalePercentage > ConfigMgrStalePercentage ? "☁️" : 
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
        
        // Data availability
        public bool HasConfigMgrData => ConfigMgrDeviceCount > 0;
        
        public string ComparisonSummary
        {
            get
            {
                // Handle missing ConfigMgr data
                if (!HasConfigMgrData && IntuneDeviceCount > 0)
                    return "No ConfigMgr TPM data - SMS_G_System_TPM not inventoried";
                
                if (IntuneTpmReadyCount > 0)
                    return $"{IntuneTpmReadyCount:N0} devices can attest TPM health to Azure AD";
                    
                return "Connect to Graph for TPM attestation data";
            }
        }
        
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
        /// <summary>Minimum devices required for meaningful comparison</summary>
        public const int MinimumDevicesForComparison = 10;
        
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
        
        /// <summary>True if ConfigMgr has enough devices for meaningful comparison</summary>
        public bool HasMinimumConfigMgrData => ConfigMgrDeviceCount >= MinimumDevicesForComparison;
        
        /// <summary>True when Intune sees significantly more devices than ConfigMgr (potential cloud visibility benefit)</summary>
        public bool CloudSeesMoreDevices => IntuneDeviceCount > ConfigMgrDeviceCount * 2;
        
        /// <summary>Additional devices visible only via cloud</summary>
        public int AdditionalCloudVisibility => Math.Max(0, IntuneDeviceCount - ConfigMgrDeviceCount);
        
        // Comparison
        public double ComplianceDifference => IntuneCompliancePercentage - ConfigMgrCompliancePercentage;
        
        /// <summary>Only claim cloud is better when we have sufficient data to compare</summary>
        public bool CloudHasBetterCompliance => HasMinimumConfigMgrData && 
            IntuneCompliancePercentage > ConfigMgrCompliancePercentage;
        
        /// <summary>Whether comparison is meaningful (sufficient data on both sides)</summary>
        public bool IsComparisonMeaningful => HasMinimumConfigMgrData && IntuneDeviceCount >= MinimumDevicesForComparison;
        
        public string ComparisonSummary
        {
            get 
            {
                // No ConfigMgr devices at all
                if (ConfigMgrDeviceCount == 0)
                    return $"Intune: {IntuneCompliantCount:N0}/{IntuneDeviceCount:N0} compliant - no ConfigMgr data";
                
                // ConfigMgr has insufficient data for meaningful comparison
                if (!HasMinimumConfigMgrData)
                {
                    if (CloudSeesMoreDevices)
                        return $"Cloud sees {AdditionalCloudVisibility:N0} devices ConfigMgr can't track";
                    return $"Limited ConfigMgr data ({ConfigMgrDeviceCount} devices)";
                }
                
                // Cloud sees significantly more devices - reframe as visibility benefit
                if (CloudSeesMoreDevices)
                    return $"Cloud visibility: {IntuneDeviceCount:N0} vs ConfigMgr {ConfigMgrDeviceCount:N0}";
                
                // Meaningful comparison available
                if (CloudHasBetterCompliance && ComplianceDifference > 0)
                    return $"Cloud-native {ComplianceDifference:F0}% more compliant";
                
                if (Math.Abs(ComplianceDifference) < 5)
                    return "Compliance rates comparable";
                
                return $"Compliance rates within {Math.Abs(ComplianceDifference):F0}%";
            }
        }
        
        public string ComparisonIcon => !IsComparisonMeaningful ? "☁️" : 
            (CloudHasBetterCompliance ? "📈" : "📊");
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

    // ═══════════════════════════════════════════════════════════════════════
    // NEW COMPARISON MODELS (v3.17.220) - Cloud Native Tab enhancements
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Compliance Policy Enforcement Depth — shows WHAT Intune actually enforces vs ConfigMgr baseline coverage.
    /// Data source: GetCompliancePolicySettingsAsync() → /deviceManagement/deviceCompliancePolicies
    /// </summary>
    public class CompliancePolicyDepthComparison
    {
        // Intune metrics (from deviceCompliancePolicies)
        public int IntunePolicyCount { get; set; }
        public List<string> IntuneEnforcedSettings { get; set; } = new();
        public int IntuneAssignedDeviceCount { get; set; }
        public List<CompliancePolicySummary> IntunePolicies { get; set; } = new();
        
        // ConfigMgr side — architectural framing
        public int ConfigMgrDeviceCount { get; set; }
        
        public bool HasIntunePolicies => IntunePolicyCount > 0;
        
        public string ComparisonSummary
        {
            get
            {
                if (!HasIntunePolicies)
                    return "No Intune compliance policies configured yet";
                
                return $"{IntunePolicyCount} policies enforcing {IntuneEnforcedSettings.Count} security requirements";
            }
        }
        
        public string ConfigMgrSummary => ConfigMgrDeviceCount > 0
            ? $"{ConfigMgrDeviceCount:N0} devices — compliance is a report, not enforcement"
            : "No ConfigMgr data";
        
        public string ComparisonIcon => HasIntunePolicies ? "📋" : "⚠️";
    }

    /// <summary>
    /// Summary of a single compliance policy for display.
    /// </summary>
    public class CompliancePolicySummary
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Requirements { get; set; } = new();
        public string AssignmentScope { get; set; } = string.Empty;
    }

    /// <summary>
    /// Co-Management Workload Authority comparison — shows which of the 7 workloads are on Intune vs ConfigMgr.
    /// Data source: GetCoManagedWorkloadAuthorityAsync() → /deviceManagement/managedDevices (configurationManagerClientEnabledFeatures)
    /// </summary>
    public class WorkloadAuthorityComparison
    {
        public int TotalCoManagedDevices { get; set; }
        public List<WorkloadSliderStatus> Workloads { get; set; } = new();
        
        // Summary
        public int WorkloadsFullyOnIntune => Workloads.Count(w => w.IntunePercentage >= 90);
        public int WorkloadsFullyOnConfigMgr => Workloads.Count(w => w.ConfigMgrPercentage >= 90);
        public int WorkloadsMixed => Workloads.Count(w => w.IntunePercentage > 10 && w.IntunePercentage < 90);
        
        public bool HasData => TotalCoManagedDevices > 0;
        
        public string ComparisonSummary
        {
            get
            {
                if (!HasData) return "No co-managed devices found";
                if (WorkloadsFullyOnIntune == 7) return "All 7 workloads fully transitioned to Intune!";
                return $"{WorkloadsFullyOnIntune}/7 workloads on Intune, {WorkloadsMixed} in transition";
            }
        }
        
        public string ComparisonIcon => WorkloadsFullyOnIntune >= 5 ? "🚀" : WorkloadsFullyOnIntune >= 3 ? "📈" : "🔄";
    }

    /// <summary>
    /// Status of a single co-management workload slider.
    /// </summary>
    public class WorkloadSliderStatus
    {
        public string WorkloadName { get; set; } = string.Empty;
        public string Icon { get; set; } = "⚙️";
        public int IntuneCount { get; set; }
        public int ConfigMgrCount { get; set; }
        public int TotalDevices => IntuneCount + ConfigMgrCount;
        public double IntunePercentage => TotalDevices > 0 ? Math.Round((double)IntuneCount / TotalDevices * 100, 1) : 0;
        public double ConfigMgrPercentage => TotalDevices > 0 ? Math.Round((double)ConfigMgrCount / TotalDevices * 100, 1) : 0;
    }

    /// <summary>
    /// Client Health / Agent Reliability comparison — ConfigMgr inactive clients vs Intune always-connected.
    /// Intune: lastSyncDateTime within 7 days. ConfigMgr: SMS_CombinedDeviceResources.ClientActiveStatus
    /// </summary>
    public class ClientHealthComparison
    {
        // Intune metrics
        public int IntuneDeviceCount { get; set; }
        public int IntuneHealthyCount { get; set; } // Synced within 7 days
        public double IntuneHealthyPercentage => IntuneDeviceCount > 0
            ? Math.Round((double)IntuneHealthyCount / IntuneDeviceCount * 100, 1) : 0;
        
        // ConfigMgr metrics (from ClientActiveStatus)
        public int ConfigMgrDeviceCount { get; set; }
        public int ConfigMgrActiveCount { get; set; } // ClientActiveStatus == 1
        public int ConfigMgrInactiveCount => ConfigMgrDeviceCount - ConfigMgrActiveCount;
        public double ConfigMgrActivePercentage => ConfigMgrDeviceCount > 0
            ? Math.Round((double)ConfigMgrActiveCount / ConfigMgrDeviceCount * 100, 1) : 0;
        
        public bool HasData => IntuneDeviceCount > 0 || ConfigMgrDeviceCount > 0;
        
        public string ComparisonSummary
        {
            get
            {
                if (ConfigMgrInactiveCount > 0)
                    return $"{ConfigMgrInactiveCount:N0} ConfigMgr clients inactive — cloud management works over any internet connection";
                if (HasData)
                    return "Both platforms showing healthy client communication";
                return "No data available";
            }
        }
        
        public string ConfigMgrSummary => ConfigMgrInactiveCount > 0
            ? $"{ConfigMgrInactiveCount:N0} inactive — likely off-network or VPN"
            : $"{ConfigMgrActiveCount:N0} active";
        
        public string ComparisonIcon => ConfigMgrInactiveCount > 10 ? "⚠️" : "✅";
    }

    /// <summary>
    /// AV Signature Freshness comparison — cloud-delivered protection vs ConfigMgr signature distribution.
    /// Intune: partnerReportedThreatState. ConfigMgr: SMS_G_System_AntimalwareHealthStatus.SignatureAge
    /// </summary>
    public class AVSignatureComparison
    {
        // Intune metrics (from partnerReportedThreatState)
        public int IntuneDeviceCount { get; set; }
        public int IntuneSecuredCount { get; set; } // threat state = Secured (up-to-date)
        public double IntuneSecuredPercentage => IntuneDeviceCount > 0
            ? Math.Round((double)IntuneSecuredCount / IntuneDeviceCount * 100, 1) : 0;
        
        // ConfigMgr metrics (from SMS_G_System_AntimalwareHealthStatus)
        public int ConfigMgrDeviceCount { get; set; }
        public int ConfigMgrUpToDateCount { get; set; } // SignatureUpToDate == true
        public double ConfigMgrAvgSignatureAgeDays { get; set; }
        public double ConfigMgrUpToDatePercentage => ConfigMgrDeviceCount > 0
            ? Math.Round((double)ConfigMgrUpToDateCount / ConfigMgrDeviceCount * 100, 1) : 0;
        
        public bool HasConfigMgrData => ConfigMgrDeviceCount > 0;
        
        public string ComparisonSummary
        {
            get
            {
                if (HasConfigMgrData && ConfigMgrAvgSignatureAgeDays > 1)
                    return $"ConfigMgr signatures average {ConfigMgrAvgSignatureAgeDays:F1} days old — each day is an attack window";
                if (IntuneSecuredCount > 0)
                    return $"{IntuneSecuredCount:N0} cloud devices confirmed secured — signatures delivered instantly from Microsoft CDN";
                return "Connect to see AV signature comparison";
            }
        }
        
        public string ComparisonIcon => HasConfigMgrData && ConfigMgrAvgSignatureAgeDays > 2 ? "⚠️" : "🛡️";
    }

    /// <summary>
    /// App Portfolio Readiness comparison — shows app migration feasibility by technology type.
    /// Intune: /deviceAppManagement/mobileApps. ConfigMgr: SMS_Application + SMS_DeploymentType
    /// </summary>
    public class AppPortfolioComparison
    {
        // Intune metrics
        public int IntuneAppCount { get; set; }
        
        // ConfigMgr metrics (from SMS_Application + SMS_DeploymentType)
        public int ConfigMgrAppCount { get; set; }
        public int ConfigMgrDeployedCount { get; set; }
        public Dictionary<string, int> TechnologyBreakdown { get; set; } = new();
        
        // Migration readiness
        public int MsiAppsCount => TechnologyBreakdown.GetValueOrDefault("MSI", 0);
        public int MsixAppsCount => TechnologyBreakdown.GetValueOrDefault("MSIX", 0);
        public int ScriptAppsCount => TechnologyBreakdown.GetValueOrDefault("Script", 0);
        public int AppVAppsCount => TechnologyBreakdown.GetValueOrDefault("App-V", 0);
        
        /// <summary>MSI + MSIX apps can migrate directly to Intune</summary>
        public int ReadyToMigrateCount => MsiAppsCount + MsixAppsCount;
        public double ReadyToMigratePercentage => ConfigMgrAppCount > 0
            ? Math.Round((double)ReadyToMigrateCount / ConfigMgrAppCount * 100, 1) : 0;
        
        public bool HasConfigMgrData => ConfigMgrAppCount > 0;
        
        public string ComparisonSummary
        {
            get
            {
                if (!HasConfigMgrData) return "No ConfigMgr application data available";
                if (ReadyToMigrateCount > 0)
                    return $"{ReadyToMigrateCount} apps ({ReadyToMigratePercentage:F0}%) use MSI/MSIX — ready for Intune migration today";
                return $"{ConfigMgrAppCount} ConfigMgr apps — review deployment types for migration readiness";
            }
        }
        
        public string ComparisonIcon => ReadyToMigratePercentage > 60 ? "✅" : ReadyToMigratePercentage > 30 ? "📦" : "⚠️";
    }

    /// <summary>
    /// WUfB Ring Coverage comparison — Intune update rings vs WSUS/SUP infrastructure.
    /// Intune: /deviceManagement/deviceConfigurations (WUfB type). ConfigMgr: SMS_UpdateComplianceStatus
    /// </summary>
    public class UpdateRingComparison
    {
        // Intune WUfB metrics
        public int IntuneRingCount { get; set; }
        public int IntuneDevicesInRings { get; set; }
        public double IntuneRingSuccessRate { get; set; }
        public List<UpdateRingSummary> IntuneRings { get; set; } = new();
        
        // ConfigMgr metrics
        public int ConfigMgrDeviceCount { get; set; }
        public double ConfigMgrUpdateComplianceRate { get; set; }
        
        public bool HasIntuneRings => IntuneRingCount > 0;
        
        public string ComparisonSummary
        {
            get
            {
                if (!HasIntuneRings)
                    return "No WUfB update rings configured — updates still coming through WSUS/SUP";
                return $"{IntuneRingCount} WUfB rings covering {IntuneDevicesInRings:N0} devices — updates from Microsoft CDN, no WSUS infrastructure";
            }
        }
        
        public string ConfigMgrSummary => ConfigMgrDeviceCount > 0
            ? $"{ConfigMgrDeviceCount:N0} devices managed through WSUS/SUP — requires on-prem infrastructure"
            : "No ConfigMgr update data";
        
        public string ComparisonIcon => HasIntuneRings ? "🔄" : "⚙️";
    }

    /// <summary>
    /// Summary of a WUfB update ring.
    /// </summary>
    public class UpdateRingSummary
    {
        public string RingName { get; set; } = string.Empty;
        public int DeviceCount { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
    }

    /// <summary>
    /// Autopilot vs Imaging comparison — cloud provisioning vs on-prem task sequences.
    /// Intune: /deviceManagement/windowsAutopilotDeviceIdentities. ConfigMgr: device count
    /// </summary>
    public class AutopilotComparison
    {
        // Intune Autopilot metrics
        public int AutopilotRegisteredCount { get; set; }
        public int AutopilotProfileAssignedCount { get; set; }
        public int AutopilotNotRegisteredCount { get; set; }
        
        // ConfigMgr metrics (limited — task sequences not queryable via Admin Service)
        public int ConfigMgrDeviceCount { get; set; }
        
        public bool HasAutopilotDevices => AutopilotRegisteredCount > 0;
        
        public string ComparisonSummary
        {
            get
            {
                if (!HasAutopilotDevices)
                    return "No devices registered in Autopilot — still dependent on imaging/task sequences";
                return $"{AutopilotRegisteredCount:N0} devices registered for Autopilot — self-provision from anywhere, no imaging required";
            }
        }
        
        public string ComparisonIcon => HasAutopilotDevices ? "🚀" : "⚙️";
    }

    /// <summary>
    /// Security Blind Spots aggregate — combines the 5 questions ConfigMgr can't answer.
    /// All data sourced from existing comparison cards (no new API calls).
    /// </summary>
    public class SecurityBlindSpotsComparison
    {
        public int CompromisedDeviceCount { get; set; } // from ThreatDetection
        public int ActiveMalwareCount { get; set; } // from ActiveMalware
        public int AutoRemediatedCount { get; set; } // from Defender
        public int CAGatedDeviceCount { get; set; } // from ConditionalAccess (Intune side)
        public int HealthAttestedCount { get; set; } // from HealthAttestation
        
        public int ConfigMgrDeviceCount { get; set; }
        public int BlindSpotCount => 5; // Always 5 questions
        
        public string ComparisonSummary => ConfigMgrDeviceCount > 0
            ? $"Your CISO will ask these 5 questions. ConfigMgr can answer 0 of them for {ConfigMgrDeviceCount:N0} devices."
            : "Connect to see security blind spot analysis";
        
        public string ComparisonIcon => "🔴";
    }

    /// <summary>
    /// Work-from-Anywhere management — shows devices online for Intune but dark for ConfigMgr.
    /// Cross-references Intune lastSyncDateTime with ConfigMgr LastPolicyRequest.
    /// </summary>
    public class WorkFromAnywhereComparison
    {
        // Intune freshness
        public int IntuneSyncedLast24h { get; set; }
        public int IntuneTotalDevices { get; set; }
        
        // ConfigMgr freshness
        public int ConfigMgrActiveLast24h { get; set; }
        public int ConfigMgrTotalDevices { get; set; }
        
        // The killer metric: devices online for Intune but dark for ConfigMgr
        public int OnlineForIntuneButDarkForConfigMgr { get; set; }
        
        // ConfigMgr inactive (stale 14+ days)
        public int ConfigMgrInactiveCount { get; set; }
        
        public bool HasData => IntuneTotalDevices > 0;
        
        public string ComparisonSummary
        {
            get
            {
                if (OnlineForIntuneButDarkForConfigMgr > 0)
                    return $"{OnlineForIntuneButDarkForConfigMgr:N0} devices are managed by Intune right now but invisible to ConfigMgr — working from home, hotels, coffee shops";
                if (HasData)
                    return $"Intune reached {IntuneSyncedLast24h:N0} devices in the last 24h — no VPN required";
                return "Connect to see work-from-anywhere analysis";
            }
        }
        
        public string ComparisonIcon => OnlineForIntuneButDarkForConfigMgr > 0 ? "🌍" : "☁️";
    }

    /// <summary>
    /// Compliance Enforcement Loop — shows what happens when devices go non-compliant.
    /// Intune: compliance state + CA enforcement. ConfigMgr: compliance is just a report.
    /// </summary>
    public class ComplianceEnforcementComparison
    {
        // Intune compliance states
        public int IntuneCompliantCount { get; set; }
        public int IntuneNonCompliantCount { get; set; }
        public int IntuneInGracePeriodCount { get; set; }
        public int IntuneTotalDevices { get; set; }
        
        // ConfigMgr
        public int ConfigMgrDeviceCount { get; set; }
        
        public bool HasData => IntuneTotalDevices > 0;
        
        public string ComparisonSummary
        {
            get
            {
                if (IntuneNonCompliantCount > 0 || IntuneInGracePeriodCount > 0)
                {
                    var parts = new List<string>();
                    if (IntuneNonCompliantCount > 0) parts.Add($"{IntuneNonCompliantCount:N0} blocked");
                    if (IntuneInGracePeriodCount > 0) parts.Add($"{IntuneInGracePeriodCount:N0} in grace period");
                    return $"{string.Join(", ", parts)} — non-compliant devices lose access to M365. On ConfigMgr? Non-compliance is just a report.";
                }
                if (HasData)
                    return $"{IntuneCompliantCount:N0} devices compliant and accessing corporate resources. Non-compliant = access revoked automatically.";
                return "Connect to see compliance enforcement data";
            }
        }
        
        public string ComparisonIcon => IntuneNonCompliantCount > 0 ? "🚫" : "✅";
    }

    /// <summary>
    /// Enrollment Velocity comparison — shows how fast devices are enrolling to Intune
    /// vs ConfigMgr imaging/deployment speed.
    /// </summary>
    public class EnrollmentVelocityComparison
    {
        // Intune enrollment velocity
        public int EnrolledThisWeek { get; set; }
        public int EnrolledPreviousWeek { get; set; }
        public int AutopilotRegisteredCount { get; set; }
        public double PeerAverageRate { get; set; }
        public string OrganizationCategory { get; set; } = string.Empty;

        // Provisioning time estimates (industry standard)
        public string AutopilotEstimate { get; set; } = "~30 min self-service";
        public string ConfigMgrImagingEstimate { get; set; } = "~4 hrs IT hands-on";

        public bool HasData => EnrolledThisWeek > 0 || EnrolledPreviousWeek > 0;

        public string WeeklyTrend
        {
            get
            {
                if (EnrolledPreviousWeek == 0) return EnrolledThisWeek > 0 ? "new" : "none";
                double change = ((double)EnrolledThisWeek - EnrolledPreviousWeek) / EnrolledPreviousWeek * 100;
                if (change > 10) return "accelerating";
                if (change < -10) return "slowing";
                return "steady";
            }
        }

        public string TrendArrow
        {
            get => WeeklyTrend switch
            {
                "accelerating" => "📈",
                "slowing" => "📉",
                "new" => "🆕",
                _ => "➡️"
            };
        }

        public string ComparisonSummary
        {
            get
            {
                if (!HasData)
                    return "Connect to see enrollment velocity data";

                var trend = WeeklyTrend switch
                {
                    "accelerating" => $"Enrollment accelerating — {EnrolledThisWeek} this week vs {EnrolledPreviousWeek} last week.",
                    "slowing" => $"Enrollment slowing — {EnrolledThisWeek} this week vs {EnrolledPreviousWeek} last week. Push Autopilot adoption.",
                    "new" => $"{EnrolledThisWeek} devices enrolled this week — momentum building!",
                    "steady" => $"Steady at {EnrolledThisWeek} devices/week.",
                    _ => $"{EnrolledThisWeek} devices enrolled this week."
                };

                if (PeerAverageRate > 0 && EnrolledThisWeek < PeerAverageRate)
                    trend += $" Peer avg: {PeerAverageRate:F0}/week.";

                return trend;
            }
        }

        public string ComparisonIcon => WeeklyTrend == "accelerating" ? "🚀" : WeeklyTrend == "slowing" ? "📉" : "⚡";
    }
}
