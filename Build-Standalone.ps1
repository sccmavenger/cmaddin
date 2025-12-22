
<#
.SYNOPSIS
    Build standalone self-contained package
.DESCRIPTION
    Creates a fully self-contained deployment package with all dependencies
#>

[CmdletBinding()]
param(
    [switch]$CreateZip
)

$ErrorActionPreference = "Stop"

Write-Host "`nBuilding Cloud Journey Add-in (Standalone Package)...`n" -ForegroundColor Cyan

# Clean
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean CloudJourneyAddin.csproj -c Release --nologo -v quiet

# Publish self-contained
Write-Host "Publishing self-contained application..." -ForegroundColor Yellow
dotnet publish CloudJourneyAddin.csproj -c Release `
    --self-contained true `
    -r win-x64 `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

$publishPath = "bin\Release\net8.0-windows\win-x64\publish"
$fileCount = (Get-ChildItem -Path $publishPath -Recurse -File).Count
$totalSize = [math]::Round((Get-ChildItem -Path $publishPath -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB, 2)

Write-Host "`n✓ Build successful!" -ForegroundColor Green
Write-Host "  Location: $publishPath" -ForegroundColor White
Write-Host "  Files: $fileCount" -ForegroundColor White
Write-Host "  Size: $totalSize MB" -ForegroundColor White

if ($CreateZip) {
    Write-Host "`nCreating deployment package..." -ForegroundColor Yellow
    
    $zipPath = "bin\CloudJourneyAddin-Standalone.zip"
    
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }
    
    # Include installer scripts
    $tempPath = Join-Path $env:TEMP "CloudJourneyAddin-Package"
    if (Test-Path $tempPath) {
        Remove-Item $tempPath -Recurse -Force
    }
    
    New-Item -ItemType Directory -Path $tempPath -Force | Out-Null
    New-Item -ItemType Directory -Path "$tempPath\bin" -Force | Out-Null
    
    Copy-Item -Path $publishPath -Destination "$tempPath\bin\Release\net8.0-windows\win-x64\publish" -Recurse -Force
    Copy-Item -Path "Install-CloudJourneyAddin.ps1" -Destination $tempPath -Force
    Copy-Item -Path "CloudJourneyAddin.xml" -Destination $tempPath -Force
    Copy-Item -Path "README.md" -Destination $tempPath -Force -ErrorAction SilentlyContinue
    
    # Create README for package
    $packageReadme = @"
# Cloud Journey Progress Add-in - Standalone Package

This package includes everything needed to install the add-in.

## Installation

1. Extract this ZIP file to a folder
2. Right-click 'Install-CloudJourneyAddin.ps1' and select 'Run with PowerShell'
3. Follow the on-screen prompts

The installer will:
- Check for administrator privileges
- Install .NET 8.0 Runtime if needed (automatic download)
- Deploy the add-in to ConfigMgr Console
- Configure all necessary components

## Requirements

- Windows 10/11 or Windows Server 2019+
- ConfigMgr Console 2103 or later
- Internet connection (for .NET Runtime download if needed)

## No Manual Setup Required

All prerequisites are automatically downloaded and installed by the installer script.
"@
    
    Set-Content -Path "$tempPath\INSTALLATION.txt" -Value $packageReadme
    
    Compress-Archive -Path "$tempPath\*" -DestinationPath $zipPath -Force
    Remove-Item $tempPath -Recurse -Force
    
    $zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
    
    Write-Host "`n✓ Deployment package created!" -ForegroundColor Green
    Write-Host "  Location: $zipPath" -ForegroundColor White
    Write-Host "  Size: $zipSize MB" -ForegroundColor White
}

Write-Host "`nTo install: .\Install-CloudJourneyAddin.ps1" -ForegroundColor Cyan
Write-Host ""
