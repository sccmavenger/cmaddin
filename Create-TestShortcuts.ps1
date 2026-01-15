
<#
.SYNOPSIS
    Create test shortcuts for different tab visibility configurations
.DESCRIPTION
    Creates desktop shortcuts with various tab visibility options for testing
#>

$exePath = Resolve-Path "bin\Release\net8.0-windows\win-x64\publish\ZeroTrustMigrationAddin.exe"
$desktopPath = [Environment]::GetFolderPath("Desktop")

Write-Host "`nCreating test shortcuts on Desktop..." -ForegroundColor Cyan

# Shortcut 1: Default (All Tabs)
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$desktopPath\CloudJourney - All Tabs.lnk")
$Shortcut.TargetPath = $exePath
$Shortcut.Arguments = ""
$Shortcut.Description = "Zero Trust Migration Journey - All Tabs Visible"
$Shortcut.IconLocation = "$exePath,0"
$Shortcut.Save()
Write-Host "✓ Created: CloudJourney - All Tabs.lnk" -ForegroundColor Green

# Shortcut 2: Enrollment Only
$Shortcut = $WshShell.CreateShortcut("$desktopPath\CloudJourney - Enrollment Focus.lnk")
$Shortcut.TargetPath = $exePath
$Shortcut.Arguments = "/showtabs:enrollment"
$Shortcut.Description = "Zero Trust Migration Journey - Enrollment Focus"
$Shortcut.IconLocation = "$exePath,0"
$Shortcut.Save()
Write-Host "✓ Created: CloudJourney - Enrollment Focus.lnk" -ForegroundColor Green

# Shortcut 3: Workloads Focus
$Shortcut = $WshShell.CreateShortcut("$desktopPath\CloudJourney - Workloads.lnk")
$Shortcut.TargetPath = $exePath
$Shortcut.Arguments = "/showtabs:workloads,brainstorm"
$Shortcut.Description = "Zero Trust Migration Journey - Workloads & Planning"
$Shortcut.IconLocation = "$exePath,0"
$Shortcut.Save()
Write-Host "✓ Created: CloudJourney - Workloads.lnk" -ForegroundColor Green

# Shortcut 4: Core Features (No AI)
$Shortcut = $WshShell.CreateShortcut("$desktopPath\CloudJourney - Core Features.lnk")
$Shortcut.TargetPath = $exePath
$Shortcut.Arguments = "/hidetabs:brainstorm,aiactions"
$Shortcut.Description = "Zero Trust Migration Journey - Core Features Only"
$Shortcut.IconLocation = "$exePath,0"
$Shortcut.Save()
Write-Host "✓ Created: CloudJourney - Core Features.lnk" -ForegroundColor Green

# Shortcut 5: Technical View
$Shortcut = $WshShell.CreateShortcut("$desktopPath\CloudJourney - Technical.lnk")
$Shortcut.TargetPath = $exePath
$Shortcut.Arguments = "/showtabs:enrollment,workloads,applications"
$Shortcut.Description = "Zero Trust Migration Journey - Technical View"
$Shortcut.IconLocation = "$exePath,0"
$Shortcut.Save()
Write-Host "✓ Created: CloudJourney - Technical.lnk" -ForegroundColor Green

Write-Host "`n✓ All test shortcuts created on Desktop!" -ForegroundColor Cyan
Write-Host "`nYou can now test different tab visibility configurations by clicking the shortcuts." -ForegroundColor White
Write-Host "Remember: Overview tab is always visible, and Security/Savings sections are hidden." -ForegroundColor Gray
