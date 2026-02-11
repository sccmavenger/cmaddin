using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroTrustMigrationAddin.Models
{
    /// <summary>
    /// Tenant license summary with feature availability.
    /// Queried from Microsoft Graph /subscribedSkus endpoint.
    /// </summary>
    public class TenantLicenseSummary
    {
        // Core license counts
        public int TotalLicenses { get; set; }
        public int AssignedLicenses { get; set; }
        public int AvailableLicenses => TotalLicenses - AssignedLicenses;
        
        // Feature availability (derived from service plans)
        public bool HasIntune { get; set; }
        public bool HasIntunePlan2 { get; set; }
        public bool HasIntuneSuite { get; set; }
        public bool HasEntraIdP1 { get; set; }
        public bool HasEntraIdP2 { get; set; }
        public bool HasMDEP1 { get; set; }
        public bool HasMDEP2 { get; set; }
        
        // Derived feature flags
        public bool HasConditionalAccess => HasEntraIdP1 || HasEntraIdP2;
        public bool HasAutopilot => HasIntune;
        public bool HasMDE => HasMDEP1 || HasMDEP2;
        public bool HasAdvancedMDE => HasMDEP2; // P2 has malware counts, remediation tracking
        
        // Specific license inventory
        public List<LicenseInfo> Licenses { get; set; } = new();
        
        // Quick access properties for Intune
        public int IntuneLicensesAvailable { get; set; }
        public int IntuneLicensesAssigned { get; set; }
        public int IntuneLicensesTotal => IntuneLicensesAvailable + IntuneLicensesAssigned;
        
        // Migration readiness indicator
        public LicenseReadiness MigrationReadiness { get; set; } = LicenseReadiness.Unknown;
        
        public DateTime LastRefreshed { get; set; }
        
        /// <summary>
        /// Gets a human-readable summary of available features.
        /// </summary>
        public string FeatureSummary
        {
            get
            {
                var features = new List<string>();
                if (HasIntune) features.Add("Intune");
                if (HasEntraIdP2) features.Add("Entra ID P2");
                else if (HasEntraIdP1) features.Add("Entra ID P1");
                if (HasMDEP2) features.Add("MDE P2");
                else if (HasMDEP1) features.Add("MDE P1");
                if (HasIntuneSuite) features.Add("Intune Suite");
                
                return features.Count > 0 
                    ? string.Join(" | ", features)
                    : "No cloud management licenses detected";
            }
        }
        
        /// <summary>
        /// Gets feature availability icons for UI display.
        /// </summary>
        public string FeatureIcons => 
            $"Intune {(HasIntune ? "✅" : "❌")} | " +
            $"Entra P1 {(HasEntraIdP1 ? "✅" : "❌")} | " +
            $"Entra P2 {(HasEntraIdP2 ? "✅" : "❌")} | " +
            $"MDE {(HasMDE ? "✅" : "❌")}";
        
        /// <summary>
        /// Gets a message explaining missing features.
        /// </summary>
        public string GetMissingFeatureMessage(string feature)
        {
            return feature switch
            {
                "MDE" when !HasMDE => "Microsoft Defender for Endpoint license not detected. Threat visibility requires MDE P1 or P2.",
                "MDE_P2" when !HasMDEP2 => "MDE P2 license not detected. Active malware counts and auto-remediation tracking require MDE P2.",
                "EntraP1" when !HasEntraIdP1 && !HasEntraIdP2 => "Entra ID P1 license not detected. Conditional Access requires Entra ID P1 or P2.",
                "EntraP2" when !HasEntraIdP2 => "Entra ID P2 license not detected. Risk-based Conditional Access requires Entra ID P2.",
                "Intune" when !HasIntune => "Intune license not detected. Device management requires an Intune license.",
                _ => ""
            };
        }
    }

    /// <summary>
    /// Individual license SKU information.
    /// </summary>
    public class LicenseInfo
    {
        public string SkuId { get; set; } = string.Empty;
        public string SkuPartNumber { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string CapabilityStatus { get; set; } = string.Empty;
        public int TotalUnits { get; set; }
        public int ConsumedUnits { get; set; }
        public int AvailableUnits => TotalUnits - ConsumedUnits;
        public List<ServicePlanInfo> ServicePlans { get; set; } = new();
        
        // Convenience properties
        public bool IncludesIntune => ServicePlans.Any(sp => 
            sp.ServicePlanName.Contains("INTUNE", StringComparison.OrdinalIgnoreCase));
        
        public bool IncludesMDE => ServicePlans.Any(sp => 
            sp.ServicePlanName.Contains("DEFENDER_ENDPOINT", StringComparison.OrdinalIgnoreCase) ||
            sp.ServicePlanName.Contains("WINDEFATP", StringComparison.OrdinalIgnoreCase));
        
        public bool IncludesEntraP1orP2 => ServicePlans.Any(sp => 
            sp.ServicePlanName.Contains("AAD_PREMIUM", StringComparison.OrdinalIgnoreCase));
        
        public bool IsEnabled => CapabilityStatus == "Enabled";
        public bool IsSuspended => CapabilityStatus == "Suspended";
        public bool IsWarning => CapabilityStatus == "Warning";
    }

    /// <summary>
    /// Service plan within a license SKU.
    /// </summary>
    public class ServicePlanInfo
    {
        public string ServicePlanId { get; set; } = string.Empty;
        public string ServicePlanName { get; set; } = string.Empty;
        public string ProvisioningStatus { get; set; } = string.Empty;
        
        public bool IsProvisioned => ProvisioningStatus == "Success";
    }

    /// <summary>
    /// Overall license readiness for cloud migration.
    /// </summary>
    public enum LicenseReadiness
    {
        /// <summary>Has Intune + MDE + Entra P1/P2 - fully ready for Zero Trust</summary>
        FullyReady,
        
        /// <summary>Has Intune + Entra P1 - ready for basic cloud management</summary>
        CloudReady,
        
        /// <summary>Has Intune only - basic device management available</summary>
        BasicReady,
        
        /// <summary>Missing Intune licenses - cannot proceed with migration</summary>
        NeedsLicenses,
        
        /// <summary>License status unknown or not queried</summary>
        Unknown
    }

    /// <summary>
    /// Known SKU part numbers and their display names.
    /// </summary>
    public static class LicenseSkuConstants
    {
        // Microsoft 365 bundles
        public const string M365_E5 = "ENTERPRISEPREMIUM";
        public const string M365_E3 = "ENTERPRISEPACK";
        public const string M365_E1 = "STANDARDPACK";
        public const string M365_F3 = "M365_F1";
        public const string M365_BUSINESS_PREMIUM = "SPB";
        public const string M365_BUSINESS_BASIC = "O365_BUSINESS_ESSENTIALS";
        
        // EMS bundles
        public const string EMS_E5 = "EMSPREMIUM";
        public const string EMS_E3 = "EMS";
        
        // Intune standalone
        public const string INTUNE_PLAN1 = "INTUNE_A";
        public const string INTUNE_PLAN2 = "INTUNE_SMB";
        public const string INTUNE_SUITE = "INTUNE_SUITE";
        
        // Entra ID standalone
        public const string ENTRA_P1 = "AAD_PREMIUM";
        public const string ENTRA_P2 = "AAD_PREMIUM_P2";
        
        // MDE standalone
        public const string MDE_P1 = "DEFENDER_ENDPOINT_P1";
        public const string MDE_P2 = "DEFENDER_ENDPOINT_P2";
        
        // Service Plan names (within SKUs)
        public const string SP_INTUNE = "INTUNE_A";
        public const string SP_INTUNE_P2 = "INTUNE_P2";
        public const string SP_AAD_PREMIUM = "AAD_PREMIUM";
        public const string SP_AAD_PREMIUM_P2 = "AAD_PREMIUM_P2";
        public const string SP_MDE_P1 = "DEFENDER_ENDPOINT_P1";
        public const string SP_MDE_P2 = "DEFENDER_ENDPOINT_P2";
        public const string SP_WINDEFATP = "WINDEFATP"; // Legacy MDE service plan name
        
        /// <summary>
        /// Gets a friendly display name for a SKU part number.
        /// </summary>
        public static string GetDisplayName(string? skuPartNumber) => skuPartNumber switch
        {
            M365_E5 => "Microsoft 365 E5",
            M365_E3 => "Microsoft 365 E3",
            M365_E1 => "Microsoft 365 E1",
            M365_F3 => "Microsoft 365 F3",
            M365_BUSINESS_PREMIUM => "Microsoft 365 Business Premium",
            M365_BUSINESS_BASIC => "Microsoft 365 Business Basic",
            EMS_E5 => "Enterprise Mobility + Security E5",
            EMS_E3 => "Enterprise Mobility + Security E3",
            INTUNE_PLAN1 => "Microsoft Intune Plan 1",
            INTUNE_PLAN2 => "Microsoft Intune Plan 2",
            INTUNE_SUITE => "Microsoft Intune Suite",
            ENTRA_P1 => "Microsoft Entra ID P1",
            ENTRA_P2 => "Microsoft Entra ID P2",
            MDE_P1 => "Microsoft Defender for Endpoint P1",
            MDE_P2 => "Microsoft Defender for Endpoint P2",
            _ => skuPartNumber ?? "Unknown License"
        };
    }
}
