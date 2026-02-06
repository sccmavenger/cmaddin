using System.Windows;
using ZeroTrustMigrationAddin.Services;

namespace ZeroTrustMigrationAddin.Views
{
    /// <summary>
    /// Modern WPF dialog for ConfigMgr Site Server input with optional alternate credentials.
    /// Replaces the legacy VB InputBox with a styled dialog matching the app's design.
    /// </summary>
    public partial class ConfigMgrServerDialog : Window
    {
        /// <summary>
        /// Gets the server name entered by the user, or null if cancelled.
        /// </summary>
        public string? ServerName { get; private set; }

        /// <summary>
        /// Gets whether the user confirmed the connection (clicked Connect).
        /// </summary>
        public bool Confirmed { get; private set; }
        
        /// <summary>
        /// Gets the updated settings with any credential changes.
        /// </summary>
        public ConfigMgrSettings? UpdatedSettings { get; private set; }

        public ConfigMgrServerDialog()
        {
            InitializeComponent();
            LoadSavedSettings();
            ServerNameTextBox.Focus();
        }
        
        /// <summary>
        /// Loads saved settings and populates the form fields.
        /// </summary>
        private void LoadSavedSettings()
        {
            var settings = ConfigMgrAdminService.SavedSettings;
            
            // Pre-populate server from last session
            if (!string.IsNullOrEmpty(settings.SiteServer))
            {
                ServerNameTextBox.Text = settings.SiteServer;
                ServerNameTextBox.SelectAll();
            }
            
            // Pre-populate alternate credentials if saved
            if (settings.UseAlternateCredentials)
            {
                UseAlternateCredsCheckBox.IsChecked = true;
                CredentialsPanel.Visibility = Visibility.Visible;
                TestConnectionButton.Visibility = Visibility.Visible;
                
                if (!string.IsNullOrEmpty(settings.AlternateUsername))
                {
                    UsernameTextBox.Text = settings.AlternateUsername;
                }
                
                // Don't pre-fill password for security, but show placeholder
                if (!string.IsNullOrEmpty(settings.EncryptedPassword))
                {
                    // Leave password box empty but update height to show password is saved
                    PasswordBox.Tag = "saved"; // Marker that password exists
                }
            }
        }

        /// <summary>
        /// Shows the dialog and returns the cleaned server name, or null if cancelled.
        /// </summary>
        /// <param name="owner">Optional owner window for centering.</param>
        /// <returns>The server name entered, or null if cancelled.</returns>
        public static string? Prompt(Window? owner = null)
        {
            var dialog = new ConfigMgrServerDialog();
            if (owner != null)
            {
                dialog.Owner = owner;
            }
            
            if (dialog.ShowDialog() == true && dialog.Confirmed)
            {
                return dialog.ServerName;
            }
            
            return null;
        }
        
        /// <summary>
        /// Event handler for the alternate credentials checkbox.
        /// Shows/hides the credential input fields.
        /// </summary>
        private void UseAlternateCredsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            bool isChecked = UseAlternateCredsCheckBox.IsChecked == true;
            CredentialsPanel.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
            TestConnectionButton.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
            
            // Window will auto-size due to SizeToContent="Height"
        }
        
        /// <summary>
        /// Event handler for the Test Connection button.
        /// Tests the connection with the provided credentials.
        /// </summary>
        private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            var serverName = CleanServerInput(ServerNameTextBox.Text);
            if (string.IsNullOrEmpty(serverName))
            {
                MessageBox.Show("Please enter a server name.", "Server Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (!ValidateCredentials())
            {
                return;
            }
            
            // Save settings temporarily for test
            var settings = CreateUpdatedSettings(serverName);
            settings.Save();
            
            // Test connection
            TestConnectionButton.IsEnabled = false;
            TestConnectionButton.Content = "Testing...";
            
            try
            {
                var service = new ConfigMgrAdminService();
                service.RefreshCredentials(); // Reload with new credential settings
                var adminServiceUrl = $"https://{serverName}/AdminService";
                bool success = await service.ConfigureAsync(adminServiceUrl);
                
                if (success)
                {
                    MessageBox.Show(
                        $"Successfully connected to {serverName}!\n\nConnection method: {service.ConnectionMethod}",
                        "Connection Successful",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"Failed to connect to {serverName}.\n\n{service.LastConnectionError}",
                        "Connection Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    $"Error testing connection:\n\n{ex.Message}",
                    "Connection Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                TestConnectionButton.IsEnabled = true;
                TestConnectionButton.Content = "Test Connection";
            }
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            var input = CleanServerInput(ServerNameTextBox.Text);
            
            if (string.IsNullOrWhiteSpace(input))
            {
                MessageBox.Show(
                    "Please enter a server name.",
                    "Server Name Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                ServerNameTextBox.Focus();
                return;
            }
            
            // Validate credentials if using alternate account
            if (UseAlternateCredsCheckBox.IsChecked == true)
            {
                if (!ValidateCredentials())
                {
                    return;
                }
            }

            ServerName = input;
            UpdatedSettings = CreateUpdatedSettings(input);
            UpdatedSettings.Save();
            
            Confirmed = true;
            DialogResult = true;
            Close();
        }
        
        /// <summary>
        /// Validates the alternate credential fields.
        /// </summary>
        /// <returns>True if valid, false if validation failed.</returns>
        private bool ValidateCredentials()
        {
            var username = UsernameTextBox.Text?.Trim();
            var password = PasswordBox.Password;
            var existingSettings = ConfigMgrAdminService.SavedSettings;
            bool hasExistingPassword = !string.IsNullOrEmpty(existingSettings.EncryptedPassword);
            
            // Username is required
            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show(
                    "Please enter a username.",
                    "Username Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                UsernameTextBox.Focus();
                return false;
            }
            
            // Validate username format (should contain \ or @)
            if (!username.Contains('\\') && !username.Contains('@'))
            {
                MessageBox.Show(
                    "Please enter the username in one of these formats:\n\n• DOMAIN\\username\n• username@domain.com",
                    "Invalid Username Format",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                UsernameTextBox.Focus();
                return false;
            }
            
            // Password is required (unless already saved and not changed)
            if (string.IsNullOrEmpty(password) && !hasExistingPassword)
            {
                MessageBox.Show(
                    "Please enter a password.",
                    "Password Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                PasswordBox.Focus();
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Creates an updated settings object from the current form values.
        /// </summary>
        private ConfigMgrSettings CreateUpdatedSettings(string serverName)
        {
            var settings = ConfigMgrAdminService.SavedSettings;
            settings.SiteServer = serverName;
            settings.AdminServiceUrl = $"https://{serverName}/AdminService";
            settings.UseAlternateCredentials = UseAlternateCredsCheckBox.IsChecked == true;
            
            if (settings.UseAlternateCredentials)
            {
                settings.AlternateUsername = UsernameTextBox.Text?.Trim();
                
                // Only update password if user entered a new one
                if (!string.IsNullOrEmpty(PasswordBox.Password))
                {
                    settings.SetPassword(PasswordBox.Password);
                }
                // Otherwise keep existing encrypted password
            }
            else
            {
                // Clear credentials if not using alternate account
                settings.AlternateUsername = null;
                settings.EncryptedPassword = null;
            }
            
            return settings;
        }
        
        /// <summary>
        /// Cleans the server input by removing protocol and path components.
        /// </summary>
        private static string? CleanServerInput(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;
                
            return input
                .Trim()
                .Replace("https://", "")
                .Replace("http://", "")
                .Split('/')[0]
                .Trim();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            DialogResult = false;
            Close();
        }
    }
}
