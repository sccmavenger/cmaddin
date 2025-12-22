# 🚀 Quick Start Guide - Zero Setup Required!

## For End Users (ConfigMgr Administrators)

### Installation in 3 Steps:

1. **Extract the files** to any folder
2. **Right-click** `Install-CloudJourneyAddin.ps1` → **Run with PowerShell**
3. **Launch ConfigMgr Console** and look for "Cloud Journey Progress" in the ribbon

**That's it!** The installer automatically:
- ✅ Checks and elevates to admin if needed
- ✅ Finds your ConfigMgr Console installation
- ✅ Downloads and installs .NET 8.0 Runtime if missing (~55MB, one-time)
- ✅ Deploys all 489 files with dependencies (233MB)
- ✅ Validates the installation
- ✅ Creates an uninstaller

---

## What Gets Installed Automatically

### Runtime Components (Auto-Downloaded if Missing):
- **.NET 8.0 Desktop Runtime** (windowsdesktop-runtime-8.0.11-win-x64.exe)
  - Downloaded from Microsoft CDN
  - Installed silently without user interaction
  - ~55MB download, installs in ~2 minutes

### Application Files (Self-Contained):
The application includes **ALL dependencies bundled**:
- Core .NET 8.0 runtime libraries (217MB of DLLs)
- WPF framework components
- LiveCharts visualization library
- Microsoft Graph SDK
- All supporting assemblies

**Total Deployment Size:** 233MB (489 files)

### Installation Locations:
```
ConfigMgr Console Extensions:
C:\Program Files (x86)\Microsoft Configuration Manager\AdminConsole\
├── XmlStorage\Extensions\Actions\CloudJourneyAddin.xml  (manifest)
└── bin\CloudJourneyAddin\                               (all app files)
    ├── CloudJourneyAddin.exe                            (main executable)
    ├── CloudJourneyAddin.dll                            (app logic)
    ├── coreclr.dll                                      (.NET runtime)
    ├── wpfgfx_cor3.dll                                  (WPF graphics)
    ├── LiveCharts.Wpf.dll                               (charts)
    └── ... (485 more files)
```

---

## System Requirements

### Minimum:
- Windows 10 (version 1809+) or Windows Server 2019+
- ConfigMgr Console 2103 or later
- Administrator privileges (script will request elevation)
- Internet connection (only for .NET Runtime download if needed)

### Disk Space:
- Application: 233MB
- .NET Runtime: 55MB (if not already installed)
- **Total:** ~300MB

### No Pre-Installation Required:
- ❌ No need to install .NET manually
- ❌ No need to install Visual Studio or SDK
- ❌ No need to configure paths or environment variables
- ❌ No need to register components
- ❌ No need to modify registry

---

## Offline Installation

If you need to install on a machine without internet access:

### Step 1: Download Prerequisites (on a connected machine):
```powershell
# Download .NET Runtime installer
$url = "https://download.visualstudio.microsoft.com/download/pr/6224f00f-08da-4e7f-85b1-00d42c2bb3d3/b775de636b91e023574a0bbc291f705a/windowsdesktop-runtime-8.0.11-win-x64.exe"
Invoke-WebRequest -Uri $url -OutFile "windowsdesktop-runtime-8.0.11-win-x64.exe"
```

### Step 2: On the offline machine:
1. Copy the application folder and the .NET Runtime installer
2. Install .NET Runtime manually: `windowsdesktop-runtime-8.0.11-win-x64.exe /install /quiet`
3. Run the installer with `-SkipBuild` flag (if using pre-built binaries)

---

## Troubleshooting

### "Script is not digitally signed"
Run PowerShell as Administrator and execute:
```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\Install-CloudJourneyAddin.ps1
```

### "ConfigMgr Console not found"
Specify the path manually:
```powershell
.\Install-CloudJourneyAddin.ps1 -ConfigMgrPath "C:\Your\Path\To\AdminConsole"
```

### ".NET Runtime download fails"
1. Download manually from: https://dotnet.microsoft.com/download/dotnet/8.0
2. Install "Desktop Runtime (x64)"
3. Re-run installer

### "Add-in doesn't appear"
1. Close ConfigMgr Console completely
2. Verify files exist:
   - XML manifest in `XmlStorage\Extensions\Actions\`
   - EXE in `bin\CloudJourneyAddin\`
3. Restart ConfigMgr Console

---

## Uninstallation

After installation, an uninstaller is created automatically:

```powershell
.\Uninstall-CloudJourneyAddin.ps1
```

This removes:
- All application files
- XML manifest
- **Does NOT remove** .NET Runtime (shared with other apps)

---

## For Developers

### Building from Source:

```powershell
# Quick build
.\Build-Standalone.ps1

# Create distribution package
.\Build-Standalone.ps1 -CreateZip
```

### Manual Installation:

```powershell
.\Install-CloudJourneyAddin.ps1
```

### Skip build (use existing binaries):

```powershell
.\Install-CloudJourneyAddin.ps1 -SkipBuild
```

---

## What Makes This "Zero Setup"?

### Traditional Approach (Manual):
1. Install .NET Runtime manually
2. Install Visual Studio or SDK
3. Clone repository
4. Build solution
5. Copy files manually
6. Configure XML manifest
7. Set permissions
8. Restart console
9. Troubleshoot issues

**Time:** 30-60 minutes with technical knowledge

### Our Approach (Automated):
1. Run one PowerShell script

**Time:** 2-5 minutes with zero technical knowledge

### The Difference:
- **Auto-detection** of ConfigMgr Console path
- **Auto-download** of .NET Runtime if missing
- **Auto-build** with all dependencies bundled
- **Auto-deployment** to correct locations
- **Auto-validation** of installation
- **Auto-creation** of uninstaller
- **Self-contained** packaging (263 DLLs included)

---

## Security Notes

- Installer requires Administrator privileges (will prompt for elevation)
- .NET Runtime downloaded from official Microsoft CDN
- No data collected or transmitted by the add-in
- All operations logged for audit purposes
- Installer script is plain-text PowerShell (inspect before running)

---

## Support

For issues during installation:
1. Check the console output for error messages
2. Verify administrator privileges
3. Confirm internet connectivity (for .NET download)
4. Review installation locations for file presence
5. Check ConfigMgr Console version (2103+)

The installer provides detailed progress and error messages at each step.

---

## License

Microsoft Internal Use - See LICENSE file for details
