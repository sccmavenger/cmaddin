#Requires -Version 5.1

<#
.SYNOPSIS
    Quick publish script to build and push a release to GitHub

.DESCRIPTION
    Simplified script that builds the application and publishes to GitHub Releases.
    This is a streamlined version of Build-And-Distribute.ps1 focused on GitHub publishing.

.PARAMETER BumpVersion
    Version component to increment (Major, Minor, or Patch). Default: Patch

.PARAMETER ReleaseNotes
    Custom release notes for the GitHub release.

.PARAMETER Draft
    Create as draft release (not visible to public until published).

.PARAMETER Prerelease
    Mark as prerelease version.

.EXAMPLE
    .\Publish-ToGitHub.ps1
    # Build and publish with patch version bump

.EXAMPLE
    .\Publish-ToGitHub.ps1 -BumpVersion Minor -ReleaseNotes "Added new enrollment features"
    # Build and publish with minor version bump and custom notes

.EXAMPLE
    .\Publish-ToGitHub.ps1 -Draft
    # Create a draft release for review before publishing
#>

[CmdletBinding()]
param(
    [ValidateSet('Major', 'Minor', 'Patch')]
    [string]$BumpVersion = 'Patch',
    [string]$ReleaseNotes,
    [switch]$Draft,
    [switch]$Prerelease
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  Zero Trust Migration Journey - GitHub Publish             ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Check for gh CLI
$ghPath = Get-Command gh -ErrorAction SilentlyContinue
if (-not $ghPath) {
    Write-Host "❌ GitHub CLI (gh) is required but not installed." -ForegroundColor Red
    Write-Host ""
    Write-Host "Install it from: https://cli.github.com/" -ForegroundColor Yellow
    Write-Host "Or run: winget install GitHub.cli" -ForegroundColor Yellow
    exit 1
}

# Check gh authentication
$authStatus = gh auth status 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Not authenticated to GitHub. Run: gh auth login" -ForegroundColor Red
    exit 1
}

Write-Host "✅ GitHub CLI authenticated" -ForegroundColor Green

# Get current version from csproj
$csprojPath = Join-Path $scriptDir "ZeroTrustMigrationAddin.csproj"
[xml]$csproj = Get-Content $csprojPath
$currentVersion = $csproj.Project.PropertyGroup.Version
Write-Host "📋 Current version: $currentVersion" -ForegroundColor Cyan

# Calculate new version
$parts = $currentVersion -split '\.'
$major = [int]$parts[0]
$minor = [int]$parts[1]
$patch = [int]$parts[2]

switch ($BumpVersion) {
    "Major" { $major++; $minor = 0; $patch = 0 }
    "Minor" { $minor++; $patch = 0 }
    "Patch" { $patch++ }
}
$newVersion = "$major.$minor.$patch"

Write-Host "🆕 New version: $newVersion" -ForegroundColor Green
Write-Host ""

# Update version in project files
Write-Host "📝 Updating version in project files..." -ForegroundColor Yellow

# Update csproj
$csprojContent = Get-Content $csprojPath -Raw
$csprojContent = $csprojContent -replace '<Version>[\d\.]+</Version>', "<Version>$newVersion</Version>"
$csprojContent = $csprojContent -replace '<AssemblyVersion>[\d\.]+</AssemblyVersion>', "<AssemblyVersion>$newVersion.0</AssemblyVersion>"
$csprojContent = $csprojContent -replace '<FileVersion>[\d\.]+</FileVersion>', "<FileVersion>$newVersion.0</FileVersion>"
[System.IO.File]::WriteAllText($csprojPath, $csprojContent)

# Update XML manifest
$xmlPath = Join-Path $scriptDir "ZeroTrustMigrationAddin.xml"
if (Test-Path $xmlPath) {
    $xmlContent = Get-Content $xmlPath -Raw
    $xmlContent = $xmlContent -replace 'Version="[\d\.]+"', "Version=`"$newVersion`""
    [System.IO.File]::WriteAllText($xmlPath, $xmlContent)
}

Write-Host "✅ Version updated" -ForegroundColor Green

# Build the application
Write-Host ""
Write-Host "🔨 Building application..." -ForegroundColor Yellow
$publishPath = Join-Path $scriptDir "publish"

# Clean publish folder
if (Test-Path $publishPath) {
    Remove-Item -Path $publishPath -Recurse -Force
}

# Run dotnet publish
$buildResult = & dotnet publish $csprojPath --configuration Release --output $publishPath --self-contained true --runtime win-x64 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed:" -ForegroundColor Red
    Write-Host $buildResult
    exit 1
}

Write-Host "✅ Build succeeded" -ForegroundColor Green

# Generate manifest.json for delta updates
Write-Host ""
Write-Host "📄 Generating manifest.json..." -ForegroundColor Yellow

$files = Get-ChildItem -Path $publishPath -Recurse -File | ForEach-Object {
    $relativePath = $_.FullName.Substring($publishPath.Length + 1)
    $hash = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash.ToLower()
    @{
        RelativePath = $relativePath -replace '\\', '/'
        FileSize = $_.Length
        SHA256Hash = $hash
        LastModified = $_.LastWriteTimeUtc.ToString("o")
        IsCritical = $relativePath -match '(ZeroTrustMigrationAddin\.dll|Azure\.Identity\.dll)$'
    }
}

$totalSize = ($files | Measure-Object -Property FileSize -Sum).Sum
$manifest = @{
    Version = $newVersion
    TotalSize = $totalSize
    BuildDate = (Get-Date).ToUniversalTime().ToString("o")
    Files = $files
}

$manifestPath = Join-Path $publishPath "manifest.json"
$manifest | ConvertTo-Json -Depth 10 | Set-Content -Path $manifestPath

# Also save versioned manifest to builds folder
$buildsDir = Join-Path $scriptDir "builds\manifests"
if (!(Test-Path $buildsDir)) {
    New-Item -ItemType Directory -Path $buildsDir -Force | Out-Null
}
Copy-Item $manifestPath (Join-Path $buildsDir "manifest-v$newVersion.json")

Write-Host "✅ Manifest generated ($($files.Count) files, $([math]::Round($totalSize / 1MB, 2)) MB)" -ForegroundColor Green

# Create ZIP package
Write-Host ""
Write-Host "📦 Creating ZIP package..." -ForegroundColor Yellow

$buildsOutput = Join-Path $scriptDir "builds"
if (!(Test-Path $buildsOutput)) {
    New-Item -ItemType Directory -Path $buildsOutput -Force | Out-Null
}

$zipName = "ZeroTrustMigrationAddin-v$newVersion.zip"
$zipPath = Join-Path $buildsOutput $zipName

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path "$publishPath\*" -DestinationPath $zipPath -Force
$zipHash = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToLower()
$zipSize = (Get-Item $zipPath).Length

Write-Host "✅ Package created: $zipName" -ForegroundColor Green
Write-Host "   Size: $([math]::Round($zipSize / 1MB, 2)) MB" -ForegroundColor Gray
Write-Host "   SHA256: $zipHash" -ForegroundColor Gray

# Create GitHub Release
Write-Host ""
Write-Host "🚀 Publishing to GitHub..." -ForegroundColor Yellow

$releaseTag = "v$newVersion"
$releaseTitle = "Zero Trust Migration Journey v$newVersion"

# Prepare release notes
if (-not $ReleaseNotes) {
    $ReleaseNotes = @"
## Zero Trust Migration Journey Add-in v$newVersion

### 📦 Installation

**New Installation:**
1. Download ``ZeroTrustMigrationAddin-v$newVersion.zip``
2. Extract to ConfigMgr Console Extensions folder
3. Restart ConfigMgr Console

**Update Existing:**
- Use the built-in auto-update feature, or
- Download and replace existing files

### 🔄 Delta Updates
The ``manifest.json`` file enables delta updates - only changed files are downloaded.

### ✅ Verification
``````powershell
# Verify download integrity
(Get-FileHash -Path "ZeroTrustMigrationAddin-v$newVersion.zip" -Algorithm SHA256).Hash
# Expected: $zipHash
``````

See CHANGELOG.md for detailed changes.
"@
}

# Write release notes to temp file
$notesFile = Join-Path $env:TEMP "release-notes-$newVersion.md"
$ReleaseNotes | Set-Content -Path $notesFile

# Build gh release command
$ghArgs = @("release", "create", $releaseTag)
$ghArgs += "--title", $releaseTitle
$ghArgs += "--notes-file", $notesFile

if ($Draft) {
    $ghArgs += "--draft"
}

if ($Prerelease) {
    $ghArgs += "--prerelease"
}

# Add assets
$ghArgs += $zipPath
$ghArgs += (Join-Path $buildsDir "manifest-v$newVersion.json")

Write-Host "Running: gh $($ghArgs -join ' ')" -ForegroundColor Gray

# Create the release
try {
    $result = & gh @ghArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw $result
    }
    
    Write-Host ""
    Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host "✅ Release v$newVersion published successfully!" -ForegroundColor Green
    Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host ""
    Write-Host "🔗 View release: $result" -ForegroundColor Cyan
    Write-Host ""
    
    # Commit version changes
    Write-Host "📝 Committing version changes..." -ForegroundColor Yellow
    git add $csprojPath
    if (Test-Path $xmlPath) { git add $xmlPath }
    git commit -m "Bump version to $newVersion" 2>$null
    git push 2>$null
    
    Write-Host "✅ Changes committed and pushed" -ForegroundColor Green
}
catch {
    Write-Host "❌ Failed to create release: $_" -ForegroundColor Red
    exit 1
}
finally {
    # Cleanup
    if (Test-Path $notesFile) {
        Remove-Item $notesFile -Force
    }
}

Write-Host ""
Write-Host "🎉 Done! Other applications can now update from:" -ForegroundColor Green
Write-Host "   https://github.com/sccmavenger/cmaddin/releases/latest" -ForegroundColor Cyan
