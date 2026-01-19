using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroTrustMigrationAddin.Models
{
    /// <summary>
    /// Security and compliance status for a single device from ConfigMgr inventory.
    /// </summary>
    public class DeviceSecurityStatus
    {
        /// <summary>ConfigMgr Resource ID.</summary>
        public int ResourceId { get; set; }

        /// <summary>Device name.</summary>
        public string DeviceName { get; set; } = string.Empty;

        /// <summary>Whether device is already enrolled in Intune.</summary>
        public bool IsEnrolledInIntune { get; set; }

        /// <summary>Whether device is co-managed.</summary>
        public bool IsCoManaged { get; set; }

        // BitLocker Status
        /// <summary>Whether BitLocker is enabled on the OS drive.</summary>
        public bool BitLockerEnabled { get; set; }

        /// <summary>BitLocker protection status (0=Off, 1=On, 2=Unknown).</summary>
        public int BitLockerProtectionStatus { get; set; }

        /// <summary>Encryption method used (AES128, AES256, etc.).</summary>
        public string? EncryptionMethod { get; set; }

        // NOTE: Firewall and Antivirus/Defender properties removed in v3.16.47
        // - SMS_G_System_FIREWALL_PRODUCT doesn't exist in ConfigMgr standard inventory
        // - SMS_G_System_AntimalwareHealthStatus requires Endpoint Protection role
        // Both are enforced by Intune Endpoint Security policies post-enrollment

        // TPM Status
        /// <summary>Whether TPM is present.</summary>
        public bool TpmPresent { get; set; }

        /// <summary>Whether TPM is enabled.</summary>
        public bool TpmEnabled { get; set; }

        /// <summary>Whether TPM is activated.</summary>
        public bool TpmActivated { get; set; }

        /// <summary>TPM specification version.</summary>
        public string? TpmVersion { get; set; }

        // OS Information
        /// <summary>Full OS name and version.</summary>
        public string? OperatingSystem { get; set; }

        /// <summary>OS version string (e.g., "10.0.19045").</summary>
        public string? OSVersion { get; set; }

        /// <summary>OS build number.</summary>
        public string? OSBuild { get; set; }

        /// <summary>Windows 10/11 feature update version (e.g., "22H2").</summary>
        public string? OSFeatureUpdate { get; set; }

        // Secure Boot
        /// <summary>Whether Secure Boot is enabled.</summary>
        public bool SecureBootEnabled { get; set; }

        // Inventory Freshness
        /// <summary>When hardware inventory was last collected.</summary>
        public DateTime? LastHardwareScan { get; set; }

        /// <summary>Days since last hardware scan.</summary>
        public int DaysSinceLastScan => LastHardwareScan.HasValue
            ? (int)(DateTime.Now - LastHardwareScan.Value).TotalDays
            : -1;

        /// <summary>Whether inventory data is considered stale (>7 days).</summary>
        public bool IsInventoryStale => DaysSinceLastScan > 7;
    }

    /// <summary>
    /// Requirements extracted from an Intune compliance policy.
    /// </summary>
    public class CompliancePolicyRequirements
    {
        /// <summary>Policy ID from Graph API.</summary>
        public string PolicyId { get; set; } = string.Empty;

        /// <summary>Display name of the policy.</summary>
        public string PolicyName { get; set; } = string.Empty;

        /// <summary>Description of the policy.</summary>
        public string? Description { get; set; }

        /// <summary>Platform (Windows10, iOS, Android, etc.).</summary>
        public string Platform { get; set; } = "Windows10";

        // Security Requirements
        /// <summary>Whether BitLocker encryption is required.</summary>
        public bool RequiresBitLocker { get; set; }

        // NOTE: RequiresDefender and RequiresFirewall removed in v3.16.47
        // These are enforced by Intune post-enrollment, not checked pre-enrollment

        /// <summary>Whether Secure Boot is required.</summary>
        public bool RequiresSecureBoot { get; set; }

        /// <summary>Whether TPM is required.</summary>
        public bool RequiresTpm { get; set; }

        // NOTE: RequiresRealTimeProtection and RequiresUpToDateSignatures removed in v3.16.47

        // OS Requirements
        /// <summary>Minimum OS version required (e.g., "10.0.19041").</summary>
        public string? MinimumOSVersion { get; set; }

        /// <summary>Maximum OS version allowed.</summary>
        public string? MaximumOSVersion { get; set; }

        /// <summary>Friendly display of OS requirement.</summary>
        public string OSRequirementDisplay => !string.IsNullOrEmpty(MinimumOSVersion)
            ? $"Windows 10 {MinimumOSVersion}+"
            : "None";

        /// <summary>Count of requirements enabled in this policy.</summary>
        public int RequirementCount
        {
            get
            {
                int count = 0;
                if (RequiresBitLocker) count++;
                if (RequiresSecureBoot) count++;
                if (RequiresTpm) count++;
                if (!string.IsNullOrEmpty(MinimumOSVersion)) count++;
                return count;
            }
        }

        /// <summary>Summary of requirements for display.</summary>
        public string RequirementsSummary
        {
            get
            {
                var items = new List<string>();
                if (RequiresBitLocker) items.Add("BitLocker");
                if (RequiresSecureBoot) items.Add("Secure Boot");
                if (RequiresTpm) items.Add("TPM");
                if (!string.IsNullOrEmpty(MinimumOSVersion)) items.Add($"OS {MinimumOSVersion}+");
                return items.Count > 0 ? string.Join(", ", items) : "No requirements";
            }
        }

        // ============================================
        // Assignment Awareness Properties (Phase 1)
        // ============================================

        /// <summary>Whether this policy is actually assigned to any targets.</summary>
        public bool HasAssignments { get; set; }

        /// <summary>Whether the policy is assigned to "All Devices" target.</summary>
        public bool IsAssignedToAllDevices { get; set; }

        /// <summary>Number of groups the policy is assigned to.</summary>
        public int AssignedGroupCount { get; set; }

        /// <summary>Names of groups the policy is assigned to.</summary>
        public List<string> AssignedGroupNames { get; set; } = new();

        /// <summary>Whether assignment filters are present (beta API limitation warning).</summary>
        public bool HasAssignmentFilters { get; set; }

        /// <summary>Human-readable summary of the assignment status.</summary>
        public string AssignmentSummary
        {
            get
            {
                if (!HasAssignments)
                    return "⚠️ Not assigned (policy won't apply)";
                if (IsAssignedToAllDevices)
                    return "✓ Assigned to All Devices";
                if (AssignedGroupCount > 0)
                {
                    var groupList = AssignedGroupNames.Any()
                        ? string.Join(", ", AssignedGroupNames.Take(3))
                        : $"{AssignedGroupCount} group(s)";
                    if (AssignedGroupNames.Count > 3)
                        groupList += $" (+{AssignedGroupNames.Count - 3} more)";
                    return $"✓ Assigned to {groupList}";
                }
                return "Unknown assignment status";
            }
        }

        /// <summary>Whether this policy is effectively active (has assignments).</summary>
        public bool IsEffectivelyActive => HasAssignments;
    }

    /// <summary>
    /// A specific compliance gap identified for a device.
    /// </summary>
    public class ComplianceGap
    {
        /// <summary>Requirement type (BitLocker, Defender, OS Version, etc.).</summary>
        public string Requirement { get; set; } = string.Empty;

        /// <summary>Icon for the requirement.</summary>
        public string Icon { get; set; } = "⚠️";

        /// <summary>Current state of the device.</summary>
        public string CurrentState { get; set; } = string.Empty;

        /// <summary>Required state from policy.</summary>
        public string RequiredState { get; set; } = string.Empty;

        /// <summary>Recommended action to remediate.</summary>
        public string RemediationAction { get; set; } = string.Empty;

        /// <summary>Difficulty of remediation (Low, Medium, High).</summary>
        public string RemediationEffort { get; set; } = "Medium";

        /// <summary>Whether this can be auto-remediated by Intune.</summary>
        public bool CanAutoRemediate { get; set; }

        /// <summary>Additional notes or caveats.</summary>
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Simulation result for a single device.
    /// </summary>
    public class DeviceSimulationResult
    {
        /// <summary>Device resource ID.</summary>
        public int ResourceId { get; set; }

        /// <summary>Device name.</summary>
        public string DeviceName { get; set; } = string.Empty;

        /// <summary>Whether the device would be compliant if enrolled today.</summary>
        public bool WouldBeCompliant { get; set; }

        /// <summary>List of compliance gaps preventing compliance.</summary>
        public List<ComplianceGap> Gaps { get; set; } = new();

        /// <summary>Count of gaps.</summary>
        public int GapCount => Gaps.Count;

        /// <summary>Whether inventory data is stale.</summary>
        public bool HasStaleInventory { get; set; }

        /// <summary>Days since last inventory scan.</summary>
        public int DaysSinceLastScan { get; set; }

        /// <summary>Summary of gaps for display.</summary>
        public string GapSummary => Gaps.Count == 0
            ? "Ready for enrollment"
            : string.Join(", ", Gaps.Select(g => g.Requirement));
    }

    /// <summary>
    /// Aggregated simulation results for a specific gap type.
    /// </summary>
    public class GapSummary
    {
        /// <summary>Requirement type (BitLocker, Defender, etc.).</summary>
        public string Requirement { get; set; } = string.Empty;

        /// <summary>Icon for display.</summary>
        public string Icon { get; set; } = "⚠️";

        /// <summary>Number of devices with this gap.</summary>
        public int DeviceCount { get; set; }

        /// <summary>Percentage of unenrolled devices with this gap.</summary>
        public double Percentage { get; set; }

        /// <summary>Remediation action.</summary>
        public string RemediationAction { get; set; } = string.Empty;

        /// <summary>Remediation effort level.</summary>
        public string RemediationEffort { get; set; } = "Medium";

        /// <summary>Whether Intune can auto-remediate.</summary>
        public bool CanAutoRemediate { get; set; }

        /// <summary>List of affected device names.</summary>
        public List<string> AffectedDevices { get; set; } = new();
    }

    /// <summary>
    /// Complete simulation results for the enrollment impact simulator.
    /// </summary>
    public class EnrollmentSimulationResult
    {
        /// <summary>When the simulation was computed.</summary>
        public DateTime ComputedAt { get; set; } = DateTime.Now;

        // Device Counts
        /// <summary>Total devices in ConfigMgr.</summary>
        public int TotalDevices { get; set; }

        /// <summary>Devices already enrolled in Intune.</summary>
        public int EnrolledDevices { get; set; }

        /// <summary>Devices not yet enrolled (simulation targets).</summary>
        public int UnenrolledDevices { get; set; }

        /// <summary>Devices with stale inventory (excluded from accurate simulation).</summary>
        public int StaleInventoryDevices { get; set; }

        // Current State
        /// <summary>Current compliance rate (enrolled devices only).</summary>
        public double CurrentComplianceRate { get; set; }

        /// <summary>Currently compliant devices.</summary>
        public int CurrentCompliantDevices { get; set; }

        // Simulation Results
        /// <summary>Unenrolled devices that WOULD be compliant if enrolled.</summary>
        public int WouldBeCompliantCount { get; set; }

        /// <summary>Unenrolled devices that WOULD FAIL compliance.</summary>
        public int WouldFailCount { get; set; }

        /// <summary>Percentage of unenrolled devices that would pass.</summary>
        public double WouldPassPercentage => UnenrolledDevices > 0
            ? (double)WouldBeCompliantCount / UnenrolledDevices * 100
            : 0;

        // Projected State
        /// <summary>Projected compliance rate if all devices enrolled (before remediation).</summary>
        public double ProjectedComplianceRate { get; set; }

        /// <summary>Projected compliant devices after enrollment (before remediation).</summary>
        public int ProjectedCompliantDevices { get; set; }

        /// <summary>Projected compliance after remediation (target).</summary>
        public double ProjectedComplianceAfterRemediation { get; set; }

        // Gap Analysis
        /// <summary>Summary of gaps by type.</summary>
        public List<GapSummary> GapSummaries { get; set; } = new();

        /// <summary>Per-device simulation results.</summary>
        public List<DeviceSimulationResult> DeviceResults { get; set; } = new();

        // Policy Information
        /// <summary>Compliance policies used for simulation.</summary>
        public List<CompliancePolicyRequirements> PoliciesUsed { get; set; } = new();

        /// <summary>Primary policy used (if multiple, the most restrictive).</summary>
        public CompliancePolicyRequirements? PrimaryPolicy { get; set; }

        // Data Quality
        /// <summary>Percentage of devices with fresh inventory data.</summary>
        public double DataFreshnessScore { get; set; }

        /// <summary>Warnings about data quality or limitations.</summary>
        public List<string> Warnings { get; set; } = new();

        // Assignment Awareness (Phase 1)
        /// <summary>Whether any used policy has assignment filters (accuracy warning).</summary>
        public bool HasAssignmentFilterWarning { get; set; }

        /// <summary>Count of policies that have no assignments (won't apply).</summary>
        public int UnassignedPolicyCount { get; set; }

        /// <summary>Names of unassigned policies for display.</summary>
        public List<string> UnassignedPolicyNames { get; set; } = new();

        // Computed Properties for UI
        /// <summary>Compliance change description.</summary>
        public string ComplianceChangeDescription
        {
            get
            {
                var change = ProjectedComplianceRate - CurrentComplianceRate;
                if (change > 0)
                    return $"📈 Compliance increases from {CurrentComplianceRate:F1}% to {ProjectedComplianceRate:F1}%";
                else if (change < 0)
                    return $"📉 Compliance initially drops from {CurrentComplianceRate:F1}% to {ProjectedComplianceRate:F1}% (remediation needed)";
                else
                    return $"➡️ Compliance stays at {CurrentComplianceRate:F1}%";
            }
        }

        /// <summary>Total devices needing remediation.</summary>
        public int DevicesNeedingRemediation => WouldFailCount;

        /// <summary>Summary headline for UI.</summary>
        public string Headline
        {
            get
            {
                if (WouldBeCompliantCount == UnenrolledDevices)
                    return $"✅ All {UnenrolledDevices} unenrolled devices are ready for enrollment!";
                else if (WouldFailCount == UnenrolledDevices)
                    return $"⚠️ All {UnenrolledDevices} unenrolled devices need remediation before enrollment.";
                else
                    return $"📊 {WouldBeCompliantCount} of {UnenrolledDevices} devices ready, {WouldFailCount} need remediation.";
            }
        }
    }
}
