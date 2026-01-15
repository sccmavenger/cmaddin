# Build Script Guide - Internal Documentation

## Overview

The `Build-And-Distribute.ps1` script is the **canonical build automation tool** for Zero Trust Migration Journey Add-in. It handles all aspects of building, packaging, testing, and distributing releases with comprehensive validation and optional GitHub integration.

**Version:** 2.0.0  
**Status:** Production-ready, all recommended improvements implemented  
**Maintenance:** This document should be updated whenever script capabilities change

---

## Table of Contents

1. [Quick Start](#quick-start)
2. [Script Capabilities](#script-capabilities)
3. [Parameters Reference](#parameters-reference)
4. [Workflow Stages](#workflow-stages)
5. [Version Management](#version-management)
6. [Manifest System](#manifest-system)
7. [GitHub Integration](#github-integration)
8. [Error Handling & Rollback](#error-handling--rollback)
9. [Troubleshooting](#troubleshooting)
10. [Advanced Usage](#advanced-usage)
11. [Development Guidelines](#development-guidelines)

---

## Quick Start

### Basic Build (Patch Version Increment)
```powershell
.\Build-And-Distribute.ps1
```
- Auto-increments patch version (3.14.31 → 3.14.32)
- Builds, tests, packages, and copies to Dropbox
- Takes ~3-5 minutes

### Minor Version Release
```powershell
.\Build-And-Distribute.ps1 -BumpVersion Minor
```
- Increments minor version (3.14.31 → 3.15.0)
- Resets patch to 0

### Major Version Release with GitHub Publish
```powershell
.\Build-And-Distribute.ps1 -BumpVersion Major -PublishToGitHub -ReleaseNotes "Complete rewrite with new architecture"
```
- Increments major version (3.14.31 → 4.0.0)
- Creates GitHub Release automatically
- Uploads ZIP + manifest.json

### Explicit Version Override
```powershell
.\Build-And-Distribute.ps1 -Version "3.14.35" -Force
```
- Uses exact version specified
- `-Force` bypasses git status checks

### Dry Run Testing
```powershell
.\Build-And-Distribute.ps1 -DryRun
```
- Validates environment without making changes
- Perfect for CI/CD pipeline testing

---

## Script Capabilities

### ✅ Automated Version Management
- Auto-increments version across **6 required locations**:
  1. `ZeroTrustMigrationAddin.csproj` (Version, AssemblyVersion, FileVersion)
  2. `README.md` (all version references)
  3. `USER_GUIDE.md` (if exists)
  4. `Views/DashboardWindow.xaml` (UI version display)
  5. `ViewModels/DashboardViewModel.cs` (About dialog)
  6. `CHANGELOG.md` (auto-inserts new entry template)

- Validation with retry to prevent .csproj update failures
- Rollback capability if version update fails

### ✅ Comprehensive Pre-Flight Checks
1. **Required Tools Validation**
   - .NET 8.0 SDK
   - Git
   - gh CLI (if using `-PublishToGitHub`)

2. **Git Repository Status**
   - Warns on uncommitted changes (bypass with `-Force`)
   - Clean working directory validation

3. **Project Configuration**
   - ZeroTrustMigrationAddin.csproj exists
   - Can read current version

4. **Disk Space Check**
   - Verifies sufficient space available
   - Warns if < 1 GB remaining

5. **Distribution Path Validation**
   - Verifies Dropbox folder access

### ✅ Build Process
- Clean → Build → Publish workflow
- Self-contained win-x64 deployment
- .NET 8.0 targeting
- Verification of critical DLLs:
  - `ZeroTrustMigrationAddin.exe`
  - `Azure.Identity.dll`
  - `Microsoft.Graph.dll`
  - And ~275 more files

### ✅ Advanced Packaging
- Creates `ZeroTrustMigrationAddin-vX.X.X-COMPLETE.zip`
- Includes binaries + PowerShell update scripts
- Optimal compression
- Integrity verification
- EXE version validation

### ✅ Manifest Generation
- SHA256 hash for all 278 files
- Critical file marking (EXE, core DLLs)
- Total size calculation
- ISO 8601 timestamps
- Delta size preview (vs previous version)

### ✅ Testing & Verification
- Package integrity checks
- Critical file presence validation
- EXE version number verification
- Post-build smoke test (launches app)
- Can be skipped with `-SkipTests`

### ✅ Distribution Management
- Auto-copy to Dropbox/distribution folder
- Archives previous builds to `builds/archive`
- Package size comparison
- Bandwidth savings calculation (delta updates)

### ✅ GitHub Release Automation (Optional)
- Commits version changes
- Creates and pushes git tag
- Creates GitHub Release
- Uploads ZIP + manifest.json
- Auto-generates release notes template
- Direct release URL output

### ✅ Build Reporting
- Comprehensive build summary
- Build duration tracking
- File locations
- Size statistics
- Next steps checklist
- Full transcript logging

---

## Parameters Reference

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `-Version` | string | (auto) | Explicit version (e.g., "3.14.35"). Overrides auto-increment |
| `-BumpVersion` | string | `Patch` | Which component to increment: `Major`, `Minor`, or `Patch` |
| `-SkipBuild` | switch | false | Skip build/publish, just package existing files |
| `-DistributionPath` | string | `C:\Users\dannygu\Dropbox` | Where to copy final package |
| `-PublishToGitHub` | switch | false | Automatically create GitHub Release |
| `-ReleaseNotes` | string | (auto-generated) | Custom release notes for GitHub |
| `-DryRun` | switch | false | Validate environment without creating files |
| `-Force` | switch | false | Build even with uncommitted git changes |
| `-ArchiveOldBuilds` | switch | true | Move previous builds to archive folder |
| `-SkipTests` | switch | false | Skip post-build smoke tests |

### Parameter Combinations

**Production Release:**
```powershell
.\Build-And-Distribute.ps1 -BumpVersion Minor -PublishToGitHub
```

**Quick Patch:**
```powershell
.\Build-And-Distribute.ps1
```

**Emergency Hotfix:**
```powershell
.\Build-And-Distribute.ps1 -Version "3.14.32.1" -Force -PublishToGitHub -ReleaseNotes "Critical security patch"
```

**CI/CD Validation:**
```powershell
.\Build-And-Distribute.ps1 -DryRun
```

**Re-package without rebuild:**
```powershell
.\Build-And-Distribute.ps1 -SkipBuild -Version "3.14.32"
```

---

## Workflow Stages

### Stage 1: Initialization & Configuration
```
- Parse parameters
- Setup logging
- Display banner
- Initialize paths
```

**Output:** Build log started at `builds/logs/build-YYYYMMDD-HHMMSS.log`

### Stage 2: Pre-Flight Environment Checks
```
1. Validate required tools (dotnet, git, gh)
2. Check git repository status
3. Verify project configuration
4. Check available disk space
5. Validate distribution path
```

**Exit Conditions:**
- Missing required tools → Exit code 1
- Uncommitted changes (without `-Force`) → Exit code 1
- Missing .csproj → Exit code 1
- Dry run → Exit code 0 (success)

### Stage 3: Version Management
```
1. Read current version from .csproj
2. Calculate new version (auto-increment or explicit)
3. Update all 6 version locations:
   - ZeroTrustMigrationAddin.csproj (3 properties)
   - README.md
   - USER_GUIDE.md
   - DashboardWindow.xaml
   - DashboardViewModel.cs
   - CHANGELOG.md (auto-insert entry)
4. Validate updates with retry logic
5. Rollback on failure
```

**Critical Logic:**
```powershell
# Auto-increment logic
switch ($BumpVersion) {
    'Major' { $major++; $minor = 0; $patch = 0 }
    'Minor' { $minor++; $patch = 0 }
    'Patch' { $patch++ }
}
```

**Rollback:** If any update fails, restores original .csproj version

### Stage 4: Build Process
```
1. dotnet clean (Release config)
2. dotnet build (Release config)
3. dotnet publish (win-x64, self-contained)
4. Verify critical files present
5. Generate dependency report
```

**Skipped if:** `-SkipBuild` parameter used

**Output:** `bin\Release\net8.0-windows\win-x64\publish\` (278 files)

### Stage 5: Package Creation
```
1. Copy files to temp folder
   - All publish binaries
   - PowerShell update scripts
   - ZeroTrustMigrationAddin.xml
2. Compress to ZIP (optimal compression)
3. Generate manifest.json:
   - SHA256 hash for each file
   - File sizes and timestamps
   - Critical file marking
4. Calculate delta size (vs previous version)
5. Archive manifest for future deltas
```

**Output:**
- `ZeroTrustMigrationAddin-vX.X.X-COMPLETE.zip` (~88 MB)
- `manifest.json` (~200 KB, 278 entries)
- `builds/manifests/manifest-vX.X.X.json` (archived)

### Stage 6: Verification & Testing
```
1. Extract package to temp folder
2. Verify critical files present
3. Check EXE version number
4. Post-build smoke test (launch app)
5. Clean up temp folders
```

**Exit Conditions:**
- Missing critical files → Exit code 1
- EXE version mismatch → Warning (continues)
- Smoke test failure → Warning (continues)

### Stage 7: Distribution
```
1. Create distribution folder (if needed)
2. Copy package to Dropbox
3. Archive previous build
4. Calculate package size comparison
5. Display bandwidth savings (delta updates)
```

**Output:** Package copied to `$DistributionPath\ZeroTrustMigrationAddin-vX.X.X-COMPLETE.zip`

### Stage 8: GitHub Release (Optional)
```
1. Commit version changes
2. Create git tag (v$newVersion)
3. Push to GitHub (main branch + tags)
4. Create GitHub Release via gh CLI
5. Upload ZIP + manifest.json as assets
6. Use auto-generated or custom release notes
```

**Enabled by:** `-PublishToGitHub` parameter

**Requirements:**
- gh CLI installed and authenticated
- Push access to repository

**Output:** Release URL (https://github.com/sccmavenger/cmaddin/releases/tag/vX.X.X)

### Stage 9: Build Summary
```
1. Display comprehensive build summary
2. Show file locations
3. Calculate build duration
4. Display next steps checklist
5. Stop transcript logging
```

---

## Version Management

### Version Numbering Scheme

Zero Trust Migration Journey Add-in uses **Semantic Versioning (SemVer)**: `MAJOR.MINOR.PATCH`

- **MAJOR** (X.0.0): Breaking changes, major feature overhauls
- **MINOR** (X.Y.0): New features, backward-compatible
- **PATCH** (X.Y.Z): Bug fixes, small improvements

### Version Update Locations

The script updates version in **6 critical locations** to maintain consistency:

#### 1. ZeroTrustMigrationAddin.csproj
```xml
<PropertyGroup>
    <Version>3.14.32</Version>
    <AssemblyVersion>3.14.32.0</AssemblyVersion>
    <FileVersion>3.14.32.0</FileVersion>
</PropertyGroup>
```

**Why:** Build system reads version from project file

**Format:** 
- `Version`: X.Y.Z
- `AssemblyVersion`: X.Y.Z.0
- `FileVersion`: X.Y.Z.0

#### 2. README.md
```markdown
Version 3.14.32
...download v3.14.32...
```

**Why:** Public-facing documentation

**Pattern:** All instances of `Version X.Y.Z` and `vX.Y.Z`

#### 3. USER_GUIDE.md (if exists)
```markdown
Zero Trust Migration Journey v3.14.32
```

**Why:** User documentation consistency

**Pattern:** All version references

#### 4. Views/DashboardWindow.xaml
```xaml
<TextBlock Text="v3.14.32" />
```

**Why:** UI displays current version in title bar

**Pattern:** `vX.Y.Z` in XAML

#### 5. ViewModels/DashboardViewModel.cs
```csharp
private const string AppVersion = "Version: 3.14.32";
```

**Why:** About dialog and logging

**Pattern:** `Version: X.Y.Z`

#### 6. CHANGELOG.md
```markdown
## [3.14.32] - 2024-01-15

### Added
- [Add new features here]
...
```

**Why:** Release history tracking

**Pattern:** Auto-inserts new entry template at top

### Validation & Rollback

The script includes **retry logic** for .csproj updates (known occasional failure):

```powershell
$csproj.Save($csprojPath)
Start-Sleep -Milliseconds 500  # Wait for file system
[xml]$verify = Get-Content $csprojPath
if ($verify.Version -ne $newVersion) {
    throw "Version update failed"
}
```

**Rollback Behavior:**
- If any update fails, restores original .csproj version
- Logs error and exits with code 1
- Prevents partially-updated versions

### Best Practices

1. **Always review CHANGELOG.md** after build - template requires manual editing
2. **Use semantic versioning correctly**:
   - Bug fix? → Patch
   - New feature? → Minor
   - Breaking change? → Major
3. **Test version display** in app UI after build
4. **Verify all 6 locations** if manual version change needed

---

## Manifest System

### Purpose

`manifest.json` enables **delta updates** - clients download only changed files instead of full 88 MB package.

### Structure

```json
{
  "Version": "3.14.32",
  "BuildDate": "2024-01-15T10:30:00.000Z",
  "Files": [
    {
      "RelativePath": "ZeroTrustMigrationAddin.exe",
      "SHA256Hash": "a1b2c3d4...",
      "FileSize": 1234567,
      "LastModified": "2024-01-15T10:25:00.000Z",
      "IsCritical": true
    },
    ...278 entries total...
  ],
  "TotalSize": 239760903
}
```

### Generation Process

```powershell
foreach ($file in $publishFiles) {
    $hash = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLower()
    
    $manifest.Files += @{
        RelativePath = $file.Name
        SHA256Hash = $hash
        FileSize = $file.Length
        LastModified = $file.LastWriteTimeUtc.ToString("o")
        IsCritical = ($criticalFiles -contains $file.Name)
    }
}
```

### Critical Files

Marked with `"IsCritical": true` for priority delta download:

- `ZeroTrustMigrationAddin.exe`
- `ZeroTrustMigrationAddin.dll`
- `Azure.Identity.dll`
- `Microsoft.Graph.dll`
- `Microsoft.Graph.Core.dll`
- `Newtonsoft.Json.dll`

### Delta Calculation

The script compares new manifest with previous version:

```powershell
$changedFiles = $manifest.Files | Where-Object {
    $newFile = $_
    $oldFile = $oldManifest.Files | Where-Object { 
        $_.RelativePath -eq $newFile.RelativePath 
    }
    (!$oldFile) -or ($oldFile.SHA256Hash -ne $newFile.SHA256Hash)
}
```

**Example Output:**
```
Delta Analysis (vs v3.14.31):
  Changed files: 12
  Delta download: ~15.2 MB (vs full 87.89 MB)
  Bandwidth savings: 82.7%
```

### Manifest Archiving

Each build archives its manifest to `builds/manifests/manifest-vX.X.X.json` for future delta calculations.

**Storage:** Retained indefinitely (manifests are small, ~200 KB each)

---

## GitHub Integration

### Requirements

1. **gh CLI installed**
   ```powershell
   winget install GitHub.cli
   ```

2. **Authentication**
   ```powershell
   gh auth login
   ```

3. **Push access** to `sccmavenger/cmaddin` repository

### Workflow

When `-PublishToGitHub` is used:

```
1. git add .
2. git commit -m "Release vX.X.X - Auto-increment version"
3. git tag -a "vX.X.X" -m "Version X.X.X"
4. git push origin main --tags
5. gh release create vX.X.X <assets> --title "..." --notes "..."
```

### Release Assets

Two files are uploaded:

1. **ZeroTrustMigrationAddin-vX.X.X-COMPLETE.zip** - Full package
2. **manifest.json** - Update manifest for delta downloads

### Release Notes

**Auto-generated template** (if `-ReleaseNotes` not provided):

```markdown
## Zero Trust Migration Journey Add-in vX.X.X

### Changes
- See CHANGELOG.md for detailed changes

### Installation
1. Download ZeroTrustMigrationAddin-vX.X.X-COMPLETE.zip
2. Extract all files to installation directory
3. Run ZeroTrustMigrationAddin.exe

### Auto-Update
Existing users on vX.Y.Z will receive automatic update prompt.

---
Build Date: 2024-01-15 10:30 UTC
Package Size: 87.89 MB
```

**Custom notes:**
```powershell
.\Build-And-Distribute.ps1 -PublishToGitHub -ReleaseNotes "Critical security fixes:
- Fixed vulnerability CVE-2024-1234
- Updated Azure.Identity to 1.12.0
- Improved error handling in auth flow"
```

### Integration with Auto-Update System

Once GitHub Release is created with both assets:

1. `Services/GitHubUpdateService.cs` detects new release via Octokit
2. `GetLatestReleaseAsync()` returns release info
3. `DeltaUpdateService.cs` downloads `manifest.json`
4. Compares local vs remote file hashes
5. Downloads only changed files from ZIP
6. Applies update via `UpdateApplier.cs`

**Critical:** Release MUST have both ZIP and manifest.json or `GetLatest()` returns null!

### Error Handling

```powershell
try {
    & gh release create vX.X.X ...
    if ($LASTEXITCODE -eq 0) {
        Write-Host "GitHub Release created successfully"
    } else {
        Write-Host "Release failed (exit code: $LASTEXITCODE)"
    }
} catch {
    Write-Host "Release creation failed: $_"
}
```

Failures in GitHub publish do **not** fail entire build - package still created locally.

---

## Error Handling & Rollback

### Error Strategies

#### 1. Pre-Flight Failures (Exit Immediately)
```
- Missing tools → Exit 1
- Git dirty (no -Force) → Exit 1
- Missing .csproj → Exit 1
```

**Rationale:** No changes made yet, safe to exit

#### 2. Version Update Failures (Rollback)
```powershell
try {
    # Update all 6 locations
} catch {
    Write-Host "Rolling back changes..."
    $csproj.Version = $oldVersion
    $csproj.Save($csprojPath)
    Stop-Transcript
    exit 1
}
```

**Rationale:** Partial version updates would cause inconsistency

#### 3. Build Failures (Exit with Status)
```powershell
dotnet build ...
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!"
    Stop-Transcript
    exit 1
}
```

**Rationale:** Cannot continue without successful build

#### 4. Package/Distribution Failures (Warning + Continue)
```powershell
try {
    Copy-Item $package $destination
} catch {
    Write-Host "Copy failed: $_"
    Write-Host "Package available at: $packagePath"
}
```

**Rationale:** Package still created locally, user can manually copy

#### 5. GitHub Publish Failures (Warning Only)
```powershell
try {
    & gh release create ...
} catch {
    Write-Host "GitHub publish failed: $_"
    # Continue - package created successfully
}
```

**Rationale:** Build succeeded, user can manually create release

### Logging

All output captured in transcript:

```
builds/logs/build-YYYYMMDD-HHMMSS.log
```

**Contents:**
- All console output (verbose)
- PowerShell command execution
- Error messages and stack traces
- Build duration and summary

**Retention:** Keep last 30 days, manually clean older logs

### Recovery Procedures

#### Version Update Failed
```powershell
# Manual rollback
git checkout ZeroTrustMigrationAddin.csproj README.md # ...all changed files
git status  # Verify rollback
```

#### Build Failed with Updated Version
```powershell
# Option 1: Fix build error and re-run
# (version already updated, will skip version bump)

# Option 2: Rollback version manually
git checkout .
git clean -fd
```

#### Package Created but Distribution Failed
```powershell
# Manually copy package
Copy-Item "ZeroTrustMigrationAddin-vX.X.X-COMPLETE.zip" "C:\Users\dannygu\Dropbox"
Copy-Item "manifest.json" "C:\Users\dannygu\Dropbox"
```

#### GitHub Publish Failed
```powershell
# Manual release creation
gh release create vX.X.X `
    ZeroTrustMigrationAddin-vX.X.X-COMPLETE.zip `
    manifest.json `
    --title "Zero Trust Migration Journey Add-in vX.X.X" `
    --notes-file release-notes.md
```

---

## Troubleshooting

### Common Issues

#### Issue: "Version update failed (got: X.Y.Z, expected: X.Y.Z)"
**Cause:** .csproj XML save occasionally doesn't flush immediately

**Solution:** Script includes retry logic, but if persists:
```powershell
# Run with explicit version
.\Build-And-Distribute.ps1 -Version "3.14.32" -SkipBuild
```

#### Issue: "Missing required tools (gh) not found"
**Cause:** GitHub CLI not installed or not in PATH

**Solution:**
```powershell
winget install GitHub.cli
# Restart PowerShell
gh auth login
```

#### Issue: "Uncommitted changes detected"
**Cause:** Working directory has modified files

**Solution:**
```powershell
# Option 1: Commit changes
git add .
git commit -m "Prepare for release"

# Option 2: Force build anyway
.\Build-And-Distribute.ps1 -Force
```

#### Issue: "Package integrity check failed"
**Cause:** Critical file missing from build output

**Solution:**
```powershell
# Clean and rebuild
dotnet clean -c Release
Remove-Item bin\Release -Recurse -Force
.\Build-And-Distribute.ps1
```

#### Issue: "GitHub Release creation failed (exit code: 1)"
**Cause:** Tag already exists or network error

**Solution:**
```powershell
# Delete existing tag
git tag -d vX.X.X
git push origin :refs/tags/vX.X.X

# Try again
.\Build-And-Distribute.ps1 -PublishToGitHub -SkipBuild
```

#### Issue: "EXE version mismatch (Expected: X.Y.Z.0, Found: X.Y.Z.0)"
**Cause:** Version property in .csproj not propagated to EXE

**Solution:**
```powershell
# Rebuild from clean state
dotnet clean
Remove-Item bin -Recurse -Force
Remove-Item obj -Recurse -Force
.\Build-And-Distribute.ps1
```

### Debug Mode

For detailed debugging:

```powershell
$VerbosePreference = "Continue"
$DebugPreference = "Continue"
.\Build-And-Distribute.ps1 -Verbose -Debug
```

View transcript log:
```powershell
Get-Content "builds\logs\build-*.log" -Tail 100
```

---

## Advanced Usage

### CI/CD Integration

**Azure Pipelines:**
```yaml
steps:
- task: PowerShell@2
  inputs:
    filePath: 'Build-And-Distribute.ps1'
    arguments: '-DryRun'
  displayName: 'Validate Build'

- task: PowerShell@2
  inputs:
    filePath: 'Build-And-Distribute.ps1'
    arguments: '-PublishToGitHub -Force'
  displayName: 'Build and Release'
  condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))
```

**GitHub Actions:**
```yaml
- name: Build and Publish
  shell: pwsh
  run: |
    .\Build-And-Distribute.ps1 -PublishToGitHub
  env:
    GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

### Scheduled Builds

**Windows Task Scheduler:**
```powershell
$trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Friday -At 6PM
$action = New-ScheduledTaskAction -Execute "powershell.exe" `
    -Argument "-File C:\path\to\Build-And-Distribute.ps1 -PublishToGitHub"
Register-ScheduledTask -TaskName "CloudJourney_WeeklyBuild" `
    -Trigger $trigger -Action $action
```

### Multi-Version Management

**Build multiple versions:**
```powershell
# Release v3.14.32 (production)
.\Build-And-Distribute.ps1 -Version "3.14.32" -PublishToGitHub

# Build v3.15.0-beta (preview)
.\Build-And-Distribute.ps1 -Version "3.15.0" -DistributionPath "C:\Preview"
```

### Custom Distribution

**Multiple distribution targets:**
```powershell
$destinations = @(
    "\\fileserver\software\CloudJourney",
    "C:\Users\dannygu\Dropbox",
    "\\testserver\deployments"
)

foreach ($dest in $destinations) {
    .\Build-And-Distribute.ps1 -SkipBuild -DistributionPath $dest
}
```

---

## Development Guidelines

### Modifying the Script

#### Adding New Version Update Location

```powershell
# 7. NewFile.config
Write-Host "   [7/7] NewFile.config" -ForegroundColor White
$configPath = Join-Path $scriptDir "NewFile.config"
if (Test-Path $configPath) {
    $content = Get-Content $configPath -Raw
    $content = $content -replace "OldVersion=$oldVersion", "OldVersion=$newVersion"
    [System.IO.File]::WriteAllText($configPath, $content)
    Write-Host "      ✅ Updated" -ForegroundColor Green
} else {
    Write-Host "      ⚠️ Not found" -ForegroundColor Yellow
}
```

**Don't forget:** Update count in progress messages ([7/7] not [6/6])

#### Adding New Pre-Flight Check

```powershell
Write-Host "[CHECK 6/6] New Validation" -ForegroundColor Yellow
# Your validation logic here
if ($validationFailed) {
    Write-Host "   ❌ Validation failed!" -ForegroundColor Red
    Stop-Transcript
    exit 1
}
Write-Host "   ✅ Validation passed" -ForegroundColor Green
Write-Host ""
```

#### Adding New Parameter

```powershell
param(
    # Existing parameters...
    [string]$NewParameter = "default value"
)

# Document in .SYNOPSIS and .PARAMETER blocks
# Add to Parameters Reference in this guide
```

### Testing Changes

1. **Always test with `-DryRun` first**
   ```powershell
   .\Build-And-Distribute-Modified.ps1 -DryRun
   ```

2. **Test with non-production version**
   ```powershell
   .\Build-And-Distribute-Modified.ps1 -Version "99.99.99"
   ```

3. **Verify all 6 version locations manually**
   ```powershell
   Select-String -Path "ZeroTrustMigrationAddin.csproj","README.md" -Pattern "99.99.99"
   ```

4. **Test rollback by forcing error**
   ```powershell
   # Temporarily add: throw "Test error" after version update
   ```

### Code Style Guidelines

- **Color scheme:**
  - `Magenta` - Section headers
  - `Yellow` - Progress/warnings
  - `Green` - Success
  - `Red` - Errors
  - `Cyan` - Information
  - `Gray/DarkGray` - Verbose details

- **Progress indicators:**
  ```
  [STEP X/Y] Description
     ✅ Success message
     ⚠️ Warning message
     ❌ Error message
     ⏳ Processing...
  ```

- **Error handling pattern:**
  ```powershell
  try {
      # Operation
      Write-Host "   ✅ Success" -ForegroundColor Green
  } catch {
      Write-Host "   ❌ Failed: $_" -ForegroundColor Red
      # Rollback if necessary
      Stop-Transcript
      exit 1
  }
  ```

---

## Version History

### v2.0.0 (2024-01-15)
- ✅ Complete rewrite with all recommended improvements
- ✅ Added pre-flight environment validation
- ✅ Fixed .csproj update validation with retry logic
- ✅ Added GitHub automation (-PublishToGitHub)
- ✅ Added delta size preview
- ✅ Added dry-run mode
- ✅ Automatic CHANGELOG.md entry creation
- ✅ Release notes auto-generation
- ✅ Post-build smoke testing
- ✅ Package size comparison
- ✅ Build performance tracking
- ✅ Build archiving
- ✅ Dependency version reporting
- ✅ Comprehensive logging
- ✅ Complete parameter documentation
- ✅ Error rollback capability

### v1.0.0 (Previous)
- Basic version increment
- Build and publish
- Manual packaging
- Manual GitHub release

---

## Quick Reference Card

```
╔═══════════════════════════════════════════════════════════════╗
║         Zero Trust Migration Journey BUILD SCRIPT QUICK REFERENCE            ║
╠═══════════════════════════════════════════════════════════════╣
║ COMMON TASKS:                                                 ║
║   Patch build:       .\Build-And-Distribute.ps1              ║
║   Minor release:     -BumpVersion Minor                       ║
║   Major release:     -BumpVersion Major -PublishToGitHub      ║
║   Explicit version:  -Version "X.Y.Z"                         ║
║   Test build:        -DryRun                                  ║
║   Force dirty:       -Force                                   ║
║                                                               ║
║ OUTPUTS:                                                      ║
║   Package:    ZeroTrustMigrationAddin-vX.X.X-COMPLETE.zip (~88 MB) ║
║   Manifest:   manifest.json (~200 KB, 278 files)             ║
║   Log:        builds/logs/build-YYYYMMDD-HHMMSS.log          ║
║                                                               ║
║ VERSION LOCATIONS (6):                                        ║
║   1. ZeroTrustMigrationAddin.csproj                                ║
║   2. README.md                                               ║
║   3. USER_GUIDE.md                                           ║
║   4. Views/DashboardWindow.xaml                              ║
║   5. ViewModels/DashboardViewModel.cs                        ║
║   6. CHANGELOG.md                                            ║
║                                                               ║
║ REQUIREMENTS:                                                 ║
║   • PowerShell 5.1+                                          ║
║   • .NET 8.0 SDK                                             ║
║   • Git                                                      ║
║   • gh CLI (for -PublishToGitHub)                           ║
╚═══════════════════════════════════════════════════════════════╝
```

---

## Support & Contact

**Documentation:** This file (BUILD_SCRIPT_GUIDE.md)  
**Build Logs:** `builds/logs/`  
**Script Version:** 2.0.0  
**Last Updated:** 2024-01-15

---

**END OF DOCUMENTATION**
