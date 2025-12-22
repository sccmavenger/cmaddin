# Installation Guide

## Prerequisites

- ConfigMgr Console installed
- .NET 6.0 Runtime
- Windows 10/11 or Windows Server 2019+

## Installation Steps

### 1. Build the Project

```powershell
cd "c:\Users\dannygu\Downloads\GitHub Copilot\cmaddin"
dotnet build -c Release
```

### 2. Deploy to ConfigMgr Console

Copy the XML manifest file to the ConfigMgr Console Extensions folder:

```powershell
$extensionsPath = "${env:ProgramFiles(x86)}\Microsoft Configuration Manager\AdminConsole\XmlStorage\Extensions\Actions"
Copy-Item "CloudJourneyAddin.xml" -Destination $extensionsPath
```

Copy the executable and dependencies to the Console bin folder:

```powershell
$binPath = "${env:ProgramFiles(x86)}\Microsoft Configuration Manager\AdminConsole\bin"
Copy-Item "bin\Release\net6.0-windows\*" -Destination $binPath -Recurse -Force
```

### 3. Restart ConfigMgr Console

Close and reopen the ConfigMgr Console. The "Cloud Journey Progress" tab should now appear in the ribbon.

## Configuration

### Tenant Attach Setup (Required for Live Data)

1. **Enable Tenant Attach**
   - In ConfigMgr Console, go to Administration > Cloud Services > Co-management
   - Enable Tenant Attach to Microsoft Endpoint Manager
   - Complete the authentication flow

2. **Configure Graph API Permissions**
   - Register an app in Azure AD
   - Grant the following permissions:
     - `DeviceManagementManagedDevices.Read.All`
     - `DeviceManagementConfiguration.Read.All`
     - `DeviceManagementServiceConfig.Read.All`

3. **Update Configuration**
   - Edit `Services\IntegrationServices.cs`
   - Add your Azure AD app credentials
   - Implement the Graph API authentication

### Data Refresh Settings

By default, the dashboard loads data on startup and can be manually refreshed. To enable auto-refresh:

1. Modify `DashboardViewModel.cs`
2. Add a timer to call `RefreshDataAsync()` periodically

```csharp
private System.Windows.Threading.DispatcherTimer _refreshTimer;

// In constructor:
_refreshTimer = new System.Windows.Threading.DispatcherTimer();
_refreshTimer.Interval = TimeSpan.FromMinutes(15);
_refreshTimer.Tick += async (s, e) => await RefreshDataAsync();
_refreshTimer.Start();
```

## Troubleshooting

### Add-in Not Appearing

- Verify the XML file is in the correct Extensions folder
- Check the XML file format is valid
- Ensure ConfigMgr Console has been restarted

### Data Not Loading

- Check Windows Event Viewer for errors
- Verify Tenant Attach is configured correctly
- Ensure Microsoft Graph permissions are granted
- Test API connectivity manually using Graph Explorer

### Chart Display Issues

- Verify LiveCharts.Wpf package is installed
- Check for .NET runtime errors in Event Viewer
- Ensure all DLL dependencies are in the bin folder

## Uninstallation

```powershell
# Remove XML manifest
$extensionsPath = "${env:ProgramFiles(x86)}\Microsoft Configuration Manager\AdminConsole\XmlStorage\Extensions\Actions"
Remove-Item "$extensionsPath\CloudJourneyAddin.xml"

# Remove executable and dependencies
$binPath = "${env:ProgramFiles(x86)}\Microsoft Configuration Manager\AdminConsole\bin"
Remove-Item "$binPath\CloudJourneyAddin.exe"
Remove-Item "$binPath\CloudJourneyAddin.dll"
# Remove other related files as needed
```

## Support

For issues or questions:
- Review the ConfigMgr Console logs
- Check Microsoft Endpoint Manager documentation
- Contact your Microsoft representative
