#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Complete uninstall script for Zero Trust Migration Journey Add-in
    
.DESCRIPTION
    Removes all traces of the application including:
    - Application files from ConfigMgr Console directory
    - Desktop and Start Menu shortcuts
    - Logs and configuration files
    - XML manifest from ConfigMgr Extensions
    
.PARAMETER KeepLogs
    If specified, keeps the log files in %LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs
    
.PARAMETER KeepBackups
    If specified, keeps backup folders
    
.EXAMPLE
    .\Uninstall-ZeroTrustMigrationAddin.ps1
    # Complete removal including logs
    
.EXAMPLE
    .\Uninstall-ZeroTrustMigrationAddin.ps1 -KeepLogs
    # Remove app but keep logs for troubleshooting
#>

param(
    [switch]$KeepLogs,
    [switch]$KeepBackups
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Red
Write-Host "Zero Trust Migration Journey Add-in - UNINSTALL" -ForegroundColor Red
Write-Host "========================================" -ForegroundColor Red
Write-Host ""
Write-Host "??  This will completely remove the Zero Trust Migration Journey Add-in" -ForegroundColor Yellow
Write-Host ""

$confirmation = Read-Host "Are you sure you want to uninstall? (Type 'YES' to confirm)"
if ($confirmation -ne "YES") {
    Write-Host ""
    Write-Host "Uninstall cancelled." -ForegroundColor Gray
    exit 0
}

Write-Host ""
Write-Host "[1/6] Stopping running processes..." -ForegroundColor Yellow

$processes = Get-Process -Name "ZeroTrustMigrationAddin" -ErrorAction SilentlyContinue
if ($processes) {
    $processes | Stop-Process -Force
    Write-Host "   ? Stopped $($processes.Count) running process(es)" -ForegroundColor Green
    Start-Sleep -Seconds 2
} else {
    Write-Host "   ? No running processes found" -ForegroundColor Gray
}

Write-Host ""
Write-Host "[2/6] Finding ConfigMgr Console installation..." -ForegroundColor Yellow

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
        Write-Host "   ? Found: $configMgrPath" -ForegroundColor Green
        break
    }
}

if (-not $configMgrPath) {
    Write-Host "   ??  ConfigMgr Console not found at standard locations" -ForegroundColor Yellow
    $configMgrPath = Read-Host "   Enter ConfigMgr AdminConsole path (or press Enter to skip)"
    if ([string]::IsNullOrWhiteSpace($configMgrPath)) {
        Write-Host "   ? Skipping ConfigMgr file removal" -ForegroundColor Gray
        $configMgrPath = $null
    }
}

Write-Host ""
Write-Host "[3/6] Removing application files..." -ForegroundColor Yellow

$removedFileCount = 0

if ($configMgrPath) {
    $targetPath = Join-Path $configMgrPath "bin\bin\ZeroTrustMigrationAddin"
    
    if (Test-Path $targetPath) {
        # List what will be removed
        $files = Get-ChildItem $targetPath -File
        Write-Host "   Found installation at: $targetPath" -ForegroundColor White
        Write-Host "   Files to remove: $($files.Count)" -ForegroundColor White
        
        # Remove backup folders if not keeping
        if (-not $KeepBackups) {
            $backupFolders = Get-ChildItem $targetPath -Directory -Filter "Backup_*"
            if ($backupFolders) {
                Write-Host "   Removing $($backupFolders.Count) backup folder(s)..." -ForegroundColor Gray
                $backupFolders | Remove-Item -Recurse -Force
            }
        }
        
        # Remove all files
        Write-Host "   Removing application files..." -ForegroundColor Gray
        Remove-Item $targetPath -Recurse -Force -ErrorAction SilentlyContinue
        $removedFileCount = $files.Count
        Write-Host "   ? Removed $removedFileCount file(s)" -ForegroundColor Green
    } else {
        Write-Host "   ? Installation folder not found: $targetPath" -ForegroundColor Gray
    }
    
    # Remove XML manifest from Extensions
    Write-Host "   Checking for XML manifest..." -ForegroundColor Gray
    $extensionsPath = Join-Path $configMgrPath "bin\XmlStorage\Extensions\Actions\ZeroTrustMigrationAddin.xml"
    if (Test-Path $extensionsPath) {
        Remove-Item $extensionsPath -Force
        Write-Host "   ? Removed XML manifest from ConfigMgr Extensions" -ForegroundColor Green
    } else {
        Write-Host "   ? XML manifest not found" -ForegroundColor Gray
    }
} else {
    Write-Host "   ? Skipped (ConfigMgr path not found)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "[4/6] Removing shortcuts..." -ForegroundColor Yellow

$shortcutsRemoved = 0

# Desktop shortcut
$desktopShortcut = [System.IO.Path]::Combine([Environment]::GetFolderPath("Desktop"), "Zero Trust Migration Journey.lnk")
if (Test-Path $desktopShortcut) {
    Remove-Item $desktopShortcut -Force
    Write-Host "   ? Removed desktop shortcut" -ForegroundColor Green
    $shortcutsRemoved++
} else {
    Write-Host "   ? Desktop shortcut not found" -ForegroundColor Gray
}

# Start Menu shortcut
$startMenuPath = [System.IO.Path]::Combine([Environment]::GetFolderPath("Programs"), "Zero Trust Migration Journey.lnk")
if (Test-Path $startMenuPath) {
    Remove-Item $startMenuPath -Force
    Write-Host "   ? Removed Start Menu shortcut" -ForegroundColor Green
    $shortcutsRemoved++
} else {
    Write-Host "   ? Start Menu shortcut not found" -ForegroundColor Gray
}

if ($shortcutsRemoved -eq 0) {
    Write-Host "   ? No shortcuts found" -ForegroundColor Gray
}

Write-Host ""
Write-Host "[5/6] Removing logs and configuration..." -ForegroundColor Yellow

$localAppData = [Environment]::GetFolderPath("LocalApplicationData")
$appDataPath = Join-Path $localAppData "ZeroTrustMigrationAddin"

if (Test-Path $appDataPath) {
    $logFiles = Get-ChildItem "$appDataPath\Logs" -Filter "*.log" -ErrorAction SilentlyContinue
    $logCount = if ($logFiles) { $logFiles.Count } else { 0 }
    
    if ($KeepLogs) {
        Write-Host "   ? Keeping log files (as requested)" -ForegroundColor Yellow
        Write-Host "   Location: $appDataPath\Logs" -ForegroundColor Gray
    } else {
        Remove-Item $appDataPath -Recurse -Force
        Write-Host "   ? Removed $logCount log file(s)" -ForegroundColor Green
        Write-Host "   ? Removed configuration folder" -ForegroundColor Green
    }
} else {
    Write-Host "   ? Application data folder not found" -ForegroundColor Gray
}

Write-Host ""
Write-Host "[6/6] Checking for standalone installations..." -ForegroundColor Yellow

$standaloneLocations = @(
    "$localAppData\ZeroTrustMigrationAddin\App",
    "$env:USERPROFILE\Desktop\ZeroTrustMigrationAddin",
    "$env:TEMP\ZeroTrustMigrationAddin"
)

$standaloneFound = $false
foreach ($location in $standaloneLocations) {
    if (Test-Path $location) {
        Write-Host "   Found standalone installation: $location" -ForegroundColor White
        $remove = Read-Host "   Remove this installation? (Y/N)"
        if ($remove -eq "Y" -or $remove -eq "y") {
            Remove-Item $location -Recurse -Force
            Write-Host "   ? Removed" -ForegroundColor Green
            $standaloneFound = $true
        }
    }
}

if (-not $standaloneFound) {
    Write-Host "   ? No standalone installations found" -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Uninstall Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Summary:" -ForegroundColor Yellow
Write-Host "  ? Application files removed: $removedFileCount" -ForegroundColor White
Write-Host "  ? Shortcuts removed: $shortcutsRemoved" -ForegroundColor White
Write-Host "  ? Logs: $(if ($KeepLogs) { 'KEPT' } else { 'REMOVED' })" -ForegroundColor White
Write-Host "  ? Backups: $(if ($KeepBackups) { 'KEPT' } else { 'REMOVED' })" -ForegroundColor White
Write-Host ""
Write-Host "? Zero Trust Migration Journey Add-in has been completely removed" -ForegroundColor Green
Write-Host ""

if ($KeepLogs) {
    Write-Host "Note: Log files preserved at:" -ForegroundColor Cyan
    Write-Host "  $appDataPath\Logs" -ForegroundColor Gray
    Write-Host ""
}

