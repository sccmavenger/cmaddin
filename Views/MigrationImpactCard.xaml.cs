using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ZeroTrustMigrationAddin.Models;
using ZeroTrustMigrationAddin.Services;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Views
{
    /// <summary>
    /// Interaction logic for MigrationImpactCard.xaml
    /// Displays the comprehensive migration impact forecast.
    /// </summary>
    public partial class MigrationImpactCard : UserControl
    {
        private MigrationImpactResult? _currentResult;
        private GraphDataService? _graphService;
        private bool _isBreakdownVisible = false;

        public MigrationImpactCard()
        {
            InitializeComponent();
            LoadDefaultData();
        }

        /// <summary>
        /// Refreshes the impact analysis with real data.
        /// </summary>
        public async Task RefreshAsync(GraphDataService? graphService)
        {
            _graphService = graphService;
            
            try
            {
                Instance.Info("[MIGRATION IMPACT CARD] Computing impact analysis...");
                
                var service = new MigrationImpactService(graphService, null);
                _currentResult = await service.ComputeImpactAsync();
                
                UpdateUI(_currentResult);
                
                Instance.Info($"[MIGRATION IMPACT CARD] Analysis complete: {_currentResult.OverallCurrentScore} → {_currentResult.OverallProjectedScore}");
            }
            catch (Exception ex)
            {
                Instance.Error($"[MIGRATION IMPACT CARD] Error computing impact: {ex.Message}");
                LoadDefaultData();
            }
        }

        private void LoadDefaultData()
        {
            // Show demo data when not connected
            CurrentScoreText.Text = "45";
            ProjectedScoreText.Text = "82";
            ImprovementText.Text = "+37";
            SummaryText.Text = "Connect to Graph API to calculate your personalized migration impact forecast. " +
                               "The analysis will show improvements across Security, Operations, User Experience, Cost, Compliance, and Modernization.";
            
            CategoryList.ItemsSource = null;
            BenefitsList.ItemsSource = null;
        }

        private void UpdateUI(MigrationImpactResult result)
        {
            CurrentScoreText.Text = result.OverallCurrentScore.ToString();
            ProjectedScoreText.Text = result.OverallProjectedScore.ToString();
            ImprovementText.Text = $"+{result.OverallImprovement}";
            SummaryText.Text = result.ExecutiveSummary;
            
            // Update category list
            var categoryViewModels = result.CategoryImpacts.Select(c => new CategoryViewModel
            {
                Icon = c.Icon,
                DisplayName = c.DisplayName,
                CurrentScore = c.CurrentScore,
                ProjectedScore = c.ProjectedScore,
                Color = c.Color
            }).ToList();
            CategoryList.ItemsSource = categoryViewModels;
            
            // Update benefits list
            var benefitViewModels = result.TopBenefits.Select(b => new BenefitViewModel
            {
                Icon = b.Icon,
                Title = b.Title,
                Description = b.Description,
                QuantifiedImpact = b.QuantifiedImpact
            }).ToList();
            BenefitsList.ItemsSource = benefitViewModels;
        }

        private void ShowBreakdownButton_Click(object sender, RoutedEventArgs e)
        {
            _isBreakdownVisible = !_isBreakdownVisible;
            BreakdownSection.Visibility = _isBreakdownVisible ? Visibility.Visible : Visibility.Collapsed;
            ShowBreakdownButton.Content = _isBreakdownVisible ? "▲ Hide Details" : "▼ Show Details";
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshButton.IsEnabled = false;
            RefreshButton.Content = "⏳ Loading...";
            
            try
            {
                await RefreshAsync(_graphService);
            }
            finally
            {
                RefreshButton.IsEnabled = true;
                RefreshButton.Content = "🔄 Refresh";
            }
        }

        private void ViewFullReportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var reportWindow = new MigrationImpactReportWindow(_currentResult);
                reportWindow.Owner = Window.GetWindow(this);
                reportWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Instance.Error($"[MIGRATION IMPACT CARD] Failed to open report: {ex.Message}");
                MessageBox.Show($"Unable to open report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ViewModels for binding
        private class CategoryViewModel
        {
            public string Icon { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public int CurrentScore { get; set; }
            public int ProjectedScore { get; set; }
            public string Color { get; set; } = "#10B981";
        }

        private class BenefitViewModel
        {
            public string Icon { get; set; } = "";
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
            public string QuantifiedImpact { get; set; } = "";
        }
    }
}
