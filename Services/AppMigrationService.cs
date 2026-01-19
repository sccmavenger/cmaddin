using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ZeroTrustMigrationAddin.Services
{
    /// <summary>
    /// Phase 2 Feature: Application Migration Intelligence
    /// Analyzes ConfigMgr applications and provides migration guidance for Intune
    /// NEVER uses demo data when ConfigMgr is connected.
    /// </summary>
    public class AppMigrationService
    {
        private readonly ConfigMgrAdminService? _configMgrService;
        private readonly GraphDataService? _graphService;

        public AppMigrationService(ConfigMgrAdminService? configMgrService, GraphDataService? graphService)
        {
            _configMgrService = configMgrService;
            _graphService = graphService;
        }

        /// <summary>
        /// Analyzes ConfigMgr applications and scores migration complexity.
        /// NEVER falls back to demo data - logs detailed errors for troubleshooting.
        /// </summary>
        public async Task<List<ApplicationMigrationAnalysis>> AnalyzeApplicationsAsync()
        {
            var apps = new List<ApplicationMigrationAnalysis>();

            FileLogger.Instance.Info("[APPMIGRATION] ========================================");
            FileLogger.Instance.Info("[APPMIGRATION] Application Migration Analysis - Starting");
            FileLogger.Instance.Info("[APPMIGRATION] ========================================");

            // Check ConfigMgr availability
            if (_configMgrService == null)
            {
                FileLogger.Instance.Error("[APPMIGRATION] ❌ CONFIGURATION ERROR: ConfigMgr service is NULL");
                FileLogger.Instance.Error("[APPMIGRATION]    Cannot analyze applications without ConfigMgr connection");
                FileLogger.Instance.Error("[APPMIGRATION]    Connect to ConfigMgr first to see real application data");
                return apps;
            }

            if (!_configMgrService.IsConfigured)
            {
                FileLogger.Instance.Error("[APPMIGRATION] ❌ CONNECTION ERROR: ConfigMgr is not configured");
                FileLogger.Instance.Error("[APPMIGRATION]    IsConfigured=false - check ConfigMgr connection status");
                return apps;
            }

            FileLogger.Instance.Info("[APPMIGRATION] ✅ ConfigMgr is configured, querying applications...");

            try
            {
                var configMgrApps = await _configMgrService.GetApplicationsAsync();
                
                if (configMgrApps == null || configMgrApps.Count == 0)
                {
                    FileLogger.Instance.Warning("[APPMIGRATION] ⚠️ ConfigMgr returned 0 applications");
                    FileLogger.Instance.Warning("[APPMIGRATION]    Possible causes:");
                    FileLogger.Instance.Warning("[APPMIGRATION]    1. No applications created in ConfigMgr");
                    FileLogger.Instance.Warning("[APPMIGRATION]    2. Query permissions issue");
                    FileLogger.Instance.Warning("[APPMIGRATION]    3. SMS_Application class query failed");
                    return apps;
                }

                FileLogger.Instance.Info($"[APPMIGRATION] ✅ Retrieved {configMgrApps.Count} applications from ConfigMgr");
                FileLogger.Instance.Info("[APPMIGRATION] Analyzing each application for Intune migration...");

                foreach (var configMgrApp in configMgrApps)
                {
                    try
                    {
                        var analysis = AnalyzeApplication(configMgrApp);
                        apps.Add(analysis);
                    }
                    catch (Exception ex)
                    {
                        FileLogger.Instance.Warning($"[APPMIGRATION] Failed to analyze app '{configMgrApp.Name}': {ex.Message}");
                    }
                }

                // Log summary
                var byPath = apps.GroupBy(a => a.MigrationPath);
                FileLogger.Instance.Info("[APPMIGRATION] ----------------------------------------");
                FileLogger.Instance.Info("[APPMIGRATION] Application Analysis Complete:");
                FileLogger.Instance.Info($"[APPMIGRATION]    Total analyzed: {apps.Count}");
                foreach (var group in byPath.OrderBy(g => g.Key))
                {
                    FileLogger.Instance.Info($"[APPMIGRATION]    - {group.Key}: {group.Count()} apps");
                }
                FileLogger.Instance.Info("[APPMIGRATION] ========================================");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"[APPMIGRATION] ❌ QUERY FAILED: {ex.Message}");
                FileLogger.Instance.Error($"[APPMIGRATION]    Exception Type: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    FileLogger.Instance.Error($"[APPMIGRATION]    Inner Exception: {ex.InnerException.Message}");
                }
            }

            return apps;
        }

        /// <summary>
        /// Analyze a single ConfigMgr application for Intune migration
        /// </summary>
        private ApplicationMigrationAnalysis AnalyzeApplication(ConfigMgrApplication configMgrApp)
        {
            var analysis = new ApplicationMigrationAnalysis
            {
                ApplicationName = configMgrApp.Name ?? "Unknown Application",
                DeploymentType = DetermineDeploymentType(configMgrApp),
                TargetedDevices = 0, // Would need deployment data
            };

            // Calculate complexity based on application characteristics
            bool hasCustomScripts = !string.IsNullOrEmpty(configMgrApp.Name) && 
                (configMgrApp.Name.Contains("Script", StringComparison.OrdinalIgnoreCase) ||
                 configMgrApp.Name.Contains("Custom", StringComparison.OrdinalIgnoreCase));
            
            analysis.ComplexityScore = CalculateComplexityScore(
                analysis.DeploymentType,
                hasCustomScripts,
                false, // Can't determine user interaction requirement
                0);    // Can't determine dependency count from basic query

            // Determine migration path and recommendation
            (analysis.MigrationPath, analysis.Recommendation, analysis.EstimatedEffort) = 
                DetermineMigrationStrategy(analysis.ApplicationName, analysis.DeploymentType, analysis.ComplexityScore);

            return analysis;
        }

        /// <summary>
        /// Determine deployment type from application name/metadata
        /// </summary>
        private string DetermineDeploymentType(ConfigMgrApplication app)
        {
            var name = app.Name?.ToLower() ?? "";
            
            // Common patterns
            if (name.Contains("msi")) return "MSI";
            if (name.Contains("appx") || name.Contains("msix")) return "APPX/MSIX";
            if (name.Contains("script") || name.Contains("powershell")) return "Script";
            
            // Default to EXE as most common
            return "EXE";
        }

        /// <summary>
        /// Determine migration strategy based on application characteristics
        /// </summary>
        private (MigrationPath path, string recommendation, string effort) DetermineMigrationStrategy(
            string appName, string deploymentType, int complexity)
        {
            var nameLower = appName.ToLower();

            // Check for apps with built-in Intune support
            if (nameLower.Contains("office") || nameLower.Contains("microsoft 365") || nameLower.Contains("m365"))
            {
                return (MigrationPath.Recommended, 
                    "Use Intune's built-in Microsoft 365 Apps deployment. No custom packaging needed.",
                    "1-2 hours");
            }

            if (nameLower.Contains("teams"))
            {
                return (MigrationPath.Recommended,
                    "Use Intune's built-in Teams deployment or new Teams 2.0 client.",
                    "1-2 hours");
            }

            if (nameLower.Contains("edge"))
            {
                return (MigrationPath.Recommended,
                    "Use Intune's built-in Microsoft Edge deployment.",
                    "30 minutes");
            }

            // Complexity-based recommendations
            if (complexity >= 70)
            {
                return (MigrationPath.RequiresReengineering,
                    "Complex application requiring significant re-engineering for Intune deployment. Consider PowerShell deployment scripts or Azure Arc.",
                    "1-3 weeks");
            }

            if (complexity >= 50)
            {
                return (MigrationPath.IntuneWin,
                    "Moderate complexity. Convert to .intunewin package. Thoroughly test silent install parameters before deployment.",
                    "4-8 hours");
            }

            // Standard apps
            return deploymentType switch
            {
                "MSI" => (MigrationPath.IntuneWin,
                    "Simple MSI deployment. Convert to .intunewin using Intune Content Prep Tool.",
                    "1-2 hours"),
                "APPX/MSIX" => (MigrationPath.Recommended,
                    "Modern package format. Deploy directly through Intune as LOB app.",
                    "1 hour"),
                "Script" => (MigrationPath.IntuneWin,
                    "Convert script-based deployment to Win32 app with PowerShell wrapper.",
                    "2-4 hours"),
                _ => (MigrationPath.IntuneWin,
                    "Package as Win32 app with appropriate silent install parameters.",
                    "2-3 hours")
            };
        }

        /// <summary>
        /// Calculates application migration complexity score (0-100)
        /// Higher score = more complex migration
        /// </summary>
        public int CalculateComplexityScore(string deploymentType, bool hasCustomScripts, 
            bool requiresUserInteraction, int dependencyCount)
        {
            int score = 0;

            // Deployment Type (0-30 points)
            score += deploymentType.ToLower() switch
            {
                "msi" => 10,        // Simple MSI install
                "exe" => 15,        // Needs silent install params
                "appx" => 5,        // Modern app, easy
                "script" => 25,     // Complex scripting
                _ => 30             // Unknown/complex
            };

            // Custom Scripts (0-25 points)
            if (hasCustomScripts) score += 25;

            // User Interaction Required (0-20 points)
            if (requiresUserInteraction) score += 20;

            // Dependencies (0-25 points)
            score += Math.Min(dependencyCount * 5, 25);

            return Math.Min(score, 100);
        }

        /// <summary>
        /// Translates ConfigMgr WQL query to Azure AD Dynamic Group syntax
        /// </summary>
        public string TranslateWQLToAzureAD(string wqlQuery)
        {
            if (string.IsNullOrWhiteSpace(wqlQuery))
                return "No query to translate";

            try
            {
                // Common translations
                var translations = new Dictionary<string, string>
                {
                    { "OperatingSystemNameandVersion like 'Microsoft Windows NT Workstation 10%'", 
                      "(device.deviceOSType -eq \"Windows\") and (device.deviceOSVersion -startsWith \"10.0\")" },
                    { "Netbios_Name0 like 'DESKTOP-%'", 
                      "device.displayName -startsWith \"DESKTOP-\"" },
                    { "ResourceDomainORWorkgroup = 'CONTOSO'", 
                      "device.companyName -eq \"CONTOSO\"" },
                    { "Department0 = 'IT'", 
                      "user.department -eq \"IT\"" },
                };

                // Try exact match first
                if (translations.ContainsKey(wqlQuery))
                    return translations[wqlQuery];

                // Pattern-based translation
                // Example: "Department0 = 'Sales'" → "user.department -eq \"Sales\""
                var deptMatch = Regex.Match(wqlQuery, @"Department0 = '(.+)'");
                if (deptMatch.Success)
                    return $"user.department -eq \"{deptMatch.Groups[1].Value}\"";

                // Default fallback
                return $"⚠️ Manual translation required: {wqlQuery}";
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"Error translating WQL: {ex.Message}");
                return "Translation error";
            }
        }
    }

    /// <summary>
    /// Application migration analysis result
    /// </summary>
    public class ApplicationMigrationAnalysis
    {
        public string ApplicationName { get; set; } = string.Empty;
        public string DeploymentType { get; set; } = string.Empty;
        public int TargetedDevices { get; set; }
        public int ComplexityScore { get; set; } // 0-100, higher = more complex
        public MigrationPath MigrationPath { get; set; }
        public string Recommendation { get; set; } = string.Empty;
        public string EstimatedEffort { get; set; } = string.Empty;
        
        public string ComplexityCategory => ComplexityScore switch
        {
            <= 30 => "Low",
            <= 60 => "Medium",
            _ => "High"
        };

        public string ComplexityColor => ComplexityScore switch
        {
            <= 30 => "#107C10", // Green
            <= 60 => "#FDB813", // Yellow
            _ => "#D13438"      // Red
        };
    }

    /// <summary>
    /// Migration path recommendation
    /// </summary>
    public enum MigrationPath
    {
        Recommended,              // Use Intune's built-in deployment
        IntuneWin,               // Convert to .intunewin package
        Winget,                  // Use Windows Package Manager
        RequiresReengineering,   // Complex, needs redesign
        NotRecommended           // Keep in ConfigMgr/use Azure Arc
    }
}
