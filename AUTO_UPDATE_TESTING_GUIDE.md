# Auto-Update Testing Guide

## Overview
This guide provides step-by-step instructions for testing the automatic update mechanism on v3.14.31.

## Prerequisites
- GitHub repository with Releases feature enabled
- GitHub CLI (`gh`) or manual release creation capability
- Two clean test machines OR ability to uninstall/reinstall on one machine
- Dropbox access to retrieve build packages

---

## Test Setup: Create Two Releases

### Step 1: Create Base Release (v3.14.31)

**Build Location:**
- Local: `C:\Users\dannygu\Downloads\GitHub Copilot\cmaddin\ZeroTrustMigrationAddin-v3.14.31-COMPLETE.zip`
- Dropbox: `C:\Users\dannygu\Dropbox\ZeroTrustMigrationAddin-v3.14.31-COMPLETE.zip`
- Manifest: `C:\Users\dannygu\Downloads\GitHub Copilot\cmaddin\manifest.json`

**Upload to GitHub:**
```powershell
# Navigate to project root
cd "C:\Users\dannygu\Downloads\GitHub Copilot\cmaddin"

# Create release v3.14.31 (base version for testing)
gh release create v3.14.31 `
  ZeroTrustMigrationAddin-v3.14.31-COMPLETE.zip `
  manifest.json `
  --title "Zero Trust Migration Journey Add-in v3.14.31 - Auto-Update Test Base" `
  --notes "Base version for testing automatic update mechanism. This version includes the auto-update infrastructure and will automatically check for updates on every launch."
```

**Alternative (Manual Upload):**
1. Go to GitHub repository → Releases → Create new release
2. Tag: `v3.14.31`
3. Title: `Zero Trust Migration Journey Add-in v3.14.31 - Auto-Update Test Base`
4. Upload both files:
   - `ZeroTrustMigrationAddin-v3.14.31-COMPLETE.zip`
   - `manifest.json`
5. Publish release

---

### Step 2: Create Test Update Release (v3.14.32)

To test the update mechanism, we need a newer version. Make a minor change and rebuild:

**Make Test Change:**
```powershell
# Option A: Update the version log message in DashboardViewModel
# Edit Views/DashboardViewModel.cs line ~60
# Change: _logger.LogInformation("ZeroTrustMigrationAddin version 3.14.31 initialized");
# To: _logger.LogInformation("ZeroTrustMigrationAddin version 3.14.32 initialized - Auto-Update Test");

# Option B: Simply increment version and rebuild (no code changes needed)
```

**Build New Version:**
```powershell
.\Build-And-Distribute.ps1
```

This will create:
- `ZeroTrustMigrationAddin-v3.14.32-COMPLETE.zip` (87.89 MB)
- `manifest.json` (updated with v3.14.32 file hashes)

**Upload to GitHub:**
```powershell
gh release create v3.14.32 `
  ZeroTrustMigrationAddin-v3.14.32-COMPLETE.zip `
  manifest.json `
  --title "Zero Trust Migration Journey Add-in v3.14.32 - Auto-Update Test Target" `
  --notes "Updated version for testing automatic updates from v3.14.31. This release validates the delta update mechanism and automatic installation process."
```

---

## Test Execution

### Test 1: Fresh Installation (Base Version)

**On Clean Test Machine:**

1. **Download Base Version**
   - Download `ZeroTrustMigrationAddin-v3.14.31-COMPLETE.zip` from GitHub Release
   - OR copy from Dropbox: `C:\Users\dannygu\Dropbox\ZeroTrustMigrationAddin-v3.14.31-COMPLETE.zip`

2. **Extract Package**
   ```powershell
   # Extract to temporary location
   Expand-Archive -Path "ZeroTrustMigrationAddin-v3.14.31-COMPLETE.zip" -DestinationPath "C:\Temp\CloudJourney-v3.14.31"
   ```

3. **Install Base Version**
   ```powershell
   cd "C:\Temp\CloudJourney-v3.14.31"
   .\Update-ZeroTrustMigrationAddin.ps1
   ```

4. **Verify Installation**
   - Check installed location: `%LocalAppData%\ZeroTrustMigrationAddin\`
   - Verify version in file properties: Right-click `ZeroTrustMigrationAddin.exe` → Properties → Details → File version should show `3.14.31.0`
   - Launch ConfigMgr Console and verify add-in ribbon appears

---

### Test 2: Automatic Update on Launch

**Expected Behavior:**
When you launch the app after v3.14.32 is published, the update process should be fully automatic:

1. **App checks for updates on launch** (no user prompt)
2. **Progress window appears automatically** (if update found)
3. **Update downloads in background** (progress bar shows percentage)
4. **App closes and restarts automatically** (using PowerShell updater script)
5. **Updated version launches** (should show v3.14.32)

**Test Steps:**

1. **Launch Application**
   ```powershell
   # Start the installed application
   & "$env:LOCALAPPDATA\ZeroTrustMigrationAddin\ZeroTrustMigrationAddin.exe"
   ```

2. **Observe Update Process**
   - ✅ **PASS:** Progress window appears within 3-5 seconds
   - ✅ **PASS:** Progress bar shows download percentage (0-100%)
   - ✅ **PASS:** Status message updates: "Checking for updates...", "Downloading...", "Applying update..."
   - ✅ **PASS:** Window is borderless and centered on screen
   - ✅ **PASS:** No user interaction required (no buttons to click)
   - ❌ **FAIL:** If no progress window appears, check logs

3. **Monitor Update Application**
   - Application should close automatically after download
   - PowerShell script runs in background (you may briefly see a PowerShell window)
   - Application restarts automatically with new version

4. **Verify Updated Version**
   ```powershell
   # Check file version
   (Get-Item "$env:LOCALAPPDATA\ZeroTrustMigrationAddin\ZeroTrustMigrationAddin.exe").VersionInfo.FileVersion
   # Should output: 3.14.32.0
   ```

5. **Verify Delta Update Efficiency**
   ```powershell
   # Check update logs to confirm delta download (not full package)
   Get-Content "$env:LOCALAPPDATA\ZeroTrustMigrationAddin\Logs\*.log" -Tail 50 | Select-String "delta|download"
   
   # Expected log entries:
   # "Found X files changed out of Y total files"
   # "Downloading delta update: [file list]"
   # "Download size: ~10-20 MB" (not 87 MB)
   ```

---

### Test 3: Verify No Re-Update Loop

After updating to v3.14.32, the app should NOT attempt to update again:

1. **Close and Relaunch App**
   ```powershell
   # Close app if running
   Stop-Process -Name ZeroTrustMigrationAddin -Force -ErrorAction SilentlyContinue
   
   # Relaunch
   & "$env:LOCALAPPDATA\ZeroTrustMigrationAddin\ZeroTrustMigrationAddin.exe"
   ```

2. **Expected Behavior**
   - ✅ **PASS:** App launches normally without update window
   - ✅ **PASS:** No progress window appears
   - ✅ **PASS:** Dashboard loads immediately
   - ❌ **FAIL:** If update window appears again, version comparison logic has an issue

---

## Troubleshooting

### Logs Location
```powershell
# Application logs
Get-ChildItem "$env:LOCALAPPDATA\ZeroTrustMigrationAddin\Logs" | Sort-Object LastWriteTime -Descending | Select-Object -First 5

# Update-specific logs (search for update-related entries)
Get-Content "$env:LOCALAPPDATA\ZeroTrustMigrationAddin\Logs\*.log" -Tail 100 | Select-String "update|GitHub|release|delta|manifest"
```

### Update Settings File
```powershell
# Check update settings (stores last check time)
Get-Content "$env:LOCALAPPDATA\ZeroTrustMigrationAddin\update-settings.json" | ConvertFrom-Json

# Expected content:
# {
#   "LastUpdateCheck": "2026-01-13T10:30:00Z",
#   "SkipVersion": null
# }
```

### Local Manifest File
```powershell
# Check installed version manifest
Get-Content "$env:LOCALAPPDATA\ZeroTrustMigrationAddin\manifest.json" | ConvertFrom-Json | Select-Object Version, ReleaseDate

# Should show:
# Version: 3.14.32
# ReleaseDate: [timestamp of release]
```

### Common Issues

**Issue 1: Progress window doesn't appear**
- Check GitHub release is public and contains `manifest.json`
- Verify internet connectivity
- Check logs for GitHub API errors (rate limiting?)
- Confirm repository name in [Services/GitHubUpdateService.cs](Services/GitHubUpdateService.cs) matches your GitHub repo

**Issue 2: Update downloads but app doesn't restart**
- Check PowerShell execution policy: `Get-ExecutionPolicy`
- Should be `RemoteSigned` or `Unrestricted`
- Check for PowerShell script in temp folder: `Get-ChildItem $env:TEMP | Where-Object { $_.Name -like "CloudJourney_Update_*.ps1" }`

**Issue 3: App updates repeatedly (loop)**
- Check version comparison logic in [Services/GitHubUpdateService.cs](Services/GitHubUpdateService.cs)
- Verify `manifest.json` version string format matches: `"3.14.32"`
- Check local manifest file was updated after applying update

**Issue 4: Delta update downloads full package**
- Verify both releases have `manifest.json` with SHA256 hashes
- Check logs for "manifest comparison" entries
- Confirm SHA256 hashes are different between versions

---

## Test Checklist

### Pre-Test Setup
- [ ] Created GitHub Release v3.14.31 with ZIP + manifest.json
- [ ] Created GitHub Release v3.14.32 with ZIP + manifest.json
- [ ] Verified both releases are public and accessible
- [ ] Have clean test machine or uninstalled previous version

### Test Execution
- [ ] Installed base version (v3.14.31) successfully
- [ ] Verified base version file properties show 3.14.31.0
- [ ] Launched app and observed automatic update check
- [ ] Progress window appeared automatically (no user prompt)
- [ ] Update downloaded with progress percentage
- [ ] App closed and restarted automatically
- [ ] Verified updated version (v3.14.32.0) after restart
- [ ] Confirmed delta download size (~10-20 MB, not 87 MB)
- [ ] Relaunched app to confirm no re-update loop

### Post-Test Validation
- [ ] Logs show successful update process
- [ ] Local manifest.json updated to v3.14.32
- [ ] Update settings file shows correct LastUpdateCheck timestamp
- [ ] No errors in application logs
- [ ] ConfigMgr console add-in functionality still works

---

## Expected Test Results

### Successful Update Flow
```
[T+0s]   User launches ZeroTrustMigrationAddin.exe (v3.14.31)
[T+1s]   App.xaml.cs CheckForUpdatesAsync() executes on startup
[T+2s]   GitHubUpdateService queries GitHub Releases API
[T+3s]   Found newer version: v3.14.32 (current: v3.14.31)
[T+4s]   DeltaUpdateService compares manifests
         -> 278 total files, 8 changed files identified
         -> Delta download size: ~15 MB (vs 87 MB full package)
[T+5s]   UpdateProgressWindow appears automatically (borderless, centered)
[T+5s]   Status: "Downloading update... 0%"
[T+10s]  Status: "Downloading update... 25%"
[T+15s]  Status: "Downloading update... 50%"
[T+20s]  Status: "Downloading update... 75%"
[T+25s]  Status: "Downloading update... 100%"
[T+26s]  Status: "Applying update..."
[T+27s]  UpdateApplier.ScheduleUpdateScript() creates PowerShell script
[T+28s]  Application closes
[T+29s]  PowerShell script runs in background:
         - Waits for ZeroTrustMigrationAddin.exe process to exit
         - Copies 8 updated files to %LocalAppData%\ZeroTrustMigrationAddin
         - Updates local manifest.json to v3.14.32
         - Restarts ZeroTrustMigrationAddin.exe
[T+35s]  Application launches with v3.14.32
[T+36s]  Update check runs again (every launch)
[T+37s]  No update available (current: 3.14.32, latest: 3.14.32)
[T+38s]  Dashboard loads normally
```

### Performance Metrics
- **Full Package Size:** 87.89 MB
- **Delta Update Size:** ~10-20 MB (depends on changes)
- **Bandwidth Savings:** 80-90%
- **Update Check Time:** 2-4 seconds
- **Delta Download Time:** ~20-30 seconds (on 10 Mbps connection)
- **Apply + Restart Time:** ~8-12 seconds
- **Total Update Time:** ~30-45 seconds (vs 2-3 minutes for full download)

---

## Next Steps After Testing

1. **Document Results**
   - Record test outcomes in test log
   - Note any issues or unexpected behaviors
   - Capture screenshots of update progress window

2. **Update Documentation**
   - Update [AUTO_UPDATE_GUIDE.md](AUTO_UPDATE_GUIDE.md) with test findings
   - Add known issues to [CHANGELOG.md](CHANGELOG.md)
   - Update [README.md](README.md) with auto-update feature description

3. **Production Deployment**
   - After successful testing, deploy to 10 test customers
   - Monitor logs for update success rate
   - Gather feedback on user experience (should be transparent!)

4. **Create Release Notes**
   - Document auto-update feature for end users
   - Explain that updates are now automatic (no manual ZIP downloads)
   - Note that app checks for updates on every launch

---

## GitHub Repository Configuration

**Repository Settings Required:**
- Repository must be public OR you must configure a GitHub Personal Access Token (PAT)
- Releases feature must be enabled
- Each release must have these assets:
  - `ZeroTrustMigrationAddin-vX.X.X-COMPLETE.zip` (naming must match pattern)
  - `manifest.json` (must contain SHA256 hashes)

**Repository Name in Code:**
Verify the repository owner/name in [Services/GitHubUpdateService.cs](Services/GitHubUpdateService.cs):
```csharp
private const string RepoOwner = "your-github-username";
private const string RepoName = "cmaddin";
```

**Optional: Configure GitHub PAT for Private Repos**
If your repository is private, users need a GitHub PAT:
1. Create PAT at: https://github.com/settings/tokens
2. Scopes required: `repo` (Full control of private repositories)
3. Store PAT in app config or environment variable
4. Update GitHubUpdateService constructor to use PAT

---

## Support Information

**Questions or Issues?**
- Check logs: `%LocalAppData%\ZeroTrustMigrationAddin\Logs\*.log`
- Review [AUTO_UPDATE_GUIDE.md](AUTO_UPDATE_GUIDE.md) for architecture details
- Review [AUTO_UPDATE_QUICKSTART.md](AUTO_UPDATE_QUICKSTART.md) for quick reference

**Testing Completed By:** _______________________
**Date Tested:** _______________________
**Test Result:** [ ] PASS / [ ] FAIL
**Notes:** _______________________________________
