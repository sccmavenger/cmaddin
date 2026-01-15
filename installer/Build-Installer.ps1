<#
.SYNOPSIS
    Builds the Zero Trust Migration Journey Dashboard MSI installer

.DESCRIPTION
    This script automates building the WiX-based MSI installer package.
    It handles:
    - Building the main application
    - Generating the application files component (Heat.exe)
    - Building custom actions
    - Compiling the MSI
    - Optionally building the bootstrapper bundle

.PARAMETER BuildApp
    Build the application first before creating installer

.PARAMETER IncludeBundle
    Also build the bootstrapper bundle with .NET prerequisite

.PARAMETER SkipHeat
    Skip running Heat.exe to regenerate ApplicationFiles.wxs

.PARAMETER OutputPath
    Path to output the MSI file (default: ..\builds\)

.EXAMPLE
    .\Build-Installer.ps1
    Builds the MSI installer only

.EXAMPLE
    .\Build-Installer.ps1 -BuildApp -IncludeBundle
    Builds everything: application, MSI, and bootstrapper

.NOTES
    Requires: WiX Toolset v4 or later (`dotnet tool install --global wix`)
#>

[CmdletBinding()]
param(
    [switch]$BuildApp,
    [switch]$IncludeBundle,
    [switch]$SkipHeat,
    [string]$OutputPath = "..\builds"
)

$ErrorActionPreference = "Stop"
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath

# Colors for output
function Write-Header($message) { Write-Host "`n=== $message ===" -ForegroundColor Cyan }
function Write-Success($message) { Write-Host "✓ $message" -ForegroundColor Green }
function Write-Warning($message) { Write-Host "⚠ $message" -ForegroundColor Yellow }
function Write-Failure($message) { Write-Host "✗ $message" -ForegroundColor Red }

Write-Header "Zero Trust Migration Journey Dashboard - MSI Installer Build"

# Check prerequisites
Write-Host "`nChecking prerequisites..."

# Check WiX Toolset
try {
    $wixVersion = & wix --version 2>&1
    Write-Success "WiX Toolset: $wixVersion"
} catch {
    Write-Failure "WiX Toolset not found"
    Write-Host "Install it with: dotnet tool install --global wix"
    exit 1
}

# Check .NET SDK
try {
    $dotnetVersion = & dotnet --version
    Write-Success ".NET SDK: $dotnetVersion"
} catch {
    Write-Failure ".NET SDK not found"
    exit 1
}

# Create output directory
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath | Out-Null
    Write-Success "Created output directory: $OutputPath"
}

# Step 1: Build Application (if requested)
if ($BuildApp) {
    Write-Header "Building Application"
    
    Set-Location ".."
    
    Write-Host "Cleaning previous build..."
    & dotnet clean -c Release
    
    Write-Host "Building application..."
    & dotnet build -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Failure "Application build failed"
        exit 1
    }
    
    Write-Host "Publishing application..."
    & dotnet publish -c Release -r win-x64 --self-contained true
    if ($LASTEXITCODE -ne 0) {
        Write-Failure "Application publish failed"
        exit 1
    }
    
    Write-Success "Application built successfully"
    
    Set-Location "installer"
}

# Verify publish folder exists
$publishPath = "..\bin\Release\net8.0-windows\win-x64\publish"
if (-not (Test-Path $publishPath)) {
    Write-Failure "Publish folder not found: $publishPath"
    Write-Host "Run with -BuildApp to build the application first"
    exit 1
}

$fileCount = (Get-ChildItem -Path $publishPath -Recurse -File).Count
Write-Success "Found $fileCount files in publish folder"

# Step 2: Generate Application Files Component with Heat
if (-not $SkipHeat) {
    Write-Header "Generating Application Files Component"
    
    Write-Host "Running Heat.exe to harvest application files..."
    
    $heatArgs = @(
        "dir", $publishPath,
        "-cg", "ApplicationFiles",
        "-dr", "INSTALLFOLDER",
        "-gg",
        "-sfrag",
        "-srd",
        "-var", "var.PublishDir",
        "-out", "ApplicationFiles.wxs"
    )
    
    & heat $heatArgs
    
    if ($LASTEXITCODE -ne 0) {
        Write-Failure "Heat.exe failed"
        exit 1
    }
    
    if (Test-Path "ApplicationFiles.wxs") {
        $lines = (Get-Content "ApplicationFiles.wxs").Count
        Write-Success "Generated ApplicationFiles.wxs ($lines lines)"
    } else {
        Write-Failure "ApplicationFiles.wxs not created"
        exit 1
    }
}

# Step 3: Build Custom Actions
Write-Header "Building Custom Actions"

Set-Location "CustomActions"

Write-Host "Building CustomActions project..."
& dotnet build -c Release -p:Platform=x64

if ($LASTEXITCODE -ne 0) {
    Write-Failure "Custom actions build failed"
    Set-Location ".."
    exit 1
}

# Find the custom action DLL
$customActionDll = Get-ChildItem -Path "bin\Release" -Filter "CustomActions.CA.dll" -Recurse | Select-Object -First 1

if ($customActionDll) {
    Write-Success "Custom actions built: $($customActionDll.FullName)"
    
    # Copy to installer root for easy reference
    Copy-Item $customActionDll.FullName "..\CustomActions.CA.dll" -Force
} else {
    Write-Warning "CustomActions.CA.dll not found - MSI will build without custom actions"
}

Set-Location ".."

# Step 4: Build MSI
Write-Header "Building MSI Package"

Write-Host "Compiling MSI..."

$wixBuildArgs = @(
    "build",
    "Product.wxs",
    "ApplicationFiles.wxs",
    "-ext", "WixToolset.UI.wixext",
    "-d", "PublishDir=$publishPath",
    "-out", "$OutputPath\ZeroTrustMigrationAddin.msi"
)

& wix $wixBuildArgs

if ($LASTEXITCODE -ne 0) {
    Write-Failure "MSI build failed"
    exit 1
}

$msiPath = Join-Path (Resolve-Path $OutputPath) "ZeroTrustMigrationAddin.msi"
if (Test-Path $msiPath) {
    $msiSize = [math]::Round((Get-Item $msiPath).Length / 1MB, 2)
    Write-Success "MSI created: $msiPath ($msiSize MB)"
} else {
    Write-Failure "MSI file not created"
    exit 1
}

# Step 5: Build Bootstrapper Bundle (if requested)
if ($IncludeBundle) {
    Write-Header "Building Bootstrapper Bundle"
    
    Write-Host "Compiling bootstrapper..."
    
    $bundleArgs = @(
        "build",
        "Bundle.wxs",
        "-ext", "WixToolset.Bal.wixext",
        "-ext", "WixToolset.Util.wixext",
        "-out", "$OutputPath\ZeroTrustMigrationAddin-Setup.exe"
    )
    
    & wix $bundleArgs
    
    if ($LASTEXITCODE -ne 0) {
        Write-Failure "Bundle build failed"
        exit 1
    }
    
    $bundlePath = Join-Path (Resolve-Path $OutputPath) "ZeroTrustMigrationAddin-Setup.exe"
    if (Test-Path $bundlePath) {
        $bundleSize = [math]::Round((Get-Item $bundlePath).Length / 1MB, 2)
        Write-Success "Bootstrapper created: $bundlePath ($bundleSize MB)"
    } else {
        Write-Failure "Bootstrapper file not created"
        exit 1
    }
}

# Summary
Write-Header "Build Complete"

Write-Host "`nOutput files:"
Get-ChildItem $OutputPath -Filter "ZeroTrustMigration*.*" | ForEach-Object {
    $size = [math]::Round($_.Length / 1MB, 2)
    Write-Host "  $($_.Name) - $size MB" -ForegroundColor White
}

Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "  1. Test MSI on clean VM with ConfigMgr Console"
Write-Host "  2. Verify ConfigMgr ribbon button appears"
Write-Host "  3. Test upgrade from old CloudJourneyAddin version"
Write-Host "  4. Distribute via SCCM/Intune or GitHub Releases"

Write-Success "`nBuild completed successfully!"
