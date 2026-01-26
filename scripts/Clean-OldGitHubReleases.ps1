<#
.SYNOPSIS
    Deletes old GitHub releases AND their associated git tags, keeping only the most recent ones.

.DESCRIPTION
    Uses GitHub CLI (gh) to list and delete old releases from the repository.
    Also deletes associated git tags both locally and remotely.
    By default, keeps the 10 most recent releases.

.PARAMETER KeepCount
    Number of releases to keep. Default is 10.

.PARAMETER WhatIf
    Shows what would be deleted without actually deleting.

.PARAMETER SkipTags
    Skip tag deletion (only delete releases).

.EXAMPLE
    .\Clean-OldGitHubReleases.ps1
    Deletes old releases and tags, keeping the 10 most recent.

.EXAMPLE
    .\Clean-OldGitHubReleases.ps1 -KeepCount 5
    Deletes old releases and tags, keeping only the 5 most recent.

.EXAMPLE
    .\Clean-OldGitHubReleases.ps1 -WhatIf
    Shows what would be deleted without deleting.

.EXAMPLE
    .\Clean-OldGitHubReleases.ps1 -SkipTags
    Only delete releases, keep all git tags.
#>

param(
    [int]$KeepCount = 10,
    [switch]$WhatIf,
    [switch]$SkipTags
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

# Get all releases (JSON format for parsing) - increased limit to 200
$releasesJson = gh release list --repo "$repoOwner/$repoName" --limit 200 --json tagName,publishedAt,name 2>&1

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
    Write-Host "`n⚠️ DRY RUN - No releases or tags were deleted" -ForegroundColor Yellow
    Write-Host "   Run without -WhatIf to actually delete" -ForegroundColor Yellow
    exit 0
}

# Confirm deletion
$tagWarning = if (-not $SkipTags) { " and $($deleteReleases.Count) tags" } else { "" }
Write-Host "`n⚠️ This will permanently delete $($deleteReleases.Count) releases$tagWarning!" -ForegroundColor Red
$confirm = Read-Host "Type 'DELETE' to confirm"

if ($confirm -ne "DELETE") {
    Write-Host "❌ Cancelled" -ForegroundColor Yellow
    exit 0
}

# Delete releases
$deleted = 0
$failed = 0
$tagsDeleted = 0
$tagsFailed = 0

foreach ($release in $deleteReleases) {
    $tagName = $release.tagName
    Write-Host "   Deleting release $tagName..." -ForegroundColor Gray -NoNewline
    
    $result = gh release delete $tagName --repo "$repoOwner/$repoName" --yes 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host " ✅" -ForegroundColor Green
        $deleted++
        
        # Also delete the git tag if not skipped
        if (-not $SkipTags) {
            Write-Host "   Deleting tag $tagName..." -ForegroundColor Gray -NoNewline
            
            # Delete remote tag
            $tagResult = git push origin --delete $tagName 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host " ✅ (remote)" -ForegroundColor Green
                $tagsDeleted++
                
                # Delete local tag if it exists
                git tag -d $tagName 2>$null | Out-Null
            } else {
                Write-Host " ❌ $tagResult" -ForegroundColor Red
                $tagsFailed++
            }
        }
    } else {
        Write-Host " ❌ $result" -ForegroundColor Red
        $failed++
    }
}

Write-Host "`n----------------------------------------" -ForegroundColor Cyan
Write-Host "✅ Deleted: $deleted releases" -ForegroundColor Green
if (-not $SkipTags) {
    Write-Host "✅ Deleted: $tagsDeleted tags" -ForegroundColor Green
}
if ($failed -gt 0) {
    Write-Host "❌ Failed: $failed releases" -ForegroundColor Red
}
if ($tagsFailed -gt 0) {
    Write-Host "❌ Failed: $tagsFailed tags" -ForegroundColor Red
}
Write-Host "📦 Remaining: $KeepCount releases" -ForegroundColor Cyan

# Check for orphaned tags (tags without releases)
if (-not $SkipTags) {
    Write-Host "`n🔍 Checking for orphaned tags..." -ForegroundColor Cyan
    
    # Get all remote tags
    $allTags = git ls-remote --tags origin 2>&1 | ForEach-Object { 
        if ($_ -match 'refs/tags/(.+)$') { $matches[1] -replace '\^{}$', '' }
    } | Select-Object -Unique
    
    # Get remaining release tags
    $remainingReleaseTags = $keepReleases | ForEach-Object { $_.tagName }
    
    # Find orphaned tags
    $orphanedTags = $allTags | Where-Object { $_ -notin $remainingReleaseTags }
    
    if ($orphanedTags.Count -gt 0) {
        Write-Host "   Found $($orphanedTags.Count) orphaned tags (tags without releases)" -ForegroundColor Yellow
        
        if (-not $WhatIf) {
            $deleteOrphans = Read-Host "Delete orphaned tags? (y/N)"
            if ($deleteOrphans -eq 'y') {
                foreach ($tag in $orphanedTags) {
                    Write-Host "   Deleting orphaned tag $tag..." -ForegroundColor Gray -NoNewline
                    git push origin --delete $tag 2>&1 | Out-Null
                    if ($LASTEXITCODE -eq 0) {
                        Write-Host " ✅" -ForegroundColor Green
                        git tag -d $tag 2>$null | Out-Null
                    } else {
                        Write-Host " ❌" -ForegroundColor Red
                    }
                }
            }
        } else {
            Write-Host "   Orphaned tags that would be deleted:" -ForegroundColor Gray
            foreach ($tag in $orphanedTags | Select-Object -First 20) {
                Write-Host "      $tag" -ForegroundColor DarkGray
            }
            if ($orphanedTags.Count -gt 20) {
                Write-Host "      ... and $($orphanedTags.Count - 20) more" -ForegroundColor DarkGray
            }
        }
    } else {
        Write-Host "   ✅ No orphaned tags found" -ForegroundColor Green
    }
}
