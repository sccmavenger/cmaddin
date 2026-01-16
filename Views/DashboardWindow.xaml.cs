using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ZeroTrustMigrationAddin.ViewModels;
using ZeroTrustMigrationAddin.Services;
using ZeroTrustMigrationAddin.Models;
using Microsoft.Graph.Models;

namespace ZeroTrustMigrationAddin.Views
{
    public partial class DashboardWindow : Window
    {
        public DashboardWindow(TabVisibilityOptions? tabVisibilityOptions = null)
        {
            try
            {
                InitializeComponent();
                
                // Set window title with current version
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                Title = $"Zero Trust Journey Dashboard v{version?.Major}.{version?.Minor}.{version?.Build}";
                
                var telemetryService = new TelemetryService();
                DataContext = new DashboardViewModel(telemetryService, tabVisibilityOptions);
                
                // Initialize Enrollment Analytics ViewModels
                InitializeEnrollmentAnalytics();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error initializing Dashboard Window:\n\n" +
                    $"Message: {ex.Message}\n\n" +
                    $"Type: {ex.GetType().Name}\n\n" +
                    $"Stack:\n{ex.StackTrace}\n\n" +
                    $"Inner: {ex.InnerException?.Message ?? "None"}",
                    "Dashboard Initialization Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                throw;
            }
        }

        /// <summary>
        /// Initialize the Enrollment Analytics components with their ViewModels
        /// </summary>
        private void InitializeEnrollmentAnalytics()
        {
            try
            {
                // Create shared GraphDataService
                var graphDataService = new GraphDataService();
                
                // Initialize Momentum View with mock data
                if (EnrollmentMomentumView != null)
                {
                    var momentumVM = new EnrollmentMomentumViewModel(graphDataService);
                    LoadMockMomentumData(momentumVM);
                    EnrollmentMomentumView.DataContext = momentumVM;
                }
                
                // Initialize Confidence Card with mock data
                if (EnrollmentConfidenceCard != null)
                {
                    var confidenceVM = new EnrollmentConfidenceViewModel();
                    LoadMockConfidenceData(confidenceVM);
                    EnrollmentConfidenceCard.DataContext = confidenceVM;
                }
                
                // Initialize Playbooks View with mock data
                if (EnrollmentPlaybooksView != null)
                {
                    var playbooksVM = new EnrollmentPlaybooksViewModel();
                    LoadMockPlaybooksData(playbooksVM);
                    EnrollmentPlaybooksView.DataContext = playbooksVM;
                }
                
                System.Diagnostics.Debug.WriteLine("Enrollment Analytics components initialized with mock data");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing Enrollment Analytics: {ex.Message}");
                // Don't throw - analytics features are supplementary
            }
        }

        /// <summary>
        /// Load mock data into the Momentum ViewModel for UI preview
        /// </summary>
        private void LoadMockMomentumData(EnrollmentMomentumViewModel vm)
        {
            // Set core metrics
            vm.TotalConfigMgrDevices = 2500;
            vm.TotalIntuneDevices = 1847;
            vm.Gap = 653;
            vm.EnrolledPct = 73.88;
            vm.EnrolledPctDisplay = "73.9%";

            // Set velocity metrics
            vm.Velocity7Day = 18.5;
            vm.Velocity30 = 15.2;
            vm.DevicesPerWeek = 129.5;
            vm.TrendDescription = "📈 Accelerating";
            vm.TrendState = "Accelerating";
            vm.WeekOverWeekChange = 12.3;
            vm.WeekOverWeekDisplay = "+12.3%";

            // Set stall risk
            vm.IsAtRisk = false;
            vm.StallRiskLevel = "Low";
            vm.StallRiskDescription = "Enrollment velocity is healthy";
            vm.IsTrustTroughRisk = false;

            // Generate mock chart data
            var snapshots = new List<Models.EnrollmentSnapshot>();
            var baseDate = DateTime.Today.AddDays(-30);
            var baseEnrolled = 1500;
            var random = new Random(42); // Fixed seed for consistent data
            
            for (int i = 0; i <= 30; i++)
            {
                var dailyGain = random.Next(10, 25);
                baseEnrolled += dailyGain;
                snapshots.Add(new Models.EnrollmentSnapshot
                {
                    Date = baseDate.AddDays(i),
                    TotalConfigMgrDevices = 2500,
                    TotalIntuneDevices = Math.Min(baseEnrolled, 1847),
                    NewEnrollmentsCount = dailyGain
                });
            }

            // Update chart via reflection to call private method
            var updateChartMethod = typeof(EnrollmentMomentumViewModel).GetMethod("UpdateChart", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            updateChartMethod?.Invoke(vm, new object[] { snapshots });
        }

        /// <summary>
        /// Load mock data into the Confidence ViewModel for UI preview
        /// </summary>
        private void LoadMockConfidenceData(EnrollmentConfidenceViewModel vm)
        {
            var mockResult = new Models.EnrollmentConfidenceResult
            {
                Score = 78,
                Band = Models.ConfidenceBand.Medium,
                Explanation = "Good enrollment velocity with some complexity factors to address",
                Breakdown = new Models.ScoreBreakdown
                {
                    VelocityScore = 85,
                    SuccessRateScore = 82,
                    ComplexityScore = 65,
                    InfrastructureScore = 90,
                    ConditionalAccessScore = 70
                },
                TopDrivers = new List<Models.ScoreDriver>
                {
                    new Models.ScoreDriver
                    {
                        Name = "Strong Velocity",
                        Description = "18+ devices/day enrollment rate",
                        Impact = 15,
                        Category = "Velocity"
                    },
                    new Models.ScoreDriver
                    {
                        Name = "CMG Configured",
                        Description = "Cloud Management Gateway enables internet-based enrollment",
                        Impact = 10,
                        Category = "Infrastructure"
                    },
                    new Models.ScoreDriver
                    {
                        Name = "Co-Management Enabled",
                        Description = "Hybrid management path established",
                        Impact = 8,
                        Category = "Infrastructure"
                    }
                },
                TopDetractors = new List<Models.ScoreDriver>
                {
                    new Models.ScoreDriver
                    {
                        Name = "ESP App Complexity",
                        Description = "12 apps blocking ESP completion",
                        Impact = -12,
                        Category = "Complexity"
                    },
                    new Models.ScoreDriver
                    {
                        Name = "CA Policy Friction",
                        Description = "3 Conditional Access policies may block enrollment",
                        Impact = -8,
                        Category = "ConditionalAccess"
                    }
                }
            };

            vm.UpdateFromResult(mockResult);
        }

        /// <summary>
        /// Load mock data into the Playbooks ViewModel for UI preview
        /// </summary>
        private void LoadMockPlaybooksData(EnrollmentPlaybooksViewModel vm)
        {
            var mockPlaybooks = new List<Models.EnrollmentPlaybook>
            {
                new Models.EnrollmentPlaybook
                {
                    Name = "Reduce ESP App Dependencies",
                    Description = "Optimize Enrollment Status Page by reducing blocking apps to improve enrollment completion rates",
                    Type = Models.PlaybookType.ReduceDependencies,
                    RiskLevel = Models.PlaybookRiskLevel.Low,
                    EstimatedTime = "2-4 hours",
                    ExpectedImpactDevices = 180,
                    IsRecommended = true,
                    RecommendationReason = "High impact with low risk - addresses top enrollment friction point",
                    Steps = new List<Models.PlaybookStep>
                    {
                        new Models.PlaybookStep { Order = 1, Title = "Review ESP apps", Description = "Identify apps marked as required during ESP", ActionType = "Review" },
                        new Models.PlaybookStep { Order = 2, Title = "Analyze app dependencies", Description = "Determine which apps truly need ESP blocking", ActionType = "Review" },
                        new Models.PlaybookStep { Order = 3, Title = "Reconfigure app assignments", Description = "Move non-critical apps to post-enrollment", ActionType = "Configure" },
                        new Models.PlaybookStep { Order = 4, Title = "Test enrollment flow", Description = "Verify ESP completes faster", ActionType = "Verify" }
                    }
                },
                new Models.EnrollmentPlaybook
                {
                    Name = "Conditional Access Policy Review",
                    Description = "Audit and optimize CA policies that may be blocking device enrollment",
                    Type = Models.PlaybookType.ReduceDependencies,
                    RiskLevel = Models.PlaybookRiskLevel.Medium,
                    EstimatedTime = "3-5 hours",
                    ExpectedImpactDevices = 95,
                    IsRecommended = true,
                    RecommendationReason = "CA policies identified as enrollment friction - review recommended",
                    Steps = new List<Models.PlaybookStep>
                    {
                        new Models.PlaybookStep { Order = 1, Title = "Export CA policies", Description = "Document current Conditional Access configuration", ActionType = "Review" },
                        new Models.PlaybookStep { Order = 2, Title = "Identify blocking policies", Description = "Find policies that block non-compliant devices", ActionType = "Review" },
                        new Models.PlaybookStep { Order = 3, Title = "Create enrollment exception", Description = "Add temporary exclusion for enrollment accounts", ActionType = "Configure", RequiresConfirmation = true },
                        new Models.PlaybookStep { Order = 4, Title = "Monitor enrollment rates", Description = "Track enrollment success after changes", ActionType = "Verify" }
                    }
                },
                new Models.EnrollmentPlaybook
                {
                    Name = "Scale Up Enrollment Batch Size",
                    Description = "Increase daily enrollment targets to accelerate migration completion",
                    Type = Models.PlaybookType.ScaleUp,
                    RiskLevel = Models.PlaybookRiskLevel.Medium,
                    EstimatedTime = "1-2 hours",
                    ExpectedImpactDevices = 250,
                    IsRecommended = false,
                    RecommendationReason = "Consider after reducing friction points",
                    Steps = new List<Models.PlaybookStep>
                    {
                        new Models.PlaybookStep { Order = 1, Title = "Review current capacity", Description = "Assess infrastructure readiness for higher volume", ActionType = "Review" },
                        new Models.PlaybookStep { Order = 2, Title = "Adjust batch settings", Description = "Increase max devices per day from 50 to 100", ActionType = "Configure" },
                        new Models.PlaybookStep { Order = 3, Title = "Monitor support tickets", Description = "Watch for increased enrollment issues", ActionType = "Verify" }
                    }
                },
                new Models.EnrollmentPlaybook
                {
                    Name = "Autopilot Device Registration Cleanup",
                    Description = "Remove stale or duplicate Autopilot device registrations causing enrollment conflicts",
                    Type = Models.PlaybookType.AutopilotHygiene,
                    RiskLevel = Models.PlaybookRiskLevel.Low,
                    EstimatedTime = "2-3 hours",
                    ExpectedImpactDevices = 45,
                    IsRecommended = false,
                    Steps = new List<Models.PlaybookStep>
                    {
                        new Models.PlaybookStep { Order = 1, Title = "Export Autopilot devices", Description = "Get list of all registered Autopilot devices", ActionType = "Review" },
                        new Models.PlaybookStep { Order = 2, Title = "Identify duplicates", Description = "Find devices with multiple registrations", ActionType = "Review" },
                        new Models.PlaybookStep { Order = 3, Title = "Remove stale entries", Description = "Delete outdated or duplicate registrations", ActionType = "Execute", RequiresConfirmation = true },
                        new Models.PlaybookStep { Order = 4, Title = "Re-sync hardware IDs", Description = "Ensure clean device registration", ActionType = "Verify" }
                    }
                },
                new Models.EnrollmentPlaybook
                {
                    Name = "Rebuild Enrollment Momentum",
                    Description = "Address enrollment stall by restarting with optimized settings and fresh batches",
                    Type = Models.PlaybookType.RebuildMomentum,
                    RiskLevel = Models.PlaybookRiskLevel.High,
                    EstimatedTime = "4-6 hours",
                    ExpectedImpactDevices = 350,
                    IsRecommended = false,
                    RecommendationReason = "Use when enrollment has stalled for 7+ days",
                    Steps = new List<Models.PlaybookStep>
                    {
                        new Models.PlaybookStep { Order = 1, Title = "Pause current enrollments", Description = "Stop active enrollment batches", ActionType = "Execute" },
                        new Models.PlaybookStep { Order = 2, Title = "Analyze failure patterns", Description = "Review recent enrollment failures", ActionType = "Review" },
                        new Models.PlaybookStep { Order = 3, Title = "Reset enrollment queues", Description = "Clear and rebuild device targeting", ActionType = "Configure", RequiresConfirmation = true },
                        new Models.PlaybookStep { Order = 4, Title = "Restart with small batch", Description = "Begin with 10 devices to validate", ActionType = "Execute" },
                        new Models.PlaybookStep { Order = 5, Title = "Gradually scale up", Description = "Increase batch size as success validates", ActionType = "Configure" }
                    }
                }
            };

            vm.UpdateFromPlaybooks(mockPlaybooks);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set window size proportional to screen resolution
            SetAdaptiveWindowSize();
        }

        private void TargetDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (TargetDatePicker.SelectedDate.HasValue && DataContext is DashboardViewModel viewModel)
                {
                    var targetDate = TargetDatePicker.SelectedDate.Value;
                    var today = DateTime.Today;
                    
                    // Calculate days until target
                    var daysUntilTarget = (targetDate - today).Days;
                    
                    if (daysUntilTarget > 0 && viewModel.DeviceEnrollment != null)
                    {
                        // Get remaining devices to enroll
                        var remainingDevices = viewModel.DeviceEnrollment.ConfigMgrOnlyDevices;
                        
                        if (remainingDevices > 0)
                        {
                            // Calculate devices per day needed
                            var devicesPerDay = Math.Ceiling((double)remainingDevices / daysUntilTarget);
                            
                            // Update the text box
                            MaxDevicesPerDayText.Text = devicesPerDay.ToString("0");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Silently handle errors - this is a convenience feature
                System.Diagnostics.Debug.WriteLine($"Error calculating max devices per day: {ex.Message}");
            }
        }

        /// <summary>
        /// Set window size based on screen resolution to ensure window controls are accessible
        /// </summary>
        private void SetAdaptiveWindowSize()
        {
            try
            {
                // Get the working area of the primary screen (excludes taskbar)
                var workingArea = SystemParameters.WorkArea;
                
                // Calculate 90% of screen dimensions to leave space for window chrome and taskbar
                var maxWidth = workingArea.Width * 0.9;
                var maxHeight = workingArea.Height * 0.9;
                
                // Set minimum sizes
                var minWidth = 1200.0;
                var minHeight = 700.0;
                
                // Determine appropriate window size based on screen resolution
                if (workingArea.Width <= 1366) // Small screens (1366x768 or smaller)
                {
                    Width = Math.Max(1100, Math.Min(workingArea.Width * 0.95, maxWidth));
                    Height = Math.Max(650, Math.Min(workingArea.Height * 0.90, maxHeight));
                    
                    // Enable scroll viewer for small screens
                    System.Diagnostics.Debug.WriteLine("Small screen detected - using compact layout");
                }
                else if (workingArea.Width <= 1920) // Standard HD screens (1920x1080)
                {
                    Width = Math.Min(1400, maxWidth);
                    Height = Math.Min(900, maxHeight);
                }
                else // Large screens (2K, 4K, etc.)
                {
                    Width = Math.Min(1600, maxWidth);
                    Height = Math.Min(1000, maxHeight);
                }
                
                // Ensure window doesn't exceed screen bounds
                if (Width > maxWidth) Width = maxWidth;
                if (Height > maxHeight) Height = maxHeight;
                
                // Ensure minimum sizes are met
                if (Width < minWidth) Width = minWidth;
                if (Height < minHeight) Height = minHeight;
                
                // Set window state
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                WindowState = WindowState.Normal; // Ensure not maximized
                
                System.Diagnostics.Debug.WriteLine($"Screen Resolution: {workingArea.Width}x{workingArea.Height}");
                System.Diagnostics.Debug.WriteLine($"Window Size: {Width}x{Height}");
                System.Diagnostics.Debug.WriteLine($"Window fits on screen: {Width <= maxWidth && Height <= maxHeight}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting adaptive window size: {ex.Message}");
                // Fall back to reasonable defaults if detection fails
                Width = 1200;
                Height = 800;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            // Open hyperlink URL in default browser
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }

        /// <summary>
        /// Handle clicks on device count numbers to show device list
        /// </summary>
        private async void DeviceCount_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.TextBlock textBlock && 
                    textBlock.Tag is string joinTypeString &&
                    DataContext is DashboardViewModel viewModel)
                {
                    // Parse join type from Tag
                    DeviceJoinType joinType = joinTypeString switch
                    {
                        "HybridJoined" => DeviceJoinType.HybridAzureADJoined,
                        "AzureADOnly" => DeviceJoinType.AzureADOnly,
                        "OnPremOnly" => DeviceJoinType.OnPremDomainOnly,
                        "Workgroup" => DeviceJoinType.WorkgroupOnly,
                        _ => DeviceJoinType.Unknown
                    };

                    if (joinType == DeviceJoinType.Unknown)
                    {
                        MessageBox.Show("Unable to determine device join type.", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    List<ManagedDevice> devices;
                    
                    // Use ViewModel's authenticated GraphDataService if available
                    if (viewModel.GraphDataService.IsAuthenticated)
                    {
                        devices = await viewModel.GraphDataService.GetDevicesByJoinType(joinType);
                    }
                    else
                    {
                        // Generate mock data for demonstration
                        devices = GenerateMockDevices(joinType, viewModel.DeviceEnrollment);
                    }

                    if (devices == null || devices.Count == 0)
                    {
                        MessageBox.Show($"No devices found for this category.", "No Devices", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // Create and show device list dialog
                    var deviceListViewModel = new DeviceListViewModel(joinType, devices);
                    var deviceListDialog = new DeviceListDialog
                    {
                        DataContext = deviceListViewModel,
                        Owner = this
                    };
                    deviceListDialog.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading device list:\n\n{ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"DeviceCount_Click error: {ex}");
            }
        }

        /// <summary>
        /// Handle clicks on setup guide links
        /// </summary>
        private void SetupGuide_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.TextBlock textBlock && 
                    textBlock.Tag is string guideType)
                {
                    string url = guideType switch
                    {
                        "HybridJoin" => "https://learn.microsoft.com/en-us/entra/identity/devices/how-to-hybrid-join",
                        "DomainJoin" => "https://learn.microsoft.com/en-us/entra/identity/devices/concept-device-registration",
                        _ => "https://learn.microsoft.com/en-us/mem/intune/enrollment/windows-enrollment-methods"
                    };

                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening documentation:\n\n{ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"SetupGuide_Click error: {ex}");
            }
        }

        /// <summary>
        /// Generate mock device data for demonstration when not connected to Graph API
        /// </summary>
        private List<ManagedDevice> GenerateMockDevices(DeviceJoinType joinType, DeviceEnrollment? enrollment)
        {
            var devices = new List<ManagedDevice>();
            var random = new Random();
            
            // Determine how many devices to generate based on enrollment data
            int count = joinType switch
            {
                DeviceJoinType.HybridAzureADJoined => enrollment?.HybridJoinedDevices ?? 78000,
                DeviceJoinType.AzureADOnly => enrollment?.AzureADOnlyDevices ?? 22000,
                DeviceJoinType.OnPremDomainOnly => enrollment?.OnPremDomainOnlyDevices ?? 12000,
                DeviceJoinType.WorkgroupOnly => enrollment?.WorkgroupDevices ?? 3000,
                _ => 100
            };

            // Limit to 50 sample devices for display
            int displayCount = Math.Min(50, count);
            
            string[] osVersions = { "Windows 10 Enterprise 22H2", "Windows 11 Enterprise 23H2", "Windows 11 Pro 23H2" };
            string[] prefixes = joinType switch
            {
                DeviceJoinType.HybridAzureADJoined => new[] { "WKS", "DESKTOP", "PC", "LAPTOP" },
                DeviceJoinType.AzureADOnly => new[] { "CLOUD", "AAD", "ENTRA", "MODERN" },
                DeviceJoinType.OnPremDomainOnly => new[] { "DOMAIN", "CORP", "AD", "LEGACY" },
                DeviceJoinType.WorkgroupOnly => new[] { "WKGRP", "LOCAL", "HOME", "TEST" },
                _ => new[] { "DEVICE" }
            };

            for (int i = 0; i < displayCount; i++)
            {
                var prefix = prefixes[random.Next(prefixes.Length)];
                var suffix = random.Next(10000, 99999);
                
                devices.Add(new ManagedDevice
                {
                    DeviceName = $"{prefix}-{suffix}",
                    OperatingSystem = osVersions[random.Next(osVersions.Length)],
                    LastSyncDateTime = DateTimeOffset.Now.AddDays(-random.Next(0, 30)),
                    ComplianceState = random.Next(10) < 9 ? ComplianceState.Compliant : ComplianceState.Noncompliant
                });
            }

            return devices;
        }
    }
}
