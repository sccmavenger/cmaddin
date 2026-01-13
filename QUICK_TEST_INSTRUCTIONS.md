# Quick Test Instructions - Auto-Update v3.14.31

## 🚀 Quick Setup (5 minutes)

### 1. Create Two GitHub Releases

```powershell
# Navigate to project
cd "C:\Users\dannygu\Downloads\GitHub Copilot\cmaddin"

# Release 1: Base version v3.14.31 (already built)
gh release create v3.14.31 `
  CloudJourneyAddin-v3.14.31-COMPLETE.zip `
  manifest.json `
  --title "v3.14.31 - Auto-Update Test Base"

# Release 2: Build and upload v3.14.32
.\Build-And-Distribute.ps1
gh release create v3.14.32 `
  CloudJourneyAddin-v3.14.32-COMPLETE.zip `
  manifest.json `
  --title "v3.14.32 - Auto-Update Test Target"
```

### 2. Install Base Version on Test Machine

```powershell
# Download from GitHub or copy from Dropbox
# Extract ZIP
Expand-Archive "CloudJourneyAddin-v3.14.31-COMPLETE.zip" -DestinationPath "C:\Temp\CloudJourney"

# Run installer
cd "C:\Temp\CloudJourney"
.\Update-CloudJourneyAddin.ps1

# Verify version
(Get-Item "$env:LOCALAPPDATA\CloudJourneyAddin\CloudJourneyAddin.exe").VersionInfo.FileVersion
# Should show: 3.14.31.0
```

### 3. Test Auto-Update

```powershell
# Launch app
& "$env:LOCALAPPDATA\CloudJourneyAddin\CloudJourneyAddin.exe"

# Watch for:
# ✅ Progress window appears automatically (within 5 seconds)
# ✅ Shows "Downloading... X%" with progress bar
# ✅ App closes and restarts automatically
# ✅ Version updates to 3.14.32.0
```

### 4. Verify Success

```powershell
# Check updated version
(Get-Item "$env:LOCALAPPDATA\CloudJourneyAddin\CloudJourneyAddin.exe").VersionInfo.FileVersion
# Should show: 3.14.32.0

# Verify no re-update loop
# Close and relaunch app - should NOT update again
```

---

## ✅ Success Criteria

- [x] Progress window appears automatically (no user clicks)
- [x] Update downloads with progress percentage
- [x] App restarts automatically
- [x] Version updates from 3.14.31 → 3.14.32
- [x] Delta download (~15 MB, not 87 MB)
- [x] No re-update loop on next launch

---

## 🔍 Quick Troubleshooting

**No progress window?**
```powershell
# Check logs
Get-Content "$env:LOCALAPPDATA\CloudJourneyAddin\Logs\*.log" -Tail 50 | Select-String "update|GitHub"
```

**App doesn't restart?**
```powershell
# Check PowerShell execution policy
Get-ExecutionPolicy
# Should be: RemoteSigned or Unrestricted
```

**Update loops forever?**
```powershell
# Check local manifest version
Get-Content "$env:LOCALAPPDATA\CloudJourneyAddin\manifest.json" | ConvertFrom-Json | Select Version
```

---

## 📋 Full Testing Guide

For complete testing instructions, see [AUTO_UPDATE_TESTING_GUIDE.md](AUTO_UPDATE_TESTING_GUIDE.md)

---

## 📦 Build Details

**Version:** 3.14.31
**Package Size:** 87.89 MB
**Files:** 286
**Location:** `C:\Users\dannygu\Dropbox\CloudJourneyAddin-v3.14.31-COMPLETE.zip`
**Manifest:** Includes SHA256 hashes for 278 files
