# Auto-Update Testing Workflow

**Date:** January 13, 2026  
**Tester:** Zero Trust Migration Journey Development Team  
**Goal:** Validate complete auto-update mechanism from v3.14.32 → v3.14.33

---

## Current State

✅ **Local Packages:**
- ZeroTrustMigrationAddin-v3.14.31-COMPLETE.zip (87.89 MB)
- ZeroTrustMigrationAddin-v3.14.32-COMPLETE.zip (87.89 MB)

✅ **manifest.json:** Version 3.14.32

✅ **.csproj:** Version 3.14.32

❌ **GitHub Releases:** None (previously deleted during troubleshooting)

---

## Testing Strategy

We'll create a controlled update path: **v3.14.31 → v3.14.32 → v3.14.33**

### Phase 1: Establish Baseline (v3.14.31)
Create GitHub release for v3.14.31 as the "currently installed" version

### Phase 2: Create First Update Target (v3.14.32)
Publish v3.14.32 to test initial update detection

### Phase 3: Create Second Update (v3.14.33)
Use new enhanced build script to create v3.14.33

### Phase 4: Test Auto-Update
Install v3.14.31 → Update to v3.14.32 → Update to v3.14.33

---

## Step-by-Step Execution

### ✅ **STEP 1: Publish v3.14.31 Baseline**

**Purpose:** Simulate "currently installed version" that will check for updates

```powershell
# Create GitHub release for v3.14.31
gh release create v3.14.31 `
  ZeroTrustMigrationAddin-v3.14.31-COMPLETE.zip `
  --title "Zero Trust Migration Journey Add-in v3.14.31 - Baseline" `
  --notes "Baseline version for testing auto-update mechanism."
```

**Problem:** We don't have `manifest-v3.14.31.json`! Need to create it.

**Solution:** Generate manifest from v3.14.31 package contents.

---

### ✅ **STEP 2: Create Manifest for v3.14.31**

```powershell
# Extract v3.14.31 package
$tempPath = "C:\Temp\CloudJourney_v3.14.31"
Expand-Archive -Path "ZeroTrustMigrationAddin-v3.14.31-COMPLETE.zip" -DestinationPath $tempPath -Force

# Generate manifest
$manifest = @{
    Version = "3.14.31"
    BuildDate = (Get-Item "ZeroTrustMigrationAddin-v3.14.31-COMPLETE.zip").LastWriteTimeUtc.ToString("o")
    Files = @()
    TotalSize = 0
}

$files = Get-ChildItem $tempPath -File
foreach ($file in $files) {
    $hash = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLower()
    
    $manifest.Files += @{
        RelativePath = $file.Name
        SHA256Hash = $hash
        FileSize = $file.Length
        LastModified = $file.LastWriteTimeUtc.ToString("o")
        IsCritical = $false
    }
    
    $manifest.TotalSize += $file.Length
}

# Save manifest
$manifestJson = $manifest | ConvertTo-Json -Depth 10
$manifestJson | Out-File "manifest-v3.14.31.json" -Encoding UTF8

# Cleanup
Remove-Item $tempPath -Recurse -Force

# Re-create release with manifest
gh release delete v3.14.31 --yes
gh release create v3.14.31 `
  ZeroTrustMigrationAddin-v3.14.31-COMPLETE.zip `
  manifest-v3.14.31.json `
  --title "Zero Trust Migration Journey Add-in v3.14.31 - Baseline" `
  --notes "Baseline version for auto-update testing. This version will check for and download updates to v3.14.32+."
```

---

### ✅ **STEP 3: Publish v3.14.32 Update Target**

```powershell
# We already have the package and manifest.json
# Just need to rename manifest for clarity and create release

# Backup current manifest as v3.14.32 specific
Copy-Item "manifest.json" "manifest-v3.14.32.json"

# Create GitHub release
gh release create v3.14.32 `
  ZeroTrustMigrationAddin-v3.14.32-COMPLETE.zip `
  manifest.json `
  --title "Zero Trust Migration Journey Add-in v3.14.32 - First Update" `
  --notes "First update target for auto-update testing.

### Changes from v3.14.31
- Enhanced build script (all improvements implemented)
- Comprehensive build documentation
- Auto-update testing preparation

### Auto-Update
Users on v3.14.31 will receive automatic update prompt."
```

---

### ✅ **STEP 4: Create v3.14.33 Using Enhanced Script**

**This tests our new build automation!**

```powershell
# Use the new enhanced build script
.\Build-And-Distribute.ps1 -PublishToGitHub -ReleaseNotes "Second update target for comprehensive auto-update testing.

### Changes from v3.14.32
- Verified enhanced build script works correctly
- Tested complete GitHub automation
- Validated auto-update mechanism

### Auto-Update
Users on v3.14.31 or v3.14.32 will receive automatic update prompt."
```

**Expected Results:**
- ✅ Version auto-incremented 3.14.32 → 3.14.33
- ✅ All 6 version locations updated
- ✅ CHANGELOG.md entry created
- ✅ Build, package, manifest generated
- ✅ GitHub Release created automatically
- ✅ ZIP + manifest.json uploaded

---

### ✅ **STEP 5: Test Auto-Update (v3.14.31 → v3.14.32)**

**Scenario 1: Clean Installation of v3.14.31**

1. **Extract v3.14.31 to test folder:**
   ```powershell
   $testPath = "C:\TestInstall\CloudJourney_v3.14.31"
   Expand-Archive -Path "ZeroTrustMigrationAddin-v3.14.31-COMPLETE.zip" -DestinationPath $testPath -Force
   ```

2. **Launch application:**
   ```powershell
   cd $testPath
   .\ZeroTrustMigrationAddin.exe
   ```

3. **Wait for update check** (happens automatically on launch)

4. **Verify update prompt appears:**
   - Should show: "Update available: v3.14.32"
   - Should show: Download size (delta vs full)

5. **Click "Update Now"**

6. **Monitor update process:**
   - ✅ Download manifest.json
   - ✅ Compare file hashes
   - ✅ Download only changed files
   - ✅ Apply update
   - ✅ Restart application

7. **Verify update succeeded:**
   - Check version in UI: Should show "v3.14.32"
   - Check About dialog
   - Check logs: `%LOCALAPPDATA%\CloudJourney\Logs`

**Expected Log Entries:**
```
[INFO] Checking for updates...
[INFO] Latest version available: 3.14.32
[INFO] Downloading manifest...
[INFO] Delta update: 12 files changed (~15 MB)
[INFO] Downloading update files...
[INFO] Applying update...
[INFO] Update successful! Restarting...
```

---

### ✅ **STEP 6: Test Auto-Update (v3.14.32 → v3.14.33)**

**Scenario 2: Update from Already-Updated Installation**

1. **Application should already be running from Step 5** (now at v3.14.32)

2. **Trigger manual update check:**
   - Menu: Help → Check for Updates
   - Or restart application

3. **Verify update prompt for v3.14.33**

4. **Apply update and verify**

---

### ✅ **STEP 7: Test "No Update Available"**

**Scenario 3: Already on Latest Version**

1. **Launch v3.14.33 (after Step 6)**

2. **Check for updates:**
   - Menu: Help → Check for Updates

3. **Verify message:**
   - "You are on the latest version (v3.14.33)"

---

### ✅ **STEP 8: Test Error Handling**

**Scenario 4: Network Issues**

1. **Disconnect from network**

2. **Launch application or check for updates**

3. **Verify graceful error:**
   - "Could not check for updates. Please check your internet connection."
   - Application continues to function normally

**Scenario 5: Corrupted Download**

1. **During update, kill network connection mid-download**

2. **Verify:**
   - Update rolls back
   - Application remains functional on current version
   - Can retry update

---

## Success Criteria

### Update Detection ✅
- [ ] Application checks for updates on launch
- [ ] GetLatestReleaseAsync() returns v3.14.32 from GitHub
- [ ] Version comparison works (3.14.31 < 3.14.32)
- [ ] Update prompt displays correctly

### Delta Download ✅
- [ ] manifest.json downloads successfully
- [ ] File hash comparison identifies changed files
- [ ] Only changed files download (not full 88 MB)
- [ ] Download progress shown to user

### Update Application ✅
- [ ] Files extracted to temp location
- [ ] Hash verification before applying
- [ ] Old files backed up
- [ ] New files copied to installation directory
- [ ] Application restarts with new version

### Error Handling ✅
- [ ] Network errors handled gracefully
- [ ] Corrupted downloads detected (hash mismatch)
- [ ] Rollback works if update fails
- [ ] User can retry failed updates

### GitHub Integration ✅
- [ ] Enhanced build script creates release correctly
- [ ] Both ZIP and manifest.json uploaded as assets
- [ ] Octokit GetLatest() returns release (not null)
- [ ] Release notes displayed in update prompt

---

## Testing Log

### Test Run 1: [DATE/TIME]

**Tester:** _______________

**v3.14.31 → v3.14.32:**
- Update detected: ☐ Yes ☐ No
- Delta size: _______ MB
- Update time: _______ seconds
- Result: ☐ Success ☐ Failed
- Notes: _________________________________

**v3.14.32 → v3.14.33:**
- Update detected: ☐ Yes ☐ No
- Delta size: _______ MB
- Update time: _______ seconds
- Result: ☐ Success ☐ Failed
- Notes: _________________________________

**Already Latest:**
- Message shown: ☐ Yes ☐ No
- App stable: ☐ Yes ☐ No
- Notes: _________________________________

**Error Scenarios:**
- Network error handled: ☐ Yes ☐ No
- Rollback worked: ☐ Yes ☐ No ☐ N/A
- Notes: _________________________________

---

## Troubleshooting

### Issue: "No releases found"
**Cause:** GitHub Release doesn't have both ZIP and manifest.json  
**Fix:** Ensure both assets uploaded

### Issue: Update not detected
**Cause:** Version comparison issue or cache  
**Fix:** Check logs, delete `%LOCALAPPDATA%\CloudJourney\update-check-cache.json`

### Issue: "Could not download update"
**Cause:** Asset URL changed or network issue  
**Fix:** Verify assets exist on GitHub release page

### Issue: Hash mismatch
**Cause:** File corrupted during download  
**Fix:** Update should auto-retry; check network stability

---

## Next Steps After Testing

1. ☐ Document any issues found
2. ☐ Fix bugs in update mechanism
3. ☐ Update AUTO_UPDATE_GUIDE.md with findings
4. ☐ Create user-facing update notifications
5. ☐ Set up automated build/release pipeline
6. ☐ Schedule regular update checks (daily? weekly?)

---

**Status:** 🟡 Ready to Begin Testing  
**Last Updated:** January 13, 2026
