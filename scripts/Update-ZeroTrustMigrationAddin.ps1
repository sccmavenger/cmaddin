#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Updates Cloud Native Assessment to latest version
.DESCRIPTION
    Updates existing ZeroTrustMigrationAddin installation with new version from extracted folder or zip
.EXAMPLE
    .\Update-ZeroTrustMigrationAddin.ps1
#>

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Cloud Native Assessment - Update Tool" -ForegroundColor Cyan
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
        
        # Search recursively for ZeroTrustMigrationAddin.exe starting from bin folder
        $binPath = Join-Path $consolePath "bin"
        if (Test-Path $binPath) {
            $searchedPaths += $binPath
            # Find ZeroTrustMigrationAddin.exe that's in a ZeroTrustMigrationAddin folder
            $foundFiles = Get-ChildItem -Path $binPath -Filter "ZeroTrustMigrationAddin.exe" -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Directory.Name -eq "ZeroTrustMigrationAddin" }
            
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
    Write-Host "[INFO] Please run a fresh installation instead (Install-ZeroTrustMigrationAddin.ps1)." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Searched within:" -ForegroundColor Yellow
    foreach ($path in ($searchedPaths | Select-Object -Unique)) {
        Write-Host "  - $path (and subdirectories)" -ForegroundColor Gray
    }
    exit 1
}

# Check if we're running from an extracted folder (files already present)
$sourceExe = Join-Path $PSScriptRoot "ZeroTrustMigrationAddin.exe"
$useExtractedFiles = Test-Path $sourceExe

if ($useExtractedFiles) {
    Write-Host "[OK] Running from extracted folder - will use files directly" -ForegroundColor Green
    $updateSource = $PSScriptRoot
    $updatePackage = $null
} else {
    # Look for zip package
    $updatePackage = $null
    $possiblePackages = @(
        (Join-Path $PSScriptRoot "ZeroTrustMigrationAddin-v*.zip"),
        (Join-Path $PSScriptRoot "ZeroTrustMigrationAddin-Latest.zip")
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
        Write-Host "[INFO] Looking for: ZeroTrustMigrationAddin-v*.zip or ZeroTrustMigrationAddin-Latest.zip" -ForegroundColor Yellow
        Write-Host "[INFO] In folder: $PSScriptRoot" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Files in current folder:" -ForegroundColor Gray
        Get-ChildItem $PSScriptRoot -Filter "*.zip" | ForEach-Object { Write-Host "  - $($_.Name)" -ForegroundColor Gray }
        exit 1
    }

    Write-Host "[OK] Update package found: $updatePackage" -ForegroundColor Green
}

# Get current version info
$currentExe = Join-Path $existingInstall "ZeroTrustMigrationAddin.exe"
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

# Check available disk space
$drive = (Get-Item $existingInstall).PSDrive
$requiredSpaceMB = 100
$availableSpaceMB = [math]::Round($drive.Free / 1MB, 2)
if ($availableSpaceMB -lt $requiredSpaceMB) {
    Write-Host "[ERROR] Insufficient disk space! Available: $($availableSpaceMB)MB, Required: $($requiredSpaceMB)MB" -ForegroundColor Red
    exit 1
}
Write-Host "[OK] Disk space check passed ($($availableSpaceMB)MB available)" -ForegroundColor Green

# Create backup
$backupPath = Join-Path $existingInstall "backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
Write-Host ""
Write-Host "[INFO] Creating backup at: $backupPath" -ForegroundColor Cyan

try {
    New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
    Copy-Item -Path (Join-Path $existingInstall "ZeroTrustMigrationAddin.exe") -Destination $backupPath -Force -ErrorAction Stop
    Copy-Item -Path (Join-Path $existingInstall "ZeroTrustMigrationAddin.dll") -Destination $backupPath -Force -ErrorAction Stop
    Write-Host "[OK] Backup created successfully" -ForegroundColor Green
    
    # Cleanup old backups (keep last 3)
    $allBackups = Get-ChildItem -Path $existingInstall -Directory -Filter "backup_*" | Sort-Object Name -Descending
    if ($allBackups.Count -gt 3) {
        Write-Host "[INFO] Cleaning up old backups (keeping last 3)..." -ForegroundColor Cyan
        $toDelete = $allBackups | Select-Object -Skip 3
        foreach ($oldBackup in $toDelete) {
            Remove-Item -Path $oldBackup.FullName -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "[OK] Removed old backup: $($oldBackup.Name)" -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "[WARN] Could not create backup: $_" -ForegroundColor Yellow
    $continue = Read-Host "Continue without backup? (Y/N)"
    if ($continue -ne "Y") {
        Write-Host "[INFO] Update cancelled." -ForegroundColor Yellow
        exit 0
    }
}

# Check if app is running
$runningProcess = Get-Process -Name "ZeroTrustMigrationAddin" -ErrorAction SilentlyContinue
if ($runningProcess) {
    Write-Host ""
    Write-Host "[WARN] Cloud Native Assessment is currently running!" -ForegroundColor Yellow
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
    # Verify ZIP integrity
    Write-Host ""
    Write-Host "[INFO] Verifying update package integrity..." -ForegroundColor Cyan
    
    if (-not (Test-Path $updatePackage)) {
        Write-Host "[ERROR] Update package not found: $updatePackage" -ForegroundColor Red
        exit 1
    }
    
    $zipInfo = Get-Item $updatePackage
    if ($zipInfo.Length -eq 0) {
        Write-Host "[ERROR] Update package is empty (0 bytes)" -ForegroundColor Red
        exit 1
    }
    
    # Test if ZIP is valid by attempting to read it
    try {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [System.IO.Compression.ZipFile]::OpenRead($updatePackage)
        $entryCount = $zip.Entries.Count
        $zip.Dispose()
        
        if ($entryCount -eq 0) {
            Write-Host "[ERROR] Update package contains no files" -ForegroundColor Red
            exit 1
        }
        
        Write-Host "[OK] Package verified: $($zipInfo.Length / 1MB | ForEach-Object { '{0:N2}' -f $_ })MB, $entryCount files" -ForegroundColor Green
    } catch {
        Write-Host "[ERROR] Update package is corrupted or invalid: $_" -ForegroundColor Red
        exit 1
    }
    
    # Extract update from zip to temporary location
    $tempExtract = Join-Path $env:TEMP "ZeroTrustMigrationAddin_Update_$(Get-Date -Format 'yyyyMMddHHmmss')"
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
    $allFiles = Get-ChildItem -Path $tempExtract -File -Recurse | Where-Object { $_.Extension -ne '.ps1' -and $_.Name -ne 'README.md' }
    $totalFiles = $allFiles.Count
    $currentFile = 0
    
    foreach ($file in $allFiles) {
        $currentFile++
        $relativePath = $file.FullName.Substring($tempExtract.Length + 1)
        $targetPath = Join-Path $existingInstall $relativePath
        $targetDir = Split-Path $targetPath -Parent
        
        # Update progress
        $percentComplete = [int](($currentFile / $totalFiles) * 100)
        Write-Progress -Activity "Installing updated files" -Status "$currentFile of $totalFiles: $relativePath" -PercentComplete $percentComplete
        
        if (-not (Test-Path $targetDir)) {
            New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
        }
        
        # Track if this is new or updated
        $isNew = -not (Test-Path $targetPath)
        
        Copy-Item -Path $file.FullName -Destination $targetPath -Force
        
        if ($isNew) {
            $filesCreated += $relativePath
        } else {
            $filesUpdated += $relativePath
        }
    }
    
    Write-Progress -Activity "Installing updated files" -Completed
    Write-Host "[OK] Files installed successfully" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Failed to copy files: $_" -ForegroundColor Red
    
    # Attempt automatic rollback from backup
    if (Test-Path $backupPath) {
        Write-Host "[INFO] Attempting automatic rollback from backup..." -ForegroundColor Yellow
        try {
            Copy-Item -Path (Join-Path $backupPath "ZeroTrustMigrationAddin.exe") -Destination $existingInstall -Force -ErrorAction Stop
            Copy-Item -Path (Join-Path $backupPath "ZeroTrustMigrationAddin.dll") -Destination $existingInstall -Force -ErrorAction Stop
            Write-Host "[OK] Rollback completed successfully" -ForegroundColor Green
            Write-Host "[INFO] Installation restored to previous version" -ForegroundColor Cyan
        } catch {
            Write-Host "[ERROR] Rollback failed: $_" -ForegroundColor Red
            Write-Host "[INFO] Manual recovery required. Backup available at: $backupPath" -ForegroundColor Yellow
        }
    } else {
        Write-Host "[WARN] No backup available for rollback" -ForegroundColor Yellow
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
Write-Host "You can now launch Cloud Native Assessment from the ConfigMgr Console." -ForegroundColor Yellow
Write-Host ""
