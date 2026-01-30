<#
.SYNOPSIS
    Shows diagnostic information for troubleshooting the Cloud Native Assessment.

.DESCRIPTION
    Displays version info, log locations, cached files, and recent log entries
    to help diagnose issues with the application or auto-update system.

.EXAMPLE
    .\Get-Diagnostics.ps1

.NOTES
    Author: Cloud Native Assessment Team
    Version: 1.0.0
#>

$ErrorActionPreference = "SilentlyContinue"

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     Cloud Native Assessment - Diagnostics                    ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Define paths
$appDataFolder = "$env:LOCALAPPDATA\ZeroTrustMigrationAddin"
$manifestPath = Join-Path $appDataFolder "manifest.json"
$logsFolder = Join-Path $appDataFolder "Logs"
$updateTempFolder = "$env:TEMP\CloudJourneyAddin-Update"

# Check if app is running
Write-Host "Application Status" -ForegroundColor White
Write-Host "─────────────────────────────────────────────────────" -ForegroundColor DarkGray
$appProcess = Get-Process -Name "ZeroTrustMigrationAddin" -ErrorAction SilentlyContinue
if ($appProcess) {
    Write-Host "  Running: Yes (PID: $($appProcess.Id))" -ForegroundColor Green
} else {
    Write-Host "  Running: No" -ForegroundColor Gray
}
Write-Host ""

# Cached Manifest
Write-Host "Cached Manifest" -ForegroundColor White
Write-Host "─────────────────────────────────────────────────────" -ForegroundColor DarkGray
if (Test-Path $manifestPath) {
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    Write-Host "  Path: $manifestPath" -ForegroundColor Gray
    Write-Host "  Cached Version: $($manifest.version)" -ForegroundColor Yellow
    $lastModified = (Get-Item $manifestPath).LastWriteTime
    Write-Host "  Last Updated: $lastModified" -ForegroundColor Gray
} else {
    Write-Host "  No cached manifest (will fetch on next launch)" -ForegroundColor Gray
}
Write-Host ""

# Update Temp Files
Write-Host "Update Temp Files" -ForegroundColor White
Write-Host "─────────────────────────────────────────────────────" -ForegroundColor DarkGray
if (Test-Path $updateTempFolder) {
    $files = Get-ChildItem $updateTempFolder -Recurse -File
    Write-Host "  Path: $updateTempFolder" -ForegroundColor Gray
    Write-Host "  Files: $($files.Count)" -ForegroundColor Yellow
    if ($files.Count -gt 0) {
        Write-Host "  ⚠️  Leftover files may indicate failed update" -ForegroundColor Yellow
    }
} else {
    Write-Host "  No temp files (clean)" -ForegroundColor Green
}
Write-Host ""

# Logs
Write-Host "Log Files" -ForegroundColor White
Write-Host "─────────────────────────────────────────────────────" -ForegroundColor DarkGray
if (Test-Path $logsFolder) {
    Write-Host "  Path: $logsFolder" -ForegroundColor Gray
    $logFiles = Get-ChildItem $logsFolder -Filter "*.log" | Sort-Object LastWriteTime -Descending
    
    foreach ($log in $logFiles | Select-Object -First 5) {
        $size = "{0:N1} KB" -f ($log.Length / 1KB)
        Write-Host "  • $($log.Name) ($size)" -ForegroundColor Gray
    }
    
    if ($logFiles.Count -gt 5) {
        Write-Host "  ... and $($logFiles.Count - 5) more" -ForegroundColor DarkGray
    }
} else {
    Write-Host "  No logs folder found" -ForegroundColor Gray
}
Write-Host ""

# Recent Update Log Entries
$updateLog = Join-Path $logsFolder "Update.log"
if (Test-Path $updateLog) {
    Write-Host "Recent Update Log (last 10 entries)" -ForegroundColor White
    Write-Host "─────────────────────────────────────────────────────" -ForegroundColor DarkGray
    $lines = Get-Content $updateLog -Tail 10
    foreach ($line in $lines) {
        if ($line -match "error|fail") {
            Write-Host "  $line" -ForegroundColor Red
        } elseif ($line -match "success|completed") {
            Write-Host "  $line" -ForegroundColor Green
        } else {
            Write-Host "  $line" -ForegroundColor Gray
        }
    }
    Write-Host ""
}

# Recent App Log Entries
$todayLog = Join-Path $logsFolder "CloudNativeReadiness_$(Get-Date -Format 'yyyyMMdd').log"
$latestLog = Get-ChildItem $logsFolder -Filter "CloudNativeReadiness_*.log" -ErrorAction SilentlyContinue | 
             Sort-Object LastWriteTime -Descending | Select-Object -First 1

if ($latestLog) {
    Write-Host "Recent App Log (last 10 entries)" -ForegroundColor White
    Write-Host "─────────────────────────────────────────────────────" -ForegroundColor DarkGray
    $lines = Get-Content $latestLog.FullName -Tail 10
    foreach ($line in $lines) {
        if ($line -match "error|fail|exception") {
            Write-Host "  $line" -ForegroundColor Red
        } elseif ($line -match "Version:") {
            Write-Host "  $line" -ForegroundColor Cyan
        } else {
            Write-Host "  $line" -ForegroundColor Gray
        }
    }
    Write-Host ""
}

Write-Host "════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "Helpful Commands:" -ForegroundColor White
Write-Host "  Reset auto-update:  .\Reset-AutoUpdate.ps1" -ForegroundColor Gray
Write-Host "  Open logs folder:   explorer `"$logsFolder`"" -ForegroundColor Gray
Write-Host ""
