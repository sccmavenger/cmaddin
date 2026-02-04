using System;
using System.Collections.Generic;

namespace ZeroTrustMigrationAddin.Models
{
    /// <summary>
    /// Application Readiness Assessment models for evaluating ConfigMgr application
    /// migration complexity to Intune/cloud-native management.
    /// v3.17.100 - Application Readiness feature
    /// </summary>

    /// <summary>
    /// Migration complexity levels for applications based on deployment technology.
    /// </summary>
    public enum MigrationComplexity
    {
        /// <summary>MSI or MSIX - straightforward migration to Intune Win32 or Store app</summary>
        Easy,
        
        /// <summary>MSI custom/LOB apps - package as Win32 app using Content Prep Tool</summary>
        Moderate,
        
        /// <summary>Script-based installers - review logic before migration</summary>
        NeedsReview,
        
        /// <summary>App-V packages - requires repackaging to Win32 or MSIX</summary>
        Complex
    }

    /// <summary>
    /// Deployment type information from ConfigMgr SMS_DeploymentType.
    /// Contains the Technology property which determines migration complexity.
    /// </summary>
    public class ConfigMgrDeploymentType
    {
        /// <summary>Display name of the deployment type</summary>
        public string LocalizedDisplayName { get; set; } = string.Empty;
        
        /// <summary>
        /// Technology/installer type. Values include:
        /// - MSI: Windows Installer
        /// - Script: Script-based installer (PowerShell, batch, etc.)
        /// - AppV5X: App-V 5.x virtual package
        /// - MSIX: Modern MSIX package
        /// - Windows8AppInstaller: Windows Store app
        /// - WebApp: Web application link
        /// - DeepLink: Deep link to Microsoft Store
        /// </summary>
        public string Technology { get; set; } = string.Empty;
        
        /// <summary>Parent application model name (links to SMS_Application)</summary>
        public string AppModelName { get; set; } = string.Empty;
        
        /// <summary>CI_UniqueID from SMS_Application for correlation</summary>
        public string? CI_UniqueID { get; set; }
        
        /// <summary>Priority order of deployment types for the application</summary>
        public int Priority { get; set; }
        
        /// <summary>Whether this deployment type is enabled</summary>
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// Application migration assessment result combining app info with deployment type analysis.
    /// </summary>
    public class AppMigrationAssessment
    {
        /// <summary>Application name from ConfigMgr</summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>Publisher/vendor of the application</summary>
        public string Publisher { get; set; } = string.Empty;
        
        /// <summary>Application version</summary>
        public string Version { get; set; } = string.Empty;
        
        /// <summary>Primary deployment technology (from highest priority deployment type)</summary>
        public string Technology { get; set; } = string.Empty;
        
        /// <summary>All deployment types for this application</summary>
        public List<ConfigMgrDeploymentType> DeploymentTypes { get; set; } = new();
        
        /// <summary>Calculated migration complexity based on deployment technology</summary>
        public MigrationComplexity Complexity { get; set; }
        
        /// <summary>Recommended migration path based on technology</summary>
        public string RecommendedPath { get; set; } = string.Empty;
        
        /// <summary>Link to Microsoft Learn documentation for migration guidance</summary>
        public string MigrationGuideUrl { get; set; } = string.Empty;
        
        /// <summary>Whether this app is currently deployed to devices</summary>
        public bool IsDeployed { get; set; }
        
        /// <summary>Number of deployment types (indicates complexity)</summary>
        public int DeploymentTypeCount { get; set; }
        
        /// <summary>
        /// Complexity display icon for UI
        /// </summary>
        public string ComplexityIcon => Complexity switch
        {
            MigrationComplexity.Easy => "✅",
            MigrationComplexity.Moderate => "🔵",
            MigrationComplexity.NeedsReview => "🟡",
            MigrationComplexity.Complex => "🔴",
            _ => "❓"
        };
        
        /// <summary>
        /// Complexity display text for UI
        /// </summary>
        public string ComplexityText => Complexity switch
        {
            MigrationComplexity.Easy => "Easy",
            MigrationComplexity.Moderate => "Moderate",
            MigrationComplexity.NeedsReview => "Needs Review",
            MigrationComplexity.Complex => "Complex",
            _ => "Unknown"
        };
        
        /// <summary>
        /// Complexity display color for UI
        /// </summary>
        public string ComplexityColor => Complexity switch
        {
            MigrationComplexity.Easy => "#107C10",      // Green
            MigrationComplexity.Moderate => "#0078D4",  // Blue
            MigrationComplexity.NeedsReview => "#FFB900", // Yellow
            MigrationComplexity.Complex => "#D13438",   // Red
            _ => "#666666"
        };
    }

    /// <summary>
    /// Summary of application readiness assessment for dashboard display.
    /// </summary>
    public class ApplicationReadinessSummary
    {
        /// <summary>Total applications assessed</summary>
        public int TotalApps { get; set; }
        
        /// <summary>Apps deployed to devices (active apps)</summary>
        public int DeployedApps { get; set; }
        
        /// <summary>Apps with Easy migration complexity</summary>
        public int EasyApps { get; set; }
        
        /// <summary>Apps with Moderate migration complexity</summary>
        public int ModerateApps { get; set; }
        
        /// <summary>Apps requiring review (script-based)</summary>
        public int NeedsReviewApps { get; set; }
        
        /// <summary>Apps with Complex migration (App-V)</summary>
        public int ComplexApps { get; set; }
        
        /// <summary>Apps where deployment type technology couldn't be determined</summary>
        public int UnknownApps { get; set; }
        
        /// <summary>
        /// Ready apps = Easy + Moderate (have clear migration paths)
        /// </summary>
        public int ReadyApps => EasyApps + ModerateApps;
        
        /// <summary>
        /// Apps that need attention = NeedsReview + Complex + Unknown
        /// </summary>
        public int BlockedApps => NeedsReviewApps + ComplexApps + UnknownApps;
        
        /// <summary>
        /// Readiness percentage (ready apps / total deployed apps)
        /// </summary>
        public double ReadinessPercentage => DeployedApps > 0 
            ? Math.Round((double)ReadyApps / DeployedApps * 100, 1) 
            : 0;
        
        /// <summary>
        /// Technology breakdown for charting
        /// </summary>
        public Dictionary<string, int> TechnologyBreakdown { get; set; } = new();
        
        /// <summary>
        /// Detailed list of all assessed applications
        /// </summary>
        public List<AppMigrationAssessment> Assessments { get; set; } = new();
    }
}
