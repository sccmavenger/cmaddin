using System;
using System.Windows;

namespace ZeroTrustMigrationAddin.Views
{
    public partial class DiagnosticsWindow : Window
    {
        public event EventHandler<string>? ManualConfigMgrRequested;
        
        public DiagnosticsWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
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
