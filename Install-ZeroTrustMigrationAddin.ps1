
<#
.SYNOPSIS
    Automated installer for ConfigMgr Cloud Native Assessment Add-in
.DESCRIPTION
    This script automatically:
    - Checks for prerequisites
    - Installs .NET 8.0 Runtime if needed
    - Builds the application with all dependencies
    - Deploys to ConfigMgr Console
    - Validates the installation
.PARAMETER SkipBuild
    Skip the build step and use existing binaries
.PARAMETER ConfigMgrPath
    Custom ConfigMgr Console installation path
.EXAMPLE
    .\Install-ZeroTrustMigrationAddin.ps1
.EXAMPLE
    .\Install-ZeroTrustMigrationAddin.ps1 -SkipBuild -ConfigMgrPath "C:\Custom\Path"
#>

[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [string]$ConfigMgrPath
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Colors for output
function Write-Success { Write-Host "[OK] $args" -ForegroundColor Green }
function Write-Info { Write-Host "[INFO] $args" -ForegroundColor Cyan }
function Write-Warning { Write-Host "[WARN] $args" -ForegroundColor Yellow }
function Write-Error { Write-Host "[ERROR] $args" -ForegroundColor Red }

Write-Host "`n===================================================" -ForegroundColor Cyan
Write-Host "   ConfigMgr Cloud Native Assessment Add-in Installer" -ForegroundColor Cyan
Write-Host "===================================================`n" -ForegroundColor Cyan

# ============================================================================
# Step 1: Check Administrator Privileges
# ============================================================================
Write-Info "Checking administrator privileges..."
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Warning "Administrator privileges required for installation."
    Write-Info "Restarting script with elevated privileges..."
    
    $scriptPath = $MyInvocation.MyCommand.Path
    $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`""
    if ($SkipBuild) { $arguments += " -SkipBuild" }
    if ($ConfigMgrPath) { $arguments += " -ConfigMgrPath `"$ConfigMgrPath`"" }
    
    Start-Process PowerShell -Verb RunAs -ArgumentList $arguments
    exit
}

Write-Success "Running with administrator privileges"

# ============================================================================
# Step 2: Detect ConfigMgr Console Installation
# ============================================================================
Write-Info "Detecting ConfigMgr Console installation..."

if (-not $ConfigMgrPath) {
    # Method 1: Try registry first
    try {
        $regPath = "HKLM:\SOFTWARE\Microsoft\ConfigMgr10\Setup"
        if (Test-Path $regPath) {
            $uiInstallPath = (Get-ItemProperty -Path $regPath -Name "UI Install Path" -ErrorAction SilentlyContinue)."UI Install Path"
            if ($uiInstallPath -and (Test-Path $uiInstallPath)) {
                $ConfigMgrPath = $uiInstallPath.TrimEnd('\\')
                Write-Info "Detected from registry: $ConfigMgrPath"
            }
        }
    } catch { }
    
    # Method 2: Try environment variable
    if (-not $ConfigMgrPath -and $env:SMS_ADMIN_UI_PATH -and (Test-Path $env:SMS_ADMIN_UI_PATH)) {
        $ConfigMgrPath = $env:SMS_ADMIN_UI_PATH.TrimEnd('\\')
        Write-Info "Detected from SMS_ADMIN_UI_PATH: $ConfigMgrPath"
    }
    
    # Method 3: Check common paths
    if (-not $ConfigMgrPath) {
        $possiblePaths = @(
            "${env:ProgramFiles(x86)}\Microsoft Configuration Manager\AdminConsole",
            "${env:ProgramFiles}\Microsoft Configuration Manager\AdminConsole"
        )
        
        # Also check all drives D-F
        foreach ($drive in @('D','E','F')) {
            $possiblePaths += "${drive}:\Program Files (x86)\Microsoft Configuration Manager\AdminConsole"
            $possiblePaths += "${drive}:\Program Files\Microsoft Configuration Manager\AdminConsole"
        }
        
        foreach ($path in $possiblePaths) {
            if (Test-Path "$path\bin\Microsoft.ConfigurationManagement.exe") {
                $ConfigMgrPath = $path
                Write-Info "Found at: $ConfigMgrPath"
                break
            }
        }
    }
}

if (-not $ConfigMgrPath -or -not (Test-Path $ConfigMgrPath)) {
    Write-Error "ConfigMgr Console not found. Please specify the path using -ConfigMgrPath parameter."
    Write-Info "Example: .\Install-ZeroTrustMigrationAddin.ps1 -ConfigMgrPath 'F:\Program Files\Microsoft Configuration Manager\AdminConsole'"
    exit 1
}

Write-Success "Found ConfigMgr Console at: $ConfigMgrPath"

$extensionsPath = Join-Path $ConfigMgrPath "XmlStorage\Extensions\Actions"
$binPath = Join-Path $ConfigMgrPath "bin"

# ============================================================================
# Step 3: Check and Install .NET 8.0 Runtime
# ============================================================================
Write-Info "Checking .NET 8.0 Runtime..."

$dotnetVersions = dotnet --list-runtimes 2>$null | Where-Object { $_ -match "Microsoft.WindowsDesktop.App 8\." }

if (-not $dotnetVersions) {
    Write-Warning ".NET 8.0 Desktop Runtime not found. Installing..."
    
    $installerUrl = "https://download.visualstudio.microsoft.com/download/pr/6224f00f-08da-4e7f-85b1-00d42c2bb3d3/b775de636b91e023574a0bbc291f705a/windowsdesktop-runtime-8.0.11-win-x64.exe"
    $installerPath = Join-Path $env:TEMP "windowsdesktop-runtime-8.0.11-win-x64.exe"
    
    try {
        Write-Info "Downloading .NET 8.0 Desktop Runtime..."
        Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing
        
        Write-Info "Installing .NET 8.0 Desktop Runtime (this may take a few minutes)..."
        $process = Start-Process -FilePath $installerPath -ArgumentList "/install", "/quiet", "/norestart" -Wait -PassThru
        
        if ($process.ExitCode -eq 0 -or $process.ExitCode -eq 3010) {
            Write-Success ".NET 8.0 Desktop Runtime installed successfully"
            Remove-Item $installerPath -Force -ErrorAction SilentlyContinue
        } else {
            Write-Error "Failed to install .NET 8.0 Runtime (Exit code: $($process.ExitCode))"
            exit 1
        }
    }
    catch {
        Write-Error "Failed to download or install .NET 8.0 Runtime: $_"
        exit 1
    }
} else {
    Write-Success ".NET 8.0 Desktop Runtime is already installed"
    Write-Info "Installed version: $($dotnetVersions[0])"
}

# ============================================================================
# Step 4: Build the Application
# ============================================================================
if (-not $SkipBuild) {
    Write-Info "Building Cloud Native Assessment Add-in with all dependencies..."
    
    $projectPath = Join-Path $PSScriptRoot "ZeroTrustMigrationAddin.csproj"
    
    if (-not (Test-Path $projectPath)) {
        Write-Error "Project file not found: $projectPath"
        exit 1
    }
    
    # Clean previous builds
    Write-Info "Cleaning previous builds..."
    dotnet clean -c Release --nologo -v quiet 2>$null
    
    # Build self-contained with all dependencies
    Write-Info "Building self-contained application..."
    $buildOutput = dotnet publish -c Release --self-contained true -r win-x64 -p:PublishSingleFile=false -p:PublishReadyToRun=true --nologo 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed!"
        Write-Host $buildOutput
        exit 1
    }
    
    Write-Success "Build completed successfully"
} else {
    Write-Info "Skipping build (using existing binaries)"
}

$publishPath = Join-Path $PSScriptRoot "bin\Release\net8.0-windows\win-x64\publish"

if (-not (Test-Path $publishPath)) {
    Write-Error "Published binaries not found at: $publishPath"
    Write-Info "Run without -SkipBuild to build the application first."
    exit 1
}

# ============================================================================
# Step 5: Close ConfigMgr Console
# ============================================================================
Write-Info "Checking if ConfigMgr Console is running..."

$consoleProcess = Get-Process -Name "Microsoft.ConfigurationManagement" -ErrorAction SilentlyContinue

if ($consoleProcess) {
    Write-Warning "ConfigMgr Console is currently running and must be closed."
    $response = Read-Host "Close ConfigMgr Console now? (Y/N)"
    
    if ($response -eq 'Y' -or $response -eq 'y') {
        Write-Info "Closing ConfigMgr Console..."
        $consoleProcess | Stop-Process -Force
        Start-Sleep -Seconds 2
        Write-Success "ConfigMgr Console closed"
    } else {
        Write-Error "Installation cancelled. Please close ConfigMgr Console and try again."
        exit 1
    }
}

# ============================================================================
# Step 6: Deploy XML Manifest
# ============================================================================
Write-Info "Deploying XML manifest..."

if (-not (Test-Path $extensionsPath)) {
    Write-Info "Creating extensions directory..."
    New-Item -ItemType Directory -Path $extensionsPath -Force | Out-Null
}

$xmlSource = Join-Path $PSScriptRoot "ZeroTrustMigrationAddin.xml"
$xmlDest = Join-Path $extensionsPath "ZeroTrustMigrationAddin.xml"

if (-not (Test-Path $xmlSource)) {
    Write-Error "XML manifest not found: $xmlSource"
    exit 1
}

Copy-Item $xmlSource -Destination $xmlDest -Force
Write-Success "XML manifest deployed to: $xmlDest"

# ============================================================================
# Step 7: Deploy Application Binaries
# ============================================================================
Write-Info "Deploying application binaries..."

$addInPath = Join-Path $binPath "ZeroTrustMigrationAddin"

# Create dedicated folder for add-in
if (-not (Test-Path $addInPath)) {
    New-Item -ItemType Directory -Path $addInPath -Force | Out-Null
}

# Copy all published files
$filesToCopy = Get-ChildItem -Path $publishPath -Recurse -File

$fileCount = 0
foreach ($file in $filesToCopy) {
    $relativePath = $file.FullName.Substring($publishPath.Length + 1)
    $destPath = Join-Path $addInPath $relativePath
    $destDir = Split-Path $destPath -Parent
    
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }
    
    Copy-Item $file.FullName -Destination $destPath -Force
    $fileCount++
}

Write-Success "Deployed $fileCount files to: $addInPath"

# Update XML to point to new location
$xmlContent = Get-Content $xmlDest -Raw
$xmlContent = $xmlContent -replace '<FilePath>ZeroTrustMigrationAddin\.exe</FilePath>', "<FilePath>ZeroTrustMigrationAddin\ZeroTrustMigrationAddin.exe</FilePath>"
Set-Content -Path $xmlDest -Value $xmlContent

# ============================================================================
# Step 8: Create Uninstaller
# ============================================================================
Write-Info "Creating uninstaller..."

$uninstallScript = @"
# Cloud Native Assessment Add-in Uninstaller
`$ErrorActionPreference = "Stop"

Write-Host "Uninstalling Cloud Native Assessment Add-in..." -ForegroundColor Yellow

# Close ConfigMgr Console if running
`$process = Get-Process -Name "Microsoft.ConfigurationManagement" -ErrorAction SilentlyContinue
if (`$process) {
    Write-Host "Closing ConfigMgr Console..."
    `$process | Stop-Process -Force
    Start-Sleep -Seconds 2
}

# Remove XML manifest
`$xmlPath = "$xmlDest"
if (Test-Path `$xmlPath) {
    Remove-Item `$xmlPath -Force
    Write-Host "✓ Removed XML manifest" -ForegroundColor Green
}

# Remove binaries
`$binPath = "$addInPath"
if (Test-Path `$binPath) {
    Remove-Item `$binPath -Recurse -Force
    Write-Host "✓ Removed application files" -ForegroundColor Green
}

Write-Host "`nCloud Native Assessment Add-in has been uninstalled." -ForegroundColor Green
Write-Host "You can now restart ConfigMgr Console." -ForegroundColor Cyan
Pause
"@

$uninstallPath = Join-Path $PSScriptRoot "Uninstall-ZeroTrustMigrationAddin.ps1"
Set-Content -Path $uninstallPath -Value $uninstallScript
Write-Success "Uninstaller created: $uninstallPath"

# ============================================================================
# Step 9: Validation
# ============================================================================
Write-Info "Validating installation..."

$validationErrors = @()

if (-not (Test-Path $xmlDest)) {
    $validationErrors += "XML manifest not found at expected location"
}

if (-not (Test-Path (Join-Path $addInPath "ZeroTrustMigrationAddin.exe"))) {
    $validationErrors += "Main executable not found"
}

if (-not (Test-Path (Join-Path $addInPath "ZeroTrustMigrationAddin.dll"))) {
    $validationErrors += "Main assembly not found"
}

if ($validationErrors.Count -gt 0) {
    Write-Error "Installation validation failed:"
    $validationErrors | ForEach-Object { Write-Error "  - $_" }
    exit 1
}

Write-Success "Installation validation passed"

# ============================================================================
# Installation Complete
# ============================================================================
Write-Host "`n===================================================" -ForegroundColor Green
Write-Host "   Installation Completed Successfully!" -ForegroundColor Green
Write-Host "===================================================`n" -ForegroundColor Green

Write-Host "Installation Summary:" -ForegroundColor Cyan
Write-Host "  - ConfigMgr Console: $ConfigMgrPath" -ForegroundColor White
Write-Host "  - XML Manifest: $xmlDest" -ForegroundColor White
Write-Host "  - Application Files: $addInPath" -ForegroundColor White
Write-Host "  - Files Deployed: $fileCount" -ForegroundColor White

Write-Host "`nNext Steps:" -ForegroundColor Yellow
Write-Host "  1. Launch ConfigMgr Console" -ForegroundColor White
Write-Host "  2. Look for Cloud Native Assessment in the ribbon" -ForegroundColor White
Write-Host "  3. Click to open the dashboard" -ForegroundColor White

Write-Host "`nTo uninstall, run: .\Uninstall-ZeroTrustMigrationAddin.ps1" -ForegroundColor Gray

Write-Host "`nPress any key to exit..."
$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
