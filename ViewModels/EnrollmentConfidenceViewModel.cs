using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ZeroTrustMigrationAddin.Models;
using ZeroTrustMigrationAddin.Services;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.ViewModels
{
    /// <summary>
    /// ViewModel for the Enrollment Confidence score card.
    /// Displays the 0-100 confidence score with breakdown.
    /// </summary>
    public class EnrollmentConfidenceViewModel : ViewModelBase
    {
        #region Observable Properties

        private int _score;
        public int Score
        {
            get => _score;
            set => SetProperty(ref _score, value);
        }

        private string _scoreDisplay = "0/100";
        public string ScoreDisplay
        {
            get => _scoreDisplay;
            set => SetProperty(ref _scoreDisplay, value);
        }

        private string _band = "Unknown";
        public string Band
        {
            get => _band;
            set => SetProperty(ref _band, value);
        }

        private string _bandColor = "#6B7280";
        public string BandColor
        {
            get => _bandColor;
            set => SetProperty(ref _bandColor, value);
        }

        private string _explanation = "";
        public string Explanation
        {
            get => _explanation;
            set => SetProperty(ref _explanation, value);
        }

        private ObservableCollection<ScoreDriverViewModel> _topDrivers = new();
        public ObservableCollection<ScoreDriverViewModel> TopDrivers
        {
            get => _topDrivers;
            set => SetProperty(ref _topDrivers, value);
        }

        private ObservableCollection<ScoreDriverViewModel> _topDetractors = new();
        public ObservableCollection<ScoreDriverViewModel> TopDetractors
        {
            get => _topDetractors;
            set => SetProperty(ref _topDetractors, value);
        }

        // Breakdown scores (for progress bars)
        private int _velocityScore;
        public int VelocityScore
        {
            get => _velocityScore;
            set => SetProperty(ref _velocityScore, value);
        }

        private int _successRateScore;
        public int SuccessRateScore
        {
            get => _successRateScore;
            set => SetProperty(ref _successRateScore, value);
        }

        private int _complexityScore;
        public int ComplexityScore
        {
            get => _complexityScore;
            set => SetProperty(ref _complexityScore, value);
        }

        private int _infrastructureScore;
        public int InfrastructureScore
        {
            get => _infrastructureScore;
            set => SetProperty(ref _infrastructureScore, value);
        }

        private int _conditionalAccessScore;
        public int ConditionalAccessScore
        {
            get => _conditionalAccessScore;
            set => SetProperty(ref _conditionalAccessScore, value);
        }

        private bool _showBreakdown;
        public bool ShowBreakdown
        {
            get => _showBreakdown;
            set => SetProperty(ref _showBreakdown, value);
        }

        #endregion

        #region Commands

        public ICommand ToggleBreakdownCommand { get; }
        public ICommand ViewDetailsCommand { get; }

        #endregion

        public EnrollmentConfidenceViewModel()
        {
            ToggleBreakdownCommand = new RelayCommand(() => ShowBreakdown = !ShowBreakdown);
            ViewDetailsCommand = new RelayCommand(() => ViewDetails());
        }

        /// <summary>
        /// Updates the view model from an EnrollmentConfidenceResult.
        /// </summary>
        public void UpdateFromResult(EnrollmentConfidenceResult result)
        {
            if (result == null) return;

            Score = result.Score;
            ScoreDisplay = result.ScoreDisplay;
            Band = result.Band.ToString();
            BandColor = result.BandColor;
            Explanation = result.Explanation;

            // Update breakdown
            VelocityScore = result.Breakdown.VelocityScore;
            SuccessRateScore = result.Breakdown.SuccessRateScore;
            ComplexityScore = result.Breakdown.ComplexityScore;
            InfrastructureScore = result.Breakdown.InfrastructureScore;
            ConditionalAccessScore = result.Breakdown.ConditionalAccessScore;

            // Update drivers
            TopDrivers.Clear();
            foreach (var driver in result.TopDrivers)
            {
                TopDrivers.Add(new ScoreDriverViewModel
                {
                    Name = driver.Name,
                    Description = driver.Description,
                    Impact = driver.Impact,
                    ImpactDisplay = driver.ImpactDisplay,
                    ImpactColor = driver.ImpactColor,
                    Category = driver.Category
                });
            }

            // Update detractors
            TopDetractors.Clear();
            foreach (var detractor in result.TopDetractors)
            {
                TopDetractors.Add(new ScoreDriverViewModel
                {
                    Name = detractor.Name,
                    Description = detractor.Description,
                    Impact = detractor.Impact,
                    ImpactDisplay = detractor.ImpactDisplay,
                    ImpactColor = detractor.ImpactColor,
                    Category = detractor.Category
                });
            }

            Instance.Debug($"[CONFIDENCE VM] Updated: Score={Score}, Band={Band}");
        }

        private void ViewDetails()
        {
            Instance.Info("[CONFIDENCE VM] View details clicked");
        }
    }

    /// <summary>
    /// ViewModel wrapper for ScoreDriver display.
    /// </summary>
    public class ScoreDriverViewModel : ViewModelBase
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Impact { get; set; }
        public string ImpactDisplay { get; set; } = string.Empty;
        public string ImpactColor { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }
}
