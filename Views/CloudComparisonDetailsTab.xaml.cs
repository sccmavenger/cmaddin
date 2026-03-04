using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ZeroTrustMigrationAddin.Models;
using ZeroTrustMigrationAddin.Services;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Views
{
    /// <summary>
    /// Cloud Comparison Details Tab — contains the extended comparison cards
    /// moved from the main Cloud Native tab to keep it focused on high-impact metrics.
    /// 
    /// This tab is hidden by default and can be shown via /showtabs:cloudcomparisondetails
    /// </summary>
    public partial class CloudComparisonDetailsTab : UserControl
    {
        private CloudReadinessService? _readinessService;
        private GraphDataService? _graphService;
        private ConfigMgrAdminService? _configMgrService;

        public CloudComparisonDetailsTab()
        {
            InitializeComponent();
            Loaded += CloudComparisonDetailsTab_Loaded;
        }
        
        private void CloudComparisonDetailsTab_Loaded(object sender, RoutedEventArgs e)
        {
            LoadMockData();
        }

        public async void Initialize(GraphDataService? graphService, ConfigMgrAdminService? configMgrService)
        {
            _graphService = graphService;
            _configMgrService = configMgrService;
            
            if (_graphService != null && _configMgrService != null)
            {
                _readinessService = new CloudReadinessService(_configMgrService, _graphService);
                Instance.Info("[COMPARISON DETAILS] Services connected, auto-refreshing...");
                await RefreshAsync();
            }
        }

        public async Task RefreshAsync()
        {
            if (_readinessService == null || _graphService == null || _configMgrService == null)
            {
                Instance.Warning("[COMPARISON DETAILS] Services not initialized, showing mock data");
                LoadMockData();
                return;
            }

            try
            {
                Instance.Info("[COMPARISON DETAILS] Loading comparison data from real sources...");
                LoadingOverlay.Visibility = Visibility.Visible;
                
                var threatTask = _readinessService.GetThreatDetectionComparisonAsync();
                var malwareTask = _readinessService.GetActiveMalwareComparisonAsync();
                var bitlockerTask = _readinessService.GetBitLockerComparisonAsync();
                var attestationTask = _readinessService.GetDeviceHealthAttestationComparisonAsync();
                var syncTask = _readinessService.GetSyncFreshnessComparisonAsync();
                var enforcementTask = _readinessService.GetComplianceEnforcementComparisonAsync();
                var wfaTask = _readinessService.GetWorkFromAnywhereComparisonAsync();
                var policyDepthTask = _readinessService.GetCompliancePolicyDepthComparisonAsync();
                var workloadTask = _readinessService.GetWorkloadAuthorityComparisonAsync();
                var appPortfolioTask = _readinessService.GetAppPortfolioComparisonAsync();
                var autopilotTask = _readinessService.GetAutopilotComparisonAsync();
                var clientHealthTask = _readinessService.GetClientHealthComparisonAsync();
                var avSignatureTask = _readinessService.GetAVSignatureComparisonAsync();
                var updateRingTask = _readinessService.GetUpdateRingComparisonAsync();
                var defenderTask = _readinessService.GetDefenderIntegrationComparisonAsync();
                
                await Task.WhenAll(threatTask, malwareTask, bitlockerTask, attestationTask,
                    syncTask, enforcementTask, wfaTask, policyDepthTask, workloadTask,
                    appPortfolioTask, autopilotTask, clientHealthTask, avSignatureTask,
                    updateRingTask, defenderTask);
                
                UpdateThreatDetectionCard(await threatTask);
                UpdateActiveMalwareCard(await malwareTask);
                UpdateBitLockerCard(await bitlockerTask);
                UpdateDeviceHealthAttestationCard(await attestationTask);
                UpdateSyncFreshnessCard(await syncTask);
                UpdateComplianceEnforcementCard(await enforcementTask);
                UpdateWorkFromAnywhereCard(await wfaTask);
                UpdateCompliancePolicyDepthCard(await policyDepthTask);
                UpdateWorkloadAuthorityCard(await workloadTask);
                UpdateAppPortfolioCard(await appPortfolioTask);
                UpdateAutopilotCard(await autopilotTask);
                UpdateClientHealthCard(await clientHealthTask);
                UpdateAVSignatureCard(await avSignatureTask);
                UpdateUpdateRingCard(await updateRingTask);
                UpdateDefenderIntegrationCard(await defenderTask);
                
                Instance.Info("[COMPARISON DETAILS] Comparison data loaded successfully");
            }
            catch (Exception ex)
            {
                Instance.Error($"[COMPARISON DETAILS] Failed to load comparison data: {ex.Message}");
                LoadMockData();
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadMockData()
        {
            Instance.Info("[COMPARISON DETAILS] Loading mock comparison data for demonstration");
            
            // Threat Detection
            IntuneThreatSecured.Text = "841";
            IntuneThreatCompromised.Text = "2";
            IntuneThreatMisconfigured.Text = "4";
            ConfigMgrProtectionEnabled.Text = "1,203";
            ThreatSummaryText.Text = "Intune shows SECURED/COMPROMISED status. ConfigMgr shows 'enabled'. (Demo)";
            
            // Active Malware
            IntuneMalwareCount.Text = "3";
            IntuneMalwareDevices.Text = "on 2 devices";
            MalwareComparisonIcon.Text = "🦠";
            MalwareSummaryText.Text = "ConfigMgr: How many devices have malware? You don't know. (Demo)";
            
            // BitLocker
            IntuneEncryptedPercent.Text = "94%";
            IntuneEncryptedCount.Text = "796 devices";
            ConfigMgrEncryptedPercent.Text = "87%";
            ConfigMgrEncryptedCount.Text = "1,089 devices";
            BitLockerSummaryText.Text = "Cloud keys accessible from any browser. MBAM needs VPN. (Demo)";
            
            // Health Attestation
            IntuneAttestedCount.Text = "789";
            AttestationSummaryText.Text = "Only cloud devices can prove hardware health to Zero Trust policies. (Demo)";
            
            // Response Time
            IntuneAvgSyncDays.Text = "0.3";
            IntuneSyncedTodayPercent.Text = "89% synced today";
            ConfigMgrAvgScanDays.Text = "2.8";
            ConfigMgrScannedTodayPercent.Text = "34% scanned today";
            SyncSummaryText.Text = "Cloud-native responds 9x faster to policy changes (Demo)";
            
            // Compliance Enforcement
            IntuneCompliantCount.Text = "796";
            IntuneNonCompliantCount.Text = "51 blocked";
            ConfigMgrEnforcementDevices.Text = "1,250";
            EnforcementSummaryText.Text = "51 non-compliant devices blocked from M365. On ConfigMgr? Just a report. (Demo)";
            
            // Work-from-Anywhere
            IntuneSynced24h.Text = "784";
            ConfigMgrActive24h.Text = "612";
            ConfigMgrDarkDevices.Text = "~172 est. dark devices";
            WFASummaryText.Text = "~172 devices estimated managed by Intune but invisible to ConfigMgr. (Demo)";
            
            // Compliance Policy Depth
            IntunePolicyCount.Text = "4";
            IntunePolicySettings.Text = "enforcing 6 settings";
            ConfigMgrPolicyDevices.Text = "1,250";
            PolicyDepthSummaryText.Text = "Intune enforces BitLocker, TPM, Secure Boot, OS version. ConfigMgr baselines report only. (Demo)";
            
            // Co-Management Workloads
            WorkloadsOnIntune.Text = "3";
            WorkloadsMixed.Text = "2 in transition";
            WorkloadsOnConfigMgr.Text = "4";
            CoManagedDeviceCount.Text = "847 co-managed";
            WorkloadSummaryText.Text = "3/7 workloads on Intune, 2 in transition — move sliders to reduce on-prem. (Demo)";
            
            // App Portfolio
            IntuneAppCount.Text = "42";
            ConfigMgrAppCount.Text = "156";
            AppReadyToMigrate.Text = "94 MSI/MSIX ready";
            AppPortfolioSummaryText.Text = "94 apps (60%) use MSI/MSIX — ready for Intune migration today. (Demo)";
            
            // Autopilot
            AutopilotRegistered.Text = "312";
            AutopilotProfiles.Text = "289 with profiles";
            ConfigMgrImagingDevices.Text = "1,250";
            AutopilotSummaryText.Text = "312 devices ready for Autopilot — self-provision from anywhere. (Demo)";
            
            // Agent Reliability
            IntuneHealthyPercent.Text = "97%";
            IntuneHealthyCount.Text = "822 of 847 healthy";
            ConfigMgrActivePercent.Text = "84%";
            ConfigMgrInactiveCount.Text = "200 inactive";
            ClientHealthSummaryText.Text = "200 ConfigMgr clients inactive — likely off-network or VPN. (Demo)";
            
            // AV Signature Freshness
            IntuneAVSecuredPercent.Text = "99%";
            IntuneAVSecuredCount.Text = "839 secured";
            ConfigMgrAVUpToDatePercent.Text = "87%";
            ConfigMgrAVSignatureAge.Text = "Avg 1.8 days old";
            AVSignatureSummaryText.Text = "ConfigMgr signatures average 1.8 days old — each day is an attack window. (Demo)";
            
            // Update Rings
            IntuneRingCount.Text = "3";
            IntuneRingDevices.Text = "covering 847 devices";
            ConfigMgrUpdateDevices.Text = "1,250";
            ConfigMgrUpdateCompliance.Text = "82% compliant";
            UpdateRingSummaryText.Text = "3 WUfB rings — updates from Microsoft CDN, no WSUS infrastructure. (Demo)";
            
            // Defender Integration
            DefenderNotLicensedBanner.Visibility = Visibility.Collapsed;
            DefenderOnboardingHint.Visibility = Visibility.Collapsed;
            DefenderDataPanel.Visibility = Visibility.Visible;
            DefenderCapabilityTags.Opacity = 1.0;
            DefenderLicenseBadge.Style = (Style)FindResource("LicenseStatusLicensed");
            DefenderLicenseIcon.Text = "🟢";
            DefenderLicenseText.Text = "Demo Data";
            DefenderLicenseText.Foreground = (System.Windows.Media.Brush)FindResource("SuccessGreen");
            DefenderMDEOnboarded.Text = "847 devices with MDE visibility";
            DefenderRealTimeProtection.Text = "812 with real-time malware reporting";
            DefenderRemediatedCount.Text = "✓ 47 threats auto-remediated this month";
            DefenderConfigMgrEnabled.Text = "1,203 devices with 'AV enabled'";
            DefenderSummaryBorder.Background = (System.Windows.Media.Brush)FindResource("SuccessGreenLight");
            DefenderSummaryText.Foreground = (System.Windows.Media.Brush)FindResource("SuccessGreen");
            DefenderSummaryText.Text = "🛡️ Intune sees 3 active threats on 2 devices. ConfigMgr only knows 'AV is enabled'. (Demo)";
        }

        #region Update Card Methods

        private void UpdateThreatDetectionCard(ThreatDetectionComparison? data)
        {
            if (data == null) return;
            IntuneThreatSecured.Text = $"{data.IntuneSecuredCount:N0}";
            IntuneThreatCompromised.Text = $"{data.IntuneCompromisedCount}";
            IntuneThreatMisconfigured.Text = $"{data.IntuneMisconfiguredCount}";
            ConfigMgrProtectionEnabled.Text = $"{data.ConfigMgrProtectionEnabledCount:N0}";
            ThreatComparisonIcon.Text = data.ComparisonIcon;
            ThreatSummaryText.Text = data.ComparisonSummary;
        }

        private void UpdateActiveMalwareCard(ActiveMalwareComparison? data)
        {
            if (data == null) return;
            IntuneMalwareCount.Text = $"{data.TotalActiveMalwareCount}";
            IntuneMalwareDevices.Text = $"on {data.DevicesWithMalwareCount} devices";
            MalwareComparisonIcon.Text = data.ComparisonIcon;
            MalwareSummaryText.Text = data.ComparisonSummary;
        }

        private void UpdateBitLockerCard(BitLockerComparison? data)
        {
            if (data == null) return;
            IntuneEncryptedPercent.Text = $"{data.IntuneEncryptedPercentage:F0}%";
            IntuneEncryptedCount.Text = $"{data.IntuneEncryptedCount:N0} devices";
            if (data.HasConfigMgrData)
            {
                ConfigMgrEncryptedPercent.Text = $"{data.ConfigMgrEncryptedPercentage:F0}%";
                ConfigMgrEncryptedCount.Text = $"{data.ConfigMgrEncryptedCount:N0} devices";
            }
            else
            {
                ConfigMgrEncryptedPercent.Text = "--%";
                ConfigMgrEncryptedCount.Text = "Not inventoried";
            }
            BitLockerSummaryText.Text = data.ComparisonSummary;
        }

        private void UpdateDeviceHealthAttestationCard(DeviceHealthAttestationComparison? data)
        {
            if (data == null) return;
            IntuneAttestedCount.Text = $"{data.IntuneFullyAttestedCount:N0}";
            AttestationSummaryText.Text = data.ComparisonSummary;
        }

        private void UpdateSyncFreshnessCard(SyncFreshnessComparison? data)
        {
            if (data == null) return;
            if (data.HasIntuneActiveData)
            {
                IntuneAvgSyncDays.Text = $"{data.IntuneActiveAvgDaysSinceSync:F1}";
                IntuneSyncedTodayPercent.Text = data.IntuneAbandonedDeviceCount > 0
                    ? $"Push delivery • {data.IntuneAbandonedDeviceCount} abandoned"
                    : "Push delivery (seconds)";
            }
            else
            {
                IntuneAvgSyncDays.Text = $"{data.IntuneAvgDaysSinceSync:F1}";
                IntuneSyncedTodayPercent.Text = $"{data.IntuneSyncedTodayPercentage:F0}% synced today";
            }
            ConfigMgrAvgScanDays.Text = $"{data.ConfigMgrAvgDaysSinceScan:F1}";
            ConfigMgrScannedTodayPercent.Text = "Poll every 60 min";
            SyncComparisonIcon.Text = data.ComparisonIcon;
            SyncSummaryText.Text = data.ComparisonSummary;
        }

        private void UpdateComplianceEnforcementCard(ComplianceEnforcementComparison? data)
        {
            if (data == null) return;
            IntuneCompliantCount.Text = $"{data.IntuneCompliantCount:N0}";
            var parts = new List<string>();
            if (data.IntuneNonCompliantCount > 0) parts.Add($"{data.IntuneNonCompliantCount:N0} blocked");
            if (data.IntuneInGracePeriodCount > 0) parts.Add($"{data.IntuneInGracePeriodCount:N0} in grace");
            IntuneNonCompliantCount.Text = parts.Any() ? string.Join(", ", parts) : "";
            ConfigMgrEnforcementDevices.Text = $"{data.ConfigMgrDeviceCount:N0}";
            EnforcementIcon.Text = data.ComparisonIcon;
            EnforcementSummaryText.Text = data.ComparisonSummary;
        }

        private void UpdateWorkFromAnywhereCard(WorkFromAnywhereComparison? data)
        {
            if (data == null) return;
            IntuneSynced24h.Text = $"{data.IntuneSyncedLast24h:N0}";
            ConfigMgrActive24h.Text = $"{data.ConfigMgrActiveLast24h:N0}";
            ConfigMgrDarkDevices.Text = data.OnlineForIntuneButDarkForConfigMgr > 0 
                ? $"~{data.OnlineForIntuneButDarkForConfigMgr:N0} est. dark devices" 
                : "";
            WFAIcon.Text = data.ComparisonIcon;
            WFASummaryText.Text = data.ComparisonSummary;
        }

        private void UpdateCompliancePolicyDepthCard(CompliancePolicyDepthComparison? data)
        {
            if (data == null) return;
            IntunePolicyCount.Text = $"{data.IntunePolicyCount}";
            IntunePolicySettings.Text = $"enforcing {data.IntuneEnforcedSettings.Count} settings";
            ConfigMgrPolicyDevices.Text = $"{data.ConfigMgrDeviceCount:N0}";
            PolicyDepthIcon.Text = data.ComparisonIcon;
            PolicyDepthSummaryText.Text = data.ComparisonSummary;
        }

        private void UpdateWorkloadAuthorityCard(WorkloadAuthorityComparison? data)
        {
            if (data == null) return;
            WorkloadsOnIntune.Text = $"{data.WorkloadsFullyOnIntune}";
            WorkloadsMixed.Text = data.WorkloadsMixed > 0 ? $"{data.WorkloadsMixed} in transition" : "";
            WorkloadsOnConfigMgr.Text = $"{data.WorkloadsFullyOnConfigMgr}";
            CoManagedDeviceCount.Text = data.TotalCoManagedDevices > 0 ? $"{data.TotalCoManagedDevices:N0} co-managed" : "";
            WorkloadIcon.Text = data.ComparisonIcon;
            WorkloadSummaryText.Text = data.ComparisonSummary;
        }

        private void UpdateAppPortfolioCard(AppPortfolioComparison? data)
        {
            if (data == null) return;
            IntuneAppCount.Text = $"{data.IntuneAppCount}";
            ConfigMgrAppCount.Text = $"{data.ConfigMgrAppCount}";
            AppReadyToMigrate.Text = data.ReadyToMigrateCount > 0 
                ? $"{data.ReadyToMigrateCount} MSI/MSIX ready" 
                : $"{data.ConfigMgrDeployedCount} deployed";
            AppPortfolioIcon.Text = data.ComparisonIcon;
            AppPortfolioSummaryText.Text = data.ComparisonSummary;
        }

        private void UpdateAutopilotCard(AutopilotComparison? data)
        {
            if (data == null) return;
            AutopilotRegistered.Text = $"{data.AutopilotRegisteredCount:N0}";
            AutopilotProfiles.Text = data.AutopilotProfileAssignedCount > 0 ? $"{data.AutopilotProfileAssignedCount:N0} with profiles" : "";
            ConfigMgrImagingDevices.Text = $"{data.ConfigMgrDeviceCount:N0}";
            AutopilotIcon.Text = data.ComparisonIcon;
            AutopilotSummaryText.Text = data.ComparisonSummary;
        }

        private void UpdateClientHealthCard(ClientHealthComparison? data)
        {
            if (data == null) return;
            IntuneHealthyPercent.Text = $"{data.IntuneHealthyPercentage:F0}%";
            IntuneHealthyCount.Text = $"{data.IntuneHealthyCount:N0} of {data.IntuneDeviceCount:N0}";
            ConfigMgrActivePercent.Text = $"{data.ConfigMgrActivePercentage:F0}%";
            ConfigMgrInactiveCount.Text = data.ConfigMgrInactiveCount > 0 ? $"{data.ConfigMgrInactiveCount:N0} inactive" : $"{data.ConfigMgrActiveCount:N0} active";
            ClientHealthIcon.Text = data.ComparisonIcon;
            ClientHealthSummaryText.Text = data.ComparisonSummary;
        }

        private void UpdateAVSignatureCard(AVSignatureComparison? data)
        {
            if (data == null) return;
            IntuneAVSecuredPercent.Text = $"{data.IntuneSecuredPercentage:F0}%";
            IntuneAVSecuredCount.Text = $"{data.IntuneSecuredCount:N0} secured";
            ConfigMgrAVUpToDatePercent.Text = data.HasConfigMgrData ? $"{data.ConfigMgrUpToDatePercentage:F0}%" : "--%";
            ConfigMgrAVSignatureAge.Text = data.ConfigMgrAvgSignatureAgeDays > 0 
                ? $"Avg {data.ConfigMgrAvgSignatureAgeDays:F1} days old" 
                : "No data";
            AVSignatureIcon.Text = data.ComparisonIcon;
            AVSignatureSummaryText.Text = data.ComparisonSummary;
        }

        private void UpdateUpdateRingCard(UpdateRingComparison? data)
        {
            if (data == null) return;
            IntuneRingCount.Text = $"{data.IntuneRingCount}";
            IntuneRingDevices.Text = data.IntuneDevicesInRings > 0 ? $"covering {data.IntuneDevicesInRings:N0} devices" : "";
            ConfigMgrUpdateDevices.Text = $"{data.ConfigMgrDeviceCount:N0}";
            ConfigMgrUpdateCompliance.Text = data.ConfigMgrUpdateComplianceRate > 0 ? $"{data.ConfigMgrUpdateComplianceRate:F0}% compliant" : "";
            UpdateRingIcon.Text = data.ComparisonIcon;
            UpdateRingSummaryText.Text = data.ComparisonSummary;
        }

        private void UpdateDefenderIntegrationCard(DefenderIntegrationComparison? data)
        {
            if (data == null) return;
            
            // Update license badge
            if (!data.IsMDELicensed)
            {
                DefenderLicenseBadge.Style = (Style)FindResource("LicenseStatusNotLicensed");
                DefenderLicenseIcon.Text = "🔴";
                DefenderLicenseText.Text = "MDE Not Licensed";
                DefenderLicenseText.Foreground = (System.Windows.Media.Brush)FindResource("ErrorRed");
            }
            else if (!data.IsMDEP2Licensed)
            {
                DefenderLicenseBadge.Style = (Style)FindResource("LicenseStatusPartial");
                DefenderLicenseIcon.Text = "🟡";
                DefenderLicenseText.Text = "MDE P1 (Limited)";
                DefenderLicenseText.Foreground = (System.Windows.Media.Brush)FindResource("WarningOrange");
            }
            else
            {
                DefenderLicenseBadge.Style = (Style)FindResource("LicenseStatusLicensed");
                DefenderLicenseIcon.Text = "🟢";
                DefenderLicenseText.Text = "MDE Licensed";
                DefenderLicenseText.Foreground = (System.Windows.Media.Brush)FindResource("SuccessGreen");
            }

            if (!data.IsMDELicensed && data.IntuneDeviceCount > 0)
            {
                DefenderNotLicensedBanner.Visibility = Visibility.Visible;
                DefenderNotLicensedHint.Text = $"Your {data.IntuneDeviceCount:N0} Intune devices could have real-time threat visibility with MDE.";
                DefenderCapabilityTags.Opacity = 0.5;
                DefenderDataPanel.Visibility = Visibility.Collapsed;
                DefenderOnboardingHint.Visibility = Visibility.Collapsed;
                DefenderSummaryBorder.Background = (System.Windows.Media.Brush)FindResource("ErrorRedLight");
                DefenderSummaryText.Foreground = (System.Windows.Media.Brush)FindResource("ErrorRedDark");
                DefenderSummaryText.Text = "⚠️ You have NO visibility into active threats.";
            }
            else if (data.IntuneMDEOnboardedCount > 0)
            {
                DefenderNotLicensedBanner.Visibility = Visibility.Collapsed;
                DefenderCapabilityTags.Opacity = 1.0;
                DefenderDataPanel.Visibility = Visibility.Visible;
                DefenderOnboardingHint.Visibility = Visibility.Collapsed;
                DefenderMDEOnboarded.Text = $"{data.IntuneMDEOnboardedCount:N0} devices with MDE visibility";
                DefenderRealTimeProtection.Text = $"{data.IntuneRealTimeProtectionCount:N0} with real-time malware reporting";
                DefenderRemediatedCount.Text = data.IntuneRemediatedMalwareCount > 0 ? $"✓ {data.IntuneRemediatedMalwareCount:N0} threats auto-remediated" : "";
                DefenderSummaryBorder.Background = (System.Windows.Media.Brush)FindResource("SuccessGreenLight");
                DefenderSummaryText.Foreground = (System.Windows.Media.Brush)FindResource("SuccessGreen");
                DefenderSummaryText.Text = data.ComparisonSummary;
            }
            else
            {
                DefenderNotLicensedBanner.Visibility = Visibility.Collapsed;
                DefenderDataPanel.Visibility = Visibility.Visible;
                DefenderOnboardingHint.Visibility = Visibility.Collapsed;
                DefenderCapabilityTags.Opacity = 0.5;
                DefenderMDEOnboarded.Text = "No Intune devices yet";
                DefenderRealTimeProtection.Text = "Connect to Intune to see threat visibility";
                DefenderRemediatedCount.Text = "";
                DefenderSummaryBorder.Background = (System.Windows.Media.Brush)FindResource("BackgroundSubtle");
                DefenderSummaryText.Foreground = (System.Windows.Media.Brush)FindResource("TextSecondary");
                DefenderSummaryText.Text = "Enroll devices to Intune to enable real-time threat visibility with MDE.";
            }
            
            DefenderConfigMgrEnabled.Text = $"{data.ConfigMgrProtectionEnabledCount:N0} devices with 'AV enabled'";
        }

        #endregion

        #region Event Handlers

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAsync();
        }

        #endregion
    }
}
