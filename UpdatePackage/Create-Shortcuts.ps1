# Create Desktop Shortcut for Cloud Journey Dashboard

$configMgrPath = "F:\Program Files\Microsoft Configuration Manager\AdminConsole"
$exePath = Join-Path $configMgrPath "bin\bin\CloudJourneyAddin\CloudJourneyAddin.exe"

if (-not (Test-Path $exePath)) {
    Write-Host "[ERROR] CloudJourneyAddin.exe not found!" -ForegroundColor Red
    exit 1
}

# Create desktop shortcut
$desktopPath = [Environment]::GetFolderPath("Desktop")
$shortcutPath = Join-Path $desktopPath "Cloud Journey Dashboard.lnk"

$WScriptShell = New-Object -ComObject WScript.Shell
$shortcut = $WScriptShell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.Description = "View cloud migration progress and insights"
$shortcut.WorkingDirectory = Split-Path $exePath
$shortcut.Save()

Write-Host "[OK] Desktop shortcut created: $shortcutPath" -ForegroundColor Green
Write-Host ""
Write-Host "You can now launch the Cloud Journey Dashboard from your desktop!" -ForegroundColor Cyan
Write-Host ""

# Also create Start Menu shortcut
$startMenuPath = [Environment]::GetFolderPath("StartMenu")
$programsPath = Join-Path $startMenuPath "Programs"
$shortcutPath2 = Join-Path $programsPath "Cloud Journey Dashboard.lnk"

$shortcut2 = $WScriptShell.CreateShortcut($shortcutPath2)
$shortcut2.TargetPath = $exePath
$shortcut2.Description = "View cloud migration progress and insights"
$shortcut2.WorkingDirectory = Split-Path $exePath
$shortcut2.Save()

Write-Host "[OK] Start Menu shortcut created" -ForegroundColor Green
Write-Host ""
