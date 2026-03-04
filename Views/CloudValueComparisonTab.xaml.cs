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
                
                await Task.WhenAll(complianceTask, staleTask, caTask, velocityTask);
                
                // Update UI with real data
                var compliance = await complianceTask;
                var stale = await staleTask;
                var ca = await caTask;
                var velocity = await velocityTask;
                
                UpdateComplianceCard(compliance);
                UpdateStaleDevicesCard(stale);
                UpdateConditionalAccessCard(ca);
                UpdateEnrollmentVelocityCard(velocity);
                
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
