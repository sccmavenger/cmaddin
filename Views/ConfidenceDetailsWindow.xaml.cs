using System.Collections.Generic;
using System.Windows;
using ZeroTrustMigrationAddin.Models;
using ZeroTrustMigrationAddin.Services;
using ZeroTrustMigrationAddin.ViewModels;

namespace ZeroTrustMigrationAddin.Views
{
    /// <summary>
    /// Interaction logic for ConfidenceDetailsWindow.xaml
    /// Shows detailed breakdown of the Enrollment Confidence score.
    /// </summary>
    public partial class ConfidenceDetailsWindow : Window
    {
        public ConfidenceDetailsWindow(EnrollmentConfidenceResult? result)
        {
            InitializeComponent();
            
            // Track window opened for telemetry
            AzureTelemetryService.Instance.TrackEvent("WindowOpened", new Dictionary<string, string>
            {
                { "WindowName", "ConfidenceDetailsWindow" },
                { "Score", (result?.Score ?? 0).ToString() },
                { "Band", result?.Band.ToString() ?? "Unknown" }
            });
            
            if (result != null)
            {
                PopulateFromResult(result);
            }
            else
            {
                PopulateWithDefaults();
            }
        }

        private void PopulateFromResult(EnrollmentConfidenceResult result)
        {
            // Overall score
            ScoreText.Text = result.Score.ToString();
            BandText.Text = result.Band.ToString();
            ExplanationText.Text = result.Explanation;
            
            // Set band color
            var bandColor = result.Band switch
            {
                ConfidenceBand.High => "#DCFCE7",
                ConfidenceBand.Medium => "#FEF3C7",
                ConfidenceBand.Low => "#FEE2E2",
                _ => "#F3F4F6"
            };
            var bandForeground = result.Band switch
            {
                ConfidenceBand.High => "#166534",
                ConfidenceBand.Medium => "#D97706",
                ConfidenceBand.Low => "#DC2626",
                _ => "#6B7280"
            };
            
            BandBadge.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(bandColor));
            BandText.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(bandForeground));
            
            // Breakdown scores
            VelocityBar.Value = result.Breakdown.VelocityScore;
            VelocityScore.Text = result.Breakdown.VelocityScore.ToString();
            
            SuccessBar.Value = result.Breakdown.SuccessRateScore;
            SuccessScore.Text = result.Breakdown.SuccessRateScore.ToString();
            
            ComplexityBar.Value = result.Breakdown.ComplexityScore;
            ComplexityScore.Text = result.Breakdown.ComplexityScore.ToString();
            
            InfraBar.Value = result.Breakdown.InfrastructureScore;
            InfraScore.Text = result.Breakdown.InfrastructureScore.ToString();
            
            CABar.Value = result.Breakdown.ConditionalAccessScore;
            CAScore.Text = result.Breakdown.ConditionalAccessScore.ToString();
            
            // Drivers
            var driverViewModels = new List<ScoreDriverViewModel>();
            foreach (var driver in result.TopDrivers)
            {
                driverViewModels.Add(new ScoreDriverViewModel
                {
                    Name = driver.Name,
                    Description = driver.Description,
                    Impact = driver.Impact,
                    ImpactDisplay = driver.ImpactDisplay,
                    Category = driver.Category
                });
            }
            DriversPanel.ItemsSource = driverViewModels;
            
            // Detractors
            var detractorViewModels = new List<ScoreDriverViewModel>();
            foreach (var detractor in result.TopDetractors)
            {
                detractorViewModels.Add(new ScoreDriverViewModel
                {
                    Name = detractor.Name,
                    Description = detractor.Description,
                    Impact = detractor.Impact,
                    ImpactDisplay = detractor.ImpactDisplay,
                    Category = detractor.Category
                });
            }
            DetractorsPanel.ItemsSource = detractorViewModels;
            
            SummaryText.Text = $"Score: {result.Score}/100 ({result.Band}) - {result.TopDrivers.Count} drivers, {result.TopDetractors.Count} areas for improvement";
        }

        private void PopulateWithDefaults()
        {
            // Show realistic demo data when no Graph API connection
            ScoreText.Text = "68";
            BandText.Text = "Medium";
            ExplanationText.Text = "Demo Mode: Your environment shows moderate readiness for cloud enrollment. Co-management is active, but CA policies and workload transitions can accelerate progress.";
            
            // Set medium band color
            BandBadge.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FEF3C7"));
            BandText.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D97706"));
            
            // Demo breakdown scores
            VelocityBar.Value = 72;
            VelocityScore.Text = "72";
            SuccessBar.Value = 85;
            SuccessScore.Text = "85";
            ComplexityBar.Value = 58;
            ComplexityScore.Text = "58";
            InfraBar.Value = 65;
            InfraScore.Text = "65";
            CABar.Value = 60;
            CAScore.Text = "60";
            
            // Demo drivers
            var demoDrivers = new List<ScoreDriverViewModel>
            {
                new ScoreDriverViewModel
                {
                    Name = "Co-Management Enabled",
                    Description = "35% of devices are co-managed, providing a strong foundation for cloud transition",
                    Impact = 15,
                    ImpactDisplay = "+15",
                    Category = "Infrastructure"
                },
                new ScoreDriverViewModel
                {
                    Name = "High Enrollment Success Rate",
                    Description = "92% of enrollment attempts succeed on first try",
                    Impact = 12,
                    ImpactDisplay = "+12",
                    Category = "Success Rate"
                },
                new ScoreDriverViewModel
                {
                    Name = "Active Device Sync",
                    Description = "78% of devices synced within the last 24 hours",
                    Impact = 8,
                    ImpactDisplay = "+8",
                    Category = "Velocity"
                }
            };
            DriversPanel.ItemsSource = demoDrivers;
            
            // Demo detractors
            var demoDetractors = new List<ScoreDriverViewModel>
            {
                new ScoreDriverViewModel
                {
                    Name = "Limited CA Coverage",
                    Description = "Only 45% of devices meet Conditional Access requirements",
                    Impact = -12,
                    ImpactDisplay = "-12",
                    Category = "Conditional Access"
                },
                new ScoreDriverViewModel
                {
                    Name = "Legacy OS Devices",
                    Description = "18% of devices running Windows 10 21H2 or older",
                    Impact = -8,
                    ImpactDisplay = "-8",
                    Category = "Complexity"
                },
                new ScoreDriverViewModel
                {
                    Name = "Stale Device Records",
                    Description = "12% of devices haven't synced in 30+ days",
                    Impact = -5,
                    ImpactDisplay = "-5",
                    Category = "Infrastructure"
                }
            };
            DetractorsPanel.ItemsSource = demoDetractors;
            
            SummaryText.Text = "Demo Mode: Score 68/100 (Medium) - 3 drivers, 3 areas for improvement";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
