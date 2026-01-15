using System.Windows;
using ZeroTrustMigrationAddin.Models;

namespace ZeroTrustMigrationAddin.Views
{
    /// <summary>
    /// Interaction logic for UpdateProgressWindow.xaml
    /// Shows automatic update progress without user interaction.
    /// </summary>
    public partial class UpdateProgressWindow : Window
    {
        private readonly double _maxWidth;

        public UpdateProgressWindow(UpdateCheckResult updateInfo)
        {
            InitializeComponent();
            
            NewVersionRun.Text = updateInfo.LatestVersion;
            _maxWidth = 440; // Width of progress bar container minus padding
        }

        /// <summary>
        /// Updates the progress bar and status message.
        /// </summary>
        /// <param name="percent">Progress percentage (0-100)</param>
        /// <param name="message">Status message to display</param>
        public void UpdateProgress(int percent, string message)
        {
            Dispatcher.Invoke(() =>
            {
                // Update progress bar width
                var width = (_maxWidth * percent) / 100.0;
                ProgressBar.Width = width;
                
                // Update text
                ProgressText.Text = $"{percent}%";
                StatusText.Text = message;
            });
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Prevent user from closing during update
            if (ProgressBar.Width < _maxWidth && ProgressBar.Width > 0)
            {
                e.Cancel = true;
            }
            
            base.OnClosing(e);
        }
    }
}
