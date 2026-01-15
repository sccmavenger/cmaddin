
<#
.SYNOPSIS
    Test script for Tab Visibility features
.DESCRIPTION
    Launches the Zero Trust Migration Journey with various tab visibility configurations
#>

$exePath = "bin\Release\net8.0-windows\win-x64\publish\ZeroTrustMigrationAddin.exe"

Write-Host "`n=== Zero Trust Migration Journey - Tab Visibility Testing ===" -ForegroundColor Cyan
Write-Host "Executable: $exePath`n" -ForegroundColor White

if (-not (Test-Path $exePath)) {
    Write-Host "Error: Executable not found at $exePath" -ForegroundColor Red
    Write-Host "Please run: dotnet publish ZeroTrustMigrationAddin.csproj -c Release --self-contained true -r win-x64" -ForegroundColor Yellow
    exit 1
}

Write-Host "Choose a test scenario:" -ForegroundColor Yellow
Write-Host "  1. Default - All tabs visible (no arguments)" -ForegroundColor White
Write-Host "  2. Hide Enrollment and Workloads" -ForegroundColor White
Write-Host "  3. Show only Enrollment tab" -ForegroundColor White
Write-Host "  4. Show only Workloads and Brainstorm" -ForegroundColor White
Write-Host "  5. Hide all AI features (Brainstorm and AI Actions)" -ForegroundColor White
Write-Host "  6. Show only Applications tab" -ForegroundColor White
Write-Host "  7. Custom arguments (enter manually)" -ForegroundColor White
Write-Host ""

$choice = Read-Host "Enter choice (1-7)"

switch ($choice) {
    "1" {
        Write-Host "`nLaunching with all tabs visible..." -ForegroundColor Green
        & $exePath
    }
    "2" {
        Write-Host "`nLaunching with Enrollment and Workloads hidden..." -ForegroundColor Green
        & $exePath /hidetabs:enrollment,workloads
    }
    "3" {
        Write-Host "`nLaunching with only Enrollment tab (+ Overview)..." -ForegroundColor Green
        & $exePath /showtabs:enrollment
    }
    "4" {
        Write-Host "`nLaunching with Workloads and Brainstorm tabs..." -ForegroundColor Green
        & $exePath /showtabs:workloads,brainstorm
    }
    "5" {
        Write-Host "`nLaunching with AI features hidden..." -ForegroundColor Green
        & $exePath /hidetabs:brainstorm,aiactions
    }
    "6" {
        Write-Host "`nLaunching with only Applications tab..." -ForegroundColor Green
        & $exePath /showtabs:applications
    }
    "7" {
        $customArgs = Read-Host "Enter custom arguments (e.g., /hidetabs:enrollment,apps)"
        Write-Host "`nLaunching with custom arguments: $customArgs" -ForegroundColor Green
        & $exePath $customArgs
    }
    default {
        Write-Host "Invalid choice. Launching with default settings..." -ForegroundColor Yellow
        & $exePath
    }
}

Write-Host "`nApplication launched!" -ForegroundColor Green
Write-Host "Note: Overview tab is always visible" -ForegroundColor Gray
Write-Host "Security & Compliance Scorecard and Savings sections are hidden on Overview tab" -ForegroundColor Gray
