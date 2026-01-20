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
        /// Gets the complete Cloud Readiness Dashboard with all signals.
        /// </summary>
        public async Task<CloudReadinessDashboard> GetCloudReadinessDashboardAsync()
        {
            Instance.Info("=== CLOUD READINESS ASSESSMENT START ===");
            
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

                Instance.Info($"=== CLOUD READINESS ASSESSMENT COMPLETE ===");
                Instance.Info($"   Overall Readiness: {dashboard.OverallReadiness}%");
                Instance.Info($"   Total Devices Assessed: {dashboard.TotalAssessedDevices}");
                Instance.Info($"   Total Blockers Identified: {dashboard.TotalBlockersIdentified}");
            }
            catch (Exception ex)
            {
                Instance.Error($"Cloud Readiness Assessment failed: {ex.Message}");
            }

            return dashboard;
        }

        /// <summary>
        /// Assesses Autopilot readiness (SCCM OSD → Autopilot transition).
        /// Requirements: TPM 2.0, UEFI, Secure Boot, Windows 10 1809+, AAD/Hybrid joined
        /// </summary>
        public async Task<CloudReadinessSignal> GetAutopilotReadinessSignalAsync()
        {
            Instance.Info("Assessing Autopilot readiness...");
            
            var signal = new CloudReadinessSignal
            {
                Id = "autopilot",
                Name = "Autopilot Readiness",
                Description = "Ready for Windows Autopilot deployment (SCCM OSD → Autopilot)",
                Icon = "🚀",
                RelatedWorkload = "Device Provisioning",
                LearnMoreUrl = "https://learn.microsoft.com/mem/autopilot/windows-autopilot"
            };

            try
            {
                // Get device data from ConfigMgr
                var devices = await _configMgrService.GetWindows1011DevicesAsync();
                var tpmStatus = await _configMgrService.GetTpmStatusAsync();
                var osDetails = await _configMgrService.GetOSDetailsAsync();
                var enrollmentData = await _graphService.GetDeviceEnrollmentAsync();

                signal.TotalDevices = devices?.Count ?? 0;
                
                if (signal.TotalDevices == 0)
                {
                    Instance.Warning("No devices found for Autopilot readiness assessment");
                    return signal;
                }

                var blockers = new List<ReadinessBlocker>();
                var readyDeviceIds = new HashSet<int>(devices.Select(d => d.ResourceId));

                // Check TPM 2.0 requirement
                var tpmLookup = tpmStatus?.ToDictionary(t => t.ResourceId) ?? new Dictionary<int, TpmStatus>();
                var devicesWithTpm20 = devices.Count(d => 
                    tpmLookup.TryGetValue(d.ResourceId, out var tpm) && 
                    tpm.IsPresent && tpm.IsEnabled &&
                    !string.IsNullOrEmpty(tpm.SpecVersion) && 
                    (tpm.SpecVersion.StartsWith("2.") || tpm.SpecVersion.Contains("2.0")));

                var noTpm20Count = signal.TotalDevices - devicesWithTpm20;
                if (noTpm20Count > 0)
                {
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "no-tpm20",
                        Name = "Missing TPM 2.0",
                        Description = "TPM 2.0 is required for Autopilot. These devices have no TPM or TPM 1.2.",
                        AffectedDeviceCount = noTpm20Count,
                        PercentageAffected = Math.Round((double)noTpm20Count / signal.TotalDevices * 100, 1),
                        Severity = BlockerSeverity.Critical,
                        RemediationAction = "Enable TPM in BIOS or upgrade hardware",
                        RemediationUrl = "https://learn.microsoft.com/mem/autopilot/autopilot-requirements"
                    });
                    
                    // Remove devices without TPM 2.0 from ready set
                    foreach (var d in devices.Where(d => !tpmLookup.TryGetValue(d.ResourceId, out var tpm) || 
                        !tpm.IsPresent || !tpm.IsEnabled || 
                        string.IsNullOrEmpty(tpm.SpecVersion) || 
                        !(tpm.SpecVersion.StartsWith("2.") || tpm.SpecVersion.Contains("2.0"))))
                    {
                        readyDeviceIds.Remove(d.ResourceId);
                    }
                }

                // Check OS version requirement (Windows 10 1809+ or Windows 11)
                var osLookup = osDetails?.ToDictionary(o => o.ResourceId) ?? new Dictionary<int, OSDetails>();
                var unsupportedOsCount = devices.Count(d =>
                {
                    if (!osLookup.TryGetValue(d.ResourceId, out var os)) return true;
                    if (string.IsNullOrEmpty(os.BuildNumber)) return true;
                    
                    // Windows 10 1809 = Build 17763, Windows 11 = Build 22000+
                    if (int.TryParse(os.BuildNumber, out var build))
                    {
                        return build < 17763;
                    }
                    return true;
                });

                if (unsupportedOsCount > 0)
                {
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "unsupported-os",
                        Name = "Unsupported OS Version",
                        Description = "Windows 10 version 1809 or later is required for Autopilot.",
                        AffectedDeviceCount = unsupportedOsCount,
                        PercentageAffected = Math.Round((double)unsupportedOsCount / signal.TotalDevices * 100, 1),
                        Severity = BlockerSeverity.High,
                        RemediationAction = "Upgrade to Windows 10 1809+ or Windows 11",
                        RemediationUrl = "https://learn.microsoft.com/windows/release-health/"
                    });
                    
                    foreach (var d in devices.Where(d => 
                        !osLookup.TryGetValue(d.ResourceId, out var os) || 
                        string.IsNullOrEmpty(os.BuildNumber) ||
                        !int.TryParse(os.BuildNumber, out var build) || build < 17763))
                    {
                        readyDeviceIds.Remove(d.ResourceId);
                    }
                }

                // Check identity requirement (AAD Joined or Hybrid Joined)
                var notAadJoined = (enrollmentData?.OnPremDomainOnlyDevices ?? 0) + (enrollmentData?.WorkgroupDevices ?? 0);
                if (notAadJoined > 0)
                {
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "not-aad-joined",
                        Name = "Not Azure AD Joined",
                        Description = "Devices must be Azure AD joined or Hybrid joined for Autopilot.",
                        AffectedDeviceCount = notAadJoined,
                        PercentageAffected = Math.Round((double)notAadJoined / signal.TotalDevices * 100, 1),
                        Severity = BlockerSeverity.High,
                        RemediationAction = "Configure Hybrid Azure AD Join or Azure AD Join",
                        RemediationUrl = "https://learn.microsoft.com/entra/identity/devices/hybrid-join-plan"
                    });
                }

                signal.ReadyDevices = readyDeviceIds.Count;
                signal.TopBlockers = blockers.OrderByDescending(b => b.AffectedDeviceCount).Take(5).ToList();
                
                signal.Recommendations = GenerateAutopilotRecommendations(signal, blockers);

                Instance.Info($"   Autopilot Readiness: {signal.ReadinessPercentage}% ({signal.ReadyDevices}/{signal.TotalDevices})");
            }
            catch (Exception ex)
            {
                Instance.Error($"Autopilot readiness assessment failed: {ex.Message}");
            }

            return signal;
        }

        /// <summary>
        /// Assesses Windows 11 upgrade readiness.
        /// Requirements: TPM 2.0, UEFI with Secure Boot, 4GB RAM, 64GB storage, compatible CPU
        /// </summary>
        public async Task<CloudReadinessSignal> GetWindows11ReadinessSignalAsync()
        {
            Instance.Info("Assessing Windows 11 readiness...");
            
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
                var devices = await _configMgrService.GetWindows1011DevicesAsync();
                var tpmStatus = await _configMgrService.GetTpmStatusAsync();
                var osDetails = await _configMgrService.GetOSDetailsAsync();

                // Only assess Windows 10 devices (Windows 11 devices are already upgraded)
                var windows10Devices = devices?.Where(d => 
                    d.OperatingSystem?.Contains("10") == true && 
                    d.OperatingSystem?.Contains("11") != true).ToList() ?? new List<ConfigMgrDevice>();

                signal.TotalDevices = windows10Devices.Count;
                
                if (signal.TotalDevices == 0)
                {
                    signal.TotalDevices = devices?.Count ?? 0;
                    signal.ReadyDevices = signal.TotalDevices; // All devices are already Windows 11
                    Instance.Info("   All devices are already Windows 11 or no Windows 10 devices found");
                    return signal;
                }

                var blockers = new List<ReadinessBlocker>();
                var readyDeviceIds = new HashSet<int>(windows10Devices.Select(d => d.ResourceId));

                // Check TPM 2.0
                var tpmLookup = tpmStatus?.ToDictionary(t => t.ResourceId) ?? new Dictionary<int, TpmStatus>();
                var noTpm20 = windows10Devices.Count(d => 
                    !tpmLookup.TryGetValue(d.ResourceId, out var tpm) || 
                    !tpm.IsPresent || !tpm.IsEnabled ||
                    string.IsNullOrEmpty(tpm.SpecVersion) || 
                    !(tpm.SpecVersion.StartsWith("2.") || tpm.SpecVersion.Contains("2.0")));

                if (noTpm20 > 0)
                {
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "no-tpm20",
                        Name = "Missing TPM 2.0",
                        Description = "TPM 2.0 is required for Windows 11.",
                        AffectedDeviceCount = noTpm20,
                        PercentageAffected = Math.Round((double)noTpm20 / signal.TotalDevices * 100, 1),
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

                signal.ReadyDevices = readyDeviceIds.Count;
                signal.TopBlockers = blockers.OrderByDescending(b => b.AffectedDeviceCount).Take(5).ToList();
                
                signal.Recommendations = GenerateWindows11Recommendations(signal, blockers);

                Instance.Info($"   Windows 11 Readiness: {signal.ReadinessPercentage}% ({signal.ReadyDevices}/{signal.TotalDevices})");
            }
            catch (Exception ex)
            {
                Instance.Error($"Windows 11 readiness assessment failed: {ex.Message}");
            }

            return signal;
        }

        /// <summary>
        /// Assesses Cloud-Native readiness (Entra Join + Intune only, no ConfigMgr).
        /// </summary>
        public async Task<CloudReadinessSignal> GetCloudNativeReadinessSignalAsync()
        {
            Instance.Info("Assessing Cloud-Native readiness...");
            
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
                var enrollmentData = await _graphService.GetDeviceEnrollmentAsync();
                var devices = await _configMgrService.GetWindows1011DevicesAsync();

                signal.TotalDevices = enrollmentData?.TotalDevices ?? devices?.Count ?? 0;
                
                if (signal.TotalDevices == 0)
                {
                    return signal;
                }

                var blockers = new List<ReadinessBlocker>();

                // Already cloud-native devices
                var alreadyCloudNative = enrollmentData?.CloudNativeDevices ?? 0;
                
                // Devices that could be cloud-native (AAD joined + Intune)
                var aadJoinedWithIntune = enrollmentData?.AzureADOnlyDevices ?? 0;
                
                // Hybrid joined devices need more work
                var hybridJoined = enrollmentData?.HybridJoinedDevices ?? 0;
                if (hybridJoined > 0)
                {
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "hybrid-joined",
                        Name = "Hybrid Azure AD Joined",
                        Description = "These devices are Hybrid joined and have on-premises AD dependencies.",
                        AffectedDeviceCount = hybridJoined,
                        PercentageAffected = Math.Round((double)hybridJoined / signal.TotalDevices * 100, 1),
                        Severity = BlockerSeverity.Medium,
                        RemediationAction = "Plan migration from Hybrid to cloud-only Azure AD join",
                        RemediationUrl = "https://learn.microsoft.com/entra/identity/devices/device-join-plan"
                    });
                }

                // On-prem only devices
                var onPremOnly = enrollmentData?.OnPremDomainOnlyDevices ?? 0;
                if (onPremOnly > 0)
                {
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "on-prem-only",
                        Name = "On-Premises AD Only",
                        Description = "These devices are only joined to on-premises AD with no cloud identity.",
                        AffectedDeviceCount = onPremOnly,
                        PercentageAffected = Math.Round((double)onPremOnly / signal.TotalDevices * 100, 1),
                        Severity = BlockerSeverity.High,
                        RemediationAction = "Configure Hybrid Azure AD Join as first step to cloud",
                        RemediationUrl = "https://learn.microsoft.com/entra/identity/devices/hybrid-join-plan"
                    });
                }

                // ConfigMgr-only devices (not enrolled in Intune)
                var configMgrOnly = enrollmentData?.ConfigMgrOnlyDevices ?? 0;
                if (configMgrOnly > 0)
                {
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "configmgr-only",
                        Name = "ConfigMgr Only (Not in Intune)",
                        Description = "These devices are managed by ConfigMgr but not enrolled in Intune.",
                        AffectedDeviceCount = configMgrOnly,
                        PercentageAffected = Math.Round((double)configMgrOnly / signal.TotalDevices * 100, 1),
                        Severity = BlockerSeverity.Medium,
                        RemediationAction = "Enable co-management and enroll in Intune",
                        RemediationUrl = "https://learn.microsoft.com/mem/configmgr/comanage/how-to-enable"
                    });
                }

                // Ready = Already cloud-native + AAD-only devices with Intune
                signal.ReadyDevices = alreadyCloudNative + aadJoinedWithIntune;
                signal.TopBlockers = blockers.OrderByDescending(b => b.AffectedDeviceCount).Take(5).ToList();
                
                signal.Recommendations = GenerateCloudNativeRecommendations(signal, blockers);

                Instance.Info($"   Cloud-Native Readiness: {signal.ReadinessPercentage}% ({signal.ReadyDevices}/{signal.TotalDevices})");
            }
            catch (Exception ex)
            {
                Instance.Error($"Cloud-Native readiness assessment failed: {ex.Message}");
            }

            return signal;
        }

        /// <summary>
        /// Assesses Identity readiness (on-prem AD → Entra).
        /// </summary>
        public async Task<CloudReadinessSignal> GetIdentityReadinessSignalAsync()
        {
            Instance.Info("Assessing Identity readiness...");
            
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
                var enrollmentData = await _graphService.GetDeviceEnrollmentAsync();

                signal.TotalDevices = enrollmentData?.TotalDevices ?? 0;
                
                if (signal.TotalDevices == 0)
                {
                    return signal;
                }

                var blockers = new List<ReadinessBlocker>();

                // Devices with cloud identity (AAD or Hybrid)
                var cloudIdentityReady = (enrollmentData?.AzureADOnlyDevices ?? 0) + (enrollmentData?.HybridJoinedDevices ?? 0);
                
                // On-prem only (no cloud identity)
                var onPremOnly = enrollmentData?.OnPremDomainOnlyDevices ?? 0;
                if (onPremOnly > 0)
                {
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "no-cloud-identity",
                        Name = "No Cloud Identity",
                        Description = "These devices have no Azure AD/Entra identity.",
                        AffectedDeviceCount = onPremOnly,
                        PercentageAffected = Math.Round((double)onPremOnly / signal.TotalDevices * 100, 1),
                        Severity = BlockerSeverity.High,
                        RemediationAction = "Configure Azure AD Connect for Hybrid Join",
                        RemediationUrl = "https://learn.microsoft.com/entra/identity/hybrid/connect/how-to-connect-install-roadmap"
                    });
                }

                // Workgroup devices
                var workgroup = enrollmentData?.WorkgroupDevices ?? 0;
                if (workgroup > 0)
                {
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "workgroup-devices",
                        Name = "Workgroup Devices",
                        Description = "These devices are not domain joined and have no cloud identity.",
                        AffectedDeviceCount = workgroup,
                        PercentageAffected = Math.Round((double)workgroup / signal.TotalDevices * 100, 1),
                        Severity = BlockerSeverity.Medium,
                        RemediationAction = "Azure AD Join these devices directly",
                        RemediationUrl = "https://learn.microsoft.com/entra/identity/devices/device-join-plan"
                    });
                }

                signal.ReadyDevices = cloudIdentityReady;
                signal.TopBlockers = blockers.OrderByDescending(b => b.AffectedDeviceCount).Take(5).ToList();
                
                signal.Recommendations = new List<string>
                {
                    cloudIdentityReady > signal.TotalDevices * 0.8 
                        ? "Great progress! Most devices have cloud identity." 
                        : "Focus on getting all devices registered with Azure AD/Entra.",
                    onPremOnly > 0 ? "Configure Azure AD Connect Hybrid Join for on-prem only devices." : null,
                    workgroup > 0 ? "Consider Azure AD Join for workgroup devices." : null
                }.Where(r => r != null).ToList()!;

                Instance.Info($"   Identity Readiness: {signal.ReadinessPercentage}% ({signal.ReadyDevices}/{signal.TotalDevices})");
            }
            catch (Exception ex)
            {
                Instance.Error($"Identity readiness assessment failed: {ex.Message}");
            }

            return signal;
        }

        /// <summary>
        /// Assesses Windows Update for Business readiness (WSUS → WUfB).
        /// </summary>
        public async Task<CloudReadinessSignal> GetWufbReadinessSignalAsync()
        {
            Instance.Info("Assessing Windows Update for Business readiness...");
            
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
                var devices = await _configMgrService.GetWindows1011DevicesAsync();
                var osDetails = await _configMgrService.GetOSDetailsAsync();
                var enrollmentData = await _graphService.GetDeviceEnrollmentAsync();

                signal.TotalDevices = devices?.Count ?? 0;
                
                if (signal.TotalDevices == 0)
                {
                    return signal;
                }

                var blockers = new List<ReadinessBlocker>();
                var readyCount = 0;

                // WUfB requires Windows 10 Pro/Enterprise/Education or Windows 11
                // Most Windows 10/11 devices are ready, but we check for enrollment state
                var osLookup = osDetails?.ToDictionary(o => o.ResourceId) ?? new Dictionary<int, OSDetails>();
                
                foreach (var device in devices)
                {
                    var isWufbReady = true;
                    
                    // Check OS version (WUfB requires Windows 10 1703+)
                    if (osLookup.TryGetValue(device.ResourceId, out var os))
                    {
                        if (int.TryParse(os.BuildNumber, out var build) && build < 15063) // 1703 = 15063
                        {
                            isWufbReady = false;
                        }
                    }
                    
                    if (isWufbReady) readyCount++;
                }

                // Check for devices not in Intune (needed for WUfB policy delivery)
                var notInIntune = enrollmentData?.ConfigMgrOnlyDevices ?? 0;
                if (notInIntune > 0)
                {
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "not-in-intune",
                        Name = "Not Enrolled in Intune",
                        Description = "WUfB policies are delivered through Intune. These devices need enrollment.",
                        AffectedDeviceCount = notInIntune,
                        PercentageAffected = Math.Round((double)notInIntune / signal.TotalDevices * 100, 1),
                        Severity = BlockerSeverity.Medium,
                        RemediationAction = "Enroll devices in Intune via co-management",
                        RemediationUrl = "https://learn.microsoft.com/mem/configmgr/comanage/how-to-enable"
                    });
                }

                signal.ReadyDevices = readyCount;
                signal.TopBlockers = blockers.OrderByDescending(b => b.AffectedDeviceCount).Take(5).ToList();
                
                signal.Recommendations = new List<string>
                {
                    "Windows Update for Business simplifies update management with cloud policies.",
                    notInIntune > 0 ? $"Enroll {notInIntune} devices in Intune to enable WUfB policy delivery." : null,
                    "Consider using Update Rings in Intune to manage feature and quality updates."
                }.Where(r => r != null).ToList()!;

                Instance.Info($"   WUfB Readiness: {signal.ReadinessPercentage}% ({signal.ReadyDevices}/{signal.TotalDevices})");
            }
            catch (Exception ex)
            {
                Instance.Error($"WUfB readiness assessment failed: {ex.Message}");
            }

            return signal;
        }

        /// <summary>
        /// Assesses Endpoint Security readiness (ConfigMgr EP → Microsoft Defender for Endpoint).
        /// </summary>
        public async Task<CloudReadinessSignal> GetEndpointSecurityReadinessSignalAsync()
        {
            Instance.Info("Assessing Endpoint Security readiness...");
            
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
                var devices = await _configMgrService.GetWindows1011DevicesAsync();
                var osDetails = await _configMgrService.GetOSDetailsAsync();

                signal.TotalDevices = devices?.Count ?? 0;
                
                if (signal.TotalDevices == 0)
                {
                    return signal;
                }

                var blockers = new List<ReadinessBlocker>();
                var osLookup = osDetails?.ToDictionary(o => o.ResourceId) ?? new Dictionary<int, OSDetails>();

                // MDE is built into Windows 10/11 - check for supported versions
                var supportedCount = 0;
                var unsupportedOs = 0;

                foreach (var device in devices)
                {
                    var isSupported = false;
                    
                    if (osLookup.TryGetValue(device.ResourceId, out var os))
                    {
                        // MDE supports Windows 10 1607+ (build 14393)
                        if (int.TryParse(os.BuildNumber, out var build))
                        {
                            isSupported = build >= 14393;
                        }
                    }
                    
                    if (isSupported)
                        supportedCount++;
                    else
                        unsupportedOs++;
                }

                if (unsupportedOs > 0)
                {
                    blockers.Add(new ReadinessBlocker
                    {
                        Id = "unsupported-mde-os",
                        Name = "Unsupported OS for MDE",
                        Description = "Microsoft Defender for Endpoint requires Windows 10 1607 or later.",
                        AffectedDeviceCount = unsupportedOs,
                        PercentageAffected = Math.Round((double)unsupportedOs / signal.TotalDevices * 100, 1),
                        Severity = BlockerSeverity.Medium,
                        RemediationAction = "Upgrade to Windows 10 1607+ or Windows 11",
                        RemediationUrl = "https://learn.microsoft.com/microsoft-365/security/defender-endpoint/minimum-requirements"
                    });
                }

                signal.ReadyDevices = supportedCount;
                signal.TopBlockers = blockers.OrderByDescending(b => b.AffectedDeviceCount).Take(5).ToList();
                
                signal.Recommendations = new List<string>
                {
                    "Microsoft Defender for Endpoint provides cloud-powered protection and EDR.",
                    "Use Intune Security Baselines to configure Defender settings.",
                    supportedCount == signal.TotalDevices 
                        ? "All devices support MDE - ready to onboard!" 
                        : $"Upgrade {unsupportedOs} devices to enable MDE support."
                };

                Instance.Info($"   Endpoint Security Readiness: {signal.ReadinessPercentage}% ({signal.ReadyDevices}/{signal.TotalDevices})");
            }
            catch (Exception ex)
            {
                Instance.Error($"Endpoint Security readiness assessment failed: {ex.Message}");
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
