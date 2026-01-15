using System.Windows.Controls;
using ZeroTrustMigrationAddin.ViewModels;

namespace ZeroTrustMigrationAddin.Views
{
    /// <summary>
    /// Interaction logic for EnrollmentPlaybooksView.xaml
    /// Displays guided playbooks with export to Markdown capability.
    /// </summary>
    public partial class EnrollmentPlaybooksView : UserControl
    {
        public EnrollmentPlaybooksView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Sets the ViewModel for data binding.
        /// </summary>
        public void SetViewModel(EnrollmentPlaybooksViewModel viewModel)
        {
            DataContext = viewModel;
        }
    }
}
