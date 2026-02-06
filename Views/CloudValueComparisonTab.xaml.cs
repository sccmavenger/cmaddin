using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ZeroTrustMigrationAddin.Models;
using ZeroTrustMigrationAddin.Services;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Views
{
    /// <summary>
    /// Interaction logic for CloudValueComparisonTab.xaml
    /// Displays side-by-side comparisons of Intune (cloud-native) vs ConfigMgr (on-premises)
    /// to demonstrate the operational advantages of cloud-native device management.
    /// 
    /// v3.17.113 - New dedicated tab for cloud-native value comparisons
    /// </summary>
    public partial class CloudValueComparisonTab : UserControl
    {
        private CloudReadinessService? _readinessService;
        private GraphDataService? _graphService;
        private ConfigMgrAdminService? _configMgrService;

        public CloudValueComparisonTab()
        {
            InitializeComponent();
            LoadMockData();
        }

        /// <summary>
        /// Initializes the tab with service references.
        /// </summary>
        public void Initialize(GraphDataService? graphService, ConfigMgrAdminService? configMgrService)
        {
            _graphService = graphService;
            _configMgrService = configMgrService;
            
            if (_graphService != null && _configMgrService != null)
            {
                _readinessService = new CloudReadinessService(_configMgrService, _graphService);
            }
        }

        /// <summary>
        /// Refreshes all comparison data.
        /// </summary>
        public async Task RefreshAsync()
        {
            if (_readinessService == null || _graphService == null || _configMgrService == null)
            {
                Instance.Warning("[COMPARISON TAB] Services not initialized, showing mock data");
                LoadMockData();
                return;
            }

            try
            {
                Instance.Info("[COMPARISON TAB] Loading comparison data from real sources...");
                LoadingOverlay.Visibility = Visibility.Visible;
                
                // Load all comparison data in parallel
                var complianceTask = _readinessService.GetDeviceComplianceComparisonAsync();
                var syncTask = _readinessService.GetSyncFreshnessComparisonAsync();
                var staleTask = _readinessService.GetStaleDeviceComparisonAsync();
                var caTask = _readinessService.GetConditionalAccessComparisonAsync();
                var threatTask = _readinessService.GetThreatDetectionComparisonAsync();
                var malwareTask = _readinessService.GetActiveMalwareComparisonAsync();
                var bitlockerTask = _readinessService.GetBitLockerComparisonAsync();
                var attestationTask = _readinessService.GetDeviceHealthAttestationComparisonAsync();
                
                await Task.WhenAll(complianceTask, syncTask, staleTask, caTask, threatTask, malwareTask, bitlockerTask, attestationTask);
                
                // Update UI with real data
                var compliance = await complianceTask;
                var sync = await syncTask;
                var stale = await staleTask;
                var ca = await caTask;
                var threat = await threatTask;
                var malware = await malwareTask;
                var bitlocker = await bitlockerTask;
                var attestation = await attestationTask;
                
                UpdateComplianceCard(compliance);
                UpdateSyncFreshnessCard(sync);
                UpdateStaleDevicesCard(stale);
                UpdateConditionalAccessCard(ca);
                UpdateThreatDetectionCard(threat);
                UpdateActiveMalwareCard(malware);
                UpdateBitLockerCard(bitlocker);
                UpdateDeviceHealthAttestationCard(attestation);
                
                // Track telemetry
                AzureTelemetryService.Instance.TrackEvent("CloudValueComparisonViewed", new Dictionary<string, string>
                {
                    { "IntuneDevices", compliance?.IntuneDeviceCount.ToString() ?? "0" },
                    { "ConfigMgrDevices", compliance?.ConfigMgrDeviceCount.ToString() ?? "0" },
                    { "UsedMockData", "false" }
                });
                
                Instance.Info("[COMPARISON TAB] Comparison data loaded successfully");
            }
            catch (Exception ex)
            {
                Instance.Error($"[COMPARISON TAB] Failed to load comparison data: {ex.Message}");
                LoadMockData();
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Loads mock data for demonstration when services are not connected.
        /// This data disappears when authenticated to Graph and ConfigMgr.
        /// </summary>
        private void LoadMockData()
        {
            Instance.Info("[COMPARISON TAB] Loading mock comparison data for demonstration");
            
            // Card 1: Threat Detection
            IntuneThreatSecured.Text = "841";
            IntuneThreatCompromised.Text = "2";
            IntuneThreatMisconfigured.Text = "4";
            ConfigMgrProtectionEnabled.Text = "1,203";
            ThreatSummaryText.Text = "Intune shows SECURED/COMPROMISED status. ConfigMgr shows 'enabled'. (Demo)";
            
            // Card 2: Active Malware
            IntuneMalwareCount.Text = "3";
            IntuneMalwareDevices.Text = "on 2 devices";
            MalwareComparisonIcon.Text = "🦠";
            MalwareSummaryText.Text = "ConfigMgr: How many devices have malware? You don't know. (Demo)";
            
            // Card 3: BitLocker
            IntuneEncryptedPercent.Text = "94%";
            IntuneEncryptedCount.Text = "796 devices";
            ConfigMgrEncryptedPercent.Text = "87%";
            ConfigMgrEncryptedCount.Text = "1,089 devices";
            BitLockerSummaryText.Text = "Cloud keys accessible from any browser. MBAM needs VPN. (Demo)";
            
            // Card 4: Device Health Attestation
            IntuneAttestedCount.Text = "789";
            AttestationSummaryText.Text = "Only cloud devices can prove hardware health to Zero Trust policies. (Demo)";
            
            // Card 5: Compliance
            IntuneCompliancePercent.Text = "94%";
            IntuneComplianceDevices.Text = "847 devices";
            ConfigMgrCompliancePercent.Text = "78%";
            ConfigMgrComplianceDevices.Text = "1,250 devices";
            ComplianceComparisonIcon.Text = "📈";
            ComplianceSummaryText.Text = "Cloud-native 16% more compliant (Demo)";
            
            // Card 6: Sync Freshness
            IntuneAvgSyncDays.Text = "0.3";
            IntuneSyncedTodayPercent.Text = "89% synced today";
            ConfigMgrAvgScanDays.Text = "2.8";
            ConfigMgrScannedTodayPercent.Text = "34% scanned today";
            SyncComparisonIcon.Text = "⚡";
            SyncSummaryText.Text = "Cloud-native responds 9x faster to policy changes (Demo)";
            
            // Card 7: Stale Devices
            IntuneStalePercent.Text = "2.1%";
            IntuneStaleCount.Text = "18 stale";
            ConfigMgrStalePercent.Text = "11.4%";
            ConfigMgrStaleCount.Text = "143 stale";
            StaleComparisonIcon.Text = "🔍";
            StaleSummaryText.Text = "5x fewer security blind spots with cloud-native (Demo)";
            
            // Card 8: Conditional Access
            IntuneCAPercent.Text = "94%";
            IntuneCACount.Text = "796 CA-ready";
            ConfigMgrCAPercent.Text = "0%";
            ConfigMgrCACount.Text = "403 not eligible";
            CAComparisonIcon.Text = "🛡️";
            CASummaryText.Text = "403 ConfigMgr-only devices cannot use Zero Trust (Demo)";
        }

        #region Update Card Methods

        private void UpdateComplianceCard(DeviceComplianceComparison? data)
        {
            if (data == null) return;
            
            IntuneCompliancePercent.Text = $"{data.IntuneCompliancePercentage:F0}%";
            IntuneComplianceDevices.Text = $"{data.IntuneCompliantCount:N0} / {data.IntuneDeviceCount:N0}";
            ConfigMgrCompliancePercent.Text = $"{data.ConfigMgrCompliancePercentage:F0}%";
            ConfigMgrComplianceDevices.Text = $"{data.ConfigMgrCompliantCount:N0} / {data.ConfigMgrDeviceCount:N0}";
            ComplianceComparisonIcon.Text = data.ComparisonIcon;
            ComplianceSummaryText.Text = data.ComparisonSummary;
        }

        private void UpdateSyncFreshnessCard(SyncFreshnessComparison? data)
        {
            if (data == null) return;
            
            IntuneAvgSyncDays.Text = $"{data.IntuneAvgDaysSinceSync:F1}";
            IntuneSyncedTodayPercent.Text = $"{data.IntuneSyncedTodayPercentage:F0}% synced today";
            ConfigMgrAvgScanDays.Text = $"{data.ConfigMgrAvgDaysSinceScan:F1}";
            ConfigMgrScannedTodayPercent.Text = $"{data.ConfigMgrScannedTodayPercentage:F0}% scanned today";
            SyncComparisonIcon.Text = data.ComparisonIcon;
            SyncSummaryText.Text = data.ComparisonSummary;
        }

        private void UpdateStaleDevicesCard(StaleDeviceComparison? data)
        {
            if (data == null) return;
            
            IntuneStalePercent.Text = $"{data.IntuneStalePercentage:F1}%";
            IntuneStaleCount.Text = $"{data.IntuneStaleCount:N0} stale";
            ConfigMgrStalePercent.Text = $"{data.ConfigMgrStalePercentage:F1}%";
            ConfigMgrStaleCount.Text = $"{data.ConfigMgrStaleCount:N0} stale";
            StaleComparisonIcon.Text = data.ComparisonIcon;
            StaleSummaryText.Text = data.ComparisonSummary;
        }

        private void UpdateConditionalAccessCard(ConditionalAccessComparison? data)
        {
            if (data == null) return;
            
            IntuneCAPercent.Text = $"{data.IntuneCAReadyPercentage:F0}%";
            IntuneCACount.Text = $"{data.IntuneCAReadyCount:N0} CA-ready";
            ConfigMgrCAPercent.Text = "0%";
            ConfigMgrCACount.Text = $"{data.ConfigMgrOnlyDeviceCount:N0} not eligible";
            CAComparisonIcon.Text = data.ComparisonIcon;
            CASummaryText.Text = data.ComparisonSummary;
        }

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
            ConfigMgrEncryptedPercent.Text = $"{data.ConfigMgrEncryptedPercentage:F0}%";
            ConfigMgrEncryptedCount.Text = $"{data.ConfigMgrEncryptedCount:N0} devices";
            BitLockerSummaryText.Text = data.ComparisonSummary;
        }

        private void UpdateDeviceHealthAttestationCard(DeviceHealthAttestationComparison? data)
        {
            if (data == null) return;
            
            IntuneAttestedCount.Text = $"{data.IntuneFullyAttestedCount:N0}";
            AttestationSummaryText.Text = data.ComparisonSummary;
        }

        #endregion

        #region Event Handlers

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAsync();
        }

        private void ViewEnrollmentButton_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to Enrollment tab
            var dashboardWindow = Window.GetWindow(this) as DashboardWindow;
            if (dashboardWindow != null)
            {
                // Find the tab control and select the Enrollment tab
                var tabControl = dashboardWindow.FindName("MainTabControl") as TabControl;
                if (tabControl != null)
                {
                    // Enrollment tab is typically index 1 (after Overview)
                    for (int i = 0; i < tabControl.Items.Count; i++)
                    {
                        if (tabControl.Items[i] is TabItem tab && tab.Header?.ToString()?.Contains("Enrollment") == true)
                        {
                            tabControl.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
        }

        #endregion
    }
}
