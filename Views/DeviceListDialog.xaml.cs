using System.Windows;

namespace ZeroTrustMigrationAddin.Views
{
    /// <summary>
    /// Interaction logic for DeviceListDialog.xaml
    /// </summary>
    public partial class DeviceListDialog : Window
    {
        public DeviceListDialog()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
