# Auto-Update Quick Start Guide

## Testing the Auto-Update System

### Step 1: Initial Setup (First Build)

```powershell
# Navigate to project directory
cd "C:\Users\dannygu\Downloads\GitHub Copilot\cmaddin"

# Build with manifest generation (creates v3.14.26 or auto-increments)
.\Build-And-Distribute.ps1

# Output files:
# - ZeroTrustMigrationAddin-v3.14.26-COMPLETE.zip (in Dropbox)
# - manifest.json (in project root)
```

### Step 2: Create GitHub Release

#### Option A: Using GitHub CLI (Recommended)

```powershell
# Upload to GitHub Releases
gh release create v3.14.26 `
  "C:\Users\dannygu\Dropbox\ZeroTrustMigrationAddin-v3.14.26-COMPLETE.zip" `
  "manifest.json" `
  --title "Version 3.14.26 - Auto-Update Test" `
  --notes "Test release for auto-update system. Includes delta update support."
```

#### Option B: Using GitHub Web Interface

1. Go to https://github.com/sccmavenger/cmaddin/releases/new
2. Tag version: `v3.14.26`
3. Release title: `Version 3.14.26 - Auto-Update Test`
4. Description: `Test release for auto-update system`
5. Attach files:
   - `ZeroTrustMigrationAddin-v3.14.26-COMPLETE.zip`
   - `manifest.json`
6. Click "Publish release"

### Step 3: Test First Install

```powershell
# Install from the release you just created
# Extract ZIP to test location
$testPath = "C:\TestInstall\ZeroTrustMigrationAddin"
Expand-Archive -Path "C:\Users\dannygu\Dropbox\ZeroTrustMigrationAddin-v3.14.26-COMPLETE.zip" `
               -DestinationPath $testPath -Force

# Run the app
& "$testPath\ZeroTrustMigrationAddin.exe"

# Expected: App starts normally (no update dialog - already on latest)
```

### Step 4: Create a New Version

```powershell
# Make a small code change (e.g., add a comment)
# Then build next version
.\Build-And-Distribute.ps1

# This auto-increments to v3.14.27
# Creates:
# - ZeroTrustMigrationAddin-v3.14.27-COMPLETE.zip
# - manifest.json (updated)
```

### Step 5: Create Second GitHub Release

```powershell
# Upload the new version
gh release create v3.14.27 `
  "C:\Users\dannygu\Dropbox\ZeroTrustMigrationAddin-v3.14.27-COMPLETE.zip" `
  "manifest.json" `
  --title "Version 3.14.27 - Update Test" `
  --notes "Testing auto-update from v3.14.26 to v3.14.27"
```

### Step 6: Test Auto-Update

```powershell
# Run the OLD version (3.14.26) from test install
& "$testPath\ZeroTrustMigrationAddin.exe"

# Expected behavior:
# 1. App starts normally
# 2. After 2-3 seconds, update progress window appears
# 3. Window shows:
#    - "Updating to version 3.14.27"
#    - Progress bar with percentage
#    - Status: "Downloading update... X%"
# 4. NO USER INTERACTION REQUIRED - Update happens automatically
# 5. When complete, shows "Applying update..."
# 6. Then shows "Update complete! Restarting..."
# 7. App closes and restarts automatically
# 8. Check version in title bar → Should show v3.14.27
```

## Troubleshooting Test Issues

### Update Window Doesn't Appear

**Check 1:** Is GitHub Release published correctly?

**Check 2:** Are you running the old version?
```powershell
# Verify EXE version
(Get-Item "$testPath\ZeroTrustMigrationAddin.exe").VersionInfo.FileVersion
# Should show: 3.14.26.0an
```

**Check 3:** Check logs for errors
```powershell
# View application log
Get-Content "$env:LOCALAPPDATA\ZeroTrustMigrationAddin\Logs\ZeroTrustMigrationAddin_$(Get-Date -Format 'yyyyMMdd').log" -Tail 50
```

### Check Logs

```powershell
# View application log
Get-Content "$env:LOCALAPPDATA\ZeroTrustMigrationAddin\Logs\ZeroTrustMigrationAddin_$(Get-Date -Format 'yyyyMMdd').log" -Tail 50

# Look for:
# "Checking for updates from GitHub Releases..."
# "Update available: 3.14.26 → 3.14.27"
# "Delta: X files, Y bytes"
```

### Check Update Settings

```powershell
# View settings
Get-Content "$env:LOCALAPPDATA\ZeroTrustMigrationAddin\update-settings.json" | ConvertFrom-Json | Format-List

# Expected:
# GitHubToken        : 
# LastUpdateCheck    : 2026-01-13T15:30:00Z
# AutoCheckForUpdates: True
# SkippedVersions    : {}
```

## Manual Testing Scenarios

### Test 1: Skip Version

1. Start old version (3.14.26)
2. Update dialog appears
3. Click "Skt network
4. Click "Download & Install"
5. **Expected:** Error message about download failure
6. Reconnect network
7. **Expected:** Can retry download

### Test 4: Delta Update Savings

```powershell
# Calculate what gets downloaded
# Run this BEFORE clicking "Download & Install":

# Full ZIP size
$fullSize = (Get-Item "C:\Users\dannygu\Dropbox\ZeroTrustMigrationAddin-v3.14.27-COMPLETE.zip").Length / 1MB
Write-Host "Full ZIP: $([math]::Round($fullSize, 2)) MB"

# Delta size (shown in update dialog)
# Typical: 5-15 MB for patch updates
# Savings: 80-85%
```

## Distribution to Test Customers

### Method 1: GitHub Releases (Public)

**SProgress window appears and starts downloading
3. Disconnect network during download
4. **Expected:** Error message "Download failed. Please check your connection."
5. Reconnect network
6. Restart app
7. **Expected:** Update will retry automatically

### Test 2d `ZeroTrustMigrationAddin-v*.zip`
3. Extract and run `Install-ZeroTrustMigrationAddin.ps1`
4. Future updates will be automatic

##Check logs to see actual delta size

**Initial distribution via email/Dropbox:**
- Send: `ZeroTrustMigrationAddin-v3.14.26-COMPLETE.zip`

**Customers install manually:**
```powershell
# Extract
Expand-Archive -Path "ZeroTrustMigrationAddin-v3.14.26-COMPLETE.zip" -DestinationPath "C:\CloudJourney"

# Run
& "C:\CloudJourney\ZeroTrustMigrationAddin.exe"
```

**Future updates:** Automatic via GitHub Releases

### Method 3: Create Update Shortcut

Create a shortcut for customers to manually check:

```powershell
# Create shortcut on desktop
$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$env:USERPROFILE\Desktop\Check Zero Trust Migration Journey Updates.lnk")
$Shortcut.TargetPath = "powershell.exe"
$Shortcut.Arguments = "-Command `"Remove-Item '$env:LOCALAPPDATA\ZeroTrustMigrationAddin\update-settings.json' -Force; Start-Process 'C:\CloudJourney\ZeroTrustMigrationAddin.exe'`""
$Shortcut.Description = "Force check for Zero Trust Migration Journey updates"
$Shortcut.Save()
```

## Advanced: GitHub Personal Access Token (Optional)

For higher rate limits (5,000 req/hr vs 60):

### Step 1: Create PAT

1. Go to https://github.com/settings/tokens
2. Click "Generate new token (classic)"
3. Name: `ZeroTrustMigrationAddin Updates`
4. Expiration: `90 days`
5. Scopes: ✅ `public_repo` (read-only)
6. Click "Generate token"
7. Copy token: `ghp_xxxxxxxxxxxxxxxxxxxx`

### Step 2: Configure in App

**Option A: Manual (JSON file)**
```powershell
# Edit settings file
$settingsPath = "$env:LOCALAPPDATA\ZeroTrustMigrationAddin\update-settings.json"
$settings = @{
    GitHubToken = "ghp_xxxxxxxxxxxxxxxxxxxx"
    AutoCheckForUpdates = $true
    LastUpdateCheck = $null
    SkippedVersions = @()
}
$settings | ConvertTo-Json | Out-File $settingsPath -Encoding UTF8
```

**Option B: Future UI (not yet implemented)**
- Settings → GitHub Token → Paste token

### Verify Token Works

```powershell
# Check logs after next update check
Get-Content "$env:LOCALAPPDATA\ZeroTrustMigrationAddin\Logs\ZeroTrustMigrationAddin_$(Get-Date -Format 'yyyyMMdd').log" | Select-String "GitHub API"

# Should show:
# "GitHub API authenticated with Personal Access Token"
# (instead of "using anonymous access")
```

## Success Criteria

✅ Build script generates manifest.json  
✅ GitHub Release contains ZIP + manifest  
✅ Old version detects new version  
✅ Update dialog shows correct delta size  
✅ Download completes successfully  
✅ Files replaced correctly  
✅ App restarts with new version  
✅ Logs show update process  

## Next Steps

1. **Test with 10 customers:**
   - Send initial ZIP to 10 users
   - Have them install manually
   - Release v3.14.28 via GitHub
   - Verify all 10 get update dialog

2. **Monitor feedback:**
   - Check logs from customers
   - Identify any issues
   - Iterate on UI/UX

3. **Plan for scale:**
   - Consider private releases (if needed)
   - Add update analytics (track adoption)
   - Implement rollback if needed later

## Questions?

Check the full guide: `AUTO_UPDATE_GUIDE.md`

For issues, check logs:
- App: `%LocalAppData%\ZeroTrustMigrationAddin\Logs\`
- Update: `%TEMP%\ZeroTrustMigrationAddin-Update.log`
