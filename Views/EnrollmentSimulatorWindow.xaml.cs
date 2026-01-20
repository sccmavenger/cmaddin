using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ZeroTrustMigrationAddin.Models;
using ZeroTrustMigrationAddin.Services;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Views
{
    /// <summary>
    /// Detailed results window for Enrollment Readiness Analyzer.
    /// Shows device-level data and gap analysis.
    /// </summary>
    public partial class EnrollmentSimulatorWindow : Window
    {
        private readonly EnrollmentSimulationResult _result;
        private List<DeviceDisplayItem> _allRemediationDevices = new();
        private List<DeviceDisplayItem> _allReadyDevices = new();

        public EnrollmentSimulatorWindow(EnrollmentSimulationResult result)
        {
            Instance.Info("[SIMULATOR WINDOW] ═══════════════════════════════════════════════════");
            Instance.Info("[SIMULATOR WINDOW] Initializing EnrollmentSimulatorWindow...");
            
            if (result == null)
            {
                Instance.Error("[SIMULATOR WINDOW] ❌ CRITICAL: result parameter is NULL!");
                throw new ArgumentNullException(nameof(result), "Simulation result cannot be null");
            }
            
            Instance.Info($"[SIMULATOR WINDOW] Result received:");
            Instance.Info($"[SIMULATOR WINDOW]    Total Devices: {result.TotalDevices}");
            Instance.Info($"[SIMULATOR WINDOW]    Enrolled: {result.EnrolledDevices}, Unenrolled: {result.UnenrolledDevices}");
            Instance.Info($"[SIMULATOR WINDOW]    Would Pass: {result.WouldBeCompliantCount}, Would Fail: {result.WouldFailCount}");
            Instance.Info($"[SIMULATOR WINDOW]    DeviceResults count: {result.DeviceResults?.Count ?? -1}");
            Instance.Info($"[SIMULATOR WINDOW]    GapSummaries count: {result.GapSummaries?.Count ?? -1}");
            Instance.Info($"[SIMULATOR WINDOW]    PrimaryPolicy: {(result.PrimaryPolicy != null ? result.PrimaryPolicy.PolicyName : "NULL")}");
            Instance.Info($"[SIMULATOR WINDOW]    PoliciesUsed count: {result.PoliciesUsed?.Count ?? -1}");
            
            InitializeComponent();
            _result = result;
            
            // Track window opened for telemetry
            AzureTelemetryService.Instance.TrackEvent("WindowOpened", new Dictionary<string, string>
            {
                { "WindowName", "EnrollmentSimulatorWindow" },
                { "TotalDevices", result.TotalDevices.ToString() },
                { "WouldPassCount", result.WouldBeCompliantCount.ToString() },
                { "WouldFailCount", result.WouldFailCount.ToString() }
            });
            
            try
            {
                LoadData();
                Instance.Info("[SIMULATOR WINDOW] ✅ Window initialized successfully");
                Instance.Info("[SIMULATOR WINDOW] ═══════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Instance.Error($"[SIMULATOR WINDOW] ❌ LoadData failed: {ex.Message}");
                Instance.Error($"[SIMULATOR WINDOW]    Exception Type: {ex.GetType().Name}");
                Instance.Error($"[SIMULATOR WINDOW]    Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Instance.Error($"[SIMULATOR WINDOW]    Inner Exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        /// <summary>
        /// Load simulation results into the UI.
        /// </summary>
        private void LoadData()
        {
            Instance.Debug("[SIMULATOR WINDOW] LoadData starting...");
            
            // Ensure collections are not null
            _result.DeviceResults ??= new List<DeviceSimulationResult>();
            _result.GapSummaries ??= new List<GapSummary>();
            _result.PoliciesUsed ??= new List<CompliancePolicyRequirements>();
            _result.UnassignedPolicyNames ??= new List<string>();
            
            // Header info
            if (_result.PrimaryPolicy != null)
            {
                PolicySummary.Text = $"Evaluated against: {_result.PrimaryPolicy.PolicyName}";
            }
            else
            {
                PolicySummary.Text = "No compliance policy found - using default requirements";
                Instance.Warning("[SIMULATOR WINDOW] PrimaryPolicy is null - no Intune compliance policies?");
            }
            FreshnessValue.Text = $"{_result.DataFreshnessScore:F0}%";

            // Summary cards
            TotalDevicesCount.Text = _result.TotalDevices.ToString("N0");
            EnrolledCount.Text = $"{_result.EnrolledDevices:N0} enrolled";
            UnenrolledCount.Text = $"{_result.UnenrolledDevices:N0} unenrolled";

            ReadyDevicesCount.Text = _result.WouldBeCompliantCount.ToString("N0");
            ReadyPercent.Text = _result.UnenrolledDevices > 0
                ? $"{(double)_result.WouldBeCompliantCount / _result.UnenrolledDevices * 100:F0}% of unenrolled"
                : "N/A";

            RemediationDevicesCount.Text = _result.WouldFailCount.ToString("N0");
            RemediationPercent.Text = _result.UnenrolledDevices > 0
                ? $"{(double)_result.WouldFailCount / _result.UnenrolledDevices * 100:F0}% of unenrolled"
                : "N/A";

            CurrentComplianceRate.Text = $"{_result.CurrentComplianceRate:F0}%";
            ProjectedComplianceRate.Text = $"{_result.ProjectedComplianceRate:F0}%";
            ComplianceNote.Text = $"{_result.ProjectedCompliantDevices:N0} of {_result.TotalDevices:N0} devices";

            // Gap Analysis grid
            LoadGapAnalysis();

            // Devices grids
            LoadDeviceData();

            // Policy details
            LoadPolicyDetails();

            // Gap filter dropdown
            PopulateGapFilter();
        }

        /// <summary>
        /// Load gap analysis data into the grid.
        /// </summary>
        private void LoadGapAnalysis()
        {
            Instance.Debug("[SIMULATOR WINDOW] LoadGapAnalysis starting...");
            
            var gapItems = _result.GapSummaries
                .Select(g => new GapAnalysisItem
                {
                    Icon = g?.Icon ?? "⚠️",
                    Requirement = g?.Requirement ?? "Unknown",
                    DeviceCount = g?.DeviceCount ?? 0,
                    Percentage = g?.Percentage ?? 0,
                    RemediationAction = g?.RemediationAction ?? "",
                    RemediationEffort = g?.RemediationEffort ?? "",
                    AutoRemediateText = g?.CanAutoRemediate == true ? "✓" : "✗"
                })
                .ToList();

            Instance.Debug($"[SIMULATOR WINDOW] Loaded {gapItems.Count} gap analysis items");
            GapAnalysisGrid.ItemsSource = gapItems;
        }

        /// <summary>
        /// Load device data into both grids.
        /// </summary>
        private void LoadDeviceData()
        {
            Instance.Debug("[SIMULATOR WINDOW] LoadDeviceData starting...");
            
            // Devices needing remediation - with defensive null checks on Gaps
            _allRemediationDevices = _result.DeviceResults
                .Where(d => !d.WouldBeCompliant)
                .Select(d => 
                {
                    var gaps = d.Gaps ?? new List<ComplianceGap>();
                    return new DeviceDisplayItem
                    {
                        DeviceName = d.DeviceName ?? "Unknown",
                        GapCount = gaps.Count,
                        GapsList = string.Join(", ", gaps.Select(g => g?.Requirement ?? "")),
                        Gaps = gaps.Select(g => g?.Requirement ?? "").ToList(),
                        MaxEffort = GetMaxEffort(gaps.Select(g => g?.RemediationEffort ?? "")),
                        LastScanText = d.HasStaleInventory ? $"⚠️ {d.DaysSinceLastScan}d ago" : "Recent"
                    };
                })
                .OrderByDescending(d => d.GapCount)
                .ToList();

            Instance.Debug($"[SIMULATOR WINDOW] Loaded {_allRemediationDevices.Count} remediation devices");
            RemediationDevicesGrid.ItemsSource = _allRemediationDevices;

            // Ready devices
            _allReadyDevices = _result.DeviceResults
                .Where(d => d.WouldBeCompliant)
                .Select(d => new DeviceDisplayItem
                {
                    DeviceName = d.DeviceName ?? "Unknown",
                    OSVersion = "N/A", // Would need to add to result model
                    BitLockerText = "✓",
                    FirewallText = "✓",
                    DefenderText = "✓",
                    TpmText = "✓",
                    SecureBootText = "✓",
                    LastScanText = d.HasStaleInventory ? $"⚠️ {d.DaysSinceLastScan}d ago" : "Recent"
                })
                .ToList();

            Instance.Debug($"[SIMULATOR WINDOW] Loaded {_allReadyDevices.Count} ready devices");
            ReadyDevicesGrid.ItemsSource = _allReadyDevices;
        }

        /// <summary>
        /// Load policy requirements details.
        /// </summary>
        private void LoadPolicyDetails()
        {
            Instance.Debug("[SIMULATOR WINDOW] LoadPolicyDetails starting...");
            
            PolicyDetailsPanel.Children.Clear();

            if (_result.PrimaryPolicy == null)
            {
                Instance.Debug("[SIMULATOR WINDOW] PrimaryPolicy is null - showing placeholder");
                PolicyDetailsPanel.Children.Add(new TextBlock
                {
                    Text = "No policy information available",
                    FontSize = 12,
                    Foreground = System.Windows.Media.Brushes.Gray
                });
                return;
            }

            var policy = _result.PrimaryPolicy;
            Instance.Debug($"[SIMULATOR WINDOW] Displaying policy: {policy.PolicyName ?? "Unknown"}");

            // Policy header
            AddPolicySection($"📋 {policy.PolicyName ?? "Unknown Policy"}", policy.Description ?? "");

            // Requirements
            AddPolicyRequirement("BitLocker Encryption", policy.RequiresBitLocker);
            AddPolicyRequirement("TPM Enabled", policy.RequiresTpm);
            AddPolicyRequirement("Secure Boot", policy.RequiresSecureBoot);
            // NOTE: Firewall/Defender/RTP/Signatures removed - enforced by Intune post-enrollment

            if (!string.IsNullOrEmpty(policy.MinimumOSVersion))
            {
                AddPolicyText($"Minimum OS Version: {policy.MinimumOSVersion}");
            }

            // If multiple policies were combined - use null-safe access
            var policiesUsed = _result.PoliciesUsed ?? new List<CompliancePolicyRequirements>();
            if (policiesUsed.Count > 1)
            {
                AddPolicySection("Source Policies (Assigned)", 
                    $"Combined from {policiesUsed.Count(p => p?.IsEffectivelyActive == true)} assigned policies:");
                
                foreach (var p in policiesUsed.Where(p => p?.IsEffectivelyActive == true))
                {
                    AddAssignmentInfo(p);
                }
            }

            // Show unassigned policies as warning
            var unassignedNames = _result.UnassignedPolicyNames ?? new List<string>();
            if (_result.UnassignedPolicyCount > 0)
            {
                AddWarningText($"⚠️ {_result.UnassignedPolicyCount} Unassigned Policies (excluded from analysis):");
                foreach (var name in unassignedNames.Take(5))
                {
                    AddPolicyText($"  • {name ?? "Unknown"} - Not assigned to any devices");
                }
                if (unassignedNames.Count > 5)
                {
                    AddPolicyText($"  ... and {unassignedNames.Count - 5} more");
                }
            }

            // Show assignment filter warning
            if (_result.HasAssignmentFilterWarning)
            {
                AddWarningText("⚠️ Assignment Filters Detected: Some policies use assignment filters. Actual impact may vary based on device properties.");
            }
            
            Instance.Debug("[SIMULATOR WINDOW] LoadPolicyDetails completed");
        }

        /// <summary>
        /// Add assignment information for a policy.
        /// </summary>
        private void AddAssignmentInfo(CompliancePolicyRequirements policy)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            
            panel.Children.Add(new TextBlock
            {
                Text = "• ",
                FontSize = 12,
                Width = 15
            });

            panel.Children.Add(new TextBlock
            {
                Text = policy.PolicyName,
                FontSize = 12,
                FontWeight = FontWeights.Medium
            });

            // Assignment status badge
            var assignmentColor = policy.IsAssignedToAllDevices 
                ? System.Windows.Media.Brushes.Green 
                : System.Windows.Media.Brushes.DodgerBlue;
            
            panel.Children.Add(new TextBlock
            {
                Text = $" [{policy.AssignmentSummary}]",
                FontSize = 11,
                Foreground = assignmentColor,
                VerticalAlignment = VerticalAlignment.Center
            });

            // Filter warning indicator
            if (policy.HasAssignmentFilters)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = " ⚡",
                    FontSize = 11,
                    ToolTip = "Uses assignment filters - simulation may not reflect actual assignment",
                    Foreground = System.Windows.Media.Brushes.Orange
                });
            }

            PolicyDetailsPanel.Children.Add(panel);
        }

        /// <summary>
        /// Add a warning text block.
        /// </summary>
        private void AddWarningText(string text)
        {
            var border = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(255, 243, 205)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(255, 193, 7)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 10, 0, 5)
            };

            border.Child = new TextBlock
            {
                Text = text,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(133, 100, 4))
            };

            PolicyDetailsPanel.Children.Add(border);
        }

        /// <summary>
        /// Add a policy section header.
        /// </summary>
        private void AddPolicySection(string title, string description)
        {
            var header = new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 0, 5)
            };
            PolicyDetailsPanel.Children.Add(header);

            if (!string.IsNullOrEmpty(description))
            {
                var desc = new TextBlock
                {
                    Text = description,
                    FontSize = 12,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                PolicyDetailsPanel.Children.Add(desc);
            }
        }

        /// <summary>
        /// Add a policy requirement row.
        /// </summary>
        private void AddPolicyRequirement(string name, bool required)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            
            panel.Children.Add(new TextBlock
            {
                Text = required ? "✓" : "○",
                FontSize = 12,
                Foreground = required 
                    ? System.Windows.Media.Brushes.Green 
                    : System.Windows.Media.Brushes.Gray,
                Width = 20
            });

            panel.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 12,
                Foreground = required 
                    ? System.Windows.Media.Brushes.Black 
                    : System.Windows.Media.Brushes.Gray
            });

            if (required)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = " (Required)",
                    FontSize = 11,
                    Foreground = System.Windows.Media.Brushes.DarkGreen,
                    FontStyle = FontStyles.Italic
                });
            }

            PolicyDetailsPanel.Children.Add(panel);
        }

        /// <summary>
        /// Add policy text.
        /// </summary>
        private void AddPolicyText(string text)
        {
            PolicyDetailsPanel.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 12,
                Margin = new Thickness(0, 5, 0, 5)
            });
        }

        /// <summary>
        /// Populate the gap filter dropdown.
        /// </summary>
        private void PopulateGapFilter()
        {
            Instance.Debug("[SIMULATOR WINDOW] PopulateGapFilter starting...");
            
            // Already has "All Gaps" as first item
            var gaps = _result.GapSummaries ?? new List<GapSummary>();
            foreach (var gap in gaps.OrderByDescending(g => g?.DeviceCount ?? 0))
            {
                if (gap == null) continue;
                GapFilterCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"{gap.Icon ?? "⚠️"} {gap.Requirement ?? "Unknown"} ({gap.DeviceCount})"
                });
            }
            
            Instance.Debug($"[SIMULATOR WINDOW] Added {gaps.Count} gap filter items");
        }

        /// <summary>
        /// Handle gap filter selection change.
        /// </summary>
        private void GapFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Guard against event firing during initialization before _result is set
            if (_result == null || _result.GapSummaries == null)
            {
                return;
            }
            
            if (GapFilterCombo.SelectedIndex == 0)
            {
                // Show all
                RemediationDevicesGrid.ItemsSource = _allRemediationDevices;
            }
            else
            {
                // Filter by selected gap - bounds check
                var gapIndex = GapFilterCombo.SelectedIndex - 1;
                if (gapIndex >= 0 && gapIndex < _result.GapSummaries.Count)
                {
                    var selectedGap = _result.GapSummaries[gapIndex];
                    var filtered = _allRemediationDevices
                        .Where(d => d.Gaps != null && d.Gaps.Contains(selectedGap?.Requirement ?? ""))
                        .ToList();
                    RemediationDevicesGrid.ItemsSource = filtered;
                }
                else
                {
                    // Fallback to show all if index is invalid
                    RemediationDevicesGrid.ItemsSource = _allRemediationDevices;
                }
            }
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

        /// <summary>
        /// Export full report.
        /// </summary>
        private void ExportAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"EnrollmentSimulation_{DateTime.Now:yyyyMMdd_HHmm}",
                    DefaultExt = ".csv",
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    ExportFullReport(saveDialog.FileName);
                    MessageBox.Show($"Report exported to:\n{saveDialog.FileName}",
                        "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Instance.Error($"[SIMULATOR WINDOW] Export failed: {ex.Message}");
                MessageBox.Show($"Export failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Export full report to CSV.
        /// </summary>
        private void ExportFullReport(string filePath)
        {
            var lines = new List<string>
            {
                "Enrollment Impact Simulation Report",
                $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}",
                $"Policy Used: {_result.PrimaryPolicy?.PolicyName ?? "Default"}",
                "",
                "SUMMARY",
                $"Total Devices,{_result.TotalDevices}",
                $"Enrolled,{_result.EnrolledDevices}",
                $"Unenrolled,{_result.UnenrolledDevices}",
                $"Ready to Enroll,{_result.WouldBeCompliantCount}",
                $"Needs Remediation,{_result.WouldFailCount}",
                $"Current Compliance,{_result.CurrentComplianceRate:F1}%",
                $"Projected Compliance,{_result.ProjectedComplianceRate:F1}%",
                $"Data Freshness,{_result.DataFreshnessScore:F1}%",
                "",
                "GAP ANALYSIS",
                "Requirement,Affected Devices,Percentage,Remediation,Effort,Auto-Remediate"
            };

            foreach (var gap in _result.GapSummaries ?? Enumerable.Empty<GapSummary>())
            {
                lines.Add($"\"{gap?.Requirement ?? ""}\",{gap?.DeviceCount ?? 0},{gap?.Percentage ?? 0:F1}%,\"{gap?.RemediationAction ?? ""}\",{gap?.RemediationEffort ?? ""},{(gap?.CanAutoRemediate == true ? "Yes" : "No")}");
            }

            lines.Add("");
            lines.Add("DEVICES NEEDING REMEDIATION");
            lines.Add("Device Name,Gap Count,Compliance Gaps,Max Effort");

            foreach (var device in (_result.DeviceResults ?? Enumerable.Empty<DeviceSimulationResult>()).Where(d => !d.WouldBeCompliant))
            {
                var deviceGaps = device.Gaps ?? new List<ComplianceGap>();
                var gaps = string.Join("; ", deviceGaps.Select(g => g?.Requirement ?? ""));
                var maxEffort = GetMaxEffort(deviceGaps.Select(g => g?.RemediationEffort ?? ""));
                lines.Add($"\"{device.DeviceName ?? ""}\",{deviceGaps.Count},\"{gaps}\",{maxEffort}");
            }

            lines.Add("");
            lines.Add("READY DEVICES");
            lines.Add("Device Name");

            foreach (var device in (_result.DeviceResults ?? Enumerable.Empty<DeviceSimulationResult>()).Where(d => d.WouldBeCompliant))
            {
                lines.Add($"\"{device.DeviceName ?? ""}\"");
            }

            System.IO.File.WriteAllLines(filePath, lines);
            Instance.Info($"[SIMULATOR WINDOW] Exported full report to {filePath}");
        }

        /// <summary>
        /// Close the window.
        /// </summary>
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    /// <summary>
    /// Display model for gap analysis grid.
    /// </summary>
    public class GapAnalysisItem
    {
        public string Icon { get; set; } = "";
        public string Requirement { get; set; } = "";
        public int DeviceCount { get; set; }
        public double Percentage { get; set; }
        public string RemediationAction { get; set; } = "";
        public string RemediationEffort { get; set; } = "";
        public string AutoRemediateText { get; set; } = "";
    }

    /// <summary>
    /// Display model for device grids.
    /// </summary>
    public class DeviceDisplayItem
    {
        public string DeviceName { get; set; } = "";
        public int GapCount { get; set; }
        public string GapsList { get; set; } = "";
        public List<string> Gaps { get; set; } = new();
        public string MaxEffort { get; set; } = "";
        public string LastScanText { get; set; } = "";
        public string OSVersion { get; set; } = "";
        public string BitLockerText { get; set; } = "";
        public string FirewallText { get; set; } = "";
        public string DefenderText { get; set; } = "";
        public string TpmText { get; set; } = "";
        public string SecureBootText { get; set; } = "";
    }
}
