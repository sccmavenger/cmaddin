using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroTrustMigrationAddin.Models;
using ZeroTrustMigrationAddin.Services;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Views
{
    /// <summary>
    /// Dashboard card for the Enrollment Impact Simulator.
    /// Shows headline metrics from simulation results.
    /// </summary>
    public partial class EnrollmentSimulatorCard : UserControl
    {
        private EnrollmentSimulatorService? _simulatorService;
        private EnrollmentSimulationResult? _lastResult;
        private GraphDataService? _graphService;
        private ConfigMgrAdminService? _configMgrService;

        public EnrollmentSimulatorCard()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Initialize the card with services.
        /// Automatically runs simulation when real services are provided.
        /// </summary>
        public async void Initialize(GraphDataService? graphService, ConfigMgrAdminService? configMgrService)
        {
            _graphService = graphService;
            _configMgrService = configMgrService;
            _simulatorService = new EnrollmentSimulatorService(graphService, configMgrService);
            
            // Update data source badge based on connection status
            UpdateDataSourceBadge();
            
            // Check if we have REAL services (not initial null call)
            bool hasRealServices = graphService != null && configMgrService != null && configMgrService.IsConfigured;
            
            Instance.Info($"[SIMULATOR CARD] Initialize called - Graph: {graphService != null}, ConfigMgr: {configMgrService != null}, ConfigMgr.IsConfigured: {configMgrService?.IsConfigured ?? false}");
            
            // Auto-run simulation when real services are connected
            if (hasRealServices)
            {
                Instance.Info("[SIMULATOR CARD] ✅ Real services detected - AUTO-RUNNING simulation...");
                await RunSimulationAsync();
            }
            else
            {
                Instance.Info("[SIMULATOR CARD] ⏳ Waiting for services - simulation requires manual trigger or connection");
                // Disable button until services are ready
                RunSimulationButton.IsEnabled = false;
                ButtonText.Text = "Connect First";
                ButtonIcon.Text = "⏳";
            }
        }

        /// <summary>
        /// Update the data source badge to show whether using real or demo data.
        /// </summary>
        private void UpdateDataSourceBadge()
        {
            bool hasGraphService = _graphService != null;
            bool hasConfigMgrService = _configMgrService != null && _configMgrService.IsConfigured;
            
            if (hasGraphService && hasConfigMgrService)
            {
                DataSourceBadge.Background = new SolidColorBrush(Color.FromRgb(232, 245, 233)); // Light green
                DataSourceText.Text = "✓ Live Data";
                DataSourceText.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50)); // Green
            }
            else if (hasGraphService || hasConfigMgrService)
            {
                DataSourceBadge.Background = new SolidColorBrush(Color.FromRgb(255, 248, 225)); // Light yellow
                DataSourceText.Text = "⚡ Partial Data";
                DataSourceText.Foreground = new SolidColorBrush(Color.FromRgb(245, 124, 0)); // Orange
            }
            else
            {
                DataSourceBadge.Background = new SolidColorBrush(Color.FromRgb(255, 243, 224)); // Light orange
                DataSourceText.Text = "📋 Demo Data";
                DataSourceText.Foreground = new SolidColorBrush(Color.FromRgb(230, 81, 0)); // Dark orange
            }
        }

        /// <summary>
        /// Run simulation button click handler.
        /// </summary>
        private async void RunSimulation_Click(object sender, RoutedEventArgs e)
        {
            Instance.Info("[SIMULATOR CARD] 🖱️ User clicked 'Run Simulation' button");
            Instance.Info($"[SIMULATOR CARD]    Graph service: {(_graphService != null ? "✅ Available" : "❌ NULL")}");
            Instance.Info($"[SIMULATOR CARD]    ConfigMgr service: {(_configMgrService != null ? (_configMgrService.IsConfigured ? "✅ Configured" : "⚠️ Not Configured") : "❌ NULL")}");
            await RunSimulationAsync();
        }

        /// <summary>
        /// Run the enrollment simulation.
        /// </summary>
        public async Task RunSimulationAsync()
        {
            if (_simulatorService == null)
            {
                _simulatorService = new EnrollmentSimulatorService(_graphService, _configMgrService);
            }

            try
            {
                // Show loading state
                InitialPanel.Visibility = Visibility.Collapsed;
                ResultsPanel.Visibility = Visibility.Collapsed;
                GapSummaryPanel.Visibility = Visibility.Collapsed;
                WarningsPanel.Visibility = Visibility.Collapsed;
                ActionsPanel.Visibility = Visibility.Collapsed;
                LoadingPanel.Visibility = Visibility.Visible;
                
                RunSimulationButton.IsEnabled = false;
                ButtonText.Text = "Running...";
                ButtonIcon.Text = "⏳";

                // Update loading text through stages
                LoadingText.Text = "Gathering device inventory from ConfigMgr...";
                await Task.Delay(300);

                LoadingText.Text = "Querying Intune compliance policies...";
                
                // Run the simulation
                _lastResult = await _simulatorService.RunSimulationAsync();

                LoadingText.Text = "Calculating compliance gaps...";
                await Task.Delay(200);

                // Display results
                DisplayResults(_lastResult);
            }
            catch (Exception ex)
            {
                Instance.Error($"[SIMULATOR CARD] Simulation failed: {ex.Message}");
                MessageBox.Show($"Simulation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                RunSimulationButton.IsEnabled = true;
                ButtonText.Text = "Run Again";
                ButtonIcon.Text = "🔄";
            }
        }

        /// <summary>
        /// Display simulation results in the card.
        /// </summary>
        private void DisplayResults(EnrollmentSimulationResult result)
        {
            // Show results panels
            ResultsPanel.Visibility = Visibility.Visible;
            ActionsPanel.Visibility = Visibility.Visible;

            // Ready devices
            ReadyCount.Text = result.WouldBeCompliantCount.ToString();
            ReadyPercent.Text = result.UnenrolledDevices > 0 
                ? $"{(double)result.WouldBeCompliantCount / result.UnenrolledDevices * 100:F0}% of {result.UnenrolledDevices} unenrolled"
                : "No unenrolled devices";

            // Remediation needed
            RemediationCount.Text = result.WouldFailCount.ToString();
            RemediationPercent.Text = result.UnenrolledDevices > 0 
                ? $"{(double)result.WouldFailCount / result.UnenrolledDevices * 100:F0}% of {result.UnenrolledDevices} unenrolled"
                : "No unenrolled devices";

            // Projected compliance
            CurrentRate.Text = $"{result.CurrentComplianceRate:F0}%";
            ProjectedRate.Text = $"{result.ProjectedComplianceRate:F0}%";
            
            // Color code the projection
            if (result.ProjectedComplianceRate >= 90)
                ProjectedRate.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50)); // Green
            else if (result.ProjectedComplianceRate >= 70)
                ProjectedRate.Foreground = new SolidColorBrush(Color.FromRgb(245, 124, 0)); // Orange
            else
                ProjectedRate.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40)); // Red

            ProjectedNote.Text = result.TotalDevices > 0 
                ? $"{result.ProjectedCompliantDevices} of {result.TotalDevices} devices"
                : "after enrollment";

            // Gap summary (top 5)
            if (result.GapSummaries.Any())
            {
                GapSummaryPanel.Visibility = Visibility.Visible;
                
                var displayGaps = result.GapSummaries
                    .Take(5)
                    .Select(g => new GapDisplayItem
                    {
                        Icon = g.Icon,
                        Requirement = g.Requirement,
                        RemediationAction = g.RemediationAction,
                        DeviceCount = g.DeviceCount,
                        RemediationEffort = g.RemediationEffort,
                        EffortColor = GetEffortColor(g.RemediationEffort)
                    })
                    .ToList();

                GapList.ItemsSource = displayGaps;
            }

            // Warnings
            if (result.Warnings.Any())
            {
                WarningsPanel.Visibility = Visibility.Visible;
                WarningText.Text = string.Join(" ", result.Warnings.Take(2));
            }

            // Data freshness
            DataFreshnessText.Text = $"Data freshness: {result.DataFreshnessScore:F0}%";
        }

        /// <summary>
        /// Get color brush for remediation effort level.
        /// </summary>
        private Brush GetEffortColor(string effort)
        {
            return effort?.ToLower() switch
            {
                "low" => new SolidColorBrush(Color.FromRgb(46, 125, 50)),     // Green
                "medium" => new SolidColorBrush(Color.FromRgb(245, 124, 0)),   // Orange
                "high" => new SolidColorBrush(Color.FromRgb(198, 40, 40)),     // Red
                _ => new SolidColorBrush(Color.FromRgb(117, 117, 117))         // Gray
            };
        }

        /// <summary>
        /// View detailed results.
        /// </summary>
        private void ViewDetails_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResult == null)
            {
                MessageBox.Show("Please run a simulation first.", "No Results", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var detailsWindow = new EnrollmentSimulatorWindow(_lastResult);
                detailsWindow.Owner = Window.GetWindow(this);
                detailsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Instance.Error($"[SIMULATOR CARD] Failed to open details: {ex.Message}");
            }
        }

        /// <summary>
        /// Export remediation plan.
        /// </summary>
        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResult == null)
            {
                MessageBox.Show("Please run a simulation first.", "No Results", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"RemediationPlan_{DateTime.Now:yyyyMMdd}",
                    DefaultExt = ".csv",
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    ExportRemediationPlan(saveDialog.FileName);
                    MessageBox.Show($"Remediation plan exported to:\n{saveDialog.FileName}", 
                        "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Instance.Error($"[SIMULATOR CARD] Export failed: {ex.Message}");
                MessageBox.Show($"Export failed: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Export remediation plan to CSV.
        /// </summary>
        private void ExportRemediationPlan(string filePath)
        {
            if (_lastResult == null) return;

            var lines = new List<string>
            {
                "Device Name,Compliance Gaps,Remediation Actions,Effort Level"
            };

            foreach (var device in _lastResult.DeviceResults.Where(d => !d.WouldBeCompliant))
            {
                var gaps = string.Join("; ", device.Gaps.Select(g => g.Requirement));
                var actions = string.Join("; ", device.Gaps.Select(g => g.RemediationAction));
                var maxEffort = GetMaxEffort(device.Gaps.Select(g => g.RemediationEffort));

                lines.Add($"\"{device.DeviceName}\",\"{gaps}\",\"{actions}\",\"{maxEffort}\"");
            }

            System.IO.File.WriteAllLines(filePath, lines);
            Instance.Info($"[SIMULATOR CARD] Exported remediation plan to {filePath}");
        }

        /// <summary>
        /// Get the highest effort level from a list.
        /// </summary>
        private string GetMaxEffort(IEnumerable<string> efforts)
        {
            var list = efforts.ToList();
            if (list.Contains("High")) return "High";
            if (list.Contains("Medium")) return "Medium";
            return "Low";
        }
    }

    /// <summary>
    /// Display model for gap summary items.
    /// </summary>
    public class GapDisplayItem
    {
        public string Icon { get; set; } = "";
        public string Requirement { get; set; } = "";
        public string RemediationAction { get; set; } = "";
        public int DeviceCount { get; set; }
        public string RemediationEffort { get; set; } = "";
        public Brush EffortColor { get; set; } = Brushes.Gray;
    }
}
