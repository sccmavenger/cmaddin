using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using ZeroTrustMigrationAddin.Models;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Views
{
    /// <summary>
    /// Dialog for displaying co-management workload authority per device.
    /// Shows which workloads are managed by ConfigMgr vs Intune for each device.
    /// v3.17.59 - Workload visibility enhancement
    /// </summary>
    public partial class WorkloadDeviceListDialog : Window
    {
        private List<WorkloadDeviceViewModel> _allDevices = new();
        private List<WorkloadDeviceViewModel> _filteredDevices = new();

        public WorkloadDeviceListDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Sets the device data to display.
        /// </summary>
        public void SetDevices(List<DeviceWorkloadAuthority> devices, string title = null, string subtitle = null)
        {
            if (title != null)
                TitleText.Text = title;
            if (subtitle != null)
                SubtitleText.Text = subtitle;

            // Convert to view model for display
            _allDevices = devices.Select(d => new WorkloadDeviceViewModel(d)).ToList();
            _filteredDevices = _allDevices.ToList();
            
            DevicesDataGrid.ItemsSource = _filteredDevices;
            UpdateDeviceCount();
            
            Instance.Info($"[WORKLOAD DIALOG] Displaying {_allDevices.Count} devices with workload authority");
        }

        private void UpdateDeviceCount()
        {
            DeviceCountText.Text = $"{_filteredDevices.Count} devices shown";
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchBox.Text?.Trim().ToLowerInvariant() ?? "";
            
            if (string.IsNullOrEmpty(searchText))
            {
                _filteredDevices = _allDevices.ToList();
            }
            else
            {
                _filteredDevices = _allDevices
                    .Where(d => d.DeviceName.ToLowerInvariant().Contains(searchText))
                    .ToList();
            }
            
            DevicesDataGrid.ItemsSource = _filteredDevices;
            UpdateDeviceCount();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"WorkloadAuthority_{DateTime.Now:yyyyMMdd_HHmmss}",
                    DefaultExt = ".csv",
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    
                    // Header
                    sb.AppendLine("Device Name,Workloads on Intune,Compliance Policy,Device Configuration,Windows Update,Endpoint Protection,Modern Apps,Office Apps,Resource Access,Inventory");
                    
                    // Data rows
                    foreach (var device in _filteredDevices)
                    {
                        sb.AppendLine($"\"{device.DeviceName}\",{device.WorkloadsManagedByIntuneCount}," +
                            $"{GetWorkloadValue(device.CompliancePolicyManagedByConfigMgr)}," +
                            $"{GetWorkloadValue(device.DeviceConfigurationManagedByConfigMgr)}," +
                            $"{GetWorkloadValue(device.WindowsUpdateManagedByConfigMgr)}," +
                            $"{GetWorkloadValue(device.EndpointProtectionManagedByConfigMgr)}," +
                            $"{GetWorkloadValue(device.ModernAppsManagedByConfigMgr)}," +
                            $"{GetWorkloadValue(device.OfficeAppsManagedByConfigMgr)}," +
                            $"{GetWorkloadValue(device.ResourceAccessManagedByConfigMgr)}," +
                            $"{GetWorkloadValue(device.InventoryManagedByConfigMgr)}");
                    }
                    
                    File.WriteAllText(dialog.FileName, sb.ToString());
                    
                    MessageBox.Show($"Exported {_filteredDevices.Count} devices to:\n{dialog.FileName}", 
                        "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    Instance.Info($"[WORKLOAD DIALOG] Exported {_filteredDevices.Count} devices to {dialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                Instance.Error($"[WORKLOAD DIALOG] Export failed: {ex.Message}");
                MessageBox.Show($"Export failed:\n{ex.Message}", "Export Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetWorkloadValue(bool managedByConfigMgr) => managedByConfigMgr ? "ConfigMgr" : "Intune";
    }

    /// <summary>
    /// View model for displaying device workload authority with display properties.
    /// </summary>
    public class WorkloadDeviceViewModel
    {
        private readonly DeviceWorkloadAuthority _device;

        public WorkloadDeviceViewModel(DeviceWorkloadAuthority device)
        {
            _device = device;
        }

        public string DeviceName => _device.DeviceName;
        public string DeviceId => _device.DeviceId;
        public int WorkloadsManagedByIntuneCount => _device.WorkloadsManagedByIntuneCount;
        public bool AllWorkloadsManagedByIntune => _device.AllWorkloadsManagedByIntune;

        // Raw boolean values for DataTriggers
        public bool CompliancePolicyManagedByConfigMgr => _device.CompliancePolicyManagedByConfigMgr;
        public bool DeviceConfigurationManagedByConfigMgr => _device.DeviceConfigurationManagedByConfigMgr;
        public bool WindowsUpdateManagedByConfigMgr => _device.WindowsUpdateManagedByConfigMgr;
        public bool EndpointProtectionManagedByConfigMgr => _device.EndpointProtectionManagedByConfigMgr;
        public bool ModernAppsManagedByConfigMgr => _device.ModernAppsManagedByConfigMgr;
        public bool OfficeAppsManagedByConfigMgr => _device.OfficeAppsManagedByConfigMgr;
        public bool ResourceAccessManagedByConfigMgr => _device.ResourceAccessManagedByConfigMgr;
        public bool InventoryManagedByConfigMgr => _device.InventoryManagedByConfigMgr;

        // Display text (✅ or ⚙️)
        public string CompliancePolicyDisplay => _device.CompliancePolicyManagedByConfigMgr ? "⚙️" : "✅";
        public string DeviceConfigurationDisplay => _device.DeviceConfigurationManagedByConfigMgr ? "⚙️" : "✅";
        public string WindowsUpdateDisplay => _device.WindowsUpdateManagedByConfigMgr ? "⚙️" : "✅";
        public string EndpointProtectionDisplay => _device.EndpointProtectionManagedByConfigMgr ? "⚙️" : "✅";
        public string ModernAppsDisplay => _device.ModernAppsManagedByConfigMgr ? "⚙️" : "✅";
        public string OfficeAppsDisplay => _device.OfficeAppsManagedByConfigMgr ? "⚙️" : "✅";
        public string ResourceAccessDisplay => _device.ResourceAccessManagedByConfigMgr ? "⚙️" : "✅";
        public string InventoryDisplay => _device.InventoryManagedByConfigMgr ? "⚙️" : "✅";

        // Tooltips
        public string CompliancePolicyTooltip => _device.CompliancePolicyManagedByConfigMgr 
            ? "Compliance Policy: Managed by ConfigMgr" 
            : "Compliance Policy: Managed by Intune ✓";
        public string DeviceConfigurationTooltip => _device.DeviceConfigurationManagedByConfigMgr 
            ? "Device Configuration: Managed by ConfigMgr" 
            : "Device Configuration: Managed by Intune ✓";
        public string WindowsUpdateTooltip => _device.WindowsUpdateManagedByConfigMgr 
            ? "Windows Update: Managed by ConfigMgr" 
            : "Windows Update: Managed by Intune ✓";
        public string EndpointProtectionTooltip => _device.EndpointProtectionManagedByConfigMgr 
            ? "Endpoint Protection: Managed by ConfigMgr" 
            : "Endpoint Protection: Managed by Intune ✓";
        public string ModernAppsTooltip => _device.ModernAppsManagedByConfigMgr 
            ? "Modern Apps (Win32/LOB): Managed by ConfigMgr" 
            : "Modern Apps (Win32/LOB): Managed by Intune ✓";
        public string OfficeAppsTooltip => _device.OfficeAppsManagedByConfigMgr 
            ? "Office Click-to-Run Apps: Managed by ConfigMgr" 
            : "Office Click-to-Run Apps: Managed by Intune ✓";
        public string ResourceAccessTooltip => _device.ResourceAccessManagedByConfigMgr 
            ? "Resource Access (VPN, Wi-Fi, Email): Managed by ConfigMgr" 
            : "Resource Access (VPN, Wi-Fi, Email): Managed by Intune ✓";
        public string InventoryTooltip => _device.InventoryManagedByConfigMgr 
            ? "Hardware/Software Inventory: Managed by ConfigMgr" 
            : "Hardware/Software Inventory: Managed by Intune ✓";
    }
}
