# Auto-Update Implementation Guide

## Overview

This document describes the GitHub Releases-based auto-update system implemented for ZeroTrustMigrationAddin. The system enables automatic distribution of updates to test customers without manually sending ZIP files.

## Architecture

### Components Created

1. **Models/UpdateManifest.cs**
   - `UpdateManifest`: Contains version, build date, file list, and total size
   - `FileEntry`: SHA256 hash, size, path, last modified, critical flag for each file
   - `UpdateSettings`: User preferences for auto-update behavior
   - `UpdateCheckResult`: Result of update check with delta information

2. **Services/GitHubUpdateService.cs**
   - Queries GitHub Releases API using Octokit library
   - Compares current version with latest release
   - Supports authenticated (PAT) and anonymous access
   - **CRITICAL for PRIVATE repos:** Authentication required to access private releases
   - Rate limits: 60 req/hr anonymous, 5,000 req/hr with token
   - Checks for updates once per 24 hours

3. **Services/DeltaUpdateService.cs**
   - Downloads and compares manifests (local vs. remote)
   - Identifies changed files by SHA256 hash comparison
   - Downloads full ZIP and extracts only changed files
   - Verifies file integrity after download
   - Generates manifest from installation if missing

4. **Services/UpdateApplier.cs**
   - Creates PowerShell script to apply updates
   - Waits for application to close gracefully
   - Replaces changed files with new versions
   - Updates local manifest
   - Restarts application automatically
   - Creates backups before updating

5. **Views/UpdateNotificationWindow.xaml + ViewModel**
   - User-friendly dialog showing update details
   - Displays current version → new version
   - Shows release notes from GitHub
   - Progress bar during download
   - Options: Download & Install, Remind Later, Skip Version

6. **App.xaml.cs Updates**
   - Checks for updates on startup (async, non-blocking)
   - Respects 24-hour check interval
   - Shows update dialog if newer version available

7. **Build-And-Distribute.ps1 Updates**
   - Step 5a: Generates `manifest.json` with SHA256 hashes for all files
   - Identifies critical files (exe, core DLLs)
   - Includes manifest in build output
   - **FIXED (Jan 2026):** Variable collision bug - `$version` renamed to `$toolVersion`
     * PowerShell is case-insensitive: `$version` (dotnet SDK) was overwriting `$Version` parameter
     * Caused builds to use .NET SDK version (9.0.308) instead of specified version
     * Now uses `$toolVersion` internally to avoid collision

## How It Works

### Update Flow

```
1. App Startup
   └─ CheckForUpdatesAsync() on EVERY launch

2. GitHub API Query
   ├─ GET /repos/sccmavenger/cmaddin/releases/latest
   └─ Compare versions (current vs. latest)

3. Download Manifest
   ├─ Download manifest.json from release assets
   └─ Compare with local manifest

4. Calculate Delta
   ├─ Compare SHA256 hashes
   ├─ Identify changed files
   └─ Calculate download size

5. AUTOMATIC Update (No User Prompt)
   ├─ Show progress window
   └─ Update happens automatically

6. Download Update
   ├─ Download full ZIP package
   ├─ Extract only changed files
   └─ Verify SHA256 hashes

7. Apply Update
   ├─ Generate PowerShell update script
   ├─ Start script (runs in background)
   ├─ Close application
   ├─ Script replaces files
   ├─ Script updates local manifest
   └─ Script restarts application
```

### Delta Update Savings

| Update Type | Typical Size | Time (10 Mbps) | Savings |
|------------|--------------|----------------|---------|
| Full ZIP | 86 MB | ~70 seconds | - |
| Patch Update | 10-15 MB | ~10 seconds | 82-85% |
| Minor Update | 20-30 MB | ~20 seconds | 65-77% |

### Typical File Changes

**Patch (3.14.25 → 3.14.26):** 5-15 files
- ZeroTrustMigrationAddin.exe
- ZeroTrustMigrationAddin.dll
- 3-10 modified service DLLs
- manifest.json

**Minor (3.14.x → 3.15.0):** 20-40 files
- All of patch changes
- New features (new DLLs)
- Updated dependencies

## Usage

### For Developers

#### 1. Build with Auto-Update

```powershell
# Build and generate manifest
.\Build-And-Distribute.ps1

# Output:
# - ZeroTrustMigrationAddin-v3.14.26-COMPLETE.zip
# - manifest.json (at project root)
```

#### 2. Create GitHub Release

```powershell
# Manual method
gh release create v3.14.26 `
  "ZeroTrustMigrationAddin-v3.14.26-COMPLETE.zip" `
  "manifest.json" `
  --title "Version 3.14.26" `
  --notes-file RELEASE_NOTES_v3.14.26.md
```

**Important:** Always include both `ZeroTrustMigrationAddin-v*.zip` AND `manifest.json` as release assets.

#### 3. Test Update Flow

1. Build v3.14.25: `.\Build-And-Distribute.ps1`
2. Install locally and generate manifest
3. Build v3.14.26: `.\Build-And-Distribute.ps1`
4. Create GitHub Release with ZIP and manifest
5. Run v3.14.25
6. Update dialog should appear automatically

### For End Users

#### First Install (Manual)

1. Download `ZeroTrustMigrationAddin-v3.14.26-COMPLETE.zip` from GitHub Releases
2. Extract to folder
3. Run `Install-ZeroTrustMigrationAddin.ps1` (or manual install)
4. Launch `ZeroTrustMigrationAddin.exe`

#### Subsequent Updates (Automatic)

1. Launch application normally
2. Progress window appears if update available
3. Update downloads and installs automatically (NO user interaction)
4. Application restarts with new version (typically 10-30 seconds total)

#### Manual Update Check

- Settings → Check for Updates (future feature)
Updates happen automatically on every launch. To disable:

```json
{
  "AutoCheckForUpdates": false
}
```

Save to `%LocalAppData%\ZeroTrustMigrationAddin\update-settings.json`
## Configuration

### Update Settings Location

```
%LocalAppData%\ZeroTrustMigrationAddin\
  ├─ manifest.json (current version info)
  └─ update-settings.json (user preferences)
```

### Update Settings St],
  "LocalManifestPath": null
}
```

**Note:** `SkippedVersions` is no longer used - updates are automatic.
  "GitHubToken": null,
  "LastUpdateCheck": "2026-01-13T10:30:00Z",
  "AutoCheckForUpdates": true,
  "SkippedVersions": ["3.14.26"],
  "LocalManifestPath": null
}
```

### Optional: GitHub PAT for Higher Rate Limits

```json
{
  "GitHubToken": "ghp_yourPersonalAccessTokenHere",
  "AutoCheckForUpdates": true
}
```

**Benefits:**
- 5,000 requests/hour vs. 60 anonymous
- Required permissions: `public_repo` (read-only)

### Authentication for Private Repositories

**⚠️ CRITICAL:** If your repository is PRIVATE, authentication is **REQUIRED**. Anonymous access cannot see private releases.

#### Symptoms of Missing Authentication
- Log shows: `"No releases found in repository sccmavenger/cmaddin"`
- Log shows: `"WARNING: No releases found"`
- Update check fails silently
- Even though releases exist on GitHub

#### Solution: Configure GitHub Token

**Method 1: Using GitHub CLI (Recommended)**

If you have GitHub CLI installed and authenticated:

```powershell
# Get your token from GitHub CLI
$ghToken = gh auth token

# Create update-settings.json with authentication
$settingsPath = "$env:LOCALAPPDATA\ZeroTrustMigrationAddin\update-settings.json"
$settings = @{
    RepositoryOwner = "sccmavenger"
    RepositoryName = "cmaddin"
    CheckOnLaunch = $true
    GitHubToken = $ghToken
}
$settings | ConvertTo-Json | Out-File $settingsPath -Encoding UTF8

Write-Host "✅ GitHub token configured for private repository access"
```

**Method 2: Create Personal Access Token**

1. Go to GitHub Settings → Developer settings → Personal access tokens
2. Click "Generate new token (classic)"
3. Select scopes:
   - `repo` (full access) for private repos
   - OR `public_repo` for public repos only
4. Copy the token (starts with `ghp_`)
5. Add to `update-settings.json`:

```json
{
  "RepositoryOwner": "sccmavenger",
  "RepositoryName": "cmaddin",
  "GitHubToken": "ghp_YourActualTokenHere",
  "CheckOnLaunch": true
}
```

#### Verification

After configuring authentication, check logs for:

```
[INFO] 🔍 [DEBUG] Authentication: Authenticated
[INFO] 🔍 [DEBUG] Total releases found: 3
[INFO] ✅ [DEBUG] GetLatest() SUCCESS: v3.15.0
```

If you see `"Authentication: Unauthenticated"` or `"using anonymous access"`, the token was not loaded correctly.

#### Security Notes

- Store token securely in local user directory only
- Never commit tokens to source control
- Tokens grant access to your GitHub account - treat like passwords
- Use tokens with minimal required permissions
- Rotate tokens periodically

## TrouProgress Window Appears

**Cause:** Already on latest version or update check disabled  
**Solution:** Check logs for update check messages
**Cause:** Last check was < 24 hours ago  
**Solution:** Delete `%LocalAppData%\ZeroTrustMigrationAddin\update-settings.json`

### "No releases found in repository"

**Cause:** Repository is PRIVATE and app is using anonymous GitHub API access  
**Solution:** Configure GitHub Personal Access Token (see Authentication section above)  
**Verification:** Check logs for `"Authentication: Authenticated"` instead of `"using anonymous access"`  
**Details:** Anonymous API cannot access private repos (returns empty list). With authentication, private releases become visible.

### "No ZIP package found in release assets"

**Cause:** ZIP file not uploaded to GitHub Release  
**Solution:** Ensure release contains `ZeroTrustMigrationAddin-v*.zip`

### "Failed to download manifest"

**Cause:** `manifest.json` not included in release assets  
**Solution:** Re-run build script and upload `manifest.json` to release

### Download Failed or Hash Mismatch

**Cause:** Corrupted download or wrong manifest  
**Solution:** 
1. Check internet connection
2. Verify manifest matches ZIP contents
3. Re-upload release assets

### Application Won't Restart After Update

**Cause:** PowerShell execution policy or script error  
**Solution:** Check `%TEMP%\ZeroTrustMigrationAddin-Update.log` for details

### Files Not Updating Despite Download Success (v3.16.1 and earlier)

**Cause:** Application installed in Program Files without UAC elevation  
**Symptoms:**
- Update downloads successfully
- Progress window completes
- App restarts but still shows old version
- `ZeroTrustMigrationAddin-Update.log` shows: `"Access to the path '...' is denied"` for ALL files

**Solution:** Update to v3.16.2 or later (includes UAC elevation fix)  
**Details:** v3.16.2+ uses `Verb = "runas"` to request administrator elevation before applying file updates

### UAC Prompt Doesn't Appear

**Cause:** User account doesn't have admin rights or UAC disabled  
**Solution:**
1. Verify user is in Administrators group
2. Check UAC settings: `Control Panel → User Accounts → Change User Account Control settings`
3. UAC should be set to at least "Notify me only when apps try to make changes"

**Alternative:** Install application to user-writable location (not Program Files)

### User Declines UAC Prompt

**Cause:** User clicked "No" on UAC elevation request  
**Behavior:**
- Update process terminates
- Application continues running with current (old) version
- No error message shown to user
- Next launch will retry the update

**Solution:** Accept UAC prompt on next launch to apply update

## Advanced Features

### First-Time Manifest Generation

If user installed via manual ZIP (no manifest exists):

```csharp
var deltaService = new DeltaUpdateService();
var currentVersion = "3.14.25"; // From assembly
deltaService.GenerateManifestFromInstallation(currentVersion);
```

This scans all files in installation folder and creates local manifest for future delta updates.

### Skip Version

**DEPRECATED** - Updates are now automatic. SkipVersion functionality removed.

### Manual Update Application

For advanced users or testing:

```csharp
var applier = new UpdateApplier();
var result = await applier.ApplyUpdateAsync(
    tempDownloadPath,
    changedFiles,
    newManifest);
```

## Security Considerations

1. **SHA256 Verification:** All downloaded files verified before installation
2. **HTTPS Only:** GitHub Releases uses HTTPS for downloads
3. **UAC Elevation Required (v3.16.2+):** Updates now request administrator elevation via UAC
   - **Why:** Application installed in Program Files requires admin rights to modify files
   - **User Experience:** Standard Windows UAC prompt appears before file updates
   - **Security:** Same elevation model used by Chrome, VS Code, and other major applications
   - **Implementation:** PowerShell update script launched with `Verb = "runas"`
4. **Backup Created:** Original files backed up to `%TEMP%\ZeroTrustMigrationAddin-Backup\`
5. **Source Authentication:** Only downloads from official `sccmavenger/cmaddin` repository

## Future Enhancements

### Completed (January 2026)
- ✅ **Build Script Fix:** Resolved version parameter collision bug
- ✅ **Private Repository Support:** Added GitHub authentication support
- ✅ **Debug Logging:** Enhanced logging for troubleshooting
- ✅ **Authentication Documentation:** Comprehensive guide for private repos

### Potential Improvements

1. **Individual File Hosting:** Host changed files separately (avoid full ZIP download)
2. **Compression:** Use 7zip or better compression for smaller downloads
3. **Update Channels:** Support stable/beta channels
4. **Rollback UI:** Add "Rollback to Previous Version" button
5. **Background Downloads:** Download in background, apply on next restart
6. **Update Schedule:** Let users choose check frequency
7. **Bandwidth Throttling:** Limit download speed for large organizations
8. **Token Management UI:** In-app configuration for GitHub authentication

### Not Implemented (By Design)

- ❌ Rollback mechanism (user requested no rollback)
- ❌ Release channels (alpha only, no stable/beta split)
- ❌ Silent auto-updates (user must approve)
- ❌ Forced updates (user can skip or remind later)

## Testing Checklist

### Before Release

- [ ] Build script completes without errors
- [ ] Version correctly embedded in all files (exe, dll, manifest)
- [ ] SHA256 hashes in manifest.json match actual file contents
- [ ] GitHub Release created with both ZIP + manifest.json assets
- [ ] Manifest.json not empty (hash should NOT be E3B0C44...)
- [ ] Update check detects new version
- [ ] Delta calculation identifies correct changed files
- [ ] Download progress shows accurately
- [ ] **UAC elevation prompt appears**
- [ ] **Files replaced successfully after accepting UAC**
- [ ] Application restarts after update with correct new version
- [ ] Local manifest updated to new version
- [ ] Logs show update process clearly (both app log and Update.log)
- [ ] ZeroTrustMigrationAddin-Update.log shows successful file copies (not "Access is denied")

### Test Scenarios

1. **First Install:** Manual ZIP → Should generate manifest on startup (278 files)
2. **Patch Update:** 3.16.1 → 3.16.2 → Should download 5 files (~2.3 MB, UAC required)
3. **Minor Update:** 3.15.x → 3.16.0 → Should download 20-40 files
4. **Full Update (No Manifest):** Delete local manifest → Should download all 278 files (87.9 MB)
5. **UAC Declined:** User clicks "No" on UAC → Update fails, app continues with old version
6. **UAC Accepted:** User clicks "Yes" on UAC → Files update successfully, app restarts
7. **Network Error:** Disconnect during download → Shows error, allows retry
8. **Rate Limit:** Make 61 anonymous requests → Should show rate limit error
9. **Private Repository:** Access without token → Should show "No releases found" error
10. **Authentication:** Configure token → Should show "Authentication: Authenticated" in logs
11. **Cached Manifest:** Update with stale cached manifest → Should recalculate delta correctly
12. **Permissions Test:** Check ZeroTrustMigrationAddin-Update.log for "Access is denied" errors

### Verified Test Results (v3.16.2 - January 14, 2026)

✅ **Authentication Test:**
- Repository: sccmavenger/cmaddin (PRIVATE)
- Without token: "No releases found in repository"
- With GitHub CLI token: "Authentication: Authenticated"
- Result: **PASS** - Successfully detected 6 releases

✅ **Release Detection:**
- Found v3.16.2, v3.16.1, v3.16.0, v3.15.0, v3.14.32, v3.14.31
- Each release has 2 assets (ZIP + manifest.json)
- GetLatest() returned v3.16.2 correctly
- Result: **PASS**

✅ **Version Comparison:**
- Current version: 3.16.2
- Latest version: 3.16.2
- Message: "No update available - current version is up to date"
- Result: **PASS**

✅ **Delta Update Test (v3.16.1 → v3.16.2):**
- Changed files detected: 5 (ZeroTrustMigrationAddin.exe, .dll, .pdb, .deps.json, README.md)
- Download size: 2,334,826 bytes (~2.3 MB)
- Bandwidth savings: 97.5% (vs 87.9 MB full package)
- Download time: ~4 seconds
- Result: **PASS**

✅ **UAC Elevation Test (v3.16.2):**
- UAC prompt appeared: YES
- User accepted elevation: YES
- PowerShell script completed: SUCCESS
- All 5 files copied: SUCCESS (verified in ZeroTrustMigrationAddin-Update.log)
- Application restarted: SUCCESS
- Final version: 3.16.2
- Result: **PASS** - Critical fix resolved permissions failures

✅ **Full Update Test (v3.15.0 → v3.16.1):**
- Test device: UCL-CM (ConfigMgr server)
- No local manifest: Treated all 278 files as changed
- Download size: 92,174,568 bytes (87.9 MB)
- Download time: ~30 seconds
- Extraction: 278 files verified
- UAC elevation: Required and accepted
- Result: **PASS**

✅ **Build Script Fix (v3.15.0):**
- Issue: PowerShell case-insensitive variable collision ($version vs $Version)
- Built v3.15.0 successfully (not 9.0.308)
- Version correctly applied to all 6 locations
- Result: **PASS**

**Key Learnings:**
- Private repositories MUST have authentication configured on all devices
- GitHub CLI (`gh auth token`) provides quick token access for dev machines
- PowerShell variable names must be unique regardless of case
- **UAC elevation is REQUIRED for Program Files installations**
  * Without elevation: ALL file copy operations fail with "Access is denied"
  * ZeroTrustMigrationAddin-Update.log is essential for diagnosing permissions failures
  * Users must accept UAC prompt for updates to apply successfully
- Manifest caching dramatically improves bandwidth efficiency (97.5% savings)
- Delta updates complete in seconds vs minutes for full package
- Debug logging is essential for diagnosing update issues

## File Locations Reference

### Source Code
```
Models/UpdateManifest.cs
Services/GitHubUpdateService.cs
Services/DeltaUpdateService.cs
Services/UpdateApplier.cs
Views/UpdateNotificationWindow.xaml
Views/UpdateNotificationWindow.xaml.cs
ViewModels/UpdateNotificationViewModel.cs
App.xaml.cs (modified)
Build-And-Distribute.ps1 (modified - Step 5a added)
ZeroTrustMigrationAddin.csproj (added Octokit NuGet)
```

### Runtime Files
```
%LocalAppData%\ZeroTrustMigrationAddin\
  ├─ manifest.json
  ├─ update-settings.json
  └─ Logs\ZeroTrustMigrationAddin_YYYYMMDD.log

%TEMP%\
  ├─ ZeroTrustMigrationAddin-Update\{GUID}\ (download temp)
  ├─ ZeroTrustMigrationAddin-Backup\{timestamp}\ (backup)
  ├─ ZeroTrustMigrationAddin-ApplyUpdate.ps1 (update script)
  └─ ZeroTrustMigrationAddin-Update.log (update log)
```

## Support

### Log File Locations

**Application Logs:**
```
%LocalAppData%\ZeroTrustMigrationAddin\Logs\ZeroTrustMigrationAddin_YYYYMMDD.log
```

**Update Script Log:**
```
%TEMP%\ZeroTrustMigrationAddin-Update.log
```

### Key Log Messages

```
[Update Check]
"Checking for updates from GitHub Releases..."
"Update available: 3.14.25 → 3.14.26"
"Delta: 12 files, 15728640 bytes"

[Download]
"Downloading ZIP package: https://..."
"ZIP downloaded: 90112345 bytes"
"Extracting 12 changed files from ZIP..."
"Verified 12/12 files"

[Apply]
"Starting Update Application Process"
"Files to update: 12"
"Update scheduled successfully"
"Application will restart to apply updates"
```

## Conclusion

The auto-update system provides:
Zero-Touch Updates:** Automatic updates on every launch  
✅ **Easy Distribution:** No more manual ZIP emails  
✅ **Fast Updates:** 80-90% smaller downloads via delta updates  
✅ **Reliable:** SHA256 verification and backup system  
✅ **Transparent:** Clear progress window and logs  
✅ **GitHub Native:** Leverages existing GitHub Releases infrastructure  

Perfect for distributing alpha builds to test customers with automatic updates
Perfect for distributing alpha builds to 10 test customers without ongoing manual effort.
