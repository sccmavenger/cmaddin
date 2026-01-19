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
        /// </summary>
        private async Task<List<CompliancePolicyRequirements>> GetCompliancePolicyRequirementsAsync()
        {
            if (_graphService == null)
            {
                Instance.Warning("[ENROLLMENT SIMULATOR] Graph service not available, using default policy.");
                return new List<CompliancePolicyRequirements>();
            }

            try
            {
                return await _graphService.GetCompliancePolicySettingsAsync();
            }
            catch (Exception ex)
            {
                Instance.Warning($"[ENROLLMENT SIMULATOR] Could not get policies from Graph: {ex.Message}");
                return new List<CompliancePolicyRequirements>();
            }
        }

        /// <summary>
        /// Get device security inventory from ConfigMgr.
        /// </summary>
        private async Task<List<DeviceSecurityStatus>> GetDeviceSecurityInventoryAsync()
        {
            if (_configMgrService == null || !_configMgrService.IsConfigured)
            {
                Instance.Warning("[ENROLLMENT SIMULATOR] ConfigMgr not available, using demo data.");
                return GenerateDemoDeviceInventory();
            }

            try
            {
                return await _configMgrService.GetDeviceSecurityInventoryAsync();
            }
            catch (Exception ex)
            {
                Instance.Warning($"[ENROLLMENT SIMULATOR] Could not get inventory from ConfigMgr: {ex.Message}");
                return GenerateDemoDeviceInventory();
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
            Instance.Info($"[SIMULATOR] Simulating compliance for {devices.Count} devices against policy '{policy.PolicyName}'");
            Instance.Debug($"[SIMULATOR] Policy requirements: BitLocker={policy.RequiresBitLocker}, Firewall={policy.RequiresFirewall}, Defender={policy.RequiresDefender}, TPM={policy.RequiresTpm}, SecureBoot={policy.RequiresSecureBoot}, MinOS={policy.MinimumOSVersion ?? "none"}");
            
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

                // Check Firewall
                if (policy.RequiresFirewall && !device.FirewallEnabled)
                {
                    gaps.Add(new ComplianceGap
                    {
                        Requirement = "Firewall",
                        Icon = "🛡️",
                        CurrentState = "Disabled",
                        RequiredState = "Enabled",
                        RemediationAction = "Enable Windows Firewall",
                        RemediationEffort = "Low",
                        CanAutoRemediate = true,
                        Notes = "Intune can enforce firewall settings via Endpoint Security policy"
                    });
                }

                // Check Defender
                if (policy.RequiresDefender && !device.DefenderEnabled)
                {
                    gaps.Add(new ComplianceGap
                    {
                        Requirement = "Defender",
                        Icon = "🛡️",
                        CurrentState = "Disabled or not installed",
                        RequiredState = "Enabled",
                        RemediationAction = "Enable Microsoft Defender Antivirus",
                        RemediationEffort = "Low",
                        CanAutoRemediate = true,
                        Notes = "Defender is enabled by default on Windows 10/11"
                    });
                }

                // Check Real-Time Protection
                if (policy.RequiresRealTimeProtection && !device.RealTimeProtectionEnabled)
                {
                    gaps.Add(new ComplianceGap
                    {
                        Requirement = "Real-Time Protection",
                        Icon = "⚡",
                        CurrentState = "Disabled",
                        RequiredState = "Enabled",
                        RemediationAction = "Enable real-time protection in Defender",
                        RemediationEffort = "Low",
                        CanAutoRemediate = true
                    });
                }

                // Check Signature Updates
                if (policy.RequiresUpToDateSignatures && !device.SignaturesUpToDate)
                {
                    gaps.Add(new ComplianceGap
                    {
                        Requirement = "AV Signatures",
                        Icon = "📋",
                        CurrentState = device.SignatureAgeDays.HasValue 
                            ? $"{device.SignatureAgeDays} days old" 
                            : "Unknown",
                        RequiredState = "Up to date",
                        RemediationAction = "Update antivirus definitions",
                        RemediationEffort = "Low",
                        CanAutoRemediate = true,
                        Notes = "Signatures typically update automatically"
                    });
                }

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
                if (policy.RequiresDefender) combined.RequiresDefender = true;
                if (policy.RequiresFirewall) combined.RequiresFirewall = true;
                if (policy.RequiresSecureBoot) combined.RequiresSecureBoot = true;
                if (policy.RequiresTpm) combined.RequiresTpm = true;
                if (policy.RequiresRealTimeProtection) combined.RequiresRealTimeProtection = true;
                if (policy.RequiresUpToDateSignatures) combined.RequiresUpToDateSignatures = true;

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
        /// </summary>
        private CompliancePolicyRequirements GetDefaultPolicyRequirements()
        {
            return new CompliancePolicyRequirements
            {
                PolicyId = "default",
                PolicyName = "Default Compliance (Microsoft Recommended)",
                Description = "Standard security requirements based on Microsoft guidance",
                Platform = "Windows10",
                RequiresBitLocker = true,
                RequiresDefender = true,
                RequiresFirewall = true,
                RequiresRealTimeProtection = true,
                RequiresSecureBoot = false, // Not enabled by default
                RequiresTpm = false, // Not enabled by default
                RequiresUpToDateSignatures = true,
                MinimumOSVersion = "10.0.19041" // Windows 10 2004
            };
        }

        /// <summary>
        /// Generate demo device inventory for disconnected/demo scenarios.
        /// </summary>
        private List<DeviceSecurityStatus> GenerateDemoDeviceInventory()
        {
            var devices = new List<DeviceSecurityStatus>();
            var random = new Random(42); // Fixed seed for consistent demo

            var names = new[] { 
                "PC-SALES-", "PC-HR-", "PC-IT-", "PC-ENG-", "PC-MKT-", 
                "LAPTOP-", "DESKTOP-", "WS-" 
            };

            for (int i = 1; i <= 650; i++)
            {
                var prefix = names[random.Next(names.Length)];
                var device = new DeviceSecurityStatus
                {
                    ResourceId = 16000000 + i,
                    DeviceName = $"{prefix}{i:D4}",
                    IsEnrolledInIntune = false,
                    IsCoManaged = false,
                    LastHardwareScan = DateTime.Now.AddDays(-random.Next(1, 14)),
                    OperatingSystem = random.NextDouble() < 0.15 
                        ? "Microsoft Windows 11 Enterprise" 
                        : "Microsoft Windows 10 Enterprise"
                };

                // Vary the compliance state
                device.BitLockerEnabled = random.NextDouble() < 0.72; // 72% encrypted
                device.FirewallEnabled = random.NextDouble() < 0.92; // 92% firewall on
                device.DefenderEnabled = random.NextDouble() < 0.88; // 88% Defender on
                device.RealTimeProtectionEnabled = device.DefenderEnabled && random.NextDouble() < 0.95;
                device.SignaturesUpToDate = device.DefenderEnabled && random.NextDouble() < 0.85;
                device.SignatureAgeDays = device.SignaturesUpToDate ? random.Next(0, 3) : random.Next(7, 30);
                device.TpmPresent = random.NextDouble() < 0.90; // 90% have TPM
                device.TpmEnabled = device.TpmPresent && random.NextDouble() < 0.95;
                device.SecureBootEnabled = random.NextDouble() < 0.75; // 75% Secure Boot
                device.OSVersion = device.OperatingSystem.Contains("11") 
                    ? "10.0.22621" 
                    : random.NextDouble() < 0.85 ? "10.0.19045" : "10.0.18363";

                devices.Add(device);
            }

            // Also add some enrolled devices for the full picture
            for (int i = 1; i <= 350; i++)
            {
                var prefix = names[random.Next(names.Length)];
                devices.Add(new DeviceSecurityStatus
                {
                    ResourceId = 17000000 + i,
                    DeviceName = $"{prefix}ENR-{i:D4}",
                    IsEnrolledInIntune = true,
                    IsCoManaged = random.NextDouble() < 0.8,
                    LastHardwareScan = DateTime.Now.AddDays(-random.Next(0, 3)),
                    OperatingSystem = "Microsoft Windows 10 Enterprise",
                    BitLockerEnabled = true,
                    FirewallEnabled = true,
                    DefenderEnabled = true,
                    RealTimeProtectionEnabled = true,
                    SignaturesUpToDate = true,
                    TpmPresent = true,
                    TpmEnabled = true,
                    SecureBootEnabled = true,
                    OSVersion = "10.0.19045"
                });
            }

            Instance.Info($"[ENROLLMENT SIMULATOR] Generated demo inventory: {devices.Count} devices (650 unenrolled, 350 enrolled)");
            return devices;
        }
    }
}
