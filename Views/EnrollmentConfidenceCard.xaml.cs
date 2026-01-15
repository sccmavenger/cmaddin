using System.Windows.Controls;
using ZeroTrustMigrationAddin.ViewModels;

namespace ZeroTrustMigrationAddin.Views
{
    /// <summary>
    /// Interaction logic for EnrollmentConfidenceCard.xaml
    /// Displays the 0-100 confidence score with breakdown.
    /// </summary>
    public partial class EnrollmentConfidenceCard : UserControl
    {
        public EnrollmentConfidenceCard()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Sets the ViewModel for data binding.
        /// </summary>
        public void SetViewModel(EnrollmentConfidenceViewModel viewModel)
        {
            DataContext = viewModel;
        }
    }
}
