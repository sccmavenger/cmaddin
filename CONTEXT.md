# Project Context

> **Living State Document** - Updated at end of each session. For historical context, see `docs/SESSION_LOG.md`.

---

## Current State

| Property | Value |
|----------|-------|
| **Version** | 3.17.230 |
| **Last Updated** | 2026-03-10 |
| **Branch** | main |
| **Status** | Stable - Docs fully updated to MSI install + Interactive Browser auth |

---

## Active Features

### Cloud Native Tab Refocus (v3.17.223)
- **Status**: Published
- **Changes**: Slimmed main Cloud Native tab from 20 cards to 4 high-impact cards + hero
  - Kept: Device Compliance, Security Blind Spots, Zero Trust Ready, Cloud Native Progress hero
  - Added: Enrollment Velocity card
  - Moved 15 cards to hidden Cloud Comparison Details tab
  - Deleted: OS Currency and "5 Questions" cards

### Documentation Overhaul (v3.17.224-226)
- **Status**: Published
- **Changes**:
  - All install instructions updated from ZIP/PS1 to MSI-first across 6 docs files
  - Build script release notes template updated to MSI-first
  - README Getting Started auth flow updated from Device Code to Interactive Browser
  - AdminUserGuide.html already had correct auth flow

### Telemetry Transparency & Privacy (v3.17.216-218)
- **Status**: Published

### Security Hardening (v3.17.193)
- **Status**: Published

### VP Dashboard & Workload Authority (v3.17.180)
- **Status**: Published

### Hidden Tabs System
All hidden tabs can be enabled via command-line:
- `/showtabs:comparison` - Cloud Value Comparison (4 high-impact cards)
- `/showtabs:cloudcomparisondetails` - Cloud Comparison Details (15 detailed cards)
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
