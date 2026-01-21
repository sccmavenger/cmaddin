using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using ZeroTrustMigrationAddin.Services;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Views
{
    /// <summary>
    /// Feedback submission window - allows users to submit bugs, features, and feedback
    /// directly to GitHub Issues using OAuth Device Flow authentication.
    /// </summary>
    public partial class FeedbackWindow : Window
    {
        private readonly FeedbackService _feedbackService;
        private readonly Window _mainWindow;
        private byte[]? _capturedScreenshot;

        public FeedbackWindow(Window mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            _feedbackService = new FeedbackService();
            _feedbackService.AuthenticationCompleted += OnAuthenticationCompleted;
            
            UpdateAuthStatus();
            Instance.Info("[FEEDBACK] Feedback window opened");
        }

        private void UpdateAuthStatus()
        {
            if (_feedbackService.IsAuthenticated)
            {
                AuthStatusIcon.Text = "✅";
                AuthStatusText.Text = $"Signed in as {_feedbackService.AuthenticatedUser}";
                AuthButton.Content = "Sign Out";
                AuthButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D"));
                SubmitButton.IsEnabled = true;
                SubmitButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0078D4"));
            }
            else
            {
                AuthStatusIcon.Text = "🔒";
                AuthStatusText.Text = "Sign in with GitHub to submit feedback";
                AuthButton.Content = "Sign In";
                AuthButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#24292E"));
                SubmitButton.IsEnabled = false;
                SubmitButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
            }
        }

        private void OnAuthenticationCompleted(object? sender, AuthenticationCompletedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.Success)
                {
                    UpdateAuthStatus();
                    ShowStatus($"✅ {e.Message}", "#DFF6DD");
                    AuthButton.IsEnabled = true;
                    AuthButton.Content = "Sign Out";
                }
                else
                {
                    ShowStatus($"❌ {e.Message}", "#FDECEA");
                    AuthButton.IsEnabled = true;
                    AuthButton.Content = "Sign In";
                }
            });
        }

        private async void AuthButton_Click(object sender, RoutedEventArgs e)
        {
            if (_feedbackService.IsAuthenticated)
            {
                _feedbackService.SignOut();
                UpdateAuthStatus();
                ShowStatus("Signed out from GitHub", "#E8F4FD");
            }
            else
            {
                AuthButton.IsEnabled = false;
                AuthButton.Content = "Waiting...";
                ShowStatus("🔄 Opening browser for GitHub authentication...\n\nPlease authorize the app in your browser, then return here.", "#E8F4FD");

                var result = await _feedbackService.StartAuthenticationAsync();
                
                if (!result.success)
                {
                    ShowStatus($"❌ {result.message}", "#FDECEA");
                    AuthButton.IsEnabled = true;
                    AuthButton.Content = "Sign In";
                }
                else
                {
                    ShowStatus($"✅ {result.message}\n\n⏳ Waiting for you to authorize in browser...", "#FFF4E6");
                }
            }
        }

        private async void Submit_Click(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(TitleBox.Text))
            {
                ShowStatus("❌ Please enter a title for your feedback", "#FDECEA");
                TitleBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(DescriptionBox.Text))
            {
                ShowStatus("❌ Please enter a description", "#FDECEA");
                DescriptionBox.Focus();
                return;
            }

            // Disable UI during submission
            SubmitButton.IsEnabled = false;
            SubmitButton.Content = "Submitting...";
            ShowStatus("🔄 Creating issue on GitHub...", "#E8F4FD");

            // Capture screenshot if requested
            if (IncludeScreenshotCheck.IsChecked == true)
            {
                try
                {
                    _capturedScreenshot = FeedbackService.CaptureWindowScreenshot(_mainWindow);
                    Instance.Info($"[FEEDBACK] Screenshot captured: {_capturedScreenshot?.Length ?? 0} bytes");
                }
                catch (Exception ex)
                {
                    Instance.Warning($"[FEEDBACK] Screenshot capture failed: {ex.Message}");
                    _capturedScreenshot = null;
                }
            }

            // Map combo selection to feedback type
            var feedbackType = (FeedbackType)FeedbackTypeCombo.SelectedIndex;
            
            var result = await _feedbackService.CreateFeedbackIssueAsync(
                feedbackType,
                TitleBox.Text.Trim(),
                DescriptionBox.Text.Trim(),
                _capturedScreenshot,
                IncludeSystemInfoCheck.IsChecked == true
            );

            if (result.success)
            {
                ShowStatus($"✅ {result.message}", "#DFF6DD");
                
                // Ask user if they want to open the issue
                string screenshotNote = IncludeScreenshotCheck.IsChecked == true && _capturedScreenshot != null
                    ? "\n\n📸 Screenshot copied to clipboard - paste it into the issue with Ctrl+V."
                    : "";
                    
                var openIssue = MessageBox.Show(
                    $"Issue created successfully!{screenshotNote}\n\nWould you like to open it in your browser to add more details?",
                    "Feedback Submitted",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (openIssue == MessageBoxResult.Yes && !string.IsNullOrEmpty(result.issueUrl))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = result.issueUrl,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        Instance.Warning($"[FEEDBACK] Failed to open browser: {ex.Message}");
                    }
                }

                // Close window after successful submission
                await System.Threading.Tasks.Task.Delay(500);
                DialogResult = true;
                Close();
            }
            else
            {
                ShowStatus($"❌ {result.message}", "#FDECEA");
                SubmitButton.IsEnabled = true;
                SubmitButton.Content = "Submit Feedback";
                SubmitButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0078D4"));
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ViewIssues_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/sccmavenger/cmaddin/issues",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Instance.Warning($"[FEEDBACK] Failed to open issues page: {ex.Message}");
            }
        }

        private void ShowStatus(string message, string backgroundColor)
        {
            StatusText.Text = message;
            StatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(backgroundColor));
            StatusBorder.Visibility = Visibility.Visible;
        }
    }
}
