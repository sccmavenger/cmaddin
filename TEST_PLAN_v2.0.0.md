# Test Plan - CloudJourney Addin v2.0.0

## 🎯 Testing Overview

**Version**: 2.0.0 - Enrollment Agent Major Release  
**Test Date**: December 19, 2025  
**Tester**: _________________  
**Build**: CloudJourneyAddin-v2.0.0-COMPLETE.zip

---

## ✅ Pre-Test Setup

### 1. Extract Package
```powershell
# Extract to test location
Expand-Archive -Path "CloudJourneyAddin-v2.0.0-COMPLETE.zip" -DestinationPath "C:\CloudJourneyTest"
cd C:\CloudJourneyTest
```

### 2. Verify File Count
```powershell
# Should be ~285 files
(Get-ChildItem -Recurse -File).Count
```
**Expected**: ~285 files  
**Result**: _______ files ✅ ❌

---

## 🧪 TEST CASES

### TEST 1: Application Launch & Version Verification

**Purpose**: Verify app launches and shows correct version

**Steps**:
1. Double-click `CloudJourneyAddin.exe`
2. Check window title bar

**Expected Results**:
- ✅ Application launches without errors
- ✅ Window title shows: "Cloud Journey Progress Dashboard v2.0.0"
- ✅ No error dialogs appear
- ✅ Dashboard loads with tabs: Overview, Enrollment, Workloads, Applications, Executive

**Actual Results**:
- Window title: _______________________
- Errors: _______________________
- Status: ✅ PASS ❌ FAIL

---

### TEST 2: Unauthenticated Mode - Mock Data Display

**Purpose**: Verify mock data shows when NOT authenticated, no AI calls made

**Steps**:
1. Launch app (do NOT click Authenticate)
2. Check Overview tab shows mock data
3. Navigate to Enrollment tab
4. Check for Agent Mode section

**Expected Results**:
- ✅ Overview shows mock enrollment data (2500 total, 1400 enrolled, 1100 ConfigMgr only)
- ✅ Charts display mock data
- ✅ Agent Mode section visible in Enrollment tab
- ✅ Agent Mode toggle is OFF by default
- ✅ No Azure OpenAI calls happening (no API errors)

**Actual Results**:
- Mock data displayed: ✅ ❌
- Agent section visible: ✅ ❌
- Errors: _______________________
- Status: ✅ PASS ❌ FAIL

---

### TEST 3: Agent Mode Toggle (Unauthenticated)

**Purpose**: Verify agent mode can be toggled ON without authentication

**Steps**:
1. Navigate to Enrollment tab
2. Find "Agent Mode" toggle
3. Toggle Agent Mode ON
4. Check UI changes

**Expected Results**:
- ✅ Agent Mode toggle turns ON
- ✅ Agent configuration section appears (Target Date, Risk Tolerance, Operating Hours)
- ✅ "✨ Generate Plan" button is visible and enabled
- ✅ Status shows: "Ready to Generate Plan" with blue ⚙️ icon
- ✅ No reasoning panel visible yet

**Actual Results**:
- Toggle works: ✅ ❌
- Config section appears: ✅ ❌
- Generate Plan button: ✅ ❌
- Status: ✅ PASS ❌ FAIL

---

### TEST 4: Agent Execution (Unauthenticated - Mock Mode)

**Purpose**: Verify agent runs with rule-based reasoning and mock data

**Steps**:
1. Agent Mode should be ON
2. Click "✨ Generate Plan" button
3. Watch status indicator
4. Watch for reasoning panel
5. Wait for completion (~10-15 seconds)

**Expected Results**:
- ✅ Generate Plan button **disables** immediately
- ✅ Status icon changes from ⚙️ (blue) to 🤖 (green)
- ✅ Status text updates to "Generating enrollment plan..."
- ✅ **🛑 STOP button appears** (red, only while running)
- ✅ **Agent Reasoning Panel appears** below status
- ✅ Reasoning steps appear one by one:
  - Step 1: "First, I need to understand the device inventory..." → query_devices
  - Step 2: "Now I have the device list. Let me analyze..." → analyze_readiness
  - Step 3: "I see devices ready for enrollment..." → enroll_devices
- ✅ Each step shows:
  - Thought (what agent is thinking)
  - Tool (which tool it's calling)
  - Observation (result from tool)
  - Reflection (agent's learning)
- ✅ Agent completes after 3-4 steps
- ✅ Generate Plan button **re-enables** when done
- ✅ Status returns to "Ready to Generate Plan"
- ✅ **NO Azure OpenAI API calls** (should be rule-based reasoning only)

**Actual Results**:
- Button disables: ✅ ❌
- Icon changes to 🤖: ✅ ❌
- Stop button appears: ✅ ❌
- Reasoning panel appears: ✅ ❌
- Step 1 displays: ✅ ❌
- Step 2 displays: ✅ ❌
- Step 3 displays: ✅ ❌
- Observations show data: ✅ ❌
- Completes successfully: ✅ ❌
- Button re-enables: ✅ ❌
- No AI calls made: ✅ ❌
- Status: ✅ PASS ❌ FAIL

**Screenshots**: (Attach reasoning panel showing steps)

---

### TEST 5: Agent Stop Function

**Purpose**: Verify emergency stop button works

**Steps**:
1. Agent Mode ON
2. Click "Generate Plan"
3. While agent is running, click **🛑 STOP** button
4. Check agent stops gracefully

**Expected Results**:
- ✅ Stop button is visible only while agent running
- ✅ Clicking Stop halts agent execution
- ✅ Reasoning trace is preserved (steps remain visible)
- ✅ Generate Plan button re-enables
- ✅ Status returns to ready state

**Actual Results**:
- Stop button visible: ✅ ❌
- Agent stops: ✅ ❌
- Steps preserved: ✅ ❌
- Status: ✅ PASS ❌ FAIL

---

### TEST 6: Authentication Flow

**Purpose**: Verify Microsoft authentication works

**Prerequisites**: 
- Valid Microsoft 365 credentials
- User has permissions to read device data

**Steps**:
1. Click **🔐 Authenticate** button (top right, orange)
2. Sign in with Microsoft credentials
3. Grant permissions if prompted
4. Wait for authentication to complete

**Expected Results**:
- ✅ Browser/auth popup appears
- ✅ Microsoft login page loads
- ✅ After successful login, popup closes
- ✅ Dashboard shows user name (top right)
- ✅ Authenticate button changes or disappears
- ✅ Data on Overview tab updates to **real data** (may take a moment)

**Actual Results**:
- Auth popup: ✅ ❌
- Login successful: ✅ ❌
- User name appears: _______________
- Data updates: ✅ ❌
- Status: ✅ PASS ❌ FAIL

---

### TEST 7: Agent Execution (Authenticated - GPT-4 Mode)

**Purpose**: Verify agent uses real GPT-4 reasoning when authenticated

**Prerequisites**: 
- Must be authenticated (TEST 6 passed)
- Azure OpenAI configured in appsettings.json

**Steps**:
1. Stay authenticated
2. Navigate to Enrollment tab
3. Toggle Agent Mode ON (if not already)
4. Click "Generate Plan"
5. Watch reasoning panel carefully

**Expected Results**:
- ✅ Agent executes with **real GPT-4 reasoning**
- ✅ Reasoning steps show more intelligent, natural language:
  - Step 1: Agent queries **real device data** from Graph API
  - Step 2: Agent analyzes **actual blockers** (e.g., "250 devices blocked by BitLocker")
  - Step 3: Agent shows **real enrollment recommendations**
- ✅ Observations contain **actual data** (not mock 1100 devices)
- ✅ Reflections show GPT-4's learning and strategy
- ✅ Steps may vary based on actual data (not always same 3 steps)

**Actual Results**:
- Real data queried: ✅ ❌
- Actual device count: _______________
- Actual blockers shown: _______________
- GPT-4 reasoning evident: ✅ ❌
- Status: ✅ PASS ❌ FAIL

**Note**: If Azure OpenAI is not configured, agent will fall back to rule-based reasoning even when authenticated.

---

### TEST 8: Agent Memory (View Memory Button)

**Purpose**: Verify agent stores and displays learned patterns

**Steps**:
1. After running agent 2-3 times
2. Click **📂 View Memory** button (in agent section)
3. Check memory viewer appears

**Expected Results**:
- ✅ Memory viewer opens (dialog or new window)
- ✅ Shows stored patterns from previous executions
- ✅ Displays success rates for different strategies
- ✅ Memory location: %LocalAppData%\CloudJourneyAddin\AgentMemory\

**Actual Results**:
- Memory viewer opens: ✅ ❌
- Patterns displayed: ✅ ❌
- Success rates shown: ✅ ❌
- Status: ✅ PASS ❌ FAIL

---

### TEST 9: Agent Configuration Changes

**Purpose**: Verify agent configuration can be modified

**Steps**:
1. Agent Mode ON
2. Change Target Completion Date (calendar picker)
3. Change Risk Tolerance (Low/Medium/High)
4. Change Operating Hours (dropdown)
5. Click **💾 Save Configuration** button

**Expected Results**:
- ✅ All fields are editable
- ✅ Save Configuration button works
- ✅ Configuration persists when re-opening app
- ✅ Settings affect agent planning (visible in reasoning)

**Actual Results**:
- Fields editable: ✅ ❌
- Save works: ✅ ❌
- Persists: ✅ ❌
- Status: ✅ PASS ❌ FAIL

---

### TEST 10: Reasoning Panel Scrolling

**Purpose**: Verify reasoning panel handles many steps

**Steps**:
1. Run agent multiple times
2. Check reasoning panel with 10+ steps

**Expected Results**:
- ✅ Reasoning panel is scrollable
- ✅ Max height is ~400px
- ✅ Scrollbar appears when content overflows
- ✅ All steps remain visible (can scroll to see)

**Actual Results**:
- Scrollable: ✅ ❌
- All steps accessible: ✅ ❌
- Status: ✅ PASS ❌ FAIL

---

### TEST 11: Multiple Tabs Navigation

**Purpose**: Verify all tabs work and agent state persists

**Steps**:
1. Start agent execution
2. While agent running, switch to Overview tab
3. Switch back to Enrollment tab
4. Check agent still running

**Expected Results**:
- ✅ Can switch tabs while agent running
- ✅ Agent execution continues in background
- ✅ Reasoning steps preserved when returning to Enrollment tab
- ✅ All tabs display data correctly

**Actual Results**:
- Tab switching works: ✅ ❌
- Agent continues: ✅ ❌
- Steps preserved: ✅ ❌
- Status: ✅ PASS ❌ FAIL

---

### TEST 12: Diagnostics Tools

**Purpose**: Verify included diagnostic scripts work

**Steps**:
1. Right-click **Diagnose-Installation.ps1**
2. Run with PowerShell (as Administrator)
3. Check output

**Expected Results**:
- ✅ Script runs without errors
- ✅ Shows environment information
- ✅ Verifies .NET, dependencies, permissions
- ✅ No critical issues reported

**Actual Results**:
- Script runs: ✅ ❌
- Environment check: ✅ ❌
- Issues found: _______________
- Status: ✅ PASS ❌ FAIL

---

### TEST 13: Performance & Stability

**Purpose**: Verify app is stable and performant

**Steps**:
1. Run agent 10 times consecutively
2. Monitor memory usage (Task Manager)
3. Check for crashes or slowdowns

**Expected Results**:
- ✅ Memory usage stays ~200-400 MB
- ✅ No memory leaks (memory doesn't keep growing)
- ✅ No crashes
- ✅ Response time remains consistent
- ✅ Each execution completes in 15-30 seconds

**Actual Results**:
- Memory usage: _______________ MB
- Crashes: ✅ ❌
- Performance: ✅ ❌
- Status: ✅ PASS ❌ FAIL

---

### TEST 14: Error Handling

**Purpose**: Verify graceful error handling

**Test Cases**:

**14A: Invalid Azure OpenAI Configuration**
- Modify appsettings.json with invalid API key
- Try authenticated agent execution
- **Expected**: Error message, falls back to rule-based reasoning

**14B: Network Disconnection**
- Disconnect network while authenticated
- Try agent execution
- **Expected**: Graceful error, doesn't crash

**14C: Rapid Button Clicks**
- Click Generate Plan multiple times rapidly
- **Expected**: Only starts once, subsequent clicks ignored

**Actual Results**:
- 14A: ✅ ❌
- 14B: ✅ ❌
- 14C: ✅ ❌
- Status: ✅ PASS ❌ FAIL

---

## 🎯 CRITICAL SUCCESS CRITERIA

### Must Pass (P0 - Critical)
- ✅ TEST 1: App launches and shows v2.0.0
- ✅ TEST 2: Mock data displays when unauthenticated
- ✅ TEST 4: Agent executes with reasoning panel
- ✅ TEST 5: Stop button works

### Should Pass (P1 - High Priority)
- ✅ TEST 3: Agent Mode toggles correctly
- ✅ TEST 6: Authentication works
- ✅ TEST 7: GPT-4 reasoning when authenticated
- ✅ TEST 11: Tab navigation works

### Nice to Have (P2 - Medium Priority)
- ✅ TEST 8: Memory viewer works
- ✅ TEST 9: Configuration saves
- ✅ TEST 10: Reasoning panel scrolls
- ✅ TEST 13: Performance is good

---

## 📊 Test Summary

**Date Tested**: _________________  
**Tester Name**: _________________  
**Environment**: _________________

| Test | Status | Notes |
|------|--------|-------|
| TEST 1: Launch & Version | ✅ ❌ | |
| TEST 2: Unauthenticated Mode | ✅ ❌ | |
| TEST 3: Agent Toggle | ✅ ❌ | |
| TEST 4: Agent Execution (Mock) | ✅ ❌ | |
| TEST 5: Stop Button | ✅ ❌ | |
| TEST 6: Authentication | ✅ ❌ | |
| TEST 7: Agent (GPT-4) | ✅ ❌ | |
| TEST 8: Memory Viewer | ✅ ❌ | |
| TEST 9: Configuration | ✅ ❌ | |
| TEST 10: Scrolling | ✅ ❌ | |
| TEST 11: Tab Navigation | ✅ ❌ | |
| TEST 12: Diagnostics | ✅ ❌ | |
| TEST 13: Performance | ✅ ❌ | |
| TEST 14: Error Handling | ✅ ❌ | |

**Overall Status**: ✅ PASS ❌ FAIL ⚠️ PARTIAL

**Critical Issues Found**:
1. _______________________________________
2. _______________________________________
3. _______________________________________

**Recommendation**: 
- [ ] ✅ Ready for Production
- [ ] ⚠️ Ready with Minor Issues
- [ ] ❌ Not Ready - Major Issues

---

## 🐛 Bug Report Template

**Bug ID**: _______  
**Test Case**: _______  
**Severity**: Critical / High / Medium / Low  
**Description**: _______________________________________  
**Steps to Reproduce**:
1. _______________________________________
2. _______________________________________
3. _______________________________________

**Expected**: _______________________________________  
**Actual**: _______________________________________  
**Screenshots**: (Attach)  
**Logs**: (Attach debug.log if available)

---

## 📝 Testing Notes

**Environment Details**:
- OS: Windows _______________
- RAM: _______________ GB
- Azure OpenAI Configured: ✅ ❌
- Microsoft 365 Tenant: _______________
- Internet Connection: ✅ ❌

**Additional Observations**:
- _______________________________________
- _______________________________________
- _______________________________________

---

## ✅ Sign-Off

**Tested By**: _________________  
**Date**: _________________  
**Signature**: _________________

**Approved By**: _________________  
**Date**: _________________  
**Signature**: _________________
