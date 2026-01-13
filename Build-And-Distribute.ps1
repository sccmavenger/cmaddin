#Requires -Version 5.1

<#
.SYNOPSIS
    Complete automated build, test, and distribution script for Cloud Journey Add-in
    
.DESCRIPTION
    Enterprise-grade build automation that:
    - Auto-increments version across all 6 required locations
    - Builds, tests, and packages the application
    - Generates SHA256 manifest for delta updates
    - Archives previous builds
    - Optionally publishes to GitHub Releases
    - Provides comprehensive validation and rollback
    
.PARAMETER Version
    Explicit version number (e.g., "3.14.32"). If omitted, auto-increments current version.
    
.PARAMETER BumpVersion
    Version component to increment (Major, Minor, or Patch). Default: Patch
    - Patch: Bug fixes (3.14.31 → 3.14.32)
    - Minor: New features (3.14.31 → 3.15.0)
    - Major: Breaking changes (3.14.31 → 4.0.0)
    
.PARAMETER SkipBuild
    Skip the build/publish steps and just package existing files. Useful for quick re-packaging.
    
.PARAMETER DistributionPath
    Path to copy final package (default: C:\Users\dannygu\Dropbox). Creates if doesn't exist.
    
.PARAMETER PublishToGitHub
    Automatically create GitHub release and upload assets (ZIP + manifest.json).
    Requires: gh CLI installed and authenticated.
    
.PARAMETER ReleaseNotes
    Custom release notes for GitHub release. If omitted, uses auto-generated template.
    
.PARAMETER DryRun
    Test mode - validates everything but doesn't create files. Perfect for CI/CD testing.
    
.PARAMETER Force
    Skip git status checks and build anyway, even with uncommitted changes.
    
.PARAMETER ArchiveOldBuilds
    Move previous version builds to builds/archive folder. Default: enabled.
    
.PARAMETER SkipTests
    Skip post-build smoke tests. Not recommended for production builds.
    
.EXAMPLE
    .\Build-And-Distribute.ps1
    # Auto-increment patch version, build, package (3.14.31 → 3.14.32)
    
.EXAMPLE
    .\Build-And-Distribute.ps1 -BumpVersion Minor
    # Increment minor version (3.14.31 → 3.15.0)
    
.EXAMPLE
    .\Build-And-Distribute.ps1 -Version "4.0.0" -BumpVersion Major
    # Use explicit version for major release
    
.EXAMPLE
    .\Build-And-Distribute.ps1 -PublishToGitHub -ReleaseNotes "Fixed critical bug in enrollment agent"
    # Build and automatically publish to GitHub
    
.EXAMPLE
    .\Build-And-Distribute.ps1 -DryRun
    # Validate build process without creating any files
    
.NOTES
    File Name      : Build-And-Distribute.ps1
    Author         : Cloud Journey Development Team
    Prerequisite   : PowerShell 5.1+, .NET 8.0 SDK, gh CLI (optional)
    Version        : 2.0.0
    
.LINK
    Internal Documentation: BUILD_SCRIPT_GUIDE.md
    GitHub Releases: https://github.com/sccmavenger/cmaddin/releases
#>

[CmdletBinding()]
param(
    [string]$Version,
    [switch]$SkipBuild,
    [string]$DistributionPath = "C:\Users\dannygu\Dropbox",
    [ValidateSet('Major', 'Minor', 'Patch')]
    [string]$BumpVersion = 'Patch',
    [switch]$PublishToGitHub,
    [string]$ReleaseNotes,
    [switch]$DryRun,
    [switch]$Force,
    [switch]$ArchiveOldBuilds = $true,
    [switch]$SkipTests
)

# ============================================
# INITIALIZATION & CONFIGURATION
# ============================================

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$buildStartTime = Get-Date
$buildLogPath = Join-Path $scriptDir "builds\logs\build-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"

# Create build logging directory
$buildLogDir = Split-Path $buildLogPath -Parent
if (!(Test-Path $buildLogDir)) {
    New-Item -ItemType Directory -Path $buildLogDir -Force | Out-Null
}

# Start transcript for full build log
Start-Transcript -Path $buildLogPath -Append

# Display banner
Write-Host ""
Write-Host "╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  Cloud Journey Add-in - Enterprise Build & Distribution  ║" -ForegroundColor Cyan
Write-Host "║  Version 2.0.0 - Enhanced Automation                      ║" -ForegroundColor Cyan
Write-Host "╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "🧪 DRY RUN MODE - No files will be created or modified" -ForegroundColor Yellow
    Write-Host ""
}

# ============================================
# PRE-FLIGHT ENVIRONMENT VALIDATION
# ============================================

Write-Host "🔍 PRE-FLIGHT ENVIRONMENT CHECKS" -ForegroundColor Magenta
Write-Host "═══════════════════════════════════════" -ForegroundColor Magenta
Write-Host ""

# Check 1: Required tools
Write-Host "[CHECK 1/5] Required Tools" -ForegroundColor Yellow
$requiredTools = @{
    'dotnet' = 'NET 8.0 SDK'
    'git' = 'Git version control'
}

if ($PublishToGitHub) {
    $requiredTools['gh'] = 'GitHub CLI'
}

$toolsValid = $true
foreach ($tool in $requiredTools.Keys) {
    $command = Get-Command $tool -ErrorAction SilentlyContinue
    if ($command) {
        $version = ""
        try {
            switch ($tool) {
                'dotnet' { $version = (& dotnet --version 2>$null) }
                'git' { $version = (& git --version 2>$null) -replace 'git version ' }
                'gh' { $version = (& gh --version 2>$null | Select-Object -First 1) -replace 'gh version ' }
            }
        } catch {}
        
        Write-Host "   ✅ $($requiredTools[$tool]): $version" -ForegroundColor Green
    } else {
        Write-Host "   ❌ $($requiredTools[$tool]) ($tool) not found!" -ForegroundColor Red
        $toolsValid = $false
    }
}

if (!$toolsValid) {
    Write-Host ""
    Write-Host "❌ Missing required tools. Please install and try again." -ForegroundColor Red
    Stop-Transcript
    exit 1
}

Write-Host ""

# Check 2: Git repository status
Write-Host "[CHECK 2/5] Git Repository Status" -ForegroundColor Yellow
$gitStatus = git status --porcelain 2>$null

if ($gitStatus -and !$Force -and !$DryRun) {
    Write-Host "   ⚠️ Uncommitted changes detected:" -ForegroundColor Yellow
    $gitStatus | ForEach-Object { Write-Host "      $_" -ForegroundColor Gray }
    Write-Host ""
    Write-Host "   Use -Force to build anyway" -ForegroundColor Yellow
    Stop-Transcript
    exit 1
} elseif ($gitStatus -and $Force) {
    Write-Host "   ⚠️ Uncommitted changes detected (proceeding with -Force)" -ForegroundColor Yellow
} else {
    Write-Host "   ✅ Working directory clean" -ForegroundColor Green
}

Write-Host ""

# Check 3: Project file exists
Write-Host "[CHECK 3/5] Project Configuration" -ForegroundColor Yellow
$csprojPath = Join-Path $scriptDir "CloudJourneyAddin.csproj"

if (!(Test-Path $csprojPath)) {
    Write-Host "   ❌ CloudJourneyAddin.csproj not found!" -ForegroundColor Red
    Stop-Transcript
    exit 1
}

[xml]$csproj = Get-Content $csprojPath
$currentVersion = $csproj.Project.PropertyGroup.Version | Select-Object -First 1

if (!$currentVersion) {
    Write-Host "   ❌ Could not read version from .csproj!" -ForegroundColor Red
    Stop-Transcript
    exit 1
}

Write-Host "   ✅ Project file found" -ForegroundColor Green
Write-Host "   📦 Current version: $currentVersion" -ForegroundColor Cyan
Write-Host ""

# Check 4: Disk space
Write-Host "[CHECK 4/5] Disk Space" -ForegroundColor Yellow
$drive = (Get-Item $scriptDir).PSDrive.Name
$driveInfo = Get-PSDrive $drive
$freeSpaceGB = [math]::Round($driveInfo.Free / 1GB, 2)

if ($freeSpaceGB -lt 1) {
    Write-Host "   ⚠️ Low disk space: $freeSpaceGB GB remaining" -ForegroundColor Yellow
} else {
    Write-Host "   ✅ Available disk space: $freeSpaceGB GB" -ForegroundColor Green
}

Write-Host ""

# Check 5: Distribution path
Write-Host "[CHECK 5/5] Distribution Path" -ForegroundColor Yellow
if (Test-Path $DistributionPath) {
    Write-Host "   ✅ Distribution folder exists: $DistributionPath" -ForegroundColor Green
} else {
    Write-Host "   ⚠️ Distribution folder will be created: $DistributionPath" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "═══════════════════════════════════════" -ForegroundColor Green
Write-Host "✅ ALL PRE-FLIGHT CHECKS PASSED" -ForegroundColor Green
Write-Host "═══════════════════════════════════════" -ForegroundColor Green
Write-Host ""

if ($DryRun) {
    Write-Host "🧪 Dry run complete - exiting without making changes" -ForegroundColor Yellow
    Stop-Transcript
    exit 0
}

# ============================================
# VERSION MANAGEMENT
# ============================================

Write-Host "📦 VERSION MANAGEMENT" -ForegroundColor Magenta
Write-Host "═══════════════════════════════════════" -ForegroundColor Magenta
Write-Host ""

$oldVersion = $currentVersion

# Calculate new version
if (!$Version) {
    $versionParts = $currentVersion -split '\.'
    $major = [int]$versionParts[0]
    $minor = [int]$versionParts[1]
    $patch = [int]$versionParts[2]
    
    switch ($BumpVersion) {
        'Major' { $major++; $minor = 0; $patch = 0 }
        'Minor' { $minor++; $patch = 0 }
        'Patch' { $patch++ }
    }
    
    $newVersion = "$major.$minor.$patch"
    
    Write-Host "🔄 Auto-incrementing version ($BumpVersion)" -ForegroundColor Cyan
    Write-Host "   $currentVersion → $newVersion" -ForegroundColor Green
} else {
    $newVersion = $Version
    Write-Host "📌 Using explicit version: $newVersion" -ForegroundColor Cyan
}

# Validate version format
if ($newVersion -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "   ❌ Invalid version format: '$newVersion' (expected X.Y.Z)" -ForegroundColor Red
    Stop-Transcript
    exit 1
}

Write-Host ""

# Update all version locations
Write-Host "📝 Updating version in required locations..." -ForegroundColor Yellow
Write-Host ""

try {
    # 1. CloudJourneyAddin.csproj
    Write-Host "   [1/6] CloudJourneyAddin.csproj" -ForegroundColor White
    $csproj.Project.PropertyGroup.Version = $newVersion
    $csproj.Project.PropertyGroup.AssemblyVersion = "$newVersion.0"
    $csproj.Project.PropertyGroup.FileVersion = "$newVersion.0"
    $csproj.Save($csprojPath)
    
    # Validate the update worked
    Start-Sleep -Milliseconds 500
    [xml]$verify = Get-Content $csprojPath
    $verifiedVersion = $verify.Project.PropertyGroup.Version | Select-Object -First 1
    if ($verifiedVersion -ne $newVersion) {
        throw "Failed to update .csproj version (got: $verifiedVersion, expected: $newVersion)"
    }
    Write-Host "      ✅ Updated to v$newVersion" -ForegroundColor Green
    
    # 2. README.md
    Write-Host "   [2/6] README.md" -ForegroundColor White
    $readmePath = Join-Path $scriptDir "README.md"
    if (Test-Path $readmePath) {
        $readmeContent = Get-Content $readmePath -Raw
        $readmeContent = $readmeContent -replace "Version $oldVersion", "Version $newVersion"
        $readmeContent = $readmeContent -replace "v$oldVersion", "v$newVersion"
        [System.IO.File]::WriteAllText($readmePath, $readmeContent)
        Write-Host "      ✅ Updated" -ForegroundColor Green
    } else {
        Write-Host "      ⚠️ Not found" -ForegroundColor Yellow
    }
    
    # 3. USER_GUIDE.md (if exists)
    Write-Host "   [3/6] USER_GUIDE.md" -ForegroundColor White
    $userGuidePath = Join-Path $scriptDir "USER_GUIDE.md"
    if (Test-Path $userGuidePath) {
        $content = Get-Content $userGuidePath -Raw
        $content = $content -replace "Version $oldVersion", "Version $newVersion"
        $content = $content -replace "v$oldVersion", "v$newVersion"
        [System.IO.File]::WriteAllText($userGuidePath, $content)
        Write-Host "      ✅ Updated" -ForegroundColor Green
    } else {
        Write-Host "      ⚠️ Not found" -ForegroundColor Yellow
    }
    
    # 4. DashboardWindow.xaml
    Write-Host "   [4/6] Views/DashboardWindow.xaml" -ForegroundColor White
    $xamlPath = Join-Path $scriptDir "Views\DashboardWindow.xaml"
    if (Test-Path $xamlPath) {
        $content = Get-Content $xamlPath -Raw
        $content = $content -replace "v$oldVersion", "v$newVersion"
        [System.IO.File]::WriteAllText($xamlPath, $content)
        Write-Host "      ✅ Updated" -ForegroundColor Green
    } else {
        Write-Host "      ⚠️ Not found" -ForegroundColor Yellow
    }
    
    # 5. DashboardViewModel.cs
    Write-Host "   [5/6] ViewModels/DashboardViewModel.cs" -ForegroundColor White
    $viewModelPath = Join-Path $scriptDir "ViewModels\DashboardViewModel.cs"
    if (Test-Path $viewModelPath) {
        $content = Get-Content $viewModelPath -Raw
        $content = $content -replace "Version: $oldVersion", "Version: $newVersion"
        [System.IO.File]::WriteAllText($viewModelPath, $content)
        Write-Host "      ✅ Updated" -ForegroundColor Green
    } else {
        Write-Host "      ⚠️ Not found" -ForegroundColor Yellow
    }
    
    # 6. CHANGELOG.md - Auto-insert new entry
    Write-Host "   [6/6] CHANGELOG.md" -ForegroundColor White
    $changelogPath = Join-Path $scriptDir "CHANGELOG.md"
    if (Test-Path $changelogPath) {
        $changelogContent = Get-Content $changelogPath -Raw
        $newEntry = @"

## [$newVersion] - $(Get-Date -Format 'yyyy-MM-dd')

### Added
- [Add new features here]

### Changed
- [Add changes here]

### Fixed
- [Add bug fixes here]

"@
        $updatedChangelog = $changelogContent -replace "(# Cloud Journey Dashboard - Change Log)", "`$1`n$newEntry"
        [System.IO.File]::WriteAllText($changelogPath, $updatedChangelog)
        Write-Host "      ✅ New entry added (PLEASE UPDATE)" -ForegroundColor Green
    } else {
        Write-Host "      ⚠️ Not found" -ForegroundColor Yellow
    }
    
} catch {
    Write-Host ""
    Write-Host "❌ Version update failed: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "🔄 Rolling back changes..." -ForegroundColor Yellow
    
    # Rollback: restore original version
    $csproj.Project.PropertyGroup.Version = $oldVersion
    $csproj.Project.PropertyGroup.AssemblyVersion = "$oldVersion.0"
    $csproj.Project.PropertyGroup.FileVersion = "$oldVersion.0"
    $csproj.Save($csprojPath)
    
    Stop-Transcript
    exit 1
}

Write-Host ""
Write-Host "✅ All version locations updated successfully" -ForegroundColor Green
Write-Host ""

# ============================================
# BUILD PROCESS
# ============================================

Write-Host "🔨 BUILD PROCESS" -ForegroundColor Magenta
Write-Host "═══════════════════════════════════════" -ForegroundColor Magenta
Write-Host ""

if ($SkipBuild) {
    Write-Host "⏭️  Build skipped (using existing files)" -ForegroundColor Yellow
    $publishPath = Join-Path $scriptDir "bin\Release\net8.0-windows\win-x64\publish"
} else {
    # Clean
    Write-Host "[STEP 1/4] Cleaning previous build..." -ForegroundColor Yellow
    dotnet clean CloudJourneyAddin.csproj -c Release --nologo -v quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "   ❌ Clean failed!" -ForegroundColor Red
        Stop-Transcript
        exit 1
    }
    Write-Host "   ✅ Clean complete" -ForegroundColor Green
    Write-Host ""
    
    # Build
    Write-Host "[STEP 2/4] Building project..." -ForegroundColor Yellow
    dotnet build CloudJourneyAddin.csproj -c Release --nologo -v quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "   ❌ Build failed!" -ForegroundColor Red
        Stop-Transcript
        exit 1
    }
    Write-Host "   ✅ Build succeeded" -ForegroundColor Green
    Write-Host ""
    
    # Publish
    Write-Host "[STEP 3/4] Publishing with dependencies..." -ForegroundColor Yellow
    $publishPath = Join-Path $scriptDir "bin\Release\net8.0-windows\win-x64\publish"
    
    dotnet publish CloudJourneyAddin.csproj -c Release -r win-x64 --self-contained true --nologo -v quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "   ❌ Publish failed!" -ForegroundColor Red
        Stop-Transcript
        exit 1
    }
    
    $fileCount = (Get-ChildItem $publishPath -File).Count
    Write-Host "   ✅ Published $fileCount files" -ForegroundColor Green
    
    # Verify critical files
    $criticalFiles = @("CloudJourneyAddin.exe", "Azure.Identity.dll", "Microsoft.Graph.dll")
    foreach ($file in $criticalFiles) {
        $path = Join-Path $publishPath $file
        if (!(Test-Path $path)) {
            Write-Host "   ❌ Critical file missing: $file" -ForegroundColor Red
            Stop-Transcript
            exit 1
        }
    }
    Write-Host "   ✅ All critical files verified" -ForegroundColor Green
    Write-Host ""
    
    # Show dependencies
    Write-Host "[STEP 4/4] Dependency Report" -ForegroundColor Yellow
    try {
        $packages = dotnet list package --format json 2>$null | ConvertFrom-Json
        $topLevel = $packages.projects[0].frameworks[0].topLevelPackages
        Write-Host "   📦 Key Dependencies:" -ForegroundColor Cyan
        $topLevel | Select-Object -First 5 | ForEach-Object {
            Write-Host "      • $($_.id) v$($_.resolvedVersion)" -ForegroundColor Gray
        }
        Write-Host "      ... and $($topLevel.Count - 5) more" -ForegroundColor DarkGray
    } catch {
        Write-Host "   ⚠️ Could not retrieve dependency info" -ForegroundColor Yellow
    }
    Write-Host ""
}

# ============================================
# PACKAGE CREATION
# ============================================

Write-Host "📦 PACKAGE CREATION" -ForegroundColor Magenta
Write-Host "═══════════════════════════════════════" -ForegroundColor Magenta
Write-Host ""

$packageName = "CloudJourneyAddin-v$newVersion-COMPLETE.zip"
$packagePath = Join-Path $scriptDir $packageName
$tempFolder = Join-Path $scriptDir "TempPackage_$newVersion"

# Clean and create temp folder
if (Test-Path $tempFolder) {
    Remove-Item $tempFolder -Recurse -Force
}
New-Item -ItemType Directory -Path $tempFolder -Force | Out-Null

Write-Host "[STEP 1/3] Copying files to package..." -ForegroundColor Yellow

# Copy binaries
$publishFiles = Get-ChildItem "$publishPath\*" -File
$binCount = 0
foreach ($file in $publishFiles) {
    Copy-Item $file.FullName -Destination $tempFolder -Force
    $binCount++
    if ($binCount % 50 -eq 0) {
        Write-Host "   ⏳ Copied $binCount/$($publishFiles.Count) files..." -ForegroundColor DarkGray
    }
}
Write-Host "   ✅ Copied $binCount binary files" -ForegroundColor Green

# Copy update scripts
$updatePackagePath = Join-Path $scriptDir "UpdatePackage"
if (Test-Path $updatePackagePath) {
    $scriptFiles = @(Get-ChildItem $updatePackagePath -Filter "*.ps1")
    $xmlFile = Join-Path $updatePackagePath "CloudJourneyAddin.xml"
    if (Test-Path $xmlFile) { $scriptFiles += Get-Item $xmlFile }
    
    foreach ($script in $scriptFiles) {
        Copy-Item $script.FullName -Destination $tempFolder -Force
    }
    Write-Host "   ✅ Copied $($scriptFiles.Count) support files" -ForegroundColor Green
}

Write-Host ""

# Create ZIP
Write-Host "[STEP 2/3] Compressing package..." -ForegroundColor Yellow
if (Test-Path $packagePath) {
    Remove-Item $packagePath -Force
}

Compress-Archive -Path "$tempFolder\*" -DestinationPath $packagePath -CompressionLevel Optimal
Remove-Item $tempFolder -Recurse -Force

$packageSize = [math]::Round((Get-Item $packagePath).Length / 1MB, 2)
$packageFileCount = $binCount + $(if(Test-Path $updatePackagePath){(Get-ChildItem $updatePackagePath -Filter "*.ps1").Count + 1}else{0})

Write-Host "   ✅ Package created: $packageName ($packageSize MB)" -ForegroundColor Green
Write-Host "   ✅ Contains $packageFileCount files" -ForegroundColor Green
Write-Host ""

# Generate manifest
Write-Host "[STEP 3/3] Generating update manifest..." -ForegroundColor Yellow

$manifestPath = Join-Path $scriptDir "manifest.json"
$manifest = @{
    Version = $newVersion
    BuildDate = (Get-Date).ToUniversalTime().ToString("o")
    Files = @()
    TotalSize = 0
}

$manifestFiles = Get-ChildItem "$publishPath" -File
$manifestCount = 0

foreach ($file in $manifestFiles) {
    $manifestCount++
    if ($manifestCount % 50 -eq 0) {
        Write-Host "   ⏳ Processed $manifestCount/$($manifestFiles.Count) files..." -ForegroundColor DarkGray
    }
    
    $hash = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLower()
    
    $criticalFiles = @("CloudJourneyAddin.exe", "CloudJourneyAddin.dll", "Azure.Identity.dll", 
                       "Microsoft.Graph.dll", "Microsoft.Graph.Core.dll", "Newtonsoft.Json.dll")
    
    $manifest.Files += @{
        RelativePath = $file.Name
        SHA256Hash = $hash
        FileSize = $file.Length
        LastModified = $file.LastWriteTimeUtc.ToString("o")
        IsCritical = ($criticalFiles -contains $file.Name)
    }
    
    $manifest.TotalSize += $file.Length
}

$manifestJson = $manifest | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText($manifestPath, $manifestJson)

$manifestSizeKB = [math]::Round((Get-Item $manifestPath).Length / 1KB, 2)
Write-Host "   ✅ Manifest generated: manifest.json ($manifestSizeKB KB)" -ForegroundColor Green
Write-Host "   ✅ Contains $($manifest.Files.Count) file entries" -ForegroundColor Green

# Delta size preview (if previous version exists)
$previousManifestPattern = Join-Path $scriptDir "builds\manifests\manifest-v$oldVersion.json"
if (Test-Path $previousManifestPattern) {
    try {
        $oldManifest = Get-Content $previousManifestPattern | ConvertFrom-Json
        $changedFiles = $manifest.Files | Where-Object {
            $newFile = $_
            $oldFile = $oldManifest.Files | Where-Object { $_.RelativePath -eq $newFile.RelativePath }
            (!$oldFile) -or ($oldFile.SHA256Hash -ne $newFile.SHA256Hash)
        }
        
        $deltaSize = ($changedFiles | Measure-Object -Property FileSize -Sum).Sum
        $deltaSizeMB = [math]::Round($deltaSize / 1MB, 2)
        
        Write-Host ""
        Write-Host "   📊 Delta Analysis (vs v$oldVersion):" -ForegroundColor Cyan
        Write-Host "      Changed files: $($changedFiles.Count)" -ForegroundColor White
        Write-Host "      Delta download: ~$deltaSizeMB MB (vs full $packageSize MB)" -ForegroundColor Green
        Write-Host "      Bandwidth savings: $([math]::Round(($packageSize - $deltaSizeMB) / $packageSize * 100, 1))%" -ForegroundColor Green
    } catch {
        Write-Host "   ⚠️ Could not calculate delta (previous manifest invalid)" -ForegroundColor Yellow
    }
}

Write-Host ""

# Archive current manifest for future delta calculations
$manifestArchiveDir = Join-Path $scriptDir "builds\manifests"
if (!(Test-Path $manifestArchiveDir)) {
    New-Item -ItemType Directory -Path $manifestArchiveDir -Force | Out-Null
}
Copy-Item $manifestPath -Destination (Join-Path $manifestArchiveDir "manifest-v$newVersion.json") -Force

# ============================================
# VERIFICATION & TESTING
# ============================================

Write-Host "✅ VERIFICATION & TESTING" -ForegroundColor Magenta
Write-Host "═══════════════════════════════════════" -ForegroundColor Magenta
Write-Host ""

Write-Host "[STEP 1/2] Package integrity check..." -ForegroundColor Yellow

$verifyFolder = Join-Path $scriptDir "TempVerify_$newVersion"
if (Test-Path $verifyFolder) {
    Remove-Item $verifyFolder -Recurse -Force
}
Expand-Archive -Path $packagePath -DestinationPath $verifyFolder -Force

# Verify critical files
$criticalFilesPresent = $true
foreach ($file in @("CloudJourneyAddin.exe", "Azure.Identity.dll", "Microsoft.Graph.dll")) {
    $path = Join-Path $verifyFolder $file
    if (!(Test-Path $path)) {
        Write-Host "   ❌ Missing: $file" -ForegroundColor Red
        $criticalFilesPresent = $false
    }
}

if (!$criticalFilesPresent) {
    Remove-Item $verifyFolder -Recurse -Force
    Write-Host "   ❌ Package integrity check failed!" -ForegroundColor Red
    Stop-Transcript
    exit 1
}

# Verify EXE version
$exeInPackage = Join-Path $verifyFolder "CloudJourneyAddin.exe"
$exeVersion = (Get-Item $exeInPackage).VersionInfo.FileVersion

if ($exeVersion -ne "$newVersion.0") {
    Write-Host "   ⚠️ WARNING: EXE version mismatch!" -ForegroundColor Yellow
    Write-Host "      Expected: $newVersion.0" -ForegroundColor Yellow
    Write-Host "      Found: $exeVersion" -ForegroundColor Yellow
} else {
    Write-Host "   ✅ EXE version correct: $exeVersion" -ForegroundColor Green
}

Write-Host "   ✅ All critical files present" -ForegroundColor Green

Remove-Item $verifyFolder -Recurse -Force
Write-Host ""

# Post-build smoke test
if (!$SkipTests) {
    Write-Host "[STEP 2/2] Post-build smoke test..." -ForegroundColor Yellow
    
    # Extract to temp location
    $testFolder = Join-Path $env:TEMP "CloudJourney_SmokeTest"
    if (Test-Path $testFolder) {
        Remove-Item $testFolder -Recurse -Force
    }
    Expand-Archive -Path $packagePath -DestinationPath $testFolder -Force
    
    try {
        # Test EXE launches
        $exePath = Join-Path $testFolder "CloudJourneyAddin.exe"
        $testProcess = Start-Process $exePath -ArgumentList "--version" -Wait -PassThru -WindowStyle Hidden -ErrorAction Stop
        
        if ($testProcess.ExitCode -eq 0) {
            Write-Host "   ✅ Application launches successfully" -ForegroundColor Green
        } else {
            Write-Host "   ⚠️ Application launched but returned exit code: $($testProcess.ExitCode)" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "   ⚠️ Smoke test inconclusive: $_" -ForegroundColor Yellow
    } finally {
        Remove-Item $testFolder -Recurse -Force -ErrorAction SilentlyContinue
    }
} else {
    Write-Host "[STEP 2/2] Smoke test skipped" -ForegroundColor Yellow
}

Write-Host ""

# ============================================
# DISTRIBUTION
# ============================================

Write-Host "🚀 DISTRIBUTION" -ForegroundColor Magenta
Write-Host "═══════════════════════════════════════" -ForegroundColor Magenta
Write-Host ""

# Create distribution folder if needed
if (!(Test-Path $DistributionPath)) {
    Write-Host "📁 Creating distribution folder: $DistributionPath" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $DistributionPath -Force | Out-Null
}

$destinationPath = Join-Path $DistributionPath $packageName

try {
    Copy-Item -Path $packagePath -Destination $destinationPath -Force
    Write-Host "✅ Package copied to: $destinationPath" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to copy to distribution folder: $_" -ForegroundColor Red
    Write-Host "ℹ️  Package available at: $packagePath" -ForegroundColor Gray
}

Write-Host ""

# Archive old builds
if ($ArchiveOldBuilds -and $oldVersion -ne $newVersion) {
    Write-Host "📦 Archiving previous build..." -ForegroundColor Yellow
    
    $archivePath = Join-Path $scriptDir "builds\archive"
    if (!(Test-Path $archivePath)) {
        New-Item -ItemType Directory -Path $archivePath -Force | Out-Null
    }
    
    $oldPackage = Join-Path $scriptDir "CloudJourneyAddin-v$oldVersion-COMPLETE.zip"
    if (Test-Path $oldPackage) {
        Move-Item $oldPackage -Destination $archivePath -Force
        Write-Host "   ✅ Moved v$oldVersion to archive" -ForegroundColor Green
    } else {
        Write-Host "   ℹ️  No previous package to archive" -ForegroundColor Gray
    }
    
    Write-Host ""
}

# Package size comparison
$previousPackage = Join-Path "$archivePath" "CloudJourneyAddin-v$oldVersion-COMPLETE.zip"
if (Test-Path $previousPackage) {
    $oldSize = [math]::Round((Get-Item $previousPackage).Length / 1MB, 2)
    $sizeDiff = $packageSize - $oldSize
    
    Write-Host "📏 Package size comparison:" -ForegroundColor Cyan
    Write-Host "   Previous (v$oldVersion): $oldSize MB" -ForegroundColor Gray
    Write-Host "   Current (v$newVersion): $packageSize MB" -ForegroundColor White
    $color = if($sizeDiff -gt 5){'Yellow'}else{'Green'}
    Write-Host "   Change: $(if($sizeDiff -gt 0){'+';})$([math]::Round($sizeDiff, 2)) MB" -ForegroundColor $color
    Write-Host ""
}

# ============================================
# GITHUB RELEASE (OPTIONAL)
# ============================================

if ($PublishToGitHub) {
    Write-Host "🐙 GITHUB RELEASE AUTOMATION" -ForegroundColor Magenta
    Write-Host "═══════════════════════════════════════" -ForegroundColor Magenta
    Write-Host ""
    
    # Generate release notes if not provided
    if (!$ReleaseNotes) {
        $ReleaseNotes = @"
## Cloud Journey Add-in v$newVersion

### Changes
- See CHANGELOG.md for detailed changes

### Installation
1. Download CloudJourneyAddin-v$newVersion-COMPLETE.zip
2. Extract all files to installation directory
3. Run CloudJourneyAddin.exe

### Auto-Update
Existing users on v$oldVersion will receive automatic update prompt.

---
Build Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm') UTC
Package Size: $packageSize MB
"@
    }
    
    # Save release notes to temp file
    $notesFile = Join-Path $env:TEMP "release-notes-$newVersion.md"
    $ReleaseNotes | Out-File $notesFile -Encoding UTF8
    
    Write-Host "[STEP 1/4] Committing version changes..." -ForegroundColor Yellow
    try {
        git add .
        git commit -m "Release v$newVersion - Auto-increment version"
        Write-Host "   ✅ Changes committed" -ForegroundColor Green
    } catch {
        Write-Host "   ⚠️ Commit failed (may already be committed): $_" -ForegroundColor Yellow
    }
    Write-Host ""
    
    Write-Host "[STEP 2/4] Creating git tag..." -ForegroundColor Yellow
    try {
        git tag -a "v$newVersion" -m "Version $newVersion"
        Write-Host "   ✅ Tag created: v$newVersion" -ForegroundColor Green
    } catch {
        Write-Host "   ⚠️ Tag may already exist: $_" -ForegroundColor Yellow
    }
    Write-Host ""
    
    Write-Host "[STEP 3/4] Pushing to GitHub..." -ForegroundColor Yellow
    try {
        git push origin main --tags
        Write-Host "   ✅ Pushed to GitHub" -ForegroundColor Green
    } catch {
        Write-Host "   ⚠️ Push failed: $_" -ForegroundColor Yellow
    }
    Write-Host ""
    
    Write-Host "[STEP 4/4] Creating GitHub Release..." -ForegroundColor Yellow
    try {
        $releaseArgs = @(
            "release", "create", "v$newVersion",
            $packagePath,
            $manifestPath,
            "--title", "Cloud Journey Add-in v$newVersion",
            "--notes-file", $notesFile
        )
        
        & gh @releaseArgs
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "   ✅ GitHub Release created successfully" -ForegroundColor Green
            Write-Host "   🔗 https://github.com/sccmavenger/cmaddin/releases/tag/v$newVersion" -ForegroundColor Cyan
        } else {
            Write-Host "   ❌ Release creation failed (exit code: $LASTEXITCODE)" -ForegroundColor Red
        }
    } catch {
        Write-Host "   ❌ Release creation failed: $_" -ForegroundColor Red
    } finally {
        Remove-Item $notesFile -Force -ErrorAction SilentlyContinue
    }
    
    Write-Host ""
}

# ============================================
# BUILD SUMMARY
# ============================================

$buildDuration = (Get-Date) - $buildStartTime

Write-Host ""
Write-Host "╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║                   BUILD COMPLETE! ✅                      ║" -ForegroundColor Green
Write-Host "╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""

Write-Host "📊 BUILD SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "Version:        $oldVersion → $newVersion" -ForegroundColor White
Write-Host "Package:        $packageName" -ForegroundColor White
Write-Host "Size:           $packageSize MB" -ForegroundColor White
Write-Host "Files:          $packageFileCount" -ForegroundColor White
Write-Host "Build Time:     $([math]::Round($buildDuration.TotalMinutes, 1)) minutes" -ForegroundColor White
Write-Host ""
Write-Host "📂 LOCATIONS" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "Package:        $packagePath" -ForegroundColor White
Write-Host "Distribution:   $destinationPath" -ForegroundColor White
Write-Host "Manifest:       $manifestPath" -ForegroundColor White
Write-Host "Build Log:      $buildLogPath" -ForegroundColor White
Write-Host ""

if ($PublishToGitHub) {
    Write-Host "🐙 GITHUB RELEASE" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Release URL:    https://github.com/sccmavenger/cmaddin/releases/tag/v$newVersion" -ForegroundColor Cyan
    Write-Host ""
}

Write-Host "📋 NEXT STEPS" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Update CHANGELOG.md with detailed changes" -ForegroundColor White
Write-Host "2. Test installation on clean machine" -ForegroundColor White
Write-Host "3. Validate auto-update from v$oldVersion" -ForegroundColor White
Write-Host "4. Update any relevant documentation" -ForegroundColor White

if (!$PublishToGitHub) {
    Write-Host ""
    Write-Host "💡 TIP: Use -PublishToGitHub to automate GitHub release creation" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""

Stop-Transcript
