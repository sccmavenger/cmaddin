using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ZeroTrustMigrationAddin.Services;

namespace ZeroTrustMigrationAddin.Views
{
    public partial class AISettingsWindow : Window
    {
        private string _apiKey = string.Empty;

        public AISettingsWindow()
        {
            InitializeComponent();
            
            // Track window opened for telemetry
            AzureTelemetryService.Instance.TrackEvent("WindowOpened", new Dictionary<string, string>
            {
                { "WindowName", "AISettingsWindow" }
            });
        }

        private void ApiKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox && DataContext != null)
            {
                _apiKey = passwordBox.Password;
                // Update the ViewModel's property through a method since PasswordBox doesn't support binding
                var viewModel = DataContext as dynamic;
                if (viewModel != null)
                {
                    viewModel.OpenAIApiKey = _apiKey;
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public void SetApiKey(string apiKey)
        {
            _apiKey = apiKey;
            ApiKeyPasswordBox.Password = apiKey;
        }
    }
}
