# Project Context

> **Living State Document** - Updated at end of each session. For historical context, see `docs/SESSION_LOG.md`.

---

## Current State

| Property | Value |
|----------|-------|
| **Version** | 3.17.244 |
| **Last Updated** | 2026-03-11 |
| **Branch** | main |
| **Status** | Stable - Ideas tab replacing Workload Brainstorm with data-driven Decision Cards |

---

## Active Features

### 💡 Ideas Tab (v3.17.239)
- **Status**: Built, compiles clean
- **What**: Replaced old "Workload Brainstorm" tab (~2000 lines of static mockups) with 5 data-driven features
- **Features**:
  - Decision Cards — per-workload 4-question cards (What decision? Why now? Cost of inaction? Next step?)
  - Workload Unlock Chain — shows downstream workloads unlocked by completing each one
  - ConfigMgr Coverage — Intune vs ConfigMgr device split visualization
  - Safe to Remove Confidence — safety scores with "what stops running" details + rollback estimates
  - Last Holdout Spotlight — special card when 5+ of 7 workloads are complete
- **Files**: `Models/DecisionCardModels.cs`, `Services/DecisionCardGenerator.cs`, updated `DashboardViewModel.cs`, `DashboardWindow.xaml`, `TabVisibilityOptions.cs`, `ValueConverters.cs`
- **Tab visibility**: Visible by default, controllable via `/hidetabs:ideas` and `/showtabs:ideas`

### Analysis Pipeline Framework (v3.17.233+)
- **Status**: Framework built, compiles clean, not yet wired to UI
- **What**: Three-layer pipeline (Signal → Analyzer → Recommendation) for detecting migration stalls
- **Architecture doc**: `docs/ANALYSIS_PIPELINE_ARCHITECTURE.md`
- **Decisions**: ADR-011 and ADR-012 in `docs/DECISIONS.md`
- **Chains implemented**:
  - Enrollment Stall: Trust Trough detection, root-cause classification, Trust Reset Batch sizing
  - Workload Stall: Per-workload stall, Workload Trust Trough, Client Apps holdout, last holdout
- **Pending**:
  - UI integration (surface results in Enrollment Momentum, Workloads tab, Recommendations window)
  - Background scheduling activation
  - Real historical snapshot persistence (currently synthetic)
- **Files**: 10 new files in `Services/Pipeline/`, `Models/PipelineModels.cs`, `Services/ServiceRegistration.cs`

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

## Recent Session Summary (2026-03-11)

### Completed
- **Analysis Pipeline Framework** — designed and implemented full Signal → Analyzer → Recommendation engine
- Created 10 new files: core interfaces, base classes, orchestrator, 2 complete analyzer chains, DI registration
- Activated `Microsoft.Extensions.DependencyInjection` for pipeline services
- Build verified clean (zero new errors/warnings)
- Comprehensive documentation: `docs/ANALYSIS_PIPELINE_ARCHITECTURE.md`, ADR-011, ADR-012

### Key Decisions
- Three-layer pipeline (not monolithic service or event bus) for separation of concerns and extensibility
- Typed generics for compile-time chain safety
- Singletons for pipeline services (caching requires single instances)
- DI additive — existing singleton/direct-construction patterns unchanged
- Pipeline and AI recommendations coexist (precision + insight)

---

## Known Issues

1. **Pipeline UI not wired** — results are produced but not visible in any view yet
2. **Historical snapshots synthetic** — `EnrollmentAnalyticsService` generates synthetic history, not real stored data
3. **Background schedule not activated** — `StartBackgroundSchedule()` is implemented but not called

---

## Immediate Next Steps

1. **UI Integration** — surface pipeline results in existing views:
   - Enrollment Momentum View: add root-cause classification badge, Trust Trough warning
   - Workloads Tab: add per-workload velocity and stall alerts
   - Recommendations Window: add pipeline action cards with blast radius and cost of inaction
2. **Background scheduling** — activate `StartBackgroundSchedule()` after initial data load
3. **Historical snapshot persistence** — store real enrollment snapshots daily for accurate velocity/stall detection
4. **Testing** — unit tests for analyzers (deterministic signal → expected assessment), integration test with MockDataService
5. **Additional analyzers** (future) — compliance drift, cost modeling, security posture, agent removal readiness

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
                              ↓
            ┌── Analysis Pipeline (DI) ──┐
            │ Signal → Analyzer → Recs   │
            └────────────────────────────┘
```

### Data Sources
- **Graph API**: Intune devices, compliance, encryption
- **ConfigMgr Admin Service**: SCCM devices, inventory
- **WMI**: Fallback for older ConfigMgr versions
