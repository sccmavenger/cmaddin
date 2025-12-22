# Check ConfigMgr Console logs for extension errors

Write-Host "Checking ConfigMgr Console logs for extension loading issues..." -ForegroundColor Cyan
Write-Host ""

# Find the most recent AdminUI log
$logPaths = @(
    "$env:TEMP\*.log",
    "$env:LOCALAPPDATA\Temp\*.log",
    "C:\Windows\Temp\*.log"
)

$adminUILogs = @()
foreach ($path in $logPaths) {
    $logs = Get-ChildItem $path -Filter "AdminUI*.log" -ErrorAction SilentlyContinue
    if ($logs) {
        $adminUILogs += $logs
    }
}

if ($adminUILogs.Count -eq 0) {
    Write-Host "[INFO] No AdminUI logs found. Try running the ConfigMgr Console first." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Logs should be in one of these locations:" -ForegroundColor Cyan
    Write-Host "  - $env:TEMP" -ForegroundColor Gray
    Write-Host "  - $env:LOCALAPPDATA\Temp" -ForegroundColor Gray
    Write-Host "  - C:\Windows\Temp" -ForegroundColor Gray
    exit 0
}

$latestLog = $adminUILogs | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host "[OK] Found log: $($latestLog.FullName)" -ForegroundColor Green
Write-Host "[INFO] Last modified: $($latestLog.LastWriteTime)" -ForegroundColor Gray
Write-Host ""

# Search for extension-related entries
Write-Host "Searching for extension loading issues..." -ForegroundColor Cyan
$content = Get-Content $latestLog.FullName -Tail 1000

# Look for CloudJourney mentions
$cloudJourneyLines = $content | Select-String -Pattern "CloudJourney" -Context 2
if ($cloudJourneyLines) {
    Write-Host ""
    Write-Host "Found CloudJourney references:" -ForegroundColor Green
    foreach ($line in $cloudJourneyLines) {
        Write-Host $line.Line -ForegroundColor White
    }
}

# Look for XML loading errors
$xmlErrors = $content | Select-String -Pattern "(?i)(xml|extension|action).*(?i)(error|fail|exception)" -Context 1
if ($xmlErrors) {
    Write-Host ""
    Write-Host "Found XML/Extension errors:" -ForegroundColor Yellow
    foreach ($line in $xmlErrors | Select-Object -First 10) {
        Write-Host $line.Line -ForegroundColor White
    }
}

# Look for action loading
$actionLoading = $content | Select-String -Pattern "(?i)loading.*action|action.*load" -Context 1
if ($actionLoading) {
    Write-Host ""
    Write-Host "Action loading entries:" -ForegroundColor Cyan
    foreach ($line in $actionLoading | Select-Object -First 5) {
        Write-Host $line.Line -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Check complete. Review any errors above." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
