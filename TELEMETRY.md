# 📊 Telemetry Guide

## Overview

The Zero Trust Migration Journey Add-in includes **Azure Application Insights telemetry** to help improve the tool through anonymous usage analytics. Telemetry is **privacy-safe**, **anonymous**, and **PII-sanitized** by design.

**Key Principles:**
- ✅ **Anonymous** - No personally identifiable information (PII)
- ✅ **Transparent** - All telemetry also logged locally in `FileLogger`
- ✅ **Optional** - Application continues if telemetry fails to initialize
- ✅ **Privacy-first** - Aggressive PII sanitization before transmission

---

## 🎯 Why Telemetry Exists

### Purpose
Telemetry helps answer critical questions to improve the tool:

1. **Feature Usage**
   - Which features are most/least used?
   - Are users finding the enrollment agent helpful?
   - Do users prefer Graph API or ConfigMgr data sources?

2. **Performance & Reliability**
   - How often do API calls fail?
   - What's the average data load time?
   - Are there memory leaks or crashes?

3. **Error Patterns**
   - Which exceptions occur most frequently?
   - Are there common environmental issues (proxy, firewall)?
   - What Graph API calls fail most often?

4. **User Experience**
   - How long do users keep the dashboard open?
   - Do users complete setup wizards or abandon them?
   - Which tabs are most frequently accessed?

### Benefits for Users
- **Faster bug fixes** - Error patterns identified quickly
- **Better features** - Development prioritized based on actual usage
- **Improved performance** - Bottlenecks detected and optimized
- **Smoother updates** - Deployment issues caught early

---

## 📡 What Telemetry Tracks

### Events (Feature Usage)
**Format:** `EventName + Properties + Metrics`

**Examples:**
```csharp
AzureTelemetryService.Instance.TrackEvent("AppStarted", new Dictionary<string, string>
{
    { "AppVersion", "3.16.6" },
    { "OSVersion", "Windows 11 22H2" }
});

AzureTelemetryService.Instance.TrackEvent("DataRefreshed", new Dictionary<string, string>
{
    { "DataSource", "Graph" },
    { "TabName", "Overview" }
});
```

**What's tracked:**
- Application lifecycle (start, exit, crash)
- Button clicks (Refresh, Configure, View Logs)
- Tab navigation (Overview, Workloads, Devices)
- Data source selection (Graph API, ConfigMgr, Mock)
- Feature usage (AI agent, compliance dashboard, enrollment insights)
- **Update checks** (authentication method, success/failure, errors)
- **Update detection** (version changes, delta size, bandwidth savings)
- **Update application** (success/failure, stage of failure)

**Update Check Events:**
```csharp
// When app checks GitHub for updates
AzureTelemetryService.Instance.TrackEvent("UpdateCheckStarted", new Dictionary<string, string>
{
    { "AuthenticationMethod", "Authenticated" },  // or "Anonymous"
    { "Repository", "sccmavenger/cmaddin" },
    { "HasGitHubToken", "True" }  // Critical for tracking private repo access
});

// When update is found
AzureTelemetryService.Instance.TrackEvent("UpdateCheckSuccess", new Dictionary<string, string>
{
    { "LatestVersion", "v3.16.7" },
    { "AssetCount", "2" },
    { "PublishedDate", "2026-01-14" }
});

// When update check fails
AzureTelemetryService.Instance.TrackEvent("UpdateCheckFailed", new Dictionary<string, string>
{
    { "ErrorType", "NotFoundException" },
    { "IsAuthenticated", "False" },
    { "Message", "Repository not found (likely private repo without token)" }
});

// When update is detected and will be downloaded
AzureTelemetryService.Instance.TrackEvent("UpdateDetected", new Dictionary<string, string>
{
    { "CurrentVersion", "3.16.6" },
    { "LatestVersion", "3.16.7" },
    { "ChangedFilesCount", "5" },
    { "DeltaSize", "2338816" },  // bytes
    { "BandwidthSavings", "97.5%" }
});

// When update is successfully applied
AzureTelemetryService.Instance.TrackEvent("UpdateApplied", new Dictionary<string, string>
{
    { "FromVersion", "3.16.6" },
    { "ToVersion", "3.16.7" },
    { "ChangedFiles", "5" }
});
```

**What's NOT tracked:**
- ❌ Specific tenant data (device names, user counts)
- ❌ User identity (who clicked what)
- ❌ Business data (enrollment numbers, compliance scores)
- ❌ Timestamps beyond aggregated statistics
- ❌ GitHub token values (only whether token exists)

---

### Metrics (Performance & Health)
**Format:** `MetricName + Value + Properties`

**Examples:**
```csharp
AzureTelemetryService.Instance.TrackMetric("GraphAPILatency", 1250.0, new Dictionary<string, string>
{
    { "Endpoint", "deviceManagement/managedDevices" }
});

AzureTelemetryService.Instance.TrackMetric("DeviceCount", 350.0);
```

**What's tracked:**
- API call latency (Graph, ConfigMgr)
- Memory usage
- Cache hit rates
- Data load times
- Dashboard render times

**What's NOT tracked:**
- ❌ Exact device counts (aggregated in 50-device buckets)
- ❌ Tenant-specific metrics (compliance percentages)
- ❌ User-specific metrics (individual session duration)

---

### Exceptions (Error Tracking)
**Format:** `ExceptionType + SanitizedMessage + Properties`

**Examples:**
```csharp
try
{
    var devices = await _graphService.GetDevicesAsync();
}
catch (Exception ex)
{
    AzureTelemetryService.Instance.TrackException(ex, new Dictionary<string, string>
    {
        { "Context", "GraphDataService.GetDevicesAsync" },
        { "Authenticated", "True" }
    });
}
```

**What's tracked:**
- Exception type (`HttpRequestException`, `UnauthorizedAccessException`)
- **Sanitized** exception message (PII removed)
- Stack trace (method names, line numbers)
- Context (which feature/service failed)

**What's NOT tracked:**
- ❌ Actual error messages with PII (sanitized first)
- ❌ User input that caused errors
- ❌ File paths with usernames
- ❌ Connection strings or credentials

---

### Dependencies (External API Calls)
**Format:** `DependencyType + Name + Duration + Success`

**Examples:**
```csharp
AzureTelemetryService.Instance.TrackDependency(
    "HTTP", 
    "graph.microsoft.com", 
    "/v1.0/deviceManagement/managedDevices", 
    startTime, 
    duration, 
    success: true
);
```

**What's tracked:**
- API endpoint names (Graph, ConfigMgr)
- Call duration (milliseconds)
- Success/failure status
- HTTP status codes (200, 401, 500)

**What's NOT tracked:**
- ❌ Request/response bodies
- ❌ Authentication tokens
- ❌ Query parameters with tenant-specific data

---

### Page Views (Navigation)
**Format:** `PageName + Properties`

**Examples:**
```csharp
AzureTelemetryService.Instance.TrackPageView("OverviewTab", new Dictionary<string, string>
{
    { "DataSource", "Graph" }
});
```

**What's tracked:**
- Tab navigation (Overview, Workloads, Devices, Settings)
- Window opens (Diagnostics, About, Configure)
- View duration (time on tab)

**What's NOT tracked:**
- ❌ What data was displayed on the page
- ❌ User interactions within the page (beyond navigation)

---

## 🛡️ Privacy & Security

### Anonymous User ID Generation

**How it works:**
1. Read Windows **Machine GUID** from registry: `HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid`
2. Hash with **SHA-256**
3. Take first 22 characters: `Base64(SHA256(MachineGuid))`

**Example:**
```csharp
Machine GUID: a1b2c3d4-e5f6-7890-abcd-ef1234567890
SHA-256 Hash: 8f7e6d5c4b3a2190...
Anonymous ID: 8f7e6d5c4b3a219012Xy
```

**Properties:**
- ✅ **Consistent** - Same machine always gets same ID
- ✅ **Anonymous** - Cannot reverse to machine name or user
- ✅ **Unique** - Extremely unlikely collision across machines
- ✅ **Persistent** - Survives app reinstalls (tied to machine, not app)

**Fallback:** If Machine GUID unavailable (rare), generates random GUID

---

### PII Sanitization Engine

**Before sending any data to Azure, all strings are sanitized:**

| PII Type | Pattern | Replacement |
|----------|---------|-------------|
| **UNC Paths** | `\\server\share\file.txt` | `[UNC_PATH]` |
| **Local Paths** | `C:\Users\JohnDoe\Documents` | `C:\Users\[USER]` |
| **Email Addresses** | `john.doe@contoso.com` | `[EMAIL]` |
| **IP Addresses** | `192.168.1.100` | `[IP]` |
| **GUIDs** | `a1b2c3d4-e5f6-7890-abcd-ef12` | `[GUID]` |
| **Domain\User** | `CONTOSO\john.doe` | `[DOMAIN\USER]` |

**Example Sanitization:**
```csharp
// Before sanitization:
"Failed to connect to \\contoso-dc01\SYSVOL$ as CONTOSO\admin from 10.0.0.15"

// After sanitization:
"Failed to connect to [UNC_PATH] as [DOMAIN\USER] from [IP]"
```

**Code:**
```csharp
private string SanitizeString(string input)
{
    var sanitized = input;
    sanitized = Regex.Replace(sanitized, @"\\\\[\w\-\.]+\\[\w\-\.\$]+", "[UNC_PATH]");
    sanitized = Regex.Replace(sanitized, @"[A-Z]:\\Users\\[\w\-\.]+", "C:\\Users\\[USER]");
    sanitized = Regex.Replace(sanitized, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", "[EMAIL]");
    sanitized = Regex.Replace(sanitized, @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b", "[IP]");
    sanitized = Regex.Replace(sanitized, @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b", "[GUID]");
    sanitized = Regex.Replace(sanitized, @"\b[A-Z0-9\-]+\\[\w\-\.]+\b", "[DOMAIN\\USER]");
    return sanitized;
}
```

---

### Telemetry vs. Diagnostic Logging

**Both systems log the same events, but with different purposes:**

| Aspect | Diagnostic Logging (FileLogger) | Telemetry (AzureTelemetryService) |
|--------|----------------------------------|-------------------------------------|
| **Location** | Local device (`%LOCALAPPDATA%`) | Azure Application Insights |
| **Purpose** | Troubleshooting individual issues | Aggregate usage patterns |
| **Audience** | End users, support teams | Developers, product managers |
| **PII Handling** | ⚠️ May contain PII (stays local) | ✅ PII-sanitized (sent to cloud) |
| **Retention** | 7 days (configurable) | 90 days (Azure default) |
| **Access** | User has full access | Only developers have access |
| **Format** | Human-readable text | Structured JSON |

**Example - Same event logged to both:**

**FileLogger (local):**
```
[2026-01-14 10:30:15.123] [INFO] [GRAPH] Fetching device enrollment data for tenant: contoso.onmicrosoft.com
[2026-01-14 10:30:16.456] [INFO] [GRAPH] Response: 350 devices, 280 enrolled, 70 ConfigMgr-only
```

**AzureTelemetryService (Azure):**
```json
{
  "name": "GraphAPICall",
  "properties": {
    "Endpoint": "deviceManagement/managedDevices",
    "TenantId": "[GUID]",
    "AppVersion": "3.16.6"
  },
  "metrics": {
    "Latency": 1234.5,
    "DeviceCount": 350
  }
}
```

---

## 🔍 Transparency: What Gets Sent

### Startup Event
**When:** Application starts  
**What's sent:**
```json
{
  "eventName": "AppStarted",
  "properties": {
    "AppVersion": "3.16.6",
    "OSVersion": "Windows 11 22H2",
    "DotNetVersion": "8.0.1"
  },
  "userId": "8f7e6d5c4b3a219012Xy",
  "sessionId": "new-guid-per-session"
}
```

**What's NOT sent:**
- ❌ Computer name
- ❌ Logged-in username
- ❌ Domain membership
- ❌ Installation path

---

### Data Refresh Event
**When:** User clicks "Refresh Data" button  
**What's sent:**
```json
{
  "eventName": "DataRefreshed",
  "properties": {
    "DataSource": "Graph",
    "TabName": "Overview"
  },
  "metrics": {
    "LoadTimeMs": 2345.6
  }
}
```

**What's NOT sent:**
- ❌ Actual device count
- ❌ Enrollment percentages
- ❌ Tenant name or ID

---

### Exception Event
**When:** Unhandled exception occurs  
**What's sent:**
```json
{
  "eventName": "Exception",
  "exceptionType": "HttpRequestException",
  "message": "Failed to connect to [IP]: Connection timeout",
  "properties": {
    "Context": "GraphDataService.GetDevicesAsync",
    "Authenticated": "True"
  },
  "stackTrace": "at ZeroTrustMigrationAddin.Services.GraphDataService.GetDevicesAsync()..."
}
```

**What's NOT sent:**
- ❌ Original error message with PII (sanitized first)
- ❌ Request/response data
- ❌ Connection strings

---

## ⚙️ Configuration

### Disabling Telemetry
**Option 1: Initialization Failure**  
If telemetry service fails to initialize (network issue, Azure unavailable), the application continues normally:
```
[2026-01-14 09:00:02.123] [WARNING] [TELEMETRY] Failed to initialize: No connection to Azure
[2026-01-14 09:00:02.124] [INFO   ] [TELEMETRY] Application will continue without telemetry
```

**Option 2: Code Modification**  
Set `_isEnabled = false` in `AzureTelemetryService` constructor

**Option 3: Network Blocking**  
Block outbound connections to:
- `eastus.in.applicationinsights.azure.com`
- `eastus.livediagnostics.monitor.azure.com`

**Note:** Telemetry is designed to fail gracefully. If Azure is unreachable, the app works normally without telemetry.

---

### Connection String
Telemetry sends data to Azure Application Insights:

```csharp
private const string ConnectionString = 
    "InstrumentationKey=30d5a38c-0d53-44f8-b26b-8b83d89b57b3;" +
    "IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;" +
    "LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;" +
    "ApplicationId=2aef4b56-7293-40e1-aaa5-445d736beb1c";
```

**This is public information** - the instrumentation key is not a secret and is intentionally embedded in the application.

---

## 📊 Viewing Your Own Telemetry

**All telemetry is also logged locally** with `[TELEMETRY]` prefix:

```powershell
# View all telemetry events sent today
Get-Content "$env:LOCALAPPDATA\ZeroTrustMigrationAddin\Logs\ZeroTrustMigrationAddin_*.log" | Select-String "\[TELEMETRY\]"
```

**Example output:**
```
[2026-01-14 09:00:02.123] [INFO   ] [TELEMETRY] Azure Application Insights initialized successfully
[2026-01-14 09:00:02.124] [INFO   ] [TELEMETRY] Anonymous User ID: 8f7e6d5c4b3a219012Xy
[2026-01-14 09:00:02.125] [INFO   ] [TELEMETRY] Session ID: a1b2c3d4-e5f6-7890-abcd-ef1234567890
[2026-01-14 09:00:03.456] [DEBUG  ] [TELEMETRY] Event: AppStarted
[2026-01-14 09:00:15.789] [DEBUG  ] [TELEMETRY] Event: DataRefreshed
[2026-01-14 09:00:30.012] [DEBUG  ] [TELEMETRY] Metric: GraphAPILatency = 1234.5
```

**This transparency ensures you can audit exactly what's being sent.**

---

## 🚨 What Telemetry Is NOT

### ❌ NOT User Tracking
- We don't track **who** you are
- We don't track **what tenant** you're managing
- We don't track **specific devices** or **users**
- We don't correlate telemetry across sessions beyond the anonymous ID

### ❌ NOT Business Intelligence
- We don't collect your enrollment numbers
- We don't collect your compliance scores
- We don't collect your device inventory
- We don't collect your workload configurations

### ❌ NOT Surveillance
- We don't track keystrokes or screenshots
- We don't monitor clipboard or file access
- We don't record session duration per tenant
- We don't track what data you're viewing

### ✅ IS Aggregate Usage Analytics
- How many users click "Refresh" per session? (average)
- How often does Graph API return 401 errors? (percentage)
- What's the median dashboard load time? (milliseconds)
- Which features are rarely used? (counts)

---

## 📚 Additional Resources

- **Diagnostic Logging**: See [DIAGNOSTIC_LOGGING.md](DIAGNOSTIC_LOGGING.md) for local logging details
- **Privacy Policy**: See [PRIVACY.md](PRIVACY.md) for complete data handling policies
- **Source Code**: Review `Services\AzureTelemetryService.cs` for implementation details

---

## 🔒 Privacy Commitment

**We will never:**
- ❌ Sell or share telemetry data with third parties
- ❌ Use telemetry to identify individual users or tenants
- ❌ Collect PII without explicit consent
- ❌ Change telemetry scope without updating this document

**We will always:**
- ✅ Sanitize all PII before transmission
- ✅ Log telemetry operations locally for transparency
- ✅ Document what's collected in this guide
- ✅ Respect your privacy and security

---

## 📞 Questions or Concerns?

If you have questions about telemetry:
1. Review source code: `Services\AzureTelemetryService.cs`
2. Check local logs: `%LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs` (all telemetry operations logged with `[TELEMETRY]` prefix)
3. Open an issue on GitHub with your concern
4. Contact the maintainer with privacy questions

**We take privacy seriously and welcome scrutiny of our telemetry practices.**
