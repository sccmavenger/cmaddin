# Azure Application Insights - Telemetry Queries

**Resource:** `appi-cloudjourney-addin`  
**Connection:** East US  
**Use these queries in:** Azure Portal → Application Insights → Logs

---

## 📊 Field Testing Dashboards

### 1. Active Users Summary (Last 7 Days)

```kql
customEvents
| where timestamp > ago(7d)
| where name == "AppStarted"
| summarize 
    TotalLaunches = count(),
    UniqueUsers = dcount(user_Id),
    UniqueSessions = dcount(session_Id)
| extend AvgLaunchesPerUser = TotalLaunches / UniqueUsers
```

**What it shows:**
- How many times the app was launched
- How many unique test users are actively using it
- Average launches per user

---

### 2. Version Adoption Tracking

```kql
customEvents
| where timestamp > ago(7d)
| where name == "AppStarted"
| extend Version = tostring(customDimensions.Version)
| summarize 
    Launches = count(),
    Users = dcount(user_Id),
    LastSeen = max(timestamp)
    by Version
| order by LastSeen desc
```

**What it shows:**
- Which versions users are running
- How fast they're updating to v3.16.5
- Who might be stuck on old versions

---

### 3. Daily Active Users (DAU) Trend

```kql
customEvents
| where timestamp > ago(30d)
| where name == "AppStarted"
| summarize UniqueUsers = dcount(user_Id) by bin(timestamp, 1d)
| render timechart
```

**What it shows:**
- Daily user engagement over last 30 days
- Usage trends (growing/declining)
- Visual chart of adoption

---

### 4. User Activity Details

```kql
customEvents
| where timestamp > ago(7d)
| where name == "AppStarted"
| extend 
    Version = tostring(customDimensions.Version),
    OS = tostring(customDimensions.OS),
    CLR = tostring(customDimensions.CLR)
| summarize 
    FirstUsed = min(timestamp),
    LastUsed = max(timestamp),
    TotalSessions = count(),
    CurrentVersion = arg_max(timestamp, Version)
    by user_Id
| extend DaysSinceFirstUse = datetime_diff('day', now(), FirstUsed)
| project user_Id, FirstUsed, LastUsed, TotalSessions, CurrentVersion, DaysSinceFirstUse
| order by LastUsed desc
```

**What it shows:**
- Individual user adoption patterns
- When each user first tried the tool
- How frequently they're using it
- Which version they're currently on

---

### 5. Error & Exception Tracking

```kql
exceptions
| where timestamp > ago(7d)
| extend Version = tostring(customDimensions.Version)
| summarize 
    ErrorCount = count(),
    AffectedUsers = dcount(user_Id),
    FirstOccurrence = min(timestamp),
    LastOccurrence = max(timestamp)
    by type, outerMessage, Version
| order by ErrorCount desc
```

**What it shows:**
- What errors users are hitting
- How many users affected by each error
- Which version has the most errors
- Error frequency

---

### 6. Session Duration (App Open to Close)

```kql
customEvents
| where timestamp > ago(7d)
| where name in ("AppStarted", "AppExited")
| extend Version = tostring(customDimensions.Version)
| order by session_Id, timestamp asc
| serialize
| extend NextEvent = next(name), NextTimestamp = next(timestamp), NextSession = next(session_Id)
| where name == "AppStarted" and NextEvent == "AppExited" and session_Id == NextSession
| extend Duration = datetime_diff('minute', NextTimestamp, timestamp)
| summarize 
    AvgDuration = avg(Duration),
    MinDuration = min(Duration),
    MaxDuration = max(Duration),
    Sessions = count()
    by Version
```

**What it shows:**
- How long users keep the app open
- Average session length by version
- Whether users are engaging deeply or just opening/closing

---

### 7. Auto-Update Success Rate

```kql
customEvents
| where timestamp > ago(7d)
| where name == "AppStarted"
| extend Version = tostring(customDimensions.Version)
| summarize arg_max(timestamp, Version) by user_Id, bin(timestamp, 1d)
| summarize OnLatest = countif(Version == "3.16.5.0"), Total = count()
| extend UpdateRate = round(100.0 * OnLatest / Total, 2)
| project UpdateRate, UsersOnLatest = OnLatest, TotalUsers = Total
```

**What it shows:**
- What % of users are on the latest version (3.16.5)
- How effective auto-update is
- Who needs help updating

---

### 8. Peak Usage Hours

```kql
customEvents
| where timestamp > ago(7d)
| where name == "AppStarted"
| extend Hour = hourofday(timestamp)
| summarize Launches = count() by Hour
| order by Hour asc
| render columnchart
```

**What it shows:**
- When users typically launch the app
- Best time to release updates (low usage hours)
- Time zone distribution of users

---

### 9. New vs Returning Users

```kql
let FirstUse = customEvents
| where name == "AppStarted"
| summarize FirstSeen = min(timestamp) by user_Id;
customEvents
| where timestamp > ago(7d)
| where name == "AppStarted"
| join kind=inner (FirstUse) on user_Id
| extend IsNew = iff(FirstSeen > ago(7d), "New", "Returning")
| summarize Users = dcount(user_Id) by IsNew
```

**What it shows:**
- How many new users vs returning users
- Field test growth rate
- User retention

---

### 10. Crash Detection (Incomplete Sessions)

```kql
let Started = customEvents
| where timestamp > ago(7d)
| where name == "AppStarted"
| project session_Id, user_Id, StartTime = timestamp;
let Exited = customEvents
| where timestamp > ago(7d)
| where name == "AppExited"
| project session_Id, ExitTime = timestamp;
Started
| join kind=leftanti Exited on session_Id
| summarize CrashCount = count(), AffectedUsers = dcount(user_Id)
| extend Message = "Sessions started but never exited (potential crashes)"
```

**What it shows:**
- Sessions that started but never properly closed
- Possible crashes or forced closures
- Users affected by stability issues

---

## 🎯 Quick Health Checks

### Is Anyone Using It? (Last 24 Hours)

```kql
customEvents
| where timestamp > ago(24h)
| where name == "AppStarted"
| summarize count()
```

---

### Latest Activity

```kql
customEvents
| where name in ("AppStarted", "AppExited")
| extend Version = tostring(customDimensions.Version)
| top 20 by timestamp desc
| project timestamp, name, Version, user_Id, session_Id
```

---

### Error Alerts (Last Hour)

```kql
exceptions
| where timestamp > ago(1h)
| project timestamp, type, outerMessage, user_Id
| order by timestamp desc
```

---

## 📈 Executive Summary (Copy/Paste for Reports)

```kql
let TimeRange = 7d;
let AppStarts = customEvents | where timestamp > ago(TimeRange) and name == "AppStarted";
let AppExits = customEvents | where timestamp > ago(TimeRange) and name == "AppExited";
let Errors = exceptions | where timestamp > ago(TimeRange);
print 
    Period = strcat(TimeRange),
    TotalLaunches = toscalar(AppStarts | count),
    UniqueUsers = toscalar(AppStarts | dcount(user_Id)),
    UniqueSessions = toscalar(AppStarts | dcount(session_Id)),
    UsersOnLatestVersion = toscalar(AppStarts | extend Version = tostring(customDimensions.Version) | where Version == "3.16.5.0" | dcount(user_Id)),
    TotalErrors = toscalar(Errors | count),
    UsersWithErrors = toscalar(Errors | dcount(user_Id)),
    AvgLaunchesPerUser = round(1.0 * toscalar(AppStarts | count) / toscalar(AppStarts | dcount(user_Id)), 2)
```

**Copy this result into your weekly field test reports!**

---

## 🔔 Set Up Alerts

To get notified when issues occur, create alerts in Azure Portal:

### Alert 1: New Errors
- **Condition:** `exceptions | count > 0`
- **Frequency:** Every 15 minutes
- **Action:** Email or Teams notification

### Alert 2: Low Adoption
- **Condition:** `customEvents | where name == "AppStarted" | where timestamp > ago(24h) | count < 5`
- **Frequency:** Daily at 9 AM
- **Action:** Email reminder to check user engagement

### Alert 3: Crash Rate
- **Condition:** Incomplete sessions > 20% of total sessions
- **Frequency:** Hourly
- **Action:** High priority notification

---

## 💡 Pro Tips

1. **Bookmark these queries** in Azure Portal (click the star icon after running)
2. **Pin charts to dashboard** - Create a custom dashboard with key metrics
3. **Export to Excel** - Click "Export" button to share with team
4. **Schedule reports** - Set up automated email reports with Power BI
5. **Time zone** - All timestamps are in UTC, adjust for your local time

---

## 📞 Support

If telemetry stops appearing:
1. Check local logs: `%LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs\`
2. Look for `[TELEMETRY]` prefixed messages
3. Verify app version is 3.16.5 or higher
4. Check Azure connection string is valid

---

**Last Updated:** January 14, 2026  
**For:** Zero Trust Migration Journey Add-in Field Testing (v3.16.5+)
