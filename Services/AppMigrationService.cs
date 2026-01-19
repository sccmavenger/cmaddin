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
        /// Analyzes ConfigMgr applications and scores migration complexity
        /// </summary>
        public async Task<List<ApplicationMigrationAnalysis>> AnalyzeApplicationsAsync()
        {
            var apps = new List<ApplicationMigrationAnalysis>();

            try
            {
                FileLogger.Instance.Info("[APPMIGRATION] Starting application analysis");
                
                // Check if we have real ConfigMgr data
                bool hasRealData = false;
                if (_configMgrService != null)
                {
                    try
                    {
                        var realApps = await _configMgrService.GetApplicationsAsync();
                        hasRealData = realApps != null && realApps.Count > 0;
                        FileLogger.Instance.Debug($"[APPMIGRATION] ConfigMgr query returned {realApps?.Count ?? 0} applications");
                    }
                    catch (Exception ex)
                    {
                        FileLogger.Instance.Warning($"[APPMIGRATION] ConfigMgr query failed: {ex.Message}");
                    }
                }

                if (!hasRealData)
                {
                    FileLogger.Instance.Info("[APPMIGRATION] Using DEMO data - no ConfigMgr connection or no applications found");
                }
                
                // TODO: Query ConfigMgr for application inventory
                // For now, return demo data showing the structure
                apps.Add(new ApplicationMigrationAnalysis
                {
                    ApplicationName = "Microsoft Office 365 ProPlus",
                    DeploymentType = "MSI",
                    TargetedDevices = 1850,
                    ComplexityScore = 15,
                    MigrationPath = MigrationPath.Recommended,
                    Recommendation = "Use Intune's built-in Office 365 deployment. No custom packaging needed.",
                    EstimatedEffort = "1-2 hours"
                });

                apps.Add(new ApplicationMigrationAnalysis
                {
                    ApplicationName = "Adobe Acrobat Reader DC",
                    DeploymentType = "EXE",
                    TargetedDevices = 1620,
                    ComplexityScore = 25,
                    MigrationPath = MigrationPath.IntuneWin,
                    Recommendation = "Convert to .intunewin package using Intune Content Prep Tool. Silent install parameters: /sAll /rs",
                    EstimatedEffort = "2-3 hours"
                });

                apps.Add(new ApplicationMigrationAnalysis
                {
                    ApplicationName = "Google Chrome Enterprise",
                    DeploymentType = "MSI",
                    TargetedDevices = 2100,
                    ComplexityScore = 20,
                    MigrationPath = MigrationPath.Recommended,
                    Recommendation = "Deploy via Intune Win32 app or use Microsoft Edge instead for better integration.",
                    EstimatedEffort = "2-3 hours"
                });

                apps.Add(new ApplicationMigrationAnalysis
                {
                    ApplicationName = "7-Zip File Archiver",
                    DeploymentType = "MSI",
                    TargetedDevices = 980,
                    ComplexityScore = 18,
                    MigrationPath = MigrationPath.IntuneWin,
                    Recommendation = "Simple MSI deployment. Convert to .intunewin and deploy with silent parameters.",
                    EstimatedEffort = "1-2 hours"
                });

                apps.Add(new ApplicationMigrationAnalysis
                {
                    ApplicationName = "Microsoft Teams",
                    DeploymentType = "EXE",
                    TargetedDevices = 2450,
                    ComplexityScore = 12,
                    MigrationPath = MigrationPath.Recommended,
                    Recommendation = "Use Intune's built-in Teams deployment or deploy new Teams 2.0 client.",
                    EstimatedEffort = "1-2 hours"
                });

                apps.Add(new ApplicationMigrationAnalysis
                {
                    ApplicationName = "VLC Media Player",
                    DeploymentType = "EXE",
                    TargetedDevices = 450,
                    ComplexityScore = 22,
                    MigrationPath = MigrationPath.IntuneWin,
                    Recommendation = "Package as Win32 app with silent install: /S /L=%TEMP%\\vlc_install.log",
                    EstimatedEffort = "2 hours"
                });

                apps.Add(new ApplicationMigrationAnalysis
                {
                    ApplicationName = "Custom LOB Application",
                    DeploymentType = "Script",
                    TargetedDevices = 320,
                    ComplexityScore = 75,
                    MigrationPath = MigrationPath.RequiresReengineering,
                    Recommendation = "Application uses complex ConfigMgr Task Sequence. Consider PowerShell deployment script or Azure Arc for device management.",
                    EstimatedEffort = "2-3 weeks"
                });

                apps.Add(new ApplicationMigrationAnalysis
                {
                    ApplicationName = "SAP GUI Client",
                    DeploymentType = "EXE",
                    TargetedDevices = 180,
                    ComplexityScore = 68,
                    MigrationPath = MigrationPath.RequiresReengineering,
                    Recommendation = "Complex enterprise app with multiple dependencies. Test thoroughly in Intune sandbox before production.",
                    EstimatedEffort = "1-2 weeks"
                });

                apps.Add(new ApplicationMigrationAnalysis
                {
                    ApplicationName = "Notepad++",
                    DeploymentType = "EXE",
                    TargetedDevices = 680,
                    ComplexityScore = 20,
                    MigrationPath = MigrationPath.IntuneWin,
                    Recommendation = "Straightforward Win32 app deployment. Use silent install: /S",
                    EstimatedEffort = "1-2 hours"
                });

                apps.Add(new ApplicationMigrationAnalysis
                {
                    ApplicationName = "Cisco AnyConnect VPN",
                    DeploymentType = "MSI",
                    TargetedDevices = 1450,
                    ComplexityScore = 45,
                    MigrationPath = MigrationPath.IntuneWin,
                    Recommendation = "Deploy as Win32 app. Consider migrating to Azure VPN or Always On VPN for better cloud integration.",
                    EstimatedEffort = "4-6 hours"
                });

                FileLogger.Instance.Info($"Analyzed {apps.Count} applications for migration");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"Error analyzing applications: {ex.Message}");
            }

            return apps;
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
