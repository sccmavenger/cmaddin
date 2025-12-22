# Development Notes

**Last Updated:** December 18, 2025 (v1.7.0 - Tabbed UI & Enrollment Momentum)

## Architecture

The Cloud Journey Progress add-in follows the MVVM (Model-View-ViewModel) pattern:

- **Models**: Data structures representing dashboard entities
- **Views**: WPF XAML UI components (now with TabControl)
- **ViewModels**: Business logic and data binding
- **Services**: Data retrieval, API integration, and AI momentum analysis
- **Converters**: Value conversion for data binding

## Version 1.7.0 Major Changes

### Tabbed UI Architecture
- **5 Focused Tabs**: Overview, Enrollment, Workloads, Applications, Executive
- **Momentum-Driven Design**: Each tab focuses on driving specific actions
- **Horizontal Button Layout**: 6 header buttons (Graph, Diagnostics, AI, Logs, Guide, Refresh) laid out horizontally to save vertical space

### New AI Momentum Services
1. **EnrollmentMomentumService** - GPT-4 velocity analysis and batch recommendations
2. **WorkloadMomentumService** - GPT-4 next workload prioritization (placeholder)
3. **ExecutiveSummaryService** - GPT-4 health score and status (placeholder)

### Testing Configurations
- **Admin Service URL**: Hardcoded to `https://localhost/AdminService` for easier testing
- **Azure OpenAI**: Hardcoded credentials in AzureOpenAIService.cs (lines 27-47)
- **Purpose**: Reduces friction during development - only need to connect to Graph

## Project Structure

```
CloudJourneyAddin/
├── Models/
│   └── DashboardModels.cs          # Data models for all dashboard sections
├── Services/
│   ├── TelemetryService.cs         # Main service with placeholder data
│   └── IntegrationServices.cs      # Stub services for future API integration
├── ViewModels/
│   ├── ViewModelBase.cs            # Base class with INotifyPropertyChanged
│   └── DashboardViewModel.cs       # Main dashboard logic and commands
├── Views/
│   ├── DashboardWindow.xaml        # Main dashboard UI
│   └── DashboardWindow.xaml.cs     # Code-behind
├── Converters/
│   └── ValueConverters.cs          # Data binding converters
├── App.xaml                         # Application resources
├── App.xaml.cs                      # Application entry point
├── CloudJourneyAddin.xml           # ConfigMgr manifest
└── CloudJourneyAddin.csproj        # Project file
```

## Adding Real Telemetry Integration

### Step 1: Implement Graph API Authentication

Update `Services/IntegrationServices.cs`:

```csharp
using Microsoft.Graph;
using Azure.Identity;

public class IntuneService
{
    private GraphServiceClient _graphClient;

    public IntuneService()
    {
        var credential = new ClientSecretCredential(
            tenantId: "YOUR_TENANT_ID",
            clientId: "YOUR_CLIENT_ID",
            clientSecret: "YOUR_CLIENT_SECRET"
        );

        _graphClient = new GraphServiceClient(credential);
    }

    public async Task<int> GetEnrolledDeviceCountAsync()
    {
        var devices = await _graphClient.DeviceManagement.ManagedDevices
            .Request()
            .Select("id")
            .GetAsync();
        
        return devices.Count;
    }
}
```

### Step 2: Replace Placeholder Data

Modify `Services/TelemetryService.cs` to call real services:

```csharp
public class TelemetryService
{
    private readonly IntuneService _intuneService;
    private readonly ConfigMgrService _configMgrService;

    public TelemetryService(IntuneService intuneService, ConfigMgrService configMgrService)
    {
        _intuneService = intuneService;
        _configMgrService = configMgrService;
    }

    public async Task<DeviceEnrollment> GetDeviceEnrollmentAsync()
    {
        var intuneCount = await _intuneService.GetEnrolledDeviceCountAsync();
        var configMgrCount = await _configMgrService.GetManagedDeviceCountAsync();

        return new DeviceEnrollment
        {
            TotalDevices = intuneCount + configMgrCount,
            IntuneEnrolledDevices = intuneCount,
            ConfigMgrOnlyDevices = configMgrCount
        };
    }
}
```

### Step 3: Query ConfigMgr Data

Use PowerShell or WMI to retrieve ConfigMgr data:

```csharp
public class ConfigMgrService
{
    private readonly string _siteServer;
    private readonly string _siteCode;

    public async Task<int> GetManagedDeviceCountAsync()
    {
        using var ps = PowerShell.Create();
        ps.AddScript($@"
            Import-Module ConfigurationManager
            Set-Location {_siteCode}:
            (Get-CMDevice).Count
        ");

        var results = await Task.Run(() => ps.Invoke());
        return (int)results.FirstOrDefault()?.BaseObject ?? 0;
    }
}
```

## Extending the Dashboard

### Adding a New Section

1. **Create Model** in `Models/DashboardModels.cs`:
```csharp
public class NewSection
{
    public string Data { get; set; }
}
```

2. **Add Property** to `DashboardViewModel.cs`:
```csharp
private NewSection? _newSection;
public NewSection? NewSection
{
    get => _newSection;
    set => SetProperty(ref _newSection, value);
}
```

3. **Fetch Data** in `LoadDataAsync()`:
```csharp
NewSection = await _telemetryService.GetNewSectionAsync();
```

4. **Add UI** in `DashboardWindow.xaml`:
```xml
<Border Style="{StaticResource SectionCard}">
    <StackPanel>
        <TextBlock Text="New Section" Style="{StaticResource SectionTitle}"/>
        <TextBlock Text="{Binding NewSection.Data}"/>
    </StackPanel>
</Border>
```

### Customizing Charts

LiveCharts provides extensive customization options:

```csharp
var series = new LineSeries
{
    Title = "Data Series",
    Values = new ChartValues<double> { 1, 2, 3, 4 },
    Stroke = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
    Fill = new SolidColorBrush(Color.FromArgb(50, 0, 120, 212)),
    PointGeometry = DefaultGeometries.Circle,
    PointGeometrySize = 10,
    LineSmoothness = 0.7
};
```

### Adding Commands

1. **Define Command** in ViewModel:
```csharp
public ICommand MyCommand { get; }

// In constructor:
MyCommand = new RelayCommand(OnMyCommand);

private void OnMyCommand()
{
    // Command logic
}
```

2. **Bind in XAML**:
```xml
<Button Content="Click Me" Command="{Binding MyCommand}"/>
```

## Testing

### Unit Testing

Create test project and mock services:

```csharp
[TestClass]
public class DashboardViewModelTests
{
    [TestMethod]
    public async Task LoadData_PopulatesAllSections()
    {
        var mockService = new Mock<TelemetryService>();
        var viewModel = new DashboardViewModel(mockService.Object);

        await viewModel.LoadDataAsync();

        Assert.IsNotNull(viewModel.MigrationStatus);
        Assert.IsNotNull(viewModel.DeviceEnrollment);
    }
}
```

### Integration Testing

Test with real APIs in a non-production environment.

## Performance Considerations

- **Async Operations**: All data retrieval is async to prevent UI blocking
- **Parallel Loading**: Multiple data sources loaded simultaneously
- **Caching**: Consider implementing caching for frequently accessed data
- **Throttling**: Implement rate limiting for API calls

## Security

- Store credentials securely using Windows Credential Manager
- Use certificate-based authentication for production
- Implement proper error handling to avoid exposing sensitive data
- Follow least privilege principle for API permissions

## Version Management Strategy

### Semantic Versioning

This project follows **Semantic Versioning 2.0.0** (https://semver.org/)

**Format:** `MAJOR.MINOR.PATCH` (e.g., 1.4.0)

#### Version Number Rules

**MAJOR version** (X.0.0) - Increment when:
- Breaking changes to APIs or data structures
- Incompatible changes requiring user intervention
- Complete redesigns or architectural changes
- Example: 1.x.x → 2.0.0

**MINOR version** (x.Y.0) - Increment when:
- New features added in backwards-compatible manner
- Significant enhancements to existing functionality
- New sections or capabilities added to dashboard
- Example: 1.3.x → 1.4.0

**PATCH version** (x.x.Z) - Increment when:
- Bug fixes and corrections
- Performance improvements without new features
- Documentation updates
- Security patches
- Example: 1.4.0 → 1.4.1

#### Version History Examples

- **v1.4.0** - NEW FEATURE: Enrollment blocker detection (MINOR bump)
- **v1.3.10** - BUG FIX: OData query syntax correction (PATCH bump)
- **v1.3.9** - NEW FEATURE: File logging system (MINOR bump)
- **v1.3.8** - CHANGE: Trust restoration, removed mock data (MINOR bump)
- **v1.2.0** - NEW FEATURE: AI recommendations (MINOR bump)

#### When to Bump Versions

**Before Creating Update Package:**
1. Determine change type (MAJOR/MINOR/PATCH)
2. Increment appropriate version number
3. Update version in ALL these locations:
   - `Views/DashboardWindow.xaml` - Window title
   - `ViewModels/DashboardViewModel.cs` - Version log message
   - `README.md` - Version header and "What's New" section
   - `CHANGELOG.md` - New entry at top
4. Create update package with new version: `CloudJourneyAddin-vX.Y.Z-win-x64.zip`

#### Version Documentation Requirements

For each new version, update:
1. **README.md** - Add "What's New in Version X.Y.Z" section at top
2. **CHANGELOG.md** - Document all changes with dates
3. **Version string in code** - Update logging messages
4. **Window title** - Update XAML title attribute

#### Version Naming in Update Packages

**Package Format:** `CloudJourneyAddin-v[VERSION]-win-x64.zip`

Examples:
- `CloudJourneyAddin-v1.4.0-win-x64.zip`
- `CloudJourneyAddin-v1.3.10-win-x64.zip`
- `CloudJourneyAddin-v2.0.0-win-x64.zip`

---

## Update Package Process

### Creating Update Packages for Deployment

**IMPORTANT: This is the established update process. Do NOT change this workflow.**

#### Update Package Structure
Updates are distributed as versioned ZIP files containing:
- `CloudJourneyAddin.exe` (main executable)
- `CloudJourneyAddin.dll` (application library)
- `CloudJourneyAddin.xml` (ConfigMgr manifest)
- `Update-CloudJourneyAddin.ps1` (update script)
- Helper scripts (`Verify-CloudJourneyAddin.ps1`, `Check-ConsoleLog.ps1`, etc.)

#### Creating a New Update Package

1. **Build the Release Version:**
   ```powershell
   dotnet publish -c Release --self-contained true -r win-x64
   ```

2. **Create Versioned Update Package:**
   ```powershell
   # Update version number
   $version = "1.3.10"
   $publishPath = "bin\Release\net8.0-windows\win-x64\publish"
   $updateFolder = "UpdatePackage"
   $zipName = "CloudJourneyAddin-v$version-win-x64.zip"
   
   # Clean UpdatePackage folder (keep .ps1 scripts)
   if (Test-Path $updateFolder) {
       Get-ChildItem $updateFolder -Filter "CloudJourneyAddin.*" -Exclude "*.ps1" | Remove-Item -Force
   }
   
   # Copy essential files
   Copy-Item "$publishPath\CloudJourneyAddin.exe" -Destination $updateFolder -Force
   Copy-Item "$publishPath\CloudJourneyAddin.dll" -Destination $updateFolder -Force
   Copy-Item "CloudJourneyAddin.xml" -Destination $updateFolder -Force
   
   # Create ZIP
   Compress-Archive -Path "$updateFolder\*" -DestinationPath $zipName -Force
   ```

3. **Verify Package Contents:**
   ```powershell
   # Check ZIP contains required files
   Expand-Archive $zipName -DestinationPath "temp_verify" -Force
   Get-ChildItem "temp_verify" | Select-Object Name, Length
   Remove-Item "temp_verify" -Recurse -Force
   ```

#### Deploying Updates to Target PC

**User Instructions (include with every update ZIP):**

1. Copy `CloudJourneyAddin-v[VERSION]-win-x64.zip` to target PC
2. Extract the ZIP file to a folder
3. Right-click `Update-CloudJourneyAddin.ps1`
4. Select **"Run with PowerShell"**
5. Follow the prompts:
   - Confirms existing installation location
   - Creates automatic backup
   - Closes running application (if open)
   - Updates files in-place
   - Verifies update success

#### Update Script Behavior

The `Update-CloudJourneyAddin.ps1` script:
- ✅ Auto-detects existing installation in ConfigMgr Console paths
- ✅ Creates timestamped backup before updating
- ✅ Handles running processes gracefully
- ✅ Verifies file integrity after update
- ✅ Provides rollback instructions if needed

#### Version Naming Convention

**Pattern:** `CloudJourneyAddin-v[MAJOR].[MINOR].[PATCH]-win-x64.zip`

Examples:
- `CloudJourneyAddin-v1.3.10-win-x64.zip` - Enrollment blocker detection
- `CloudJourneyAddin-v1.3.9-win-x64.zip` - File logging system
- `CloudJourneyAddin-v1.3.8-win-x64.zip` - Trust restoration

#### Update Package Checklist

Before releasing an update package, verify:
- [ ] Version number incremented in file name
- [ ] Build succeeded with no errors
- [ ] All three core files included (`.exe`, `.dll`, `.xml`)
- [ ] Update script included in ZIP
- [ ] ZIP file size is reasonable (~200-300 KB)
- [ ] README.md updated with new version section
- [ ] CHANGELOG.md updated with changes
- [ ] Tested update script on clean installation

#### Rollback Procedure

If update fails, rollback is automatic:
1. Update script creates backup: `backup_YYYYMMDD_HHMMSS\`
2. Contains previous `.exe` and `.dll` files
3. Manual rollback: Copy backup files back to installation folder
4. Restart ConfigMgr Console

#### Storage Location

Update packages are stored in repository root:
```
c:\Users\dannygu\Downloads\GitHub Copilot\cmaddin\
├── CloudJourneyAddin-v1.3.10-win-x64.zip  ← Latest
├── CloudJourneyAddin-v1.3.9-win-x64.zip
├── CloudJourneyAddin-v1.3.8-win-x64.zip
└── ...older versions...
```

**Retention Policy:** Keep last 5 versions for rollback capability.

---

## Future Enhancements

- Export dashboard data to PDF/Excel
- Configurable refresh intervals
- Custom alert thresholds
- Historical trend analysis
- Multi-tenant support
- Dark theme support
