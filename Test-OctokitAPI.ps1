Add-Type -Path "C:\TestInstall\CloudJourney\v3.14.31\Octokit.dll"
$client = New-Object Octokit.GitHubClient -ArgumentList (New-Object Octokit.ProductHeaderValue -ArgumentList "TestScript")
try {
    $release = $client.Repository.Release.GetLatest("sccmavenger", "cmaddin").Result
    Write-Host "✅ SUCCESS: Found release $($release.TagName)" -ForegroundColor Green
    Write-Host "   Published: $($release.PublishedAt)" -ForegroundColor Cyan
    Write-Host "   Assets: $($release.Assets.Count)" -ForegroundColor Cyan
} catch {
    Write-Host "❌ FAILED: $($_.Exception.InnerException.Message)" -ForegroundColor Red
}
