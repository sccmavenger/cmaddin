using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroTrustMigrationAddin.Models;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Services
{
    /// <summary>
    /// Service for assessing device readiness for cloud migration scenarios.
    /// Provides readiness signals for Autopilot, Windows 11, Cloud-Native, WUfB, and more.
    /// v3.17.0 - Cloud Readiness Signals feature
    /// </summary>
    public class CloudReadinessService
    {
        private readonly ConfigMgrAdminService _configMgrService;
        private readonly GraphDataService _graphService;

        public CloudReadinessService(ConfigMgrAdminService configMgrService, GraphDataService graphService)
        {
            _configMgrService = configMgrService;
            _graphService = graphService;
        }

        /// <summary>
        /// Helper to calculate blocker percentage, capped at 100% to handle data source mismatches.
        /// </summary>
        private static double SafeBlockerPercentage(int affectedCount, int totalDevices)
        {
            if (totalDevices <= 0) return 0;
            var capped = Math.Min(affectedCount, totalDevices);
            return Math.Round((double)capped / totalDevices * 100, 1);
        }

        /// <summary>
        /// Helper to cap ReadyDevices to TotalDevices to prevent impossible displays like "83 of 2 ready".
        /// Logs a warning when data sources appear mismatched.
        /// </summary>
        private static int SafeReadyDevices(int readyCount, int totalDevices, string signalName)
        {
            if (readyCount > totalDevices && totalDevices > 0)
            {
                Instance.Warning($"   ⚠️ [{signalName}] Data source mismatch: ReadyDevices ({readyCount}) > TotalDevices ({totalDevices}). Capping to {totalDevices}.");
                return totalDevices;
            }
            return Math.Max(0, readyCount);
        }

        /// <summary>
        /// Gets the complete Cloud Readiness Dashboard with all signals.
        /// </summary>
        public async Task<CloudReadinessDashboard> GetCloudReadinessDashboardAsync()
        {
            Instance.Info("╔══════════════════════════════════════════════════════════════════════════════════════════╗");
            Instance.Info("║                       CLOUD READINESS ASSESSMENT START                                   ║");
            Instance.Info("╚══════════════════════════════════════════════════════════════════════════════════════════╝");
            
            var dashboard = new CloudReadinessDashboard
            {
                LastRefreshed = DateTime.Now
            };

            try
            {
                // Run all assessments in parallel for better performance
                // NOTE: Windows 11, Identity, WUfB, Endpoint Security hidden per Rob's feedback (2026-01-29)
                // NOTE: Autopatch hidden (2026-02-03) - better suited for AI Recommendations
                var autopilotTask = GetAutopilotReadinessSignalAsync();
                // var windows11Task = GetWindows11ReadinessSignalAsync(); // Hidden - not part of cloud-native readiness
                var cloudNativeTask = GetCloudNativeReadinessSignalAsync();
                var applicationTask = GetApplicationReadinessSignalAsync(); // v3.17.100 - Application Readiness
                // var autopatchTask = GetAutopatchReadinessSignalAsync(); // Hidden - requires Intune enrollment first, better for AI Recommendations
                // var identityTask = GetIdentityReadinessSignalAsync(); // Hidden per Rob's feedback
                // var wufbTask = GetWufbReadinessSignalAsync(); // Hidden per Rob's feedback
                // var endpointSecurityTask = GetEndpointSecurityReadinessSignalAsync(); // Hidden per Rob's feedback

                await Task.WhenAll(autopilotTask, cloudNativeTask, applicationTask);

                dashboard.Signals.Add(await autopilotTask);
                // dashboard.Signals.Add(await windows11Task); // Hidden per Rob's feedback
                dashboard.Signals.Add(await cloudNativeTask);
                dashboard.Signals.Add(await applicationTask); // v3.17.100 - Application Readiness
                // dashboard.Signals.Add(await autopatchTask); // Hidden - requires Intune enrollment first, better for AI Recommendations
                // dashboard.Signals.Add(await identityTask); // Hidden per Rob's feedback
                // dashboard.Signals.Add(await wufbTask); // Hidden per Rob's feedback
                // dashboard.Signals.Add(await endpointSecurityTask); // Hidden per Rob's feedback

                Instance.Info("╔══════════════════════════════════════════════════════════════════════════════════════════╗");
                Instance.Info("║                       CLOUD READINESS ASSESSMENT SUMMARY                                 ║");
                Instance.Info("╚══════════════════════════════════════════════════════════════════════════════════════════╝");
                Instance.Info($"   📊 Overall Readiness Score: {dashboard.OverallReadiness}%");
                Instance.Info($"   📱 Total Devices Assessed: {dashboard.TotalAssessedDevices}");
                Instance.Info($"   🚫 Total Blockers Identified: {dashboard.TotalBlockersIdentified}");
                Instance.Info("");
                Instance.Info("   SIGNAL BREAKDOWN:");
                foreach (var sig in dashboard.Signals)
                {
                    var status = sig.ReadinessPercentage >= 80 ? "✅" : sig.ReadinessPercentage >= 50 ? "🟡" : "🔴";
                    Instance.Info($"      {status} {sig.Name}: {sig.ReadinessPercentage}% ({sig.ReadyDevices}/{sig.TotalDevices} ready)");
                    if (sig.TopBlockers.Any())
                    {
                        foreach (var blocker in sig.TopBlockers.Take(3))
                        {
                            Instance.Info($"         └─ 🚫 {blocker.Name}: {blocker.AffectedDeviceCount} devices ({blocker.PercentageAffected}%)");
                        }
                    }
                }
                Instance.Info("═══════════════════════════════════════════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Instance.Error($"Cloud Readiness Assessment failed: {ex.Message}");
            }

            return dashboard;
        }

        /// <summary>
        /// Assesses Autopilot Registration status.
        /// Shows ConfigMgr devices NOT yet registered to Windows Autopilot.
        /// NOTE: TPM 2.0 is NOT required for Autopilot registration (it's required for Windows 11/BitLocker/Hello).
        /// Requirements: Windows 10 1809+, Entra ID/Hybrid joined, Hardware hash captured
        /// </summary>
        public async Task<CloudReadinessSignal> GetAutopilotReadinessSignalAsync()
        {
            Instance.Info("┌─────────────────────────────────────────────────────────────────────────────────────────┐");
            Instance.Info("│ 🚀 AUTOPILOT REGISTRATION STATUS                                                        │");
            Instance.Info("└─────────────────────────────────────────────────────────────────────────────────────────┘");
            
            var signal = new CloudReadinessSignal
            {
                Id = "autopilot",
                Name = "Autopilot Readiness",
                Description = "Ready for Windows Autopilot deployment",
                Icon = "🚀",
                RelatedWorkload = "Device Provisioning",
                LearnMoreUrl = "https://learn.microsoft.com/mem/autopilot/windows-autopilot"
            };

            try
            {
                // Get device data from ConfigMgr AND Autopilot
                Instance.Info("   Fetching device data from ConfigMgr...");
                var configMgrDevices = await _configMgrService.GetWindows1011DevicesAsync();
                var osDetails = await _configMgrService.GetOSDetailsAsync();
                var enrollmentData = await _graphService.GetDeviceEnrollmentAsync();
                
                Instance.Info("   Fetching Autopilot registered devices from Graph API...");
                var autopilotDevices = await _graphService.GetAutopilotDeviceStatusAsync();
                
                // Get Intune devices to cross-reference serial numbers with Autopilot
                Instance.Info("   Fetching Intune devices to identify Autopilot-registered devices by name...");
                var intuneDevices = await _graphService.GetCachedManagedDevicesAsync();

                var configMgrCount = configMgrDevices?.Count ?? 0;
                var autopilotCount = autopilotDevices?.Count ?? 0;
                
                Instance.Info($"   📱 ConfigMgr Windows 10/11 devices: {configMgrCount}");
                Instance.Info($"   🚀 Autopilot registered devices: {autopilotCount}");
                Instance.Info($"   📊 OS detail records retrieved: {osDetails?.Count ?? 0}");
                Instance.Info($"   💻 Intune managed devices: {intuneDevices?.Count ?? 0}");
                
                // TotalDevices = ConfigMgr devices (these are the ones we want to register)
                signal.TotalDevices = configMgrCount;
                
                if (signal.TotalDevices == 0)
                {
                    Instance.Warning("   ⚠️ No ConfigMgr devices found for Autopilot registration assessment");
                    return signal;
                }

                var blockers = new List<ReadinessBlocker>();

                // Build a set of Autopilot serial numbers (case-insensitive)
                var autopilotSerials = new HashSet<string>(
                    autopilotDevices?.Select(a => a.SerialNumber?.ToUpperInvariant() ?? "").Where(s => !string.IsNullOrEmpty(s)) ?? new List<string>(),
                    StringComparer.OrdinalIgnoreCase);
                
                Instance.Info($"   📋 Autopilot serial numbers found: {autopilotSerials.Count}");
                
                // Find Intune devices whose serial numbers ARE in Autopilot (these are registered)
                var intuneDevicesWithAutopilot = intuneDevices?
                    .Where(d => !string.IsNullOrEmpty(d.SerialNumber) && autopilotSerials.Contains(d.SerialNumber.ToUpperInvariant()))
                    .Select(d => d.DeviceName ?? "")
                    .Where(n => !string.IsNullOrEmpty(n))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                
                Instance.Info($"   ✅ Intune devices with Autopilot registration: {intuneDevicesWithAutopilot.Count}");
                
                // Find ConfigMgr devices NOT in the Autopilot-registered set
                var safeConfigMgrDevices = configMgrDevices ?? new List<ConfigMgrDevice>();
                var configMgrDevicesNotInAutopilot = safeConfigMgrDevices
                    .Where(d => !string.IsNullOrEmpty(d.Name) && !intuneDevicesWithAutopilot.Contains(d.Name))
                    .ToList();
                
                var devicesNotRegistered = configMgrDevicesNotInAutopilot.Count;
                var devicesRegistered = configMgrCount - devicesNotRegistered;
                
                Instance.Info("");
                Instance.Info("   [CHECK 1/2] AUTOPILOT REGISTRATION STATUS");
                Instance.Info($"      ✅ Already registered to Autopilot: {devicesRegistered} devices");
                Instance.Info($"      ⚠️ Not yet registered: {devicesNotRegistered} devices");
                
                if (devicesNotRegistered > 0)
                {
                    // Log first few device names for debugging
                    var sampleDevices = configMgrDevicesNotInAutopilot.Take(5).Select(d => d.Name).ToList();
                    Instance.Info($"      📋 Sample unregistered devices: {string.Join(", ", sampleDevices)}");
                    
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "not-autopilot-registered",
                        Name = "Not Registered to Autopilot",
                        Description = "These devices are not yet registered to Windows Autopilot. Register them to enable Autopilot provisioning.",
                        AffectedDeviceCount = devicesNotRegistered,
                        PercentageAffected = SafeBlockerPercentage(devicesNotRegistered, signal.TotalDevices),
                        Severity = BlockerSeverity.Medium,
                        RemediationAction = "Register devices to Autopilot via hardware hash upload or OEM registration",
                        RemediationUrl = "https://learn.microsoft.com/mem/autopilot/add-devices",
                        AffectedDevices = configMgrDevicesNotInAutopilot
                    });
                }

                // Check OS version requirement (Windows 10 1809+ or Windows 11)
                Instance.Info("");
                Instance.Info("   [CHECK 2/2] OS VERSION REQUIREMENT (Windows 10 1809+ or Windows 11)");;
                var osLookup = osDetails?.ToDictionary(o => o.ResourceId) ?? new Dictionary<int, OSDetails>();
                
                var devicesWithNoOsData = safeConfigMgrDevices.Where(d => !osLookup.ContainsKey(d.ResourceId) || string.IsNullOrEmpty(osLookup[d.ResourceId].BuildNumber)).ToList();
                var devicesBelowMinBuild = safeConfigMgrDevices.Where(d => {
                    if (!osLookup.TryGetValue(d.ResourceId, out var os)) return false;
                    if (string.IsNullOrEmpty(os.BuildNumber)) return false;
                    if (int.TryParse(os.BuildNumber, out var build)) return build < 17763;
                    return false;
                }).ToList();
                var devicesMeetingOsReq = safeConfigMgrDevices.Where(d => {
                    if (!osLookup.TryGetValue(d.ResourceId, out var os)) return false;
                    if (string.IsNullOrEmpty(os.BuildNumber)) return false;
                    if (int.TryParse(os.BuildNumber, out var build)) return build >= 17763;
                    return false;
                }).ToList();

                Instance.Info($"      ✅ Windows 10 1809+ or Windows 11: {devicesMeetingOsReq.Count} devices");
                Instance.Info($"      ⚠️ No OS build data: {devicesWithNoOsData.Count} devices");
                Instance.Info($"      ❌ Below minimum build (< 17763): {devicesBelowMinBuild.Count} devices");

                // Log OS version distribution
                var osBuildGroups = safeConfigMgrDevices
                    .Where(d => osLookup.ContainsKey(d.ResourceId) && !string.IsNullOrEmpty(osLookup[d.ResourceId].BuildNumber))
                    .GroupBy(d => osLookup[d.ResourceId].BuildNumber)
                    .OrderByDescending(g => g.Count())
                    .Take(10);
                Instance.Info("      OS Build Distribution (top 10):");
                foreach (var group in osBuildGroups)
                {
                    var buildNum = int.TryParse(group.Key, out var b) ? b : 0;
                    var osName = buildNum >= 22000 ? "Windows 11" : buildNum >= 19041 ? "Windows 10 2004+" : buildNum >= 17763 ? "Windows 10 1809+" : "Windows 10 (old)";
                    var status = buildNum >= 17763 ? "✅" : "❌";
                    Instance.Info($"         {status} Build {group.Key} ({osName}): {group.Count()} devices");
                }

                var unsupportedOsCount = devicesWithNoOsData.Count + devicesBelowMinBuild.Count;
                if (unsupportedOsCount > 0)
                {
                    var unsupportedOsDevices = devicesWithNoOsData.Concat(devicesBelowMinBuild).ToList();
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "unsupported-os",
                        Name = "Unsupported OS Version",
                        Description = "Windows 10 version 1809 or later is required for Autopilot registration.",
                        AffectedDeviceCount = unsupportedOsCount,
                        PercentageAffected = SafeBlockerPercentage(unsupportedOsCount, signal.TotalDevices),
                        Severity = BlockerSeverity.High,
                        RemediationAction = "Upgrade to Windows 10 1809+ or Windows 11",
                        RemediationUrl = "https://learn.microsoft.com/windows/release-health/",
                        AffectedDevices = unsupportedOsDevices
                    });
                }

                // Calculate ready devices: registered to Autopilot AND meeting OS requirements
                var meetsOsRequirements = signal.TotalDevices - unsupportedOsCount;
                signal.ReadyDevices = Math.Min(devicesRegistered, meetsOsRequirements);
                signal.TopBlockers = blockers.OrderByDescending(b => b.AffectedDeviceCount).Take(5).ToList();
                
                signal.Recommendations = GenerateAutopilotRecommendations(signal, blockers);

                Instance.Info("");
                Instance.Info($"   ═══════════════════════════════════════════════════════════════");
                Instance.Info($"   🚀 AUTOPILOT REGISTRATION RESULT: {signal.ReadinessPercentage}%");
                Instance.Info($"      Registered devices: {signal.ReadyDevices} / {signal.TotalDevices}");
                Instance.Info($"      Blockers found: {blockers.Count}");
                Instance.Info($"   ═══════════════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Instance.Error($"Autopilot readiness assessment failed: {ex.Message}");
                Instance.Error($"Stack trace: {ex.StackTrace}");
            }

            return signal;
        }

        /// <summary>
        /// Assesses Windows 11 upgrade readiness.
        /// Requirements: TPM 2.0, UEFI with Secure Boot, 4GB RAM, 64GB storage, compatible CPU
        /// </summary>
        public async Task<CloudReadinessSignal> GetWindows11ReadinessSignalAsync()
        {
            Instance.Info("┌─────────────────────────────────────────────────────────────────────────────────────────┐");
            Instance.Info("│ 🪟 WINDOWS 11 READINESS ASSESSMENT                                                      │");
            Instance.Info("└─────────────────────────────────────────────────────────────────────────────────────────┘");
            
            var signal = new CloudReadinessSignal
            {
                Id = "windows11",
                Name = "Windows 11 Readiness",
                Description = "Ready for Windows 11 upgrade",
                Icon = "🪟",
                RelatedWorkload = "OS Deployment",
                LearnMoreUrl = "https://learn.microsoft.com/windows/whats-new/windows-11-requirements"
            };

            try
            {
                Instance.Info("   Fetching device and hardware data...");
                var devices = await _configMgrService.GetWindows1011DevicesAsync();
                var tpmStatus = await _configMgrService.GetTpmStatusAsync();
                var osDetails = await _configMgrService.GetOSDetailsAsync();

                Instance.Info($"   📱 Total devices found: {devices?.Count ?? 0}");
                Instance.Info($"   📊 TPM records retrieved: {tpmStatus?.Count ?? 0}");
                Instance.Info($"   📊 OS records retrieved: {osDetails?.Count ?? 0}");

                // Separate Windows 10 and Windows 11 devices
                var windows11Devices = devices?.Where(d => 
                    d.OperatingSystem?.Contains("11") == true).ToList() ?? new List<ConfigMgrDevice>();
                var windows10Devices = devices?.Where(d => 
                    d.OperatingSystem?.Contains("10") == true && 
                    d.OperatingSystem?.Contains("11") != true).ToList() ?? new List<ConfigMgrDevice>();

                Instance.Info("");
                Instance.Info("   OS DISTRIBUTION:");
                Instance.Info($"      ✅ Already Windows 11: {windows11Devices.Count} devices");
                Instance.Info($"      🔄 Still Windows 10: {windows10Devices.Count} devices");

                signal.TotalDevices = windows10Devices.Count;
                
                if (signal.TotalDevices == 0)
                {
                    signal.TotalDevices = devices?.Count ?? 0;
                    signal.ReadyDevices = signal.TotalDevices; // All devices are already Windows 11
                    Instance.Info("   ✅ All devices are already Windows 11 or no Windows 10 devices found");
                    return signal;
                }

                var blockers = new List<ReadinessBlocker>();
                var readyDeviceIds = new HashSet<int>(windows10Devices.Select(d => d.ResourceId));

                // Check TPM 2.0
                Instance.Info("");
                Instance.Info("   [CHECK 1/1] TPM 2.0 REQUIREMENT (most common blocker)");
                var tpmLookup = tpmStatus?.ToDictionary(t => t.ResourceId) ?? new Dictionary<int, TpmStatus>();
                
                var devicesWithTpm20 = windows10Devices.Where(d => 
                    tpmLookup.TryGetValue(d.ResourceId, out var tpm) && 
                    tpm.IsPresent && tpm.IsEnabled &&
                    !string.IsNullOrEmpty(tpm.SpecVersion) && 
                    (tpm.SpecVersion.StartsWith("2.") || tpm.SpecVersion.Contains("2.0"))).ToList();
                var devicesWithNoTpmData = windows10Devices.Where(d => !tpmLookup.ContainsKey(d.ResourceId)).ToList();
                var devicesWithTpmDisabled = windows10Devices.Where(d => 
                    tpmLookup.TryGetValue(d.ResourceId, out var t) && (!t.IsPresent || !t.IsEnabled)).ToList();
                var devicesWithTpm12 = windows10Devices.Where(d => 
                    tpmLookup.TryGetValue(d.ResourceId, out var t) && t.IsPresent && t.IsEnabled && 
                    !string.IsNullOrEmpty(t.SpecVersion) && 
                    !t.SpecVersion.StartsWith("2.") && !t.SpecVersion.Contains("2.0")).ToList();

                Instance.Info($"      ✅ TPM 2.0 Present & Enabled: {devicesWithTpm20.Count} devices");
                Instance.Info($"      ⚠️ No TPM data available: {devicesWithNoTpmData.Count} devices");
                Instance.Info($"      ❌ TPM Missing or Disabled: {devicesWithTpmDisabled.Count} devices");
                Instance.Info($"      ❌ TPM 1.2 (needs upgrade): {devicesWithTpm12.Count} devices");

                // Collect devices without TPM 2.0
                var devicesWithoutTpm20 = windows10Devices.Where(d => 
                    !tpmLookup.TryGetValue(d.ResourceId, out var tpm) || 
                    !tpm.IsPresent || !tpm.IsEnabled ||
                    string.IsNullOrEmpty(tpm.SpecVersion) || 
                    !(tpm.SpecVersion.StartsWith("2.") || tpm.SpecVersion.Contains("2.0"))).ToList();

                var noTpm20 = devicesWithoutTpm20.Count;
                if (noTpm20 > 0)
                {
                    Instance.Info($"      → {noTpm20} devices cannot upgrade to Windows 11 due to TPM");
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "no-tpm20",
                        Name = "Missing TPM 2.0",
                        Description = "TPM 2.0 is required for Windows 11.",
                        AffectedDeviceCount = noTpm20,
                        PercentageAffected = SafeBlockerPercentage(noTpm20, signal.TotalDevices),
                        Severity = BlockerSeverity.Critical,
                        RemediationAction = "Enable TPM 2.0 in BIOS or plan hardware refresh",
                        RemediationUrl = "https://support.microsoft.com/windows/enable-tpm-2-0-on-your-pc",
                        AffectedDevices = devicesWithoutTpm20
                    });
                    
                    foreach (var d in devicesWithoutTpm20)
                    {
                        readyDeviceIds.Remove(d.ResourceId);
                    }
                }

                // Log sample devices without TPM 2.0
                if (devicesWithNoTpmData.Any())
                {
                    Instance.Debug("      Devices with no TPM data (first 10):");
                    foreach (var d in devicesWithNoTpmData.Take(10))
                    {
                        Instance.Debug($"         - {d.Name} (ResourceId: {d.ResourceId})");
                    }
                }

                signal.ReadyDevices = SafeReadyDevices(readyDeviceIds.Count, signal.TotalDevices, "Windows11");
                signal.TopBlockers = blockers.OrderByDescending(b => b.AffectedDeviceCount).Take(5).ToList();
                
                signal.Recommendations = GenerateWindows11Recommendations(signal, blockers);

                Instance.Info("");
                Instance.Info($"   ═══════════════════════════════════════════════════════════════");
                Instance.Info($"   🪟 WINDOWS 11 READINESS RESULT: {signal.ReadinessPercentage}%");
                Instance.Info($"      Ready Win10 devices: {signal.ReadyDevices} / {signal.TotalDevices}");
                Instance.Info($"      Already on Windows 11: {windows11Devices.Count}");
                Instance.Info($"      Blockers found: {blockers.Count}");
                Instance.Info($"   ═══════════════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Instance.Error($"Windows 11 readiness assessment failed: {ex.Message}");
                Instance.Error($"Stack trace: {ex.StackTrace}");
            }

            return signal;
        }

        /// <summary>
        /// Assesses Cloud-Native readiness for devices with a ConfigMgr record.
        /// 
        /// CRITERIA:
        /// - Assessment scope: ONLY devices with a record in ConfigMgr (migration targets)
        /// - Cloud-Native Ready: ConfigMgr devices that are co-managed with ALL workloads on Intune
        /// - Born-in-Cloud devices (Entra + Intune, no ConfigMgr) are already cloud native and excluded from scope
        /// 
        /// Uses Graph API configurationManagerClientEnabledFeatures for per-device workload authority.
        /// </summary>
        public async Task<CloudReadinessSignal> GetCloudNativeReadinessSignalAsync()
        {
            Instance.Info("┌─────────────────────────────────────────────────────────────────────────────────────────┐");
            Instance.Info("│ ☁️ CLOUD-NATIVE READINESS ASSESSMENT                                                    │");
            Instance.Info("└─────────────────────────────────────────────────────────────────────────────────────────┘");
            
            var signal = new CloudReadinessSignal
            {
                Id = "cloud-native",
                Name = "Cloud-Native Readiness",
                Description = "Ready for cloud-only management",
                Icon = "☁️",
                RelatedWorkload = "Device Management",
                LearnMoreUrl = "https://learn.microsoft.com/mem/solutions/cloud-native-endpoints/cloud-native-endpoints-overview"
            };

            try
            {
                Instance.Info("   Fetching enrollment data and workload authority...");
                var enrollmentData = await _graphService.GetDeviceEnrollmentAsync();
                var configMgrDevices = await _configMgrService.GetWindows1011DevicesAsync();
                
                // Get per-device workload authority for co-managed devices
                Instance.Info("   Querying co-management workload authority via Graph API...");
                var workloadAuthority = await _graphService.GetCoManagedWorkloadAuthorityAsync();

                // IMPORTANT: Assessment scope = ONLY devices with a ConfigMgr record
                // Born-in-cloud devices (Entra + Intune, no ConfigMgr) are already cloud native
                var configMgrDeviceCount = configMgrDevices?.Count ?? 0;
                var bornInCloudCount = enrollmentData?.CloudNativeDevices ?? 0;
                
                // Total devices for this assessment = ConfigMgr devices only (migration targets)
                signal.TotalDevices = configMgrDeviceCount;
                
                Instance.Info($"   📱 ASSESSMENT SCOPE:");
                Instance.Info($"      ConfigMgr devices (migration targets): {configMgrDeviceCount}");
                Instance.Info($"      Born-in-Cloud (already cloud native, excluded): {bornInCloudCount}");
                Instance.Info("");
                Instance.Info("   DEVICE MANAGEMENT STATE BREAKDOWN:");
                Instance.Info($"      ☁️ Born-in-Cloud (Entra + Intune, no ConfigMgr): {bornInCloudCount} ← Already done!");
                Instance.Info($"      🔄 Co-Managed (ConfigMgr + Intune): {enrollmentData?.CoManagedDevices ?? 0}");
                Instance.Info($"         └─ All workloads on Intune (CLOUD-NATIVE READY): {workloadAuthority.DevicesReadyForCloudNative}");
                Instance.Info($"         └─ Some workloads on ConfigMgr: {workloadAuthority.TotalCoManagedDevices - workloadAuthority.DevicesReadyForCloudNative}");
                Instance.Info($"      🟡 Hybrid Entra ID Joined: {enrollmentData?.HybridJoinedDevices ?? 0}");
                Instance.Info($"      🔴 ConfigMgr-Only (not in Intune): {enrollmentData?.ConfigMgrOnlyDevices ?? 0}");
                Instance.Info($"      🔴 On-Prem AD Only (no cloud identity): {enrollmentData?.OnPremDomainOnlyDevices ?? 0}");
                Instance.Info($"      ⚫ Workgroup devices: {enrollmentData?.WorkgroupDevices ?? 0}");
                
                if (signal.TotalDevices == 0)
                {
                    Instance.Warning("   ⚠️ No ConfigMgr devices found - nothing to migrate");
                    Instance.Info($"   ☁️ You have {bornInCloudCount} born-in-cloud devices that are already cloud native");
                    signal.ReadyDevices = 0;
                    return signal;
                }

                var blockers = new List<ReadinessBlocker>();
                
                // Create lookup dictionary for ConfigMgr devices by name (for workload blockers)
                var configMgrDeviceLookup = (configMgrDevices ?? new List<ConfigMgrDevice>())
                    .Where(d => !string.IsNullOrEmpty(d.Name))
                    .GroupBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                // Cloud-Native Ready = ConfigMgr devices that are co-managed with ALL workloads on Intune
                // These devices can have the ConfigMgr client removed and become fully cloud native
                var coManagedReadyForCloudNative = workloadAuthority.DevicesReadyForCloudNative;
                
                // Co-managed devices still with some workloads on ConfigMgr
                var coManagedNotReady = workloadAuthority.TotalCoManagedDevices - coManagedReadyForCloudNative;
                
                Instance.Info("");
                Instance.Info("   BLOCKERS ANALYSIS (ConfigMgr devices not yet cloud-native ready):");
                
                // Co-managed devices with workloads still on ConfigMgr
                if (coManagedNotReady > 0)
                {
                    Instance.Info($"      🟡 Co-Managed with workloads on ConfigMgr: {coManagedNotReady} devices");
                    Instance.Info($"         → These devices are co-managed but still have workloads on ConfigMgr");
                    Instance.Info($"         → Move ALL workloads to Intune to become cloud-native ready");
                    
                    // Calculate which workloads are most commonly still on ConfigMgr
                    var workloadsNotOnIntune = new List<string>();
                    foreach (var workload in workloadAuthority.WorkloadIntuneAdoptionCounts)
                    {
                        var notOnIntune = workloadAuthority.TotalCoManagedDevices - workload.Value;
                        if (notOnIntune > 0)
                        {
                            var pct = Math.Round((double)notOnIntune / workloadAuthority.TotalCoManagedDevices * 100, 0);
                            workloadsNotOnIntune.Add($"{workload.Key} ({notOnIntune} devices, {pct}%)");
                        }
                    }
                    
                    // Get ConfigMgr device objects for devices that still have workloads on ConfigMgr
                    var deviceNamesWithWorkloadsOnConfigMgr = workloadAuthority.Devices
                        .Where(d => !d.AllWorkloadsManagedByIntune)
                        .Select(d => d.DeviceName)
                        .Where(n => !string.IsNullOrEmpty(n))
                        .ToList();
                    
                    // Lookup full ConfigMgr device objects by name
                    var devicesWithWorkloadsOnConfigMgr = deviceNamesWithWorkloadsOnConfigMgr
                        .Select(name => configMgrDeviceLookup.GetValueOrDefault(name))
                        .Where(d => d != null)
                        .ToList()!;
                    
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "comanaged-workloads-on-configmgr",
                        Name = "Co-managed with workloads in ConfigMgr",
                        Description = $"These devices are co-managed but still have workloads managed by ConfigMgr: {string.Join(", ", workloadsNotOnIntune.Take(3))}",
                        AffectedDeviceCount = coManagedNotReady,
                        PercentageAffected = SafeBlockerPercentage(coManagedNotReady, signal.TotalDevices),
                        Severity = BlockerSeverity.Medium,
                        RemediationAction = "Move remaining co-management workload sliders to Intune",
                        RemediationUrl = "https://learn.microsoft.com/mem/configmgr/comanage/how-to-switch-workloads",
                        AffectedDevices = devicesWithWorkloadsOnConfigMgr!
                    });
                }
                
                // ConfigMgr-only devices (not enrolled in Intune at all)
                var configMgrOnly = enrollmentData?.ConfigMgrOnlyDevices ?? 0;
                if (configMgrOnly > 0)
                {
                    Instance.Info($"      🔴 ConfigMgr Only (not in Intune): {configMgrOnly} devices");
                    Instance.Info($"         → Managed by ConfigMgr but not enrolled in Intune");
                    Instance.Info($"         → Enable co-management to start cloud journey");
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "configmgr-only",
                        Name = "ConfigMgr Only (Not in Intune)",
                        Description = "These devices are managed by ConfigMgr but not enrolled in Intune.",
                        AffectedDeviceCount = configMgrOnly,
                        PercentageAffected = SafeBlockerPercentage(configMgrOnly, signal.TotalDevices),
                        Severity = BlockerSeverity.High,
                        RemediationAction = "Enable co-management and enroll in Intune",
                        RemediationUrl = "https://learn.microsoft.com/mem/configmgr/comanage/how-to-enable"
                    });
                }

                // On-prem only devices (no cloud identity)
                var onPremOnly = enrollmentData?.OnPremDomainOnlyDevices ?? 0;
                if (onPremOnly > 0)
                {
                    Instance.Info($"      🔴 On-Premises AD Only: {onPremOnly} devices");
                    Instance.Info($"         → These devices are only joined to on-premises AD");
                    Instance.Info($"         → No cloud identity - need Hybrid Entra ID Join as first step");
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "on-prem-only",
                        Name = "On-Premises AD Only",
                        Description = "These devices are only joined to on-premises AD with no cloud identity.",
                        AffectedDeviceCount = onPremOnly,
                        PercentageAffected = SafeBlockerPercentage(onPremOnly, signal.TotalDevices),
                        Severity = BlockerSeverity.High,
                        RemediationAction = "Configure Hybrid Entra ID Join as first step to cloud",
                        RemediationUrl = "https://learn.microsoft.com/entra/identity/devices/hybrid-join-plan"
                    });
                }

                // Hybrid joined is NOT a blocker for cloud-native readiness
                // It's the expected state during migration - they can still be co-managed with all workloads on Intune
                var hybridJoined = enrollmentData?.HybridJoinedDevices ?? 0;
                if (hybridJoined > 0)
                {
                    Instance.Info($"      ℹ️ Hybrid Entra ID Joined: {hybridJoined} devices");
                    Instance.Info($"         → This is expected during migration (not a blocker)");
                    Instance.Info($"         → Can still achieve cloud-native ready with all workloads on Intune");
                    // Note: NOT adding as a blocker - Hybrid join + all workloads on Intune = cloud-native ready
                }

                // Ready devices = ConfigMgr devices that are co-managed with ALL workloads on Intune
                signal.ReadyDevices = SafeReadyDevices(coManagedReadyForCloudNative, signal.TotalDevices, "CloudNative");
                signal.TopBlockers = blockers.OrderByDescending(b => b.AffectedDeviceCount).Take(5).ToList();
                
                signal.Recommendations = GenerateCloudNativeRecommendations(signal, blockers, workloadAuthority);

                Instance.Info("");
                Instance.Info($"   ═══════════════════════════════════════════════════════════════");
                Instance.Info($"   ☁️ CLOUD-NATIVE READINESS RESULT: {signal.ReadinessPercentage}%");
                Instance.Info($"      Assessment scope: {signal.TotalDevices} ConfigMgr devices");
                Instance.Info($"      Cloud-native ready: {signal.ReadyDevices} (co-managed, ALL workloads on Intune)");
                Instance.Info($"      Born-in-cloud (already done): {bornInCloudCount} (excluded from scope)");
                Instance.Info($"      Blockers found: {blockers.Count}");
                Instance.Info($"   ═══════════════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Instance.Error($"Cloud-Native readiness assessment failed: {ex.Message}");
                Instance.Error($"Stack trace: {ex.StackTrace}");
            }

            return signal;
        }

        /// <summary>
        /// Assesses Windows Autopatch readiness.
        /// Requirements per Microsoft docs:
        /// - Windows 10/11 Enterprise or Education edition (Pro also supported per Autopatch docs)
        /// - Entra ID joined or Hybrid Entra ID joined
        /// - Intune enrolled (or co-managed with Windows Update workload on Intune)
        /// - Co-management workloads: Windows Update, Device Configuration, Office Click-to-Run must be Intune
        /// 
        /// What we CAN check via Graph API:
        /// - OS Edition (Enterprise/Education/Pro)
        /// - Entra ID join status
        /// - Intune enrollment status
        /// - Co-management workload authority
        /// 
        /// What we CANNOT check:
        /// - User licensing (E3/E5/Business Premium) - would require per-user license query
        /// - Windows diagnostic data level (requires device-level policy check)
        /// - Network connectivity to Windows Update endpoints
        /// </summary>
        public async Task<CloudReadinessSignal> GetAutopatchReadinessSignalAsync()
        {
            Instance.Info("┌─────────────────────────────────────────────────────────────────────────────────────────┐");
            Instance.Info("│ 🔄 WINDOWS AUTOPATCH READINESS ASSESSMENT                                               │");
            Instance.Info("└─────────────────────────────────────────────────────────────────────────────────────────┘");
            
            var signal = new CloudReadinessSignal
            {
                Id = "autopatch",
                Name = "Autopatch Readiness",
                Description = "Ready for Windows Autopatch automated updates",
                Icon = "🔄",
                RelatedWorkload = "Update Management",
                LearnMoreUrl = "https://learn.microsoft.com/windows/deployment/windows-autopatch/overview/windows-autopatch-overview"
            };

            try
            {
                Instance.Info("   Fetching device and enrollment data...");
                var configMgrDevices = await _configMgrService.GetWindows1011DevicesAsync();
                var osDetails = await _configMgrService.GetOSDetailsAsync();
                var enrollmentData = await _graphService.GetDeviceEnrollmentAsync();
                var workloadAuthority = await _graphService.GetCoManagedWorkloadAuthorityAsync();

                var configMgrCount = configMgrDevices?.Count ?? 0;
                
                Instance.Info($"   📱 ConfigMgr Windows 10/11 devices: {configMgrCount}");
                Instance.Info($"   📊 OS detail records retrieved: {osDetails?.Count ?? 0}");
                
                signal.TotalDevices = configMgrCount;
                
                if (signal.TotalDevices == 0)
                {
                    Instance.Warning("   ⚠️ No ConfigMgr devices found for Autopatch assessment");
                    return signal;
                }

                var blockers = new List<ReadinessBlocker>();
                var osLookup = osDetails?.ToDictionary(o => o.ResourceId) ?? new Dictionary<int, OSDetails>();
                var safeConfigMgrDevices = configMgrDevices ?? new List<ConfigMgrDevice>();
                
                // Create lookup dictionary for ConfigMgr devices by name (for workload blockers)
                var configMgrDeviceLookup = safeConfigMgrDevices
                    .Where(d => !string.IsNullOrEmpty(d.Name))
                    .GroupBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                // CHECK 1: OS Edition - Autopatch requires Enterprise, Education, or Pro for Workstations
                Instance.Info("");
                Instance.Info("   [CHECK 1/4] OS EDITION (Enterprise, Education, Pro for Workstations)");
                
                // Note: We can infer edition from OS caption in ConfigMgr
                var enterpriseDevices = safeConfigMgrDevices.Where(d => 
                    d.OperatingSystem?.Contains("Enterprise", StringComparison.OrdinalIgnoreCase) == true ||
                    d.OperatingSystem?.Contains("Education", StringComparison.OrdinalIgnoreCase) == true).ToList();
                var proDevices = safeConfigMgrDevices.Where(d => 
                    d.OperatingSystem?.Contains("Pro", StringComparison.OrdinalIgnoreCase) == true &&
                    d.OperatingSystem?.Contains("Enterprise", StringComparison.OrdinalIgnoreCase) != true).ToList();
                var homeDevices = safeConfigMgrDevices.Where(d => 
                    d.OperatingSystem?.Contains("Home", StringComparison.OrdinalIgnoreCase) == true).ToList();
                var unknownEdition = safeConfigMgrDevices.Where(d =>
                    d.OperatingSystem?.Contains("Enterprise", StringComparison.OrdinalIgnoreCase) != true &&
                    d.OperatingSystem?.Contains("Education", StringComparison.OrdinalIgnoreCase) != true &&
                    d.OperatingSystem?.Contains("Pro", StringComparison.OrdinalIgnoreCase) != true &&
                    d.OperatingSystem?.Contains("Home", StringComparison.OrdinalIgnoreCase) != true).ToList();

                var supportedEditionCount = enterpriseDevices.Count + proDevices.Count;
                var unsupportedEditionCount = homeDevices.Count;

                Instance.Info($"      ✅ Enterprise/Education: {enterpriseDevices.Count} devices");
                Instance.Info($"      ✅ Pro (supported for Autopatch): {proDevices.Count} devices");
                Instance.Info($"      ❌ Home (not supported): {homeDevices.Count} devices");
                Instance.Info($"      ⚠️ Unknown edition: {unknownEdition.Count} devices");

                if (unsupportedEditionCount > 0)
                {
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "unsupported-edition",
                        Name = "Windows Home Edition",
                        Description = "Windows Autopatch requires Enterprise, Education, or Pro edition. Home edition is not supported.",
                        AffectedDeviceCount = unsupportedEditionCount,
                        PercentageAffected = SafeBlockerPercentage(unsupportedEditionCount, signal.TotalDevices),
                        Severity = BlockerSeverity.Critical,
                        RemediationAction = "Upgrade to Windows 10/11 Pro, Enterprise, or Education",
                        RemediationUrl = "https://learn.microsoft.com/windows/deployment/windows-autopatch/prepare/windows-autopatch-prerequisites",
                        AffectedDevices = homeDevices
                    });
                }

                // CHECK 2: Intune Enrollment - Required for Autopatch
                Instance.Info("");
                Instance.Info("   [CHECK 2/4] INTUNE ENROLLMENT (required for Autopatch)");
                
                var coManagedCount = enrollmentData?.CoManagedDevices ?? 0;
                var intuneOnlyCount = enrollmentData?.CloudNativeDevices ?? 0;
                var configMgrOnlyCount = enrollmentData?.ConfigMgrOnlyDevices ?? 0;
                var enrolledInIntune = coManagedCount + intuneOnlyCount;
                
                Instance.Info($"      ✅ Enrolled in Intune (cloud-native): {intuneOnlyCount} devices");
                Instance.Info($"      ✅ Enrolled in Intune (co-managed): {coManagedCount} devices");
                Instance.Info($"      ❌ ConfigMgr-only (not in Intune): {configMgrOnlyCount} devices");

                if (configMgrOnlyCount > 0)
                {
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "not-enrolled-intune",
                        Name = "Not Enrolled in Intune",
                        Description = "Windows Autopatch requires Intune enrollment for policy delivery and update management.",
                        AffectedDeviceCount = configMgrOnlyCount,
                        PercentageAffected = SafeBlockerPercentage(configMgrOnlyCount, signal.TotalDevices),
                        Severity = BlockerSeverity.High,
                        RemediationAction = "Enable co-management and enroll devices in Intune",
                        RemediationUrl = "https://learn.microsoft.com/mem/configmgr/comanage/how-to-enable"
                    });
                }

                // CHECK 3: Windows Update workload on Intune (for co-managed devices)
                Instance.Info("");
                Instance.Info("   [CHECK 3/4] WINDOWS UPDATE WORKLOAD (must be Intune for co-managed)");
                
                var wuWorkloadOnIntune = workloadAuthority.WorkloadIntuneAdoptionCounts.GetValueOrDefault("Windows Update", 0);
                var wuWorkloadOnConfigMgr = workloadAuthority.TotalCoManagedDevices - wuWorkloadOnIntune;
                
                Instance.Info($"      ✅ Windows Update workload on Intune: {wuWorkloadOnIntune} devices");
                Instance.Info($"      ❌ Windows Update workload on ConfigMgr: {wuWorkloadOnConfigMgr} devices");

                if (wuWorkloadOnConfigMgr > 0)
                {
                    // Get device names from Graph workload authority data
                    var deviceNamesWithWuOnConfigMgr = workloadAuthority.Devices
                        .Where(d => d.WindowsUpdateManagedByConfigMgr)
                        .Select(d => d.DeviceName)
                        .Where(n => !string.IsNullOrEmpty(n))
                        .ToList();
                    
                    // Lookup full ConfigMgr device objects by name
                    var devicesWithWuOnConfigMgr = deviceNamesWithWuOnConfigMgr
                        .Select(name => configMgrDeviceLookup.GetValueOrDefault(name))
                        .Where(d => d != null)
                        .ToList()!;

                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "wu-workload-configmgr",
                        Name = "Windows Update Workload on ConfigMgr",
                        Description = "Windows Autopatch requires the Windows Update for Business workload to be managed by Intune.",
                        AffectedDeviceCount = wuWorkloadOnConfigMgr,
                        PercentageAffected = SafeBlockerPercentage(wuWorkloadOnConfigMgr, signal.TotalDevices),
                        Severity = BlockerSeverity.High,
                        RemediationAction = "Move Windows Update workload slider to Intune in co-management settings",
                        RemediationUrl = "https://learn.microsoft.com/mem/configmgr/comanage/how-to-switch-workloads",
                        AffectedDevices = devicesWithWuOnConfigMgr!
                    });
                }

                // CHECK 4: Entra ID Join Status
                Instance.Info("");
                Instance.Info("   [CHECK 4/4] ENTRA ID JOIN STATUS (required for Autopatch)");
                
                var entraJoinedCount = (enrollmentData?.AzureADOnlyDevices ?? 0) + (enrollmentData?.HybridJoinedDevices ?? 0);
                var noCloudIdentity = (enrollmentData?.OnPremDomainOnlyDevices ?? 0) + (enrollmentData?.WorkgroupDevices ?? 0);
                
                Instance.Info($"      ✅ Entra ID Joined (AAD-only): {enrollmentData?.AzureADOnlyDevices ?? 0} devices");
                Instance.Info($"      ✅ Hybrid Entra ID Joined: {enrollmentData?.HybridJoinedDevices ?? 0} devices");
                Instance.Info($"      ❌ On-Prem AD Only: {enrollmentData?.OnPremDomainOnlyDevices ?? 0} devices");
                Instance.Info($"      ❌ Workgroup (no identity): {enrollmentData?.WorkgroupDevices ?? 0} devices");

                if (noCloudIdentity > 0)
                {
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "no-entra-identity",
                        Name = "No Entra ID Identity",
                        Description = "Windows Autopatch requires devices to be Entra ID joined or Hybrid Entra ID joined.",
                        AffectedDeviceCount = noCloudIdentity,
                        PercentageAffected = SafeBlockerPercentage(noCloudIdentity, signal.TotalDevices),
                        Severity = BlockerSeverity.High,
                        RemediationAction = "Configure Hybrid Entra ID Join or Azure AD Join for these devices",
                        RemediationUrl = "https://learn.microsoft.com/entra/identity/devices/hybrid-join-plan"
                    });
                }

                // Calculate ready devices: must meet ALL criteria
                // - Supported edition (Enterprise/Education/Pro)
                // - Enrolled in Intune
                // - Windows Update workload on Intune (if co-managed)
                // - Entra ID joined
                
                // Ready = Devices in Intune with WU workload on Intune and supported edition
                var readyDevices = Math.Min(
                    supportedEditionCount,
                    Math.Min(
                        enrolledInIntune,
                        intuneOnlyCount + wuWorkloadOnIntune // Cloud-native OR co-managed with WU on Intune
                    )
                );
                
                // Also subtract devices without cloud identity
                readyDevices = Math.Max(0, readyDevices - noCloudIdentity);
                
                signal.ReadyDevices = SafeReadyDevices(readyDevices, signal.TotalDevices, "Autopatch");
                signal.TopBlockers = blockers.OrderByDescending(b => b.AffectedDeviceCount).Take(5).ToList();
                
                signal.Recommendations = new List<string>
                {
                    "Windows Autopatch automates quality and feature updates with minimal IT effort.",
                    configMgrOnlyCount > 0 ? $"Enable co-management for {configMgrOnlyCount} ConfigMgr-only devices to enable Autopatch." : null!,
                    wuWorkloadOnConfigMgr > 0 ? $"Move Windows Update workload to Intune for {wuWorkloadOnConfigMgr} co-managed devices." : null!,
                    unsupportedEditionCount > 0 ? $"Upgrade {unsupportedEditionCount} Home edition devices to Pro/Enterprise." : null!,
                    signal.ReadinessPercentage >= 80 ? "Most devices are Autopatch-ready! Consider enrolling in Windows Autopatch." : null!
                }.Where(r => !string.IsNullOrEmpty(r)).ToList();

                Instance.Info("");
                Instance.Info($"   ═══════════════════════════════════════════════════════════════");
                Instance.Info($"   🔄 AUTOPATCH READINESS RESULT: {signal.ReadinessPercentage}%");
                Instance.Info($"      Ready devices: {signal.ReadyDevices} / {signal.TotalDevices}");
                Instance.Info($"      Blockers found: {blockers.Count}");
                Instance.Info($"   ═══════════════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Instance.Error($"Autopatch readiness assessment failed: {ex.Message}");
                Instance.Error($"Stack trace: {ex.StackTrace}");
            }

            return signal;
        }

        /// <summary>
        /// Assesses Identity readiness (on-prem AD → Entra).
        /// </summary>
        public async Task<CloudReadinessSignal> GetIdentityReadinessSignalAsync()
        {
            Instance.Info("┌─────────────────────────────────────────────────────────────────────────────────────────┐");
            Instance.Info("│ 🔐 IDENTITY READINESS ASSESSMENT                                                        │");
            Instance.Info("└─────────────────────────────────────────────────────────────────────────────────────────┘");
            
            var signal = new CloudReadinessSignal
            {
                Id = "identity",
                Name = "Identity Readiness",
                Description = "Ready for cloud identity (Entra ID)",
                Icon = "🔐",
                RelatedWorkload = "Identity Management",
                LearnMoreUrl = "https://learn.microsoft.com/entra/identity/devices/overview"
            };

            try
            {
                Instance.Info("   Fetching identity data from Graph API...");
                var enrollmentData = await _graphService.GetDeviceEnrollmentAsync();

                signal.TotalDevices = enrollmentData?.TotalDevices ?? 0;
                
                Instance.Info($"   📱 Total devices: {signal.TotalDevices}");
                Instance.Info("");
                Instance.Info("   IDENTITY STATE BREAKDOWN:");
                
                var aadOnly = enrollmentData?.AzureADOnlyDevices ?? 0;
                var hybridJoined = enrollmentData?.HybridJoinedDevices ?? 0;
                var onPremOnly = enrollmentData?.OnPremDomainOnlyDevices ?? 0;
                var workgroup = enrollmentData?.WorkgroupDevices ?? 0;
                
                Instance.Info($"      ✅ Azure AD Joined (cloud-native identity): {aadOnly}");
                Instance.Info($"      ✅ Hybrid Azure AD Joined (dual identity): {hybridJoined}");
                Instance.Info($"      🔴 On-Prem AD Only (no cloud identity): {onPremOnly}");
                Instance.Info($"      ⚫ Workgroup (no domain identity): {workgroup}");
                
                if (signal.TotalDevices == 0)
                {
                    Instance.Warning("   ⚠️ No devices found for identity assessment");
                    return signal;
                }

                var blockers = new List<ReadinessBlocker>();

                // Devices with cloud identity (AAD or Hybrid)
                var cloudIdentityReady = aadOnly + hybridJoined;
                
                Instance.Info("");
                Instance.Info("   BLOCKERS ANALYSIS:");
                
                // On-prem only (no cloud identity)
                if (onPremOnly > 0)
                {
                    Instance.Info($"      🔴 No Cloud Identity: {onPremOnly} devices ({Math.Round((double)onPremOnly / signal.TotalDevices * 100, 1)}%)");
                    Instance.Info($"         → These devices cannot authenticate to cloud services");
                    Instance.Info($"         → Configure Azure AD Connect for Hybrid Join");
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "no-cloud-identity",
                        Name = "No Cloud Identity",
                        Description = "These devices have no Azure AD/Entra identity.",
                        AffectedDeviceCount = onPremOnly,
                        PercentageAffected = SafeBlockerPercentage(onPremOnly, signal.TotalDevices),
                        Severity = BlockerSeverity.High,
                        RemediationAction = "Configure Azure AD Connect for Hybrid Join",
                        RemediationUrl = "https://learn.microsoft.com/entra/identity/hybrid/connect/how-to-connect-install-roadmap"
                    });
                }

                // Workgroup devices
                if (workgroup > 0)
                {
                    Instance.Info($"      ⚫ Workgroup Devices: {workgroup} devices ({Math.Round((double)workgroup / signal.TotalDevices * 100, 1)}%)");
                    Instance.Info($"         → Not domain joined, no cloud identity");
                    Instance.Info($"         → Consider Azure AD Join for these devices");
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "workgroup-devices",
                        Name = "Workgroup Devices",
                        Description = "These devices are not domain joined and have no cloud identity.",
                        AffectedDeviceCount = workgroup,
                        PercentageAffected = SafeBlockerPercentage(workgroup, signal.TotalDevices),
                        Severity = BlockerSeverity.Medium,
                        RemediationAction = "Azure AD Join these devices directly",
                        RemediationUrl = "https://learn.microsoft.com/entra/identity/devices/device-join-plan"
                    });
                }

                signal.ReadyDevices = SafeReadyDevices(cloudIdentityReady, signal.TotalDevices, "Identity");
                signal.TopBlockers = blockers.OrderByDescending(b => b.AffectedDeviceCount).Take(5).ToList();
                
                signal.Recommendations = new List<string>
                {
                    cloudIdentityReady > signal.TotalDevices * 0.8 
                        ? "Great progress! Most devices have cloud identity." 
                        : "Focus on getting all devices registered with Azure AD/Entra.",
                    onPremOnly > 0 ? "Configure Azure AD Connect Hybrid Join for on-prem only devices." : null,
                    workgroup > 0 ? "Consider Azure AD Join for workgroup devices." : null
                }.Where(r => r != null).ToList()!;

                Instance.Info("");
                Instance.Info($"   ═══════════════════════════════════════════════════════════════");
                Instance.Info($"   🔐 IDENTITY READINESS RESULT: {signal.ReadinessPercentage}%");
                Instance.Info($"      Ready devices: {signal.ReadyDevices} / {signal.TotalDevices}");
                Instance.Info($"      (AAD: {aadOnly} + Hybrid: {hybridJoined})");
                Instance.Info($"      Blockers found: {blockers.Count}");
                Instance.Info($"   ═══════════════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Instance.Error($"Identity readiness assessment failed: {ex.Message}");
                Instance.Error($"Stack trace: {ex.StackTrace}");
            }

            return signal;
        }

        /// <summary>
        /// Assesses Windows Update for Business readiness (WSUS → WUfB).
        /// </summary>
        public async Task<CloudReadinessSignal> GetWufbReadinessSignalAsync()
        {
            Instance.Info("┌─────────────────────────────────────────────────────────────────────────────────────────┐");
            Instance.Info("│ 🔄 UPDATE MANAGEMENT (WUfB) READINESS ASSESSMENT                                        │");
            Instance.Info("└─────────────────────────────────────────────────────────────────────────────────────────┘");
            
            var signal = new CloudReadinessSignal
            {
                Id = "wufb",
                Name = "Update Management Readiness",
                Description = "Ready for Windows Update for Business",
                Icon = "🔄",
                RelatedWorkload = "Update Management",
                LearnMoreUrl = "https://learn.microsoft.com/windows/deployment/update/waas-manage-updates-wufb"
            };

            try
            {
                Instance.Info("   Fetching device and OS data...");
                var devices = await _configMgrService.GetWindows1011DevicesAsync();
                var osDetails = await _configMgrService.GetOSDetailsAsync();
                var enrollmentData = await _graphService.GetDeviceEnrollmentAsync();

                signal.TotalDevices = devices?.Count ?? 0;
                
                Instance.Info($"   📱 Total devices: {signal.TotalDevices}");
                Instance.Info($"   📊 OS records retrieved: {osDetails?.Count ?? 0}");
                
                if (signal.TotalDevices == 0)
                {
                    Instance.Warning("   ⚠️ No devices found for WUfB assessment");
                    return signal;
                }

                var blockers = new List<ReadinessBlocker>();
                var readyCount = 0;
                var oldOsCount = 0;

                // WUfB requires Windows 10 Pro/Enterprise/Education or Windows 11
                var osLookup = osDetails?.ToDictionary(o => o.ResourceId) ?? new Dictionary<int, OSDetails>();
                
                Instance.Info("");
                Instance.Info("   [CHECK 1/2] OS VERSION REQUIREMENT (Windows 10 1703+)");
                
                foreach (var device in devices)
                {
                    var isWufbReady = true;
                    
                    // Check OS version (WUfB requires Windows 10 1703+)
                    if (osLookup.TryGetValue(device.ResourceId, out var os))
                    {
                        if (int.TryParse(os.BuildNumber, out var build) && build < 15063) // 1703 = 15063
                        {
                            isWufbReady = false;
                            oldOsCount++;
                        }
                    }
                    
                    if (isWufbReady) readyCount++;
                }

                Instance.Info($"      ✅ Windows 10 1703+ or Windows 11: {readyCount} devices");
                Instance.Info($"      ❌ Below minimum build (< 15063): {oldOsCount} devices");
                
                if (oldOsCount > 0)
                {
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "old-os-wufb",
                        Name = "OS Too Old for WUfB",
                        Description = "WUfB requires Windows 10 version 1703 or later.",
                        AffectedDeviceCount = oldOsCount,
                        PercentageAffected = SafeBlockerPercentage(oldOsCount, signal.TotalDevices),
                        Severity = BlockerSeverity.Medium,
                        RemediationAction = "Upgrade to Windows 10 1703+ or Windows 11",
                        RemediationUrl = "https://learn.microsoft.com/windows/release-health/"
                    });
                }

                // Check for devices not in Intune (needed for WUfB policy delivery)
                Instance.Info("");
                Instance.Info("   [CHECK 2/2] INTUNE ENROLLMENT (for policy delivery)");
                var notInIntune = enrollmentData?.ConfigMgrOnlyDevices ?? 0;
                var inIntune = (enrollmentData?.CoManagedDevices ?? 0) + (enrollmentData?.CloudNativeDevices ?? 0);
                
                Instance.Info($"      ✅ Enrolled in Intune (can receive WUfB policies): {inIntune} devices");
                Instance.Info($"      🔴 Not in Intune (ConfigMgr-only): {notInIntune} devices");
                
                if (notInIntune > 0)
                {
                    Instance.Info($"         → WUfB policies require Intune for delivery");
                    Instance.Info($"         → Enable co-management to enroll in Intune");
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "not-in-intune",
                        Name = "Not Enrolled in Intune",
                        Description = "WUfB policies are delivered through Intune. These devices need enrollment.",
                        AffectedDeviceCount = notInIntune,
                        PercentageAffected = SafeBlockerPercentage(notInIntune, signal.TotalDevices),
                        Severity = BlockerSeverity.Medium,
                        RemediationAction = "Enroll devices in Intune via co-management",
                        RemediationUrl = "https://learn.microsoft.com/mem/configmgr/comanage/how-to-enable"
                    });
                }

                signal.ReadyDevices = SafeReadyDevices(readyCount, signal.TotalDevices, "WUfB");
                signal.TopBlockers = blockers.OrderByDescending(b => b.AffectedDeviceCount).Take(5).ToList();
                
                signal.Recommendations = new List<string>
                {
                    "Windows Update for Business simplifies update management with cloud policies.",
                    notInIntune > 0 ? $"Enroll {notInIntune} devices in Intune to enable WUfB policy delivery." : null,
                    "Consider using Update Rings in Intune to manage feature and quality updates."
                }.Where(r => r != null).ToList()!;

                Instance.Info("");
                Instance.Info($"   ═══════════════════════════════════════════════════════════════");
                Instance.Info($"   🔄 UPDATE MANAGEMENT READINESS RESULT: {signal.ReadinessPercentage}%");
                Instance.Info($"      Ready devices (OS compatible): {signal.ReadyDevices} / {signal.TotalDevices}");
                Instance.Info($"      Blockers found: {blockers.Count}");
                Instance.Info($"   ═══════════════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Instance.Error($"WUfB readiness assessment failed: {ex.Message}");
                Instance.Error($"Stack trace: {ex.StackTrace}");
            }

            return signal;
        }

        /// <summary>
        /// Assesses Endpoint Security readiness (ConfigMgr EP → Microsoft Defender for Endpoint).
        /// </summary>
        public async Task<CloudReadinessSignal> GetEndpointSecurityReadinessSignalAsync()
        {
            Instance.Info("┌─────────────────────────────────────────────────────────────────────────────────────────┐");
            Instance.Info("│ 🛡️ ENDPOINT SECURITY (MDE) READINESS ASSESSMENT                                         │");
            Instance.Info("└─────────────────────────────────────────────────────────────────────────────────────────┘");
            
            var signal = new CloudReadinessSignal
            {
                Id = "endpoint-security",
                Name = "Endpoint Security Readiness",
                Description = "Ready for Microsoft Defender for Endpoint",
                Icon = "🛡️",
                RelatedWorkload = "Endpoint Security",
                LearnMoreUrl = "https://learn.microsoft.com/microsoft-365/security/defender-endpoint/microsoft-defender-endpoint"
            };

            try
            {
                Instance.Info("   Fetching device and OS data...");
                var devices = await _configMgrService.GetWindows1011DevicesAsync();
                var osDetails = await _configMgrService.GetOSDetailsAsync();
                var enrollmentData = await _graphService.GetDeviceEnrollmentAsync();

                signal.TotalDevices = devices?.Count ?? 0;
                
                Instance.Info($"   📱 Total devices: {signal.TotalDevices}");
                Instance.Info($"   📊 OS records retrieved: {osDetails?.Count ?? 0}");
                
                if (signal.TotalDevices == 0)
                {
                    Instance.Warning("   ⚠️ No devices found for Endpoint Security assessment");
                    return signal;
                }

                var blockers = new List<ReadinessBlocker>();
                var osLookup = osDetails?.ToDictionary(o => o.ResourceId) ?? new Dictionary<int, OSDetails>();

                // MDE is built into Windows 10/11 - check for supported versions
                var supportedCount = 0;
                var unsupportedOs = 0;
                var noOsData = 0;

                Instance.Info("");
                Instance.Info("   [CHECK 1/2] OS VERSION REQUIREMENT (Windows 10 1607+)");
                
                foreach (var device in devices)
                {
                    var isSupported = false;
                    
                    if (osLookup.TryGetValue(device.ResourceId, out var os))
                    {
                        // MDE supports Windows 10 1607+ (build 14393)
                        if (int.TryParse(os.BuildNumber, out var build))
                        {
                            isSupported = build >= 14393;
                            if (!isSupported) unsupportedOs++;
                        }
                        else
                        {
                            noOsData++;
                        }
                    }
                    else
                    {
                        noOsData++;
                    }
                    
                    if (isSupported)
                        supportedCount++;
                }

                Instance.Info($"      ✅ Windows 10 1607+ (MDE supported): {supportedCount} devices");
                Instance.Info($"      ❌ Below minimum build (< 14393): {unsupportedOs} devices");
                Instance.Info($"      ⚠️ No OS data available: {noOsData} devices");

                if (unsupportedOs > 0)
                {
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "unsupported-mde-os",
                        Name = "Unsupported OS for MDE",
                        Description = "Microsoft Defender for Endpoint requires Windows 10 1607 or later.",
                        AffectedDeviceCount = unsupportedOs,
                        PercentageAffected = SafeBlockerPercentage(unsupportedOs, signal.TotalDevices),
                        Severity = BlockerSeverity.Medium,
                        RemediationAction = "Upgrade to Windows 10 1607+ or Windows 11",
                        RemediationUrl = "https://learn.microsoft.com/microsoft-365/security/defender-endpoint/minimum-requirements"
                    });
                }

                // Check Intune enrollment for MDE onboarding via Intune
                Instance.Info("");
                Instance.Info("   [CHECK 2/2] INTUNE ENROLLMENT (for MDE onboarding)");
                var notInIntune = enrollmentData?.ConfigMgrOnlyDevices ?? 0;
                var inIntune = (enrollmentData?.CoManagedDevices ?? 0) + (enrollmentData?.CloudNativeDevices ?? 0);
                
                Instance.Info($"      ✅ Enrolled in Intune (MDE onboarding ready): {inIntune} devices");
                Instance.Info($"      🟡 ConfigMgr-only (can use ConfigMgr for MDE): {notInIntune} devices");
                Instance.Info($"         Note: MDE can be onboarded via ConfigMgr or Intune");

                signal.ReadyDevices = SafeReadyDevices(supportedCount, signal.TotalDevices, "EndpointSecurity");
                signal.TopBlockers = blockers.OrderByDescending(b => b.AffectedDeviceCount).Take(5).ToList();
                
                signal.Recommendations = new List<string>
                {
                    "Microsoft Defender for Endpoint provides cloud-powered protection and EDR.",
                    "Use Intune Security Baselines to configure Defender settings.",
                    supportedCount == signal.TotalDevices 
                        ? "All devices support MDE - ready to onboard!" 
                        : $"Upgrade {unsupportedOs} devices to enable MDE support."
                };

                Instance.Info("");
                Instance.Info($"   ═══════════════════════════════════════════════════════════════");
                Instance.Info($"   🛡️ ENDPOINT SECURITY READINESS RESULT: {signal.ReadinessPercentage}%");
                Instance.Info($"      Ready devices (OS supported): {signal.ReadyDevices} / {signal.TotalDevices}");
                Instance.Info($"      Blockers found: {blockers.Count}");
                Instance.Info($"   ═══════════════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Instance.Error($"Endpoint Security readiness assessment failed: {ex.Message}");
                Instance.Error($"Stack trace: {ex.StackTrace}");
            }

            return signal;
        }

        /// <summary>
        /// Assesses Application Readiness for migration to Intune/cloud-native management.
        /// Analyzes ConfigMgr application deployment types to determine migration complexity.
        /// v3.17.100 - Application Readiness feature
        /// 
        /// Complexity Categories:
        /// - Easy: MSI, MSIX - Use Enterprise App Catalog or Microsoft Store
        /// - Moderate: MSI (custom/LOB) - Package as Win32 app using Content Prep Tool
        /// - Needs Review: Script-based - Review installer logic before migration
        /// - Complex: App-V - Requires repackaging to Win32 or MSIX
        /// </summary>
        public async Task<CloudReadinessSignal> GetApplicationReadinessSignalAsync()
        {
            Instance.Info("┌─────────────────────────────────────────────────────────────────────────────────────────┐");
            Instance.Info("│ 📦 APPLICATION READINESS ASSESSMENT                                                     │");
            Instance.Info("└─────────────────────────────────────────────────────────────────────────────────────────┘");
            
            var signal = new CloudReadinessSignal
            {
                Id = "application-readiness",
                Name = "Application Readiness",
                Description = "Applications ready for Intune deployment",
                Icon = "📦",
                RelatedWorkload = "Application Management",
                LearnMoreUrl = "https://learn.microsoft.com/mem/intune/apps/apps-add"
            };

            try
            {
                // Get applications and deployment types from ConfigMgr
                Instance.Info("   Fetching applications from ConfigMgr...");
                var applications = await _configMgrService.GetApplicationsAsync();
                
                Instance.Info("   Fetching deployment types to analyze installer technologies...");
                var deploymentTypes = await _configMgrService.GetDeploymentTypesAsync();

                var totalApps = applications?.Count ?? 0;
                var deployedApps = applications?.Where(a => a.IsDeployed).ToList() ?? new List<ConfigMgrApplication>();
                
                Instance.Info($"   📱 Total applications in ConfigMgr: {totalApps}");
                Instance.Info($"   🚀 Deployed applications (active): {deployedApps.Count}");
                Instance.Info($"   📋 Total deployment types: {deploymentTypes?.Count ?? 0}");

                // Use deployed apps as the total for readiness calculation
                signal.TotalDevices = deployedApps.Count; // Reusing TotalDevices for app count in signal model
                
                if (signal.TotalDevices == 0)
                {
                    Instance.Warning("   ⚠️ No deployed applications found for assessment");
                    return signal;
                }

                // Group deployment types by technology
                var dtByTechnology = deploymentTypes?
                    .Where(dt => dt.IsEnabled)
                    .GroupBy(dt => dt.Technology ?? "Unknown")
                    .ToDictionary(g => g.Key, g => g.ToList()) ?? new Dictionary<string, List<ConfigMgrDeploymentType>>();

                // Count apps by technology category (based on their deployment types)
                var dtByApp = deploymentTypes?
                    .Where(dt => dt.IsEnabled)
                    .GroupBy(dt => dt.AppModelName)
                    .ToDictionary(g => g.Key, g => g.ToList()) ?? new Dictionary<string, List<ConfigMgrDeploymentType>>();

                int easyCount = 0;      // MSI, MSIX, Windows8AppInstaller, DeepLink
                int moderateCount = 0;  // MSI (will count separately as "LOB MSI")
                int needsReviewCount = 0; // Script
                int complexCount = 0;   // App-V
                int unknownCount = 0;   // Unknown or no deployment types

                var assessments = new List<AppMigrationAssessment>();

                foreach (var app in deployedApps)
                {
                    // Find deployment types for this application
                    // Try matching by app name (ConfigMgr uses LocalizedDisplayName for AppModelName correlation)
                    var appDTs = dtByApp.GetValueOrDefault(app.Name) ?? new List<ConfigMgrDeploymentType>();
                    
                    // Get the primary technology (lowest priority = primary)
                    var primaryDT = appDTs.OrderBy(dt => dt.Priority).FirstOrDefault();
                    var primaryTech = primaryDT?.Technology ?? "Unknown";

                    var assessment = new AppMigrationAssessment
                    {
                        Name = app.Name,
                        Version = app.Version,
                        Technology = primaryTech,
                        DeploymentTypes = appDTs,
                        DeploymentTypeCount = app.DeploymentTypeCount,
                        IsDeployed = app.IsDeployed
                    };

                    // Determine complexity based on technology
                    switch (primaryTech.ToUpperInvariant())
                    {
                        case "MSIX":
                        case "WINDOWS8APPINSTALLER":
                        case "DEEPLINK":
                            assessment.Complexity = MigrationComplexity.Easy;
                            assessment.RecommendedPath = "Deploy via Microsoft Store or Enterprise App Catalog";
                            assessment.MigrationGuideUrl = "https://learn.microsoft.com/mem/intune/apps/store-apps-microsoft";
                            easyCount++;
                            break;
                            
                        case "MSI":
                            // MSI is straightforward - use Win32 app model
                            assessment.Complexity = MigrationComplexity.Moderate;
                            assessment.RecommendedPath = "Package as Win32 app using Microsoft Win32 Content Prep Tool";
                            assessment.MigrationGuideUrl = "https://learn.microsoft.com/mem/intune/apps/apps-win32-app-management";
                            moderateCount++;
                            break;
                            
                        case "SCRIPT":
                            assessment.Complexity = MigrationComplexity.NeedsReview;
                            assessment.RecommendedPath = "Review installer logic, consider repackaging as Win32 app";
                            assessment.MigrationGuideUrl = "https://learn.microsoft.com/mem/intune/apps/apps-win32-prepare";
                            needsReviewCount++;
                            break;
                            
                        case "APPV5X":
                        case "APPV":
                            assessment.Complexity = MigrationComplexity.Complex;
                            assessment.RecommendedPath = "Requires repackaging - convert to MSIX or Win32";
                            assessment.MigrationGuideUrl = "https://learn.microsoft.com/windows/application-management/app-v/appv-for-windows";
                            complexCount++;
                            break;
                            
                        default:
                            // No deployment type info or unknown technology
                            if (appDTs.Count == 0)
                            {
                                assessment.Complexity = MigrationComplexity.NeedsReview;
                                assessment.RecommendedPath = "Deployment type information unavailable - manual review required";
                            }
                            else
                            {
                                assessment.Complexity = MigrationComplexity.NeedsReview;
                                assessment.RecommendedPath = $"Unknown technology '{primaryTech}' - manual review required";
                            }
                            assessment.MigrationGuideUrl = "https://learn.microsoft.com/mem/intune/apps/apps-add";
                            unknownCount++;
                            break;
                    }

                    assessments.Add(assessment);
                }

                // Ready apps = Easy + Moderate (have clear migration paths)
                var readyCount = easyCount + moderateCount;
                signal.ReadyDevices = SafeReadyDevices(readyCount, signal.TotalDevices, "Application Readiness");

                Instance.Info("");
                Instance.Info("   APPLICATION READINESS BREAKDOWN:");
                Instance.Info($"      ✅ Easy (Store/MSIX): {easyCount} apps");
                Instance.Info($"      🔵 Moderate (Win32/MSI): {moderateCount} apps");
                Instance.Info($"      🟡 Needs Review (Script): {needsReviewCount} apps");
                Instance.Info($"      🔴 Complex (App-V): {complexCount} apps");
                if (unknownCount > 0)
                {
                    Instance.Info($"      ❓ Unknown: {unknownCount} apps");
                }

                // Create blockers for apps needing attention
                var blockers = new List<ReadinessBlocker>();

                if (complexCount > 0)
                {
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "app-v-apps",
                        Name = "App-V Packages",
                        Description = "App-V virtualized applications require repackaging to MSIX or Win32 format for Intune deployment.",
                        AffectedDeviceCount = complexCount,
                        PercentageAffected = SafeBlockerPercentage(complexCount, signal.TotalDevices),
                        Severity = BlockerSeverity.High,
                        RemediationAction = "Convert App-V packages to MSIX using MSIX Packaging Tool or repackage as Win32",
                        RemediationUrl = "https://learn.microsoft.com/windows/application-management/msix-app-packaging-tool"
                    });
                }

                if (needsReviewCount > 0)
                {
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "script-installers",
                        Name = "Script-Based Installers",
                        Description = "Applications using custom scripts need review to ensure compatibility with Intune Win32 app deployment.",
                        AffectedDeviceCount = needsReviewCount,
                        PercentageAffected = SafeBlockerPercentage(needsReviewCount, signal.TotalDevices),
                        Severity = BlockerSeverity.Medium,
                        RemediationAction = "Review installer scripts and package as Win32 app with appropriate detection rules",
                        RemediationUrl = "https://learn.microsoft.com/mem/intune/apps/apps-win32-prepare"
                    });
                }

                if (unknownCount > 0)
                {
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "unknown-technology",
                        Name = "Unknown Deployment Technology",
                        Description = "Applications with unrecognized or missing deployment type information require manual assessment.",
                        AffectedDeviceCount = unknownCount,
                        PercentageAffected = SafeBlockerPercentage(unknownCount, signal.TotalDevices),
                        Severity = BlockerSeverity.Low,
                        RemediationAction = "Review application deployment types in ConfigMgr and determine migration path",
                        RemediationUrl = "https://learn.microsoft.com/mem/intune/apps/apps-add"
                    });
                }

                signal.TopBlockers = blockers.OrderByDescending(b => b.Severity).ThenByDescending(b => b.AffectedDeviceCount).Take(5).ToList();
                signal.Recommendations = GenerateApplicationReadinessRecommendations(signal, easyCount, moderateCount, needsReviewCount, complexCount);

                Instance.Info("");
                Instance.Info($"   ═══════════════════════════════════════════════════════════════");
                Instance.Info($"   📦 APPLICATION READINESS RESULT: {signal.ReadinessPercentage}%");
                Instance.Info($"      Ready apps: {signal.ReadyDevices} / {signal.TotalDevices}");
                Instance.Info($"      Blockers found: {blockers.Count}");
                Instance.Info($"   ═══════════════════════════════════════════════════════════════");

                // Track telemetry for Application Readiness
                var technologyBreakdown = deploymentTypes?
                    .Where(dt => dt.IsEnabled)
                    .GroupBy(dt => dt.Technology ?? "Unknown")
                    .ToDictionary(g => g.Key, g => g.Count());

                AzureTelemetryService.Instance.TrackApplicationReadinessAssessed(
                    totalApps: totalApps,
                    deployedApps: deployedApps.Count,
                    easyApps: easyCount,
                    moderateApps: moderateCount,
                    needsReviewApps: needsReviewCount,
                    complexApps: complexCount,
                    unknownApps: unknownCount,
                    readinessPercentage: signal.ReadinessPercentage,
                    technologyBreakdown: technologyBreakdown
                );
            }
            catch (Exception ex)
            {
                Instance.Error($"Application readiness assessment failed: {ex.Message}");
                Instance.Error($"Stack trace: {ex.StackTrace}");
            }

            return signal;
        }

        #region Helper Methods

        private List<string> GenerateApplicationReadinessRecommendations(CloudReadinessSignal signal, int easyCount, int moderateCount, int needsReviewCount, int complexCount)
        {
            var recommendations = new List<string>();

            if (signal.ReadinessPercentage >= 80)
            {
                recommendations.Add("Excellent! Most applications have clear migration paths to Intune.");
                recommendations.Add("Start migrating Easy and Moderate apps while planning for complex ones.");
            }
            else if (signal.ReadinessPercentage >= 60)
            {
                recommendations.Add("Good progress! Focus on packaging MSI apps as Win32 for Intune.");
            }
            else
            {
                recommendations.Add("Many applications need attention before Intune migration.");
            }

            if (moderateCount > 0)
            {
                recommendations.Add($"{moderateCount} MSI apps can use the Win32 app model - use Microsoft Win32 Content Prep Tool to package.");
            }

            if (easyCount > 0)
            {
                recommendations.Add($"{easyCount} apps may be available in Enterprise App Catalog or Microsoft Store - check before packaging.");
            }

            if (complexCount > 0)
            {
                recommendations.Add($"Plan App-V migration strategy: {complexCount} apps need repackaging to MSIX or Win32.");
            }

            if (needsReviewCount > 0)
            {
                recommendations.Add($"Review {needsReviewCount} script-based apps for Win32 compatibility.");
            }

            return recommendations;
        }

        private List<string> GenerateAutopilotRecommendations(CloudReadinessSignal signal, List<ReadinessBlocker> blockers)
        {
            var recommendations = new List<string>();

            if (signal.ReadinessPercentage >= 80)
            {
                recommendations.Add("Excellent! Most devices are ready for Autopilot deployment.");
                recommendations.Add("Start with a pilot group of ready devices.");
            }
            else if (signal.ReadinessPercentage >= 60)
            {
                recommendations.Add("Good progress on Autopilot readiness.");
            }
            else
            {
                recommendations.Add("Focus on addressing blockers before Autopilot rollout.");
            }

            if (blockers.Any(b => b.Id == "no-tpm20"))
            {
                recommendations.Add("Plan hardware refresh for devices without TPM 2.0.");
            }

            if (blockers.Any(b => b.Id == "not-aad-joined"))
            {
                recommendations.Add("Configure Hybrid Azure AD Join as a stepping stone to Autopilot.");
            }

            return recommendations;
        }

        private List<string> GenerateWindows11Recommendations(CloudReadinessSignal signal, List<ReadinessBlocker> blockers)
        {
            var recommendations = new List<string>();

            if (signal.ReadinessPercentage >= 80)
            {
                recommendations.Add("Great! Most devices meet Windows 11 hardware requirements.");
            }
            else
            {
                recommendations.Add("Identify devices that need hardware upgrades for Windows 11.");
            }

            if (blockers.Any(b => b.Id == "no-tpm20"))
            {
                recommendations.Add("TPM 2.0 is the most common blocker - check BIOS settings first.");
            }

            recommendations.Add("Use Windows Update for Business to manage Windows 11 feature updates.");

            return recommendations;
        }

        private List<string> GenerateCloudNativeRecommendations(CloudReadinessSignal signal, List<ReadinessBlocker> blockers, WorkloadAuthoritySummary? workloadAuthority = null)
        {
            var recommendations = new List<string>();

            if (signal.ReadinessPercentage >= 50)
            {
                recommendations.Add("Good cloud-native progress! Continue transitioning remaining devices.");
            }
            else
            {
                recommendations.Add("Start with co-management as a bridge to cloud-native.");
            }

            if (blockers.Any(b => b.Id == "hybrid-joined"))
            {
                recommendations.Add("For new devices, consider direct Azure AD Join instead of Hybrid.");
            }

            if (blockers.Any(b => b.Id == "configmgr-only"))
            {
                recommendations.Add("Enable co-management to start the Intune journey.");
            }

            // Workload-specific recommendations based on Graph API data
            if (workloadAuthority != null && workloadAuthority.TotalCoManagedDevices > 0)
            {
                var lowestAdoption = workloadAuthority.WorkloadIntuneAdoptionCounts
                    .OrderBy(w => w.Value)
                    .FirstOrDefault();

                if (lowestAdoption.Value < workloadAuthority.TotalCoManagedDevices)
                {
                    recommendations.Add($"Move '{lowestAdoption.Key}' workload slider to Intune - currently lowest adoption.");
                }

                if (workloadAuthority.DevicesReadyForCloudNative > 0)
                {
                    recommendations.Add($"{workloadAuthority.DevicesReadyForCloudNative} co-managed devices have ALL workloads on Intune - consider removing ConfigMgr client.");
                }
            }

            return recommendations;
        }

        #endregion

        #region Comparison Methods

        /// <summary>
        /// Gets Update Management Comparison data between cloud-native (Intune) and ConfigMgr-managed devices.
        /// Compares overall compliance state and sync/scan frequency.
        /// </summary>
        public async Task<UpdateManagementComparison> GetUpdateManagementComparisonAsync()
        {
            Instance.Info("┌─────────────────────────────────────────────────────────────────────────────────────────┐");
            Instance.Info("│ 📊 UPDATE MANAGEMENT COMPARISON (Intune vs ConfigMgr)                                   │");
            Instance.Info("└─────────────────────────────────────────────────────────────────────────────────────────┘");

            var comparison = new UpdateManagementComparison();

            try
            {
                // Get Intune managed Windows workstations (excludes servers and MDE-only devices)
                Instance.Info("   Fetching Intune device compliance data (workstations only, excluding servers)...");
                var intuneWindowsDevices = await _graphService.GetWindowsWorkstationsAsync();

                comparison.IntuneDeviceCount = intuneWindowsDevices.Count;

                if (intuneWindowsDevices.Count > 0)
                {
                    // Calculate compliance percentage (Compliant vs all states)
                    var compliantCount = intuneWindowsDevices.Count(d => 
                        d.ComplianceState == Microsoft.Graph.Models.ComplianceState.Compliant);
                    comparison.IntuneCompliancePercentage = Math.Round((double)compliantCount / intuneWindowsDevices.Count * 100, 1);

                    // Calculate average days since last sync
                    var devicesWithSync = intuneWindowsDevices.Where(d => d.LastSyncDateTime != null).ToList();
                    if (devicesWithSync.Any())
                    {
                        var totalDays = devicesWithSync.Sum(d => 
                            (DateTime.UtcNow - d.LastSyncDateTime.Value.UtcDateTime).TotalDays);
                        comparison.IntuneAvgDaysSinceSync = Math.Round(totalDays / devicesWithSync.Count, 1);
                    }
                    
                    Instance.Info($"   ✓ Intune: {comparison.IntuneDeviceCount} devices, {comparison.IntuneCompliancePercentage}% compliant, avg {comparison.IntuneAvgDaysSinceSync} days since sync");
                }
                else
                {
                    Instance.Info("   ⚠️ No Intune Windows devices found");
                }

                // Get ConfigMgr update compliance
                Instance.Info("   Fetching ConfigMgr update compliance data...");
                var configMgrCompliance = await _configMgrService.GetSoftwareUpdateComplianceAsync();
                
                comparison.ConfigMgrDeviceCount = configMgrCompliance.Count;

                if (configMgrCompliance.Count > 0)
                {
                    // Status 1 = Compliant in SMS_UpdateComplianceStatus
                    var compliantCount = configMgrCompliance.Count(c => c.ComplianceStatus == 1);
                    comparison.ConfigMgrCompliancePercentage = Math.Round((double)compliantCount / configMgrCompliance.Count * 100, 1);

                    // Calculate average days since last compliance check
                    var devicesWithCheck = configMgrCompliance.Where(c => c.LastCheckTime != null).ToList();
                    if (devicesWithCheck.Any())
                    {
                        var totalDays = devicesWithCheck.Sum(c => 
                            (DateTime.UtcNow - c.LastCheckTime.Value).TotalDays);
                        comparison.ConfigMgrAvgDaysSinceScan = Math.Round(totalDays / devicesWithCheck.Count, 1);
                    }
                    
                    Instance.Info($"   ✓ ConfigMgr: {comparison.ConfigMgrDeviceCount} devices, {comparison.ConfigMgrCompliancePercentage}% compliant, avg {comparison.ConfigMgrAvgDaysSinceScan} days since scan");
                }
                else
                {
                    Instance.Info("   ⚠️ No ConfigMgr update compliance data found");
                }

                Instance.Info($"   📈 COMPARISON RESULT: {comparison.ComparisonIcon} {comparison.ComparisonSummary}");
            }
            catch (Exception ex)
            {
                Instance.Error($"Update Management Comparison failed: {ex.Message}");
            }

            return comparison;
        }

        /// <summary>
        /// Gets OS Currency Comparison data showing Windows 11 adoption rates between
        /// cloud-native (Intune) and ConfigMgr-managed devices.
        /// </summary>
        public async Task<OSCurrencyComparison> GetOSCurrencyComparisonAsync()
        {
            Instance.Info("┌─────────────────────────────────────────────────────────────────────────────────────────┐");
            Instance.Info("│ 💻 OS CURRENCY COMPARISON (Intune vs ConfigMgr)                                          │");
            Instance.Info("└─────────────────────────────────────────────────────────────────────────────────────────┘");

            var comparison = new OSCurrencyComparison();

            try
            {
                // Get Intune devices with OS version
                Instance.Info("   Fetching Intune device OS version data...");
                var intuneDevices = await _graphService.GetCachedManagedDevicesAsync();
                
                // Filter to Windows devices with OS version info
                var intuneWindowsDevices = intuneDevices
                    .Where(d => d.OperatingSystem?.Contains("Windows", StringComparison.OrdinalIgnoreCase) == true 
                                && !string.IsNullOrEmpty(d.OsVersion))
                    .ToList();

                comparison.IntuneDeviceCount = intuneWindowsDevices.Count;

                if (intuneWindowsDevices.Count > 0)
                {
                    // Group by OS version and create distribution
                    var intuneGroups = intuneWindowsDevices
                        .GroupBy(d => GetWindowsVersionGroup(d.OsVersion ?? ""))
                        .Select(g => new OSVersionGroup
                        {
                            OSVersion = g.Key.version,
                            FriendlyName = g.Key.friendlyName,
                            DeviceCount = g.Count(),
                            Percentage = Math.Round((double)g.Count() / intuneWindowsDevices.Count * 100, 1)
                        })
                        .OrderByDescending(g => g.FriendlyName)
                        .ToList();

                    comparison.IntuneDistribution = intuneGroups;
                    comparison.IntuneWindows11Percentage = intuneGroups
                        .Where(g => g.FriendlyName.Contains("11"))
                        .Sum(g => g.Percentage);
                    comparison.IntuneLatestBuildPercentage = intuneGroups
                        .FirstOrDefault(g => g.FriendlyName.Contains("24H2"))?.Percentage ?? 0;

                    Instance.Info($"   ✓ Intune: {comparison.IntuneDeviceCount} devices, {comparison.IntuneWindows11Percentage}% on Windows 11");
                }
                else
                {
                    Instance.Info("   ⚠️ No Intune Windows devices with OS version found");
                }

                // Get ConfigMgr OS details
                Instance.Info("   Fetching ConfigMgr device OS version data...");
                var configMgrOSDetails = await _configMgrService.GetOSDetailsAsync();

                // Filter to Windows 10/11 devices
                var configMgrWindowsDevices = configMgrOSDetails
                    .Where(os => os.Caption?.Contains("Windows 10", StringComparison.OrdinalIgnoreCase) == true
                                || os.Caption?.Contains("Windows 11", StringComparison.OrdinalIgnoreCase) == true)
                    .ToList();

                comparison.ConfigMgrDeviceCount = configMgrWindowsDevices.Count;

                if (configMgrWindowsDevices.Count > 0)
                {
                    // Group by OS version using build number
                    var configMgrGroups = configMgrWindowsDevices
                        .GroupBy(os => GetWindowsVersionGroupFromBuild(os.BuildNumber ?? "", os.Caption ?? ""))
                        .Select(g => new OSVersionGroup
                        {
                            OSVersion = g.Key.version,
                            FriendlyName = g.Key.friendlyName,
                            DeviceCount = g.Count(),
                            Percentage = Math.Round((double)g.Count() / configMgrWindowsDevices.Count * 100, 1)
                        })
                        .OrderByDescending(g => g.FriendlyName)
                        .ToList();

                    comparison.ConfigMgrDistribution = configMgrGroups;
                    comparison.ConfigMgrWindows11Percentage = configMgrGroups
                        .Where(g => g.FriendlyName.Contains("11"))
                        .Sum(g => g.Percentage);
                    comparison.ConfigMgrLatestBuildPercentage = configMgrGroups
                        .FirstOrDefault(g => g.FriendlyName.Contains("24H2"))?.Percentage ?? 0;

                    Instance.Info($"   ✓ ConfigMgr: {comparison.ConfigMgrDeviceCount} devices, {comparison.ConfigMgrWindows11Percentage}% on Windows 11");
                }
                else
                {
                    Instance.Info("   ⚠️ No ConfigMgr Windows 10/11 devices found");
                }

                Instance.Info($"   🚀 COMPARISON RESULT: {comparison.ComparisonIcon} {comparison.ComparisonSummary}");
            }
            catch (Exception ex)
            {
                Instance.Error($"OS Currency Comparison failed: {ex.Message}");
            }

            return comparison;
        }

        /// <summary>
        /// Parse Intune OsVersion string (like "10.0.19045.4780") to Windows version group.
        /// </summary>
        private (string version, string friendlyName) GetWindowsVersionGroup(string osVersion)
        {
            if (string.IsNullOrEmpty(osVersion)) return ("Unknown", "Unknown");

            // Parse build number from version string (e.g., "10.0.19045.4780" -> 19045)
            var parts = osVersion.Split('.');
            if (parts.Length >= 3 && int.TryParse(parts[2], out var buildNumber))
            {
                return GetFriendlyNameFromBuild(buildNumber);
            }

            return (osVersion, $"Unknown ({osVersion})");
        }

        /// <summary>
        /// Parse ConfigMgr build number to Windows version group.
        /// </summary>
        private (string version, string friendlyName) GetWindowsVersionGroupFromBuild(string buildNumber, string caption)
        {
            if (int.TryParse(buildNumber, out var build))
            {
                return GetFriendlyNameFromBuild(build);
            }

            // Fallback to caption
            if (caption.Contains("11"))
                return ("11", "Windows 11");
            if (caption.Contains("10"))
                return ("10", "Windows 10");

            return ("Unknown", caption);
        }

        /// <summary>
        /// Map Windows build number to friendly version name.
        /// Source: https://learn.microsoft.com/windows/release-health/
        /// </summary>
        private (string version, string friendlyName) GetFriendlyNameFromBuild(int buildNumber)
        {
            return buildNumber switch
            {
                // Windows 11 builds
                >= 26100 => ("11.24H2", "Windows 11 24H2"),
                >= 22631 => ("11.23H2", "Windows 11 23H2"),
                >= 22621 => ("11.22H2", "Windows 11 22H2"),
                >= 22000 => ("11.21H2", "Windows 11 21H2"),
                
                // Windows 10 builds
                >= 19045 => ("10.22H2", "Windows 10 22H2"),
                >= 19044 => ("10.21H2", "Windows 10 21H2"),
                >= 19043 => ("10.21H1", "Windows 10 21H1"),
                >= 19042 => ("10.20H2", "Windows 10 20H2"),
                >= 19041 => ("10.2004", "Windows 10 2004"),
                >= 18363 => ("10.1909", "Windows 10 1909"),
                >= 18362 => ("10.1903", "Windows 10 1903"),
                >= 17763 => ("10.1809", "Windows 10 1809"),
                >= 17134 => ("10.1803", "Windows 10 1803"),
                
                _ => ("10.Older", "Windows 10 (Older)")
            };
        }

        #endregion
    }
}
