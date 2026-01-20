using System;
using System.Collections.Generic;
using System.Windows;
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
            
            // Track window opened for telemetry
            AzureTelemetryService.Instance.TrackEvent("WindowOpened", new Dictionary<string, string>
            {
                { "WindowName", "DiagnosticsWindow" },
                { "QueryCount", FileLogger.Instance.GetRecentQueries().Count.ToString() }
            });
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
            var input = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter your ConfigMgr Site Server name or FQDN:\n\n" +
                "Examples:\n" +
                "• CM01\n" +
                "• CM01.contoso.com\n" +
                "• sccm.contoso.local\n\n" +
                "The app will try both Admin Service (REST API) and WMI fallback.",
                "ConfigMgr Site Server",
                "",
                -1, -1);

            if (!string.IsNullOrWhiteSpace(input))
            {
                // Remove https:// if user pasted a full URL
                input = input.Replace("https://", "").Replace("http://", "").Split('/')[0].Trim();
                
                if (!string.IsNullOrEmpty(input))
                {
                    ManualConfigMgrRequested?.Invoke(this, input);
                }
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
