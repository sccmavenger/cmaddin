using System.Windows;
using CloudJourneyAddin.Models;
using CloudJourneyAddin.ViewModels;

namespace CloudJourneyAddin.Views
{
    /// <summary>
    /// Interaction logic for UpdateNotificationWindow.xaml
    /// </summary>
    public partial class UpdateNotificationWindow : Window
    {
        private readonly UpdateNotificationViewModel _viewModel;

        public UpdateNotificationWindow(UpdateCheckResult updateInfo)
        {
            InitializeComponent();
            
            _viewModel = new UpdateNotificationViewModel(updateInfo);
            DataContext = _viewModel;

            // Handle dialog result from ViewModel
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.DialogResult) && _viewModel.DialogResult)
                {
                    DialogResult = true;
                    Close();
                }
            };
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_viewModel.IsDownloading)
            {
                var result = MessageBox.Show(
                    "An update is currently downloading. Are you sure you want to cancel?",
                    "Update in Progress",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
            }

            base.OnClosing(e);
        }
    }
}
