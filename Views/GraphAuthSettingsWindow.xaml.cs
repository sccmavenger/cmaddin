using System.Windows;
using ZeroTrustMigrationAddin.Models;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Views
{
    /// <summary>
    /// Settings window for Microsoft Graph authentication configuration.
    /// Allows users to change auth method and specify custom app registration.
    /// </summary>
    public partial class GraphAuthSettingsWindow : Window
    {
        private GraphAuthSettings _settings;
        private readonly Services.GraphDataService? _graphDataService;

        public GraphAuthSettingsWindow(Services.GraphDataService? graphDataService = null)
        {
            InitializeComponent();
            _graphDataService = graphDataService;
            _settings = GraphAuthSettings.Load();
            LoadSettingsToUI();
        }

        /// <summary>
        /// Loads current settings into UI controls.
        /// </summary>
        private void LoadSettingsToUI()
        {
            // Auth method
            if (_settings.AuthMethod == GraphAuthMethod.InteractiveBrowser)
            {
                BrowserAuthRadio.IsChecked = true;
            }
            else
            {
                DeviceCodeRadio.IsChecked = true;
            }

            // Custom app registration
            UseCustomAppCheckBox.IsChecked = _settings.UseCustomApp;
            ClientIdTextBox.Text = _settings.CustomClientId ?? "";
            TenantIdTextBox.Text = _settings.CustomTenantId ?? "";
            
            // Update visibility
            CustomAppPanel.Visibility = _settings.UseCustomApp ? Visibility.Visible : Visibility.Collapsed;

            // Connection status
            UpdateConnectionStatus();
        }

        /// <summary>
        /// Updates the connection status display.
        /// </summary>
        private void UpdateConnectionStatus()
        {
            if (!string.IsNullOrEmpty(_settings.DetectedTenantName))
            {
                TenantNameText.Text = _settings.DetectedTenantName;
                if (!string.IsNullOrEmpty(_settings.DetectedTenantId))
                {
                    TenantNameText.Text += $" ({_settings.DetectedTenantId.Substring(0, 8)}...)";
                }
            }
            else
            {
                TenantNameText.Text = "Not connected";
            }

            CurrentMethodText.Text = _settings.AuthMethod == GraphAuthMethod.InteractiveBrowser 
                ? "Interactive Browser" 
                : "Device Code Flow";
        }

        /// <summary>
        /// Handles changes to the custom app checkbox.
        /// </summary>
        private void UseCustomAppCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            CustomAppPanel.Visibility = UseCustomAppCheckBox.IsChecked == true 
                ? Visibility.Visible 
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Saves settings and closes the window.
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Read values from UI
            _settings.AuthMethod = BrowserAuthRadio.IsChecked == true 
                ? GraphAuthMethod.InteractiveBrowser 
                : GraphAuthMethod.DeviceCode;
            
            _settings.UseCustomApp = UseCustomAppCheckBox.IsChecked == true;
            
            if (_settings.UseCustomApp)
            {
                var clientId = ClientIdTextBox.Text?.Trim();
                var tenantId = TenantIdTextBox.Text?.Trim();
                
                // Validate Client ID if custom app is enabled
                if (string.IsNullOrWhiteSpace(clientId))
                {
                    MessageBox.Show(
                        "Please enter an Application (Client) ID when using a custom app registration.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Basic GUID validation
                if (!System.Guid.TryParse(clientId, out _))
                {
                    MessageBox.Show(
                        "The Application (Client) ID must be a valid GUID.\n\n" +
                        "Example: 12345678-1234-1234-1234-123456789abc",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                _settings.CustomClientId = clientId;
                _settings.CustomTenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;
            }
            else
            {
                // Clear custom app settings when disabled
                _settings.CustomClientId = null;
                _settings.CustomTenantId = null;
            }

            // Save to disk
            _settings.Save();
            
            // Notify GraphDataService to reload settings
            _graphDataService?.ReloadAuthSettings();
            
            Instance.Info($"Graph auth settings saved: Method={_settings.AuthMethod}, UseCustomApp={_settings.UseCustomApp}");

            MessageBox.Show(
                "Settings saved successfully!\n\n" +
                "The new authentication settings will be used the next time you connect to Microsoft Graph.",
                "Settings Saved",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Resets settings to defaults.
        /// </summary>
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Reset all settings to defaults?\n\n" +
                "This will:\n" +
                "• Set auth method to Interactive Browser\n" +
                "• Disable custom app registration\n" +
                "• Clear stored tenant information",
                "Confirm Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _settings.Reset();
                _graphDataService?.ReloadAuthSettings();
                LoadSettingsToUI();
                
                MessageBox.Show(
                    "Settings have been reset to defaults.",
                    "Reset Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Cancels and closes the window.
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
