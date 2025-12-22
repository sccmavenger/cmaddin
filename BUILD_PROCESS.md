# Official Build Process for CloudJourney Addin

## 📋 Overview

This document defines the **ONE TRUE BUILD PROCESS** to prevent confusion and ensure consistent releases.

## ⚠️ IMPORTANT: Only One Build Process

**DO NOT** manually create packages. **ALWAYS** use the `Build-And-Distribute.ps1` script.

## 🔢 Automatic Version Bump Rules (Semantic Versioning)

**The build script automatically determines version bumps based on these rules:**

### PATCH Bump (X.X.+1) - Bug Fixes & Minor Corrections
**Use when:**
- Fixing bugs (e.g., UI not displaying correctly, binding errors)
- Correcting typos or text
- Fixing broken functionality without adding features
- Performance optimizations that don't change behavior
- Documentation fixes
- Internal refactoring (no user-facing changes)

**Examples:**
- Fixed reasoning panel not showing observations → 2.0.0 → 2.0.1
- Fixed window title showing wrong version → 2.0.1 → 2.0.2
- Corrected encoding issue in uninstall script → 2.0.2 → 2.0.3

### MINOR Bump (X.+1.0) - New Features (Backward Compatible)
**Use when:**
- Adding new features (e.g., new buttons, new reports)
- Adding new tools to the agent
- Enhancing existing functionality without breaking changes
- Adding new configuration options
- Adding new tabs or sections to UI

**Examples:**
- Added Agent Mode with reasoning panel → 1.16.0 → 2.0.0 (was major because architectural)
- Add new "Export to Excel" button → 2.0.1 → 2.1.0
- Add new device filter options → 2.1.0 → 2.2.0

### MAJOR Bump (+1.0.0) - Breaking Changes or Major Releases
**Use when:**
- Removing features (breaking change)
- Changing authentication mechanism (breaking change)
- Major architectural changes (e.g., switching from mock to real data)
- Changing data formats that affect existing installations
- Any change that requires users to reconfigure or reinstall
- Major milestone releases (e.g., v1.0 → v2.0 for Enrollment Agent)

**Examples:**
- Initial v1.0.0 release
- Added Enrollment Agent v2.0 (major architectural change) → 1.16.0 → 2.0.0
- Remove deprecated API → 2.5.0 → 3.0.0

### 🤖 Automated Decision Logic

When running builds, the agent should:
1. Analyze the changes made since last version
2. Apply the LOWEST appropriate bump:
   - If only bug fixes → PATCH
   - If new features added → MINOR  
   - If breaking changes → MAJOR
3. Run: `.\Build-And-Distribute.ps1 -BumpVersion [Patch|Minor|Major]`

**NO user input required - the agent determines the correct bump type.**

## 🎯 What Build-And-Distribute.ps1 Does (Complete Breakdown)

### PHASE 1: VERSION MANAGEMENT (Auto-Updates 6 Locations)

When you run the script **WITHOUT** the `-Version` parameter, it automatically:

1. **Reads current version** from `CloudJourneyAddin.csproj`
2. **Prompts you** for version bump type (PATCH/MINOR/MAJOR)
3. **Auto-increments** the version number following semantic versioning
4. **Updates ALL 6 REQUIRED LOCATIONS:**

   **[1] CloudJourneyAddin.csproj** (3 properties)
   - `<Version>X.X.X</Version>`
   - `<AssemblyVersion>X.X.X.0</AssemblyVersion>`
   - `<FileVersion>X.X.X.0</FileVersion>`

   **[2] README.md**
   - Replaces "Version X.X.X" with new version
   - Replaces "vX.X.X" references

   **[3] USER_GUIDE.md**
   - Replaces "Version X.X.X" with new version
   - Updates version references in header

   **[4] INTERNAL_DOCS_vX.X.X.md**
   - Creates NEW file for the new version
   - Copies from previous version and updates

   **[5] Views/DashboardWindow.xaml**
   - Updates `Title="Cloud Journey Progress Dashboard vX.X.X"`
   - This is what shows in the window title bar

   **[6] ViewModels/DashboardViewModel.cs**
   - Updates `LogInformation("Dashboard initialized - Version: X.X.X")`

### PHASE 2: PRE-FLIGHT CHECKS

5. **Verifies all 6 locations** contain the correct version
6. **Warns** if any location is missing or mismatched
7. **Continues anyway** (warnings don't stop build)

### PHASE 3: BUILD PROCESS

8. **Cleans previous builds**: Deletes `bin/` and `obj/` folders
9. **Builds project**: `dotnet build --configuration Release`
10. **Publishes**: `dotnet publish --configuration Release --runtime win-x64 --self-contained true`
11. **Verifies critical files**: Checks `CloudJourneyAddin.exe` and `Azure.Identity.dll` exist

### PHASE 4: PACKAGE CREATION

12. **Creates package folder**: `CloudJourneyAddin-vX.X.X-COMPLETE/`
13. **Copies 277+ binary files** from `bin\Release\net8.0-windows\win-x64\publish\`
14. **Copies 8 support scripts**:
    - `Update-CloudJourneyAddin.ps1` - Automated update tool
    - `Diagnose-Installation.ps1` - Environment diagnostics
    - `Verify-CloudJourneyAddin.ps1` - Post-install verification
    - `Check-ConsoleLog.ps1` - Log viewer
    - `Find-ConsoleLogs.ps1` - Log file finder
    - `Uninstall-CloudJourneyAddin.ps1` - Clean uninstall
    - `Create-Shortcuts.ps1` - Desktop shortcut creator
    - `CloudJourneyAddin.xml` - Configuration
15. **Compresses to ZIP**: `CloudJourneyAddin-vX.X.X-COMPLETE.zip`

### PHASE 5: VERIFICATION

16. **Checks EXE version**: Reads file version from CloudJourneyAddin.exe
17. **Compares versions**: Warns if EXE version doesn't match expected
18. **Verifies Azure.Identity.dll**: Confirms critical dependency present
19. **Reports package metrics**: File count (~285) and size (~86 MB)

### PHASE 6: DISTRIBUTION

20. **Copies to build folder**: `.\CloudJourneyAddin-vX.X.X-COMPLETE.zip`
21. **Copies to Dropbox**: `C:\Users\dannygu\Dropbox\CloudJourneyAddin-vX.X.X-COMPLETE.zip`
22. **Displays both locations** for easy access

### PHASE 7: POST-BUILD REMINDERS

23. **Reminds to update CHANGELOG.md** (not auto-updated, manual step)
24. **Reminds to create RELEASE_NOTES** if needed
25. **Reminds to test** on clean machine
26. **Reminds to git commit and tag**:
    ```bash
    git add .
    git commit -m 'Release vX.X.X'
    git tag -a vX.X.X -m 'Version X.X.X'
    git push origin main --tags
    ```

---

## 🎯 The Official Process

### ✅ RECOMMENDED: Auto-Increment (Use This!)

```powershell
# Let the script auto-increment and update ALL 6 locations
.\Build-And-Distribute.ps1
```

**What happens:**
1. ✅ Prompts for PATCH/MINOR/MAJOR
2. ✅ Auto-increments version
3. ✅ Updates all 6 locations automatically
4. ✅ Builds, packages, distributes

### ⚠️ ADVANCED: Specific Version (Use With Caution)

```powershell
# Specify exact version (SKIPS auto-update of 6 locations!)
.\Build-And-Distribute.ps1 -Version "2.0.0"
```

**What happens:**
1. ❌ SKIPS Phase 1 (version auto-update)
2. ❌ Only updates .csproj
3. ❌ XAML title stays old (window shows wrong version!)
4. ❌ Docs stay old
5. ⚠️ You must manually update all 6 locations first!

**When to use this:**
- Only when you've already manually updated all 6 locations
- For rebuilding same version after code changes
- NOT recommended for new releases

### 🔄 Version Bump Types

```powershell
# Bug fixes only
.\Build-And-Distribute.ps1 -BumpVersion Patch  # 2.0.0 → 2.0.1

# New features (backward compatible)
.\Build-And-Distribute.ps1 -BumpVersion Minor  # 2.0.1 → 2.1.0

# Breaking changes
.\Build-And-Distribute.ps1 -BumpVersion Major  # 2.1.0 → 3.0.0
```

## 📦 Output Files

After running the script, you will have:

### 1. COMPLETE Package (Primary Distribution)
**Location**: `CloudJourneyAddin-vX.X.X-COMPLETE.zip`
- In build folder: `C:\Users\dannygu\Downloads\GitHub Copilot\cmaddin\`
- In Dropbox: `C:\Users\dannygu\Dropbox\`

**Contains**:
- All application binaries (.exe, .dll, dependencies)
- All update/diagnostic scripts
- Configuration files
- ~285 files, ~85-90 MB

**Use for**:
- New installations
- Updates to existing installations
- Production deployment
- Testing on remote machines

### 2. DO NOT Create Manual Packages

**DO NOT** create packages manually like:
- ❌ CloudJourneyAddin_v2.0.0_Production.zip
- ❌ CloudJourneyAddin_v2.0.0_UpdatePackage.zip
- ❌ UpdatePackage_v2.0.0_Complete/

These are **NOT** needed. The `Build-And-Distribute.ps1` script creates the **ONLY** package you need.

## 🚀 Deployment Instructions

### For End Users (Update Existing Installation)

1. Extract `CloudJourneyAddin-vX.X.X-COMPLETE.zip`
2. Run `Update-CloudJourneyAddin.ps1` as Administrator
3. Script auto-detects installation and updates files

### For End Users (New Installation)

1. Extract `CloudJourneyAddin-vX.X.X-COMPLETE.zip` to any location
2. Run `CloudJourneyAddin.exe`

### For Production Testing

1. Copy package from Dropbox to test machine
2. Extract all files
3. Run `Diagnose-Installation.ps1` to verify environment
4. Run `CloudJourneyAddin.exe`

## 🔍 Troubleshooting

### Version Mismatch Warning

If you see:
```
⚠️ WARNING: EXE version mismatch! Expected: 2.0.0.0, Found: 3.0.0.0
```

**Solution**: Update the version in `CloudJourneyAddin.csproj` to match your intended version before running the script.

### Script Won't Run

```powershell
# Unblock the script
Unblock-File -Path .\Build-And-Distribute.ps1

# Run as Administrator
Right-click → Run as Administrator
```

### Build Fails

Check:
1. .NET 8.0 SDK installed
2. All code compiles without errors
3. No files locked by running application

## 📝 Version History Management

The script automatically validates that documentation is updated:

**Required Updates Before Building:**
1. `README.md` - Add version to "What's New" section
2. `USER_GUIDE.md` - Update version in header
3. `INTERNAL_DOCS_vX.X.X.md` - Create new file for version
4. `CHANGELOG.md` - Add entry with changes (do this manually)

**Semantic Versioning Rules:**
- **PATCH** (x.x.1): Bug fixes only, no new features
- **MINOR** (x.1.0): New features, backward compatible
- **MAJOR** (1.0.0): Breaking changes, major rewrites

## 📊 Post-Build Checklist

After running `Build-And-Distribute.ps1`:

- [ ] Package created in build folder
- [ ] Package copied to Dropbox
- [ ] Version matches expected version (no warnings)
- [ ] Package size is reasonable (~85-90 MB)
- [ ] File count is correct (~285 files)
- [ ] Update CHANGELOG.md with release date
- [ ] Test package on clean machine
- [ ] Commit and tag in git:
  ```bash
  git add .
  git commit -m 'Release vX.X.X'
  git tag -a vX.X.X -m 'Version X.X.X'
  git push origin main --tags
  ```

## 🎯 Decision Tree: Which Version to Use?

```
Starting a build?
│
├─ Is this a bug fix only?
│  └─ Use PATCH: .\Build-And-Distribute.ps1 -BumpVersion Patch
│
├─ Adding new features (backward compatible)?
│  └─ Use MINOR: .\Build-And-Distribute.ps1 -BumpVersion Minor
│
├─ Major rewrite or breaking changes?
│  └─ Use MAJOR: .\Build-And-Distribute.ps1 -BumpVersion Major
│
└─ Specific version needed?
   └─ Use: .\Build-And-Distribute.ps1 -Version "X.X.X"
```

## ❌ What NOT to Do

1. ❌ Don't manually create ZIP files
2. ❌ Don't manually copy files to Dropbox
3. ❌ Don't create "UpdatePackage" folders manually
4. ❌ Don't use `dotnet publish` directly
5. ❌ Don't create multiple package variants
6. ❌ Don't skip the Build-And-Distribute.ps1 script

## ✅ What TO Do

1. ✅ Always use `Build-And-Distribute.ps1`
2. ✅ Let the script handle version management
3. ✅ Let the script create and distribute packages
4. ✅ Use the COMPLETE package for everything
5. ✅ Follow semantic versioning rules
6. ✅ Update documentation before building

---

## 🎉 Summary

**The Golden Rule**: 
> There is ONE script (`Build-And-Distribute.ps1`) that creates ONE package (`CloudJourneyAddin-vX.X.X-COMPLETE.zip`). This package is used for EVERYTHING (new installations, updates, testing, production).

**Never manually create packages. Period.**
