# GitHub Copilot Instructions for Cloud Native Assessment

## Project Overview
This is a WPF add-in for Microsoft Configuration Manager (ConfigMgr/SCCM) that helps IT administrators plan and execute migrations to Microsoft Intune cloud-native management.

## Architecture
- **Framework**: .NET 8.0 WPF with MVVM pattern
- **Data Sources**: Microsoft Graph API (Intune), ConfigMgr Admin Service (REST), WMI fallback
- **Update System**: GitHub Releases with manifest.json + ZIP assets
- **Logging**: FileLogger singleton to %LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs\

## Key Directories
- `Services/` - Business logic, API integrations, logging
- `Models/` - Data models and DTOs
- `Views/` - XAML UI components
- `ViewModels/` - MVVM view models
- `Constants/` - Static constants and terminology

## Documentation Requirements

### ALWAYS Update These Files When Making Changes:

1. **CHANGELOG.md** - Add entry for EVERY change with:
   - Version number (increment patch for fixes, minor for features)
   - Date
   - Category: Added, Changed, Fixed, Security, Deprecated
   - Description of what changed and WHY
   - Files modified

2. **README.md** - Update if:
   - New features are added
   - Setup/installation steps change
   - New dependencies added

3. **DECISIONS.md** - Document architectural decisions:
   - WHY a particular approach was chosen
   - Alternatives considered
   - Trade-offs made

4. **CONTEXT.md** - Update current project state:
   - Active features in development
   - Known issues
   - Next planned work

### Commit Message Format (Conventional Commits)
```
<type>(<scope>): <description>

[optional body with WHY and DECISIONS]

[optional footer]
```

Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `refactor`: Code change that neither fixes nor adds
- `test`: Adding tests
- `chore`: Build, CI, dependencies

Example:
```
feat(migration): add Migration Impact Analysis feature

Adds 6-category impact analysis with before/after projections.
Categories: Security, Operations, UX, Cost, Compliance, Modernization.

DECISION: Used percentage-based scoring (0-100) for consistency
with existing Enrollment Confidence metrics.

DECISION: Mock data shown when disconnected to demonstrate
feature capabilities without live environment.
```

## Build & Release Checklist
1. Update version in CloudJourneyAddin.csproj
2. Update CHANGELOG.md with new version entry
3. Run: `.\Build-And-Distribute.ps1 -PublishToGitHub`
4. CRITICAL: Both ZIP and manifest.json MUST be in release assets

## Code Patterns

### Adding New Services
```csharp
// Inject FileLogger for query logging
private readonly FileLogger _logger = FileLogger.Instance;

// Log all external queries
_logger.LogGraphQuery("endpoint", "query details");
_logger.LogAdminServiceQuery("endpoint", "query details");
_logger.LogWmiQuery("namespace", "WQL query");
```

### Adding New Views
1. Create XAML in Views/
2. Create code-behind with mock data for disconnected state
3. Register any new commands
4. Update DiagnosticsWindow if debugging info needed

## Common Gotchas
- GraphDataService uses `GetCachedManagedDevicesAsync()` not `GetDevicesAsync()`
- Compliance property is `OverallComplianceRate` not `ComplianceRate`
- Update logs go to %LOCALAPPDATA% not %TEMP%
- GitHub releases need manifest.json uploaded separately

## Testing
- Always test with mock/disconnected data first
- Test auto-update by checking manifest.json accessibility
- Run `scripts/Test-AutoUpdate.ps1` to verify update system

---

## File Organization Rules

### Directory Structure
```
root/
├── .github/           # GitHub config and copilot-instructions.md
├── builds/            # Build outputs (ZIP, MSI, logs, manifests)
├── Constants/         # Static constants
├── Converters/        # WPF converters
├── docs/              # ALL documentation markdown files
├── installer/         # WiX installer files
├── Models/            # Data models and DTOs
├── publish/           # Published output
├── research/          # Research notes (temporary)
├── scripts/           # ALL PowerShell scripts except Build-And-Distribute.ps1
├── Services/          # Business logic and API integrations
├── telemetry/         # Telemetry queries and config
├── ViewModels/        # MVVM view models
├── Views/             # XAML UI components
└── Build-And-Distribute.ps1  # ONLY build script (kept at root for convenience)
```

### Rules
1. **ONE build script**: `Build-And-Distribute.ps1` at root. Use `-PublishToGitHub` flag for releases.
2. **Docs go in `docs/`**: All markdown except README.md, CHANGELOG.md, CONTEXT.md
3. **Scripts go in `scripts/`**: All PowerShell except Build-And-Distribute.ps1
4. **No ZIP files in root**: Build outputs go in `builds/`
5. **Research is temporary**: `research/` contents can be deleted after features ship

---

## Command Shortcuts

When the user says these commands, respond with the corresponding action:

### `@build` - Build and Publish
```powershell
.\Build-And-Distribute.ps1 -PublishToGitHub -Force
```
- Increments version, builds ZIP, uploads to GitHub Releases
- Updates manifest.json and CHANGELOG.md

### `@handoff` - Session Handoff
Append to `docs/SESSION_LOG.md`:
```markdown
## [DATE] Session Summary
### Completed
- [List of completed work]

### Pending
- [List of incomplete work]

### Key Decisions
- [Any architectural decisions made]

### Next Steps
- [What the next session should do]
```

Update `CONTEXT.md` with current state.

### `@status` - Project Status
Read and display:
1. Current version from `ZeroTrustMigrationAddin.csproj`
2. Last 3 entries from CHANGELOG.md
3. Active work from CONTEXT.md
4. Any compilation errors

### `@cleanup` - Directory Cleanup
Move files to proper locations:
- Markdown files (except README, CHANGELOG, CONTEXT) → `docs/`
- PowerShell scripts (except Build-And-Distribute.ps1) → `scripts/`
- Delete any stray build artifacts from root

### `@whatsnew` - Recent Changes
Show CHANGELOG.md entries since last major version.

### `@cleanreleases` - Clean Old GitHub Releases
```powershell
# Preview what would be deleted (dry run)
.\scripts\Clean-OldGitHubReleases.ps1 -WhatIf

# Actually delete (default: keep 3 most recent)
.\scripts\Clean-OldGitHubReleases.ps1

# Keep more releases if needed
.\scripts\Clean-OldGitHubReleases.ps1 -KeepCount 5

# Delete releases but keep git tags
.\scripts\Clean-OldGitHubReleases.ps1 -KeepTags
```
- Deletes releases AND their git tags by default
- Requires typing "DELETE" to confirm
- Uses GitHub CLI (gh) - must be authenticated

---

## Context Preservation

### CONTEXT.md (Living State Document)
Updated at END of each session with:
- Current version
- Features in development
- Known issues / blockers
- Immediate next steps

### docs/SESSION_LOG.md (Append-Only History)
Appended with `@handoff` command:
- Date and summary of work done
- Decisions made and why
- What was left incomplete

### Reading Context at Session Start
New sessions should:
1. Read CONTEXT.md for current state
2. Check docs/SESSION_LOG.md for recent history
3. Check get_errors() for compilation issues
