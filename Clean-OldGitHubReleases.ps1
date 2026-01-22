<#
.SYNOPSIS
    Deletes old GitHub releases, keeping only the most recent ones.

.DESCRIPTION
    Uses GitHub CLI (gh) to list and delete old releases from the repository.
    By default, keeps the 10 most recent releases.

.PARAMETER KeepCount
    Number of releases to keep. Default is 10.

.PARAMETER WhatIf
    Shows what would be deleted without actually deleting.

.EXAMPLE
    .\Clean-OldGitHubReleases.ps1
    Deletes old releases, keeping the 10 most recent.

.EXAMPLE
    .\Clean-OldGitHubReleases.ps1 -KeepCount 5
    Deletes old releases, keeping only the 5 most recent.

.EXAMPLE
    .\Clean-OldGitHubReleases.ps1 -WhatIf
    Shows what would be deleted without deleting.
#>

param(
    [int]$KeepCount = 10,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

Write-Host "🔍 Checking GitHub CLI..." -ForegroundColor Cyan

# Check if gh is installed
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Host "❌ GitHub CLI (gh) is not installed." -ForegroundColor Red
    Write-Host "   Install it from: https://cli.github.com/" -ForegroundColor Yellow
    exit 1
}

# Check if authenticated
$authStatus = gh auth status 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Not authenticated with GitHub CLI." -ForegroundColor Red
    Write-Host "   Run: gh auth login" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ GitHub CLI authenticated" -ForegroundColor Green

# Get repository info
$repoOwner = "sccmavenger"
$repoName = "cmaddin"

Write-Host "`n📋 Fetching releases from $repoOwner/$repoName..." -ForegroundColor Cyan

# Get all releases (JSON format for parsing)
$releasesJson = gh release list --repo "$repoOwner/$repoName" --limit 100 --json tagName,publishedAt,name 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to fetch releases: $releasesJson" -ForegroundColor Red
    exit 1
}

$releases = $releasesJson | ConvertFrom-Json

if ($releases.Count -eq 0) {
    Write-Host "ℹ️ No releases found." -ForegroundColor Yellow
    exit 0
}

Write-Host "   Found $($releases.Count) releases" -ForegroundColor Gray

# Sort by publishedAt (newest first)
$sortedReleases = $releases | Sort-Object { [DateTime]$_.publishedAt } -Descending

# Separate into keep and delete
$keepReleases = $sortedReleases | Select-Object -First $KeepCount
$deleteReleases = $sortedReleases | Select-Object -Skip $KeepCount

Write-Host "`n✅ Keeping $($keepReleases.Count) most recent releases:" -ForegroundColor Green
foreach ($release in $keepReleases) {
    $date = ([DateTime]$release.publishedAt).ToString("yyyy-MM-dd")
    Write-Host "   📦 $($release.tagName) ($date)" -ForegroundColor Gray
}

if ($deleteReleases.Count -eq 0) {
    Write-Host "`n✅ No old releases to delete (already at or below $KeepCount)" -ForegroundColor Green
    exit 0
}

Write-Host "`n🗑️ Releases to delete ($($deleteReleases.Count)):" -ForegroundColor Yellow
foreach ($release in $deleteReleases) {
    $date = ([DateTime]$release.publishedAt).ToString("yyyy-MM-dd")
    Write-Host "   ❌ $($release.tagName) ($date)" -ForegroundColor DarkGray
}

if ($WhatIf) {
    Write-Host "`n⚠️ DRY RUN - No releases were deleted" -ForegroundColor Yellow
    Write-Host "   Run without -WhatIf to actually delete" -ForegroundColor Yellow
    exit 0
}

# Confirm deletion
Write-Host "`n⚠️ This will permanently delete $($deleteReleases.Count) releases!" -ForegroundColor Red
$confirm = Read-Host "Type 'DELETE' to confirm"

if ($confirm -ne "DELETE") {
    Write-Host "❌ Cancelled" -ForegroundColor Yellow
    exit 0
}

# Delete releases
$deleted = 0
$failed = 0

foreach ($release in $deleteReleases) {
    Write-Host "   Deleting $($release.tagName)..." -ForegroundColor Gray -NoNewline
    
    $result = gh release delete $release.tagName --repo "$repoOwner/$repoName" --yes 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host " ✅" -ForegroundColor Green
        $deleted++
    } else {
        Write-Host " ❌ $result" -ForegroundColor Red
        $failed++
    }
}

Write-Host "`n----------------------------------------" -ForegroundColor Cyan
Write-Host "✅ Deleted: $deleted releases" -ForegroundColor Green
if ($failed -gt 0) {
    Write-Host "❌ Failed: $failed releases" -ForegroundColor Red
}
Write-Host "📦 Remaining: $KeepCount releases" -ForegroundColor Cyan
