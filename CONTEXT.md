# Project Context

> **Living State Document** - Updated at end of each session. For historical context, see `docs/SESSION_LOG.md`.

---

## Current State

| Property | Value |
|----------|-------|
| **Version** | 3.17.114 |
| **Last Updated** | 2026-02-05 |
| **Branch** | main |
| **Status** | Stable - Published to GitHub |

---

## Active Features

### Cloud Value Comparison Tab (NEW)
- **Status**: Complete, hidden by default
- **Enable**: Launch with `/showtabs:comparison` argument
- **Location**: [CloudValueComparisonTab.xaml](Views/CloudValueComparisonTab.xaml)
- **Description**: 10 comparison cards showing Intune vs ConfigMgr capabilities using real customer data

### Hidden Tabs System
All hidden tabs can be enabled via command-line:
- `/showtabs:comparison` - Cloud Value Comparison (10 cards)
- `/showtabs:agent` - Enrollment Agent (chat interface)

---

## Recent Session Summary (2026-02-05)

### Completed
- **Alternate Credentials for ConfigMgr** - Users can now connect with different credentials than their Windows login
  - DPAPI-encrypted password storage
  - Supports DOMAIN\user and UPN formats
  - Test Connection button for validation
- Created CloudValueComparisonTab with 10 comparison cards
- Implemented real data integration from Graph API and ConfigMgr
- Published v3.17.110-114 with successive fixes
- Fixed auto-update manifest.json issue
- Deleted redundant scripts (Publish-ToGitHub.ps1, Build-And-Distribute-v1-backup.ps1)
- Updated copilot-instructions.md with command system and file organization rules
- Moved 22 markdown files to `docs/`, 8 scripts to `scripts/`

### Key Decisions
- **ADR-008**: Single build script policy - only `Build-And-Distribute.ps1` at root
- **ADR-009**: File organization - docs in `docs/`, scripts in `scripts/`
- **ADR-010**: Context preservation via CONTEXT.md + SESSION_LOG.md
- **ADR-011**: DPAPI for credential encryption (user-scoped, machine-bound)

---

## Known Issues

1. **None critical** - v3.17.114 is stable

---

## Immediate Next Steps

1. Build and publish v3.17.115 with alternate credentials feature
2. Test alternate credentials with customer environment

---

## Quick Reference

### Build Commands
```powershell
# Quick build
dotnet build

# Build and publish to GitHub
.\Build-And-Distribute.ps1 -PublishToGitHub -Force
```

### File Locations
| Purpose | Location |
|---------|----------|
| Application Logs | `%LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs\` |
| Build Output | `.\builds\` |
| Published Releases | GitHub Releases |

### Architecture
```
UI (Views) → ViewModels → Services → External APIs
                              ↓
                    FileLogger (singleton)
```

### Data Sources
- **Graph API**: Intune devices, compliance, encryption
- **ConfigMgr Admin Service**: SCCM devices, inventory
- **WMI**: Fallback for older ConfigMgr versions
