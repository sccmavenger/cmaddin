# Find ConfigMgr Console logs in install directory

$configMgrPath = "F:\Program Files\Microsoft Configuration Manager\AdminConsole"

if (-not (Test-Path $configMgrPath)) {
    Write-Host "[ERROR] ConfigMgr path not found!" -ForegroundColor Red
    exit 1
}

Write-Host "Searching for logs in ConfigMgr directories..." -ForegroundColor Cyan
Write-Host ""

# Check for logs in AdminConsole\AdminUILog
$logDir = Join-Path $configMgrPath "AdminUILog"
if (Test-Path $logDir) {
    Write-Host "[OK] Found AdminUILog directory: $logDir" -ForegroundColor Green
    $logs = Get-ChildItem $logDir -Filter "*.log" | Sort-Object LastWriteTime -Descending
    if ($logs) {
        Write-Host "Recent log files:" -ForegroundColor Cyan
        $logs | Select-Object -First 5 | ForEach-Object {
            Write-Host "  - $($_.Name) (Modified: $($_.LastWriteTime))" -ForegroundColor Gray
        }
        
        # Check the most recent log
        $latestLog = $logs | Select-Object -First 1
        Write-Host ""
        Write-Host "Checking latest log: $($latestLog.Name)" -ForegroundColor Cyan
        Write-Host ""
        
        $content = Get-Content $latestLog.FullName -Tail 500 -ErrorAction SilentlyContinue
        
        # Search for CloudJourney
        $cloudJourney = $content | Select-String -Pattern "CloudJourney" -Context 2
        if ($cloudJourney) {
            Write-Host "Found CloudJourney references:" -ForegroundColor Green
            $cloudJourney | ForEach-Object { Write-Host $_.Line -ForegroundColor White }
        } else {
            Write-Host "[INFO] No CloudJourney references found" -ForegroundColor Yellow
        }
        
        Write-Host ""
        # Search for extension/action loading
        $extensions = $content | Select-String -Pattern "(?i)extension|action.*xml" -Context 1
        if ($extensions) {
            Write-Host "Extension/Action loading entries (last 10):" -ForegroundColor Cyan
            $extensions | Select-Object -Last 10 | ForEach-Object { Write-Host $_.Line -ForegroundColor Gray }
        }
    }
} else {
    Write-Host "[WARN] AdminUILog directory not found at: $logDir" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan

# Also show what's in the Extensions folder
$extensionsPath = Join-Path $configMgrPath "bin\XmlStorage\Extensions\Actions"
Write-Host ""
Write-Host "Extensions currently deployed:" -ForegroundColor Cyan
if (Test-Path $extensionsPath) {
    Get-ChildItem $extensionsPath -Filter "*.xml" | ForEach-Object {
        Write-Host "  - $($_.Name)" -ForegroundColor Gray
        
        # Show first few lines of each XML
        if ($_.Name -eq "ZeroTrustMigrationAddin.xml") {
            Write-Host "    Content preview:" -ForegroundColor Yellow
            Get-Content $_.FullName | Select-Object -First 8 | ForEach-Object {
                Write-Host "      $_" -ForegroundColor Gray
            }
        }
    }
}

Write-Host ""
