using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ZeroTrustMigrationAddin.Services;

namespace ZeroTrustMigrationAddin.Views
{
    public partial class DiagnosticsWindow : Window
    {
        public event EventHandler<string>? ManualConfigMgrRequested;
        
        public DiagnosticsWindow()
        {
            InitializeComponent();
            LoadQueryLog();
            
            // Initialize telemetry toggle state
            TelemetryToggle.IsChecked = AzureTelemetryService.Instance.IsTelemetryEnabled;
            UpdateTelemetryStatusText();
            
            // Track window opened for telemetry
            AzureTelemetryService.Instance.TrackEvent("WindowOpened", new Dictionary<string, string>
            {
                { "WindowName", "DiagnosticsWindow" },
                { "QueryCount", FileLogger.Instance.GetRecentQueries().Count.ToString() }
            });
        }

        private void TelemetryToggle_Checked(object sender, RoutedEventArgs e)
        {
            AzureTelemetryService.Instance.SetTelemetryEnabled(true);
            UpdateTelemetryStatusText();
        }

        private void TelemetryToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            AzureTelemetryService.Instance.SetTelemetryEnabled(false);
            UpdateTelemetryStatusText();
        }

        private void UpdateTelemetryStatusText()
        {
            TelemetryStatusText.Text = TelemetryToggle.IsChecked == true 
                ? "Telemetry Enabled" 
                : "Telemetry Disabled";
            TelemetryStatusText.Foreground = TelemetryToggle.IsChecked == true
                ? new SolidColorBrush(Color.FromRgb(0, 120, 212))
                : new SolidColorBrush(Color.FromRgb(102, 102, 102));
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void LoadQueryLog()
        {
            try
            {
                var queries = FileLogger.Instance.GetRecentQueries();
                QueryLogGrid.ItemsSource = queries;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load query log: {ex.Message}");
            }
        }

        private void RefreshQueryLog_Click(object sender, RoutedEventArgs e)
        {
            LoadQueryLog();
        }

        private void CopyQuery_Click(object sender, RoutedEventArgs e)
        {
            if (QueryLogGrid.SelectedItem is QueryLogEntry entry)
            {
                Clipboard.SetText(entry.CopyableQuery);
                MessageBox.Show($"Query copied to clipboard:\n\n{entry.CopyableQuery}", 
                    "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Please select a query from the list first.", 
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ExportQueryLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var exportPath = FileLogger.Instance.ExportQueryLog();
                if (!string.IsNullOrEmpty(exportPath))
                {
                    var result = MessageBox.Show(
                        $"Query log exported to:\n{exportPath}\n\nOpen in Notepad?",
                        "Export Complete",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start("notepad.exe", exportPath);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export query log: {ex.Message}",
                    "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
        {
            FileLogger.Instance.OpenLogDirectory();
        }

        private async void ManualConfigMgrButton_Click(object sender, RoutedEventArgs e)
        {
            var input = ConfigMgrServerDialog.Prompt(this);

            if (!string.IsNullOrWhiteSpace(input))
            {
                // Note: ConfigMgrServerDialog already cleans the input
                ManualConfigMgrRequested?.Invoke(this, input);
            }
        }

        public void SetGraphStatus(bool connected, string message, string dataSources)
        {
            GraphStatusIcon.Text = connected ? "✅" : "❌";
            GraphStatusIcon.Foreground = connected ? 
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green) :
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            GraphStatusText.Text = message;
            GraphDataSources.Text = dataSources;
        }

        public void SetConfigMgrStatus(bool connected, string message, string dataSources)
        {
            ConfigMgrStatusIcon.Text = connected ? "✅" : "❌";
            ConfigMgrStatusIcon.Foreground = connected ? 
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green) :
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            ConfigMgrStatusText.Text = message;
            ConfigMgrDataSources.Text = dataSources;
        }

        public void SetAIStatus(bool connected, string message, string dataSources)
        {
            AIStatusIcon.Text = connected ? "✅" : "❌";
            AIStatusIcon.Foreground = connected ? 
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green) :
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            AIStatusText.Text = message;
            AIDataSources.Text = dataSources;
        }

        public void SetOverallStatus(bool fullyAuthenticated, string statusHeader, string statusMessage)
        {
            OverallStatusIcon.Text = fullyAuthenticated ? "✅" : "⚠️";
            OverallStatusIcon.Foreground = fullyAuthenticated ? 
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green) :
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);
            OverallStatusHeader.Text = statusHeader;
            OverallStatusText.Text = statusMessage;
        }

        public void SetSectionsStatus(string status)
        {
            SectionsStatus.Text = status;
        }

        public void SetDebugLog(string log)
        {
            DebugLog.Text = log;
        }
    }
}
