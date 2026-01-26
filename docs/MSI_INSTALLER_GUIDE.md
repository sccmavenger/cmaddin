# MSI Installer Implementation Guide

## Overview

This document provides complete instructions for building and deploying the **Zero Trust Migration Journey Dashboard** MSI installer, created to replace the PowerShell-based installation method with an enterprise-ready Windows Installer package.

---

## What Was Implemented

### ✅ Complete Product Rebranding
- **Old Name:** Cloud Journey Progress Dashboard / CloudJourneyAddin
- **New Name:** Zero Trust Migration Journey Dashboard / ZeroTrustMigrationAddin
- **Files Updated:** 120+ files across code, documentation, scripts, and manifests

### ✅ ConfigMgr Console Ribbon Button
- **Enhancement:** Added `DefaultHomeTab` to XML manifest for persistent ribbon button
- **Visibility:** Button now appears in both:
  - ConfigMgr Console Home ribbon (always visible)
  - Right-click context menus (as before)
- **Display Name:** "Zero Trust Migration Journey"
- **Mnemonic:** "Zero Trust"

### ✅ WiX Toolset MSI Installer Project
- **Technology:** WiX Toolset v4
- **Package Size:** ~95MB (compressed MSI)
- **Installed Size:** 233MB (489 files)
- **Features:**
  - Core Application (required)
  - Desktop Shortcuts (optional, default enabled)
  - Start Menu Shortcuts (optional, default enabled)

### ✅ Custom Installation Actions
1. **ConfigMgr Path Detection** - Registry search with fallback
2. **.NET 8.0 Runtime Check** - Validates prerequisite
3. **XML Manifest Path Update** - Updates hardcoded EXE path

### ✅ Bootstrapper Bundle
- **Purpose:** Download and install .NET 8.0 Desktop Runtime if missing
- **Download URL:** Microsoft official CDN (~55MB)
- **User Experience:** Silent installation with progress bar

### ✅ Build Automation Integration
- **Script:** `Build-And-Distribute.ps1 -BuildMsi`
- **Process:** Automatically builds MSI after compiling application
- **Distribution:** Copies MSI to Dropbox folder and optionally publishes to GitHub

---

## Project Structure

```
cmaddin/
├── installer/                              # MSI project folder (NEW)
│   ├── Product.wxs                         # Main MSI definition
│   ├── Bundle.wxs                          # Bootstrapper with .NET prerequisite
│   ├── ApplicationFiles.wxs                # Auto-generated (489 files)
│   ├── Build-Installer.ps1                 # MSI build script
│   ├── README.md                           # Installation documentation
│   ├── CustomActions/                      # C# custom action library
│   │   ├── CustomActions.csproj
│   │   └── CustomActions.cs                # .NET check + XML updater
│   └── [Assets: License.rtf, Banner.bmp, Dialog.bmp, Logo.png]
│
├── ZeroTrustMigrationAddin.csproj          # RENAMED from CloudJourneyAddin.csproj
├── ZeroTrustMigrationAddin.xml             # RENAMED ConfigMgr extension manifest
├── Install-ZeroTrustMigrationAddin.ps1     # RENAMED installation script
├── Update-ZeroTrustMigrationAddin.ps1      # RENAMED update script
├── Uninstall-ZeroTrustMigrationAddin.ps1   # RENAMED uninstall script
├── Build-And-Distribute.ps1                # UPDATED with -BuildMsi parameter
└── [All C#, XAML, and documentation files updated with new branding]
```

---

## Prerequisites for Building MSI

### 1. WiX Toolset v4 or Later

```powershell
# Install globally via .NET CLI
dotnet tool install --global wix

# Verify installation
wix --version
```

**Alternative:** Download installer from https://wixtoolset.org/

### 2. .NET 8.0 SDK

```powershell
# Check if installed
dotnet --version

# Should be 8.0.x or later
```

### 3. Visual Studio 2022 (Optional)

For GUI editing of WiX files, install:
- WiX Toolset Visual Studio Extension
- Enables IntelliSense and visual designers

---

## Building the MSI Installer

### Quick Start (Recommended)

```powershell
# Navigate to project root
cd "c:\Users\dannygu\Downloads\GitHub Copilot\cmaddin"

# Build application and create MSI in one command
.\Build-And-Distribute.ps1 -BuildMsi

# Output: builds\ZeroTrustMigrationAddin.msi
```

### Advanced Build Options

```powershell
# Build MSI only (application already built)
cd installer
.\Build-Installer.ps1

# Build MSI + Bootstrapper with .NET downloader
.\Build-Installer.ps1 -IncludeBundle

# Build application first, then MSI
.\Build-Installer.ps1 -BuildApp

# Skip regenerating ApplicationFiles.wxs (faster)
.\Build-Installer.ps1 -SkipHeat
```

### Manual Build Process

```powershell
# Step 1: Build application
dotnet publish -c Release -r win-x64 --self-contained true

# Step 2: Generate file list with Heat.exe
cd installer
heat dir "..\bin\Release\net8.0-windows\win-x64\publish" `
    -cg ApplicationFiles `
    -dr INSTALLFOLDER `
    -gg -sfrag -srd `
    -var var.PublishDir `
    -out ApplicationFiles.wxs

# Step 3: Build custom actions
cd CustomActions
dotnet build -c Release -p:Platform=x64
cd ..

# Step 4: Build MSI
wix build Product.wxs ApplicationFiles.wxs `
    -ext WixToolset.UI.wixext `
    -d PublishDir="..\bin\Release\net8.0-windows\win-x64\publish" `
    -out ..\builds\ZeroTrustMigrationAddin.msi

# Step 5: Build bootstrapper (optional)
wix build Bundle.wxs `
    -ext WixToolset.Bal.wixext `
    -ext WixToolset.Util.wixext `
    -out ..\builds\ZeroTrustMigrationAddin-Setup.exe
```

---

## Installation Methods

### Method 1: MSI Direct Install (IT Admins)

**Prerequisites:** .NET 8.0 Desktop Runtime must be pre-installed

```cmd
msiexec /i ZeroTrustMigrationAddin.msi /qn
```

**Silent install with log:**
```cmd
msiexec /i ZeroTrustMigrationAddin.msi /qn /l*v install.log
```

**Interactive install:**
```cmd
msiexec /i ZeroTrustMigrationAddin.msi
```

### Method 2: Bootstrapper (End Users) - RECOMMENDED

```cmd
ZeroTrustMigrationAddin-Setup.exe
```

- Downloads and installs .NET 8.0 automatically if missing
- Runs MSI installation
- Single EXE, no manual steps

### Method 3: Enterprise Deployment (SCCM/Intune)

**SCCM Application:**
```
Install Command: msiexec /i ZeroTrustMigrationAddin.msi /qn
Uninstall Command: msiexec /x {PRODUCT-CODE} /qn
Detection Method: File - [ConfigMgrPath]\bin\ZeroTrustMigrationAddin\ZeroTrustMigrationAddin.exe
```

**Intune Win32 App:**
```
Install: ZeroTrustMigrationAddin.msi /quiet
Uninstall: msiexec /x {PRODUCT-CODE} /quiet
Requirement: .NET Desktop Runtime 8.0 (dependency)
Detection: File path exists
```

---

## Testing Checklist

### Pre-Release Testing

- [ ] **Clean VM Test**
  - Windows 10/11 VM without .NET 8.0
  - ConfigMgr Console installed on C: drive
  - Run bootstrapper, verify .NET installs automatically
  - Verify ribbon button appears in ConfigMgr Console

- [ ] **Non-Standard Path Test**
  - ConfigMgr Console on D:, E:, or F: drive
  - Verify installer detects path correctly
  - Verify XML manifest updated with correct path

- [ ] **Upgrade Test**
  - Install old CloudJourneyAddin via PowerShell
  - Run new MSI installer
  - Verify old files removed, new files installed
  - Verify button name changed in ConfigMgr Console

- [ ] **Feature Selection Test**
  - Uncheck "Desktop Shortcut" during install
  - Verify shortcut not created
  - Verify application still works

- [ ] **Uninstall Test**
  - Uninstall via Add/Remove Programs
  - Verify all files removed
  - Verify XML manifest removed
  - Verify logs preserved in %LOCALAPPDATA%

- [ ] **Repair Test**
  - Delete ZeroTrustMigrationAddin.exe manually
  - Run "Repair" from Add/Remove Programs
  - Verify file restored

### Post-Installation Validation

```powershell
# Verify files installed
Test-Path "$env:ProgramFiles(x86)\Microsoft Configuration Manager\AdminConsole\bin\ZeroTrustMigrationAddin\ZeroTrustMigrationAddin.exe"

# Verify XML manifest
Test-Path "$env:ProgramFiles(x86)\Microsoft Configuration Manager\AdminConsole\XmlStorage\Extensions\Actions\ZeroTrustMigrationAddin.xml"

# Verify shortcut
Test-Path "$env:USERPROFILE\Desktop\Zero Trust Migration Journey.lnk"

# Check Add/Remove Programs entry
Get-WmiObject -Class Win32_Product | Where-Object { $_.Name -like "*Zero Trust*" }

# Launch application
& "$env:ProgramFiles(x86)\Microsoft Configuration Manager\AdminConsole\bin\ZeroTrustMigrationAddin\ZeroTrustMigrationAddin.exe"
```

---

## Troubleshooting

### Issue: "ConfigMgr Console not found"

**Cause:** Installer can't detect ConfigMgr Console path in registry

**Solutions:**
1. Verify registry key exists:
   ```powershell
   Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\ConfigMgr10\Setup" | Select-Object "UI Installation Directory"
   ```

2. Manual override:
   ```cmd
   msiexec /i ZeroTrustMigrationAddin.msi CONFIGMGR_PATH="C:\Program Files (x86)\Microsoft Configuration Manager\AdminConsole"
   ```

### Issue: Ribbon button doesn't appear

**Cause:** ConfigMgr Console hasn't restarted, or XML path is incorrect

**Solutions:**
1. **Close ALL ConfigMgr Console windows** and reopen
2. Verify XML exists:
   ```powershell
   Get-Content "$env:ProgramFiles(x86)\Microsoft Configuration Manager\AdminConsole\XmlStorage\Extensions\Actions\ZeroTrustMigrationAddin.xml"
   ```
3. Check `<FilePath>` in XML points to actual EXE location

### Issue: .NET runtime error on launch

**Cause:** .NET 8.0 Desktop Runtime not installed

**Solution:**
```powershell
# Check installed runtimes
dotnet --list-runtimes

# Should see: Microsoft.WindowsDesktop.App 8.x

# Download if missing
Start-Process "https://dotnet.microsoft.com/download/dotnet/8.0"
```

### Issue: MSI build fails with "Heat.exe not found"

**Cause:** WiX not in PATH

**Solution:**
```powershell
# Reinstall WiX globally
dotnet tool uninstall --global wix
dotnet tool install --global wix

# Verify PATH
where.exe wix
```

### Issue: Custom action fails during installation

**Cause:** CustomActions.CA.dll not found or incompatible

**Solution:**
```powershell
# Rebuild custom actions
cd installer\CustomActions
dotnet clean
dotnet build -c Release -p:Platform=x64

# Copy DLL to installer root
Copy-Item "bin\Release\net8.0\CustomActions.CA.dll" "..\CustomActions.CA.dll"
```

---

## Distribution Recommendations

### For End Users
✅ **Distribute:** `ZeroTrustMigrationAddin-Setup.exe` (bootstrapper)
- Self-contained, downloads .NET automatically
- Best user experience

### For IT Admins (Manual Install)
✅ **Distribute:** `ZeroTrustMigrationAddin.msi`
- Requires .NET 8.0 pre-installed
- More control over installation

### For Enterprise (SCCM/Intune)
✅ **Distribute:** `ZeroTrustMigrationAddin.msi` as managed app
- Deploy .NET 8.0 Desktop Runtime as dependency first
- Use silent installation flags
- Set detection rules

### For GitHub Releases
✅ **Publish Both:**
- `ZeroTrustMigrationAddin-v3.16.9-COMPLETE.zip` (traditional)
- `ZeroTrustMigrationAddin-v3.16.9.msi` (enterprise)
- `ZeroTrustMigrationAddin-v3.16.9-Setup.exe` (bootstrapper)

---

## Next Steps

1. **Test on Clean VM** - Validate installation works end-to-end
2. **Create Assets** - Generate License.rtf, Banner.bmp, Dialog.bmp, Logo.png
3. **Build First MSI** - Run `.\Build-And-Distribute.ps1 -BuildMsi`
4. **Test Upgrade** - Install old version, then new MSI
5. **Publish to GitHub** - Release both ZIP and MSI

---

## Maintenance

### Updating Version

Version is automatically handled by `Build-And-Distribute.ps1`:

```powershell
# Patch release (3.16.9 → 3.16.10)
.\Build-And-Distribute.ps1 -BuildMsi

# Minor release (3.16.9 → 3.17.0)
.\Build-And-Distribute.ps1 -BuildMsi -BumpVersion Minor

# Major release (3.16.9 → 4.0.0)
.\Build-And-Distribute.ps1 -BuildMsi -BumpVersion Major -Version "4.0.0"
```

Version is synchronized across:
- ZeroTrustMigrationAddin.csproj
- Product.wxs
- Bundle.wxs
- README.md
- All documentation

### Adding New Files

If you add files to the application:

```powershell
# Rebuild application
dotnet publish -c Release

# Regenerate file list
cd installer
heat dir "..\bin\Release\net8.0-windows\win-x64\publish" -cg ApplicationFiles -dr INSTALLFOLDER -gg -sfrag -srd -var var.PublishDir -out ApplicationFiles.wxs

# Rebuild MSI
.\Build-Installer.ps1
```

---

## Support

- **GitHub Issues:** https://github.com/sccmavenger/cmaddin/issues
- **Wiki:** https://github.com/sccmavenger/cmaddin/wiki
- **Installer README:** `installer\README.md`
- **Build Guide:** `BUILD_SCRIPT_GUIDE.md`

---

**Implementation Date:** January 14, 2026  
**WiX Version:** 4.0 or later  
**Product Version:** 3.16.9  
**Status:** ✅ Ready for Testing
