using System;

namespace CloudJourneyAddin.Constants
{
    /// <summary>
    /// Centralized terminology constants for consistent Microsoft Entra branding.
    /// Updated to reflect Microsoft's rebranding from "Azure AD" to "Entra".
    /// </summary>
    public static class DeviceJoinTerminology
    {
        /// <summary>
        /// Device joined to both on-premises Active Directory and Microsoft Entra ID
        /// </summary>
        public const string HybridEntraJoined = "Hybrid Entra joined";
        
        /// <summary>
        /// Short form for display in limited space
        /// </summary>
        public const string HybridEntraJoinedShort = "Hybrid Entra";
        
        /// <summary>
        /// Device joined only to Microsoft Entra ID (cloud-only)
        /// </summary>
        public const string EntraJoined = "Entra joined";
        
        /// <summary>
        /// Device joined only to on-premises Active Directory (not cloud-managed)
        /// </summary>
        public const string ActiveDirectoryJoined = "Active Directory joined";
        
        /// <summary>
        /// Short form for AD-only devices
        /// </summary>
        public const string ActiveDirectoryJoinedShort = "AD Domain Only";
        
        /// <summary>
        /// Device not joined to any directory (standalone workgroup device)
        /// </summary>
        public const string WorkgroupDevice = "Workgroup device";
        
        /// <summary>
        /// Short form for workgroup devices
        /// </summary>
        public const string WorkgroupShort = "Workgroup";

        /// <summary>
        /// Get display name for a device join type enum value
        /// </summary>
        public static string GetDisplayName(Models.DeviceJoinType joinType)
        {
            return joinType switch
            {
                Models.DeviceJoinType.HybridAzureADJoined => HybridEntraJoined,
                Models.DeviceJoinType.AzureADOnly => EntraJoined,
                Models.DeviceJoinType.OnPremDomainOnly => ActiveDirectoryJoined,
                Models.DeviceJoinType.WorkgroupOnly => WorkgroupDevice,
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Get short display name for a device join type enum value
        /// </summary>
        public static string GetShortDisplayName(Models.DeviceJoinType joinType)
        {
            return joinType switch
            {
                Models.DeviceJoinType.HybridAzureADJoined => HybridEntraJoinedShort,
                Models.DeviceJoinType.AzureADOnly => EntraJoined,
                Models.DeviceJoinType.OnPremDomainOnly => ActiveDirectoryJoinedShort,
                Models.DeviceJoinType.WorkgroupOnly => WorkgroupShort,
                _ => "Unknown"
            };
        }
    }
}
