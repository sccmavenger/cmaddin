#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Updates Cloud Journey Dashboard to latest version
.DESCRIPTION
    Updates existing CloudJourneyAddin installation with new version from extracted folder or zip
.EXAMPLE
    .\Update-CloudJourneyAddin.ps1
#>

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Cloud Journey Dashboard - Update Tool" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Find existing installation
Write-Host "[INFO] Searching for existing installation..." -ForegroundColor Cyan

# Detect ConfigMgr Console path from registry or environment
$consolePaths = @()

# Method 1: Registry
try {
    $regPath = "HKLM:\SOFTWARE\Microsoft\ConfigMgr10\Setup"
    if (Test-Path $regPath) {
        $uiInstallPath = (Get-ItemProperty -Path $regPath -Name "UI Install Path" -ErrorAction SilentlyContinue)."UI Install Path"
        if ($uiInstallPath -and (Test-Path $uiInstallPath)) {
            $consolePaths += $uiInstallPath.TrimEnd('\\')
            Write-Host "[OK] Detected Console path from registry: $uiInstallPath" -ForegroundColor Green
        }
    }
} catch { }

# Method 2: Environment variable
if ($env:SMS_ADMIN_UI_PATH -and (Test-Path $env:SMS_ADMIN_UI_PATH)) {
    $consolePaths += $env:SMS_ADMIN_UI_PATH.TrimEnd('\\')
    Write-Host "[OK] Detected Console path from SMS_ADMIN_UI_PATH: $($env:SMS_ADMIN_UI_PATH)" -ForegroundColor Green
}

# Method 3: Common paths as fallback
$consolePaths += @(
    "C:\Program Files (x86)\Microsoft Configuration Manager\AdminConsole",
    "C:\Program Files\Microsoft Configuration Manager\AdminConsole",
    "D:\Program Files (x86)\Microsoft Configuration Manager\AdminConsole",
    "D:\Program Files\Microsoft Configuration Manager\AdminConsole",
    "E:\Program Files (x86)\Microsoft Configuration Manager\AdminConsole",
    "E:\Program Files\Microsoft Configuration Manager\AdminConsole",
    "F:\Program Files (x86)\Microsoft Configuration Manager\AdminConsole",
    "F:\Program Files\Microsoft Configuration Manager\AdminConsole"
)

$existingInstall = $null
$searchedPaths = @()

foreach ($consolePath in ($consolePaths | Select-Object -Unique)) {
    try {
        # Check if path exists
        if (-not (Test-Path $consolePath -ErrorAction SilentlyContinue)) {
            continue
        }
        
        Write-Host "[INFO] Searching within: $consolePath" -ForegroundColor Cyan
        
        # Search recursively for CloudJourneyAddin.exe starting from bin folder
        $binPath = Join-Path $consolePath "bin"
        if (Test-Path $binPath) {
            $searchedPaths += $binPath
            # Find CloudJourneyAddin.exe that's in a CloudJourneyAddin folder
            $foundFiles = Get-ChildItem -Path $binPath -Filter "CloudJourneyAddin.exe" -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Directory.Name -eq "CloudJourneyAddin" }
            
            if ($foundFiles) {
                $existingInstall = $foundFiles[0].DirectoryName
                Write-Host "[OK] Found existing installation at: $existingInstall" -ForegroundColor Green
                break
            }
        }
    }
    catch {
        continue
    }
}

if (-not $existingInstall) {
    Write-Host "[ERROR] No existing installation found!" -ForegroundColor Red
    Write-Host "[INFO] Please run a fresh installation instead (Install-CloudJourneyAddin.ps1)." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Searched within:" -ForegroundColor Yellow
    foreach ($path in ($searchedPaths | Select-Object -Unique)) {
        Write-Host "  - $path (and subdirectories)" -ForegroundColor Gray
    }
    exit 1
}

# Check if we're running from an extracted folder (files already present)
$sourceExe = Join-Path $PSScriptRoot "CloudJourneyAddin.exe"
$useExtractedFiles = Test-Path $sourceExe

if ($useExtractedFiles) {
    Write-Host "[OK] Running from extracted folder - will use files directly" -ForegroundColor Green
    $updateSource = $PSScriptRoot
    $updatePackage = $null
} else {
    # Look for zip package
    $updatePackage = $null
    $possiblePackages = @(
        (Join-Path $PSScriptRoot "CloudJourneyAddin-v*.zip"),
        (Join-Path $PSScriptRoot "CloudJourneyAddin-Latest.zip")
    )

    foreach ($pattern in $possiblePackages) {
        $found = Get-ChildItem -Path $pattern -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) {
            $updatePackage = $found.FullName
            break
        }
    }
    
    if (-not $updatePackage -or -not (Test-Path $updatePackage)) {
        Write-Host "[ERROR] Update package not found!" -ForegroundColor Red
        Write-Host "[INFO] Looking for: CloudJourneyAddin-v*.zip or CloudJourneyAddin-Latest.zip" -ForegroundColor Yellow
        Write-Host "[INFO] In folder: $PSScriptRoot" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Files in current folder:" -ForegroundColor Gray
        Get-ChildItem $PSScriptRoot -Filter "*.zip" | ForEach-Object { Write-Host "  - $($_.Name)" -ForegroundColor Gray }
        exit 1
    }

    Write-Host "[OK] Update package found: $updatePackage" -ForegroundColor Green
}

# Get current version info
$currentExe = Join-Path $existingInstall "CloudJourneyAddin.exe"
$currentFileInfo = Get-Item $currentExe
Write-Host "[INFO] Current version: Last modified $($currentFileInfo.LastWriteTime)" -ForegroundColor Cyan
Write-Host "[INFO] Current size: $([math]::Round($currentFileInfo.Length / 1KB, 2)) KB" -ForegroundColor Cyan

# Prompt for confirmation
Write-Host ""
Write-Host "Ready to update installation at: $existingInstall" -ForegroundColor Yellow
$confirm = Read-Host "Continue with update? (Y/N)"
if ($confirm -ne "Y") {
    Write-Host "[INFO] Update cancelled." -ForegroundColor Yellow
    exit 0
}

# Create backup
$backupPath = Join-Path $existingInstall "backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
Write-Host ""
Write-Host "[INFO] Creating backup at: $backupPath" -ForegroundColor Cyan

try {
    New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
    Copy-Item -Path (Join-Path $existingInstall "CloudJourneyAddin.exe") -Destination $backupPath -Force -ErrorAction Stop
    Copy-Item -Path (Join-Path $existingInstall "CloudJourneyAddin.dll") -Destination $backupPath -Force -ErrorAction Stop
    Write-Host "[OK] Backup created successfully" -ForegroundColor Green
} catch {
    Write-Host "[WARN] Could not create backup: $_" -ForegroundColor Yellow
    $continue = Read-Host "Continue without backup? (Y/N)"
    if ($continue -ne "Y") {
        Write-Host "[INFO] Update cancelled." -ForegroundColor Yellow
        exit 0
    }
}

# Check if app is running
$runningProcess = Get-Process -Name "CloudJourneyAddin" -ErrorAction SilentlyContinue
if ($runningProcess) {
    Write-Host ""
    Write-Host "[WARN] Cloud Journey Dashboard is currently running!" -ForegroundColor Yellow
    Write-Host "[INFO] The application must be closed to update." -ForegroundColor Yellow
    $closeApp = Read-Host "Close application now? (Y/N)"
    if ($closeApp -eq "Y") {
        try {
            $runningProcess | Stop-Process -Force
            Write-Host "[OK] Application closed" -ForegroundColor Green
            Start-Sleep -Seconds 2
        } catch {
            Write-Host "[ERROR] Failed to close application: $_" -ForegroundColor Red
            Write-Host "[INFO] Please close the application manually and run this script again." -ForegroundColor Yellow
            exit 1
        }
    } else {
        Write-Host "[INFO] Please close the application and run this script again." -ForegroundColor Yellow
        exit 0
    }
}

# Extract or use files
if ($useExtractedFiles) {
    # Files are already extracted in the current folder
    Write-Host ""
    Write-Host "[INFO] Using files from extracted folder..." -ForegroundColor Cyan
    $tempExtract = $PSScriptRoot
} else {
    # Extract update from zip to temporary location
    $tempExtract = Join-Path $env:TEMP "CloudJourneyAddin_Update_$(Get-Date -Format 'yyyyMMddHHmmss')"
    Write-Host ""
    Write-Host "[INFO] Extracting update package..." -ForegroundColor Cyan

    try {
        Expand-Archive -Path $updatePackage -DestinationPath $tempExtract -Force -ErrorAction Stop
        Write-Host "[OK] Package extracted to temp location" -ForegroundColor Green
    } catch {
        Write-Host "[ERROR] Failed to extract package: $_" -ForegroundColor Red
        exit 1
    }
}

# Copy new files
Write-Host "[INFO] Installing updated files..." -ForegroundColor Cyan

# Track changes
$filesCreated = @()
$filesUpdated = @()

try {
    # Get list of existing files before update
    $existingFiles = @{}
    if (Test-Path $existingInstall) {
        Get-ChildItem -Path $existingInstall -File -Recurse | ForEach-Object {
            $relativePath = $_.FullName.Substring($existingInstall.Length + 1)
            $existingFiles[$relativePath] = $_.LastWriteTime
        }
    }
    
    # Copy all files from temp to installation directory (exclude scripts and docs)
    Get-ChildItem -Path $tempExtract -File -Recurse | Where-Object { $_.Extension -ne '.ps1' -and $_.Name -ne 'README.md' } | ForEach-Object {
        $relativePath = $_.FullName.Substring($tempExtract.Length + 1)
        $targetPath = Join-Path $existingInstall $relativePath
        $targetDir = Split-Path $targetPath -Parent
        
        if (-not (Test-Path $targetDir)) {
            New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
        }
        
        # Track if this is new or updated
        $isNew = -not (Test-Path $targetPath)
        
        Copy-Item -Path $_.FullName -Destination $targetPath -Force
        
        if ($isNew) {
            $filesCreated += $relativePath
        } else {
            $filesUpdated += $relativePath
        }
    }
    Write-Host "[OK] Files installed successfully" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Failed to copy files: $_" -ForegroundColor Red
    if (Test-Path $backupPath) {
        Write-Host "[INFO] Backup is available at: $backupPath" -ForegroundColor Yellow
    }
    exit 1
}

# Cleanup temp files (only if we extracted from zip)
if (-not $useExtractedFiles -and (Test-Path $tempExtract)) {
    Write-Host "[INFO] Cleaning up temporary files..." -ForegroundColor Cyan
    Remove-Item -Path $tempExtract -Recurse -Force -ErrorAction SilentlyContinue
}

# Get new version info
$newFileInfo = Get-Item $currentExe
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Update completed successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Installation location: $existingInstall" -ForegroundColor Cyan
Write-Host "New version: Last modified $($newFileInfo.LastWriteTime)" -ForegroundColor Cyan
Write-Host "New size: $([math]::Round($newFileInfo.Length / 1KB, 2)) KB" -ForegroundColor Cyan
if (Test-Path $backupPath) {
    Write-Host "Backup location: $backupPath" -ForegroundColor Cyan
}

# Display file change summary
Write-Host ""
Write-Host "File Changes Summary:" -ForegroundColor Yellow
Write-Host "  Created: $($filesCreated.Count) file(s)" -ForegroundColor Green
Write-Host "  Updated: $($filesUpdated.Count) file(s)" -ForegroundColor Cyan

if ($filesCreated.Count -gt 0 -and $filesCreated.Count -le 10) {
    Write-Host ""
    Write-Host "  New files:" -ForegroundColor Green
    $filesCreated | ForEach-Object { Write-Host "    + $_" -ForegroundColor Gray }
}

if ($filesUpdated.Count -gt 0 -and $filesUpdated.Count -le 20) {
    Write-Host ""
    Write-Host "  Updated files:" -ForegroundColor Cyan
    $filesUpdated | ForEach-Object { Write-Host "    ~ $_" -ForegroundColor Gray }
} elseif ($filesUpdated.Count -gt 20) {
    Write-Host ""
    Write-Host "  Updated files (showing first 20 of $($filesUpdated.Count)):" -ForegroundColor Cyan
    $filesUpdated | Select-Object -First 20 | ForEach-Object { Write-Host "    ~ $_" -ForegroundColor Gray }
}

Write-Host ""
Write-Host "You can now launch Cloud Journey Dashboard from the ConfigMgr Console." -ForegroundColor Yellow
Write-Host ""
