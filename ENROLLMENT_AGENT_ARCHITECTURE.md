# Enrollment Agent Architecture
**Version**: 2.0  
**Date**: December 19, 2025  
**Status**: Phase 1 Implementation (Agent Rebranding)

## Executive Summary

Transform the Cloud Journey Add-in from a monitoring/insights tool into an AI agent-powered enrollment orchestration platform. The enrollment agent will automatically plan and execute device enrollments with intelligent decision making, requiring minimal human intervention while maintaining strong safety controls.

**Vision**: "AI agent for device enrollment" - Set your goals, the agent executes the plan to achieve them.

---

## Three-Phase Roadmap

### Phase 1: Supervised Agent ✅ CURRENT
**Goal**: AI agent automates planning and execution while maintaining human oversight.

**Features**:
- AI agent automatically generates enrollment plans
- One-click approval workflow (no manual plan creation)
- Agent runs background execution service
- Real-time progress monitoring
- Emergency stop controls for agent
- Detailed audit logging of agent actions

**Safety Level**: HIGH - Human approves each agent plan before execution

**Risk Tolerance**: CONSERVATIVE
- Only enroll devices with readiness score ≥ 60 (Good/Excellent)
- Start with batch sizes ≤ 25 devices
- Require approval for all plans
- Pause if failure rate exceeds 15%
- Rollback capability for last 24 hours

---

### Phase 2: Conditional Autonomy (Future)
**Goal**: Full autonomy for low-risk devices, approval for medium/high-risk.

**Features**:
- Fully autonomous for devices with score ≥ 80 (Excellent)
- Auto-approval for batches ≤ 10 devices
- Requires approval for devices 60-79 (Good) or batches > 10
- Blocks devices < 60 (Fair/Poor) until admin reviews
- Self-adjusting batch sizes based on success rates
- Predictive issue detection

**Safety Level**: MEDIUM - Mix of autonomous and supervised operations

**Risk Tolerance**: BALANCED
- Auto-enroll excellent devices without approval
- Alert on medium-risk enrollments after execution
- Require approval for any risky scenarios

---

### Phase 3: Full Autonomy (Future)
**Goal**: Set goals and walk away - system handles everything.

**Features**:
- Complete end-to-end autonomy
- Self-learning optimization
- Dynamic strategy adjustment
- Automatic remediation
- Only alerts on critical issues or goal completion
- Advanced predictive analytics

**Safety Level**: LOW - Minimal human intervention

**Risk Tolerance**: AGGRESSIVE
- System decides all parameters autonomously
- Only stops for emergencies
- Weekly summary reports instead of real-time alerts

---

## Phase 1 Technical Architecture

### 1. Core Components

#### A. AutonomousEnrollmentService
**Location**: `Services/AutonomousEnrollmentService.cs`

**Responsibilities**:
- Generate enrollment plans using AI insights
- Manage plan approval workflow
- Execute approved plans in background
- Monitor execution progress and failures
- Implement emergency stop functionality
- Maintain audit trail

**Key Methods**:
```csharp
Task<EnrollmentPlan> GeneratePlanAsync(EnrollmentGoals goals)
Task<bool> SubmitPlanForApprovalAsync(EnrollmentPlan plan)
Task ExecutePlanAsync(EnrollmentPlan approvedPlan, CancellationToken ct)
Task EmergencyStopAsync()
Task<EnrollmentProgress> GetProgressAsync()
```

**State Management**:
- `NotConfigured` - No goals set
- `PlanGenerated` - AI created plan, awaiting approval
- `Executing` - Plan approved, enrollment in progress
- `Paused` - Emergency stop activated or failure threshold exceeded
- `Completed` - All devices enrolled successfully
- `Failed` - Critical errors encountered

---

#### B. EnrollmentGoals Model
**Location**: `Models/EnrollmentGoals.cs`

**Configuration Properties**:
```csharp
DateTime TargetCompletionDate          // Deadline for completion
int? MaxDevicesPerDay                  // Rate limiting (null = AI decides)
int? PreferredBatchSize                // Batch preference (null = AI decides)
RiskTolerance RiskLevel                // Conservative/Balanced/Aggressive
OperatingHours Schedule                // BusinessHours/Extended/Always
bool RequireApprovalForAllPlans        // Phase 1: Always true
double FailureThresholdPercent         // Default: 15% (pause if exceeded)
List<string> ExcludedDeviceIds         // Devices to skip
List<string> PriorityDeviceIds         // Enroll these first
```

**Risk Tolerance Definitions**:
- **Conservative** (Phase 1 default):
  - Only devices with score ≥ 60
  - Max batch size: 25
  - Pause if failures > 15%
  - Require approval for all plans

- **Balanced** (Phase 2):
  - Devices with score ≥ 50
  - Max batch size: 50
  - Pause if failures > 20%
  - Auto-approve low-risk, require approval for medium/high

- **Aggressive** (Phase 3):
  - All devices (AI decides readiness)
  - No batch size limit
  - Pause if failures > 30%
  - Full autonomy

---

#### C. EnrollmentPlan Model
**Location**: `Models/EnrollmentPlan.cs`

**Plan Properties**:
```csharp
string PlanId                          // Unique identifier
DateTime GeneratedDate                 // When AI created plan
EnrollmentGoals Goals                  // Original goals
List<EnrollmentBatch> Batches          // Ordered list of batches
int TotalDevices                       // Total devices in plan
TimeSpan EstimatedDuration             // How long plan will take
string AIReasoning                     // Why AI chose this approach
PlanStatus Status                      // Generated/Approved/Executing/Complete
```

**EnrollmentBatch**:
```csharp
int BatchNumber                        // Sequence (1, 2, 3...)
List<string> DeviceIds                 // Devices in this batch
DateTime ScheduledTime                 // When to enroll
string Justification                   // Why these devices grouped together
double AverageRiskScore                // Batch risk assessment
```

---

#### D. EnrollmentProgress Model
**Location**: `Models/EnrollmentProgress.cs`

**Real-time Tracking**:
```csharp
string PlanId                          // Associated plan
int TotalDevices                       // Total in plan
int DevicesEnrolled                    // Successfully completed
int DevicesFailed                      // Enrollment failures
int DevicesPending                     // Not yet attempted
double SuccessRate                     // Percentage successful
int CurrentBatch                       // Which batch is executing
DateTime StartTime                     // When execution began
DateTime? EstimatedCompletion          // Projected finish time
List<EnrollmentResult> RecentResults   // Last 10 enrollments
```

**EnrollmentResult**:
```csharp
string DeviceId
string DeviceName
DateTime AttemptTime
bool Success
string ErrorMessage                    // If failed
TimeSpan Duration                      // How long enrollment took
```

---

### 2. User Interface Components

#### A. Autonomous Mode Control Panel
**Location**: DashboardWindow.xaml - New section in Enrollment tab

**Components**:
1. **Enable/Disable Toggle**
   - Clearly visible switch to activate autonomous mode
   - Confirmation dialog before enabling

2. **Goal Configuration Card**
   - Target completion date picker
   - Risk tolerance selector (Conservative/Balanced/Aggressive)
   - Advanced options (collapsed by default):
     - Max devices per day
     - Preferred batch size
     - Operating hours schedule
     - Failure threshold

3. **Current Status Display**
   - Large status indicator (NotConfigured/PlanGenerated/Executing/etc.)
   - Progress bar with percentage
   - Devices enrolled / total
   - Success rate meter

4. **Action Buttons**
   - "Generate Plan" (when configured)
   - "Review & Approve Plan" (when plan ready)
   - "Emergency Stop" (large, red, always visible when executing)
   - "View Audit Log"

---

#### B. Plan Approval Dialog
**Location**: New window `Views/PlanApprovalDialog.xaml`

**Displays**:
- Plan summary (total devices, batches, duration)
- AI reasoning for this approach
- Batch-by-batch breakdown
- Risk assessment per batch
- Estimated timeline
- Approve/Reject/Modify buttons

**Modification Options**:
- Remove specific devices
- Adjust batch sizes
- Change schedule times
- Add delays between batches

---

#### C. Progress Monitoring Panel
**Location**: DashboardWindow.xaml - Expands when execution active

**Real-time Displays**:
- Current batch progress bar
- Recently enrolled devices (scrolling list)
- Live success/failure counts
- Next batch scheduled time
- Estimated completion countdown

**Refresh Rate**: Every 5 seconds during active execution

---

#### D. Audit Log Viewer
**Location**: New window `Views/AuditLogViewer.xaml`

**Shows**:
- Every enrollment attempt (success/failure)
- Plan generation events
- Approval/rejection decisions
- Emergency stop activations
- Configuration changes
- AI reasoning for decisions
- System-initiated actions (auto-pause, etc.)

**Filters**:
- Date range
- Event type
- Device name/ID
- Success/failure only

---

### 3. Safety Controls (Critical)

#### A. Emergency Stop
**Trigger**: Large red button in UI, always visible during execution

**Actions**:
1. Immediately cancel all in-flight enrollments
2. Mark plan status as "Paused"
3. Log emergency stop event with timestamp
4. Send notification to admin
5. Require explicit "Resume" approval to continue

**Use Cases**:
- Infrastructure issues detected
- Business requirement changes
- User observes unexpected behavior

---

#### B. Automatic Pause Conditions
System automatically stops if:

1. **Failure Rate Threshold Exceeded**
   - Default: 15% of enrollments failing
   - Pauses immediately
   - Generates failure analysis report
   - Requires admin review before resume

2. **Infrastructure Overload Detected**
   - Monitors Graph API throttling
   - Monitors ConfigMgr response times
   - Pauses if services degraded

3. **Unexpected Errors**
   - Authentication failures
   - Permission errors
   - Critical exceptions

4. **Time Window Expiration**
   - If operating hours set to "Business Hours"
   - Automatically pauses at end of window
   - Resumes next business day

---

#### C. Rollback Capability
**Scope**: Last 24 hours of enrollments

**Mechanism**:
1. Track every device enrollment with timestamp
2. Store pre-enrollment state (if possible)
3. Provide "Rollback Last Batch" button
4. Reverses enrollments in reverse chronological order
5. Logs all rollback actions

**Limitations**:
- May not be fully reversible (Intune API constraints)
- Best effort basis
- Admin must validate post-rollback

---

#### D. Approval Workflow (Phase 1)

**Required Approvals**:
- Every generated plan (no auto-execution)
- Any plan modifications
- Resume after emergency stop
- Resume after auto-pause

**Approval Tracking**:
- Who approved (current user)
- When approved (timestamp)
- What was approved (plan ID)
- Stored in audit log

---

### 4. AI Integration

#### A. Plan Generation
**Input**: EnrollmentGoals + current device data

**AI Prompt Structure**:
```
You are an autonomous enrollment orchestrator. Given:
- Goal: Enroll {X} devices by {date}
- Risk tolerance: {Conservative/Balanced/Aggressive}
- Current device data: {readiness scores, constraints}

Generate an optimal enrollment plan with:
1. Batch sizes (start small, increase if successful)
2. Device prioritization (best candidates first)
3. Timeline (respect operating hours, rate limits)
4. Risk mitigation (pause conditions, validation points)
5. Reasoning (explain your strategy)

Output as JSON: { batches: [...], reasoning: "...", estimatedDuration: ... }
```

**AI Decision Factors**:
- Device readiness scores
- Historical enrollment success rates
- Infrastructure capacity
- Risk tolerance settings
- Time constraints
- Detected patterns (e.g., certain device types fail more)

---

#### B. Adaptive Optimization (Future: Phase 2)
After each batch:
- Analyze success/failure patterns
- Adjust subsequent batches
- Learn which device characteristics predict success
- Optimize batch sizes dynamically

Example: "Devices with OS build 22000.x have 95% success rate - prioritize these"

---

### 5. Data Persistence

#### A. Configuration Storage
**File**: `%LocalAppData%\CloudJourneyAddin\autonomous-config.json`

**Contents**:
- Enabled/disabled state
- Current goals
- Risk tolerance settings
- Operating hours

---

#### B. Plan Storage
**File**: `%LocalAppData%\CloudJourneyAddin\plans\{plan-id}.json`

**Contents**:
- Complete plan details
- Approval metadata
- Execution progress
- Results for each device

**Retention**: 90 days, then archive

---

#### C. Audit Log
**File**: `%LocalAppData%\CloudJourneyAddin\audit-log.jsonl`

**Format**: JSON Lines (one event per line)

**Rotation**: Daily, keep 30 days

**Example Entry**:
```json
{
  "timestamp": "2025-12-19T14:32:18Z",
  "event": "PlanApproved",
  "planId": "plan-2025-12-19-001",
  "user": "admin@contoso.com",
  "details": { "totalDevices": 150, "batches": 6 }
}
```

---

### 6. Performance Considerations

#### A. Throttling
- Respect Graph API rate limits (default: 100 requests/minute)
- Respect ConfigMgr Admin Service limits
- Configurable delays between enrollments (default: 30 seconds)

#### B. Background Processing
- Service runs on background thread
- Does not block UI
- Progress updates via events/callbacks

#### C. Resource Usage
- Monitor CPU/memory during execution
- Pause if system resources exhausted
- Log resource usage metrics

---

### 7. Security Considerations

#### A. Permissions Required
**Current**: Read-only access to Intune + ConfigMgr

**New (Phase 1)**: Need write permissions to trigger enrollments

**Required Scopes**:
- `DeviceManagementManagedDevices.ReadWrite.All` (Intune)
- ConfigMgr Admin Service write permissions

**Authentication**: Leverage existing Azure AD auth flow

---

#### B. Access Control
- Only users with admin role can enable autonomous mode
- All actions logged with user identity
- Sensitive operations require re-authentication (optional enhancement)

---

#### C. Data Protection
- Plans stored locally (not transmitted)
- Audit logs encrypted at rest (future enhancement)
- No PII in logs beyond device IDs

---

### 8. Testing Strategy

#### A. Unit Tests
- Test plan generation logic
- Test batch scheduling algorithm
- Test failure threshold detection
- Test emergency stop mechanism

#### B. Integration Tests
- End-to-end plan execution (test environment)
- Approval workflow
- Progress tracking accuracy
- Rollback functionality

#### C. Dry Run Mode (Future Enhancement)
- Execute plan without actual enrollments
- Simulate timing and outcomes
- Validate plan structure before committing

---

### 9. Monitoring & Alerting

#### A. Real-Time Monitoring
**In-App**:
- Progress dashboard (live updates every 5s)
- Failure rate alerts
- Batch completion notifications

**Windows Notifications** (optional):
- Plan approved and started
- Emergency stop triggered
- Plan completed successfully
- Failures exceed threshold

---

#### B. Email Notifications (Future)
- Daily summary during execution
- Immediate alerts for critical issues
- Weekly reports when idle

---

#### C. Metrics to Track
- Total devices enrolled
- Success rate (overall and per batch)
- Average enrollment duration
- Time to completion vs. estimate
- Emergency stops count
- Auto-pause events
- Plans generated vs. approved

---

### 10. Error Handling

#### A. Enrollment Failures
**Actions**:
1. Log detailed error message
2. Mark device as "Failed"
3. Continue with remaining devices in batch
4. If failure rate > threshold, pause

**Retry Logic**:
- Retry failed devices in next batch (configurable)
- Exponential backoff for transient errors
- Max 3 retries per device

---

#### B. Critical Errors
**Scenarios**:
- Authentication expired
- Permissions revoked
- Service unavailable

**Actions**:
1. Emergency stop all operations
2. Log critical error
3. Alert admin
4. Require intervention before resuming

---

### 11. Future Enhancements (Post-Phase 1)

#### A. Phase 2 Additions
- Conditional autonomy (auto-approve low-risk)
- Self-adjusting batch sizes
- Predictive issue detection
- Learning from historical data

#### B. Phase 3 Additions
- Full autonomy mode
- Multi-tenant support
- Advanced AI optimization
- Automated remediation

#### C. Long-term Vision
- Integration with ITSM ticketing
- Compliance reporting
- Cost optimization insights
- Multi-cloud support (AWS, GCP)

---

## Implementation Timeline

### Phase 1: Autopilot with Supervision
**Duration**: 2-3 weeks

**Week 1**:
- Create service foundation (AutonomousEnrollmentService)
- Implement models (Goals, Plan, Progress)
- Build basic UI controls

**Week 2**:
- Integrate AI plan generation
- Build approval workflow
- Implement execution engine

**Week 3**:
- Add safety controls (emergency stop, auto-pause)
- Build progress monitoring UI
- Testing and refinement

---

## Success Metrics

### Phase 1 Goals
- 90%+ success rate on approved plans
- Zero unintended enrollments (approval workflow works)
- Emergency stop responds within 5 seconds
- Users report "significantly less manual work"

### Long-term Goals (All Phases)
- Reduce admin time spent on enrollments by 80%
- Achieve 95%+ enrollment success rate
- Complete migrations 50% faster than manual process
- Zero security incidents related to autonomous operations

---

## Risk Mitigation

### Identified Risks & Mitigations

**Risk 1**: Autonomous system enrolls wrong devices
- **Mitigation**: Phase 1 requires approval for all plans
- **Mitigation**: Detailed plan preview before approval
- **Mitigation**: Exclude list for protected devices

**Risk 2**: Mass enrollment failures cause outage
- **Mitigation**: Auto-pause at 15% failure threshold
- **Mitigation**: Conservative batch sizes (start with 25)
- **Mitigation**: Emergency stop always available

**Risk 3**: AI generates flawed plan
- **Mitigation**: Human approval required (Phase 1)
- **Mitigation**: Display AI reasoning for transparency
- **Mitigation**: Ability to modify plan before approval

**Risk 4**: System runs during maintenance window
- **Mitigation**: Operating hours configuration
- **Mitigation**: Manual pause before maintenance
- **Mitigation**: Maintenance mode setting (future)

**Risk 5**: Security concerns with autonomous operations
- **Mitigation**: Comprehensive audit logging
- **Mitigation**: Role-based access control
- **Mitigation**: All actions reversible (rollback)

---

## Glossary

- **Autonomous Mode**: System operating with minimal human intervention
- **Enrollment Plan**: AI-generated sequence of device batches to enroll
- **Batch**: Group of devices enrolled together
- **Risk Tolerance**: How aggressive the system should be (Conservative/Balanced/Aggressive)
- **Emergency Stop**: Immediate halt of all autonomous operations
- **Auto-Pause**: System-initiated stop due to threshold/condition
- **Approval Workflow**: Human review before plan execution
- **Rollback**: Reverse recent enrollments
- **Audit Log**: Complete history of all autonomous actions

---

## Appendix: Configuration Examples

### Example 1: Conservative Approach
```json
{
  "targetCompletionDate": "2026-01-31",
  "riskTolerance": "Conservative",
  "maxDevicesPerDay": 50,
  "preferredBatchSize": 25,
  "operatingHours": "BusinessHours",
  "requireApprovalForAllPlans": true,
  "failureThresholdPercent": 15
}
```

**Result**: Slow and steady, maximum safety

---

### Example 2: Aggressive Timeline
```json
{
  "targetCompletionDate": "2025-12-31",
  "riskTolerance": "Balanced",
  "maxDevicesPerDay": 200,
  "preferredBatchSize": null,
  "operatingHours": "Extended",
  "requireApprovalForAllPlans": true,
  "failureThresholdPercent": 20
}
```

**Result**: Faster pace, AI decides batch sizes

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-12-19 | System | Initial architecture document for Phase 1 |

---

## References

- User Request: "Self-driving car for device enrollment"
- Risk Tolerance: Conservative approach for Phase 1
- Safety First: All plans require approval before execution
