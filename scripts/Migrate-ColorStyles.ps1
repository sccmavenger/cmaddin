<# 
.SYNOPSIS
    Migrates hardcoded hex colors to centralized StaticResource references in XAML files.
#>

param(
    [switch]$WhatIf
)

$replacements = @(
    # Text colors
    @{ Pattern = 'Foreground="#333333"'; Replace = 'Foreground="{StaticResource TextPrimary}"' }
    @{ Pattern = 'Foreground="#666666"'; Replace = 'Foreground="{StaticResource TextSecondary}"' }
    @{ Pattern = 'Foreground="#888888"'; Replace = 'Foreground="{StaticResource TextTertiary}"' }
    @{ Pattern = 'Foreground="#999999"'; Replace = 'Foreground="{StaticResource TextLight}"' }
    @{ Pattern = 'Foreground="#1F2937"'; Replace = 'Foreground="{StaticResource TextDark}"' }
    @{ Pattern = 'Foreground="#374151"'; Replace = 'Foreground="{StaticResource Gray700}"' }
    @{ Pattern = 'Foreground="#4B5563"'; Replace = 'Foreground="{StaticResource Gray600}"' }
    @{ Pattern = 'Foreground="#6B7280"'; Replace = 'Foreground="{StaticResource TextMuted}"' }
    @{ Pattern = 'Foreground="#9CA3AF"'; Replace = 'Foreground="{StaticResource TextLight}"' }
    @{ Pattern = 'Foreground="#444444"'; Replace = 'Foreground="{StaticResource TextPrimary}"' }
    
    # Value= format for Styles
    @{ Pattern = 'Value="#333333"'; Replace = 'Value="{StaticResource TextPrimary}"' }
    @{ Pattern = 'Value="#666666"'; Replace = 'Value="{StaticResource TextSecondary}"' }
    @{ Pattern = 'Value="#888888"'; Replace = 'Value="{StaticResource TextTertiary}"' }
    @{ Pattern = 'Value="#0078D4"'; Replace = 'Value="{StaticResource PrimaryBlue}"' }
    @{ Pattern = 'Value="#106EBE"'; Replace = 'Value="{StaticResource PrimaryBlueHover}"' }
    @{ Pattern = 'Value="#107C10"'; Replace = 'Value="{StaticResource SuccessGreen}"' }
    @{ Pattern = 'Value="#E0E0E0"'; Replace = 'Value="{StaticResource BorderLight}"' }
    @{ Pattern = 'Value="#F0F0F0"'; Replace = 'Value="{StaticResource BackgroundPage}"' }
    @{ Pattern = 'Value="#F5F5F5"'; Replace = 'Value="{StaticResource BackgroundPage}"' }
    @{ Pattern = 'Value="#F8F9FA"'; Replace = 'Value="{StaticResource BackgroundSubtle}"' }
    @{ Pattern = 'Value="#F0F8FF"'; Replace = 'Value="{StaticResource BackgroundAlice}"' }
    @{ Pattern = 'Value="#E8E8E8"'; Replace = 'Value="{StaticResource Gray200}"' }
    @{ Pattern = 'Value="#444444"'; Replace = 'Value="{StaticResource TextPrimary}"' }
    @{ Pattern = 'Value="#E8F4FD"'; Replace = 'Value="{StaticResource InfoBlueLight}"' }
    @{ Pattern = 'Value="#E8F5E9"'; Replace = 'Value="{StaticResource SuccessGreenLight}"' }
    @{ Pattern = 'Value="#FFEBEE"'; Replace = 'Value="{StaticResource ErrorRedLight}"' }
    @{ Pattern = 'Value="#FFF3E0"'; Replace = 'Value="{StaticResource WarningOrangeLight}"' }
    
    # Success/Green colors
    @{ Pattern = 'Foreground="#107C10"'; Replace = 'Foreground="{StaticResource SuccessGreen}"' }
    @{ Pattern = 'Foreground="#2E7D32"'; Replace = 'Foreground="{StaticResource SuccessGreenDark}"' }
    @{ Pattern = 'Foreground="#10B981"'; Replace = 'Foreground="{StaticResource SuccessGreen}"' }
    @{ Pattern = 'Foreground="#059669"'; Replace = 'Foreground="{StaticResource SuccessGreen}"' }
    @{ Pattern = 'Foreground="#4CAF50"'; Replace = 'Foreground="{StaticResource SuccessGreen}"' }
    @{ Pattern = 'Background="#E8F5E9"'; Replace = 'Background="{StaticResource SuccessGreenLight}"' }
    @{ Pattern = 'Background="#C8E6C9"'; Replace = 'Background="{StaticResource SuccessGreenMedium}"' }
    @{ Pattern = 'Background="#ECFDF5"'; Replace = 'Background="{StaticResource BackgroundMint}"' }
    @{ Pattern = 'Background="#107C10"'; Replace = 'Background="{StaticResource SuccessGreen}"' }
    
    # Warning/Orange colors
    @{ Pattern = 'Foreground="#E65100"'; Replace = 'Foreground="{StaticResource WarningOrange}"' }
    @{ Pattern = 'Foreground="#F57C00"'; Replace = 'Foreground="{StaticResource WarningOrange}"' }
    @{ Pattern = 'Foreground="#CA5010"'; Replace = 'Foreground="{StaticResource WarningOrange}"' }
    @{ Pattern = 'Foreground="#D97706"'; Replace = 'Foreground="{StaticResource WarningOrange}"' }
    @{ Pattern = 'Foreground="#FF8C00"'; Replace = 'Foreground="{StaticResource WarningOrange}"' }
    @{ Pattern = 'Foreground="#F59E0B"'; Replace = 'Foreground="{StaticResource AccentAmber}"' }
    @{ Pattern = 'Background="#FFF3E0"'; Replace = 'Background="{StaticResource WarningOrangeLight}"' }
    @{ Pattern = 'Background="#FFE0B2"'; Replace = 'Background="{StaticResource WarningOrangeMedium}"' }
    @{ Pattern = 'Background="#FEF3C7"'; Replace = 'Background="{StaticResource AccentAmberLight}"' }
    @{ Pattern = 'Background="#FF8C00"'; Replace = 'Background="{StaticResource WarningOrange}"' }
    @{ Pattern = 'Background="#E65100"'; Replace = 'Background="{StaticResource WarningOrange}"' }
    
    # Error/Red colors
    @{ Pattern = 'Foreground="#D32F2F"'; Replace = 'Foreground="{StaticResource ErrorRed}"' }
    @{ Pattern = 'Foreground="#D13438"'; Replace = 'Foreground="{StaticResource ErrorRed}"' }
    @{ Pattern = 'Foreground="#DC2626"'; Replace = 'Foreground="{StaticResource ErrorRed}"' }
    @{ Pattern = 'Foreground="#C62828"'; Replace = 'Foreground="{StaticResource ErrorRedDark}"' }
    @{ Pattern = 'Foreground="#EF4444"'; Replace = 'Foreground="{StaticResource ErrorRed}"' }
    @{ Pattern = 'Background="#FFEBEE"'; Replace = 'Background="{StaticResource ErrorRedLight}"' }
    @{ Pattern = 'Background="#FFCDD2"'; Replace = 'Background="{StaticResource ErrorRedMedium}"' }
    @{ Pattern = 'Background="#FEE2E2"'; Replace = 'Background="{StaticResource ErrorRedLight}"' }
    @{ Pattern = 'Background="#E74C3C"'; Replace = 'Background="{StaticResource ErrorRed}"' }
    @{ Pattern = 'Background="#D13438"'; Replace = 'Background="{StaticResource ErrorRed}"' }
    
    # Primary Blue
    @{ Pattern = 'Foreground="#0078D4"'; Replace = 'Foreground="{StaticResource PrimaryBlue}"' }
    @{ Pattern = 'Foreground="#3B82F6"'; Replace = 'Foreground="{StaticResource PrimaryBlue}"' }
    @{ Pattern = 'Foreground="#2563EB"'; Replace = 'Foreground="{StaticResource PrimaryBlue}"' }
    @{ Pattern = 'Background="#0078D4"'; Replace = 'Background="{StaticResource PrimaryBlue}"' }
    @{ Pattern = 'Background="#106EBE"'; Replace = 'Background="{StaticResource PrimaryBlueHover}"' }
    @{ Pattern = 'BorderBrush="#0078D4"'; Replace = 'BorderBrush="{StaticResource PrimaryBlue}"' }
    @{ Pattern = 'Stroke="#0078D4"'; Replace = 'Stroke="{StaticResource PrimaryBlue}"' }
    
    # Info Blue
    @{ Pattern = 'Foreground="#1976D2"'; Replace = 'Foreground="{StaticResource InfoBlue}"' }
    @{ Pattern = 'Foreground="#0D47A1"'; Replace = 'Foreground="{StaticResource InfoBlueDark}"' }
    @{ Pattern = 'Background="#E3F2FD"'; Replace = 'Background="{StaticResource InfoBlueLight}"' }
    @{ Pattern = 'Background="#BBDEFB"'; Replace = 'Background="{StaticResource InfoBlueMedium}"' }
    
    # Accent colors
    @{ Pattern = 'Foreground="#6A1B9A"'; Replace = 'Foreground="{StaticResource AccentPurple}"' }
    @{ Pattern = 'Foreground="#9B59B6"'; Replace = 'Foreground="{StaticResource AccentPurple}"' }
    @{ Pattern = 'Foreground="#5C2D91"'; Replace = 'Foreground="{StaticResource AccentPurple}"' }
    @{ Pattern = 'Background="#F3E5F5"'; Replace = 'Background="{StaticResource AccentPurpleLight}"' }
    @{ Pattern = 'Background="#9B59B6"'; Replace = 'Background="{StaticResource AccentPurple}"' }
    @{ Pattern = 'Background="#5C2D91"'; Replace = 'Background="{StaticResource AccentPurple}"' }
    @{ Pattern = 'Foreground="#00897B"'; Replace = 'Foreground="{StaticResource AccentTeal}"' }
    @{ Pattern = 'Background="#E0F2F1"'; Replace = 'Background="{StaticResource AccentTealLight}"' }
    
    # Borders
    @{ Pattern = 'BorderBrush="#E0E0E0"'; Replace = 'BorderBrush="{StaticResource BorderLight}"' }
    @{ Pattern = 'BorderBrush="#E5E7EB"'; Replace = 'BorderBrush="{StaticResource Gray200}"' }
    @{ Pattern = 'BorderBrush="#D1D5DB"'; Replace = 'BorderBrush="{StaticResource Gray300}"' }
    @{ Pattern = 'Stroke="#E0E0E0"'; Replace = 'Stroke="{StaticResource BorderLight}"' }
    
    # Backgrounds
    @{ Pattern = 'Background="#F8F9FA"'; Replace = 'Background="{StaticResource BackgroundSubtle}"' }
    @{ Pattern = 'Background="#F5F5F5"'; Replace = 'Background="{StaticResource BackgroundPage}"' }
    @{ Pattern = 'Background="#F9FAFB"'; Replace = 'Background="{StaticResource BackgroundMuted}"' }
    @{ Pattern = 'Background="#F3F4F6"'; Replace = 'Background="{StaticResource Gray100}"' }
    @{ Pattern = 'Background="#F0F8FF"'; Replace = 'Background="{StaticResource BackgroundAlice}"' }
    @{ Pattern = 'Background="#F0F0F0"'; Replace = 'Background="{StaticResource BackgroundPage}"' }
    @{ Pattern = 'Background="#CCCCCC"'; Replace = 'Background="{StaticResource Gray300}"' }
    @{ Pattern = 'Background="#2C3E50"'; Replace = 'Background="{StaticResource TextDark}"' }
    @{ Pattern = 'Background="#6C757D"'; Replace = 'Background="{StaticResource Gray600}"' }
    
    # Fill for shapes
    @{ Pattern = 'Fill="#107C10"'; Replace = 'Fill="{StaticResource SuccessGreen}"' }
    @{ Pattern = 'Fill="#E8F5E9"'; Replace = 'Fill="{StaticResource SuccessGreenLight}"' }
    @{ Pattern = 'Fill="#0078D4"'; Replace = 'Fill="{StaticResource PrimaryBlue}"' }
)

$viewsPath = Join-Path $PSScriptRoot "..\Views"
$xamlFiles = Get-ChildItem $viewsPath -Filter "*.xaml" -File

$totalBefore = 0
$totalAfter = 0

foreach ($file in $xamlFiles) {
    $content = Get-Content $file.FullName -Raw
    $before = ([regex]::Matches($content, '#[0-9A-Fa-f]{6}')).Count
    $totalBefore += $before
    
    if (-not $WhatIf) {
        foreach ($r in $replacements) {
            $content = $content -replace [regex]::Escape($r.Pattern), $r.Replace
        }
        Set-Content $file.FullName $content -Encoding UTF8
    }
    
    $after = ([regex]::Matches($content, '#[0-9A-Fa-f]{6}')).Count
    $totalAfter += $after
    
    $changed = $before - $after
    if ($changed -gt 0 -or $before -gt 0) {
        Write-Host "$($file.Name): $before → $after ($changed migrated)" -ForegroundColor $(if ($after -eq 0) { "Green" } elseif ($after -lt 10) { "Yellow" } else { "Cyan" })
    }
}

Write-Host ""
Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Total: $totalBefore → $totalAfter ($(($totalBefore - $totalAfter)) migrated)" -ForegroundColor White
if ($totalAfter -gt 0) {
    Write-Host "Remaining hardcoded colors: $totalAfter" -ForegroundColor Yellow
}
if ($WhatIf) {
    Write-Host "(DRY RUN - no changes made)" -ForegroundColor Yellow
}
