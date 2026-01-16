using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ZeroTrustMigrationAddin.Models;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Views
{
    /// <summary>
    /// Interaction logic for MigrationImpactReportWindow.xaml
    /// Full detailed report of migration impact analysis.
    /// </summary>
    public partial class MigrationImpactReportWindow : Window
    {
        private readonly MigrationImpactResult? _result;

        public MigrationImpactReportWindow(MigrationImpactResult? result)
        {
            InitializeComponent();
            _result = result;
            
            if (result != null)
            {
                PopulateFromResult(result);
            }
            else
            {
                PopulateWithDefaults();
            }
        }

        private void PopulateFromResult(MigrationImpactResult result)
        {
            GeneratedText.Text = $"Generated: {result.ComputedAt:MMMM d, yyyy h:mm tt}";
            CurrentScoreText.Text = result.OverallCurrentScore.ToString();
            ProjectedScoreText.Text = result.OverallProjectedScore.ToString();
            ImprovementText.Text = $"+{result.OverallImprovement} points";
            SummaryText.Text = result.ExecutiveSummary;
            TimelineText.Text = result.TimelineEstimate;

            // Category cards
            var categoryViewModels = result.CategoryImpacts.Select(c => new CategoryViewModel
            {
                Icon = c.Icon,
                DisplayName = c.DisplayName,
                CurrentScore = c.CurrentScore,
                ProjectedScore = c.ProjectedScore,
                ScoreImprovementDisplay = $"(+{c.ScoreImprovement})",
                Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c.Color + "20")), // 20% opacity
                Summary = c.Summary,
                Metrics = c.Metrics.Take(4).Select(m => new MetricViewModel
                {
                    Name = m.Name,
                    CurrentDisplay = m.CurrentDisplay,
                    ProjectedDisplay = m.ProjectedDisplay
                }).ToList()
            }).ToList();
            CategoryCards.ItemsSource = categoryViewModels;

            // Workload list
            var workloadViewModels = result.WorkloadImpacts.Select(w => new WorkloadViewModel
            {
                Icon = w.Icon,
                WorkloadName = w.WorkloadName,
                CurrentState = w.CurrentState,
                StatusText = w.IsCloudManaged ? "Cloud-Managed ✓" : "On-Prem",
                StatusBackground = w.IsCloudManaged 
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7")),
                StatusForeground = w.IsCloudManaged
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#166534"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#92400E"))
            }).ToList();
            WorkloadList.ItemsSource = workloadViewModels;
        }

        private void PopulateWithDefaults()
        {
            GeneratedText.Text = $"Generated: {DateTime.Now:MMMM d, yyyy h:mm tt}";
            CurrentScoreText.Text = "—";
            ProjectedScoreText.Text = "—";
            ImprovementText.Text = "No data";
            SummaryText.Text = "Connect to Graph API and refresh the Migration Impact card to generate a full report.";
            TimelineText.Text = "Timeline unavailable without data";
            CategoryCards.ItemsSource = null;
            WorkloadList.ItemsSource = null;
        }

        private void CopySummary_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_result == null)
                {
                    Clipboard.SetText("No migration impact data available.");
                    return;
                }

                var summary = GenerateTextSummary(_result);
                Clipboard.SetText(summary);
                MessageBox.Show("Summary copied to clipboard!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to copy summary: {ex.Message}");
                MessageBox.Show($"Failed to copy: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GenerateTextSummary(MigrationImpactResult result)
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine("=== MIGRATION IMPACT REPORT ===");
            sb.AppendLine($"Generated: {result.ComputedAt:MMMM d, yyyy}");
            sb.AppendLine();
            sb.AppendLine($"OVERALL IMPROVEMENT: {result.OverallCurrentScore} → {result.OverallProjectedScore} (+{result.OverallImprovement} points)");
            sb.AppendLine();
            sb.AppendLine("EXECUTIVE SUMMARY:");
            sb.AppendLine(result.ExecutiveSummary);
            sb.AppendLine();
            sb.AppendLine("IMPACT BY CATEGORY:");
            
            foreach (var cat in result.CategoryImpacts)
            {
                sb.AppendLine($"  {cat.Icon} {cat.DisplayName}: {cat.CurrentScore} → {cat.ProjectedScore} (+{cat.ScoreImprovement})");
            }
            
            sb.AppendLine();
            sb.AppendLine("TOP BENEFITS:");
            foreach (var benefit in result.TopBenefits)
            {
                sb.AppendLine($"  {benefit.Icon} {benefit.Title}: {benefit.QuantifiedImpact}");
            }
            
            sb.AppendLine();
            sb.AppendLine($"ESTIMATED TIMELINE: {result.TimelineEstimate}");
            
            return sb.ToString();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ViewModels for binding
        private class CategoryViewModel
        {
            public string Icon { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public int CurrentScore { get; set; }
            public int ProjectedScore { get; set; }
            public string ScoreImprovementDisplay { get; set; } = "";
            public Brush Color { get; set; } = Brushes.LightGray;
            public string Summary { get; set; } = "";
            public List<MetricViewModel> Metrics { get; set; } = new();
        }

        private class MetricViewModel
        {
            public string Name { get; set; } = "";
            public string CurrentDisplay { get; set; } = "";
            public string ProjectedDisplay { get; set; } = "";
        }

        private class WorkloadViewModel
        {
            public string Icon { get; set; } = "";
            public string WorkloadName { get; set; } = "";
            public string CurrentState { get; set; } = "";
            public string StatusText { get; set; } = "";
            public Brush StatusBackground { get; set; } = Brushes.LightGray;
            public Brush StatusForeground { get; set; } = Brushes.Black;
        }
    }
}
