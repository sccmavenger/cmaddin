# Version Management Strategy

**Current Version:** 3.14.31 (as of January 13, 2026)

**Latest Feature:** Automatic update system with GitHub Releases integration

## Versioning Scheme

This project follows **Semantic Versioning 2.0.0** (https://semver.org):

```
MAJOR.MINOR.PATCH
  |     |     |
  |     |     +-- Bug fixes, no new features (1.4.0 → 1.4.1)
  |     +-------- New features, backward compatible (1.4.0 → 1.5.0)
  +-------------- Breaking changes, major rewrites (1.4.0 → 2.0.0)
```

### Version Increment Rules

**PATCH (x.x.PATCH)** - Increment when:
- Bug fixes only
- Performance improvements
- Documentation updates
- No new features
- No API changes
- Example: Fix Azure.Identity.dll missing → 1.4.0 → 1.4.1

**MINOR (x.MINOR.x)** - Increment when:
- New features added
- New workload detection
- Enhanced UI components
- Backward compatible changes
- Example: Add automatic shortcut creation → 1.4.0 → 1.5.0

**MAJOR (MAJOR.x.x)** - Increment when:
- Breaking changes
- Major architecture rewrites
- Incompatible API changes
- Remove deprecated features
- Example: Rewrite from WPF to Blazor → 1.4.0 → 2.0.0

## Pre-Release Versions (Future)

When implementing:
- **Alpha:** `1.5.0-alpha.1` - Internal testing only
- **Beta:** `1.5.0-beta.1` - Limited user testing
- **RC:** `1.5.0-rc.1` - Release candidate, final testing

## Version Update Checklist

**EVERY build release MUST update these 4 locations:**

### 1. CloudJourneyAddin.csproj (Lines 9-12)
```xml
<Version>1.4.0</Version>
<AssemblyVersion>1.4.0.0</AssemblyVersion>
<FileVersion>1.4.0.0</FileVersion>
```

**Format:**
- `Version`: MAJOR.MINOR.PATCH (e.g., 1.4.0)
- `AssemblyVersion`: MAJOR.MINOR.PATCH.0 (e.g., 1.4.0.0)
- `FileVersion`: MAJOR.MINOR.PATCH.0 (e.g., 1.4.0.0)

### 2. README.md (Line 3)
```markdown
**Version 1.4.0** | December 17, 2025 (Release Title)
```

**Also add release notes section:**
```markdown
## 🔧 What's New in Version X.X.X (Release Title)

### Feature Summary
Brief description of changes

#### What's New
- ✅ Feature 1
- ✅ Feature 2
```

### 3. Views/DashboardWindow.xaml (Line 6)
```xml
Title="Cloud Journey Progress Dashboard v1.4.0"
```

### 4. ViewModels/DashboardViewModel.cs (Constructor)
```csharp
_fileLogger.Log(FileLogger.LogLevel.INFO, "Dashboard version: 1.4.0");
```

### 5. CHANGELOG.md (If exists)
Add entry at top:
```markdown
## [1.4.0] - 2025-12-17
### Added
- Feature descriptions

### Fixed
- Bug fixes
```

## Build and Deployment Process

**CRITICAL: Application is NOT run on the development machine. All builds are deployed to remote PCs.**

### Standard Build Process (After Version Update)

**Step 1: Update Version (4 locations above)**

**Step 2: Build and Package**
```powershell
# Clean build
dotnet clean -c Release

# Build project
dotnet build -c Release

# Publish with all dependencies
dotnet publish -c Release -r win-x64 --self-contained true

# Create complete package
.\Build-Package.ps1  # Or manual compression
```

**Step 3: Copy to Distribution Folder (REQUIRED)**
```powershell
# After successful build, ALWAYS copy package to Dropbox for deployment
Copy-Item "CloudJourneyAddin-v{VERSION}-COMPLETE.zip" -Destination "C:\Users\dannygu\Dropbox\" -Force
```

**Distribution Location:** `C:\Users\dannygu\Dropbox\`
- All release packages MUST be copied here after successful build
- This folder is used to transfer packages to target deployment machines
- Remote users will download from this location to deploy

**Step 4: Deploy on Target Machine**
1. Copy package from `C:\Users\dannygu\Dropbox\` to target PC
2. Extract ALL files (ensure ~500 files extracted, not partial)
3. Run `Diagnose-Installation.ps1` to verify all dependencies present
4. Run `Update-CloudJourneyAddin.ps1` to deploy to ConfigMgr Console
   - OR use `Quick-Deploy.ps1` for standalone testing

### Automated Build Script (Recommended)

Use this script for complete build + distribution:

```powershell
.\Build-And-Distribute.ps1 -Version "1.4.0"
```

This script will:
1. Verify version updated in all 4 locations
2. Clean and rebuild project
3. Publish with all dependencies
4. Create compressed package
5. Verify package integrity (file count, Azure.Identity.dll present)
6. Copy to `C:\Users\dannygu\Dropbox\` automatically
7. Display summary with package location

### Deployment Workflow

```
[Dev Machine]                    [Distribution]              [Target PC]
    │                                 │                           │
    ├─ Update version (4 files)      │                           │
    ├─ Build & Publish               │                           │
    ├─ Create ZIP package            │                           │
    └─ Copy to Dropbox ──────────────┤                           │
                                      │                           │
                                      └─ Download ZIP ────────────┤
                                                                  │
                                                                  ├─ Extract ZIP
                                                                  ├─ Run Diagnose-Installation.ps1
                                                                  ├─ Run Update-CloudJourneyAddin.ps1
                                                                  └─ Launch CloudJourneyAddin.exe
```

### Version Verification (On Target PC)

After deployment, verify:
1. **File Properties:** Right-click CloudJourneyAddin.exe → Properties → Details → File version: 1.4.0.0
2. **Window Title:** Launch app → Title bar shows "Cloud Journey Progress Dashboard v1.4.0"
3. **Logs:** Click "Open Logs" → Check for "Dashboard version: 1.4.0"
4. **No Missing DLLs:** Click "Connect to Microsoft Graph" → Should NOT see "Could not load Azure.Identity.dll" error

## Version History

| Version | Date | Type | Summary |
|---------|------|------|---------|
| 1.4.0 | 2025-12-17 | MINOR | Enrollment blocker detection, User Guide button, shortcut automation |
| 1.3.10 | 2025-12-16 | PATCH | OData v4 query syntax fix (HTTP 404 → 200) |
| 1.3.9 | 2025-12-16 | PATCH | File logging system implementation |
| 1.3.8 | 2025-12-16 | PATCH | Trust restoration - removed mock data |
| 1.2.2 | 2025-12-15 | PATCH | Windows 10/11 filtering enhancement |
| 1.2.1 | 2025-12-15 | PATCH | Server filtering fix |
| 1.2.0 | 2025-12-14 | MINOR | AI-powered migration guidance |
| 1.1.3 | 2025-12-13 | PATCH | Documentation enhancement |
| 1.1.2 | 2025-12-13 | PATCH | XAML binding fixes |
| 1.1.0 | 2025-12-12 | MINOR | Real Intune alerts, dynamic workload status |
| 1.0.0 | 2025-12-10 | MAJOR | Initial release |

## Next Version Planning

**Version 1.4.1 (PATCH - Next Planned):**
- Fix XAML formatting corruption in DashboardWindow.xaml
- Enhance diagnostic tool with .NET runtime checks
- Improve error messages for missing dependencies

**Version 1.5.0 (MINOR - Future):**
- Automatic milestone detection from tenant data
- Real blocker detection (replace example data)
- Enhanced workload detection (Windows Update rings, Endpoint Protection)
- Azure Cost Management API integration for real ROI

**Version 2.0.0 (MAJOR - Long-term):**
- MSI installer (prevent partial extraction issues)
- Auto-updater (built-in update mechanism)
- Multi-tenant support
- ConfigMgr Console integration (COM registration)

## Version Update Automation (Future Enhancement)

Consider creating PowerShell script:
```powershell
.\Update-Version.ps1 -NewVersion "1.5.0" -ReleaseTitle "Feature Name"
```

Would automatically update all 4 locations + generate CHANGELOG entry.

## Build & Package Naming Convention

**Build Output:**
- `CloudJourneyAddin.exe` (version in file properties)

**Update Packages:**
- `CloudJourneyAddin-v1.4.0-COMPLETE.zip` (full package with all dependencies)
- `CloudJourneyAddin-v1.4.1-PATCH.zip` (small update, critical files only)

**Naming Format:**
```
CloudJourneyAddin-v{VERSION}-{TYPE}.zip
                    |         |
                    |         +-- COMPLETE, PATCH, HOTFIX, ALPHA, BETA, RC
                    +------------ 1.4.0, 1.4.1, etc.
```

## Version Verification

**After updating version, verify:**

1. **Build succeeds:** `dotnet build -c Release`
2. **File Properties correct:**
   ```powershell
   (Get-Item "bin\Release\net8.0-windows\win-x64\publish\CloudJourneyAddin.exe").VersionInfo
   ```
   Should show: `FileVersion: 1.4.0.0`, `ProductVersion: 1.4.0`

3. **Window title matches:** Launch app, verify title bar shows "v1.4.0"

4. **Logs show correct version:**
   ```powershell
   Get-Content "$env:LOCALAPPDATA\CloudJourneyAddin\Logs\*.log" | Select-String "Dashboard version"
   ```
   Should show: `Dashboard version: 1.4.0`

5. **README.md matches:** First line should show current version

## Git Tagging Strategy

**After releasing, create Git tag:**

```bash
git tag -a v1.4.0 -m "Release 1.4.0: Enrollment Blocker Detection"
git push origin v1.4.0
```

**Tag format:** `v{VERSION}` (e.g., v1.4.0, v1.5.0)

## Release Notes Template

When incrementing version, add to README.md:

```markdown
## 🔧 What's New in Version {VERSION} ({RELEASE_TITLE})

### {Category Summary}
Brief 1-2 sentence description of the release focus.

#### What's New
- ✅ **Feature 1** - Description with impact
- ✅ **Feature 2** - Description with impact
- 🐛 **Bug Fix** - Description of what was broken and how it's fixed

#### Technical Changes
- File/Component modified
- API changes
- Dependencies updated

**Impact:** 1-2 sentences explaining why this release matters to users.

---
```

## Emergency Hotfix Process

**For critical bugs requiring immediate release:**

1. Increment PATCH version (e.g., 1.4.0 → 1.4.1)
2. Update all 4 version locations
3. Build and test fix
4. Create package: `CloudJourneyAddin-v1.4.1-HOTFIX.zip`
5. Document in README.md with 🚨 emoji
6. Git tag: `v1.4.1-hotfix`

## Version Control Best Practices

- **Commit message format:** `chore: Bump version to 1.4.1`
- **Include:** All 4 version locations in same commit
- **Tag after merge:** Don't tag until tested and merged to main branch
- **CHANGELOG.md:** Update in same commit as version bump

---

**Last Updated:** December 17, 2025
**Current Maintainer:** GitHub Copilot
**Next Review:** After v1.5.0 release
