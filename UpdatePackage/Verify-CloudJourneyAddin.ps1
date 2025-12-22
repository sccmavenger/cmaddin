# Cloud Journey Add-in - Verification Script
# This script checks if the add-in is properly installed

$ErrorActionPreference = "Continue"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Cloud Journey Add-in - Verification" -ForegroundColor Cyan
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
    Write-Host "[ERROR] ConfigMgr Console not found!" -ForegroundColor Red
    exit 1
}

# Check executable
$exePath = Join-Path $configMgrPath "bin\bin\CloudJourneyAddin\CloudJourneyAddin.exe"
if (Test-Path $exePath) {
    Write-Host "[OK] CloudJourneyAddin.exe found" -ForegroundColor Green
} else {
    Write-Host "[ERROR] CloudJourneyAddin.exe NOT found at: $exePath" -ForegroundColor Red
}

# Check DLL
$dllPath = Join-Path $configMgrPath "bin\bin\CloudJourneyAddin\CloudJourneyAddin.dll"
if (Test-Path $dllPath) {
    Write-Host "[OK] CloudJourneyAddin.dll found" -ForegroundColor Green
} else {
    Write-Host "[ERROR] CloudJourneyAddin.dll NOT found at: $dllPath" -ForegroundColor Red
}

# Check XML in Extensions folder
$xmlPath = Join-Path $configMgrPath "bin\XmlStorage\Extensions\Actions\CloudJourneyAddin.xml"
if (Test-Path $xmlPath) {
    Write-Host "[OK] XML manifest found in Extensions folder" -ForegroundColor Green
    Write-Host "     Location: $xmlPath" -ForegroundColor Gray
    
    # Show XML content
    Write-Host ""
    Write-Host "XML Manifest Content:" -ForegroundColor Cyan
    Get-Content $xmlPath | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
} else {
    Write-Host "[ERROR] XML manifest NOT found at: $xmlPath" -ForegroundColor Red
    Write-Host "     This is why the button doesn't appear in ConfigMgr Console!" -ForegroundColor Yellow
}

# List all XML files in Extensions folder
Write-Host ""
Write-Host "Other Extensions Found:" -ForegroundColor Cyan
$extensionsPath = Join-Path $configMgrPath "bin\XmlStorage\Extensions\Actions"
if (Test-Path $extensionsPath) {
    Get-ChildItem $extensionsPath -Filter "*.xml" | ForEach-Object {
        Write-Host "  - $($_.Name)" -ForegroundColor Gray
    }
} else {
    Write-Host "[ERROR] Extensions folder doesn't exist: $extensionsPath" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Verification Complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
