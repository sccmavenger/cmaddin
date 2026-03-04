# Project Context

> **Living State Document** - Updated at end of each session. For historical context, see `docs/SESSION_LOG.md`.

---

## Current State

| Property | Value |
|----------|-------|
| **Version** | 3.17.218 (pending 3.17.219) |
| **Last Updated** | 2026-03-02 |
| **Branch** | main |
| **Status** | Stable - Cloud Readiness tab reviewed, no code changes |

---

## Active Features

### Telemetry Transparency & Privacy (v3.17.216-218)
- **Status**: Complete, ready for publish
- **Changes**:
  - **Telemetry Opt-Out Toggle**: New toggle in Diagnostics window to enable/disable anonymous usage analytics
  - **First-Run Notice**: Telemetry notice popup on first launch with opt-out option
  - **Separate Telemetry Log**: All telemetry events logged to `TelemetryLog_*.log` (not main log)
  - **PII Removal**: Username, UPN, email, job title redacted from local logs (Microsoft Privacy compliance)
- **Files Modified**:
  - Views/DiagnosticsWindow.xaml/.cs (toggle UI)
  - Views/TelemetryNoticeWindow.xaml/.cs (NEW - first-run popup)
  - Models/TelemetrySettings.cs (NEW - settings model)
  - Services/AzureTelemetryService.cs (opt-out logic, log separation)
  - Services/FileLogger.cs (LogTelemetry method, PII redaction)

### UI Zoom / Accessibility (v3.17.216)
- **Status**: Complete
- **Changes**:
  - Zoom slider in title bar (80%-150% range)
  - Keyboard shortcuts: Ctrl+Plus, Ctrl+Minus, Ctrl+0
  - Persisted via ZoomLevel in settings
- **Files Modified**:
  - Views/MainWindow.xaml/.cs (ScaleTransform, slider UI)

### Preview Title Bar (v3.17.216)
- **Status**: Complete
- **Changes**: "Preview" prefix added to application title

### Security Hardening (v3.17.193)
- **Status**: Published
- **Changes**:
  - SSL certificate validation uses thumbprint-based trust
  - All API keys/tokens encrypted with Windows DPAPI
  - Centralized `SecureCredentialManager.cs`

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

## Recent Session Summary (2026-03-02)

### Completed
- **Cloud Readiness tab deep-dive review** — mapped architecture across View, code-behind, models, and service layers
- Documented 2 active signals (Autopilot Readiness, Cloud-Native Readiness) with data sources and assessment logic
- Identified 6 hidden signals disabled per stakeholder feedback
- No code changes — planning/review session

### Key Decisions
- Cloud-Native signal scopes to migration targets only (excludes born-in-cloud devices)
- Hybrid Entra ID Join is not a blocker (expected intermediate state)
- 6 signals hidden per Rob's feedback — may revisit later

---

## Known Issues

1. **None critical** - v3.17.116 is stable

---

## Immediate Next Steps

1. Decide on Cloud Readiness tab changes (re-enable signals? adjust logic? UI tweaks?)
2. Consider whether hidden signals (Win 11, Identity, WUfB, Endpoint Security, Autopatch, App Readiness) should be revisited or removed
3. Test alternate credentials feature with customer environment

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
