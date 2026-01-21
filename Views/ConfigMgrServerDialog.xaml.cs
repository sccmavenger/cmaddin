using System.Windows;

namespace ZeroTrustMigrationAddin.Views
{
    /// <summary>
    /// Modern WPF dialog for ConfigMgr Site Server input.
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

        public ConfigMgrServerDialog()
        {
            InitializeComponent();
            ServerNameTextBox.Focus();
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

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            var input = ServerNameTextBox.Text?.Trim();
            
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

            // Clean the input - remove protocol and path if user pasted a full URL
            input = input
                .Replace("https://", "")
                .Replace("http://", "")
                .Split('/')[0]
                .Trim();

            ServerName = input;
            Confirmed = true;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            DialogResult = false;
            Close();
        }
    }
}
