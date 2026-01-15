# Zero Trust Migration Journey Add-in - Quick Update Script
# This script updates just the fixed executable and DLL

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Zero Trust Migration Journey Add-in - Quick Update" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Find ConfigMgr installation
$configMgrPath = $null
$possiblePaths = @(
    "C:\Program Files\Microsoft Configuration Manager\AdminConsole",
    "F:\Program Files\Microsoft Configuration Manager\AdminConsole",
    "D:\Program Files\Microsoft Configuration Manager\AdminConsole",
    "E:\Program Files\Microsoft Configuration Manager\AdminConsole"
)

foreach ($path in $possiblePaths) {
    if (Test-Path $path) {
        $configMgrPath = $path
        Write-Host "[OK] Found ConfigMgr Console at: $configMgrPath" -ForegroundColor Green
        break
    }
}

if (-not $configMgrPath) {
    Write-Host "[ERROR] ConfigMgr Console installation not found!" -ForegroundColor Red
    Write-Host "Please specify the path manually:" -ForegroundColor Yellow
    $configMgrPath = Read-Host "Enter ConfigMgr AdminConsole path"
    if (-not (Test-Path $configMgrPath)) {
        Write-Host "[ERROR] Invalid path!" -ForegroundColor Red
        exit 1
    }
}

$targetPath = Join-Path $configMgrPath "bin\bin\ZeroTrustMigrationAddin"

if (-not (Test-Path $targetPath)) {
    Write-Host "[INFO] ZeroTrustMigrationAddin folder not found - creating for fresh install..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
    Write-Host "[OK] Created directory: $targetPath" -ForegroundColor Green
}

Write-Host "[INFO] Target directory: $targetPath" -ForegroundColor Cyan
Write-Host ""

# Stop any running instances
$process = Get-Process -Name "ZeroTrustMigrationAddin" -ErrorAction SilentlyContinue
if ($process) {
    Write-Host "[INFO] Stopping running ZeroTrustMigrationAddin process..." -ForegroundColor Yellow
    $process | Stop-Process -Force
    Start-Sleep -Seconds 2
}

# Backup existing files
$backupPath = Join-Path $targetPath "Backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
Write-Host "[INFO] Creating backup folder: $backupPath" -ForegroundColor Cyan

$filesToBackup = @("ZeroTrustMigrationAddin.exe", "ZeroTrustMigrationAddin.dll")
$backedUpCount = 0

foreach ($file in $filesToBackup) {
    $sourcePath = Join-Path $targetPath $file
    if (Test-Path $sourcePath) {
        Copy-Item $sourcePath $backupPath -ErrorAction SilentlyContinue
        $fileSize = [math]::Round((Get-Item $sourcePath).Length / 1KB, 2)
        Write-Host "   Backed up: $file ($fileSize KB)" -ForegroundColor Gray
        $backedUpCount++
    }
}

Write-Host "[OK] Backed up $backedUpCount file(s)" -ForegroundColor Green
Write-Host ""

# Copy updated files
Write-Host "[INFO] Detecting files to update..." -ForegroundColor Cyan
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Get all files from update package
$updateFiles = Get-ChildItem $scriptDir -File | Where-Object { 
    $_.Extension -in @('.exe', '.dll', '.xml', '.json', '.config', '.html', '.md') -and 
    $_.Name -ne 'Update-ZeroTrustMigrationAddin.ps1' -and
    $_.Name -ne 'Diagnose-Installation.ps1' -and
    $_.Name -ne 'Verify-ZeroTrustMigrationAddin.ps1' -and
    $_.Name -ne 'Check-ConsoleLog.ps1' -and
    $_.Name -ne 'Find-ConsoleLogs.ps1' -and
    $_.Name -ne 'Create-Shortcuts.ps1' -and
    $_.Name -ne 'Uninstall-ZeroTrustMigrationAddin.ps1'
}

if ($updateFiles.Count -eq 0) {
    Write-Host "[WARN] No update files found in package. Installing all files from package..." -ForegroundColor Yellow
    $updateFiles = Get-ChildItem $scriptDir -File | Where-Object { 
        $_.Name -ne 'Update-ZeroTrustMigrationAddin.ps1' 
    }
}

Write-Host "[INFO] Found $($updateFiles.Count) file(s) to deploy" -ForegroundColor Cyan
Write-Host ""

$copiedCount = 0
$skippedCount = 0
$errorCount = 0

# Track specific files for summary
$installedFiles = @()
$upgradedFiles = @()
$updatedFiles = @()
$skippedFiles = @()

try {
    foreach ($file in $updateFiles) {
        $destPath = Join-Path $targetPath $file.Name
        $sourceSize = [math]::Round($file.Length / 1KB, 2)
        
        # Check if file exists and compare versions for .exe and .dll
        $shouldCopy = $true
        $action = "Installing"
        
        if (Test-Path $destPath) {
            $action = "Updating"
            $destSize = [math]::Round((Get-Item $destPath).Length / 1KB, 2)
            
            # For executables and DLLs, compare file versions
            if ($file.Extension -in @('.exe', '.dll')) {
                try {
                    $sourceVersion = (Get-Item $file.FullName).VersionInfo.FileVersion
                    $destVersion = (Get-Item $destPath).VersionInfo.FileVersion
                    
                    if ($sourceVersion -eq $destVersion -and $sourceSize -eq $destSize) {
                        Write-Host "   $($file.Name)" -ForegroundColor Gray -NoNewline
                        Write-Host " (v$sourceVersion, $sourceSize KB) - " -ForegroundColor DarkGray -NoNewline
                        Write-Host "SAME VERSION, SKIPPED" -ForegroundColor DarkGray
                        $skippedCount++
                        $skippedFiles += @{Name=$file.Name; Version=$sourceVersion; Size=$sourceSize}
                        $shouldCopy = $false
                    } else {
                        $action = "Upgrading"
                    }
                } catch {
                    # If version check fails, just copy based on size
                }
            }
        }
        
        if ($shouldCopy) {
            Copy-Item $file.FullName $destPath -Force
            
            # Show detailed feedback
            Write-Host "   $action : " -ForegroundColor Green -NoNewline
            Write-Host "$($file.Name) " -ForegroundColor White -NoNewline
            Write-Host "($sourceSize KB)" -ForegroundColor Gray
            
            # For main executable, show version info
            if ($file.Name -eq "ZeroTrustMigrationAddin.exe") {
                $newVersion = (Get-Item $destPath).VersionInfo.FileVersion
                Write-Host "      Version: $newVersion" -ForegroundColor Cyan
            }
            
            # Track file by action type
            $fileInfo = @{Name=$file.Name; Size=$sourceSize}
            if ($file.Extension -in @('.exe', '.dll') -and $action -eq "Upgrading") {
                try {
                    $oldVer = $destVersion
                    $newVer = (Get-Item $destPath).VersionInfo.FileVersion
                    $fileInfo.OldVersion = $oldVer
                    $fileInfo.NewVersion = $newVer
                    $upgradedFiles += $fileInfo
                } catch {
                    $upgradedFiles += $fileInfo
                }
            } elseif ($action -eq "Installing") {
                $installedFiles += $fileInfo
            } else {
                $updatedFiles += $fileInfo
            }
            
            $copiedCount++
        }
    }
    
    Write-Host ""
    Write-Host "[OK] Deployment complete: $copiedCount updated, $skippedCount skipped" -ForegroundColor Green
    
    # Update XML manifest in the Extensions folder
    Write-Host ""
    Write-Host "[INFO] Checking for XML manifest..." -ForegroundColor Cyan
    $xmlSourcePath = Join-Path $scriptDir "ZeroTrustMigrationAddin.xml"
    if (Test-Path $xmlSourcePath) {
        $extensionsPath = Join-Path $configMgrPath "bin\XmlStorage\Extensions\Actions"
        if (Test-Path $extensionsPath) {
            $xmlDestPath = Join-Path $extensionsPath "ZeroTrustMigrationAddin.xml"
            Copy-Item $xmlSourcePath $xmlDestPath -Force
            $xmlSize = [math]::Round((Get-Item $xmlSourcePath).Length / 1KB, 2)
            Write-Host "   Updated: ZeroTrustMigrationAddin.xml ($xmlSize KB)" -ForegroundColor Green
            Write-Host "   Location: $xmlDestPath" -ForegroundColor Gray
        } else {
            Write-Host "[WARN] Extensions folder not found: $extensionsPath" -ForegroundColor Yellow
        }
    } else {
        Write-Host "   No XML manifest in update package (optional)" -ForegroundColor Gray
    }
} catch {
    Write-Host ""
    Write-Host "[ERROR] Failed to copy files: $_" -ForegroundColor Red
    Write-Host "Stack trace: $($_.ScriptStackTrace)" -ForegroundColor DarkRed
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Update Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

# Display detailed summary
Write-Host "DEPLOYMENT SUMMARY" -ForegroundColor Cyan
Write-Host "==================" -ForegroundColor Cyan
Write-Host ""

if ($installedFiles.Count -gt 0) {
    Write-Host "Newly Installed ($($installedFiles.Count)):" -ForegroundColor Green
    foreach ($f in $installedFiles | Sort-Object Name) {
        Write-Host "   + $($f.Name)" -ForegroundColor White -NoNewline
        Write-Host " ($($f.Size) KB)" -ForegroundColor Gray
    }
    Write-Host ""
}

if ($upgradedFiles.Count -gt 0) {
    Write-Host "Upgraded ($($upgradedFiles.Count)):" -ForegroundColor Yellow
    foreach ($f in $upgradedFiles | Sort-Object Name) {
        Write-Host "   ↑ $($f.Name)" -ForegroundColor White -NoNewline
        if ($f.OldVersion -and $f.NewVersion) {
            Write-Host " [$($f.OldVersion) -> $($f.NewVersion)]" -ForegroundColor Cyan -NoNewline
        }
        Write-Host " ($($f.Size) KB)" -ForegroundColor Gray
    }
    Write-Host ""
}

if ($updatedFiles.Count -gt 0) {
    Write-Host "Updated ($($updatedFiles.Count)):" -ForegroundColor Blue
    foreach ($f in $updatedFiles | Sort-Object Name) {
        Write-Host "   • $($f.Name)" -ForegroundColor White -NoNewline
        Write-Host " ($($f.Size) KB)" -ForegroundColor Gray
    }
    Write-Host ""
}

if ($skippedFiles.Count -gt 0) {
    Write-Host "Skipped (already up-to-date) ($($skippedFiles.Count)):" -ForegroundColor DarkGray
    foreach ($f in $skippedFiles | Sort-Object Name | Select-Object -First 5) {
        Write-Host "   = $($f.Name)" -ForegroundColor DarkGray -NoNewline
        if ($f.Version) {
            Write-Host " (v$($f.Version))" -ForegroundColor DarkGray -NoNewline
        }
        Write-Host " ($($f.Size) KB)" -ForegroundColor DarkGray
    }
    if ($skippedFiles.Count -gt 5) {
        Write-Host "   ... and $($skippedFiles.Count - 5) more" -ForegroundColor DarkGray
    }
    Write-Host ""
}

Write-Host "Total: $copiedCount deployed, $skippedCount skipped" -ForegroundColor Cyan
Write-Host ""

# Create/Update shortcuts
Write-Host "[INFO] Creating desktop and start menu shortcuts..." -ForegroundColor Cyan
$createShortcutsScript = Join-Path $scriptDir "Create-Shortcuts.ps1"
if (Test-Path $createShortcutsScript) {
    try {
        & $createShortcutsScript -TargetPath $targetPath
        Write-Host "[OK] Shortcuts created/updated!" -ForegroundColor Green
    } catch {
        Write-Host "[WARN] Failed to create shortcuts: $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "[WARN] Create-Shortcuts.ps1 not found, skipping shortcut creation" -ForegroundColor Yellow
}
Write-Host ""

Write-Host "IMPORTANT: Restart the ConfigMgr Console to see the add-in button!" -ForegroundColor Yellow
Write-Host ""
Write-Host "After restarting the console:" -ForegroundColor Cyan
Write-Host "  1. Go to the Home ribbon or Devices section" -ForegroundColor White
Write-Host "  2. Look for 'Zero Trust Migration Journey Progress' button" -ForegroundColor White
Write-Host "  3. Click it to launch the dashboard" -ForegroundColor White
Write-Host ""
Write-Host "Or run standalone:" -ForegroundColor Cyan
Write-Host "  cd `"$targetPath`"" -ForegroundColor White
Write-Host "  .\ZeroTrustMigrationAddin.exe" -ForegroundColor White
Write-Host ""
