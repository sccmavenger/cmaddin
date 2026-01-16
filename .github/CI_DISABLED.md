# GitHub Actions CI - Disabled

**Date:** January 16, 2026  
**Decision:** CI workflows removed in favor of local build script

## Why CI Was Disabled

1. **Redundant Build Process** - The `Build-And-Distribute.ps1` PowerShell script handles the complete CI/CD pipeline locally:
   - Builds the solution
   - Creates versioned packages
   - Generates manifests for delta updates
   - Publishes to GitHub Releases
   - Copies to Dropbox distribution folder

2. **RuntimeIdentifier Conflict** - The project is configured for self-contained win-x64 deployment with these settings in the csproj:
   ```xml
   <SelfContained>true</SelfContained>
   <RuntimeIdentifier>win-x64</RuntimeIdentifier>
   <PublishReadyToRun>true</PublishReadyToRun>
   ```
   These settings caused the GitHub Actions `dotnet restore` and `dotnet build` steps to fail because they conflict with the default multi-platform build process.

3. **No Value Added** - The CI workflow only validated builds; it didn't create releases. All releases are created manually via the PowerShell script which provides better control and includes smoke testing.

## Files Removed

- `.github/workflows/ci.yml` - Ran on every push to validate builds
- `.github/workflows/build-and-publish.yml` - Unused publish workflow

## How to Build & Publish

Use the local PowerShell script:

```powershell
# Build and publish to GitHub
.\Build-And-Distribute.ps1 -Publish -Force

# Build only (no publish)
.\Build-And-Distribute.ps1
```

## Re-enabling CI (If Needed Later)

If you need CI in the future, you have two options:

### Option 1: Fix csproj for CI Compatibility

Remove the `RuntimeIdentifier` from the csproj and specify it only during publish:

```xml
<!-- Remove this line from csproj -->
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
```

Then update the CI workflow to specify the runtime during publish only.

### Option 2: Create CI That Mirrors Local Build

Create a workflow that runs the same commands as `Build-And-Distribute.ps1`:

```yaml
name: CI Build

on:
  push:
    branches: [main]

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Build
        run: dotnet build cmaddin.sln --configuration Release -r win-x64
      - name: Publish
        run: dotnet publish --configuration Release -r win-x64 --self-contained true
```

## Reference

- Build script: `Build-And-Distribute.ps1`
- Build logs: `builds/logs/`
- Archived builds: `builds/archive/`
