using System.Windows.Controls;
using ZeroTrustMigrationAddin.ViewModels;

namespace ZeroTrustMigrationAddin.Views
{
    /// <summary>
    /// Interaction logic for EnrollmentMomentumView.xaml
    /// Displays real-time velocity metrics and trend visualization.
    /// </summary>
    public partial class EnrollmentMomentumView : UserControl
    {
        public EnrollmentMomentumView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Sets the ViewModel for data binding.
        /// </summary>
        public void SetViewModel(EnrollmentMomentumViewModel viewModel)
        {
            DataContext = viewModel;
        }
    }
}
