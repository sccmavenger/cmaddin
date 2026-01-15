# 📝 Diagnostic Logging Guide

## Overview

The Zero Trust Migration Journey Add-in includes comprehensive diagnostic logging via the **FileLogger** service to help troubleshoot issues, track operations, and monitor application health. All logs are stored locally on the device and **never transmitted** to external services.

---

## 📍 Log File Location

Logs are written to:
```
%LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs\ZeroTrustMigrationAddin_YYYYMMDD.log
```

**Example:**
```
C:\Users\JohnDoe\AppData\Local\ZeroTrustMigrationAddin\Logs\ZeroTrustMigrationAddin_20260114.log
```

### How to Access Logs Quickly

**Option 1: Windows Run Dialog**
1. Press `Win + R`
2. Type: `%LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs`
3. Press Enter

**Option 2: PowerShell**
```powershell
Start-Process "$env:LOCALAPPDATA\ZeroTrustMigrationAddin\Logs"
```

**Option 3: From Application**
The application includes a "View Logs" button on the Diagnostics tab that opens the log directory automatically.

---

## 📊 Log Levels

The FileLogger service uses **5 log levels** with different purposes:

| Level | Symbol | Purpose | Example Use Cases |
|-------|--------|---------|-------------------|
| **DEBUG** | 🔍 | Detailed diagnostic information | Variable values, API request/response bodies, state transitions |
| **INFO** | ℹ️ | General informational messages | Application startup, feature usage, successful operations |
| **WARNING** | ⚠️ | Unexpected but recoverable issues | Missing optional configuration, deprecated API usage, retry attempts |
| **ERROR** | ❌ | Error conditions that require attention | Failed API calls, file access errors, null reference exceptions |
| **CRITICAL** | 🔥 | Severe errors requiring immediate action | Application crash, data corruption, unrecoverable failures |

---

## 🏷️ Log Prefixes

Logs use **prefixed categories** to organize messages by functional area:

### Common Prefixes

| Prefix | Purpose | Example |
|--------|---------|---------|
| `[APP]` | Application lifecycle | `[APP] Application started` |
| `[GRAPH]` | Microsoft Graph API calls | `[GRAPH] Fetching device enrollment data` |
| `[CONFIGMGR]` | ConfigMgr database queries | `[CONFIGMGR] Connected to SMS Provider` |
| `[AUTH]` | Authentication operations | `[AUTH] User authenticated successfully` |
| `[TELEMETRY]` | Telemetry service operations | `[TELEMETRY] Event: ButtonClicked` |
| `[UPDATE]` | Auto-update mechanism | `[UPDATE] Checking for updates from GitHub` |
| `[AI]` | Azure OpenAI integration | `[AI] Generating enrollment recommendations` |
| `[AGENT]` | Enrollment ReAct Agent | `[AGENT] Analyzing blockers for tenant` |
| `[DIAGNOSTICS]` | System diagnostics checks | `[DIAGNOSTICS] Graph API: Connected` |

### Usage Examples

```
[2026-01-14 09:15:32.123] [INFO    ] [APP] Application started
[2026-01-14 09:15:33.456] [INFO    ] [AUTH] User authenticated as admin@contoso.com
[2026-01-14 09:15:34.789] [INFO    ] [GRAPH] Fetching device enrollment data...
[2026-01-14 09:15:35.012] [DEBUG   ] [GRAPH] Response: 350 devices, 280 enrolled
[2026-01-14 09:15:36.345] [WARNING ] [CONFIGMGR] Site code not found in registry, using default: CM1
[2026-01-14 09:15:37.678] [ERROR   ] [GRAPH] Failed to fetch compliance data: Timeout after 30s
```

---

## 🔄 Log File Rotation

### Automatic Daily Rotation
- **New log file created daily** with date in filename: `ZeroTrustMigrationAddin_20260114.log`
- Each day's logs are written to a separate file
- Previous days' logs are preserved

### Automatic Cleanup
- **Retention policy:** Last 7 days
- `CleanupOldLogs()` method automatically deletes logs older than 7 days
- Cleanup runs when `FileLogger` initializes (on application startup)

### Manual Cleanup
```csharp
FileLogger.Instance.CleanupOldLogs(30); // Keep last 30 days
```

---

## 🛠️ When Each Log Level Is Used

### DEBUG Level
**Purpose:** Detailed diagnostic information for debugging specific issues  
**When to use:**
- Logging variable values during calculations
- Recording API request/response bodies
- Tracking state transitions in complex workflows
- Capturing intermediate results in multi-step processes

**Examples:**
```csharp
FileLogger.Instance.Debug($"[GRAPH] API Response Body: {responseJson}");
FileLogger.Instance.Debug($"[AGENT] Current step: {currentStep}, Iteration: {iteration}");
FileLogger.Instance.Debug($"[CALC] Enrollment velocity: {velocity} devices/week");
```

**Output:**
```
[2026-01-14 10:30:15.123] [DEBUG   ] [GRAPH] API Response Body: {"value":[{"id":"device-123"...
[2026-01-14 10:30:16.456] [DEBUG   ] [AGENT] Current step: AnalyzeBlockers, Iteration: 2
[2026-01-14 10:30:17.789] [DEBUG   ] [CALC] Enrollment velocity: 23.5 devices/week
```

---

### INFO Level
**Purpose:** General informational messages about application operations  
**When to use:**
- Application startup/shutdown
- Feature usage tracking
- Successful API calls
- User actions (button clicks, navigation)
- Configuration changes

**Examples:**
```csharp
FileLogger.Instance.Info("[APP] Application started");
FileLogger.Instance.Info("[GRAPH] Device enrollment loaded: Total=350, Intune=280");
FileLogger.Instance.Info("[UPDATE] No updates available (current version: 3.16.6)");
FileLogger.Instance.Info("[AUTH] User authenticated successfully");
```

**Output:**
```
[2026-01-14 09:00:00.000] [INFO    ] ================================================================================
[2026-01-14 09:00:00.001] [INFO    ] CloudJourney Add-in Started - 2026-01-14 09:00:00
[2026-01-14 09:00:00.002] [INFO    ] Log File: C:\Users\...\ZeroTrustMigrationAddin_20260114.log
[2026-01-14 09:00:00.003] [INFO    ] ================================================================================
[2026-01-14 09:00:01.234] [INFO    ] [APP] Application started
[2026-01-14 09:00:02.567] [INFO    ] [TELEMETRY] Azure Application Insights initialized successfully
```

---

### WARNING Level
**Purpose:** Unexpected but recoverable issues that don't stop execution  
**When to use:**
- Missing optional configuration (will use defaults)
- Deprecated API usage (still works, but should be updated)
- Retry attempts after transient failures
- Non-critical errors (feature disabled but app continues)
- Resource constraints (low memory, slow performance)

**Examples:**
```csharp
FileLogger.Instance.Warning("[CONFIGMGR] Site code not found, using default: CM1");
FileLogger.Instance.Warning("[TELEMETRY] Failed to track event 'ButtonClicked': Network timeout");
FileLogger.Instance.Warning("[UPDATE] Update check failed: Unable to reach GitHub API");
FileLogger.Instance.Warning("[AI] Azure OpenAI not configured - AI features disabled");
```

**Output:**
```
[2026-01-14 10:15:00.123] [WARNING ] [CONFIGMGR] Site code not found in registry, using default: CM1
[2026-01-14 10:15:01.456] [WARNING ] [TELEMETRY] Failed to track event 'RefreshClicked': Timeout
[2026-01-14 10:15:02.789] [WARNING ] [UPDATE] Update check failed: Network unreachable
```

---

### ERROR Level
**Purpose:** Error conditions that prevent a specific operation from completing  
**When to use:**
- Failed API calls (Graph, ConfigMgr)
- File access errors (permissions, not found)
- Database query failures
- Null reference exceptions
- Authentication failures
- Data parsing errors

**Examples:**
```csharp
FileLogger.Instance.Error("[GRAPH] Failed to fetch device enrollment: Unauthorized (401)");
FileLogger.Instance.Error("[CONFIGMGR] Cannot connect to SMS Provider: Access denied");
FileLogger.Instance.Error("[FILE] Failed to write log file: Disk full");
```

**Output:**
```
[2026-01-14 11:00:00.123] [ERROR   ] [GRAPH] Failed to fetch device enrollment: Unauthorized (401)
[2026-01-14 11:00:01.456] [ERROR   ] EXCEPTION in GraphAPI:
[2026-01-14 11:00:01.457] [ERROR   ]   Type: UnauthorizedAccessException
[2026-01-14 11:00:01.458] [ERROR   ]   Message: Access token expired or invalid
[2026-01-14 11:00:01.459] [ERROR   ]   Stack Trace: at ZeroTrustMigrationAddin.Services.GraphDataService...
```

---

### CRITICAL Level
**Purpose:** Severe errors requiring immediate attention that may cause application crash  
**When to use:**
- Unrecoverable application errors
- Data corruption detected
- Critical resource unavailable (out of memory, disk full)
- Security violations (tampered files, unauthorized access)
- Catastrophic failures (database corruption, system state invalid)

**Examples:**
```csharp
FileLogger.Instance.Critical("[APP] Unhandled exception in main thread - application will exit");
FileLogger.Instance.Critical("[DB] Database corruption detected - cannot read configuration");
FileLogger.Instance.Critical("[SECURITY] Certificate validation failed - potential man-in-the-middle attack");
```

**Output:**
```
[2026-01-14 12:00:00.000] [CRITICAL] [APP] Unhandled exception in DispatcherUnhandledException
[2026-01-14 12:00:00.001] [CRITICAL] EXCEPTION in MainWindow:
[2026-01-14 12:00:00.002] [CRITICAL]   Type: OutOfMemoryException
[2026-01-14 12:00:00.003] [CRITICAL]   Message: Insufficient memory to continue the execution
[2026-01-14 12:00:00.004] [CRITICAL]   Stack Trace: at System.Windows.Media.Imaging...
```

---

## 📋 Exception Logging

The `LogException()` method provides structured exception logging with full details:

### Usage
```csharp
try
{
    // Risky operation
    var result = await _graphService.GetDevicesAsync();
}
catch (Exception ex)
{
    FileLogger.Instance.LogException(ex, "GraphDataService.GetDevicesAsync");
}
```

### Output Format
```
[2026-01-14 14:30:00.123] [ERROR   ] EXCEPTION in GraphDataService.GetDevicesAsync:
[2026-01-14 14:30:00.124] [ERROR   ]   Type: HttpRequestException
[2026-01-14 14:30:00.125] [ERROR   ]   Message: Response status code does not indicate success: 500 (Internal Server Error)
[2026-01-14 14:30:00.126] [ERROR   ]   Stack Trace: 
   at System.Net.Http.HttpResponseMessage.EnsureSuccessStatusCode()
   at ZeroTrustMigrationAddin.Services.GraphDataService.GetDevicesAsync() in C:\...\GraphDataService.cs:line 142
   at ZeroTrustMigrationAddin.ViewModels.DashboardViewModel.LoadRealDataAsync() in C:\...\DashboardViewModel.cs:line 1882
[2026-01-14 14:30:00.127] [ERROR   ]   Inner Exception: SocketException
[2026-01-14 14:30:00.128] [ERROR   ]   Inner Message: No such host is known
```

---

## 🔍 Log Analysis Examples

### Finding All Errors
```powershell
Get-Content $env:LOCALAPPDATA\ZeroTrustMigrationAddin\Logs\ZeroTrustMigrationAddin_*.log | Select-String "\[ERROR"
```

### Finding Graph API Issues
```powershell
Get-Content $env:LOCALAPPDATA\ZeroTrustMigrationAddin\Logs\ZeroTrustMigrationAddin_*.log | Select-String "\[GRAPH\]"
```

### Finding Telemetry Events
```powershell
Get-Content $env:LOCALAPPDATA\ZeroTrustMigrationAddin\Logs\ZeroTrustMigrationAddin_*.log | Select-String "\[TELEMETRY\]"
```

### Last 50 Lines (Real-time Monitoring)
```powershell
Get-Content $env:LOCALAPPDATA\ZeroTrustMigrationAddin\Logs\ZeroTrustMigrationAddin_*.log -Tail 50 -Wait
```

### Count Log Entries by Level
```powershell
$log = Get-Content $env:LOCALAPPDATA\ZeroTrustMigrationAddin\Logs\ZeroTrustMigrationAddin_*.log
[PSCustomObject]@{
    DEBUG = ($log | Select-String "\[DEBUG" | Measure-Object).Count
    INFO = ($log | Select-String "\[INFO" | Measure-Object).Count
    WARNING = ($log | Select-String "\[WARNING" | Measure-Object).Count
    ERROR = ($log | Select-String "\[ERROR" | Measure-Object).Count
    CRITICAL = ($log | Select-String "\[CRITICAL" | Measure-Object).Count
}
```

---

## ⚙️ Configuration

### Log File Size Monitoring
```csharp
double sizeMB = FileLogger.Instance.GetLogFileSizeMB();
if (sizeMB > 100) 
{
    FileLogger.Instance.Warning($"[APP] Log file is large: {sizeMB:F2}MB");
}
```

### Custom Retention Policy
```csharp
// Keep last 30 days instead of default 7
FileLogger.Instance.CleanupOldLogs(30);
```

### Log Separator for Sections
```csharp
FileLogger.Instance.LogSeparator();
// Output: [INFO    ] --------------------------------------------------------------------------------
```

---

## 🛡️ Privacy & Security

### What's Logged
✅ **Safe to log:**
- Event names ("ButtonClicked", "DataRefreshed")
- Numeric metrics (device count, API latency)
- Operation results (success/failure)
- Application state (authenticated, loading, ready)
- Error types and codes
- API endpoint names (without parameters)

❌ **Never logged (PII):**
- Device names
- Usernames
- Email addresses
- Tenant IDs
- IP addresses
- Access tokens
- Passwords or secrets
- User-entered text

### Example: Safe Logging
```csharp
// ✅ GOOD - No PII
FileLogger.Instance.Info($"[GRAPH] Loaded {deviceCount} devices");

// ❌ BAD - Contains PII
FileLogger.Instance.Info($"[GRAPH] Loaded device: {deviceName}");

// ✅ GOOD - Sanitized
FileLogger.Instance.Info($"[GRAPH] Loaded device: <redacted>");
```

---

## 🚨 Troubleshooting Common Issues

### Issue: No log file created
**Possible causes:**
- Insufficient permissions to `%LOCALAPPDATA%`
- Disk full
- Antivirus blocking file creation

**Solution:**
1. Check folder permissions: `%LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs`
2. Verify disk space: `Get-PSDrive C | Select-Object Free`
3. Temporarily disable antivirus and test

---

### Issue: Log file too large
**Possible causes:**
- DEBUG logging enabled in production
- Long-running session with high activity
- Log cleanup not running

**Solution:**
```powershell
# Check log file size
Get-ChildItem "$env:LOCALAPPDATA\ZeroTrustMigrationAddin\Logs" | Select-Object Name, @{N='SizeMB';E={[math]::Round($_.Length/1MB, 2)}}

# Manual cleanup (delete old logs)
Get-ChildItem "$env:LOCALAPPDATA\ZeroTrustMigrationAddin\Logs" -Filter "ZeroTrustMigrationAddin_*.log" |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-7) } |
    Remove-Item -Force
```

---

### Issue: Missing log entries
**Possible causes:**
- Application crashed before flushing logs
- Buffering delay (logs not written immediately)
- Looking at wrong day's log file

**Solution:**
- Check latest log file: `Get-ChildItem "$env:LOCALAPPDATA\ZeroTrustMigrationAddin\Logs" | Sort-Object LastWriteTime -Descending | Select-Object -First 1`
- Logs are written immediately (no buffering), so check for crash/exception
- Check previous day's log if issue occurred around midnight

---

## 📚 Additional Resources

- **Telemetry Guide**: See [TELEMETRY.md](TELEMETRY.md) for Azure Application Insights integration
- **Privacy Policy**: See [PRIVACY.md](PRIVACY.md) for data handling policies
- **Troubleshooting**: See [README.md](README.md) for common issues and solutions

---

## 📞 Support

If logs show errors you cannot resolve:
1. Collect the relevant log file from `%LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs`
2. **Sanitize any PII** (device names, usernames) before sharing
3. Open an issue on GitHub with:
   - Application version (from About dialog)
   - Relevant log excerpts (with timestamps)
   - Steps to reproduce
   - Expected vs actual behavior
