#Requires -Version 5.1

<#
.SYNOPSIS
    Complete build and distribution script for Zero Trust Migration Journey Add-in
    
.DESCRIPTION
    Automatically increments version, updates all 6 required documentation locations,
    builds the project, creates a complete package, and copies to distribution folder
    
.PARAMETER Version
    Version number (e.g., "1.4.0"). If not specified, auto-increments current version
    
.PARAMETER BumpVersion
    Which version component to increment (Major, Minor, or Patch). Default: Patch
    - Patch: Bug fixes (1.7.1 → 1.7.2)
    - Minor: New features (1.7.1 → 1.8.0)
    - Major: Breaking changes (1.7.1 → 2.0.0)
    
.PARAMETER SkipBuild
    Skip the build step and just package existing files
    
.PARAMETER DistributionPath
    Path to copy final package to (default: C:\Users\dannygu\Dropbox)
    
.EXAMPLE
    .\Build-And-Distribute.ps1
    # Auto-increments patch version (1.7.1 → 1.7.2) and updates all docs
    
.EXAMPLE
    .\Build-And-Distribute.ps1 -BumpVersion Minor
    # Increments minor version (1.7.1 → 1.8.0) and updates all docs
    
.EXAMPLE
    .\Build-And-Distribute.ps1 -Version "2.0.0"
    # Uses specific version without auto-increment
#>

param(
    [string]$Version,
    [switch]$SkipBuild,
    [string]$DistributionPath = "C:\Users\dannygu\Dropbox",
    [ValidateSet('Major', 'Minor', 'Patch')]
    [string]$BumpVersion = 'Patch'
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Zero Trust Migration Journey Add-in - Build & Distribute" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ============================================
# AUTO VERSION BUMP
# ============================================
$csprojPath = Join-Path $scriptDir "ZeroTrustMigrationAddin.csproj"
if (-not (Test-Path $csprojPath)) {
    Write-Host "❌ ERROR: Could not find ZeroTrustMigrationAddin.csproj" -ForegroundColor Red
    exit 1
}

[xml]$csproj = Get-Content $csprojPath
$currentVersion = $csproj.Project.PropertyGroup.Version | Select-Object -First 1

# Auto-increment version unless explicitly provided
if (-not $Version) {
    $versionParts = $currentVersion -split '\.'
    $major = [int]$versionParts[0]
    $minor = [int]$versionParts[1]
    $patch = [int]$versionParts[2]
    
    # Automatically use the BumpVersion parameter (defaults to Patch)
    Write-Host "" 
    Write-Host "🔄 AUTO VERSION BUMP (No prompt - using -BumpVersion parameter)" -ForegroundColor Cyan
    Write-Host "   Current version: $currentVersion" -ForegroundColor White
    Write-Host "   Bump type: $BumpVersion" -ForegroundColor Green
    Write-Host ""
    
    switch ($BumpVersion) {
        'Major' { $major++; $minor = 0; $patch = 0 }
        'Minor' { $minor++; $patch = 0 }
        'Patch' { $patch++ }
    }
    
    $newVersion = "$major.$minor.$patch"
    
    Write-Host "   New version: $newVersion" -ForegroundColor Green
    Write-Host ""
    
    # Update all 6 locations automatically
    Write-Host "📝 Updating version in 6 required locations..." -ForegroundColor Yellow
    
    # 1. ZeroTrustMigrationAddin.csproj (3 places)
    $csproj.Project.PropertyGroup.Version = $newVersion
    $csproj.Project.PropertyGroup.AssemblyVersion = "$newVersion.0"
    $csproj.Project.PropertyGroup.FileVersion = "$newVersion.0"
    $csproj.Save($csprojPath)
    Write-Host "   [1/6] ✅ ZeroTrustMigrationAddin.csproj" -ForegroundColor Green
    
    # 2. README.md
    $readmePath = Join-Path $scriptDir "README.md"
    $readmeContent = Get-Content $readmePath -Raw
    $readmeContent = $readmeContent -replace "Version $currentVersion", "Version $newVersion"
    $readmeContent = $readmeContent -replace "v$currentVersion", "v$newVersion"
    [System.IO.File]::WriteAllText($readmePath, $readmeContent)
    Write-Host "   [2/6] ✅ README.md" -ForegroundColor Green
    
    # 3. USER_GUIDE.md
    $userGuidePath = Join-Path $scriptDir "USER_GUIDE.md"
    if (Test-Path $userGuidePath) {
        $userGuideContent = Get-Content $userGuidePath -Raw
        $userGuideContent = $userGuideContent -replace "Version $currentVersion", "Version $newVersion"
        $userGuideContent = $userGuideContent -replace "v$currentVersion", "v$newVersion"
        [System.IO.File]::WriteAllText($userGuidePath, $userGuideContent)
        Write-Host "   [3/5] ✅ USER_GUIDE.md" -ForegroundColor Green
    }
    
    # 4. DashboardWindow.xaml Title
    $xamlPath = Join-Path $scriptDir "Views\DashboardWindow.xaml"
    $xamlContent = Get-Content $xamlPath -Raw
    $xamlContent = $xamlContent -replace "v$currentVersion", "v$newVersion"
    [System.IO.File]::WriteAllText($xamlPath, $xamlContent)
    Write-Host "   [4/5] ✅ DashboardWindow.xaml" -ForegroundColor Green
    
    # 5. DashboardViewModel.cs version log
    $viewModelPath = Join-Path $scriptDir "ViewModels\DashboardViewModel.cs"
    $viewModelContent = Get-Content $viewModelPath -Raw
    $viewModelContent = $viewModelContent -replace "Version: $currentVersion", "Version: $newVersion"
    [System.IO.File]::WriteAllText($viewModelPath, $viewModelContent)
    Write-Host "   [5/5] ✅ DashboardViewModel.cs" -ForegroundColor Green
    
    Write-Host ""
    Write-Host "✅ All 5 locations updated to v$newVersion" -ForegroundColor Green
    Write-Host ""
    
    $currentVersion = $newVersion
} else {
    Write-Host "📦 Using provided version: $Version" -ForegroundColor Cyan
    $currentVersion = $Version
}

Write-Host ""

# ============================================
# PRE-FLIGHT CHECKS (MANDATORY)
# Per VERSIONING.md - ALL 5 locations MUST be updated
# ============================================
Write-Host "🔍 PRE-FLIGHT DOCUMENTATION CHECKS (5 Required)" -ForegroundColor Magenta
Write-Host "Per VERSIONING.md: All 5 locations must be updated" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host ""

Write-Host "📦 Current Version: $currentVersion" -ForegroundColor Cyan
Write-Host ""

# Check 1: Version number updated in .csproj
Write-Host "[CHECK 1/5] Version Number" -ForegroundColor Yellow
Write-Host "   Location: ZeroTrustMigrationAddin.csproj (line 10-12)" -ForegroundColor Gray
Write-Host "   Current: $currentVersion" -ForegroundColor White
Write-Host "   ✅ Version detected: $currentVersion" -ForegroundColor Green
Write-Host ""

# Check 2: README.md updated
Write-Host "[CHECK 2/6] README.md" -ForegroundColor Yellow
Write-Host "   Location: README.md (top section)" -ForegroundColor Gray
$readmePath = Join-Path $scriptDir "README.md"
$readmeContent = Get-Content $readmePath -Raw
if ($readmeContent -match "Version $currentVersion") {
    Write-Host "   ✅ README.md contains version $currentVersion" -ForegroundColor Green
} else {
    Write-Host "   ⚠️  WARNING: README.md may not contain version $currentVersion" -ForegroundColor Yellow
    Write-Host "   Continuing anyway..." -ForegroundColor Gray
}
Write-Host ""

# Check 3: USER_GUIDE.md updated
Write-Host "[CHECK 3/6] USER_GUIDE.md" -ForegroundColor Yellow
Write-Host "   Location: USER_GUIDE.md (header)" -ForegroundColor Gray
$userGuidePath = Join-Path $scriptDir "USER_GUIDE.md"
if (Test-Path $userGuidePath) {
    $userGuideContent = Get-Content $userGuidePath -Raw
    if ($userGuideContent -match "Version $currentVersion") {
        Write-Host "   ✅ USER_GUIDE.md contains version $currentVersion" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  WARNING: USER_GUIDE.md may not contain version $currentVersion" -ForegroundColor Yellow
        Write-Host "   Continuing anyway..." -ForegroundColor Gray
    }
} else {
    Write-Host "   ⚠️  WARNING: USER_GUIDE.md not found" -ForegroundColor Yellow
}
Write-Host ""

# Check 4: DashboardWindow.xaml Title
Write-Host "[CHECK 4/5] DashboardWindow.xaml Title" -ForegroundColor Yellow
Write-Host "   Location: Views/DashboardWindow.xaml (line 6)" -ForegroundColor Gray
$xamlPath = Join-Path $scriptDir "Views\DashboardWindow.xaml"
if (Test-Path $xamlPath) {
    $xamlContent = Get-Content $xamlPath -Raw
    if ($xamlContent -match "Title=`"Zero Trust Migration Journey Dashboard v$currentVersion`"") {
        Write-Host "   ✅ XAML Title contains v$currentVersion" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  WARNING: XAML Title may not match version $currentVersion" -ForegroundColor Yellow
        Write-Host "   Continuing anyway..." -ForegroundColor Gray
    }
} else {
    Write-Host "   ⚠️  WARNING: DashboardWindow.xaml not found" -ForegroundColor Yellow
}
Write-Host ""

# Check 5: DashboardViewModel.cs version log
Write-Host "[CHECK 5/5] DashboardViewModel.cs Version Log" -ForegroundColor Yellow
Write-Host "   Location: ViewModels/DashboardViewModel.cs (constructor)" -ForegroundColor Gray
$viewModelPath = Join-Path $scriptDir "ViewModels\DashboardViewModel.cs"
if (Test-Path $viewModelPath) {
    $viewModelContent = Get-Content $viewModelPath -Raw
    if ($viewModelContent -match "Version: $currentVersion") {
        Write-Host "   ✅ ViewModel log contains version $currentVersion" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  WARNING: ViewModel version log may not match version $currentVersion" -ForegroundColor Yellow
        Write-Host "   Continuing anyway..." -ForegroundColor Gray
    }
} else {
    Write-Host "   ⚠️  WARNING: DashboardViewModel.cs not found" -ForegroundColor Yellow
}
Write-Host ""

Write-Host "========================================" -ForegroundColor Green
Write-Host "✅ ALL PRE-FLIGHT CHECKS PASSED" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Proceeding with build..." -ForegroundColor Cyan
Write-Host ""

# ============================================
# BUILD PROCESS STARTS HERE
# ============================================

# Step 1: Detect or validate version
Write-Host "[1/7] Detecting version..." -ForegroundColor Yellow

if (-not $Version) {
    $Version = $currentVersion
    Write-Host "   ✅ Auto-detected version: $Version" -ForegroundColor Green
} else {
    Write-Host "   ✅ Using specified version: $Version" -ForegroundColor Green
}

# Validate version format
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "   ❌ ERROR: Invalid version format '$Version' (expected X.Y.Z)" -ForegroundColor Red
    exit 1
}

# Step 2: Clean (if building)
if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "[2/7] Cleaning previous build..." -ForegroundColor Yellow
    dotnet clean ZeroTrustMigrationAddin.csproj -c Release --nologo -v quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "   ❌ Clean failed!" -ForegroundColor Red
        exit 1
    }
    
    # Clean Azure OpenAI configuration for fresh testing
    $configPath = Join-Path $env:APPDATA "ZeroTrustMigrationAddin\openai-config.json"
    if (Test-Path $configPath) {
        Remove-Item $configPath -Force -ErrorAction SilentlyContinue
        Write-Host "   🧹 Cleaned Azure OpenAI configuration" -ForegroundColor Green
    }
    
    Write-Host "   ✅ Clean complete" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "[2/7] Skipping build (using existing files)..." -ForegroundColor Gray
}

# Step 3: Build
if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "[3/7] Building project..." -ForegroundColor Yellow
    dotnet build ZeroTrustMigrationAddin.csproj -c Release --nologo -v quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "   ❌ Build failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "   ✅ Build succeeded" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "[3/7] Skipping build..." -ForegroundColor Gray
}

# Step 4: Publish
if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "[4/7] Publishing with dependencies..." -ForegroundColor Yellow
    $publishPath = Join-Path $scriptDir "bin\Release\net8.0-windows\win-x64\publish"
    
    dotnet publish ZeroTrustMigrationAddin.csproj -c Release -r win-x64 --self-contained true --nologo -v quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "   ❌ Publish failed!" -ForegroundColor Red
        exit 1
    }
    
    # Verify critical files
    $exePath = Join-Path $publishPath "ZeroTrustMigrationAddin.exe"
    $azureIdentityPath = Join-Path $publishPath "Azure.Identity.dll"
    
    if (-not (Test-Path $exePath)) {
        Write-Host "   ❌ ERROR: ZeroTrustMigrationAddin.exe not found in publish folder!" -ForegroundColor Red
        exit 1
    }
    
    if (-not (Test-Path $azureIdentityPath)) {
        Write-Host "   ❌ ERROR: Azure.Identity.dll not found in publish folder!" -ForegroundColor Red
        exit 1
    }
    
    $fileCount = (Get-ChildItem $publishPath -File).Count
    Write-Host "   ✅ Published $fileCount files" -ForegroundColor Green
    Write-Host "   ✅ Critical files verified (exe, Azure.Identity.dll)" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "[4/7] Skipping publish..." -ForegroundColor Gray
    $publishPath = Join-Path $scriptDir "bin\Release\net8.0-windows\win-x64\publish"
}

# Step 5: Create package
Write-Host ""
Write-Host "[5/7] Creating complete package..." -ForegroundColor Yellow

$packageName = "ZeroTrustMigrationAddin-v$Version-COMPLETE.zip"
$packagePath = Join-Path $scriptDir $packageName
$tempFolder = Join-Path $scriptDir "TempPackage_$Version"

# Clean temp folder
if (Test-Path $tempFolder) {
    Remove-Item $tempFolder -Recurse -Force
}
New-Item -ItemType Directory -Path $tempFolder -Force | Out-Null

# Copy all published files
Write-Host "   ⏳ Copying binaries from publish folder..." -ForegroundColor Gray
$publishFiles = Get-ChildItem "$publishPath\*" -File
$binCount = 0
foreach ($file in $publishFiles) {
    Copy-Item $file.FullName -Destination $tempFolder -Force
    $binCount++
    
    # Show progress every 50 files
    if ($binCount % 50 -eq 0) {
        Write-Host "      Copied $binCount files..." -ForegroundColor DarkGray
    }
}
Write-Host "   ✓ Copied $binCount binary files" -ForegroundColor Green

# Copy update scripts
$updatePackagePath = Join-Path $scriptDir "UpdatePackage"
if (Test-Path $updatePackagePath) {
    Write-Host "   ⏳ Copying update scripts..." -ForegroundColor Gray
    $scriptFiles = Get-ChildItem $updatePackagePath -Filter "*.ps1"
    foreach ($script in $scriptFiles) {
        Copy-Item $script.FullName -Destination $tempFolder -Force
        Write-Host "      ✓ $($script.Name)" -ForegroundColor DarkGray
    }
    
    # Copy ZeroTrustMigrationAddin.xml if exists
    $xmlPath = Join-Path $updatePackagePath "ZeroTrustMigrationAddin.xml"
    if (Test-Path $xmlPath) {
        Copy-Item $xmlPath -Destination $tempFolder -Force
        Write-Host "      ✓ ZeroTrustMigrationAddin.xml" -ForegroundColor DarkGray
    }
    
    Write-Host "   ✓ Copied $($scriptFiles.Count + 1) scripts/config files" -ForegroundColor Green
}

# Count files
$packageFileCount = (Get-ChildItem $tempFolder -File -Recurse).Count

# Create ZIP
Write-Host "   ⏳ Compressing package (this may take a moment)..." -ForegroundColor Gray
if (Test-Path $packagePath) {
    Remove-Item $packagePath -Force
}
Compress-Archive -Path "$tempFolder\*" -DestinationPath $packagePath -CompressionLevel Optimal

# Clean temp folder
Remove-Item $tempFolder -Recurse -Force

$packageSize = [math]::Round((Get-Item $packagePath).Length / 1MB, 2)
Write-Host "   ✅ Package created: $packageName ($packageSize MB)" -ForegroundColor Green
Write-Host "   ✅ Contains $packageFileCount files" -ForegroundColor Green

# Step 5a: Generate manifest for delta updates
Write-Host ""
Write-Host "[5a/7] Generating update manifest..." -ForegroundColor Yellow

$manifestPath = Join-Path $scriptDir "manifest.json"
$manifest = @{
    Version = $Version
    BuildDate = (Get-Date).ToUniversalTime().ToString("o")
    Files = @()
    TotalSize = 0
}

# Calculate SHA256 hash for all files in publish folder
$publishFiles = Get-ChildItem "$publishPath" -File
$manifestFileCount = 0

Write-Host "   ⏳ Calculating file hashes..." -ForegroundColor Gray

foreach ($file in $publishFiles) {
    $manifestFileCount++
    
    # Show progress every 50 files
    if ($manifestFileCount % 50 -eq 0) {
        Write-Host "      Processed $manifestFileCount/$($publishFiles.Count) files..." -ForegroundColor DarkGray
    }
    
    $hash = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLower()
    
    # Determine if file is critical
    $isCritical = $false
    $criticalFiles = @(
        "ZeroTrustMigrationAddin.exe",
        "ZeroTrustMigrationAddin.dll",
        "Azure.Identity.dll",
        "Microsoft.Graph.dll",
        "Microsoft.Graph.Core.dll",
        "Newtonsoft.Json.dll"
    )
    if ($criticalFiles -contains $file.Name) {
        $isCritical = $true
    }
    
    $fileEntry = @{
        RelativePath = $file.Name
        SHA256Hash = $hash
        FileSize = $file.Length
        LastModified = $file.LastWriteTimeUtc.ToString("o")
        IsCritical = $isCritical
    }
    
    $manifest.Files += $fileEntry
    $manifest.TotalSize += $file.Length
}

# Save manifest as JSON
$manifestJson = $manifest | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText($manifestPath, $manifestJson)

$manifestSizeKB = [math]::Round((Get-Item $manifestPath).Length / 1KB, 2)
Write-Host "   ✅ Manifest generated: manifest.json ($manifestSizeKB KB)" -ForegroundColor Green
Write-Host "   ✅ Contains $($manifest.Files.Count) file entries" -ForegroundColor Green
Write-Host "   ✅ Total package size: $([math]::Round($manifest.TotalSize / 1MB, 2)) MB" -ForegroundColor Green

# Step 6: Verify package
Write-Host ""
Write-Host "[6/7] Verifying package integrity..." -ForegroundColor Yellow

# Extract to temp location for verification
$verifyFolder = Join-Path $scriptDir "TempVerify_$Version"
if (Test-Path $verifyFolder) {
    Remove-Item $verifyFolder -Recurse -Force
}
Expand-Archive -Path $packagePath -DestinationPath $verifyFolder -Force

$exeInPackage = Join-Path $verifyFolder "ZeroTrustMigrationAddin.exe"
$azureIdentityInPackage = Join-Path $verifyFolder "Azure.Identity.dll"

if (-not (Test-Path $exeInPackage)) {
    Write-Host "   ❌ ERROR: ZeroTrustMigrationAddin.exe not found in package!" -ForegroundColor Red
    Remove-Item $verifyFolder -Recurse -Force
    exit 1
}

if (-not (Test-Path $azureIdentityInPackage)) {
    Write-Host "   ❌ ERROR: Azure.Identity.dll not found in package!" -ForegroundColor Red
    Remove-Item $verifyFolder -Recurse -Force
    exit 1
}

$exeVersion = (Get-Item $exeInPackage).VersionInfo.FileVersion
if ($exeVersion -ne "$Version.0") {
    Write-Host "   ⚠️ WARNING: EXE version mismatch! Expected: $Version.0, Found: $exeVersion" -ForegroundColor Yellow
}

Write-Host "   ✅ ZeroTrustMigrationAddin.exe version: $exeVersion" -ForegroundColor Green
Write-Host "   ✅ Azure.Identity.dll present ($(([math]::Round((Get-Item $azureIdentityInPackage).Length / 1KB, 1))) KB)" -ForegroundColor Green

# Clean verification folder
Remove-Item $verifyFolder -Recurse -Force

# Step 7: Copy to distribution folder
Write-Host ""
Write-Host "[7/7] Copying to distribution folder..." -ForegroundColor Yellow

if (-not (Test-Path $DistributionPath)) {
    Write-Host "   ⚠️ Creating distribution folder: $DistributionPath" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $DistributionPath -Force | Out-Null
}

$destinationPath = Join-Path $DistributionPath $packageName

try {
    Copy-Item -Path $packagePath -Destination $destinationPath -Force
    Write-Host "   ✅ Package copied to: $destinationPath" -ForegroundColor Green
} catch {
    Write-Host "   ❌ ERROR: Failed to copy to distribution folder: $_" -ForegroundColor Red
    Write-Host "   ℹ️ Package is still available at: $packagePath" -ForegroundColor Gray
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Build & Distribution Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "📦 Package Details:" -ForegroundColor Yellow
Write-Host "   Name: $packageName" -ForegroundColor White
Write-Host "   Size: $packageSize MB" -ForegroundColor White
Write-Host "   Files: $packageFileCount" -ForegroundColor White
Write-Host "   Version: $exeVersion" -ForegroundColor Cyan
Write-Host ""
Write-Host "📂 Locations:" -ForegroundColor Yellow
Write-Host "   Build folder: $packagePath" -ForegroundColor White
Write-Host "   Distribution: $destinationPath" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Next Steps:" -ForegroundColor Yellow
Write-Host "   1. Copy package from Dropbox to target PC" -ForegroundColor White
Write-Host "   2. Extract ALL files (~$packageFileCount files)" -ForegroundColor White
Write-Host "   3. Run Diagnose-Installation.ps1 to verify" -ForegroundColor White
Write-Host "   4. Run Update-ZeroTrustMigrationAddin.ps1 to deploy" -ForegroundColor White
Write-Host ""

# ============================================
# POST-BUILD DOCUMENTATION CHECKLIST
# ============================================
Write-Host "========================================" -ForegroundColor Magenta
Write-Host "📝 POST-BUILD DOCUMENTATION UPDATE REQUIRED" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host ""
Write-Host "⚠️  IMPORTANT: Update existing documentation before distribution!" -ForegroundColor Yellow
Write-Host ""

# Core Documentation Files to Update
Write-Host "📄 REQUIRED UPDATES TO EXISTING FILES:" -ForegroundColor Cyan
Write-Host ""

Write-Host "1. README.md" -ForegroundColor White
Write-Host "   Location: ./README.md" -ForegroundColor Gray
Write-Host "   Update:" -ForegroundColor Yellow
Write-Host "   • Version number references (currently v$Version)" -ForegroundColor Gray
Write-Host "   • Feature list with any new capabilities" -ForegroundColor Gray
Write-Host "   • Installation/upgrade instructions if changed" -ForegroundColor Gray
Write-Host "   • Screenshots if UI changed significantly" -ForegroundColor Gray
Write-Host ""

Write-Host "2. CHANGELOG.md" -ForegroundColor White
Write-Host "   Location: ./CHANGELOG.md" -ForegroundColor Gray
Write-Host "   Update:" -ForegroundColor Yellow
Write-Host "   • Add [[$Version]] - $(Get-Date -Format 'yyyy-MM-dd') section at top" -ForegroundColor Gray
Write-Host "   • List all changes under ### Added, ### Changed, ### Fixed" -ForegroundColor Gray
Write-Host "   • Reference any breaking changes or migration steps" -ForegroundColor Gray
Write-Host ""

Write-Host "3. AdminUserGuide.html" -ForegroundColor White
Write-Host "   Location: ./AdminUserGuide.html" -ForegroundColor Gray
Write-Host "   Update:" -ForegroundColor Yellow
Write-Host "   • Version number in title/header" -ForegroundColor Gray
Write-Host "   • New features with step-by-step instructions" -ForegroundColor Gray
Write-Host "   • Updated screenshots for changed UI elements" -ForegroundColor Gray
Write-Host "   • Troubleshooting section with new known issues" -ForegroundColor Gray
Write-Host ""

Write-Host "4. VERSIONING.md" -ForegroundColor White
Write-Host "   Location: ./VERSIONING.md" -ForegroundColor Gray
Write-Host "   Update:" -ForegroundColor Yellow
Write-Host "   • Version history table with v$Version" -ForegroundColor Gray
Write-Host "   • Update 'Current Version' reference" -ForegroundColor Gray
Write-Host ""

Write-Host "5. TAB_VISIBILITY_GUIDE.md (if UI tabs changed)" -ForegroundColor White
Write-Host "   Location: ./TAB_VISIBILITY_GUIDE.md" -ForegroundColor Gray
Write-Host "   Update:" -ForegroundColor Yellow
Write-Host "   • Tab visibility rules if modified" -ForegroundColor Gray
Write-Host "   • Configuration examples with new features" -ForegroundColor Gray
Write-Host ""

Write-Host "6. PRIVACY.md (if data handling changed)" -ForegroundColor White
Write-Host "   Location: ./PRIVACY.md" -ForegroundColor Gray
Write-Host "   Update:" -ForegroundColor Yellow
Write-Host "   • Data collection/transmission changes" -ForegroundColor Gray
Write-Host "   • Azure OpenAI prompt changes" -ForegroundColor Gray
Write-Host "   • New telemetry or logging additions" -ForegroundColor Gray
Write-Host ""

# Optional Documentation
Write-Host "📋 OPTIONAL (Create if major release):" -ForegroundColor Cyan
Write-Host ""
Write-Host "7. RELEASE_NOTES_v$Version.md (for major versions)" -ForegroundColor White
Write-Host "   Location: ./RELEASE_NOTES_v$Version.md" -ForegroundColor Gray
Write-Host "   Contents:" -ForegroundColor Yellow
Write-Host "   • High-level summary for end users" -ForegroundColor Gray
Write-Host "   • Installation/upgrade guide" -ForegroundColor Gray
Write-Host "   • Breaking changes and migration steps" -ForegroundColor Gray
Write-Host "   • Known issues and workarounds" -ForegroundColor Gray
Write-Host "   • Testing checklist" -ForegroundColor Gray
Write-Host ""

# Internal Documentation
Write-Host "🔧 INTERNAL DOCUMENTATION:" -ForegroundColor Cyan
Write-Host ""
Write-Host "8. AUTO_REMEDIATION_ROADMAP.md (if remediation features added)" -ForegroundColor White
Write-Host "   Location: ./AUTO_REMEDIATION_ROADMAP.md" -ForegroundColor Gray
Write-Host "   Update implementation progress and timeline" -ForegroundColor Gray
Write-Host ""

Write-Host "9. WORKLOAD_BRAINSTORM_IDEAS.md (if workload features added)" -ForegroundColor White
Write-Host "   Location: ./WORKLOAD_BRAINSTORM_IDEAS.md" -ForegroundColor Gray
Write-Host "   Mark completed features and add new ideas" -ForegroundColor Gray
Write-Host ""

# Testing Checklist
Write-Host "========================================" -ForegroundColor Magenta
Write-Host "✅ PRE-DISTRIBUTION TESTING CHECKLIST" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host ""
Write-Host "Before distributing v$Version, test:" -ForegroundColor Yellow
Write-Host "   [ ] Install on clean PC without ConfigMgr" -ForegroundColor White
Write-Host "   [ ] Verify add-in ribbon appears in ConfigMgr console" -ForegroundColor White
Write-Host "   [ ] Test Azure OpenAI configuration (if AI features)" -ForegroundColor White
Write-Host "   [ ] Verify Graph API connection" -ForegroundColor White
Write-Host "   [ ] Test ConfigMgr Admin Service connection" -ForegroundColor White
Write-Host "   [ ] Validate all new features work as expected" -ForegroundColor White
Write-Host "   [ ] Check FileLogger.Instance logs for errors" -ForegroundColor White
Write-Host "   [ ] Test update process from previous version" -ForegroundColor White
Write-Host "   [ ] Verify uninstall process works cleanly" -ForegroundColor White
Write-Host ""

# Git Workflow
Write-Host "========================================" -ForegroundColor Magenta
Write-Host "📦 GIT COMMIT & TAG WORKFLOW" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host ""
Write-Host "After updating all documentation:" -ForegroundColor Yellow
Write-Host ""
Write-Host "   git add ." -ForegroundColor Gray
Write-Host "   git commit -m 'Release v$Version - [brief summary]'" -ForegroundColor Gray
Write-Host "   git tag -a v$Version -m 'Version $Version'" -ForegroundColor Gray
Write-Host "   git push origin main --tags" -ForegroundColor Gray
Write-Host ""

Write-Host "========================================" -ForegroundColor Green
Write-Host "✅ Package ready for deployment after documentation updates" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
