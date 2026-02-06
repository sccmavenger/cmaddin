# Auto-Update Manual Test Script
# This script helps you test the auto-update mechanism step-by-step

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('3.14.31', '3.14.32', '3.14.33')]
    [string]$Version
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     Cloud Native Assessment Auto-Update Manual Test Launcher           ║" -ForegroundColor Cyan
Write-Host "╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

$testRootPath = "C:\TestInstall\CloudJourney"
$versionPath = Join-Path $testRootPath "v$Version"
$packageName = "ZeroTrustMigrationAddin-v$Version-COMPLETE.zip"

# Check if package exists
if (!(Test-Path $packageName)) {
    Write-Host "❌ Package not found: $packageName" -ForegroundColor Red
    Write-Host "   Current directory: $PWD" -ForegroundColor Yellow
    exit 1
}

Write-Host "📦 Test Version: v$Version" -ForegroundColor Cyan
Write-Host "📂 Install Path: $versionPath" -ForegroundColor Cyan
Write-Host ""

# Clean previous test installation
if (Test-Path $versionPath) {
    Write-Host "🧹 Cleaning previous test installation..." -ForegroundColor Yellow
    
    # Kill any running instances
    Get-Process -Name "ZeroTrustMigrationAddin" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1
    
    Remove-Item $versionPath -Recurse -Force
    Write-Host "   ✅ Cleaned" -ForegroundColor Green
}

# Create test directory
Write-Host "📁 Creating test directory..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $versionPath -Force | Out-Null
Write-Host "   ✅ Created: $versionPath" -ForegroundColor Green
Write-Host ""

# Extract package
Write-Host "📦 Extracting package..." -ForegroundColor Yellow
Expand-Archive -Path $packageName -DestinationPath $versionPath -Force

$fileCount = (Get-ChildItem $versionPath -File).Count
Write-Host "   ✅ Extracted $fileCount files" -ForegroundColor Green
Write-Host ""

# Verify critical files
Write-Host "✅ Verifying installation..." -ForegroundColor Yellow
$criticalFiles = @("ZeroTrustMigrationAddin.exe", "Azure.Identity.dll", "Microsoft.Graph.dll")
$allPresent = $true

foreach ($file in $criticalFiles) {
    $path = Join-Path $versionPath $file
    if (Test-Path $path) {
        $size = [math]::Round((Get-Item $path).Length / 1KB, 0)
        Write-Host "   ✅ $file ($size KB)" -ForegroundColor Green
    } else {
        Write-Host "   ❌ Missing: $file" -ForegroundColor Red
        $allPresent = $false
    }
}

if (!$allPresent) {
    Write-Host ""
    Write-Host "❌ Installation incomplete!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║               READY TO TEST AUTO-UPDATE!                  ║" -ForegroundColor Green
Write-Host "╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "📋 TESTING INSTRUCTIONS:" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "1️⃣  Application will launch shortly" -ForegroundColor White
Write-Host "2️⃣  Check version displayed in UI (should show v$Version)" -ForegroundColor White
Write-Host "3️⃣  Update check happens automatically on launch" -ForegroundColor White
Write-Host "4️⃣  If update available, note:" -ForegroundColor White
Write-Host "      • New version number" -ForegroundColor Gray
Write-Host "      • Download size (delta vs full)" -ForegroundColor Gray
Write-Host "      • Update time estimate" -ForegroundColor Gray
Write-Host "5️⃣  Click 'Update Now' to test update" -ForegroundColor White
Write-Host "6️⃣  After update, verify:" -ForegroundColor White
Write-Host "      • New version displayed in UI" -ForegroundColor Gray
Write-Host "      • Application functions normally" -ForegroundColor Gray
Write-Host "      • Check logs: %LOCALAPPDATA%\CloudJourney\Logs" -ForegroundColor Gray
Write-Host ""
Write-Host "💡 Manual Update Check:" -ForegroundColor Cyan
Write-Host "   Menu → Help → Check for Updates" -ForegroundColor White
Write-Host ""
Write-Host "📝 Logs Location:" -ForegroundColor Cyan
Write-Host "   $env:LOCALAPPDATA\CloudJourney\Logs\" -ForegroundColor White
Write-Host ""
Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "Press Enter to launch ZeroTrustMigrationAddin v$Version..." -ForegroundColor Yellow
Read-Host

# Launch application
$exePath = Join-Path $versionPath "ZeroTrustMigrationAddin.exe"
Write-Host "🚀 Launching application..." -ForegroundColor Green
Write-Host ""

Start-Process -FilePath $exePath -WorkingDirectory $versionPath

Write-Host "✅ Application launched!" -ForegroundColor Green
Write-Host ""
Write-Host "👀 Watch the application for update prompts..." -ForegroundColor Cyan
Write-Host "📊 Monitor progress and take notes on testing form" -ForegroundColor Cyan
Write-Host ""
Write-Host "To view logs in real-time:" -ForegroundColor Yellow
Write-Host "   Get-Content `"$env:LOCALAPPDATA\CloudJourney\Logs\*.log`" -Wait -Tail 20" -ForegroundColor Gray
Write-Host ""
