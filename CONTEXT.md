# Project Context

This document provides current project state for developers and AI assistants. Updated automatically during builds and manually for significant changes.

---

## Current Version
**Version**: 3.17.75 (Unreleased: 3.16.31)  
**Last Updated**: 2026-01-28  
**Branch**: main

---

## Recent Changes (Last 5 Sessions)

### Session: 2025-01-17
**Focus**: Enrollment Impact Simulator - Credibility-first feature design
- ✅ Created 100% data-driven Enrollment Impact Simulator
- ✅ Added ConfigMgr security inventory methods (BitLocker, Firewall, Defender, TPM, OS)
- ✅ Added Graph API compliance policy extraction
- ✅ Created EnrollmentSimulatorCard (dashboard summary)
- ✅ Created EnrollmentSimulatorWindow (detailed results)

**Key Decision**: Rejected hardcoded estimates in favor of data-driven calculations. See ADR-007 in DECISIONS.md.

**Files Created**:
- Models/EnrollmentSimulatorModels.cs
- Services/EnrollmentSimulatorService.cs
- Views/EnrollmentSimulatorCard.xaml/.cs
- Views/EnrollmentSimulatorWindow.xaml/.cs

**Files Modified**:
- Services/ConfigMgrAdminService.cs - Security inventory methods
- Services/GraphDataService.cs - Compliance policy settings methods

### Session: 2025-01-15/16
**Focus**: Feature Development & Infrastructure
- ✅ Added Migration Impact Analysis (6 categories, 30+ metrics)
- ✅ Fixed Enrollment Confidence card buttons (View Full Analysis, Get Recommendations)
- ✅ Implemented query logging for transparency
- ✅ Consolidated logs to single location
- ✅ Fixed auto-update (uploaded missing manifest.json)
- ✅ Added documentation automation (copilot-instructions, commit template, DECISIONS.md)

**Files Modified**:
- Services/FileLogger.cs - Query logging
- Services/MigrationImpactService.cs - NEW
- Models/MigrationImpactModels.cs - NEW
- Views/MigrationImpactCard.xaml/.cs - NEW
- Views/ConfidenceDetailsWindow.xaml/.cs - NEW
- Views/RecommendationsWindow.xaml/.cs - NEW
- Views/DiagnosticsWindow.xaml/.cs - Query log viewer

---

## Active Development

### In Progress
- Enrollment Impact Simulator - Wire up to dashboard

### Planned Next
- Integrate EnrollmentSimulatorCard into dashboard
- Review Migration Impact Analysis for similar credibility issues
- Documentation automation testing

---

## Known Issues

1. **Migration Impact Analysis has hardcoded estimates** - May need revision per ADR-007 principles
2. **Empty CHANGELOG entries** - Versions 3.16.28-3.16.30 have placeholder entries that need filling
3. **Query logging overhead** - Not measured, likely negligible but should verify

---

## Architecture Quick Reference

```
┌─────────────────────────────────────────────────────────┐
│                    WPF UI Layer                         │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐       │
│  │ Dashboard   │ │ Enrollment  │ │ Migration   │       │
│  │ View        │ │ Cards       │ │ Impact      │       │
│  └─────────────┘ └─────────────┘ └─────────────┘       │
│  ┌─────────────────────────────────────────────┐       │
│  │ Enrollment Simulator Card (NEW)              │       │
│  └─────────────────────────────────────────────┘       │
├─────────────────────────────────────────────────────────┤
│                  ViewModel Layer                        │
│  ┌─────────────────────────────────────────────┐       │
│  │ DashboardViewModel (Main orchestration)      │       │
│  └─────────────────────────────────────────────┘       │
├─────────────────────────────────────────────────────────┤
│                   Services Layer                        │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐    │
│  │ GraphData    │ │ ConfigMgr    │ │ Enrollment   │    │
│  │ Service      │ │ AdminService │ │ Simulator    │    │
│  └──────────────┘ └──────────────┘ └──────────────┘    │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐    │
│  │ FileLogger   │ │ UpdateService│ │ Migration    │    │
│  │ (Singleton)  │ │              │ │ Impact       │    │
│  └──────────────┘ └──────────────┘ └──────────────┘    │
├─────────────────────────────────────────────────────────┤
│                  External APIs                          │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐    │
│  │ Microsoft    │ │ ConfigMgr    │ │ WMI          │    │
│  │ Graph API    │ │ Admin REST   │ │ (Fallback)   │    │
│  └──────────────┘ └──────────────┘ └──────────────┘    │
└─────────────────────────────────────────────────────────┘
```

---

## File Locations

| Purpose | Location |
|---------|----------|
| Application Logs | `%LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs\` |
| Query Log | `%LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs\QueryLog.txt` |
| Update Log | `%LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs\Update.log` |
| Cached Data | `%LOCALAPPDATA%\ZeroTrustMigrationAddin\Cache\` |
| Build Output | `.\builds\` |
| Published Releases | GitHub Releases |

---

## Build Commands

```powershell
# Quick build (debug)
dotnet build

# Release build
dotnet build -c Release

# Build and publish to GitHub
.\Build-And-Distribute.ps1 -PublishToGitHub

# Test auto-update system
.\Test-AutoUpdate.ps1
```

---

## Key Configuration

- **Target Framework**: net8.0-windows7.0
- **Graph API Scopes**: DeviceManagementManagedDevices.Read.All, etc.
- **ConfigMgr**: Requires Admin Service or WMI access
- **Updates**: GitHub repo `sccmavenger/cmaddin`

---

## Team Notes

*Add notes for team members or future sessions here*

- Remember: GitHub releases need BOTH zip AND manifest.json
- Mock data is shown when disconnected - useful for demos
- Query log viewer is in Diagnostics window
