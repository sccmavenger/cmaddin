using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Graph.Models;
using ZeroTrustMigrationAddin.Models;
using ZeroTrustMigrationAddin.Services;
using ZeroTrustMigrationAddin.ViewModels;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Views
{
    /// <summary>
    /// Interaction logic for CloudReadinessTab.xaml
    /// Displays cloud readiness signals for migration workloads.
    /// v3.17.0 - Cloud Readiness Signals feature
    /// v3.17.59 - Added workload device list dialog for co-managed workloads blocker
    /// </summary>
    public partial class CloudReadinessTab : UserControl
    {
        private CloudReadinessService? _readinessService;
        private CloudReadinessDashboard? _currentDashboard;
        private GraphDataService? _graphService;
        private ConfigMgrAdminService? _configMgrService;
        
        // Cache workload authority data for drill-down display
        private WorkloadAuthoritySummary? _cachedWorkloadAuthority;

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
                
                // Fetch workload authority data and cache it for drill-down
                if (_graphService != null)
                {
                    _cachedWorkloadAuthority = await _graphService.GetCoManagedWorkloadAuthorityAsync();
                    Instance.Info($"[CLOUD READINESS TAB] Cached {_cachedWorkloadAuthority?.Devices.Count ?? 0} devices with workload authority");
                }
                
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
                            new ReadinessBlocker { Name = "Not Entra ID Joined", AffectedDeviceCount = 120, Severity = BlockerSeverity.High }
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
                        LearnMoreUrl = "https://learn.microsoft.com/mem/solutions/cloud-native-endpoints/cloud-native-endpoints-overview",
                        TopBlockers = new List<ReadinessBlocker>
                        {
                            new ReadinessBlocker { Name = "Hybrid Entra ID Joined", AffectedDeviceCount = 688, Severity = BlockerSeverity.Medium },
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

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Instance.Info("User requested help for Cloud Readiness Signals");
                
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string userGuidePath = System.IO.Path.Combine(appDirectory, "AdminUserGuide.html");
                
                if (System.IO.File.Exists(userGuidePath))
                {
                    System.Diagnostics.Process.Start(new ProcessStartInfo
                    {
                        FileName = $"{userGuidePath}#cloud-readiness",
                        UseShellExecute = true
                    });
                    Instance.Info("Opened User Guide at cloud-readiness section");
                }
                else
                {
                    MessageBox.Show(
                        "User Guide not found.\n\nPlease ensure AdminUserGuide.html is in the application directory.",
                        "Help Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "HelpButton_Click");
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
                System.Diagnostics.Process.Start(new ProcessStartInfo
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

        private void AdminGuideHelpButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string signalId)
            {
                try
                {
                    // Build the anchor based on signal ID
                    string anchor = $"#signal-{signalId}";
                    
                    // Look for AdminUserGuide.html in the application directory
                    string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    string userGuidePath = System.IO.Path.Combine(appDirectory, "AdminUserGuide.html");
                    
                    if (System.IO.File.Exists(userGuidePath))
                    {
                        // Open with anchor - browsers will navigate to the section
                        string fullPath = userGuidePath + anchor;
                        System.Diagnostics.Process.Start(new ProcessStartInfo
                        {
                            FileName = fullPath,
                            UseShellExecute = true
                        });
                        Instance.Info($"[CLOUD READINESS TAB] Opened Admin Guide: {fullPath}");
                    }
                    else
                    {
                        System.Windows.MessageBox.Show(
                            $"Admin User Guide not found.\n\nExpected location: {userGuidePath}",
                            "User Guide Not Found",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                        Instance.Warning($"[CLOUD READINESS TAB] Admin Guide not found: {userGuidePath}");
                    }
                }
                catch (Exception ex)
                {
                    Instance.Warning($"[CLOUD READINESS TAB] Failed to open Admin Guide: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Handles click on blocker device count to show affected devices.
        /// For co-managed workloads blocker, shows the WorkloadDeviceListDialog with per-device workload authority.
        /// </summary>
        private async void BlockerDeviceCount_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (sender is not System.Windows.Controls.TextBlock textBlock ||
                    textBlock.Tag is not ReadinessBlocker blocker)
                {
                    return;
                }

                Instance.Info($"[CLOUD READINESS TAB] User clicked blocker: {blocker.Name} ({blocker.AffectedDeviceCount} devices)");

                // Special handling for co-managed workloads blocker - show workload authority dialog
                if (blocker.Id == "comanaged-workloads-on-configmgr")
                {
                    await ShowWorkloadDeviceListDialog(blocker);
                    return;
                }

                List<ManagedDevice> devices;

                // If blocker has specific affected device names, use those to filter
                if (blocker.AffectedDeviceNames.Any())
                {
                    Instance.Info($"[CLOUD READINESS TAB] Using {blocker.AffectedDeviceNames.Count} device names from blocker");
                    devices = await GetDevicesByNamesAsync(blocker.AffectedDeviceNames);
                }
                // Fall back to blocker-specific logic if authenticated
                else if (_graphService != null && _graphService.IsAuthenticated)
                {
                    devices = await GetDevicesForBlockerAsync(blocker.Id);
                }
                else
                {
                    // Generate mock data for demonstration
                    devices = GenerateMockDevicesForBlocker(blocker);
                }

                if (devices == null || devices.Count == 0)
                {
                    System.Windows.MessageBox.Show($"No devices found for blocker: {blocker.Name}", "No Devices",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }

                // Show device list dialog
                var deviceListViewModel = new DeviceListViewModel(
                    $"{blocker.Name} ({blocker.AffectedDeviceCount} devices)",
                    devices);
                var deviceListDialog = new DeviceListDialog
                {
                    DataContext = deviceListViewModel,
                    Owner = Window.GetWindow(this)
                };
                deviceListDialog.ShowDialog();
            }
            catch (Exception ex)
            {
                Instance.Error($"[CLOUD READINESS TAB] Error showing blocker devices: {ex.Message}");
                System.Windows.MessageBox.Show($"Error loading device list:\n\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Shows the WorkloadDeviceListDialog for co-managed devices with workload authority details.
        /// </summary>
        private async Task ShowWorkloadDeviceListDialog(ReadinessBlocker blocker)
        {
            try
            {
                List<DeviceWorkloadAuthority> devicesWithWorkloads;

                // Use cached data if available, otherwise fetch
                if (_cachedWorkloadAuthority?.Devices != null && _cachedWorkloadAuthority.Devices.Any())
                {
                    // Filter to only devices that have workloads on ConfigMgr
                    devicesWithWorkloads = _cachedWorkloadAuthority.Devices
                        .Where(d => !d.AllWorkloadsManagedByIntune)
                        .ToList();
                    Instance.Info($"[CLOUD READINESS TAB] Using cached workload data: {devicesWithWorkloads.Count} devices");
                }
                else if (_graphService != null && _graphService.IsAuthenticated)
                {
                    // Fetch fresh workload authority data
                    var workloadAuthority = await _graphService.GetCoManagedWorkloadAuthorityAsync();
                    devicesWithWorkloads = workloadAuthority.Devices
                        .Where(d => !d.AllWorkloadsManagedByIntune)
                        .ToList();
                    Instance.Info($"[CLOUD READINESS TAB] Fetched fresh workload data: {devicesWithWorkloads.Count} devices");
                }
                else
                {
                    // Generate mock data for demonstration
                    devicesWithWorkloads = GenerateMockWorkloadDevices(blocker.AffectedDeviceCount);
                    Instance.Info($"[CLOUD READINESS TAB] Using mock workload data: {devicesWithWorkloads.Count} devices");
                }

                if (devicesWithWorkloads.Count == 0)
                {
                    System.Windows.MessageBox.Show($"No devices found with workloads on ConfigMgr.", "No Devices",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }

                // Show the workload device list dialog
                var dialog = new WorkloadDeviceListDialog
                {
                    Owner = Window.GetWindow(this)
                };
                dialog.SetDevices(devicesWithWorkloads, 
                    $"Device workload view ({devicesWithWorkloads.Count} devices)",
                    "These devices need workloads moved to Microsoft Intune to become cloud-native ready");
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                Instance.Error($"[CLOUD READINESS TAB] Error showing workload dialog: {ex.Message}");
                System.Windows.MessageBox.Show($"Error loading workload data:\n\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Generates mock workload device data for demonstration.
        /// </summary>
        private List<DeviceWorkloadAuthority> GenerateMockWorkloadDevices(int count)
        {
            var devices = new List<DeviceWorkloadAuthority>();
            var random = new Random();
            var deviceNames = new[] { "DESKTOP-", "LAPTOP-", "PC-", "WS-" };

            for (int i = 0; i < count; i++)
            {
                var prefix = deviceNames[random.Next(deviceNames.Length)];
                devices.Add(new DeviceWorkloadAuthority
                {
                    DeviceId = Guid.NewGuid().ToString(),
                    DeviceName = $"{prefix}{random.Next(1000, 9999)}",
                    // Randomly assign some workloads to ConfigMgr
                    CompliancePolicyManagedByConfigMgr = random.Next(2) == 1,
                    DeviceConfigurationManagedByConfigMgr = random.Next(2) == 1,
                    WindowsUpdateManagedByConfigMgr = random.Next(3) == 1, // Less likely
                    EndpointProtectionManagedByConfigMgr = random.Next(2) == 1,
                    ModernAppsManagedByConfigMgr = random.Next(2) == 1,
                    OfficeAppsManagedByConfigMgr = random.Next(3) == 1,
                    ResourceAccessManagedByConfigMgr = random.Next(3) == 1,
                    InventoryManagedByConfigMgr = random.Next(4) == 1 // Least likely
                });
            }

            return devices;
        }

        /// <summary>
        /// Gets devices affected by a specific blocker from Graph API.
        /// </summary>
        private async Task<List<ManagedDevice>> GetDevicesForBlockerAsync(string blockerId)
        {
            if (_graphService == null) return new List<ManagedDevice>();

            try
            {
                // Map blocker IDs to device filters using existing GraphDataService methods
                return blockerId switch
                {
                    "hybrid-joined" => await _graphService.GetDevicesByJoinType(DeviceJoinType.HybridAzureADJoined),
                    "ad-joined-only" or "domain-only" or "on-prem-only" or "no-cloud-identity" => await _graphService.GetDevicesByJoinType(DeviceJoinType.OnPremDomainOnly),
                    "workgroup" or "workgroup-devices" => await _graphService.GetDevicesByJoinType(DeviceJoinType.WorkgroupOnly),
                    "configmgr-only" or "legacy-agent" or "sccm-agent" => await GetConfigMgrOnlyDevicesAsync(),
                    "comanaged-workloads-on-configmgr" => await GetCoManagedDevicesAsync(),
                    "missing-autopilot" or "no-autopilot" or "not-autopilot-registered" => await GetDevicesWithoutAutopilotAsync(),
                    "non-compliant" or "compliance-issues" => await GetNonCompliantDevicesAsync(),
                    "outdated-os" or "legacy-os" or "unsupported-os" or "old-os-wufb" or "unsupported-mde-os" => await GetOutdatedOSDevicesAsync(),
                    "not-in-intune" => await GetNotInIntuneDevicesAsync(),
                    _ => (await _graphService.GetCachedManagedDevicesAsync()).Take(50).ToList()
                };
            }
            catch (Exception ex)
            {
                Instance.Error($"[CLOUD READINESS TAB] Error fetching devices for blocker {blockerId}: {ex.Message}");
                return new List<ManagedDevice>();
            }
        }

        /// <summary>
        /// Gets devices managed only by ConfigMgr (not MDM enrolled).
        /// </summary>
        private async Task<List<ManagedDevice>> GetConfigMgrOnlyDevicesAsync()
        {
            var allDevices = await _graphService!.GetCachedManagedDevicesAsync();
            return allDevices
                .Where(d => d.ManagementAgent == ManagementAgentType.ConfigurationManagerClientMdmEas ||
                           d.ManagementAgent == ManagementAgentType.ConfigurationManagerClient)
                .ToList();
        }

        /// <summary>
        /// Gets devices not enrolled in Autopilot.
        /// </summary>
        private async Task<List<ManagedDevice>> GetDevicesWithoutAutopilotAsync()
        {
            var allDevices = await _graphService!.GetCachedManagedDevicesAsync();
            // Devices without Autopilot registration typically have no WindowsAutopilotDeviceIdentities
            // For now, return devices that are not MDM enrolled or have no Azure AD Device ID
            return allDevices
                .Where(d => string.IsNullOrEmpty(d.AzureADDeviceId))
                .ToList();
        }

        /// <summary>
        /// Gets devices by their names from the cached device list.
        /// </summary>
        private async Task<List<ManagedDevice>> GetDevicesByNamesAsync(List<string> deviceNames)
        {
            if (_graphService == null || !deviceNames.Any()) 
                return new List<ManagedDevice>();

            try
            {
                var deviceNameSet = new HashSet<string>(deviceNames, StringComparer.OrdinalIgnoreCase);
                var allDevices = await _graphService.GetCachedManagedDevicesAsync();
                
                return allDevices
                    .Where(d => !string.IsNullOrEmpty(d.DeviceName) && deviceNameSet.Contains(d.DeviceName))
                    .ToList();
            }
            catch (Exception ex)
            {
                Instance.Error($"[CLOUD READINESS TAB] Error fetching devices by names: {ex.Message}");
                return new List<ManagedDevice>();
            }
        }

        /// <summary>
        /// Gets non-compliant devices.
        /// </summary>
        private async Task<List<ManagedDevice>> GetNonCompliantDevicesAsync()
        {
            var allDevices = await _graphService!.GetCachedManagedDevicesAsync();
            return allDevices
                .Where(d => d.ComplianceState != ComplianceState.Compliant)
                .ToList();
        }

        /// <summary>
        /// Gets co-managed devices (ConfigMgr + Intune).
        /// </summary>
        private async Task<List<ManagedDevice>> GetCoManagedDevicesAsync()
        {
            var allDevices = await _graphService!.GetCachedManagedDevicesAsync();
            return allDevices
                .Where(d => d.ManagementAgent == ManagementAgentType.ConfigurationManagerClientMdm ||
                           d.ManagementAgent == ManagementAgentType.ConfigurationManagerClientMdmEas)
                .ToList();
        }

        /// <summary>
        /// Gets devices not enrolled in Intune.
        /// </summary>
        private async Task<List<ManagedDevice>> GetNotInIntuneDevicesAsync()
        {
            var allDevices = await _graphService!.GetCachedManagedDevicesAsync();
            return allDevices
                .Where(d => d.ManagementAgent != ManagementAgentType.Mdm &&
                           d.ManagementAgent != ManagementAgentType.ConfigurationManagerClientMdm &&
                           d.ManagementAgent != ManagementAgentType.ConfigurationManagerClientMdmEas)
                .ToList();
        }

        /// <summary>
        /// Gets devices with outdated OS versions.
        /// </summary>
        private async Task<List<ManagedDevice>> GetOutdatedOSDevicesAsync()
        {
            var allDevices = await _graphService!.GetCachedManagedDevicesAsync();
            return allDevices
                .Where(d => IsOutdatedOS(d.OperatingSystem, d.OsVersion))
                .ToList();
        }

        /// <summary>
        /// Check if OS version is outdated (Windows 10 before 22H2 or old Windows 11).
        /// </summary>
        private bool IsOutdatedOS(string? osName, string? osVersion)
        {
            if (string.IsNullOrEmpty(osName) || string.IsNullOrEmpty(osVersion))
                return false;

            if (osName.Contains("Windows 10", StringComparison.OrdinalIgnoreCase))
            {
                // Windows 10 before 22H2 (build 19045) is outdated
                if (Version.TryParse(osVersion, out var version))
                {
                    return version.Build < 19045;
                }
            }

            return false;
        }

        /// <summary>
        /// Generate mock devices for demonstration when not connected.
        /// </summary>
        private List<ManagedDevice> GenerateMockDevicesForBlocker(ReadinessBlocker blocker)
        {
            var random = new Random();
            var devices = new List<ManagedDevice>();
            var count = Math.Min(blocker.AffectedDeviceCount, 25); // Cap at 25 for demo

            for (int i = 1; i <= count; i++)
            {
                devices.Add(new ManagedDevice
                {
                    DeviceName = $"DEVICE-{blocker.Id.ToUpper().Replace("-", "")}-{i:D3}",
                    Id = Guid.NewGuid().ToString(),
                    UserPrincipalName = $"user{i}@contoso.com",
                    OperatingSystem = "Windows 10 Enterprise",
                    OsVersion = "10.0.19045",
                    ComplianceState = random.Next(100) < 70 ? ComplianceState.Compliant : ComplianceState.Noncompliant,
                    ManagementAgent = blocker.Id.Contains("configmgr") 
                        ? ManagementAgentType.ConfigurationManagerClient 
                        : ManagementAgentType.Mdm,
                    LastSyncDateTime = DateTimeOffset.Now.AddDays(-random.Next(1, 30))
                });
            }

            return devices;
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
