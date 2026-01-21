using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

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
}
