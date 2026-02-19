# Project Context

> **Living State Document** - Updated at end of each session. For historical context, see `docs/SESSION_LOG.md`.

---

## Current State

| Property | Value |
|----------|-------|
| **Version** | 3.17.215 |
| **Last Updated** | 2026-02-16 |
| **Branch** | main |
| **Status** | Stable - Security hardening complete |

---

## Active Features

### Security Hardening (v3.17.193)
- **Status**: Complete, ready for publish
- **Changes**:
  - **CRITICAL FIX**: SSL certificate validation now uses thumbprint-based trust (was bypassing ALL validation)
  - All API keys and tokens now encrypted with Windows DPAPI
  - Centralized `SecureCredentialManager.cs` for consistent credential handling
  - Automatic migration from plaintext to encrypted storage on first load
- **Files Modified**:
  - Services/SecureCredentialManager.cs (NEW)
  - Services/ConfigMgrAdminService.cs (SSL validation + cert trust)
  - Services/AzureOpenAIService.cs (API key encryption)
  - Services/FeedbackService.cs (OAuth token encryption)
  - Services/GitHubUpdateService.cs (PAT encryption)
  - Models/UpdateManifest.cs (UpdateSettings encryption)

### Comparison Tile UX Improvements (Unreleased)
- **Status**: Built, pending publish
- **Changes**:
  - Simplified "data not available" messages (less technical jargon)
  - Added ConfigMgrDataSuspect check for contradictory data (0 days avg with 0% scanned)
  - Response Time and Security Blind Spots tiles now show cleaner messages

### VP Dashboard & Workload Authority (v3.17.180)
- **Status**: Published
- **Features**:
  - 4 new headline tiles: Co-Managed Opportunity, Identity Ready, Hardware Ready, Don't Waste Time
  - WorkloadAuthoritySnapshot telemetry event
  - Workload Authority Analysis section in Azure Workbook

### Alternate Credentials for ConfigMgr (v3.17.115)
- **Status**: Complete, production-ready
- **Description**: Connect to ConfigMgr Admin Service with different credentials than Windows login
- **Security**: Passwords encrypted with Windows DPAPI (user-scoped, machine-bound)
- **Formats**: Supports `DOMAIN\username` and `user@domain.com` (UPN)

### Cloud Value Comparison Tab
- **Status**: Complete, hidden by default
- **Enable**: Launch with `/showtabs:comparison` argument
- **Location**: [CloudValueComparisonTab.xaml](Views/CloudValueComparisonTab.xaml)
- **Description**: 11 comparison cards showing Intune vs ConfigMgr capabilities using real customer data
- **Documentation**: [docs/COMPARISON_METHODOLOGY.md](docs/COMPARISON_METHODOLOGY.md)

### Hidden Tabs System
All hidden tabs can be enabled via command-line:
- `/showtabs:comparison` - Cloud Value Comparison (11 cards)
- `/showtabs:agent` - Enrollment Agent (chat interface)

---

## Recent Session Summary (2026-02-05)

### Completed
- **Alternate Credentials for ConfigMgr** - Users can now connect with different credentials than their Windows login
  - DPAPI-encrypted password storage
  - Supports DOMAIN\user and UPN formats
  - Test Connection button for validation
- **Comparison Methodology Documentation** - Complete documentation for admins explaining the math/logic behind each comparison card
  - Created `docs/COMPARISON_METHODOLOGY.md` for developers
  - Added "Comparison Methodology" section to `AdminUserGuide.html` for admins
- Created CloudValueComparisonTab with 10 comparison cards
- Implemented real data integration from Graph API and ConfigMgr
- Published v3.17.110-116 with successive fixes
- Fixed auto-update manifest.json issue
- Fixed ConfigMgrServerDialog not resizing when credentials panel expanded
- Deleted redundant scripts (Publish-ToGitHub.ps1, Build-And-Distribute-v1-backup.ps1)
- Updated copilot-instructions.md with command system and file organization rules
- Moved 22 markdown files to `docs/`, 8 scripts to `scripts/`
- Updated INTERNAL_HIDDEN_FEATURES.md with comparison tab info

### Key Decisions
- **ADR-008**: Single build script policy - only `Build-And-Distribute.ps1` at root
- **ADR-009**: File organization - docs in `docs/`, scripts in `scripts/`
- **ADR-010**: Context preservation via CONTEXT.md + SESSION_LOG.md
- **ADR-011**: DPAPI for credential encryption (user-scoped, machine-bound)
- **ADR-012**: Comparison methodology documented in both markdown and HTML for different audiences

---

## Known Issues

1. **None critical** - v3.17.116 is stable

---

## Immediate Next Steps

1. Test alternate credentials feature with customer environment
2. Consider adding Build-And-Distribute.ps1 validation for documentation files

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
