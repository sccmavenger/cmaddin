# Architectural Decisions Log

This document captures key architectural and design decisions made during development. Each decision includes context, the choice made, and reasoning.

---

## ADR-001: Query Logging for Transparency
**Date**: 2025-01-15  
**Status**: Implemented  
**Context**: Customers asked "what queries are being used to display this data?"

**Decision**: Implemented comprehensive query logging in FileLogger with:
- Dedicated log file: `QueryLog.txt`
- Structured entries with timestamp, source, endpoint, query details
- Integration with DiagnosticsWindow for viewing logs

**Alternatives Considered**:
1. Log to main application log - Rejected: Would clutter general logs
2. Real-time display only - Rejected: No historical record
3. Separate database - Rejected: Overkill for this use case

**Consequences**:
- All Graph API, Admin Service, and WMI queries are now logged
- Users can export query log for troubleshooting
- Slight overhead for logging, but negligible

---

## ADR-002: Migration Impact Analysis Categories
**Date**: 2025-01-15  
**Status**: Implemented  
**Context**: Need to show before/after projections for migration planning

**Decision**: Six impact categories with percentage scores (0-100):
1. Security Impact
2. Operational Impact  
3. User Experience Impact
4. Cost Impact
5. Compliance Impact
6. Modernization Impact

**Rationale**:
- Percentage scoring matches existing Enrollment Confidence UI
- Six categories cover all stakeholder concerns
- Before/After format clearly shows improvement potential

**Alternatives Considered**:
1. Letter grades (A-F) - Rejected: Less precise
2. 1-5 star rating - Rejected: Limited granularity
3. Red/Yellow/Green only - Rejected: Too simplistic

---

## ADR-003: Log File Consolidation
**Date**: 2025-01-15  
**Status**: Implemented  
**Context**: Update logs were in %TEMP%, other logs in %LOCALAPPDATA%

**Decision**: All logs consolidated to `%LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs\`
- CloudJourneyAddin.log - Main application log
- QueryLog.txt - API query history
- Update.log - Auto-update operations

**Rationale**:
- Single location easier for support/troubleshooting
- %LOCALAPPDATA% persists longer than %TEMP%
- Follows Windows application conventions

---

## ADR-004: Mock Data for Disconnected State
**Date**: 2025-01-15  
**Status**: Implemented  
**Context**: Demo/presentation scenarios where live environment unavailable

**Decision**: Show realistic mock data in UI components when:
- Not connected to Graph API
- Not connected to ConfigMgr
- Demo mode explicitly enabled

**Implementation**:
- ConfidenceDetailsWindow shows sample score breakdowns
- RecommendationsWindow shows sample remediation items
- Clearly marked as "Demo Data" in UI

**Rationale**:
- Allows sales demos without live environment
- Helps users understand feature capabilities
- Better UX than blank/error screens

---

## ADR-005: GitHub Releases for Auto-Update
**Date**: 2024 (Original)  
**Status**: Implemented  
**Context**: Need mechanism for distributing updates to installed add-ins

**Decision**: Use GitHub Releases with:
- manifest.json - Version info, download URL, release notes
- ZIP file - Compiled add-in files

**Critical Lesson Learned** (2025-01-15):
- BOTH files MUST be uploaded to release assets
- Missing manifest.json causes update check to fail silently
- Use `-PublishToGitHub` flag in Build-And-Distribute.ps1

---

## ADR-006: Conventional Commits for Documentation
**Date**: 2025-01-16  
**Status**: Implemented  
**Context**: Need to maintain project context across development sessions

**Decision**: Adopt three-part documentation strategy:
1. `.github/copilot-instructions.md` - AI guidance for documentation
2. `.gitmessage` - Commit template with DECISION prompts
3. This file (DECISIONS.md) - Architectural decision record

**Rationale**:
- Conventional commits enable automated changelog generation
- DECISION markers in commits capture reasoning at commit time
- copilot-instructions.md ensures AI assistants update docs

---

## Template for New Decisions

```markdown
## ADR-XXX: [Title]
**Date**: YYYY-MM-DD  
**Status**: Proposed | Implemented | Deprecated | Superseded  
**Context**: [What is the issue?]

**Decision**: [What was decided]

**Alternatives Considered**:
1. [Alternative 1] - [Why rejected]
2. [Alternative 2] - [Why rejected]

**Consequences**:
- [Positive or negative outcomes]
- [Technical debt introduced]
- [Dependencies created]
```
