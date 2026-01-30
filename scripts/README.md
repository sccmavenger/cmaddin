# Helper Scripts for Cloud Native Readiness Tool

This folder contains helper scripts for testers and administrators.

## Scripts

| Script | Description |
|--------|-------------|
| `Reset-AutoUpdate.ps1` | Clears cached manifests and temp files when auto-update gets stuck or fails |
| `Get-Diagnostics.ps1` | Shows version info, log locations, and recent log entries for troubleshooting |

## Common Scenarios

### Auto-update stuck or failed mid-update
```powershell
.\Reset-AutoUpdate.ps1
```
Then restart the application.

### Check what version is installed and recent activity
```powershell
.\Get-Diagnostics.ps1
```

### Force an update check
1. Run `Reset-AutoUpdate.ps1` to clear the cached manifest
2. Launch the application - it will check GitHub for the latest version

## File Locations

| Item | Path |
|------|------|
| App Data | `%LOCALAPPDATA%\ZeroTrustMigrationAddin\` |
| Logs | `%LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs\` |
| Cached Manifest | `%LOCALAPPDATA%\ZeroTrustMigrationAddin\manifest.json` |
| Update Temp Files | `%TEMP%\CloudJourneyAddin-Update\` |

## Log Files

- **CloudNativeReadiness_YYYYMMDD.log** - Main application log (daily rotation)
- **Update.log** - Auto-update activity log
