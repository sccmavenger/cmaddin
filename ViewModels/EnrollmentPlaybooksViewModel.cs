using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ZeroTrustMigrationAddin.Models;
using ZeroTrustMigrationAddin.Services;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.ViewModels
{
    /// <summary>
    /// ViewModel for the Enrollment Playbooks list view.
    /// Displays recommended actions and allows exporting to Markdown.
    /// </summary>
    public class EnrollmentPlaybooksViewModel : ViewModelBase
    {
        #region Observable Properties

        private ObservableCollection<PlaybookViewModel> _playbooks = new();
        public ObservableCollection<PlaybookViewModel> Playbooks
        {
            get => _playbooks;
            set => SetProperty(ref _playbooks, value);
        }

        private PlaybookViewModel? _selectedPlaybook;
        public PlaybookViewModel? SelectedPlaybook
        {
            get => _selectedPlaybook;
            set
            {
                if (SetProperty(ref _selectedPlaybook, value))
                {
                    OnPropertyChanged(nameof(HasSelectedPlaybook));
                }
            }
        }

        public bool HasSelectedPlaybook => SelectedPlaybook != null;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private int _totalPlaybooks;
        public int TotalPlaybooks
        {
            get => _totalPlaybooks;
            set => SetProperty(ref _totalPlaybooks, value);
        }

        private int _highPriorityCount;
        public int HighPriorityCount
        {
            get => _highPriorityCount;
            set => SetProperty(ref _highPriorityCount, value);
        }

        private int _mediumPriorityCount;
        public int MediumPriorityCount
        {
            get => _mediumPriorityCount;
            set => SetProperty(ref _mediumPriorityCount, value);
        }

        // Filter options
        private string _selectedRiskFilter = "All";
        public string SelectedRiskFilter
        {
            get => _selectedRiskFilter;
            set
            {
                if (SetProperty(ref _selectedRiskFilter, value))
                {
                    ApplyFilters();
                }
            }
        }

        private string _selectedTypeFilter = "All";
        public string SelectedTypeFilter
        {
            get => _selectedTypeFilter;
            set
            {
                if (SetProperty(ref _selectedTypeFilter, value))
                {
                    ApplyFilters();
                }
            }
        }

        public List<string> RiskFilterOptions { get; } = new() { "All", "Low", "Medium", "High" };
        public List<string> TypeFilterOptions { get; } = new() { "All", "Enrollment", "Hygiene", "Security", "Monitoring" };

        #endregion

        #region Commands

        public ICommand ExportToMarkdownCommand { get; }
        public ICommand ExportSelectedCommand { get; }
        public ICommand CopyStepsCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ExecutePlaybookCommand { get; }

        #endregion

        private List<EnrollmentPlaybook> _allPlaybooks = new();

        public EnrollmentPlaybooksViewModel()
        {
            ExportToMarkdownCommand = new RelayCommand(async () => await ExportAllToMarkdownAsync());
            ExportSelectedCommand = new RelayCommand(async () => await ExportSelectedToMarkdownAsync(), () => HasSelectedPlaybook);
            CopyStepsCommand = new RelayCommand(() => CopyStepsToClipboard(), () => HasSelectedPlaybook);
            RefreshCommand = new RelayCommand(async () => await Task.CompletedTask);
            ExecutePlaybookCommand = new RelayCommand<PlaybookViewModel>(pb => ExecutePlaybook(pb));
        }

        /// <summary>
        /// v3.16.23 - Refresh playbooks from real Graph/ConfigMgr data
        /// </summary>
        public async Task RefreshAsync(GraphDataService graphDataService)
        {
            try
            {
                Instance.Info("[PLAYBOOKS VM] Refreshing with real data...");
                IsLoading = true;
                StatusMessage = "Loading playbook recommendations...";
                
                var analyticsService = new EnrollmentAnalyticsService(graphDataService);
                var result = await analyticsService.ComputeAsync();
                
                if (result?.RecommendedPlaybooks != null)
                {
                    UpdateFromPlaybooks(result.RecommendedPlaybooks);
                    Instance.Info($"[PLAYBOOKS VM] Refreshed with real data: {result.RecommendedPlaybooks.Count} playbooks");
                }
                else
                {
                    Instance.Warning("[PLAYBOOKS VM] No playbook data returned from analytics service");
                }
            }
            catch (Exception ex)
            {
                Instance.Error($"[PLAYBOOKS VM] Refresh failed: {ex.Message}");
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Updates the view model from a list of playbooks.
        /// </summary>
        public void UpdateFromPlaybooks(List<EnrollmentPlaybook> playbooks)
        {
            _allPlaybooks = playbooks ?? new List<EnrollmentPlaybook>();
            TotalPlaybooks = _allPlaybooks.Count;
            HighPriorityCount = _allPlaybooks.Count(p => p.RiskLevel == PlaybookRiskLevel.Low);
            MediumPriorityCount = _allPlaybooks.Count(p => p.RiskLevel == PlaybookRiskLevel.Medium);

            ApplyFilters();
            
            Instance.Debug($"[PLAYBOOKS VM] Updated: {TotalPlaybooks} playbooks loaded");
        }

        private void ApplyFilters()
        {
            var filtered = _allPlaybooks.AsEnumerable();

            // Apply risk filter
            if (SelectedRiskFilter != "All" && Enum.TryParse<PlaybookRiskLevel>(SelectedRiskFilter, out var riskLevel))
            {
                filtered = filtered.Where(p => p.RiskLevel == riskLevel);
            }

            // Apply type filter
            if (SelectedTypeFilter != "All" && Enum.TryParse<PlaybookType>(SelectedTypeFilter, out var playbookType))
            {
                filtered = filtered.Where(p => p.Type == playbookType);
            }

            // Update observable collection
            Playbooks.Clear();
            foreach (var playbook in filtered.OrderBy(p => p.RiskLevel).ThenBy(p => p.Name))
            {
                Playbooks.Add(new PlaybookViewModel(playbook));
            }

            StatusMessage = $"Showing {Playbooks.Count} of {TotalPlaybooks} playbooks";
        }

        private async Task ExportAllToMarkdownAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Exporting playbooks to Markdown...";

                var sb = new StringBuilder();
                sb.AppendLine("# Enrollment Playbooks");
                sb.AppendLine();
                sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();
                sb.AppendLine("## Summary");
                sb.AppendLine();
                sb.AppendLine($"- **Total Playbooks:** {TotalPlaybooks}");
                sb.AppendLine($"- **Low Risk (Recommended):** {HighPriorityCount}");
                sb.AppendLine($"- **Medium Risk:** {MediumPriorityCount}");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();

                foreach (var playbook in _allPlaybooks.OrderBy(p => p.RiskLevel).ThenBy(p => p.Name))
                {
                    AppendPlaybookMarkdown(sb, playbook);
                }

                // Save to file
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var exportPath = Path.Combine(documentsPath, "CloudJourney", "Exports");
                Directory.CreateDirectory(exportPath);
                
                var fileName = $"Enrollment_Playbooks_{DateTime.Now:yyyyMMdd_HHmmss}.md";
                var filePath = Path.Combine(exportPath, fileName);

                await File.WriteAllTextAsync(filePath, sb.ToString());

                StatusMessage = $"Exported to: {filePath}";
                Instance.Info($"[PLAYBOOKS VM] Exported all playbooks to: {filePath}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
                Instance.Error($"[PLAYBOOKS VM] Export failed: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ExportSelectedToMarkdownAsync()
        {
            if (SelectedPlaybook?.Playbook == null) return;

            try
            {
                IsLoading = true;
                StatusMessage = "Exporting selected playbook...";

                var sb = new StringBuilder();
                sb.AppendLine("# Enrollment Playbook");
                sb.AppendLine();
                sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();

                AppendPlaybookMarkdown(sb, SelectedPlaybook.Playbook);

                // Save to file
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var exportPath = Path.Combine(documentsPath, "CloudJourney", "Exports");
                Directory.CreateDirectory(exportPath);

                var safeName = SanitizeFileName(SelectedPlaybook.Title);
                var fileName = $"Playbook_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.md";
                var filePath = Path.Combine(exportPath, fileName);

                await File.WriteAllTextAsync(filePath, sb.ToString());

                StatusMessage = $"Exported to: {filePath}";
                Instance.Info($"[PLAYBOOKS VM] Exported playbook '{SelectedPlaybook.Title}' to: {filePath}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
                Instance.Error($"[PLAYBOOKS VM] Export selected failed: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void AppendPlaybookMarkdown(StringBuilder sb, EnrollmentPlaybook playbook)
        {
            sb.AppendLine($"## {playbook.Name}");
            sb.AppendLine();
            sb.AppendLine($"**Risk Level:** {GetRiskBadge(playbook.RiskLevel)}");
            sb.AppendLine($"**Type:** {playbook.Type}");
            sb.AppendLine($"**Estimated Duration:** {playbook.EstimatedTime}");
            sb.AppendLine($"**Affected Devices:** {playbook.ExpectedImpactDevices}");
            sb.AppendLine();
            sb.AppendLine($"### Description");
            sb.AppendLine();
            sb.AppendLine(playbook.Description);
            sb.AppendLine();
            sb.AppendLine("### Prerequisites");
            sb.AppendLine();
            foreach (var prereq in playbook.Prerequisites)
            {
                sb.AppendLine($"- [ ] {prereq}");
            }
            sb.AppendLine();
            sb.AppendLine("### Steps");
            sb.AppendLine();
            foreach (var step in playbook.Steps.OrderBy(s => s.Order))
            {
                sb.AppendLine($"{step.Order}. **{step.Title}**");
                sb.AppendLine($"   - {step.Description}");
                if (!string.IsNullOrEmpty(step.PortalLink))
                {
                    sb.AppendLine($"   - 📚 [Documentation]({step.PortalLink})");
                }
                sb.AppendLine();
            }
            sb.AppendLine("### Expected Outcome");
            sb.AppendLine();
            sb.AppendLine($"- ✅ {playbook.RecommendationReason}");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        private string GetRiskBadge(PlaybookRiskLevel risk)
        {
            return risk switch
            {
                PlaybookRiskLevel.Low => "🟢 Low",
                PlaybookRiskLevel.Medium => "🟡 Medium",
                PlaybookRiskLevel.High => "🔴 High",
                _ => "⚪ Unknown"
            };
        }

        private void CopyStepsToClipboard()
        {
            if (SelectedPlaybook?.Playbook == null) return;

            var sb = new StringBuilder();
            foreach (var step in SelectedPlaybook.Playbook.Steps.OrderBy(s => s.Order))
            {
                sb.AppendLine($"{step.Order}. {step.Title}");
                sb.AppendLine($"   {step.Description}");
                if (!string.IsNullOrEmpty(step.PortalLink))
                {
                    sb.AppendLine($"   Link: {step.PortalLink}");
                }
                sb.AppendLine();
            }

            System.Windows.Clipboard.SetText(sb.ToString());
            StatusMessage = "Steps copied to clipboard";
            Instance.Debug($"[PLAYBOOKS VM] Steps copied to clipboard for: {SelectedPlaybook.Title}");
        }

        private void ExecutePlaybook(PlaybookViewModel? playbook)
        {
            if (playbook?.Playbook == null)
            {
                Instance.Warning("[PLAYBOOKS VM] Execute called with null playbook");
                return;
            }

            // Log intent - actual execution requires explicit user confirmation
            Instance.Info($"[PLAYBOOKS VM] Execute playbook requested: {playbook.Title}");
            StatusMessage = $"Playbook '{playbook.Title}' ready for execution (requires confirmation)";
            
            // TODO: Show execution confirmation dialog
        }

        private string SanitizeFileName(string fileName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        }
    }

    /// <summary>
    /// ViewModel wrapper for EnrollmentPlaybook display.
    /// </summary>
    public class PlaybookViewModel : ViewModelBase
    {
        public EnrollmentPlaybook Playbook { get; }

        public string Title => Playbook.Name;
        public string Description => Playbook.Description;
        public string Type => Playbook.Type.ToString();
        public string RiskLevel => Playbook.RiskLevel.ToString();
        public string RiskColor => Playbook.RiskLevel switch
        {
            PlaybookRiskLevel.Low => "#10B981",    // Green
            PlaybookRiskLevel.Medium => "#F59E0B", // Amber
            PlaybookRiskLevel.High => "#EF4444",   // Red
            _ => "#6B7280"                          // Gray
        };
        public string RiskBadge => Playbook.RiskLevel switch
        {
            PlaybookRiskLevel.Low => "✓ Low Risk",
            PlaybookRiskLevel.Medium => "⚠ Medium Risk",
            PlaybookRiskLevel.High => "⚠ High Risk",
            _ => "Unknown"
        };
        public string EstimatedDuration => Playbook.EstimatedTime;
        public int AffectedDeviceCount => Playbook.ExpectedImpactDevices;
        public int StepCount => Playbook.Steps.Count;
        public string StepsDisplay => $"{StepCount} steps";

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        private bool _isCompleted;
        public bool IsCompleted
        {
            get => _isCompleted;
            set => SetProperty(ref _isCompleted, value);
        }

        public ObservableCollection<PlaybookStepViewModel> Steps { get; } = new();

        public PlaybookViewModel(EnrollmentPlaybook playbook)
        {
            Playbook = playbook;
            foreach (var step in playbook.Steps.OrderBy(s => s.Order))
            {
                Steps.Add(new PlaybookStepViewModel(step));
            }
        }
    }

    /// <summary>
    /// ViewModel wrapper for PlaybookStep display.
    /// </summary>
    public class PlaybookStepViewModel : ViewModelBase
    {
        public PlaybookStep Step { get; }

        public int Order => Step.Order;
        public string Title => Step.Title;
        public string Description => Step.Description;
        public string? Script => null; // Not in current model - placeholder
        public bool HasScript => false;
        public string? Documentation => Step.PortalLink;
        public bool HasDocumentation => !string.IsNullOrEmpty(Step.PortalLink);
        public bool IsManual => Step.RequiresConfirmation;
        public bool IsAutomatable => !Step.RequiresConfirmation;

        private bool _isCompleted;
        public bool IsCompleted
        {
            get => _isCompleted;
            set => SetProperty(ref _isCompleted, value);
        }

        public PlaybookStepViewModel(PlaybookStep step)
        {
            Step = step;
        }
    }
}
