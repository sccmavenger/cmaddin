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
                var autopilotTask = GetAutopilotReadinessSignalAsync();
                var windows11Task = GetWindows11ReadinessSignalAsync();
                var cloudNativeTask = GetCloudNativeReadinessSignalAsync();
                var identityTask = GetIdentityReadinessSignalAsync();
                var wufbTask = GetWufbReadinessSignalAsync();
                var endpointSecurityTask = GetEndpointSecurityReadinessSignalAsync();

                await Task.WhenAll(autopilotTask, windows11Task, cloudNativeTask, identityTask, wufbTask, endpointSecurityTask);

                dashboard.Signals.Add(await autopilotTask);
                dashboard.Signals.Add(await windows11Task);
                dashboard.Signals.Add(await cloudNativeTask);
                dashboard.Signals.Add(await identityTask);
                dashboard.Signals.Add(await wufbTask);
                dashboard.Signals.Add(await endpointSecurityTask);

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
                Name = "Autopilot Registration",
                Description = "ConfigMgr devices registered to Windows Autopilot",
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

                var configMgrCount = configMgrDevices?.Count ?? 0;
                var autopilotCount = autopilotDevices?.Count ?? 0;
                
                Instance.Info($"   📱 ConfigMgr Windows 10/11 devices: {configMgrCount}");
                Instance.Info($"   🚀 Autopilot registered devices: {autopilotCount}");
                Instance.Info($"   📊 OS detail records retrieved: {osDetails?.Count ?? 0}");
                
                // TotalDevices = ConfigMgr devices (these are the ones we want to register)
                signal.TotalDevices = configMgrCount;
                
                if (signal.TotalDevices == 0)
                {
                    Instance.Warning("   ⚠️ No ConfigMgr devices found for Autopilot registration assessment");
                    return signal;
                }

                var blockers = new List<ReadinessBlocker>();

                // Match ConfigMgr devices to Autopilot by serial number
                // Devices already in Autopilot are "ready", devices not in Autopilot need registration
                var autopilotSerials = new HashSet<string>(
                    autopilotDevices?.Select(a => a.SerialNumber?.ToUpperInvariant() ?? "").Where(s => !string.IsNullOrEmpty(s)) ?? new List<string>());
                
                Instance.Info($"   📋 Autopilot serial numbers found: {autopilotSerials.Count}");
                
                // Count devices already registered vs not registered
                // Note: ConfigMgr doesn't always have serial numbers, so we estimate based on counts
                var devicesNotRegistered = configMgrCount > autopilotCount ? configMgrCount - autopilotCount : 0;
                var devicesRegistered = Math.Min(configMgrCount, autopilotCount);
                
                Instance.Info("");
                Instance.Info("   [CHECK 1/2] AUTOPILOT REGISTRATION STATUS");
                Instance.Info($"      ✅ Already registered to Autopilot: ~{devicesRegistered} devices (estimated)");
                Instance.Info($"      ⚠️ Not yet registered: ~{devicesNotRegistered} devices (estimated)");
                
                if (devicesNotRegistered > 0)
                {
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "not-autopilot-registered",
                        Name = "Not Registered to Autopilot",
                        Description = "These devices are not yet registered to Windows Autopilot. Register them to enable Autopilot provisioning.",
                        AffectedDeviceCount = devicesNotRegistered,
                        PercentageAffected = SafeBlockerPercentage(devicesNotRegistered, signal.TotalDevices),
                        Severity = BlockerSeverity.Medium,
                        RemediationAction = "Register devices to Autopilot via hardware hash upload or OEM registration",
                        RemediationUrl = "https://learn.microsoft.com/mem/autopilot/add-devices"
                    });
                }

                // Check OS version requirement (Windows 10 1809+ or Windows 11)
                Instance.Info("");
                Instance.Info("   [CHECK 2/2] OS VERSION REQUIREMENT (Windows 10 1809+ or Windows 11)");
                var osLookup = osDetails?.ToDictionary(o => o.ResourceId) ?? new Dictionary<int, OSDetails>();
                
                // BUG FIX: Ensure configMgrDevices is not null before using LINQ
                var safeConfigMgrDevices = configMgrDevices ?? new List<ConfigMgrDevice>();
                
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
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "unsupported-os",
                        Name = "Unsupported OS Version",
                        Description = "Windows 10 version 1809 or later is required for Autopilot registration.",
                        AffectedDeviceCount = unsupportedOsCount,
                        PercentageAffected = SafeBlockerPercentage(unsupportedOsCount, signal.TotalDevices),
                        Severity = BlockerSeverity.High,
                        RemediationAction = "Upgrade to Windows 10 1809+ or Windows 11",
                        RemediationUrl = "https://learn.microsoft.com/windows/release-health/"
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

                var noTpm20 = signal.TotalDevices - devicesWithTpm20.Count;
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
                        RemediationUrl = "https://support.microsoft.com/windows/enable-tpm-2-0-on-your-pc"
                    });
                    
                    foreach (var d in windows10Devices.Where(d => 
                        !tpmLookup.TryGetValue(d.ResourceId, out var tpm) || 
                        !tpm.IsPresent || !tpm.IsEnabled ||
                        string.IsNullOrEmpty(tpm.SpecVersion) || 
                        !(tpm.SpecVersion.StartsWith("2.") || tpm.SpecVersion.Contains("2.0"))))
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
        /// Assesses Cloud-Native readiness (Entra Join + Intune only, no ConfigMgr).
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
                Description = "Ready for cloud-only management (Entra + Intune, no ConfigMgr)",
                Icon = "☁️",
                RelatedWorkload = "Device Management",
                LearnMoreUrl = "https://learn.microsoft.com/mem/intune/fundamentals/cloud-native-endpoints-overview"
            };

            try
            {
                Instance.Info("   Fetching enrollment data from Graph API and ConfigMgr...");
                var enrollmentData = await _graphService.GetDeviceEnrollmentAsync();
                var devices = await _configMgrService.GetWindows1011DevicesAsync();

                signal.TotalDevices = enrollmentData?.TotalDevices ?? devices?.Count ?? 0;
                
                Instance.Info($"   📱 Total devices: {signal.TotalDevices}");
                Instance.Info("");
                Instance.Info("   DEVICE MANAGEMENT STATE BREAKDOWN:");
                Instance.Info($"      ☁️ Already Cloud-Native (Intune-only, no ConfigMgr): {enrollmentData?.CloudNativeDevices ?? 0}");
                Instance.Info($"      ✅ Entra ID Joined (ready for cloud-native): {enrollmentData?.AzureADOnlyDevices ?? 0}");
                Instance.Info($"      🔄 Co-Managed (ConfigMgr + Intune): {enrollmentData?.CoManagedDevices ?? 0}");
                Instance.Info($"      🟡 Hybrid Entra ID Joined: {enrollmentData?.HybridJoinedDevices ?? 0}");
                Instance.Info($"      🔴 ConfigMgr-Only (not in Intune): {enrollmentData?.ConfigMgrOnlyDevices ?? 0}");
                Instance.Info($"      🔴 On-Prem AD Only (no cloud identity): {enrollmentData?.OnPremDomainOnlyDevices ?? 0}");
                Instance.Info($"      ⚫ Workgroup devices: {enrollmentData?.WorkgroupDevices ?? 0}");
                
                if (signal.TotalDevices == 0)
                {
                    Instance.Warning("   ⚠️ No devices found for cloud-native assessment");
                    return signal;
                }

                var blockers = new List<ReadinessBlocker>();

                // Already cloud-native devices
                var alreadyCloudNative = enrollmentData?.CloudNativeDevices ?? 0;
                
                // Devices that could be cloud-native (AAD joined + Intune)
                var aadJoinedWithIntune = enrollmentData?.AzureADOnlyDevices ?? 0;
                
                Instance.Info("");
                Instance.Info("   BLOCKERS ANALYSIS:");
                
                // Hybrid joined devices need more work
                var hybridJoined = enrollmentData?.HybridJoinedDevices ?? 0;
                if (hybridJoined > 0)
                {
                    Instance.Info($"      🟡 Hybrid Entra ID Joined: {hybridJoined} devices");
                    Instance.Info($"         → These devices have on-prem AD dependencies");
                    Instance.Info($"         → Need to migrate from Hybrid to cloud-only Entra ID join");
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "hybrid-joined",
                        Name = "Hybrid Entra ID Joined",
                        Description = "These devices are Hybrid joined and have on-premises AD dependencies.",
                        AffectedDeviceCount = hybridJoined,
                        PercentageAffected = SafeBlockerPercentage(hybridJoined, signal.TotalDevices),
                        Severity = BlockerSeverity.Medium,
                        RemediationAction = "Plan migration from Hybrid to cloud-only Entra ID join",
                        RemediationUrl = "https://learn.microsoft.com/entra/identity/devices/device-join-plan"
                    });
                }

                // On-prem only devices
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

                // ConfigMgr-only devices (not enrolled in Intune)
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
                        Severity = BlockerSeverity.Medium,
                        RemediationAction = "Enable co-management and enroll in Intune",
                        RemediationUrl = "https://learn.microsoft.com/mem/configmgr/comanage/how-to-enable"
                    });
                }

                // Ready = Already cloud-native + AAD-only devices with Intune
                signal.ReadyDevices = SafeReadyDevices(alreadyCloudNative + aadJoinedWithIntune, signal.TotalDevices, "CloudNative");
                signal.TopBlockers = blockers.OrderByDescending(b => b.AffectedDeviceCount).Take(5).ToList();
                
                signal.Recommendations = GenerateCloudNativeRecommendations(signal, blockers);

                Instance.Info("");
                Instance.Info($"   ═══════════════════════════════════════════════════════════════");
                Instance.Info($"   ☁️ CLOUD-NATIVE READINESS RESULT: {signal.ReadinessPercentage}%");
                Instance.Info($"      Ready devices: {signal.ReadyDevices} / {signal.TotalDevices}");
                Instance.Info($"      (Cloud-native: {alreadyCloudNative} + AAD-only: {aadJoinedWithIntune})");
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
                Description = "Ready for cloud identity (Entra ID/Azure AD)",
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
                Description = "Ready for Windows Update for Business (WSUS/SCCM → WUfB)",
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
                Description = "Ready for Microsoft Defender for Endpoint (SCEP → MDE)",
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

        #region Helper Methods

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

        private List<string> GenerateCloudNativeRecommendations(CloudReadinessSignal signal, List<ReadinessBlocker> blockers)
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

            return recommendations;
        }

        #endregion
    }
}
