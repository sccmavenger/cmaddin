using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using ZeroTrustMigrationAddin.Models;
using Microsoft.Graph.Models;
using Microsoft.Win32;

namespace ZeroTrustMigrationAddin.ViewModels
{
    /// <summary>
    /// ViewModel for the Device List Dialog showing filtered devices by join type
    /// </summary>
    public class DeviceListViewModel : INotifyPropertyChanged
    {
        private readonly List<ManagedDevice> _allDevices;
        private string _searchText = string.Empty;
        private ObservableCollection<ManagedDevice> _filteredDevices;

        public event PropertyChangedEventHandler? PropertyChanged;

        public DeviceListViewModel(DeviceJoinType joinType, List<ManagedDevice> devices)
        {
            _allDevices = devices ?? new List<ManagedDevice>();
            _filteredDevices = new ObservableCollection<ManagedDevice>(_allDevices);

            // Set title based on join type
            string joinTypeName = Constants.DeviceJoinTerminology.GetDisplayName(joinType);
            Title = $"{joinTypeName} Devices";
            Subtitle = $"Showing {devices.Count} devices in this category";

            ExportCommand = new RelayCommand(ExecuteExport, CanExecuteExport);
        }

        /// <summary>
        /// Constructor with custom title for blocker device lists.
        /// </summary>
        public DeviceListViewModel(string customTitle, List<ManagedDevice> devices)
        {
            _allDevices = devices ?? new List<ManagedDevice>();
            _filteredDevices = new ObservableCollection<ManagedDevice>(_allDevices);

            Title = customTitle;
            Subtitle = $"Showing {devices.Count} devices affected by this blocker";

            ExportCommand = new RelayCommand(ExecuteExport, CanExecuteExport);
        }

        public string Title { get; }
        public string Subtitle { get; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    FilterDevices();
                }
            }
        }

        public ObservableCollection<ManagedDevice> FilteredDevices
        {
            get => _filteredDevices;
            private set
            {
                _filteredDevices = value;
                OnPropertyChanged();
            }
        }

        public ICommand ExportCommand { get; }

        private void FilterDevices()
        {
            if (string.IsNullOrWhiteSpace(_searchText))
            {
                FilteredDevices = new ObservableCollection<ManagedDevice>(_allDevices);
            }
            else
            {
                var searchLower = _searchText.ToLower();
                var filtered = _allDevices.Where(d =>
                    (d.DeviceName?.ToLower().Contains(searchLower) ?? false) ||
                    (d.OperatingSystem?.ToLower().Contains(searchLower) ?? false)
                ).ToList();

                FilteredDevices = new ObservableCollection<ManagedDevice>(filtered);
            }
        }

        private bool CanExecuteExport()
        {
            return FilteredDevices != null && FilteredDevices.Count > 0;
        }

        private void ExecuteExport()
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    FileName = $"Devices_{Title.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    DefaultExt = ".csv"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ExportToCsv(saveFileDialog.FileName);
                    MessageBox.Show(
                        $"Successfully exported {FilteredDevices.Count} devices to:\n{saveFileDialog.FileName}",
                        "Export Successful",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to export devices: {ex.Message}",
                    "Export Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ExportToCsv(string filePath)
        {
            var csv = new StringBuilder();

            // Header
            csv.AppendLine("Device Name,Operating System,Last Sync,Compliance State,Enrolled Date,Management Agent,Azure AD Device ID");

            // Rows
            foreach (var device in FilteredDevices)
            {
                csv.AppendLine($"\"{device.DeviceName ?? "N/A"}\"," +
                               $"\"{device.OperatingSystem ?? "N/A"}\"," +
                               $"\"{device.LastSyncDateTime?.ToString("g") ?? "Never"}\"," +
                               $"\"{device.ComplianceState?.ToString() ?? "Unknown"}\"," +
                               $"\"{device.EnrolledDateTime?.ToString("d") ?? "N/A"}\"," +
                               $"\"{device.ManagementAgent?.ToString() ?? "Unknown"}\"," +
                               $"\"{device.AzureADDeviceId ?? "N/A"}\"");
            }

            File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
