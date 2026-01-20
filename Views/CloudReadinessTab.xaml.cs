using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ZeroTrustMigrationAddin.Models;
using ZeroTrustMigrationAddin.Services;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Views
{
    /// <summary>
    /// Interaction logic for CloudReadinessTab.xaml
    /// Displays cloud readiness signals for migration workloads.
    /// v3.17.0 - Cloud Readiness Signals feature
    /// </summary>
    public partial class CloudReadinessTab : UserControl
    {
        private CloudReadinessService? _readinessService;
        private CloudReadinessDashboard? _currentDashboard;
        private GraphDataService? _graphService;
        private ConfigMgrAdminService? _configMgrService;

        public CloudReadinessTab()
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
        /// Refreshes the cloud readiness assessment.
        /// </summary>
        public async Task RefreshAsync()
        {
            if (_readinessService == null)
            {
                Instance.Warning("[CLOUD READINESS TAB] Service not initialized, showing mock data");
                LoadMockData();
                return;
            }

            try
            {
                Instance.Info("[CLOUD READINESS TAB] Starting cloud readiness assessment...");
                LoadingOverlay.Visibility = Visibility.Visible;
                
                _currentDashboard = await _readinessService.GetCloudReadinessDashboardAsync();
                UpdateUI(_currentDashboard);
                
                // Track CloudReadinessViewed telemetry
                AzureTelemetryService.Instance.TrackEvent("CloudReadinessViewed", new Dictionary<string, string>
                {
                    { "OverallReadiness", _currentDashboard.OverallReadiness.ToString() },
                    { "TotalDevices", _currentDashboard.TotalAssessedDevices.ToString() },
                    { "SignalCount", _currentDashboard.Signals.Count.ToString() },
                    { "UsedMockData", "false" }
                });
                
                Instance.Info($"[CLOUD READINESS TAB] Assessment complete: {_currentDashboard.OverallReadiness}% overall readiness");
            }
            catch (Exception ex)
            {
                Instance.Error($"[CLOUD READINESS TAB] Assessment failed: {ex.Message}");
                LoadMockData();
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadMockData()
        {
            // Create mock data for demonstration when services are not connected
            var mockDashboard = new CloudReadinessDashboard
            {
                LastRefreshed = DateTime.Now,
                Signals = new List<CloudReadinessSignal>
                {
                    new CloudReadinessSignal
                    {
                        Id = "autopilot",
                        Name = "Autopilot Readiness",
                        Description = "Ready for Windows Autopilot deployment",
                        Icon = "🚀",
                        TotalDevices = 1250,
                        ReadyDevices = 950,
                        RelatedWorkload = "Device Provisioning",
                        LearnMoreUrl = "https://learn.microsoft.com/mem/autopilot/windows-autopilot",
                        TopBlockers = new List<ReadinessBlocker>
                        {
                            new ReadinessBlocker { Name = "Missing TPM 2.0", AffectedDeviceCount = 180, Severity = BlockerSeverity.Critical },
                            new ReadinessBlocker { Name = "Not Azure AD Joined", AffectedDeviceCount = 120, Severity = BlockerSeverity.High }
                        }
                    },
                    new CloudReadinessSignal
                    {
                        Id = "windows11",
                        Name = "Windows 11 Readiness",
                        Description = "Ready for Windows 11 upgrade",
                        Icon = "🪟",
                        TotalDevices = 1250,
                        ReadyDevices = 875,
                        RelatedWorkload = "OS Deployment",
                        LearnMoreUrl = "https://learn.microsoft.com/windows/whats-new/windows-11-requirements",
                        TopBlockers = new List<ReadinessBlocker>
                        {
                            new ReadinessBlocker { Name = "Missing TPM 2.0", AffectedDeviceCount = 250, Severity = BlockerSeverity.Critical },
                            new ReadinessBlocker { Name = "Incompatible CPU", AffectedDeviceCount = 125, Severity = BlockerSeverity.Critical }
                        }
                    },
                    new CloudReadinessSignal
                    {
                        Id = "cloud-native",
                        Name = "Cloud-Native Readiness",
                        Description = "Ready for cloud-only management",
                        Icon = "☁️",
                        TotalDevices = 1250,
                        ReadyDevices = 312,
                        RelatedWorkload = "Device Management",
                        LearnMoreUrl = "https://learn.microsoft.com/mem/intune/fundamentals/cloud-native-endpoints-overview",
                        TopBlockers = new List<ReadinessBlocker>
                        {
                            new ReadinessBlocker { Name = "Hybrid Azure AD Joined", AffectedDeviceCount = 688, Severity = BlockerSeverity.Medium },
                            new ReadinessBlocker { Name = "ConfigMgr Only", AffectedDeviceCount = 250, Severity = BlockerSeverity.Medium }
                        }
                    },
                    new CloudReadinessSignal
                    {
                        Id = "identity",
                        Name = "Identity Readiness",
                        Description = "Ready for cloud identity (Entra ID)",
                        Icon = "🔐",
                        TotalDevices = 1250,
                        ReadyDevices = 1100,
                        RelatedWorkload = "Identity Management",
                        LearnMoreUrl = "https://learn.microsoft.com/entra/identity/devices/overview",
                        TopBlockers = new List<ReadinessBlocker>
                        {
                            new ReadinessBlocker { Name = "On-Premises AD Only", AffectedDeviceCount = 100, Severity = BlockerSeverity.High },
                            new ReadinessBlocker { Name = "Workgroup Devices", AffectedDeviceCount = 50, Severity = BlockerSeverity.Medium }
                        }
                    },
                    new CloudReadinessSignal
                    {
                        Id = "wufb",
                        Name = "Update Management Readiness",
                        Description = "Ready for Windows Update for Business",
                        Icon = "🔄",
                        TotalDevices = 1250,
                        ReadyDevices = 1000,
                        RelatedWorkload = "Update Management",
                        LearnMoreUrl = "https://learn.microsoft.com/windows/deployment/update/waas-manage-updates-wufb",
                        TopBlockers = new List<ReadinessBlocker>
                        {
                            new ReadinessBlocker { Name = "Not Enrolled in Intune", AffectedDeviceCount = 250, Severity = BlockerSeverity.Medium }
                        }
                    },
                    new CloudReadinessSignal
                    {
                        Id = "endpoint-security",
                        Name = "Endpoint Security Readiness",
                        Description = "Ready for Microsoft Defender for Endpoint",
                        Icon = "🛡️",
                        TotalDevices = 1250,
                        ReadyDevices = 1200,
                        RelatedWorkload = "Endpoint Security",
                        LearnMoreUrl = "https://learn.microsoft.com/microsoft-365/security/defender-endpoint/microsoft-defender-endpoint",
                        TopBlockers = new List<ReadinessBlocker>
                        {
                            new ReadinessBlocker { Name = "Unsupported OS Version", AffectedDeviceCount = 50, Severity = BlockerSeverity.Medium }
                        }
                    }
                }
            };

            _currentDashboard = mockDashboard;
            UpdateUI(mockDashboard);
        }

        private void UpdateUI(CloudReadinessDashboard dashboard)
        {
            // Update overall score
            OverallScoreText.Text = $"{dashboard.OverallReadiness:F0}%";
            OverallStatusText.Text = dashboard.OverallStatus;
            DeviceCountText.Text = $"{dashboard.TotalAssessedDevices:N0} devices assessed";
            LastRefreshText.Text = $"Last refresh: {dashboard.LastRefreshed:MMM dd, yyyy h:mm tt}";

            // Update score circle color based on readiness
            var color = (Color)ColorConverter.ConvertFromString(dashboard.OverallStatusColor);
            OverallScoreBorderBrush.Color = color;
            OverallScoreText.Foreground = new SolidColorBrush(color);

            // Update quick stats
            var signalsReady = dashboard.Signals.Count(s => s.ReadinessPercentage >= 60);
            SignalsReadyText.Text = $"{signalsReady} / {dashboard.Signals.Count}";
            BlockersFoundText.Text = dashboard.TotalBlockersIdentified.ToString();

            // Update signals list
            SignalsItemsControl.ItemsSource = dashboard.Signals;

            // Update top blockers
            if (dashboard.TopOverallBlockers.Any())
            {
                TopBlockersSection.Visibility = Visibility.Visible;
                TopBlockersItemsControl.ItemsSource = dashboard.TopOverallBlockers;
            }
            else
            {
                TopBlockersSection.Visibility = Visibility.Collapsed;
            }

            // Update recommendations
            UpdateRecommendations(dashboard);
        }

        private void UpdateRecommendations(CloudReadinessDashboard dashboard)
        {
            RecommendationsPanel.Children.Clear();

            var recommendations = new List<string>();

            // Generate overall recommendations based on signals
            if (dashboard.OverallReadiness >= 80)
            {
                recommendations.Add("🎉 Excellent cloud readiness! Your environment is well-prepared for cloud migration.");
            }
            else if (dashboard.OverallReadiness >= 60)
            {
                recommendations.Add("👍 Good progress on cloud readiness. Address remaining blockers to accelerate migration.");
            }
            else if (dashboard.OverallReadiness >= 40)
            {
                recommendations.Add("📋 Moderate cloud readiness. Focus on the highest-impact blockers first.");
            }
            else
            {
                recommendations.Add("⚠️ Cloud readiness needs attention. Start with identity and enrollment fundamentals.");
            }

            // Add signal-specific recommendations
            foreach (var signal in dashboard.Signals.Where(s => s.ReadinessPercentage < 60).Take(3))
            {
                if (signal.Recommendations.Any())
                {
                    recommendations.Add($"📌 {signal.Name}: {signal.Recommendations.First()}");
                }
            }

            // Add quick wins
            var quickWins = dashboard.Signals.Where(s => s.ReadinessPercentage >= 80).ToList();
            if (quickWins.Any())
            {
                recommendations.Add($"✅ Quick wins available: {string.Join(", ", quickWins.Select(s => s.Name))} are ready to go!");
            }

            // Display recommendations
            foreach (var rec in recommendations)
            {
                var textBlock = new TextBlock
                {
                    Text = rec,
                    FontSize = 13,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32")),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                RecommendationsPanel.Children.Add(textBlock);
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshButton.IsEnabled = false;
            RefreshButton.Content = "⏳ Assessing...";
            
            try
            {
                await RefreshAsync();
            }
            finally
            {
                RefreshButton.IsEnabled = true;
                RefreshButton.Content = "🔄 Refresh Assessment";
            }
        }

        private void ViewWorkloadsButton_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to Workloads tab - find parent DashboardWindow and switch tab
            try
            {
                var dashboardWindow = Window.GetWindow(this) as DashboardWindow;
                if (dashboardWindow != null)
                {
                    // Find the TabControl and switch to Workloads tab
                    var tabControl = FindChild<TabControl>(dashboardWindow, "MainTabControl");
                    if (tabControl != null)
                    {
                        // Find Workloads tab by header
                        foreach (TabItem tab in tabControl.Items)
                        {
                            if (tab.Header?.ToString()?.Contains("Workloads") == true)
                            {
                                tabControl.SelectedItem = tab;
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Instance.Warning($"[CLOUD READINESS TAB] Failed to navigate to Workloads tab: {ex.Message}");
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Instance.Warning($"[CLOUD READINESS TAB] Failed to open URL: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper to find a child element by name or type.
        /// </summary>
        private static T? FindChild<T>(DependencyObject parent, string? childName = null) where T : DependencyObject
        {
            if (parent == null) return null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                {
                    if (string.IsNullOrEmpty(childName))
                        return typedChild;

                    if (child is FrameworkElement frameworkElement && frameworkElement.Name == childName)
                        return typedChild;
                }

                var foundChild = FindChild<T>(child, childName);
                if (foundChild != null)
                    return foundChild;
            }

            return null;
        }
    }
}
