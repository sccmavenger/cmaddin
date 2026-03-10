using System;
using System.Collections.Generic;
using System.Linq;
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
                
                // Load HIGH-IMPACT comparison data in parallel (focused view)
                var complianceTask = _readinessService.GetDeviceComplianceComparisonAsync();
                var staleTask = _readinessService.GetStaleDeviceComparisonAsync();
                var caTask = _readinessService.GetConditionalAccessComparisonAsync();
                var velocityTask = _readinessService.GetEnrollmentVelocityComparisonAsync();
                var autopilotTask = _readinessService.GetAutopilotComparisonAsync();
                var updateRingTask = _readinessService.GetUpdateRingComparisonAsync();
                var defenderTask = _readinessService.GetDefenderIntegrationComparisonAsync();
                var workloadTask = _readinessService.GetWorkloadAuthorityComparisonAsync();
                
                await Task.WhenAll(complianceTask, staleTask, caTask, velocityTask,
                    autopilotTask, updateRingTask, defenderTask, workloadTask);
                
                // Update UI with real data
                var compliance = await complianceTask;
                var stale = await staleTask;
                var ca = await caTask;
                var velocity = await velocityTask;
                
                UpdateComplianceCard(compliance);
                UpdateStaleDevicesCard(stale);
                UpdateConditionalAccessCard(ca);
                UpdateEnrollmentVelocityCard(velocity);
                UpdateAutopilotCard(await autopilotTask);
                UpdateUpdateRingCard(await updateRingTask);
                UpdateDefenderIntegrationCard(await defenderTask);
                UpdateAgentRemovalCard(await workloadTask);
                
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
            
            // Card 1: Compliance
            IntuneCompliancePercent.Text = "94%";
            IntuneComplianceDevices.Text = "847 devices";
            ConfigMgrCompliancePercent.Text = "78%";
            ConfigMgrComplianceDevices.Text = "1,250 devices";
            ComplianceComparisonIcon.Text = "📈";
            ComplianceSummaryText.Text = "Cloud-native 16% more compliant (Demo)";
            
            // Card 2: Stale Devices
            IntuneStalePercent.Text = "2.1%";
            IntuneStaleCount.Text = "18 stale";
            ConfigMgrStalePercent.Text = "11.4%";
            ConfigMgrStaleCount.Text = "143 stale";
            StaleComparisonIcon.Text = "🔍";
            StaleSummaryText.Text = "5x fewer security blind spots with cloud-native (Demo)";
            
            // Card 3: Conditional Access (Zero Trust Ready)
            IntuneCAPercent.Text = "94%";
            IntuneCACount.Text = "796 CA-ready";
            ConfigMgrCAPercent.Text = "0%";
            ConfigMgrCACount.Text = "403 not eligible";
            CAComparisonIcon.Text = "🛡️";
            CASummaryText.Text = "403 ConfigMgr-only devices cannot use Zero Trust (Demo)";
            
            // Card 4: Enrollment Velocity
            EnrollmentThisWeek.Text = "24";
            EnrollmentTrendArrow.Text = "📈 Accelerating (+38% vs last week)";
            ConfigMgrProvisionEstimate.Text = "~4 hrs";
            AutopilotProvisionEstimate.Text = "vs ~30 min self-service";
            VelocityComparisonIcon.Text = "📈";
            VelocitySummaryText.Text = "24 enrolled this week — up from 17 last week. Autopilot: ~30 min vs ~4 hrs imaging. (Demo)";
            
            // Card 5: Autopilot vs Imaging
            AutopilotRegistered.Text = "312";
            AutopilotProfiles.Text = "289 with profiles";
            ConfigMgrImagingDevices.Text = "1,250";
            AutopilotSummaryText.Text = "312 devices ready for Autopilot — self-provision from anywhere. (Demo)";
            
            // Card 6: Update Rings
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
            
            // Agent Removal Recommendation
            AgentRemovalReadyCount.Text = "124";
            AgentRemovalReadyPercent.Text = "14.6% of co-managed";
            AgentRemovalNotReadyCount.Text = "723";
            AgentRemovalBlockerText.Text = "avg 2.3 workloads remaining";
            AgentRemovalSummaryText.Text = "124 devices have ALL workloads on Intune — recommend removing the ConfigMgr agent to complete cloud-native transition. (Demo)";
            UpdateAgentRemovalWorkloadChecklist(new Dictionary<string, double>
            {
                ["Compliance Policy"] = 72.0,
                ["Device Configuration"] = 58.0,
                ["Windows Update"] = 85.0,
                ["Endpoint Protection"] = 45.0,
                ["Modern Apps"] = 63.0,
                ["Office Apps"] = 38.0,
                ["Resource Access"] = 91.0
            });
            
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
                
                // Show chart for mock data demo
                TrendChartPanel.Visibility = Visibility.Visible;
                NoTrendDataPanel.Visibility = Visibility.Collapsed;
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

        private void UpdateStaleDevicesCard(StaleDeviceComparison? data)
        {
            if (data == null) return;
            
            IntuneStalePercent.Text = $"{data.IntuneStalePercentage:F1}%";
            // Use contextual display: "62 of 80 tracked" vs "0 of 4 visible" to show scope difference
            IntuneStaleCount.Text = data.IntuneCountDisplay;
            ConfigMgrStalePercent.Text = $"{data.ConfigMgrStalePercentage:F1}%";
            ConfigMgrStaleCount.Text = data.ConfigMgrCountDisplay;
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

        private void UpdateEnrollmentVelocityCard(EnrollmentVelocityComparison? data)
        {
            if (data == null) return;
            
            EnrollmentThisWeek.Text = $"{data.EnrolledThisWeek}";
            
            var trend = data.WeeklyTrend;
            if (trend == "accelerating")
            {
                var pctChange = data.EnrolledPreviousWeek > 0 
                    ? ((double)(data.EnrolledThisWeek - data.EnrolledPreviousWeek) / data.EnrolledPreviousWeek * 100) 
                    : 0;
                EnrollmentTrendArrow.Text = $"{data.TrendArrow} Accelerating (+{pctChange:F0}% vs last week)";
            }
            else if (trend == "slowing")
            {
                EnrollmentTrendArrow.Text = $"{data.TrendArrow} Slowing (was {data.EnrolledPreviousWeek} last week)";
            }
            else if (trend == "new")
            {
                EnrollmentTrendArrow.Text = $"{data.TrendArrow} First enrollments this week!";
            }
            else
            {
                EnrollmentTrendArrow.Text = data.EnrolledPreviousWeek > 0 
                    ? $"{data.TrendArrow} Steady ({data.EnrolledPreviousWeek} last week)" 
                    : "";
            }
            
            ConfigMgrProvisionEstimate.Text = data.ConfigMgrImagingEstimate;
            AutopilotProvisionEstimate.Text = $"vs {data.AutopilotEstimate}";
            VelocityComparisonIcon.Text = data.ComparisonIcon;
            VelocitySummaryText.Text = data.ComparisonSummary;
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

        private void UpdateAgentRemovalCard(WorkloadAuthorityComparison? data)
        {
            if (data == null || !data.HasData) return;
            
            // Count devices ready for agent removal (all workloads on Intune)
            // We derive this from the workload comparison data
            var readyCount = 0;
            var notReadyCount = 0;
            var totalCoManaged = data.TotalCoManagedDevices;
            
            // If all 7 workloads are at 90%+ Intune, most devices are ready
            var fullyOnIntune = data.WorkloadsFullyOnIntune;
            if (fullyOnIntune == 7 && totalCoManaged > 0)
            {
                // All workloads fully on Intune — use the lowest workload adoption as floor
                var minAdoption = data.Workloads.Min(w => w.IntunePercentage);
                readyCount = (int)(totalCoManaged * minAdoption / 100.0);
                notReadyCount = totalCoManaged - readyCount;
            }
            else if (totalCoManaged > 0)
            {
                // Estimate: devices where ALL workloads are on Intune
                // Conservative estimate: use minimum workload adoption percentage
                var minAdoption = data.Workloads.Any() ? data.Workloads.Min(w => w.IntunePercentage) : 0;
                readyCount = (int)(totalCoManaged * Math.Max(0, minAdoption - 10) / 100.0); // Conservative
                notReadyCount = totalCoManaged - readyCount;
            }
            
            AgentRemovalReadyCount.Text = $"{readyCount:N0}";
            AgentRemovalReadyPercent.Text = totalCoManaged > 0 
                ? $"{(double)readyCount / totalCoManaged * 100:F1}% of co-managed" 
                : "--% of co-managed";
            AgentRemovalNotReadyCount.Text = $"{notReadyCount:N0}";
            
            var avgWorkloadsRemaining = data.Workloads.Any() 
                ? 7.0 - data.Workloads.Average(w => w.IntunePercentage) / 100.0 * 7.0 
                : 7.0;
            AgentRemovalBlockerText.Text = $"avg {avgWorkloadsRemaining:F1} workloads remaining";
            
            // Update workload checklist
            var workloadAdoption = new Dictionary<string, double>();
            foreach (var wl in data.Workloads)
            {
                workloadAdoption[wl.WorkloadName] = wl.IntunePercentage;
            }
            UpdateAgentRemovalWorkloadChecklist(workloadAdoption);
            
            // Summary
            if (readyCount > 0)
            {
                AgentRemovalBadgeText.Text = "ACTION RECOMMENDED";
                AgentRemovalSummaryText.Text = $"{readyCount:N0} devices have all workloads on Intune — recommend removing the ConfigMgr agent to complete cloud-native transition.";
            }
            else if (fullyOnIntune >= 5)
            {
                AgentRemovalBadgeText.Text = "GETTING CLOSE";
                AgentRemovalSummaryText.Text = $"{fullyOnIntune}/7 workloads fully on Intune. Move the remaining {7 - fullyOnIntune} to unlock agent removal.";
            }
            else
            {
                AgentRemovalBadgeText.Text = "IN PROGRESS";
                AgentRemovalSummaryText.Text = $"{fullyOnIntune}/7 workloads on Intune. Continue transitioning workloads to prepare for ConfigMgr agent removal.";
            }
        }

        private void UpdateAgentRemovalWorkloadChecklist(Dictionary<string, double> workloadAdoption)
        {
            var controls = new Dictionary<string, TextBlock>
            {
                ["Compliance Policy"] = AgentRemovalWL1,
                ["Device Configuration"] = AgentRemovalWL2,
                ["Windows Update"] = AgentRemovalWL3,
                ["Endpoint Protection"] = AgentRemovalWL4,
                ["Modern Apps"] = AgentRemovalWL5,
                ["Office Apps"] = AgentRemovalWL6,
                ["Resource Access"] = AgentRemovalWL7
            };
            
            foreach (var kvp in controls)
            {
                if (workloadAdoption.TryGetValue(kvp.Key, out var pct))
                {
                    var icon = pct >= 90 ? "✅" : pct >= 50 ? "🟡" : "🔴";
                    kvp.Value.Text = $"{icon} {kvp.Key} ({pct:F0}% on Intune)";
                    kvp.Value.Foreground = pct >= 90 
                        ? (System.Windows.Media.Brush)FindResource("SuccessGreen") 
                        : (System.Windows.Media.Brush)FindResource("TextSecondary");
                }
            }
        }

        private void UpdateCloudNativeSection(DeviceEnrollment? enrollment)
        {
            if (enrollment == null)
            {
                CloudNativeCount.Text = "--";
                CloudNativePercentText.Text = "--% of total estate";
                CloudNativeGoalProgress.Value = 0;
                ShowNoTrendDataMessage();
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
                    
                    // Show chart, hide message
                    TrendChartPanel.Visibility = Visibility.Visible;
                    NoTrendDataPanel.Visibility = Visibility.Collapsed;
                    
                    Instance.Info($"[COMPARISON TAB] Cloud Native trend chart updated with {enrollment.TrendData.Length} data points");
                }
                else
                {
                    // No trend data available - show message
                    ShowNoTrendDataMessage();
                    Instance.Info($"[COMPARISON TAB] No trend data available for Cloud Native chart");
                }
            }
            catch (Exception ex)
            {
                Instance.Warning($"[COMPARISON TAB] Failed to update trend chart: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows the 'no trend data' message and hides the chart
        /// </summary>
        private void ShowNoTrendDataMessage()
        {
            try
            {
                TrendChartPanel.Visibility = Visibility.Collapsed;
                NoTrendDataPanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Instance.Warning($"[COMPARISON TAB] Failed to show no trend data message: {ex.Message}");
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
