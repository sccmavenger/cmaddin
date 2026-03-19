 do# Cloud Native Assessment - Product Requirements Document

Last updated: 2026-03-18

---

## 1. Executive Summary

Cloud Native Assessment is a WPF add-in for Microsoft Configuration Manager (ConfigMgr/SCCM) that helps IT administrators plan, monitor, and execute migrations from ConfigMgr to Microsoft Intune cloud-native device management.

The product connects to both Microsoft Graph (Intune) and ConfigMgr Admin Service to build a unified, real-time picture of an organization's migration posture — enrollment progress, workload authority transitions, device identity states, compliance gaps, and security exposure. It transforms raw data from two management platforms into actionable decision intelligence that drives migration velocity.

**MVP Goal**: Provide ConfigMgr administrators with a single dashboard that answers: *Where are we? What should we do next? What's blocking us? What's the cost of waiting?*

---

## 2. Mission

**Mission Statement**: Accelerate enterprise migration from ConfigMgr to Intune by making migration state visible, migration decisions data-driven, and migration stalls detectable.

**Core Principles**:
1. **Data over opinions** — Every metric, recommendation, and alert is derived from real environment data or clearly labeled as an industry benchmark estimate.
2. **Mock-first development** — Every feature works with demonstration data before requiring authentication, enabling evaluation without risk.
3. **Influence by design** — The UI is deliberately ordered to create urgency (security fear → cost savings → hygiene cleanup) rather than passively displaying diagnostics.
4. **Dual-source truth** — ConfigMgr and Intune data are cross-referenced to identify gaps neither system sees alone (orphaned devices, ghost records, enrollment blockers).
5. **Progressive autonomy** — From manual analysis to supervised AI agent to autonomous enrollment orchestration across three operational phases.

---

## 3. Target Users

### Primary: ConfigMgr Administrator
- **Role**: IT admin managing 10,000–200,000+ devices via ConfigMgr
- **Technical comfort**: Expert in ConfigMgr/SCCM, learning Intune
- **Pain points**: No unified view of migration progress, can't quantify security risk of staying on ConfigMgr, no data to justify migration timeline to leadership
- **Goal**: Migrate workloads to Intune while maintaining management coverage

### Secondary: IT Director / Cloud Strategy Lead
- **Role**: Decision-maker authorizing migration phases
- **Technical comfort**: Understands concepts, not daily operations
- **Pain points**: Needs executive summary, cost justification, and risk quantification
- **Goal**: Approve migration waves with confidence, report progress to leadership

---

## 4. MVP Scope

### In Scope: Core Functionality
- ✅ Device enrollment dashboard (total, Intune-enrolled, ConfigMgr-only, co-managed, cloud-native)
- ✅ Device identity state breakdown (Hybrid Entra joined, Entra joined, AD-only, Workgroup)
- ✅ Workload authority tracking across all 7 co-management workloads
- ✅ Compliance score comparison (Intune vs ConfigMgr)
- ✅ Migration plan timeline visualization
- ✅ Alerts & recommendations engine (enrollment velocity, stall detection, Hybrid Join growth trends)
- ✅ Progress targets with actionable next steps
- ✅ Cost savings estimator

### In Scope: Deep Analysis Features
- ✅ Security Exposure Gap — facts-only Conditional Access headline, compliance comparison, per-workload risk
- ✅ ConfigMgr Client Uninstall Readiness — bottleneck math across workload adoption
- ✅ Stale Device & Orphan Detection — ranged estimates with methodology labels and data confidence

### In Scope: Decision Intelligence
- ✅ Decision Cards — per-workload decision framing (What? Why now? Cost of inaction? Next step?)
- ✅ Workload Unlock Chain — downstream dependency visualization
- ✅ Safe to Remove Confidence — safety scores with rollback estimates
- ✅ Last Holdout Spotlight — special focus when 6/7 workloads complete

### In Scope: Technical
- ✅ Microsoft Graph API integration (Intune managed devices, compliance, apps, enrollment config)
- ✅ ConfigMgr Admin Service REST integration with WMI fallback
- ✅ Interactive Browser + Device Code authentication flows
- ✅ Auto-update system via GitHub Releases (manifest.json + ZIP)
- ✅ MSI installer for enterprise deployment
- ✅ DPAPI-encrypted credential storage
- ✅ Application Insights telemetry (anonymous)
- ✅ FileLogger diagnostic logging

### In Scope: AI Features
- ✅ Azure OpenAI-powered recommendations
- ✅ Enrollment ReAct Agent (Phase 1: supervised, human-approved actions)

### Out of Scope (Future)
- ❌ ReAct Agent Phase 2/3 (conditional and full autonomy)
- ❌ Analysis Pipeline UI integration (framework built, not yet wired)
- ❌ Real historical snapshot persistence for trend analysis
- ❌ Multi-tenant support
- ❌ Role-based access control within the add-in
- ❌ Direct Intune enrollment execution from the UI
- ❌ Reminder/notification system outside the dashboard
- ❌ Mobile or web companion app

---

## 5. User Stories

1. **As a ConfigMgr admin**, I want to see all my devices categorized by enrollment state (ConfigMgr-only, co-managed, cloud-native), so that I know exactly how far my migration has progressed.

2. **As a ConfigMgr admin**, I want to see which of the 7 co-management workloads have been transitioned to Intune authority and their adoption percentages, so that I can prioritize the next workload to move.

3. **As a ConfigMgr admin**, I want alerts when enrollment velocity drops or stalls, so that I can investigate and resume migration momentum before timelines slip.

4. **As a ConfigMgr admin**, I want to see which devices can safely have the ConfigMgr client uninstalled today (based on all workloads being Intune-managed), so that I can reduce dual-agent overhead.

5. **As an IT director**, I want a security exposure summary showing how many devices lack Conditional Access enforcement, so that I can quantify the risk of delayed migration to leadership.

6. **As a ConfigMgr admin**, I want to see stale, orphaned, and ghost device estimates with clear methodology labels, so that I can clean up my inventory before planning infrastructure retirement.

7. **As a ConfigMgr admin**, I want a warning when my fleet is growing with Hybrid Entra Joined devices, so that I can redirect new device provisioning to Entra-only (cloud-native) and avoid compounding future migration debt.

8. **As an IT director**, I want per-workload decision cards that frame the specific decision, the urgency, and the cost of inaction, so that I can make data-driven go/no-go decisions in change advisory meetings.

9. **As a ConfigMgr admin**, I want the tool to work with realistic demonstration data without requiring authentication, so that I can evaluate the tool's capabilities and present it to stakeholders without connecting to production systems.

10. **As a ConfigMgr admin**, I want the tool to auto-update from GitHub Releases on launch, so that I always have the latest features and fixes without manual downloads.

11. **As a ConfigMgr admin**, I want enrollment momentum analytics showing velocity trends and confidence scoring, so that I can forecast migration completion dates and identify slowdowns early.

12. **As a ConfigMgr admin**, I want an enrollment simulator that models what-if scenarios (e.g., "what if we enroll all Windows 11 devices next?"), so that I can estimate impact before committing to a migration wave.

---

## 6. Core Architecture & Patterns

### High-Level Architecture

```
┌───────────────────────────────────────────────────┐
│                   WPF Dashboard                    │
│  (DashboardWindow.xaml + DashboardViewModel.cs)    │
├───────────────┬───────────────┬────────────────────┤
│   Overview    │  Enrollment   │  Decision Cards    │
│   Workloads   │  Cloud Native │  Applications      │
│   Readiness   │  AI Actions   │  Cloud Comparison  │
├───────────────┴───────────────┴────────────────────┤
│              ViewModels (MVVM)                      │
├────────────────────────────────────────────────────┤
│              Services Layer                         │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────┐│
│  │GraphDataSvc  │  │ConfigMgrAdmin│  │MockDataSvc││
│  │(Intune/Graph)│  │(REST + WMI)  │  │(Demo mode) ││
│  └──────┬───────┘  └──────┬───────┘  └─────┬─────┘│
│         │                 │                 │      │
│  ┌──────┴─────────────────┴─────────────────┴────┐ │
│  │   DecisionCardGenerator, AnalyticsSvc,        │ │
│  │   MomentumSvc, ImpactSvc, ReadinessSvc        │ │
│  └───────────────────────────────────────────────┘ │
├────────────────────────────────────────────────────┤
│  Pipeline: Signal → Analyzer → Recommendation      │
├────────────────────────────────────────────────────┤
│  AI: AzureOpenAIService + EnrollmentReActAgent     │
└────────────────────────────────────────────────────┘
```

### Directory Structure

```
root/
├── Models/              # Data models and DTOs (15 files)
├── ViewModels/          # MVVM ViewModels (7 files)
├── Views/               # XAML UI + code-behind (30+ files)
├── Services/            # Business logic, API integrations (25+ files)
│   ├── Pipeline/        # Signal → Analyzer → Recommendation framework
│   └── AgentTools/      # ReAct agent tool implementations
├── Constants/           # Terminology mappings
├── Converters/          # WPF value converters
├── Styles/              # Fluent Design color palette
├── installer/           # WiX MSI installer
├── scripts/             # PowerShell utilities
├── docs/                # Documentation files
└── Build-And-Distribute.ps1  # Build + GitHub Release script
```

### Key Design Patterns

- **MVVM**: ViewModelBase → DashboardViewModel → DashboardWindow.xaml with data binding
- **Dual-source data**: GraphDataService (Intune) + ConfigMgrAdminService (ConfigMgr) cross-referenced in ViewModels
- **Mock-first**: MockDataService provides complete demo data for every feature before authentication
- **Singleton services**: FileLogger, AzureTelemetryService persist across lifetime
- **DI container**: Microsoft.Extensions.DependencyInjection via ServiceRegistration
- **Tab visibility system**: Runtime `/showtabs:`/`/hidetabs:` switches for feature flagging
- **Facts-only analysis**: Deep Analysis features use only real data or clearly labeled industry benchmarks — no fabricated metrics

---

## 7. Features

### 7.1 Overview Tab
- Device enrollment summary with drill-through to device lists
- Device identity state breakdown (Hybrid Entra, Entra-only, AD-only, Workgroup)
- Migration plan timeline
- Alerts & Recommendations panel with severity-based rendering
- Progress targets with actionable next steps
- Cost savings estimator

### 7.2 Enrollment Tab
- Smart Enrollment Management with device readiness scoring
- Enrollment Momentum & Analytics (velocity tracking, trend charts)
- Enrollment Confidence scoring
- Enrollment Simulator for what-if scenarios

### 7.3 Workloads Tab
- Per-workload authority status across all 7 co-management workloads
- Workload velocity tracking with stall detection
- Pipeline stall indicators (red dot on tab when stall detected)

### 7.4 Decision Cards Tab
- **Decision Cards**: Per-workload 4-question cards (What decision? Why now? Cost of inaction? Next step?)
- **Deep Analysis** (influence-ordered):
  1. **Security Exposure Gap**: Conditional Access headline, compliance comparison, per-workload risk scaled by actual adoption gap
  2. **ConfigMgr Client Uninstall Readiness**: Bottleneck math — green/yellow/red tiers based on min workload adoption
  3. **Stale Device & Orphan Detection**: Ranges with methodology labels, infrastructure decommission framing, data confidence indicator
- **Workload Unlock Chain**: Dependency visualization
- **Safe to Remove Confidence**: Per-workload safety scores
- **Last Holdout Spotlight**: Special card when 6/7 workloads complete

### 7.5 Cloud Readiness Tab
- Device compliance scorecard
- Security blind spots analysis
- Zero Trust readiness assessment

### 7.6 Cloud Native Tab
- Cloud Native Progress hero card
- Enrollment velocity card
- High-impact security and compliance cards

### 7.7 Alert System
- **Co-Management Status**: Critical alert when not enabled, Info when expansion opportunity
- **Enrollment Velocity**: Warning when declining >50%, Info when accelerating
- **Critical Blockers**: Critical alert when devices blocked by enrollment errors
- **Migration Stalled**: Warning when no enrollments in 14+ days
- **Hybrid Join Growth**: Warning when >50% of fleet is Hybrid Entra Joined — warns about increasing Entra-only complexity (GPO conversion, device cleanup, AD dependency)

### 7.8 AI Actions Tab (Hidden by default)
- Azure OpenAI-powered migration recommendations
- ReAct Agent execution with reasoning trace visualization

---

## 8. Technology Stack

### Core
| Component | Technology | Version |
|-----------|-----------|---------|
| Runtime | .NET | 8.0 |
| UI Framework | WPF | .NET 8 Windows |
| Language | C# | 12 |
| Pattern | MVVM | ViewModelBase custom |
| DI | Microsoft.Extensions.DependencyInjection | 8.0.0 |

### API & Data
| Component | Technology | Version |
|-----------|-----------|---------|
| Graph SDK | Microsoft.Graph | 5.36.0 |
| Auth | Azure.Identity | 1.17.1 |
| ConfigMgr | Admin Service REST API | N/A (server-side) |
| Serialization | Newtonsoft.Json | 13.0.3 |
| WMI | System.Management | 8.0.0 |

### AI & Analytics
| Component | Technology | Version |
|-----------|-----------|---------|
| AI | Azure.AI.OpenAI | 1.0.0-beta.17 |
| Telemetry | Microsoft.ApplicationInsights | 2.22.0 |
| Charts | LiveCharts.Wpf | 0.9.7 |

### Distribution
| Component | Technology | Version |
|-----------|-----------|---------|
| GitHub API | Octokit | 13.0.1 |
| Installer | WiX Toolset | 6.0 |
| Build | PowerShell | Build-And-Distribute.ps1 |

---

## 9. Security & Configuration

### Authentication
- **Primary**: Interactive Browser via `InteractiveBrowserCredential` (opens system browser for login)
- **Fallback**: Device Code flow via `DeviceCodeCredential` (for restricted environments)
- **Configurable**: Auth method, tenant ID, client ID stored in user settings

### Microsoft Graph Permissions (Read-Only)
| Scope | Purpose |
|-------|---------|
| DeviceManagementManagedDevices.Read.All | Intune device inventory, compliance |
| DeviceManagementConfiguration.Read.All | Compliance policies, device configurations |
| DeviceManagementApps.Read.All | Mobile apps, app protection policies |
| DeviceManagementServiceConfig.Read.All | Autopilot, enrollment config |
| Directory.Read.All | Azure AD device objects |
| Group.Read.All | Policy assignment groups |
| User.Read | User profile for sign-in |

### ConfigMgr Admin Service
- Current-user Windows credentials (default) or alternate credentials
- Optional SHA256 thumbprint pinning for self-signed SSL certificates
- WMI fallback when Admin Service is unavailable

### Credential Storage
- All secrets (GitHub token, ConfigMgr alternate password, Azure OpenAI key) encrypted via Windows DPAPI
- Stored in `%LOCALAPPDATA%\ZeroTrustMigrationAddin\`
- No credentials transmitted to external services beyond intended API endpoints

### Security Scope
- **In scope**: Read-only Graph access, encrypted credential storage, SSL certificate validation, input sanitization for ConfigMgr queries
- **Out of scope**: OAuth token refresh management (delegated to Azure.Identity), network-level security, endpoint protection

---

## 10. Success Criteria

### MVP Success Definition
The product succeeds when a ConfigMgr admin can install the add-in, authenticate to their tenant, and immediately see a complete picture of their migration posture — including device enrollment state, workload authority gaps, security exposure, and prioritized next actions. Mock data renders in under 3 seconds; live data load time scales with fleet size and Graph API pagination.

### Functional Requirements
- ✅ Dashboard renders with mock data in <3 seconds when disconnected
- ✅ Authentication succeeds with Interactive Browser flow against production tenants
- ✅ Device enrollment counts reflect Intune/ConfigMgr state within a 5-minute cache window (Graph and ConfigMgr Admin Service both use 5-minute device cache TTL)
- ✅ All 7 co-management workloads display with correct authority status
- ✅ Alerts fire accurately for stall, velocity, and Hybrid Join growth conditions
- ✅ Deep Analysis features show only real data or clearly labeled estimates
- ✅ Auto-update detects new versions and applies without manual intervention
- ✅ MSI installer deploys into ConfigMgr console extension directory

### Quality Indicators
- ✅ Build succeeds with 0 errors on `dotnet build`
- ✅ All features functional in mock/disconnected mode
- ✅ No fabricated metrics — only real data or labeled industry benchmarks
- ✅ Per-workload gap points scaled by actual adoption percentages

### User Experience Goals
- One-click daily check: admin opens dashboard, scans Overview, knows migration state
- Decision Cards frame workload transitions as specific business decisions, not technical tasks
- Security Exposure creates urgency through factual Conditional Access gaps
- Alert panel surfaces actionable issues without requiring tab navigation

---

## 11. Implementation Phases

### Phase 1: Foundation (v2.4.0–v3.16.33) ✅ Complete
**Goal**: Core dashboard with device enrollment, workload tracking, compliance, auto-update
- ✅ Device enrollment dashboard with Graph API integration
- ✅ ConfigMgr Admin Service integration with WMI fallback
- ✅ Workload authority tracking (7 co-management workloads)
- ✅ Compliance score comparison
- ✅ Migration plan timeline
- ✅ Enrollment Analytics: Momentum, Confidence Score, Playbooks (v3.16.14–v3.16.26)
- ✅ Enrollment Impact Simulator (v3.16.27–v3.16.33)
- ✅ Migration Impact Analysis (v3.16.26)
- ✅ Auto-update system via GitHub Releases (v3.15.0)
- ✅ MSI installer via WiX 6.0
- ✅ Enrollment ReAct Agent (supervised, v3.14.x)

### Phase 2: Cloud Readiness & Native (v3.17.0–v3.17.230) ✅ Complete
**Goal**: Cloud posture assessment, security blind spots, executive summaries, telemetry
- ✅ Cloud Readiness Signals tab (v3.17.0)
- ✅ Cloud Native Progress tab with enrollment velocity cards
- ✅ Security Blind Spots analysis
- ✅ Executive Headlines for leadership reporting
- ✅ Application Insights telemetry integration
- ✅ Per-device workload authority from Graph API
- ✅ Contextual help links on dashboard tiles
- ✅ Connection status warning banner

### Phase 3: Intelligence (v3.17.231–v3.17.244) ✅ Complete
**Goal**: Data-driven decision features, analysis pipeline framework
- ✅ Signal → Analyzer → Recommendation pipeline framework
- ✅ Decision Cards with per-workload 4-question framing
- ✅ Deep Analysis: Security Exposure, Uninstall Readiness, Stale/Orphan Detection
- ✅ Workload Unlock Chain, Safe to Remove Confidence, Last Holdout Spotlight
- ✅ Azure OpenAI integration for recommendations

### Phase 4: Precision & Influence (v3.17.245–v3.17.247) ✅ Complete
**Goal**: Remove fabricated data, add influence design, ensure precision
- ✅ Facts-only Security Exposure (CA headline, scaled gap points)
- ✅ Bottleneck-math Uninstall Readiness
- ✅ Ranged Stale/Orphan with methodology labels and data confidence
- ✅ Influence-ordered XAML (Security → Uninstall → Stale)
- ✅ Hybrid Join Growth trend alert
- ✅ Realistic mock workload data with per-workload adoption percentages

### Phase 5: Pipeline Integration & Autonomy (Next)
**Goal**: Wire analysis pipeline to UI, advance agent autonomy
- ❌ Surface pipeline stall detection in Enrollment Momentum and Workloads tabs
- ❌ Background scheduling for continuous pipeline analysis
- ❌ Historical snapshot persistence for real trend data
- ❌ ReAct Agent Phase 2 (conditional autonomy for low-risk actions)
- ❌ Application readiness tab activation
- ❌ CHANGELOG.md population for v3.17.243–247

**Validation**: Pipeline stall indicators visible in tabs, agent can auto-approve device enrollments matching readiness criteria

---

## 12. Future Considerations

- **Agent Phase 3 (Full Autonomy)**: Continuous enrollment orchestration with monitoring and rollback
- **Executive Report Export**: One-click PDF/PowerPoint generation for leadership briefings
- **Multi-tenant support**: Switch between tenants without reconfiguration
- **Trend persistence**: Store historical snapshots for actual enrollment velocity trending over months
- **Webhook integration**: Push alerts to Teams/Slack channels
- **ConfigMgr infrastructure retirement planning**: Correlate device migration with server decommission

---

## 13. Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| **Graph API rate limiting** | Data refresh failures under heavy load | 5-minute device cache, 30-minute license cache, exponential backoff on 429 responses |
| **ConfigMgr Admin Service unavailability** | Incomplete enrollment picture | WMI fallback for device enumeration, graceful degradation with data source warnings |
| **Mock data divergence** | Demo mode misrepresents real behavior | Mock data values modeled on realistic enterprise fleet (106K devices, 7 workloads with varied adoption) |
| **Fabricated metrics eroding trust** | Admins distrust tool accuracy | Facts-only principle: all metrics from real data or clearly labeled industry benchmarks with methodology |
| **Stale CHANGELOG/CONTEXT** | Lost institutional knowledge across sessions | Build script auto-creates version entries; session handoff protocol updates CONTEXT.md |

---

## 14. Appendix

### Key Dependencies
- [Microsoft Graph SDK](https://github.com/microsoftgraph/msgraph-sdk-dotnet) v5.36.0
- [Azure Identity](https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/identity/Azure.Identity) v1.17.1
- [WiX Toolset](https://wixtoolset.org/) v6.0 for MSI packaging
- [LiveCharts](https://lvcharts.net/) v0.9.7 for WPF charting

### ConfigMgr & Intune Reference Documentation
- [Co-management overview](https://learn.microsoft.com/mem/configmgr/comanage/overview) — What co-management is, how workloads transition
- [Co-management workloads](https://learn.microsoft.com/mem/configmgr/comanage/workloads) — All 7 workloads and what each controls
- [How to switch workloads](https://learn.microsoft.com/mem/configmgr/comanage/how-to-switch-workloads) — Pilot vs production slider mechanics
- [How to monitor co-management](https://learn.microsoft.com/mem/configmgr/comanage/how-to-monitor) — Built-in ConfigMgr monitoring for co-management
- [ConfigMgr Admin Service REST API](https://learn.microsoft.com/mem/configmgr/develop/adminservice/overview) — The REST API this tool uses for ConfigMgr data
- [Admin Service usage guide](https://learn.microsoft.com/mem/configmgr/develop/adminservice/usage) — Authentication, querying, and endpoint reference
- [Microsoft Graph: Intune device management](https://learn.microsoft.com/graph/api/resources/intune-devices-manageddevice) — ManagedDevice resource used for enrollment data
- [Microsoft Graph: Device compliance](https://learn.microsoft.com/graph/api/resources/intune-deviceconfig-devicecompliancepolicy) — Compliance policy resources
- [Entra hybrid join vs Entra join](https://learn.microsoft.com/entra/identity/devices/hybrid-join-plan) — Device identity states this tool categorizes
- [Plan cloud-native Windows endpoints](https://learn.microsoft.com/mem/solutions/cloud-native-windows-endpoints/cloud-native-windows-endpoints) — The end-state this tool drives toward
- [Conditional Access overview](https://learn.microsoft.com/entra/identity/conditional-access/overview) — The security control referenced in Security Exposure analysis
- [Autopilot overview](https://learn.microsoft.com/autopilot/windows-autopilot) — Cloud-native provisioning referenced in Hybrid Join Growth alerts

### Security Posture Reference Documentation
- [Conditional Access: require compliant devices](https://learn.microsoft.com/entra/identity/conditional-access/howto-conditional-access-policy-compliant-device) — The specific CA policy gap quantified in Security Exposure analysis (ConfigMgr-only devices = 0% CA capable)
- [Zero Trust identity and device access](https://learn.microsoft.com/microsoft-365/security/office-365-security/zero-trust-identity-device-access-policies-overview) — Zero Trust framework used in Cloud Readiness scoring
- [Device health attestation](https://learn.microsoft.com/windows/security/hardware-security/tpm/trusted-platform-module-overview) — TPM attestation to Azure AD; only Intune can prove device health remotely
- [Intune security baselines](https://learn.microsoft.com/mem/intune/protect/security-baselines) — Cloud-delivered hardening profiles referenced in Decision Cards security weights
- [Microsoft Defender for Endpoint integration with Intune](https://learn.microsoft.com/mem/intune/protect/advanced-threat-protection) — MDE onboarding, threat state reporting (`partnerReportedThreatState`), ASR rules
- [Attack surface reduction rules](https://learn.microsoft.com/defender-endpoint/attack-surface-reduction) — ASR rules referenced in Endpoint Security workload risk scoring
- [BitLocker management with Intune](https://learn.microsoft.com/mem/intune/protect/encrypt-devices) — Cloud recovery key escrow vs MBAM; `isEncrypted` device property
- [Intune device compliance policies](https://learn.microsoft.com/mem/intune/protect/device-compliance-get-started) — Compliance evaluation that feeds CA enforcement and Security Exposure analysis
- [Windows active malware detection](https://learn.microsoft.com/graph/api/resources/intune-devices-manageddevice#properties) — `windowsActiveMalwareCount` property used in Active Malware comparison (requires MDE P2)
- [Intune remote actions](https://learn.microsoft.com/mem/intune/remote-actions/device-management) — 15+ remote actions (wipe, retire, lock, rotate BitLocker keys) vs ConfigMgr's 3; referenced in Cloud Readiness comparison

### Repository
- **GitHub**: `sccmavenger/cmaddin` (public)
- **Branch**: `main`
- **Current Version**: 3.17.247
- **Build**: `.\Build-And-Distribute.ps1 -PublishToGitHub -Force`

### Co-Management Workloads (7 Total)
1. Compliance Policies
2. Device Configuration
3. Endpoint Protection
4. Windows Update for Business
5. Client Apps
6. Office Click-to-Run Apps
7. Resource Access Policies
