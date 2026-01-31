using System.Windows;

namespace ZeroTrustMigrationAddin.Models
{
    /// <summary>
    /// Configuration options for tab visibility in the Dashboard Window.
    /// Allows selective display of tabs based on command-line arguments.
    /// 
    /// SPECIAL BUILD: Only Overview and Enrollment tabs visible by default.
    /// All other tabs hidden unless explicitly enabled via command-line arguments.
    /// </summary>
    public class TabVisibilityOptions
    {
        /// <summary>
        /// Shows or hides the Enrollment tab
        /// </summary>
        public Visibility ShowEnrollmentTab { get; set; } = Visibility.Visible;

        /// <summary>
        /// Shows or hides the Workloads tab
        /// </summary>
        public Visibility ShowWorkloadsTab { get; set; } = Visibility.Collapsed;

        /// <summary>
        /// Shows or hides the Workload Brainstorm tab
        /// </summary>
        public Visibility ShowWorkloadBrainstormTab { get; set; } = Visibility.Collapsed;

        /// <summary>
        /// Shows or hides the Applications tab
        /// </summary>
        public Visibility ShowApplicationsTab { get; set; } = Visibility.Collapsed;

        /// <summary>
        /// Shows or hides the AI Actions tab
        /// </summary>
        public Visibility ShowAIActionsTab { get; set; } = Visibility.Collapsed;

        /// <summary>
        /// Shows or hides the Cloud Readiness Signals tab
        /// </summary>
        public Visibility ShowCloudReadinessTab { get; set; } = Visibility.Visible;

        /// <summary>
        /// Shows or hides the Migration Impact Forecast card on the Overview tab.
        /// Hidden by default per ADR-007: projected values use hardcoded estimates without citable sources.
        /// TODO: Rework to show only current state or use Microsoft-documented benchmarks.
        /// </summary>
        public Visibility ShowMigrationImpactCard { get; set; } = Visibility.Collapsed;

        /// <summary>
        /// Shows or hides the Smart Enrollment Management section on the Enrollment tab.
        /// Hidden by default: Feature needs refinement before broader release.
        /// </summary>
        public Visibility ShowSmartEnrollmentSection { get; set; } = Visibility.Collapsed;

        /// <summary>
        /// Parse command-line arguments to determine tab visibility.
        /// 
        /// Usage examples:
        /// - ZeroTrustMigrationAddin.exe /hidetabs:enrollment,workloads
        /// - ZeroTrustMigrationAddin.exe /showtabs:overview,enrollment
        /// - ZeroTrustMigrationAddin.exe (shows all tabs by default)
        /// </summary>
        public static TabVisibilityOptions ParseArguments(string[] args)
        {
            var options = new TabVisibilityOptions();

            foreach (var arg in args)
            {
                var lowerArg = arg.ToLower();

                // Parse /hidetabs argument
                if (lowerArg.StartsWith("/hidetabs:") || lowerArg.StartsWith("-hidetabs:"))
                {
                    var tabsToHide = lowerArg.Substring(lowerArg.IndexOf(':') + 1).Split(',');
                    foreach (var tab in tabsToHide)
                    {
                        switch (tab.Trim())
                        {
                            case "enrollment":
                                options.ShowEnrollmentTab = Visibility.Collapsed;
                                break;
                            case "workloads":
                                options.ShowWorkloadsTab = Visibility.Collapsed;
                                break;
                            case "workloadbrainstorm":
                            case "brainstorm":
                                options.ShowWorkloadBrainstormTab = Visibility.Collapsed;
                                break;
                            case "applications":
                            case "apps":
                                options.ShowApplicationsTab = Visibility.Collapsed;
                                break;
                            case "aiactions":
                            case "ai":
                                options.ShowAIActionsTab = Visibility.Collapsed;
                                break;
                            case "cloudreadiness":
                            case "readiness":
                                options.ShowCloudReadinessTab = Visibility.Collapsed;
                                break;
                            case "migrationimpact":
                            case "impact":
                                options.ShowMigrationImpactCard = Visibility.Collapsed;
                                break;
                            case "smartenrollment":
                            case "agent":
                                options.ShowSmartEnrollmentSection = Visibility.Collapsed;
                                break;
                        }
                    }
                }

                // Parse /showtabs argument (hides all others)
                if (lowerArg.StartsWith("/showtabs:") || lowerArg.StartsWith("-showtabs:"))
                {
                    // First hide all tabs
                    options.ShowEnrollmentTab = Visibility.Collapsed;
                    options.ShowWorkloadsTab = Visibility.Collapsed;
                    options.ShowWorkloadBrainstormTab = Visibility.Collapsed;
                    options.ShowApplicationsTab = Visibility.Collapsed;
                    options.ShowAIActionsTab = Visibility.Collapsed;
                    options.ShowCloudReadinessTab = Visibility.Collapsed;
                    options.ShowMigrationImpactCard = Visibility.Collapsed;
                    options.ShowSmartEnrollmentSection = Visibility.Collapsed;

                    // Then show only specified tabs
                    var tabsToShow = lowerArg.Substring(lowerArg.IndexOf(':') + 1).Split(',');
                    foreach (var tab in tabsToShow)
                    {
                        switch (tab.Trim())
                        {
                            case "enrollment":
                                options.ShowEnrollmentTab = Visibility.Visible;
                                break;
                            case "workloads":
                                options.ShowWorkloadsTab = Visibility.Visible;
                                break;
                            case "workloadbrainstorm":
                            case "brainstorm":
                                options.ShowWorkloadBrainstormTab = Visibility.Visible;
                                break;
                            case "applications":
                            case "apps":
                                options.ShowApplicationsTab = Visibility.Visible;
                                break;
                            case "aiactions":
                            case "ai":
                                options.ShowAIActionsTab = Visibility.Visible;
                                break;
                            case "cloudreadiness":
                            case "readiness":
                                options.ShowCloudReadinessTab = Visibility.Visible;
                                break;
                            case "migrationimpact":
                            case "impact":
                                options.ShowMigrationImpactCard = Visibility.Visible;
                                break;
                            case "smartenrollment":
                            case "agent":
                                options.ShowSmartEnrollmentSection = Visibility.Visible;
                                break;
                        }
                    }
                }
            }

            return options;
        }
    }

    /// <summary>
    /// Device identity join type - determines enrollment eligibility
    /// </summary>
    public enum DeviceJoinType
    {
        Unknown,              // Cannot determine (missing data)
        WorkgroupOnly,        // Not domain or AAD joined (BLOCKER)
        OnPremDomainOnly,     // AD joined, not AAD joined (BLOCKER - needs Hybrid AAD Join)
        HybridAzureADJoined,  // Both AD + AAD joined (READY for co-management)
        AzureADOnly           // Pure cloud AAD joined (READY for Intune-only)
    }
}
