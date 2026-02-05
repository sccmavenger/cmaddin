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
}

