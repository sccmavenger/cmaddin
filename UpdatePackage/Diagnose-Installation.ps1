# CloudJourneyAddin Installation Diagnostic Script
# Checks for missing dependencies and provides remediation steps

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "CloudJourneyAddin - Installation Diagnostic" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get the application directory
$appDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "[INFO] Checking installation at: $appDir" -ForegroundColor Cyan
Write-Host ""

# Critical dependencies to check
$criticalFiles = @(
    "CloudJourneyAddin.exe",
    "CloudJourneyAddin.dll",
    "Azure.Identity.dll",
    "Microsoft.Graph.dll",
    "Microsoft.Identity.Client.dll",
    "System.Management.dll",
    "LiveCharts.dll",
    "LiveCharts.Wpf.dll"
)

$missingFiles = @()
$foundFiles = @()

Write-Host "[INFO] Checking critical dependencies..." -ForegroundColor Yellow
foreach ($file in $criticalFiles) {
    $filePath = Join-Path $appDir $file
    if (Test-Path $filePath) {
        $fileInfo = Get-Item $filePath
        $foundFiles += "$file ($([math]::Round($fileInfo.Length / 1KB, 1)) KB)"
        Write-Host "  ✓ $file" -ForegroundColor Green
    } else {
        $missingFiles += $file
        Write-Host "  ✗ $file - MISSING!" -ForegroundColor Red
    }
}

Write-Host ""

# Count all DLLs
$allDlls = Get-ChildItem $appDir -Filter "*.dll" -ErrorAction SilentlyContinue
$allFiles = Get-ChildItem $appDir -File -ErrorAction SilentlyContinue

Write-Host "[INFO] Installation Statistics:" -ForegroundColor Cyan
Write-Host "  Total files: $($allFiles.Count)" -ForegroundColor White
Write-Host "  Total DLLs: $($allDlls.Count)" -ForegroundColor White
Write-Host "  Found critical files: $($foundFiles.Count)/$($criticalFiles.Count)" -ForegroundColor White

if ($missingFiles.Count -gt 0) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "PROBLEM DETECTED" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "[ERROR] Missing $($missingFiles.Count) critical file(s):" -ForegroundColor Red
    foreach ($file in $missingFiles) {
        Write-Host "  • $file" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "SOLUTION:" -ForegroundColor Yellow
    Write-Host "1. You may have only extracted SOME files from the ZIP" -ForegroundColor White
    Write-Host "2. The update ZIP should contain ~500 files (~90 MB)" -ForegroundColor White
    Write-Host "3. Extract the COMPLETE ZIP again:" -ForegroundColor White
    Write-Host "   - Right-click CloudJourneyAddin-v1.4.0-FINAL.zip" -ForegroundColor Gray
    Write-Host "   - Select 'Extract All...'" -ForegroundColor Gray
    Write-Host "   - Choose a folder" -ForegroundColor Gray
    Write-Host "   - Wait for ALL files to extract" -ForegroundColor Gray
    Write-Host "4. Then run Update-CloudJourneyAddin.ps1 from the extracted folder" -ForegroundColor White
    Write-Host ""
    Write-Host "Expected file count: ~500 files" -ForegroundColor Cyan
    Write-Host "Your file count: $($allFiles.Count) files" -ForegroundColor $(if ($allFiles.Count -lt 400) { "Red" } else { "Green" })
} else {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "✓ All Critical Dependencies Found!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Your installation appears complete." -ForegroundColor Green
    Write-Host ""
    Write-Host "If you're still getting errors:" -ForegroundColor Yellow
    Write-Host "1. Try running CloudJourneyAddin.exe directly from this folder" -ForegroundColor White
    Write-Host "2. Check the log file at: %LOCALAPPDATA%\CloudJourneyAddin\Logs" -ForegroundColor White
    Write-Host "3. Make sure you have .NET 8.0 Runtime installed" -ForegroundColor White
}

Write-Host ""
Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
