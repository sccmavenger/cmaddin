# Cloud Journey Add-in - Power BI Telemetry Dashboard

## 📊 Overview

This Power BI dashboard provides comprehensive insights into the Cloud Journey Add-in deployment, usage patterns, and health metrics using Azure Application Insights telemetry data.

---

## 🚀 Quick Setup (NEW - Use Template!)

### Option 1: Generate Template (Easiest)
```powershell
cd powerbi
.\Create-PowerBITemplate.ps1
```
This creates `CloudJourneyAddin-Telemetry.pbit` - double-click to open in Power BI Desktop!

### Option 2: Power BI Desktop Project
If you have Power BI Desktop with "Power BI Projects" preview enabled:
1. Open Power BI Desktop → File → Open
2. Browse to `powerbi\CloudJourneyAddin-Telemetry.pbip`

---

## 🔧 Manual Setup

### Prerequisites
- Power BI Desktop (latest version)
- Azure Application Insights API access
- Application ID and API Key from your Application Insights resource

### Step 1: Get Your Credentials

1. Go to **Azure Portal** → **Application Insights** → `appi-cloudjourney-addin`
2. Navigate to **Configure** → **API Access**
3. Copy your **Application ID**
4. Click **Create API Key** → Select "Read telemetry" → Copy the key

### Step 2: Connect Power BI

1. Open **Power BI Desktop**
2. Click **Get Data** → **Azure** → **Azure Application Insights**
3. Enter your **Application ID**
4. Choose **Advanced** mode
5. Paste queries from `CloudJourneyAddin-Telemetry-Dashboard.pq`

---

## 📈 Dashboard Pages

### Page 1: Executive Summary
**KPIs at a glance**

| Metric | Description |
|--------|-------------|
| Total Launches (7d) | App launch count in last 7 days |
| Unique Users (7d) | Distinct users in last 7 days |
| % on Latest Version | Adoption rate of current release |
| Error Rate | Errors per 100 launches |
| Avg Session Duration | Time users spend in app |

**Visuals:**
- 📊 KPI Cards (5 across top)
- 📈 Daily Active Users trend line
- 🥧 Version distribution pie chart
- 📉 Error trend area chart

---

### Page 2: User Adoption

| Visual | Description |
|--------|-------------|
| DAU Trend | Daily active users over 90 days |
| WAU Trend | Weekly active users over 90 days |
| New vs Returning | First-time vs repeat users |
| User Cohorts | When users first started using the app |
| Version Adoption | How quickly users update |

**Key Insights:**
- User growth rate
- Retention patterns
- Update velocity

---

### Page 3: Version Analytics

| Visual | Description |
|--------|-------------|
| Version Distribution | Users per version (pie/donut) |
| Version Timeline | Version adoption over time |
| Update Success Rate | % of users who auto-update successfully |
| Update Failures | Breakdown of why updates fail |
| Stragglers List | Users still on old versions |

**Key Insights:**
- Identify users stuck on old versions
- Track auto-update effectiveness
- Prioritize version deprecation

---

### Page 4: Feature Usage

| Visual | Description |
|--------|-------------|
| Tab Navigation Heatmap | Which tabs users visit most |
| Feature Usage Bar Chart | Event counts by feature |
| Data Source Preference | Graph API vs ConfigMgr vs Mock |
| Button Clicks | Which actions users take |
| Time-of-Day Usage | Peak usage hours heatmap |

**Key Insights:**
- Most valuable features
- Underutilized capabilities
- User workflow patterns

---

### Page 5: Error & Health

| Visual | Description |
|--------|-------------|
| Error Trend | Daily errors over 30 days |
| Error Types | Breakdown by exception type |
| Crash Detection | Incomplete sessions (potential crashes) |
| Affected Users | Users experiencing errors |
| Error Details Table | Drilldown to specific errors |

**Key Insights:**
- Stability trends
- Critical bugs to fix
- User impact of issues

---

### Page 6: Performance

| Visual | Description |
|--------|-------------|
| Session Duration | Average time in app |
| Session Duration by Version | Performance regression detection |
| API Latency | Graph/ConfigMgr response times |
| Load Times | Dashboard rendering speed |

**Key Insights:**
- Performance regressions
- User engagement depth
- API health

---

## 📋 KQL Queries Reference

### 1. Daily Active Users
```kql
customEvents
| where timestamp > ago(90d)
| where name == "AppStarted"
| summarize DAU = dcount(user_Id) by bin(timestamp, 1d)
| render timechart
```

### 2. Version Distribution
```kql
customEvents
| where timestamp > ago(7d)
| where name == "AppStarted"
| extend Version = tostring(customDimensions.Version)
| summarize Users = dcount(user_Id) by Version
| render piechart
```

### 3. Auto-Update Success Rate
```kql
customEvents
| where timestamp > ago(7d)
| where name in ("UpdateCheckSuccess", "UpdateCheckFailed", "UpdateApplied")
| summarize count() by name
| render piechart
```

### 4. Feature Usage
```kql
customEvents
| where timestamp > ago(30d)
| where name !in ("AppStarted", "AppExited")
| summarize Count = count() by name
| top 20 by Count
| render barchart
```

### 5. Error Breakdown
```kql
exceptions
| where timestamp > ago(7d)
| summarize Errors = count() by type
| render piechart
```

### 6. Session Duration Distribution
```kql
customEvents
| where timestamp > ago(30d)
| where name in ("AppStarted", "AppExited")
| order by session_Id, timestamp asc
| serialize
| extend NextEvent = next(name), NextTime = next(timestamp), NextSession = next(session_Id)
| where name == "AppStarted" and NextEvent == "AppExited" and session_Id == NextSession
| extend DurationMin = datetime_diff('minute', NextTime, timestamp)
| where DurationMin > 0 and DurationMin < 480
| summarize count() by bin(DurationMin, 5)
| render columnchart
```

---

## 🎨 Recommended Visual Design

### Color Palette
| Purpose | Color | Hex |
|---------|-------|-----|
| Primary (Microsoft Blue) | Blue | #0078D4 |
| Success | Green | #107C10 |
| Warning | Yellow | #FDB813 |
| Error | Red | #D13438 |
| Neutral | Gray | #666666 |

### Card Layout (Executive Summary)
```
┌──────────┬──────────┬──────────┬──────────┬──────────┐
│ Launches │  Users   │ % Latest │  Errors  │ Avg Sess │
│   📈     │   👥     │   ✅     │   ⚠️     │   ⏱️     │
│  1,234   │   47     │   89%    │    12    │  23 min  │
└──────────┴──────────┴──────────┴──────────┴──────────┘
```

---

## 🔄 Refresh Schedule

| Data | Recommended Refresh |
|------|---------------------|
| KPIs | Every 1 hour |
| Trends | Every 4 hours |
| Detailed Tables | Daily at 6 AM |

### Setting Up Scheduled Refresh
1. Publish to Power BI Service
2. Dataset Settings → Scheduled Refresh
3. Set daily refresh at 6:00 AM UTC
4. Add your credentials for Application Insights

---

## 📊 Sample Dashboard Layout

```
╔═══════════════════════════════════════════════════════════════════════════════════╗
║  CLOUD JOURNEY ADD-IN - TELEMETRY DASHBOARD                    Last Refresh: Now  ║
╠═══════════════════════════════════════════════════════════════════════════════════╣
║  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐                      ║
║  │ 1,234   │ │   47    │ │  89%    │ │   12    │ │ 23 min  │                      ║
║  │Launches │ │ Users   │ │ Latest  │ │ Errors  │ │Avg Sess │                      ║
║  └─────────┘ └─────────┘ └─────────┘ └─────────┘ └─────────┘                      ║
╠═══════════════════════════════════════════════════════════════════════════════════╣
║  ┌────────────────────────────────────┐ ┌───────────────────────────────────────┐ ║
║  │ DAILY ACTIVE USERS (90 DAYS)       │ │ VERSION DISTRIBUTION                  │ ║
║  │                                    │ │                                       │ ║
║  │    📈 ────────────────────        │ │      ┌───────┐                        │ ║
║  │   /                        \       │ │  v3.16.49 │ 89%                       │ ║
║  │  /                          \      │ │      └───────┘                        │ ║
║  │ /                            \     │ │  v3.16.48 │ 8%                        │ ║
║  │                                    │ │  v3.16.47 │ 3%                        │ ║
║  └────────────────────────────────────┘ └───────────────────────────────────────┘ ║
╠═══════════════════════════════════════════════════════════════════════════════════╣
║  ┌────────────────────────────────────┐ ┌───────────────────────────────────────┐ ║
║  │ ERROR TREND (30 DAYS)              │ │ FEATURE USAGE                         │ ║
║  │                                    │ │                                       │ ║
║  │    📉 ────────────────────        │ │  DataRefreshed    ████████████ 234    │ ║
║  │                                    │ │  TabNavigated     ████████ 156       │ ║
║  │                                    │ │  ExportClicked    ████ 78            │ ║
║  │                                    │ │  SettingsSaved    ██ 45              │ ║
║  └────────────────────────────────────┘ └───────────────────────────────────────┘ ║
╚═══════════════════════════════════════════════════════════════════════════════════╝
```

---

## 🚨 Alerts to Configure

### Critical Alerts
| Alert | Condition | Action |
|-------|-----------|--------|
| Error Spike | Errors > 50/hour | Email team |
| Crash Rate | >10% incomplete sessions | Email + Teams |
| Zero Users | 0 launches in 24h | Check service health |

### Warning Alerts  
| Alert | Condition | Action |
|-------|-----------|--------|
| Low Adoption | <50% on latest version after 7 days | Send update reminder |
| Session Failures | Update failures > 20% | Check GitHub release |

---

## 📁 Files Included

| File | Description |
|------|-------------|
| `CloudJourneyAddin-Telemetry-Dashboard.pq` | Power Query M code for all queries |
| `POWERBI_DASHBOARD_GUIDE.md` | This setup guide |

---

## 🔗 Related Resources

- [Azure Application Insights Documentation](https://docs.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview)
- [Power BI + App Insights Integration](https://docs.microsoft.com/en-us/azure/azure-monitor/app/export-power-bi)
- [KQL Quick Reference](https://docs.microsoft.com/en-us/azure/data-explorer/kql-quick-reference)
- [TELEMETRY_QUERIES.md](../TELEMETRY_QUERIES.md) - Raw KQL queries for Azure Portal

---

**Last Updated:** January 19, 2026  
**Version:** 1.0  
**For:** Cloud Journey Add-in v3.16.49+
