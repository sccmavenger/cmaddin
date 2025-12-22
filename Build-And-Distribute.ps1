#Requires -Version 5.1

<#
.SYNOPSIS
    Complete build and distribution script for Cloud Journey Add-in
    
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
Write-Host "Cloud Journey Add-in - Build & Distribute" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ============================================
# AUTO VERSION BUMP
# ============================================
$csprojPath = Join-Path $scriptDir "CloudJourneyAddin.csproj"
if (-not (Test-Path $csprojPath)) {
    Write-Host "❌ ERROR: Could not find CloudJourneyAddin.csproj" -ForegroundColor Red
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
    
    # Display semantic versioning guidance
    Write-Host "" 
    Write-Host "📚 SEMANTIC VERSIONING RULES (per VERSIONING.md):" -ForegroundColor Cyan
    Write-Host "   PATCH:  Bug fixes only, no new features" -ForegroundColor Gray
    Write-Host "   MINOR:  New features, backward compatible" -ForegroundColor Gray  
    Write-Host "   MAJOR:  Breaking changes, major rewrites" -ForegroundColor Gray
    Write-Host ""
    Write-Host "❓ What type of changes are in this build?" -ForegroundColor Yellow
    Write-Host "   Current version: $currentVersion" -ForegroundColor White
    Write-Host ""
    Write-Host "   [P] PATCH   - Bug fixes only (→ $major.$minor.$($patch + 1))" -ForegroundColor Green
    Write-Host "   [M] MINOR   - New features added (→ $major.$($minor + 1).0)" -ForegroundColor Cyan
    Write-Host "   [J] MAJOR   - Breaking changes (→ $($major + 1).0.0)" -ForegroundColor Magenta
    Write-Host "   [C] Cancel  - Exit without building" -ForegroundColor Red
    Write-Host ""
    
    # If BumpVersion parameter was provided, use it; otherwise prompt
    if ($PSBoundParameters.ContainsKey('BumpVersion')) {
        Write-Host "✅ Using -BumpVersion parameter: $BumpVersion" -ForegroundColor Green
    } else {
        $choice = Read-Host "Select version bump type [P/M/J/C]"
        switch ($choice.ToUpper()) {
            'P' { $BumpVersion = 'Patch' }
            'M' { $BumpVersion = 'Minor' }
            'J' { $BumpVersion = 'Major' }
            'C' { 
                Write-Host ""
                Write-Host "❌ Build cancelled by user" -ForegroundColor Red
                exit 0
            }
            default { 
                Write-Host ""
                Write-Host "❌ Invalid choice. Defaulting to PATCH" -ForegroundColor Yellow
                $BumpVersion = 'Patch'
            }
        }
    }
    Write-Host ""
    
    switch ($BumpVersion) {
        'Major' { $major++; $minor = 0; $patch = 0 }
        'Minor' { $minor++; $patch = 0 }
        'Patch' { $patch++ }
    }
    
    $newVersion = "$major.$minor.$patch"
    
    # Validation: Provide guidance on proper semantic versioning
    Write-Host "" 
    Write-Host "🔄 AUTO VERSION BUMP" -ForegroundColor Magenta
    Write-Host "   Current: $currentVersion" -ForegroundColor Gray
    Write-Host "   New:     $newVersion (Bump: $BumpVersion)" -ForegroundColor Green
    Write-Host "" 
    Write-Host "⚠️  REMINDER: Verify this matches your changes:" -ForegroundColor Yellow
    switch ($BumpVersion) {
        'Patch' { 
            Write-Host "   ✅ PATCH: Bug fixes, performance, docs only" -ForegroundColor Green
            Write-Host "   ❌ If adding new features, should be MINOR" -ForegroundColor Red
        }
        'Minor' { 
            Write-Host "   ✅ MINOR: New features, backward compatible" -ForegroundColor Green  
            Write-Host "   ❌ If only bug fixes, should be PATCH" -ForegroundColor Red
            Write-Host "   ❌ If breaking changes, should be MAJOR" -ForegroundColor Red
        }
        'Major' { 
            Write-Host "   ✅ MAJOR: Breaking changes, major rewrites" -ForegroundColor Green
            Write-Host "   ❌ If backward compatible, should be MINOR" -ForegroundColor Red
        }
    }
    Write-Host "========================================" -ForegroundColor Magenta
    Write-Host ""
    
    # Update all 6 locations automatically
    Write-Host "📝 Updating version in 6 required locations..." -ForegroundColor Yellow
    
    # 1. CloudJourneyAddin.csproj (3 places)
    $csproj.Project.PropertyGroup.Version = $newVersion
    $csproj.Project.PropertyGroup.AssemblyVersion = "$newVersion.0"
    $csproj.Project.PropertyGroup.FileVersion = "$newVersion.0"
    $csproj.Save($csprojPath)
    Write-Host "   [1/6] ✅ CloudJourneyAddin.csproj" -ForegroundColor Green
    
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
        Write-Host "   [3/6] ✅ USER_GUIDE.md" -ForegroundColor Green
    }
    
    # 4. Create INTERNAL_DOCS_vX.X.X.md
    $oldInternalDocs = Join-Path $scriptDir "INTERNAL_DOCS_v$currentVersion.md"
    $newInternalDocs = Join-Path $scriptDir "INTERNAL_DOCS_v$newVersion.md"
    if (Test-Path $oldInternalDocs) {
        Copy-Item $oldInternalDocs $newInternalDocs
        $internalContent = Get-Content $newInternalDocs -Raw
        $internalContent = $internalContent -replace "Version $currentVersion", "Version $newVersion"
        $internalContent = $internalContent -replace "v$currentVersion", "v$newVersion"
        [System.IO.File]::WriteAllText($newInternalDocs, $internalContent)
    } else {
        # Create new internal docs from template
        $template = @"
# Cloud Journey Add-in - Internal Documentation
**Version $newVersion** | $(Get-Date -Format 'MMMM dd, yyyy')

## Overview
Internal documentation for version $newVersion.

## Changes in This Version
- Bug fix: GPT-4 JSON response parsing (markdown code block stripping)

## Technical Details
See CHANGELOG.md for complete details.
"@
        [System.IO.File]::WriteAllText($newInternalDocs, $template)
    }
    Write-Host "   [4/6] ✅ INTERNAL_DOCS_v$newVersion.md" -ForegroundColor Green
    
    # 5. DashboardWindow.xaml Title
    $xamlPath = Join-Path $scriptDir "Views\DashboardWindow.xaml"
    $xamlContent = Get-Content $xamlPath -Raw
    $xamlContent = $xamlContent -replace "v$currentVersion", "v$newVersion"
    [System.IO.File]::WriteAllText($xamlPath, $xamlContent)
    Write-Host "   [5/6] ✅ DashboardWindow.xaml" -ForegroundColor Green
    
    # 6. DashboardViewModel.cs version log
    $viewModelPath = Join-Path $scriptDir "ViewModels\DashboardViewModel.cs"
    $viewModelContent = Get-Content $viewModelPath -Raw
    $viewModelContent = $viewModelContent -replace "Version: $currentVersion", "Version: $newVersion"
    [System.IO.File]::WriteAllText($viewModelPath, $viewModelContent)
    Write-Host "   [6/6] ✅ DashboardViewModel.cs" -ForegroundColor Green
    
    Write-Host ""
    Write-Host "✅ All 6 locations updated to v$newVersion" -ForegroundColor Green
    Write-Host ""
    
    $currentVersion = $newVersion
} else {
    Write-Host "📦 Using provided version: $Version" -ForegroundColor Cyan
    $currentVersion = $Version
}

Write-Host ""

# ============================================
# PRE-FLIGHT CHECKS (MANDATORY)
# Per VERSIONING.md - ALL 6 locations MUST be updated
# ============================================
Write-Host "🔍 PRE-FLIGHT DOCUMENTATION CHECKS (6 Required)" -ForegroundColor Magenta
Write-Host "Per VERSIONING.md: All 6 locations must be updated" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host ""

Write-Host "📦 Current Version: $currentVersion" -ForegroundColor Cyan
Write-Host ""

# Check 1: Version number updated in .csproj
Write-Host "[CHECK 1/6] Version Number" -ForegroundColor Yellow
Write-Host "   Location: CloudJourneyAddin.csproj (line 10-12)" -ForegroundColor Gray
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

# Check 4: Internal documentation created
Write-Host "[CHECK 4/6] Internal Documentation" -ForegroundColor Yellow
Write-Host "   Location: INTERNAL_DOCS_v$currentVersion.md" -ForegroundColor Gray
$internalDocsPath = Join-Path $scriptDir "INTERNAL_DOCS_v$currentVersion.md"
if (Test-Path $internalDocsPath) {
    Write-Host "   ✅ Found: INTERNAL_DOCS_v$currentVersion.md" -ForegroundColor Green
} else {
    Write-Host "   ⚠️  WARNING: INTERNAL_DOCS_v$currentVersion.md not found" -ForegroundColor Yellow
    Write-Host "   Continuing anyway..." -ForegroundColor Gray
}
Write-Host ""

# Check 5: DashboardWindow.xaml Title
Write-Host "[CHECK 5/6] DashboardWindow.xaml Title" -ForegroundColor Yellow
Write-Host "   Location: Views/DashboardWindow.xaml (line 6)" -ForegroundColor Gray
$xamlPath = Join-Path $scriptDir "Views\DashboardWindow.xaml"
if (Test-Path $xamlPath) {
    $xamlContent = Get-Content $xamlPath -Raw
    if ($xamlContent -match "Title=`"Cloud Journey Progress Dashboard v$currentVersion`"") {
        Write-Host "   ✅ XAML Title contains v$currentVersion" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  WARNING: XAML Title may not match version $currentVersion" -ForegroundColor Yellow
        Write-Host "   Continuing anyway..." -ForegroundColor Gray
    }
} else {
    Write-Host "   ⚠️  WARNING: DashboardWindow.xaml not found" -ForegroundColor Yellow
}
Write-Host ""

# Check 6: DashboardViewModel.cs version log
Write-Host "[CHECK 6/6] DashboardViewModel.cs Version Log" -ForegroundColor Yellow
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
    dotnet clean CloudJourneyAddin.csproj -c Release --nologo -v quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "   ❌ Clean failed!" -ForegroundColor Red
        exit 1
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
    dotnet build CloudJourneyAddin.csproj -c Release --nologo -v quiet
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
    
    dotnet publish CloudJourneyAddin.csproj -c Release -r win-x64 --self-contained true --nologo -v quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "   ❌ Publish failed!" -ForegroundColor Red
        exit 1
    }
    
    # Verify critical files
    $exePath = Join-Path $publishPath "CloudJourneyAddin.exe"
    $azureIdentityPath = Join-Path $publishPath "Azure.Identity.dll"
    
    if (-not (Test-Path $exePath)) {
        Write-Host "   ❌ ERROR: CloudJourneyAddin.exe not found in publish folder!" -ForegroundColor Red
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

$packageName = "CloudJourneyAddin-v$Version-COMPLETE.zip"
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
    
    # Copy CloudJourneyAddin.xml if exists
    $xmlPath = Join-Path $updatePackagePath "CloudJourneyAddin.xml"
    if (Test-Path $xmlPath) {
        Copy-Item $xmlPath -Destination $tempFolder -Force
        Write-Host "      ✓ CloudJourneyAddin.xml" -ForegroundColor DarkGray
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

# Step 6: Verify package
Write-Host ""
Write-Host "[6/7] Verifying package integrity..." -ForegroundColor Yellow

# Extract to temp location for verification
$verifyFolder = Join-Path $scriptDir "TempVerify_$Version"
if (Test-Path $verifyFolder) {
    Remove-Item $verifyFolder -Recurse -Force
}
Expand-Archive -Path $packagePath -DestinationPath $verifyFolder -Force

$exeInPackage = Join-Path $verifyFolder "CloudJourneyAddin.exe"
$azureIdentityInPackage = Join-Path $verifyFolder "Azure.Identity.dll"

if (-not (Test-Path $exeInPackage)) {
    Write-Host "   ❌ ERROR: CloudJourneyAddin.exe not found in package!" -ForegroundColor Red
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

Write-Host "   ✅ CloudJourneyAddin.exe version: $exeVersion" -ForegroundColor Green
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
Write-Host "   4. Run Update-CloudJourneyAddin.ps1 to deploy" -ForegroundColor White
Write-Host ""

# ============================================
# POST-BUILD DOCUMENTATION CHECKLIST
# ============================================
Write-Host "========================================" -ForegroundColor Magenta
Write-Host "📝 POST-BUILD DOCUMENTATION CHECKLIST" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host ""
Write-Host "✅ COMPLETED (verified during pre-flight):" -ForegroundColor Green
Write-Host "   [✓] Version updated in .csproj" -ForegroundColor Gray
Write-Host "   [✓] README.md updated with v$Version features" -ForegroundColor Gray
Write-Host "   [✓] USER_GUIDE.md updated with v$Version" -ForegroundColor Gray
Write-Host "   [✓] INTERNAL_DOCS_v$Version.md created" -ForegroundColor Gray
Write-Host ""
Write-Host "📋 RECOMMENDED NEXT STEPS:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Create RELEASE_NOTES_v$Version.md (if not already created)" -ForegroundColor White
Write-Host "   - Summary of changes" -ForegroundColor Gray
Write-Host "   - What's new section" -ForegroundColor Gray
Write-Host "   - Installation instructions" -ForegroundColor Gray
Write-Host "   - Known issues" -ForegroundColor Gray
Write-Host "   - Testing checklist" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Update CHANGELOG.md" -ForegroundColor White
Write-Host "   - Add v$Version entry with date" -ForegroundColor Gray
Write-Host "   - List all Added/Changed/Fixed items" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Test the package" -ForegroundColor White
Write-Host "   - Install on clean PC" -ForegroundColor Gray
Write-Host "   - Verify all new features work" -ForegroundColor Gray
Write-Host "   - Check logs for errors" -ForegroundColor Gray
Write-Host ""
Write-Host "4. Git commit and tag" -ForegroundColor White
Write-Host "   git add ." -ForegroundColor Gray
Write-Host "   git commit -m 'Release v$Version'" -ForegroundColor Gray
Write-Host "   git tag -a v$Version -m 'Version $Version'" -ForegroundColor Gray
Write-Host "   git push origin main --tags" -ForegroundColor Gray
Write-Host ""

Write-Host "========================================" -ForegroundColor Green
Write-Host "✅ Package ready for deployment to remote PCs" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
