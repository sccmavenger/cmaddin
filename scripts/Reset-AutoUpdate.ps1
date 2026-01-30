<#
.SYNOPSIS
    Resets the auto-update system for Cloud Native Readiness Tool.

.DESCRIPTION
    Use this script when auto-update gets stuck or fails mid-update.
    It cleans up cached manifests and temporary update files, allowing
    the app to re-check for updates on next launch.

.PARAMETER Force
    Skips confirmation prompts.

.EXAMPLE
    .\Reset-AutoUpdate.ps1
    
.EXAMPLE
    .\Reset-AutoUpdate.ps1 -Force

.NOTES
    Author: Cloud Native Readiness Tool Team
    Version: 1.0.0
#>

param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     Cloud Native Readiness Tool - Auto-Update Reset          ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Define paths
$appDataFolder = "$env:LOCALAPPDATA\ZeroTrustMigrationAddin"
$manifestPath = Join-Path $appDataFolder "manifest.json"
$updateTempFolder = "$env:TEMP\CloudJourneyAddin-Update"
$logsFolder = Join-Path $appDataFolder "Logs"

# Check if app is running
$appProcess = Get-Process -Name "ZeroTrustMigrationAddin" -ErrorAction SilentlyContinue
if ($appProcess) {
    Write-Host "⚠️  Application is currently running!" -ForegroundColor Yellow
    Write-Host ""
    if (-not $Force) {
        $response = Read-Host "Close the application and continue? (Y/N)"
        if ($response -ne 'Y' -and $response -ne 'y') {
            Write-Host "Aborted." -ForegroundColor Red
            exit 1
        }
    }
    Write-Host "Closing application..." -ForegroundColor Gray
    $appProcess | Stop-Process -Force
    Start-Sleep -Seconds 2
    Write-Host "✅ Application closed" -ForegroundColor Green
}

Write-Host ""
Write-Host "Cleaning up auto-update files..." -ForegroundColor White
Write-Host ""

# 1. Delete cached manifest
Write-Host "  [1/3] Cached manifest" -ForegroundColor Gray -NoNewline
if (Test-Path $manifestPath) {
    Remove-Item $manifestPath -Force
    Write-Host " → Deleted" -ForegroundColor Green
} else {
    Write-Host " → Not found (OK)" -ForegroundColor DarkGray
}

# 2. Delete update temp folder
Write-Host "  [2/3] Update temp files" -ForegroundColor Gray -NoNewline
if (Test-Path $updateTempFolder) {
    Remove-Item $updateTempFolder -Recurse -Force
    Write-Host " → Deleted" -ForegroundColor Green
} else {
    Write-Host " → Not found (OK)" -ForegroundColor DarkGray
}

# 3. Show log location (don't delete - useful for debugging)
Write-Host "  [3/3] Logs folder" -ForegroundColor Gray -NoNewline
if (Test-Path $logsFolder) {
    $logFiles = Get-ChildItem $logsFolder -Filter "*.log" -ErrorAction SilentlyContinue
    Write-Host " → $($logFiles.Count) log file(s) preserved at:" -ForegroundColor DarkGray
    Write-Host "        $logsFolder" -ForegroundColor DarkGray
} else {
    Write-Host " → Not found" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "✅ Auto-update reset complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor White
Write-Host "  1. Launch the Cloud Native Readiness Tool" -ForegroundColor Gray
Write-Host "  2. The app will check for updates on startup" -ForegroundColor Gray
Write-Host "  3. If an update is available, it will download automatically" -ForegroundColor Gray
Write-Host "  4. DO NOT close the app while 'Applying update...' is shown" -ForegroundColor Yellow
Write-Host ""
