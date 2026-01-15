# Script to delete stale manifest on test device
$manifestPath = "$env:LOCALAPPDATA\ZeroTrustMigrationAddin\manifest.json"

if (Test-Path $manifestPath) {
    Remove-Item $manifestPath -Force
    Write-Host "✅ Deleted cached manifest: $manifestPath" -ForegroundColor Green
} else {
    Write-Host "⚠️ No cached manifest found at $manifestPath" -ForegroundColor Yellow
}

Write-Host "
Now launch ZeroTrustMigrationAddin v3.15.0 on the test device" -ForegroundColor Cyan
