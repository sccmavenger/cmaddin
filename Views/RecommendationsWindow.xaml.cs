using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroTrustMigrationAddin.Models;
using ZeroTrustMigrationAddin.Services;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Views
{
    /// <summary>
    /// Interaction logic for RecommendationsWindow.xaml
    /// Shows actionable recommendations to improve enrollment confidence.
    /// </summary>
    public partial class RecommendationsWindow : Window
    {
        private readonly EnrollmentConfidenceResult? _result;
        private readonly GraphDataService? _graphService;

        public RecommendationsWindow(EnrollmentConfidenceResult? result, GraphDataService? graphService)
        {
            InitializeComponent();
            _result = result;
            _graphService = graphService;
            
            LoadRecommendations();
        }

        private void LoadRecommendations()
        {
            RecommendationsPanel.Children.Clear();
            
            var recommendations = GenerateRecommendations();
            
            if (recommendations.Count == 0)
            {
                AddNoRecommendationsMessage();
                return;
            }

            int priority = 1;
            foreach (var rec in recommendations)
            {
                AddRecommendationCard(rec, priority++);
            }
            
            SubtitleText.Text = $"{recommendations.Count} recommendations based on your current score of {_result?.Score ?? 0}/100";
        }

        private List<Recommendation> GenerateRecommendations()
        {
            var recommendations = new List<Recommendation>();
            
            if (_result == null)
            {
                recommendations.Add(new Recommendation
                {
                    Title = "Connect to Graph API",
                    Description = "Sign in with Graph API to enable real-time analysis of your enrollment posture.",
                    Category = "Setup",
                    Impact = "High",
                    Effort = "Low",
                    Icon = "🔗"
                });
                return recommendations;
            }

            // Analyze detractors and generate recommendations
            foreach (var detractor in _result.TopDetractors)
            {
                var rec = GenerateRecommendationForDetractor(detractor);
                if (rec != null)
                {
                    recommendations.Add(rec);
                }
            }

            // Add general recommendations based on score band
            if (_result.Band == ConfidenceBand.Low)
            {
                recommendations.Add(new Recommendation
                {
                    Title = "Enable Co-Management",
                    Description = "Co-management is the foundation for cloud migration. Enable it to start managing devices with both ConfigMgr and Intune.",
                    Category = "Infrastructure",
                    Impact = "High",
                    Effort = "Medium",
                    Icon = "🔄",
                    LearnMoreUrl = "https://learn.microsoft.com/mem/configmgr/comanage/overview"
                });
            }

            if (_result.Breakdown.VelocityScore < 50)
            {
                recommendations.Add(new Recommendation
                {
                    Title = "Increase Enrollment Velocity",
                    Description = "Your enrollment rate is below target. Consider batch enrollments, automated Autopilot registration, or dedicated enrollment sprints.",
                    Category = "Velocity",
                    Impact = "High",
                    Effort = "Medium",
                    Icon = "⚡"
                });
            }

            if (_result.Breakdown.InfrastructureScore < 60)
            {
                recommendations.Add(new Recommendation
                {
                    Title = "Deploy Cloud Management Gateway (CMG)",
                    Description = "CMG enables ConfigMgr to manage internet-based clients, bridging the gap to cloud-native management.",
                    Category = "Infrastructure",
                    Impact = "High",
                    Effort = "High",
                    Icon = "☁️",
                    LearnMoreUrl = "https://learn.microsoft.com/mem/configmgr/core/clients/manage/cmg/overview"
                });
            }

            if (_result.Breakdown.ConditionalAccessScore < 60)
            {
                recommendations.Add(new Recommendation
                {
                    Title = "Configure Conditional Access Policies",
                    Description = "Conditional Access ensures only compliant devices can access corporate resources. Start with a report-only policy to assess impact.",
                    Category = "Security",
                    Impact = "High",
                    Effort = "Medium",
                    Icon = "🛡️",
                    LearnMoreUrl = "https://learn.microsoft.com/entra/identity/conditional-access/overview"
                });
            }

            if (_result.Breakdown.SuccessRateScore < 70)
            {
                recommendations.Add(new Recommendation
                {
                    Title = "Review Enrollment Failures",
                    Description = "Your enrollment success rate indicates issues. Check Intune enrollment troubleshooting for common failure patterns.",
                    Category = "Operations",
                    Impact = "Medium",
                    Effort = "Medium",
                    Icon = "🔍",
                    LearnMoreUrl = "https://learn.microsoft.com/mem/intune/enrollment/troubleshoot-device-enrollment-in-intune"
                });
            }

            if (_result.Score >= 75)
            {
                recommendations.Add(new Recommendation
                {
                    Title = "Plan Workload Transitions",
                    Description = "Your infrastructure is ready. Start transitioning co-management workloads to Intune, beginning with Compliance Policies.",
                    Category = "Modernization",
                    Impact = "High",
                    Effort = "Medium",
                    Icon = "🎯"
                });
            }

            return recommendations;
        }

        private Recommendation? GenerateRecommendationForDetractor(ScoreDriver detractor)
        {
            return detractor.Category switch
            {
                "Velocity" => new Recommendation
                {
                    Title = $"Address: {detractor.Name}",
                    Description = $"{detractor.Description}. Consider automating enrollment through Autopilot or scheduling dedicated enrollment windows.",
                    Category = "Velocity",
                    Impact = "High",
                    Effort = "Medium",
                    Icon = "⚡"
                },
                "Infrastructure" => new Recommendation
                {
                    Title = $"Address: {detractor.Name}",
                    Description = $"{detractor.Description}. Review your cloud infrastructure readiness and address any gaps.",
                    Category = "Infrastructure",
                    Impact = "High",
                    Effort = "High",
                    Icon = "🏗️"
                },
                "Complexity" => new Recommendation
                {
                    Title = $"Address: {detractor.Name}",
                    Description = $"{detractor.Description}. Simplify your environment by standardizing configurations and reducing app dependencies.",
                    Category = "Operations",
                    Impact = "Medium",
                    Effort = "High",
                    Icon = "🔧"
                },
                _ => new Recommendation
                {
                    Title = $"Address: {detractor.Name}",
                    Description = detractor.Description,
                    Category = detractor.Category,
                    Impact = "Medium",
                    Effort = "Medium",
                    Icon = "📋"
                }
            };
        }

        private void AddRecommendationCard(Recommendation rec, int priority)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Priority badge
            var priorityBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EFF6FF")),
                CornerRadius = new CornerRadius(4),
                Width = 32,
                Height = 32,
                VerticalAlignment = VerticalAlignment.Top
            };
            var priorityText = new TextBlock
            {
                Text = priority.ToString(),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            priorityBorder.Child = priorityText;
            Grid.SetColumn(priorityBorder, 0);
            mainGrid.Children.Add(priorityBorder);

            // Content
            var contentPanel = new StackPanel { Margin = new Thickness(12, 0, 12, 0) };
            
            // Title row
            var titlePanel = new StackPanel { Orientation = Orientation.Horizontal };
            titlePanel.Children.Add(new TextBlock
            {
                Text = rec.Icon,
                FontSize = 16,
                Margin = new Thickness(0, 0, 8, 0)
            });
            titlePanel.Children.Add(new TextBlock
            {
                Text = rec.Title,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F2937"))
            });
            contentPanel.Children.Add(titlePanel);

            // Description
            contentPanel.Children.Add(new TextBlock
            {
                Text = rec.Description,
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 8)
            });

            // Tags
            var tagsPanel = new StackPanel { Orientation = Orientation.Horizontal };
            AddTag(tagsPanel, rec.Category, "#E0E7FF", "#4338CA");
            AddTag(tagsPanel, $"Impact: {rec.Impact}", GetImpactColor(rec.Impact).bg, GetImpactColor(rec.Impact).fg);
            AddTag(tagsPanel, $"Effort: {rec.Effort}", "#F3F4F6", "#6B7280");
            contentPanel.Children.Add(tagsPanel);

            // Learn more link
            if (!string.IsNullOrEmpty(rec.LearnMoreUrl))
            {
                var linkText = new TextBlock
                {
                    Text = "📖 Learn more",
                    FontSize = 11,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Margin = new Thickness(0, 8, 0, 0)
                };
                linkText.MouseLeftButtonUp += (s, e) =>
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = rec.LearnMoreUrl,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        Instance.Error($"Failed to open URL: {ex.Message}");
                    }
                };
                contentPanel.Children.Add(linkText);
            }

            Grid.SetColumn(contentPanel, 1);
            mainGrid.Children.Add(contentPanel);

            border.Child = mainGrid;
            RecommendationsPanel.Children.Add(border);
        }

        private void AddTag(StackPanel panel, string text, string bgColor, string fgColor)
        {
            var tag = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgColor)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 6, 0)
            };
            tag.Child = new TextBlock
            {
                Text = text,
                FontSize = 10,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fgColor))
            };
            panel.Children.Add(tag);
        }

        private (string bg, string fg) GetImpactColor(string impact)
        {
            return impact switch
            {
                "High" => ("#DCFCE7", "#166534"),
                "Medium" => ("#FEF3C7", "#92400E"),
                "Low" => ("#F3F4F6", "#6B7280"),
                _ => ("#F3F4F6", "#6B7280")
            };
        }

        private void AddNoRecommendationsMessage()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(24)
            };

            var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            panel.Children.Add(new TextBlock
            {
                Text = "🎉",
                FontSize = 48,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            panel.Children.Add(new TextBlock
            {
                Text = "No Recommendations Needed!",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0)
            });
            panel.Children.Add(new TextBlock
            {
                Text = "Your enrollment confidence is excellent. Keep up the great work!",
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            });

            border.Child = panel;
            RecommendationsPanel.Children.Add(border);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadRecommendations();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private class Recommendation
        {
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string Impact { get; set; } = "Medium";
            public string Effort { get; set; } = "Medium";
            public string Icon { get; set; } = "📋";
            public string? LearnMoreUrl { get; set; }
        }
    }
}
