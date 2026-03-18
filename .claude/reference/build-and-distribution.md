# Build & Distribution Patterns - Cloud Native Assessment

## Build Script
- Single entry point: `Build-And-Distribute.ps1` at repository root
- Key flags:
  - `-PublishToGitHub` — Full build + GitHub Release upload
  - `-Force` — Skip confirmation prompts
  - `-SkipBuild` — Skip dotnet build (use existing output)

### Build Flow
```
Increment version in .csproj
    ↓
dotnet publish -c Release -r win-x64 --self-contained
    ↓
Create ZIP: ZeroTrustMigrationAddin-v{version}.zip
    ↓
Generate manifest.json with version, URL, checksum
    ↓
Upload ZIP + manifest.json to GitHub Release
    ↓
Update CHANGELOG.md
```

## Version Scheme
- Format: `Major.Minor.Patch` (e.g., 3.17.247)
- Major: Reserved (currently 3)
- Minor: Feature milestone (17 = cloud native focus)
- Patch: Auto-incremented per build

## Auto-Update System

### Client Side
- `GitHubUpdateService` checks for updates on startup
- Downloads `manifest.json` from latest GitHub Release
- Compares `LatestVersion` against current assembly version
- If newer: downloads ZIP, extracts, applies via `UpdateApplier`

### manifest.json Format
```json
{
  "LatestVersion": "3.17.247",
  "DownloadUrl": "https://github.com/sccmavenger/cmaddin/releases/download/v3.17.247/ZeroTrustMigrationAddin-v3.17.247.zip",
  "ReleaseNotes": "...",
  "Checksum": "SHA256 hash"
}
```

### Critical Rule
Both `manifest.json` AND the ZIP file MUST be uploaded as release assets. If either is missing, auto-update fails silently.

## MSI Installer

### WiX Toolset 6.0
- Installer files in `installer/` directory
- `Product.wxs` — Main product definition
- `ApplicationFiles.wxs` — File manifest (auto-generated)
- `Bundle.wxs` — Bootstrapper bundle
- `Generate-ApplicationFiles.ps1` — Regenerates file list from publish output
- `Build-Installer.ps1` — Builds MSI from WiX sources

### Install Target
- ConfigMgr console extensions directory
- Requires admin rights for installation
- Adds Start Menu shortcut

## GitHub Releases

### Release Management
- `scripts/Clean-OldGitHubReleases.ps1` — Prune old releases
- Uses GitHub CLI (`gh`) for API operations
- Also available via Octokit in `GitHubUpdateService`

### Build Outputs
- ZIP files: `builds/ZeroTrustMigrationAddin-v{version}.zip`
- WiX outputs: `builds/*.wixpdb`
- Manifests: `builds/manifests/`
- Build logs: `builds/logs/`

## Testing
- Test auto-update: `scripts/Test-AutoUpdate.ps1`
- Test mock data: Run app without connecting to Graph/ConfigMgr
- Test build: `dotnet build` (should complete with 0 errors)

## Diagnostic Logging
- All logs: `%LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs\`
- `FileLogger.Instance` — Singleton logger
- Methods: `Info()`, `Warn()`, `Error()`, `LogGraphQuery()`, `LogAdminServiceQuery()`, `LogWmiQuery()`
- Diagnostics window: `DiagnosticsWindow.xaml` shows real-time log viewer
