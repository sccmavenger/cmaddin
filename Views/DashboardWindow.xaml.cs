using System;
using System.Windows;
using System.Windows.Controls;
using CloudJourneyAddin.ViewModels;
using CloudJourneyAddin.Services;

namespace CloudJourneyAddin.Views
{
    public partial class DashboardWindow : Window
    {
        public DashboardWindow()
        {
            try
            {
                InitializeComponent();
                
                // Set window size based on screen resolution
                SetAdaptiveWindowSize();
                
                var telemetryService = new TelemetryService();
                DataContext = new DashboardViewModel(telemetryService);
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
    }
}
