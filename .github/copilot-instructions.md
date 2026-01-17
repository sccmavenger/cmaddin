# GitHub Copilot Instructions for CloudJourneyAddin

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
- Run `Test-AutoUpdate.ps1` to verify update system
