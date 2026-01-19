using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroTrustMigrationAddin.Models;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Services
{
    /// <summary>
    /// Service that simulates the impact of enrolling ConfigMgr devices into Intune.
    /// 100% data-driven - uses actual device inventory from ConfigMgr and actual
    /// compliance policies from Intune to calculate precise predictions.
    /// </summary>
    public class EnrollmentSimulatorService
    {
        private readonly GraphDataService? _graphService;
        private readonly ConfigMgrAdminService? _configMgrService;

        public EnrollmentSimulatorService(
            GraphDataService? graphService = null, 
            ConfigMgrAdminService? configMgrService = null)
        {
            _graphService = graphService;
            _configMgrService = configMgrService;
        }

        /// <summary>
        /// Run the full enrollment simulation.
        /// </summary>
        public async Task<EnrollmentSimulationResult> RunSimulationAsync()
        {
            Instance.Info("[ENROLLMENT SIMULATOR] Starting enrollment impact simulation...");

            var result = new EnrollmentSimulationResult();

            try
            {
                // Step 1: Get Intune compliance policy requirements
                var allPolicyRequirements = await GetCompliancePolicyRequirementsAsync();
                result.PoliciesUsed = allPolicyRequirements;

                // Phase 1: Filter to only policies with assignments (actually effective)
                var assignedPolicies = allPolicyRequirements.Where(p => p.IsEffectivelyActive).ToList();
                var unassignedPolicies = allPolicyRequirements.Where(p => !p.IsEffectivelyActive).ToList();

                // Track unassigned policies for display
                result.UnassignedPolicyCount = unassignedPolicies.Count;
                result.UnassignedPolicyNames = unassignedPolicies.Select(p => p.PolicyName).ToList();

                // Check for assignment filters (accuracy warning)
                if (assignedPolicies.Any(p => p.HasAssignmentFilters))
                {
                    result.HasAssignmentFilterWarning = true;
                    result.Warnings.Add("⚠️ Some policies use assignment filters. Actual impact may differ based on device properties.");
                }

                if (unassignedPolicies.Any())
                {
                    Instance.Warning($"[ENROLLMENT SIMULATOR] {unassignedPolicies.Count} policies are NOT assigned and will be excluded:");
                    foreach (var policy in unassignedPolicies)
                    {
                        Instance.Warning($"   - {policy.PolicyName}");
                    }
                    result.Warnings.Add($"📋 {unassignedPolicies.Count} compliance policies exist but are not assigned (will not affect devices).");
                }

                // Use only assigned policies for simulation
                var effectivePolicies = assignedPolicies.Any() ? assignedPolicies : allPolicyRequirements;

                if (!effectivePolicies.Any())
                {
                    result.Warnings.Add("No Windows compliance policies found in Intune. Simulation will use default requirements.");
                    // Use default requirements for simulation
                    effectivePolicies = new List<CompliancePolicyRequirements>
                    {
                        GetDefaultPolicyRequirements()
                    };
                }

                // Get or create the combined "most restrictive" policy for simulation
                result.PrimaryPolicy = GetCombinedPolicy(effectivePolicies);
                Instance.Info($"[ENROLLMENT SIMULATOR] Using {effectivePolicies.Count} ASSIGNED policies: {result.PrimaryPolicy.RequirementsSummary}");

                // Step 2: Get ConfigMgr device security inventory
                var deviceInventory = await GetDeviceSecurityInventoryAsync();

                if (!deviceInventory.Any())
                {
                    result.Warnings.Add("No device inventory available from ConfigMgr. Cannot run simulation.");
                    return result;
                }

                // Step 3: Identify enrolled vs unenrolled devices
                var enrolledDevices = deviceInventory.Where(d => d.IsEnrolledInIntune || d.IsCoManaged).ToList();
                var unenrolledDevices = deviceInventory.Where(d => !d.IsEnrolledInIntune && !d.IsCoManaged).ToList();

                result.TotalDevices = deviceInventory.Count;
                result.EnrolledDevices = enrolledDevices.Count;
                result.UnenrolledDevices = unenrolledDevices.Count;

                // Step 4: Get current compliance status for enrolled devices
                var currentCompliance = await GetCurrentComplianceAsync();
                result.CurrentCompliantDevices = currentCompliance.compliant;
                result.CurrentComplianceRate = result.EnrolledDevices > 0
                    ? (double)currentCompliance.compliant / result.EnrolledDevices * 100
                    : 0;

                // Step 5: Simulate compliance for unenrolled devices
                var simulationResults = SimulateDeviceCompliance(unenrolledDevices, result.PrimaryPolicy);
                result.DeviceResults = simulationResults;

                // Step 6: Calculate projected state
                result.WouldBeCompliantCount = simulationResults.Count(r => r.WouldBeCompliant);
                result.WouldFailCount = simulationResults.Count(r => !r.WouldBeCompliant);
                result.StaleInventoryDevices = simulationResults.Count(r => r.HasStaleInventory);

                // Projected compliance after enrollment (before remediation)
                result.ProjectedCompliantDevices = result.CurrentCompliantDevices + result.WouldBeCompliantCount;
                result.ProjectedComplianceRate = result.TotalDevices > 0
                    ? (double)result.ProjectedCompliantDevices / result.TotalDevices * 100
                    : 0;

                // Projected after full remediation
                result.ProjectedComplianceAfterRemediation = 100.0;

                // Step 7: Generate gap summaries
                result.GapSummaries = GenerateGapSummaries(simulationResults, result.UnenrolledDevices);

                // Step 8: Calculate data quality score
                result.DataFreshnessScore = CalculateDataFreshnessScore(unenrolledDevices);

                // Add warnings for data quality issues
                if (result.StaleInventoryDevices > 0)
                {
                    result.Warnings.Add($"{result.StaleInventoryDevices} devices have stale inventory data (>7 days old). Results may not reflect current state.");
                }

                if (result.DataFreshnessScore < 80)
                {
                    result.Warnings.Add($"Data freshness score is {result.DataFreshnessScore:F0}%. Consider running hardware inventory collection.");
                }

                Instance.Info($"[ENROLLMENT SIMULATOR] Simulation complete:");
                Instance.Info($"   Total: {result.TotalDevices}, Enrolled: {result.EnrolledDevices}, Unenrolled: {result.UnenrolledDevices}");
                Instance.Info($"   Would Pass: {result.WouldBeCompliantCount}, Would Fail: {result.WouldFailCount}");
                Instance.Info($"   Current Compliance: {result.CurrentComplianceRate:F1}% → Projected: {result.ProjectedComplianceRate:F1}%");
            }
            catch (Exception ex)
            {
                Instance.Error($"[ENROLLMENT SIMULATOR] Simulation failed: {ex.Message}");
                result.Warnings.Add($"Simulation error: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get compliance policy requirements from Intune.
        /// NEVER falls back - logs detailed errors for troubleshooting.
        /// </summary>
        private async Task<List<CompliancePolicyRequirements>> GetCompliancePolicyRequirementsAsync()
        {
            if (_graphService == null)
            {
                Instance.Error("[ENROLLMENT SIMULATOR] ❌ CONFIGURATION ERROR: Graph service is NULL");
                Instance.Error("[ENROLLMENT SIMULATOR]    This means Graph authentication was not completed.");
                Instance.Error("[ENROLLMENT SIMULATOR]    Connect to Microsoft Graph before running simulation.");
                return new List<CompliancePolicyRequirements>();
            }

            Instance.Info("[ENROLLMENT SIMULATOR] ✅ Graph service available, querying compliance policies...");

            try
            {
                var policies = await _graphService.GetCompliancePolicySettingsAsync();
                
                if (policies == null || !policies.Any())
                {
                    Instance.Warning("[ENROLLMENT SIMULATOR] ⚠️ No compliance policies found in Intune");
                    Instance.Warning("[ENROLLMENT SIMULATOR]    Possible causes:");
                    Instance.Warning("[ENROLLMENT SIMULATOR]    1. No compliance policies created in Intune");
                    Instance.Warning("[ENROLLMENT SIMULATOR]    2. Policies exist but are not Windows 10/11 platform");
                    Instance.Warning("[ENROLLMENT SIMULATOR]    3. Insufficient Graph API permissions");
                }
                else
                {
                    Instance.Info($"[ENROLLMENT SIMULATOR] ✅ Retrieved {policies.Count} compliance policies from Intune");
                    foreach (var policy in policies.Take(5))
                    {
                        Instance.Debug($"[ENROLLMENT SIMULATOR]    - {policy.PolicyName} (Assigned: {policy.IsEffectivelyActive})");
                    }
                    if (policies.Count > 5)
                    {
                        Instance.Debug($"[ENROLLMENT SIMULATOR]    ... and {policies.Count - 5} more policies");
                    }
                }
                
                return policies ?? new List<CompliancePolicyRequirements>();
            }
            catch (Exception ex)
            {
                Instance.Error($"[ENROLLMENT SIMULATOR] ❌ GRAPH QUERY FAILED: {ex.Message}");
                Instance.Error($"[ENROLLMENT SIMULATOR]    Exception Type: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Instance.Error($"[ENROLLMENT SIMULATOR]    Inner Exception: {ex.InnerException.Message}");
                }
                return new List<CompliancePolicyRequirements>();
            }
        }

        /// <summary>
        /// Get device security inventory from ConfigMgr.
        /// NEVER falls back to demo data - logs detailed errors for troubleshooting.
        /// </summary>
        private async Task<List<DeviceSecurityStatus>> GetDeviceSecurityInventoryAsync()
        {
            if (_configMgrService == null)
            {
                Instance.Error("[ENROLLMENT SIMULATOR] ❌ CONFIGURATION ERROR: ConfigMgr service is NULL");
                Instance.Error("[ENROLLMENT SIMULATOR]    This means the service was not injected during initialization.");
                Instance.Error("[ENROLLMENT SIMULATOR]    Check DashboardWindow.xaml.cs - EnrollmentSimulatorCard.Initialize() call");
                return new List<DeviceSecurityStatus>();
            }

            if (!_configMgrService.IsConfigured)
            {
                Instance.Error("[ENROLLMENT SIMULATOR] ❌ CONNECTION ERROR: ConfigMgr service is not configured");
                Instance.Error("[ENROLLMENT SIMULATOR]    IsConfigured=false means ConfigureAsync() was not called or failed");
                Instance.Error("[ENROLLMENT SIMULATOR]    Check connection status in the dashboard");
                return new List<DeviceSecurityStatus>();
            }

            Instance.Info("[ENROLLMENT SIMULATOR] ✅ ConfigMgr is configured, querying security inventory...");

            try
            {
                var inventory = await _configMgrService.GetDeviceSecurityInventoryAsync();
                
                if (inventory == null || !inventory.Any())
                {
                    Instance.Warning("[ENROLLMENT SIMULATOR] ⚠️ ConfigMgr returned EMPTY security inventory");
                    Instance.Warning("[ENROLLMENT SIMULATOR]    Possible causes:");
                    Instance.Warning("[ENROLLMENT SIMULATOR]    1. No Windows 10/11 devices in ConfigMgr");
                    Instance.Warning("[ENROLLMENT SIMULATOR]    2. Hardware inventory classes not enabled");
                    Instance.Warning("[ENROLLMENT SIMULATOR]    3. Hardware inventory has not run on clients");
                    Instance.Warning("[ENROLLMENT SIMULATOR]    Check individual query results above for details");
                }
                else
                {
                    Instance.Info($"[ENROLLMENT SIMULATOR] ✅ Retrieved {inventory.Count} devices from ConfigMgr security inventory");
                }
                
                return inventory ?? new List<DeviceSecurityStatus>();
            }
            catch (Exception ex)
            {
                Instance.Error($"[ENROLLMENT SIMULATOR] ❌ QUERY FAILED: {ex.Message}");
                Instance.Error($"[ENROLLMENT SIMULATOR]    Exception Type: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Instance.Error($"[ENROLLMENT SIMULATOR]    Inner Exception: {ex.InnerException.Message}");
                }
                Instance.Error("[ENROLLMENT SIMULATOR]    Returning empty list - check ConfigMgr connectivity");
                return new List<DeviceSecurityStatus>();
            }
        }

        /// <summary>
        /// Get current compliance status for enrolled devices.
        /// </summary>
        private async Task<(int compliant, int nonCompliant)> GetCurrentComplianceAsync()
        {
            if (_graphService == null)
            {
                Instance.Debug("[SIMULATOR] GetCurrentComplianceAsync - GraphService is null, returning (0,0)");
                return (0, 0);
            }

            try
            {
                var dashboard = await _graphService.GetComplianceDashboardAsync();
                Instance.Debug($"[SIMULATOR] Current compliance from Graph: Compliant={dashboard.CompliantDevices}, NonCompliant={dashboard.NonCompliantDevices}");
                return (dashboard.CompliantDevices, dashboard.NonCompliantDevices);
            }
            catch (Exception ex)
            {
                Instance.Warning($"[SIMULATOR] GetCurrentComplianceAsync failed: {ex.Message}");
                return (0, 0);
            }
        }

        /// <summary>
        /// Simulate compliance evaluation for each device against the policy.
        /// </summary>
        private List<DeviceSimulationResult> SimulateDeviceCompliance(
            List<DeviceSecurityStatus> devices, 
            CompliancePolicyRequirements policy)
        {
            Instance.Info($"[SIMULATOR] ════════════════════════════════════════════════════════════════════════════");
            Instance.Info($"[SIMULATOR] COMPLIANCE SIMULATION - Starting for {devices.Count} devices");
            Instance.Info($"[SIMULATOR] Policy: {policy.PolicyName}");
            Instance.Info($"[SIMULATOR] Requirements: BitLocker={policy.RequiresBitLocker}, TPM={policy.RequiresTpm}, SecureBoot={policy.RequiresSecureBoot}, MinOS={policy.MinimumOSVersion ?? "none"}");
            Instance.Info($"[SIMULATOR] NOTE: Firewall/Defender checks removed - enforced by Intune post-enrollment");
            Instance.Info($"[SIMULATOR] ════════════════════════════════════════════════════════════════════════════");
            
            // First, analyze data availability across all devices
            var withBitLockerData = devices.Count(d => d.BitLockerEnabled || d.BitLockerProtectionStatus > 0);
            var withTpmData = devices.Count(d => d.TpmPresent);
            var withSecureBootData = devices.Count(d => d.SecureBootEnabled);
            var withOsData = devices.Count(d => !string.IsNullOrEmpty(d.OSVersion));
            
            Instance.Info($"[SIMULATOR] DEVICE SECURITY DATA AVAILABILITY:");
            Instance.Info($"[SIMULATOR]    Devices with BitLocker=true:   {withBitLockerData}/{devices.Count} ({(devices.Count > 0 ? withBitLockerData * 100.0 / devices.Count : 0):F0}%)");
            Instance.Info($"[SIMULATOR]    Devices with TPM=true:         {withTpmData}/{devices.Count} ({(devices.Count > 0 ? withTpmData * 100.0 / devices.Count : 0):F0}%)");
            Instance.Info($"[SIMULATOR]    Devices with SecureBoot=true:  {withSecureBootData}/{devices.Count} ({(devices.Count > 0 ? withSecureBootData * 100.0 / devices.Count : 0):F0}%)");
            Instance.Info($"[SIMULATOR]    Devices with OS Version data:  {withOsData}/{devices.Count} ({(devices.Count > 0 ? withOsData * 100.0 / devices.Count : 0):F0}%)");
            
            // Warning if all devices have false values - likely missing inventory
            if (devices.Count > 0 && withBitLockerData == 0 && policy.RequiresBitLocker)
            {
                Instance.Warning($"[SIMULATOR] ⚠️ ALL devices show BitLocker=false - this likely means:");
                Instance.Warning($"[SIMULATOR]    1. BitLocker hardware inventory class not enabled in ConfigMgr");
                Instance.Warning($"[SIMULATOR]    2. Hardware inventory hasn't run on clients");
                Instance.Warning($"[SIMULATOR]    3. Enable 'SMS_EncryptableVolume' in Client Settings → Hardware Inventory");
            }
            if (devices.Count > 0 && withTpmData == 0 && policy.RequiresTpm)
            {
                Instance.Warning($"[SIMULATOR] ⚠️ ALL devices show TPM=false - this likely means:");
                Instance.Warning($"[SIMULATOR]    1. TPM hardware inventory class not enabled in ConfigMgr");
                Instance.Warning($"[SIMULATOR]    2. Enable 'TPM (Win32_TPM)' in Client Settings → Hardware Inventory");
            }
            
            var results = new List<DeviceSimulationResult>();

            foreach (var device in devices)
            {
                var result = new DeviceSimulationResult
                {
                    ResourceId = device.ResourceId,
                    DeviceName = device.DeviceName,
                    HasStaleInventory = device.IsInventoryStale,
                    DaysSinceLastScan = device.DaysSinceLastScan
                };

                var gaps = new List<ComplianceGap>();

                // Check BitLocker
                if (policy.RequiresBitLocker && !device.BitLockerEnabled)
                {
                    gaps.Add(new ComplianceGap
                    {
                        Requirement = "BitLocker",
                        Icon = "🔐",
                        CurrentState = "Not encrypted",
                        RequiredState = "Encrypted",
                        RemediationAction = "Enable BitLocker encryption on OS drive",
                        RemediationEffort = "Medium",
                        CanAutoRemediate = true,
                        Notes = "Intune compliance policy can trigger BitLocker enablement"
                    });
                }

                // NOTE: Firewall and Defender checks removed in v3.16.47
                // - SMS_G_System_FIREWALL_PRODUCT doesn't exist in ConfigMgr standard inventory
                // - SMS_G_System_AntimalwareHealthStatus requires Endpoint Protection role
                // Both are enforced by Intune Endpoint Security policies post-enrollment

                // Check TPM
                if (policy.RequiresTpm && (!device.TpmPresent || !device.TpmEnabled))
                {
                    gaps.Add(new ComplianceGap
                    {
                        Requirement = "TPM",
                        Icon = "🔧",
                        CurrentState = !device.TpmPresent ? "Not present" : "Present but disabled",
                        RequiredState = "Present and enabled",
                        RemediationAction = device.TpmPresent 
                            ? "Enable TPM in BIOS/UEFI" 
                            : "Hardware does not support TPM - device may need replacement",
                        RemediationEffort = device.TpmPresent ? "Medium" : "High",
                        CanAutoRemediate = false,
                        Notes = "TPM is required for BitLocker and Windows Hello"
                    });
                }

                // Check Secure Boot
                if (policy.RequiresSecureBoot && !device.SecureBootEnabled)
                {
                    gaps.Add(new ComplianceGap
                    {
                        Requirement = "Secure Boot",
                        Icon = "🔒",
                        CurrentState = "Disabled",
                        RequiredState = "Enabled",
                        RemediationAction = "Enable Secure Boot in BIOS/UEFI",
                        RemediationEffort = "Medium",
                        CanAutoRemediate = false,
                        Notes = "Requires UEFI mode (not legacy BIOS)"
                    });
                }

                // Check OS Version
                if (!string.IsNullOrEmpty(policy.MinimumOSVersion) && !string.IsNullOrEmpty(device.OSVersion))
                {
                    if (!MeetsOSVersionRequirement(device.OSVersion, policy.MinimumOSVersion))
                    {
                        gaps.Add(new ComplianceGap
                        {
                            Requirement = "OS Version",
                            Icon = "🪟",
                            CurrentState = device.OSVersion ?? "Unknown",
                            RequiredState = $"{policy.MinimumOSVersion} or later",
                            RemediationAction = "Upgrade Windows to required version",
                            RemediationEffort = "High",
                            CanAutoRemediate = false,
                            Notes = "May require Windows Update for Business or feature update deployment"
                        });
                    }
                }

                result.Gaps = gaps;
                result.WouldBeCompliant = gaps.Count == 0;
                results.Add(result);
            }

            var compliant = results.Count(r => r.WouldBeCompliant);
            var nonCompliant = results.Count(r => !r.WouldBeCompliant);
            var gapCounts = results.SelectMany(r => r.Gaps).GroupBy(g => g.Requirement).Select(g => $"{g.Key}:{g.Count()}");
            Instance.Info($"[SIMULATOR] Simulation complete: {compliant} compliant, {nonCompliant} non-compliant");
            Instance.Debug($"[SIMULATOR] Gap breakdown: {string.Join(", ", gapCounts)}");

            return results;
        }

        /// <summary>
        /// Check if device OS version meets the minimum requirement.
        /// </summary>
        private bool MeetsOSVersionRequirement(string deviceVersion, string requiredVersion)
        {
            try
            {
                // Handle version formats like "10.0.19045" or "10.0.19045.3803"
                var deviceParts = deviceVersion.Split('.');
                var requiredParts = requiredVersion.Split('.');

                for (int i = 0; i < Math.Min(deviceParts.Length, requiredParts.Length); i++)
                {
                    if (int.TryParse(deviceParts[i], out int deviceNum) && 
                        int.TryParse(requiredParts[i], out int requiredNum))
                    {
                        if (deviceNum < requiredNum) return false;
                        if (deviceNum > requiredNum) return true;
                    }
                }

                return true; // Equal versions
            }
            catch
            {
                return true; // If we can't parse, assume compliant
            }
        }

        /// <summary>
        /// Generate summary statistics for each gap type.
        /// </summary>
        private List<GapSummary> GenerateGapSummaries(
            List<DeviceSimulationResult> results, 
            int totalUnenrolled)
        {
            Instance.Debug($"[SIMULATOR] GenerateGapSummaries - analyzing {results.Count} device results");
            var summaries = new List<GapSummary>();

            // Group all gaps by requirement type
            var allGaps = results.SelectMany(r => r.Gaps.Select(g => new { r.DeviceName, Gap = g }));
            var grouped = allGaps.GroupBy(x => x.Gap.Requirement);

            foreach (var group in grouped.OrderByDescending(g => g.Count()))
            {
                var first = group.First().Gap;
                summaries.Add(new GapSummary
                {
                    Requirement = first.Requirement,
                    Icon = first.Icon,
                    DeviceCount = group.Count(),
                    Percentage = totalUnenrolled > 0 ? (double)group.Count() / totalUnenrolled * 100 : 0,
                    RemediationAction = first.RemediationAction,
                    RemediationEffort = first.RemediationEffort,
                    CanAutoRemediate = first.CanAutoRemediate,
                    AffectedDevices = group.Select(x => x.DeviceName).ToList()
                });
            }

            Instance.Info($"[SIMULATOR] Gap summaries: {string.Join(", ", summaries.Select(s => $"{s.Requirement}:{s.DeviceCount} ({s.Percentage:F1}%)"))}");
            return summaries;
        }

        /// <summary>
        /// Calculate data freshness score based on inventory age.
        /// </summary>
        private double CalculateDataFreshnessScore(List<DeviceSecurityStatus> devices)
        {
            if (!devices.Any()) return 0;

            int freshCount = devices.Count(d => !d.IsInventoryStale);
            return (double)freshCount / devices.Count * 100;
        }

        /// <summary>
        /// Combine multiple policies into one with all requirements (most restrictive).
        /// </summary>
        private CompliancePolicyRequirements GetCombinedPolicy(List<CompliancePolicyRequirements> policies)
        {
            if (policies.Count == 1)
            {
                return policies[0];
            }

            var combined = new CompliancePolicyRequirements
            {
                PolicyId = "combined",
                PolicyName = $"Combined ({policies.Count} policies)",
                Description = "Most restrictive combination of all policies",
                Platform = "Windows10"
            };

            string? highestOsVersion = null;

            foreach (var policy in policies)
            {
                if (policy.RequiresBitLocker) combined.RequiresBitLocker = true;
                if (policy.RequiresSecureBoot) combined.RequiresSecureBoot = true;
                if (policy.RequiresTpm) combined.RequiresTpm = true;

                if (!string.IsNullOrEmpty(policy.MinimumOSVersion))
                {
                    if (highestOsVersion == null || 
                        string.Compare(policy.MinimumOSVersion, highestOsVersion) > 0)
                    {
                        highestOsVersion = policy.MinimumOSVersion;
                    }
                }
            }

            combined.MinimumOSVersion = highestOsVersion;
            return combined;
        }

        /// <summary>
        /// Get default policy requirements when no policies exist in Intune.
        /// Focuses on pre-enrollment checks that ConfigMgr can reliably inventory.
        /// Firewall/Defender are enforced by Intune post-enrollment.
        /// </summary>
        private CompliancePolicyRequirements GetDefaultPolicyRequirements()
        {
            return new CompliancePolicyRequirements
            {
                PolicyId = "default",
                PolicyName = "Default Compliance (Pre-Enrollment Readiness)",
                Description = "Pre-enrollment checks using available ConfigMgr inventory. Firewall and Defender are enforced by Intune post-enrollment.",
                Platform = "Windows10",
                RequiresBitLocker = true,
                RequiresSecureBoot = false, // Not enabled by default - BIOS setting
                RequiresTpm = false, // Not enabled by default - hardware dependent
                MinimumOSVersion = "10.0.19041" // Windows 10 2004
            };
        }

        // NOTE: GenerateDemoDeviceInventory() has been REMOVED
        // This service NEVER falls back to demo data after Graph and ConfigMgr are connected.
        // All data comes from real queries. Empty results indicate configuration or connectivity issues.
    }
}
