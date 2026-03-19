<!--
  Sync Impact Report
  ==================
  Version change: 0.0.0 → 1.0.0 (MAJOR — initial ratification)
  Modified principles: N/A (initial creation)
  Added sections:
    - Core Principles (5 principles)
    - Technology & Architecture Constraints
    - Development Workflow & Quality Gates
    - Governance
  Removed sections: N/A
  Templates requiring updates:
    - .specify/templates/plan-template.md — ✅ compatible (Constitution Check
      section already uses dynamic gates from constitution file)
    - .specify/templates/spec-template.md — ✅ compatible (no constitution-
      specific references to update)
    - .specify/templates/tasks-template.md — ✅ compatible (no constitution-
      specific references to update)
    - .specify/templates/commands/ — N/A (directory does not exist)
  Follow-up TODOs: none
-->

# Cloud Native Assessment Constitution

## Core Principles

### I. Mission Alignment (NON-NEGOTIABLE)

Every feature MUST trace to at least one primary mission goal:

1. **Influence customers to enroll more devices into Microsoft Intune.**
2. **Influence customers to move workloads to Microsoft Intune.**

Features that do not demonstrably support Goal 1, Goal 2, or both
MUST NOT be built. Sub-goals (data accuracy, UX, diagnostics,
automation) are valid only when they trace back to a primary goal.

**Rationale:** The tool exists to give IT administrators situational
awareness of their cloud-native device management journey and to
motivate forward progress. Unaligned features dilute focus and
increase maintenance burden.

### II. Actionable Insight Over Passive Reporting

Every data visualization or metric presented to the user MUST include
a clear, actionable next step. The tool MUST NOT display vanity
metrics or read-only dashboards that do not influence enrollment or
workload migration decisions.

- Data MUST answer "what should I do next?"
- Projections and estimates MUST cite their data source (see ADR-007).
- Complexity that causes analysis paralysis MUST be avoided.

**Rationale:** Situational awareness is only valuable when it drives
action. Passive reporting duplicates existing ConfigMgr/Intune console
capabilities without adding value.

### III. Data Privacy & Transparency

- The application MUST NOT collect, store, or transmit personally
  identifiable information (PII) such as device names, hostnames,
  usernames, or email addresses.
- All external queries (Graph API, Admin Service, WMI) MUST be logged
  via `FileLogger` so administrators can audit exactly what data the
  tool retrieves.
- Authentication credentials MUST never be persisted by the
  application; MSAL handles token lifecycle.

**Rationale:** Enterprise IT teams require full transparency about
what data leaves their environment. Trust is a prerequisite for
adoption, and adoption drives enrollment.

### IV. Graceful Degradation & Mock Data

The application MUST function in disconnected or partially connected
states by providing mock/demo data that demonstrates feature
capabilities. Every view MUST render meaningfully when:

- Graph API is unreachable (no Intune connection).
- ConfigMgr Admin Service is unreachable (WMI fallback, then mock).
- Azure OpenAI is not configured (AI features degrade gracefully).

**Rationale:** Administrators evaluate the tool before committing to
full setup. A broken first-run experience kills adoption. Mock data
also enables demos and stakeholder presentations without live
environments.

### V. Simplicity & YAGNI

- Start with the simplest implementation that delivers value.
- Do not build for hypothetical future requirements.
- Prefer extending existing patterns (MVVM, services, converters)
  over introducing new architectural layers.
- New abstractions MUST be justified against the complexity they add;
  every added layer MUST solve a concrete, current problem.

**Rationale:** A WPF add-in running inside the ConfigMgr console has
a constrained execution environment. Unnecessary complexity increases
load time, memory use, and maintenance cost with no user benefit.

## Technology & Architecture Constraints

- **Framework**: .NET 8.0, WPF, MVVM pattern.
- **Data sources**: Microsoft Graph API (Intune), ConfigMgr Admin
  Service (REST), WMI fallback.
- **Logging**: `FileLogger` singleton to
  `%LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs\`.
- **Update system**: GitHub Releases with `manifest.json` + ZIP/MSI
  assets.
- **Versioning**: `MAJOR.MINOR.PATCH` in `ZeroTrustMigrationAddin.csproj`.
  Increment PATCH for fixes, MINOR for features, MAJOR for breaking
  changes.
- **Documentation**: `CHANGELOG.md` updated with every change;
  `DECISIONS.md` records architectural decisions; `CONTEXT.md`
  reflects current project state.
- **Directory rules**: Docs in `docs/`, scripts in `scripts/`,
  build outputs in `builds/`, no stray files at root.

## Development Workflow & Quality Gates

1. **Mission gate**: Before any feature work, confirm alignment with
   Goal 1, Goal 2, or both (reference `docs/MISSION.md`).
2. **Mock-first development**: New views MUST render with mock data
   before live data integration is attempted.
3. **Build verification**: Every change MUST compile cleanly with
   zero new errors or warnings before commit.
4. **Changelog discipline**: Every commit MUST have a corresponding
   `CHANGELOG.md` entry categorized as Added, Changed, Fixed,
   Security, or Deprecated.
5. **Conventional commits**: Commit messages follow the format
   `<type>(<scope>): <description>` (see `copilot-instructions.md`).
6. **Architectural decisions**: Non-trivial design choices MUST be
   recorded as ADR entries in `docs/DECISIONS.md` with context,
   alternatives considered, and rationale.

## Governance

This constitution supersedes ad-hoc practices and MUST be consulted
before proposing new features or architectural changes.

- **Amendments**: Any change to this constitution MUST be documented
  with the reason for the change, recorded in `CHANGELOG.md`, and
  reflected in the version below.
- **Versioning**: Constitution version follows semantic versioning —
  MAJOR for principle removals or redefinitions, MINOR for new
  principles or material expansions, PATCH for clarifications.
- **Compliance**: All pull requests and code reviews MUST verify that
  changes do not violate the principles above. Violations MUST be
  resolved or justified in the Complexity Tracking section of the
  relevant plan document.
- **Runtime guidance**: See `.github/copilot-instructions.md` for
  day-to-day development conventions and command shortcuts.

**Version**: 1.0.0 | **Ratified**: 2026-03-18 | **Last Amended**: 2026-03-18
