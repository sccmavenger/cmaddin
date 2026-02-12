using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LiveCharts;
using LiveCharts.Wpf;
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
        private bool _chartInitialized = false;

        public CloudValueComparisonTab()
        {
            InitializeComponent();
            Loaded += CloudValueComparisonTab_Loaded;
            Unloaded += CloudValueComparisonTab_Unloaded;
        }
        
        private void CloudValueComparisonTab_Loaded(object sender, RoutedEventArgs e)
        {
            // Delay chart initialization until control is fully loaded
            if (!_chartInitialized)
            {
                _chartInitialized = true;
                LoadMockData();
            }
        }
        
        private void CloudValueComparisonTab_Unloaded(object sender, RoutedEventArgs e)
        {
            // Clean up chart resources to prevent DLL unload errors
            try
            {
                if (CloudNativeTrendChart != null)
                {
                    CloudNativeTrendChart.Series?.Clear();
                    CloudNativeTrendChart.AxisX?.Clear();
                    CloudNativeTrendChart.AxisY?.Clear();
                }
            }
            catch (Exception ex)
            {
                Instance.Warning($"[COMPARISON TAB] Error cleaning up chart: {ex.Message}");
            }
        }

        /// <summary>
        /// Initializes the tab with service references.
        /// </summary>
        public async void Initialize(GraphDataService? graphService, ConfigMgrAdminService? configMgrService)
        {
            _graphService = graphService;
            _configMgrService = configMgrService;
            
            if (_graphService != null && _configMgrService != null)
            {
                _readinessService = new CloudReadinessService(_configMgrService, _graphService);
                
                // Auto-refresh when services become available
                Instance.Info("[COMPARISON TAB] Services connected, auto-refreshing...");
                await RefreshAsync();
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
                var defenderTask = _readinessService.GetDefenderIntegrationComparisonAsync();
                
                await Task.WhenAll(complianceTask, syncTask, staleTask, caTask, threatTask, malwareTask, bitlockerTask, attestationTask, defenderTask);
                
                // Update UI with real data
                var compliance = await complianceTask;
                var sync = await syncTask;
                var stale = await staleTask;
                var ca = await caTask;
                var threat = await threatTask;
                var malware = await malwareTask;
                var bitlocker = await bitlockerTask;
                var attestation = await attestationTask;
                var defender = await defenderTask;
                
                UpdateComplianceCard(compliance);
                UpdateSyncFreshnessCard(sync);
                UpdateStaleDevicesCard(stale);
                UpdateConditionalAccessCard(ca);
                UpdateThreatDetectionCard(threat);
                UpdateActiveMalwareCard(malware);
                UpdateBitLockerCard(bitlocker);
                UpdateDeviceHealthAttestationCard(attestation);
                UpdateDefenderIntegrationCard(defender);
                
                // Load Cloud Native data from enrollment
                var enrollment = await _graphService.GetDeviceEnrollmentAsync();
                UpdateCloudNativeSection(enrollment);
                
                // Track telemetry
                AzureTelemetryService.Instance.TrackEvent("CloudValueComparisonViewed", new Dictionary<string, string>
                {
                    { "IntuneDevices", compliance?.IntuneDeviceCount.ToString() ?? "0" },
                    { "ConfigMgrDevices", compliance?.ConfigMgrDeviceCount.ToString() ?? "0" },
                    { "UsedMockData", "false" }
                });
                
                // NEW: Track security posture comparison for VP dashboards
                // This is UNIQUE data only this tool can provide - comparing security across both platforms
                AzureTelemetryService.Instance.TrackSecurityPostureComparison(
                    compliance?.IntuneCompliancePercentage ?? 0,
                    compliance?.ConfigMgrCompliancePercentage ?? 0,
                    ca?.IntuneCAReadyPercentage ?? 0,
                    0, // ConfigMgr-only devices cannot participate in CA (always 0%)
                    bitlocker?.IntuneEncryptedPercentage ?? 0,
                    bitlocker?.ConfigMgrEncryptedPercentage ?? 0,
                    compliance?.IntuneDeviceCount ?? 0,
                    compliance?.ConfigMgrDeviceCount ?? 0);
                
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
            
            // Card 9: Defender Integration - Demo showing VALUE of the feature
            DefenderNotLicensedBanner.Visibility = Visibility.Collapsed;
            DefenderOnboardingHint.Visibility = Visibility.Collapsed;
            DefenderDataPanel.Visibility = Visibility.Visible;
            DefenderCapabilityTags.Opacity = 1.0;
            
            // Show demo as if MDE is working with real data
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
            
            // Cloud Native Section (Hero)
            CloudNativeCount.Text = "201";
            CloudNativePercentText.Text = "5.0% of total estate";
            CloudNativeGoalProgress.Value = 5.0;
            
            // Mock trend data for chart - wrapped in try-catch to prevent crashes
            try
            {
                var mockValues = new ChartValues<int> { 45, 78, 112, 145, 178, 201 };
                var mockLabels = new List<string> { "Sep 1", "Oct 1", "Nov 1", "Dec 1", "Jan 1", "Feb 1" };
                CloudNativeSeries.Values = mockValues;
                CloudNativeAxisX.Labels = mockLabels;
            }
            catch (Exception ex)
            {
                Instance.Warning($"[COMPARISON TAB] Failed to initialize trend chart: {ex.Message}");
            }
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

        private void UpdateCloudNativeSection(DeviceEnrollment? enrollment)
        {
            if (enrollment == null)
            {
                CloudNativeCount.Text = "--";
                CloudNativePercentText.Text = "--% of total estate";
                CloudNativeGoalProgress.Value = 0;
                ClearTrendChart();
                return;
            }
            
            CloudNativeCount.Text = enrollment.CloudNativeDevices.ToString("N0");
            CloudNativePercentText.Text = $"{enrollment.CloudNativePercentage:F1}% of total estate";
            CloudNativeGoalProgress.Value = enrollment.CloudNativePercentage;
            
            // Update trend chart with real data - wrapped in try-catch to prevent crashes
            try
            {
                if (enrollment.TrendData != null && enrollment.TrendData.Length > 0)
                {
                    var values = new ChartValues<int>();
                    var labels = new List<string>();
                    
                    foreach (var trend in enrollment.TrendData)
                    {
                        values.Add(trend.CloudNativeDevices);
                        labels.Add(trend.Month.ToString("MMM d"));
                    }
                    
                    CloudNativeSeries.Values = values;
                    CloudNativeAxisX.Labels = labels;
                    
                    Instance.Info($"[COMPARISON TAB] Cloud Native trend chart updated with {enrollment.TrendData.Length} data points");
                }
                else
                {
                    // No trend data available - show placeholder
                    ClearTrendChart();
                    Instance.Info($"[COMPARISON TAB] No trend data available for Cloud Native chart");
                }
            }
            catch (Exception ex)
            {
                Instance.Warning($"[COMPARISON TAB] Failed to update trend chart: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears the trend chart and shows placeholder data
        /// </summary>
        private void ClearTrendChart()
        {
            try
            {
                // Show empty chart with placeholder labels
                var emptyValues = new ChartValues<int> { 0, 0, 0, 0, 0, 0 };
                var placeholderLabels = new List<string> { "Month 1", "Month 2", "Month 3", "Month 4", "Month 5", "Month 6" };
                CloudNativeSeries.Values = emptyValues;
                CloudNativeAxisX.Labels = placeholderLabels;
            }
            catch (Exception ex)
            {
                Instance.Warning($"[COMPARISON TAB] Failed to clear trend chart: {ex.Message}");
            }
        }

        private void UpdateDefenderIntegrationCard(DefenderIntegrationComparison? data)
        {
            if (data == null) return;
            
            Instance.Info($"[COMPARISON TAB] Updating Defender card: Licensed={data.IsMDELicensed}, P2={data.IsMDEP2Licensed}, Onboarded={data.IntuneMDEOnboardedCount}");
            
            // Update license status badge
            UpdateDefenderLicenseBadge(data);
            
            // Scenario 1: Not licensed - show the warning banner
            if (!data.IsMDELicensed && data.IntuneDeviceCount > 0)
            {
                DefenderNotLicensedBanner.Visibility = Visibility.Visible;
                DefenderNotLicensedHint.Text = $"Your {data.IntuneDeviceCount:N0} Intune devices could have real-time threat visibility with MDE.";
                DefenderCapabilityTags.Opacity = 0.5; // Dim the capability tags
                DefenderDataPanel.Visibility = Visibility.Collapsed;
                DefenderOnboardingHint.Visibility = Visibility.Collapsed;
                
                // Summary shows the gap
                DefenderSummaryBorder.Background = (System.Windows.Media.Brush)FindResource("ErrorRedLight");
                DefenderSummaryText.Foreground = (System.Windows.Media.Brush)FindResource("ErrorRedDark");
                DefenderSummaryText.Text = "⚠️ You have NO visibility into active threats. ConfigMgr only knows if AV is installed.";
            }
            // Scenario 2: Licensed but no devices onboarded
            else if (data.IsMDELicensed && data.IntuneMDEOnboardedCount == 0 && data.IntuneDeviceCount > 0)
            {
                DefenderNotLicensedBanner.Visibility = Visibility.Collapsed;
                DefenderCapabilityTags.Opacity = 1.0;
                DefenderDataPanel.Visibility = Visibility.Visible;
                DefenderOnboardingHint.Visibility = Visibility.Visible;
                
                DefenderMDEOnboarded.Text = $"0 of {data.IntuneDeviceCount:N0} devices reporting";
                DefenderRealTimeProtection.Text = "Deploy MDE sensor to enable visibility";
                DefenderRemediatedCount.Text = "";
                
                // Summary shows onboarding needed
                DefenderSummaryBorder.Background = (System.Windows.Media.Brush)FindResource("WarningOrangeLight");
                DefenderSummaryText.Foreground = (System.Windows.Media.Brush)FindResource("WarningOrange");
                DefenderSummaryText.Text = "⚠️ MDE is licensed but devices need the sensor deployed to report threat data.";
            }
            // Scenario 3: Working - show real data
            else if (data.IntuneMDEOnboardedCount > 0)
            {
                DefenderNotLicensedBanner.Visibility = Visibility.Collapsed;
                DefenderCapabilityTags.Opacity = 1.0;
                DefenderDataPanel.Visibility = Visibility.Visible;
                DefenderOnboardingHint.Visibility = Visibility.Collapsed;
                
                DefenderMDEOnboarded.Text = $"{data.IntuneMDEOnboardedCount:N0} devices with MDE visibility";
                DefenderRealTimeProtection.Text = $"{data.IntuneRealTimeProtectionCount:N0} with real-time malware reporting";
                
                if (data.IntuneRemediatedMalwareCount > 0)
                {
                    DefenderRemediatedCount.Text = $"✓ {data.IntuneRemediatedMalwareCount:N0} threats auto-remediated";
                }
                else
                {
                    DefenderRemediatedCount.Text = "";
                }
                
                // Summary shows the value
                DefenderSummaryBorder.Background = (System.Windows.Media.Brush)FindResource("SuccessGreenLight");
                DefenderSummaryText.Foreground = (System.Windows.Media.Brush)FindResource("SuccessGreen");
                DefenderSummaryText.Text = data.ComparisonSummary;
            }
            // Scenario 4: No Intune devices yet
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
            
            // ConfigMgr side is always the same - they only have AV status
            DefenderConfigMgrEnabled.Text = $"{data.ConfigMgrProtectionEnabledCount:N0} devices with 'AV enabled'";
        }
        
        private void UpdateDefenderLicenseBadge(DefenderIntegrationComparison data)
        {
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
