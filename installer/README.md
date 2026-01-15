# Zero Trust Migration Journey Dashboard - MSI Installer

This directory contains the WiX Toolset source files for building the MSI installer.

## Prerequisites

1. **WiX Toolset v4** or later
   - Download from: https://wixtoolset.org/
   - Install globally: `dotnet tool install --global wix`

2. **.NET 8.0 SDK**
   - Required to build custom actions

3. **Visual Studio 2022** (optional, for GUI editing)
   - WiX Toolset Visual Studio Extension

## Project Structure

```
installer/
├── Product.wxs              # Main MSI product definition
├── Bundle.wxs               # Bootstrapper with .NET prerequisite
├── ApplicationFiles.wxs     # Auto-generated file list (489 files)
├── CustomActions/
│   ├── CustomActions.csproj # Custom action library project
│   └── CustomActions.cs     # .NET runtime check + XML path updater
├── License.rtf              # EULA (required)
├── Banner.bmp               # Installer banner (493x58 pixels)
├── Dialog.bmp               # Installer dialog (493x312 pixels)
├── Logo.png                 # Bootstrapper logo
└── README.md                # This file
```

## Build Instructions

### Step 1: Generate Application Files Component

Before building, generate the `ApplicationFiles.wxs` file containing all 489 application files:

```powershell
# Navigate to installer directory
cd installer

# Run Heat.exe to harvest files from publish folder
heat dir "..\bin\Release\net8.0-windows\win-x64\publish" `
    -cg ApplicationFiles `
    -dr INSTALLFOLDER `
    -gg `
    -sfrag `
    -srd `
    -var var.PublishDir `
    -out ApplicationFiles.wxs

# Alternative: Use the build script (recommended)
.\Build-Installer.ps1
```

### Step 2: Build Custom Actions

```powershell
cd CustomActions
dotnet build -c Release
cd ..
```

### Step 3: Build MSI

```powershell
# Build the MSI
wix build Product.wxs ApplicationFiles.wxs `
    -ext WixToolset.UI.wixext `
    -d PublishDir="..\bin\Release\net8.0-windows\win-x64\publish" `
    -out ..\builds\ZeroTrustMigrationAddin.msi

# Or build everything with the script
.\Build-Installer.ps1 -IncludeBundle
```

### Step 4: Build Bootstrapper (Optional)

```powershell
# Build the bootstrapper bundle (includes .NET runtime downloader)
wix build Bundle.wxs `
    -ext WixToolset.Bal.wixext `
    -ext WixToolset.Util.wixext `
    -out ..\builds\ZeroTrustMigrationAddin-Setup.exe
```

## MSI Features

### Core Features
- ✅ **ConfigMgr Console Integration** - Adds button to ribbon and context menu
- ✅ **Dynamic Path Detection** - Finds ConfigMgr Console automatically
- ✅ **XML Manifest Path Update** - Updates hardcoded path in XML
- ✅ **Major Upgrade Support** - Removes old versions automatically
- ✅ **Add/Remove Programs Integration** - Standard Windows uninstaller

### Optional Features
- ✅ **Desktop Shortcut** - Quick launch from desktop (default: enabled)
- ✅ **Start Menu Shortcuts** - Program group with app + user guide (default: enabled)

### Prerequisites
- ✅ **Windows 10 1809+** - Launch condition check
- ✅ **ConfigMgr Console** - Registry detection with error message
- ✅ **.NET 8.0 Desktop Runtime** - Bootstrapper downloads if missing

## Custom Actions

### 1. CheckDotNetRuntime (Immediate)
- Runs: After LaunchConditions
- Purpose: Verify .NET 8.0 Desktop Runtime installed
- Action: Sets `DOTNET_RUNTIME_INSTALLED` property
- Result: Warning if missing (bootstrapper handles installation)

### 2. UpdateXmlManifestPath (Deferred)
- Runs: After InstallFiles
- Purpose: Update `<FilePath>` in ConfigMgr extension XML
- Input: XML path and EXE path from `CA_SetXmlPath`
- Result: Failure if XML update fails

## Testing

### Test on Clean VM
1. Windows 10/11 VM without .NET 8.0
2. ConfigMgr Console installed on non-standard drive (D:, E:, F:)
3. Run bootstrapper: `ZeroTrustMigrationAddin-Setup.exe`
4. Verify:
   - .NET 8.0 installs automatically
   - ConfigMgr path detected correctly
   - XML manifest updated with correct path
   - Button appears in ConfigMgr Console ribbon + context menu
   - Desktop and Start Menu shortcuts work

### Test Upgrade Scenario
1. Install old PowerShell version (CloudJourneyAddin)
2. Run MSI installer (ZeroTrustMigrationAddin)
3. Verify:
   - Old files removed
   - New files installed to new path
   - User data preserved in %LOCALAPPDATA%
   - ConfigMgr Console shows new button name

### Test Uninstall
1. Uninstall via Add/Remove Programs
2. Verify:
   - Application files removed
   - XML manifest removed
   - Shortcuts removed
   - Logs preserved (optional)
   - ConfigMgr Console button removed

## Integration with Build-And-Distribute.ps1

The main build script has been updated to build MSI automatically:

```powershell
# Build everything (application + MSI)
.\Build-And-Distribute.ps1 -BuildMsi

# Build and publish to GitHub with MSI
.\Build-And-Distribute.ps1 -BuildMsi -PublishToGitHub
```

## Distribution

### MSI Only (Advanced Users)
- File: `ZeroTrustMigrationAddin.msi`
- Size: ~95MB
- Requires: .NET 8.0 Desktop Runtime pre-installed

### Bootstrapper (Recommended)
- File: `ZeroTrustMigrationAddin-Setup.exe`
- Size: ~2MB (downloads .NET if needed)
- Self-contained: Downloads prerequisites automatically

### Enterprise Deployment
- Deploy via SCCM/Intune as Application
- Command Line: `msiexec /i ZeroTrustMigrationAddin.msi /qn`
- Detection Method: File exists `[ConfigMgrPath]\bin\ZeroTrustMigrationAddin\ZeroTrustMigrationAddin.exe`

## Known Issues

1. **ConfigMgr Console Restart Required**
   - After installation, users must close and reopen ConfigMgr Console
   - MSI displays completion dialog with instructions

2. **Large File Count**
   - 489 files to install (233MB)
   - Installation may take 2-3 minutes on slow disks

3. **Non-Standard ConfigMgr Paths**
   - If ConfigMgr installed on network drive, detection may fail
   - Workaround: Install MSI with `CONFIGMGR_PATH="\\server\share\AdminConsole"`

## Troubleshooting

### Installation Fails with "ConfigMgr Console not found"
- Check registry: `HKLM\SOFTWARE\Microsoft\ConfigMgr10\Setup\UI Installation Directory`
- Manual override: `msiexec /i ZeroTrustMigrationAddin.msi CONFIGMGR_PATH="C:\Program Files (x86)\Microsoft Configuration Manager\AdminConsole"`

### Button doesn't appear in ConfigMgr Console
- Verify XML exists: `[ConfigMgrPath]\XmlStorage\Extensions\Actions\ZeroTrustMigrationAddin.xml`
- Check FilePath in XML points to correct EXE location
- Restart ConfigMgr Console completely (close all windows)

### .NET Runtime Error on Launch
- Verify .NET 8.0 Desktop Runtime installed: `dotnet --list-runtimes`
- Download manually: https://dotnet.microsoft.com/download/dotnet/8.0
- Check for conflicts with .NET 9.0 or preview versions

## License

Copyright © 2026 Microsoft Configuration Manager
See LICENSE file for details.

## Support

- GitHub Issues: https://github.com/sccmavenger/cmaddin/issues
- Documentation: https://github.com/sccmavenger/cmaddin/wiki
