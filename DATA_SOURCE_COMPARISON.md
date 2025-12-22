# Data Source Comparison: ConfigMgr vs Intune

**Cloud Journey Progress Dashboard v2.5.0**  
**Last Updated:** December 21, 2025

---

## Overview

The dashboard uses a **dual-source architecture** to provide complete visibility into your ConfigMgr-to-Intune migration. This document details exactly what data comes from each system.

---

## 📊 Complete Data Source Matrix

| Dashboard Section | Data Element | Source System | API/Endpoint | Used For |
|------------------|--------------|---------------|--------------|----------|
| **Device Enrollment** | Total Windows 10/11 Devices | **ConfigMgr Admin Service** | `/wmi/SMS_R_System` | Complete device inventory baseline |
| | Device Names | **ConfigMgr Admin Service** | `SMS_R_System.Name` | Device identification |
| | Operating System Version | **ConfigMgr Admin Service** | `SMS_R_System.OperatingSystemNameandVersion` | Eligibility filtering (Win10/11 only) |
| | ConfigMgr Client Version | **ConfigMgr Admin Service** | `SMS_R_System.ClientVersion` | Client health tracking |
| | Last Active Time | **ConfigMgr Admin Service** | `SMS_R_System.LastActiveTime` | Device activity status |
| | Co-Management Flags | **ConfigMgr Admin Service** | `SMS_Client_ComanagementState.CoManagementFlags` | Workload authority tracking |
| | Intune-Enrolled Device Count | **Microsoft Graph** | `/deviceManagement/managedDevices` | Enrollment progress |
| | Management Agent Type | **Microsoft Graph** | `managedDevice.managementAgent` | MDM vs ConfigMgr vs Co-managed |
| | Enrollment Date | **Microsoft Graph** | `managedDevice.enrolledDateTime` | Enrollment timeline |
| | Device Compliance State | **Microsoft Graph** | `managedDevice.complianceState` | Compliance tracking |
| | Last Sync Time | **Microsoft Graph** | `managedDevice.lastSyncDateTime` | Device health monitoring |
| **Compliance Scorecard** | Device Compliance Policies | **Microsoft Graph** | `/deviceManagement/deviceCompliancePolicies` | Policy inventory |
| | Compliance Policy Count | **Microsoft Graph** | Policy count | Policy coverage metric |
| | Device Compliance Status | **Microsoft Graph** | `managedDevice.complianceState` | Compliant/Non-compliant/InGracePeriod |
| | Policy Assignment Status | **Microsoft Graph** | `/deviceManagement/deviceCompliancePolicies/{id}/deviceStatuses` | Per-policy compliance |
| | Non-Compliant Device Count | **Microsoft Graph** | Calculated from compliance states | Risk assessment |
| **Workload Status** | Workload Names | **Static Configuration** | Hardcoded list of 7 workloads | Migration checklist |
| | Compliance Policies Status | **Microsoft Graph** | Checks for policy existence | "Completed" if policies exist |
| | Device Configuration Status | **Microsoft Graph** | `/deviceManagement/deviceConfigurations` | "Completed" if configs exist |
| | Client Apps Status | **Microsoft Graph** | `/deviceAppManagement/mobileApps` | "Completed" if apps deployed |
| | Co-Management Workload Authority | **ConfigMgr Admin Service** | `CoManagementFlags` per device | Per-device workload tracking |
| | Update Policies Status | **Microsoft Graph** | `/deviceManagement/deviceConfigurations` (Windows10UpdateRings) | Update workload status |
| **Workload Velocity** | Historical Completion % | **Calculated** | Based on policy deployment trends | Velocity trend analysis |
| | Workload Transition Dates | **ConfigMgr Admin Service** | Co-management flag change dates | Timeline tracking |
| | Stalled Workload Detection | **Calculated** | No progress >30 days | Stall identification |
| **Migration Plan** | Total Workload Count | **Static** | 7 workloads hardcoded | Migration scope |
| | Completed Workload Count | **Mixed** | ConfigMgr + Graph API | Progress tracking |
| | Projected Finish Date | **Calculated** | Based on velocity | Timeline estimation |
| **Device Selection** | Device Readiness Scores | **AI-Calculated** | From ConfigMgr + Graph data | Batch prioritization |
| | ConfigMgr Client Health | **ConfigMgr Admin Service** | Client version, last active | Readiness assessment |
| | Intune Compliance History | **Microsoft Graph** | Historical compliance data | Stability assessment |
| | Device Age/Model | **ConfigMgr Admin Service** | Hardware inventory | Risk assessment |
| **Application Migration** | ConfigMgr Application List | **ConfigMgr Admin Service** | `SMS_Application` | Source app inventory |
| | Application Complexity | **Calculated** | Based on deployment types | Migration difficulty |
| | Intune Application List | **Microsoft Graph** | `/deviceAppManagement/mobileApps` | Target app inventory |
| | Win32 App Count | **Microsoft Graph** | Filter by `@odata.type` | Modern app count |
| **Enrollment Blockers** | CMG Configuration | **Microsoft Graph** | `/deviceManagement/configurationSettings` | Infrastructure check |
| | Network Bandwidth | **Not Available** | N/A - placeholder | Future enhancement |
| | Certificate Issues | **Microsoft Graph** | Device certificate errors | Blocker detection |
| | Conditional Access Policies | **Microsoft Graph** | `/identity/conditionalAccess/policies` | Enrollment prerequisites |
| **ROI Calculator** | Device Count for Calculations | **ConfigMgr Admin Service** | Total managed devices | Cost basis |
| | Annual Savings Estimate | **Static Formula** | Industry averages | Financial projection |
| | Infrastructure Cost Reduction | **Static Formula** | $180k average | Cost savings |
| | Admin Time Reduction | **Static Formula** | 35.5% (Forrester) | Efficiency gain |
| **Peer Benchmarking** | Your Completion % | **Calculated** | From actual migration data | Progress metric |
| | Peer Averages | **Static Data** | Microsoft published stats | Comparison baseline |
| | Organization Category | **Static/Inferred** | Based on device count | Peer grouping |
| **Executive Summary** | Migration Health Score | **AI-Calculated** | GPT-4 analysis of all data | Executive KPI |
| | Critical Issues | **AI-Analyzed** | From ConfigMgr + Graph data | Risk summary |
| | Key Achievements | **Calculated** | From milestone tracking | Progress summary |
| | Next Critical Action | **AI-Recommended** | Based on current state | Action prioritization |

---

## 🔍 Detailed Breakdown by System

### **ConfigMgr Admin Service** (REST API or WMI)

#### Endpoint: `/wmi/SMS_R_System`
**Purpose:** Complete Windows 10/11 device inventory

**Data Retrieved:**
```csharp
ResourceId                           // Unique device ID
Name                                 // Device hostname
OperatingSystemNameandVersion        // OS details
ClientVersion                        // ConfigMgr client version
LastActiveTime                       // Last communication with site server
```

**Filter Applied:**
```sql
OperatingSystemNameandVersion LIKE 'Microsoft Windows NT Workstation 10%' 
OR OperatingSystemNameandVersion LIKE 'Microsoft Windows NT Workstation 11%'
```
(Excludes servers, older OS versions)

#### Endpoint: `SMS_Client_ComanagementState`
**Purpose:** Co-management workload status per device

**Data Retrieved:**
```csharp
ResourceID                           // Device ID
CoManagementFlags                    // Bitmask of workload authority
    // Flag values:
    // 0 = Not co-managed
    // 1 = Compliance Policies (Intune)
    // 2 = Resource Access (Intune)
    // 4 = Device Configuration (Intune)
    // 8 = Windows Update (Intune)
    // 16 = Endpoint Protection (Intune)
    // 32 = Client Apps (Intune)
    // 64 = Office Click-to-Run (Intune)
```

**Usage:**
- Determine which workloads are Intune-managed per device
- Track workload transition progress
- Identify pilot vs production co-managed devices

---

### **Microsoft Graph API** (Intune)

#### Endpoint: `/deviceManagement/managedDevices`
**Purpose:** Intune enrollment and management status

**Query Parameters:**
```http
$select=id,deviceName,operatingSystem,managementAgent,
        enrolledDateTime,lastSyncDateTime,complianceState,
        osVersion,model,manufacturer
```

**Data Retrieved:**
```csharp
id                                   // Intune device ID
deviceName                           // Device hostname
operatingSystem                      // "Windows" or other
managementAgent                      // MDM, ConfigMgr, or ConfigMgrClientMdm
enrolledDateTime                     // When enrolled in Intune
lastSyncDateTime                     // Last check-in to Intune
complianceState                      // Compliant, NonCompliant, InGracePeriod, etc.
osVersion                           // Windows version (e.g., "10.0.19045")
model                               // Device model
manufacturer                        // Device manufacturer
```

**Management Agent Types:**
- `mdm` - Pure Intune (cloud-only)
- `configurationManagerClient` - ConfigMgr-only (via Tenant Attach)
- `configurationManagerClientMdm` - Co-managed (both)

#### Endpoint: `/deviceManagement/deviceCompliancePolicies`
**Purpose:** Intune compliance policy inventory

**Data Retrieved:**
```csharp
id                                   // Policy ID
displayName                          // Policy name
description                          // Policy description
version                             // Policy version
assignments                         // Group assignments
```

#### Endpoint: `/deviceManagement/deviceCompliancePolicies/{id}/deviceStatuses`
**Purpose:** Per-policy compliance status

**Data Retrieved:**
```csharp
deviceDisplayName                    // Device name
status                              // Compliant, NonCompliant, Conflict, Error
lastReportedDateTime                // Status timestamp
```

#### Endpoint: `/deviceManagement/deviceConfigurations`
**Purpose:** Device configuration profiles

**Data Retrieved:**
```csharp
id                                   // Configuration ID
displayName                          // Configuration name
description                          // Description
@odata.type                         // Configuration type
```

**Configuration Types Used:**
- `#microsoft.graph.windows10GeneralConfiguration`
- `#microsoft.graph.windows10EndpointProtectionConfiguration`
- `#microsoft.graph.windowsUpdateForBusinessConfiguration`

#### Endpoint: `/deviceAppManagement/mobileApps`
**Purpose:** Intune application inventory

**Data Retrieved:**
```csharp
id                                   // App ID
displayName                          // App name
publisher                           // App publisher
@odata.type                         // App type (Win32, MSI, LOB)
```

---

## 🔄 Data Flow & Processing

### **1. Device Count Calculation**

```
IF ConfigMgr Admin Service Connected:
    Total Devices = ConfigMgr SMS_R_System count (Windows 10/11 only)
    Co-Managed Devices = ConfigMgr devices with CoManagementFlags > 0
    ConfigMgr-Only Devices = Total - Co-Managed
    
    Intune-Enrolled Devices = Graph API managedDevices count (MDM + Co-managed)
    
    Migration Gap = Total Devices - Intune-Enrolled Devices

ELSE (Graph API only):
    Total Devices = Graph API managedDevices count (all types)
    Intune-Enrolled Devices = managementAgent = MDM or ConfigMgrClientMdm
    ConfigMgr-Only Devices = managementAgent = ConfigMgrClient
    
    ⚠️ WARNING: May be incomplete if devices not Tenant Attached
```

### **2. Workload Status Determination**

```
FOR EACH Workload (7 total):
    IF Has Intune policies deployed:
        Status = "Completed"
    ELSE IF ConfigMgr CoManagementFlags show Intune authority:
        Status = "In Progress" (pilot)
    ELSE:
        Status = "Not Started"
```

### **3. Compliance Score Calculation**

```
Compliant Devices = COUNT(complianceState = "Compliant")
Total Devices = COUNT(all managedDevices)

Intune Compliance Score = (Compliant / Total) * 100

ConfigMgr Compliance Score = Static (not available via API)
```

---

## 📈 Data Refresh Strategy

| Data Source | Refresh Trigger | Frequency | Cache Duration |
|------------|-----------------|-----------|----------------|
| ConfigMgr Device Inventory | Manual "Refresh" button | On-demand | No cache |
| Graph API Device List | Manual "Refresh" button | On-demand | No cache |
| Compliance Policies | Manual "Refresh" button | On-demand | No cache |
| AI Recommendations | Auto on data change | On-demand | 30 minutes |
| Workload Status | Manual "Refresh" button | On-demand | No cache |

---

## ⚠️ Data Limitations & Future Enhancements

### Current Limitations

| Limitation | Impact | Workaround |
|-----------|--------|------------|
| No ConfigMgr compliance data via API | Can't compare ConfigMgr vs Intune compliance | Uses Intune compliance only |
| No historical trend data stored | Limited velocity analysis | Simulates 6-month trends |
| No real-time workload slider positions | Can't show exact workload transition state | Uses CoManagementFlags |
| No actual ROI costs | Savings are estimates | Uses industry averages |
| No peer benchmark API | Can't compare to real peers | Uses Microsoft published stats |

### Planned Enhancements

1. **ConfigMgr Compliance Data**
   - Use PowerShell to query ConfigMgr reporting
   - Compare pre/post migration compliance scores

2. **Historical Trend Storage**
   - Store daily snapshots in local database
   - Generate accurate velocity charts

3. **Real-Time Workload Status**
   - Query ConfigMgr co-management settings
   - Show per-device workload authority

4. **Actual Cost Integration**
   - Azure Cost Management API
   - ConfigMgr infrastructure cost input

5. **Live Peer Benchmarking**
   - Partner with third-party for anonymous comparison
   - Real-time percentile rankings

---

## 🔧 Technical Details

### Connection Methods

**ConfigMgr:**
- **Primary:** Admin Service REST API (HTTPS)
  - URL: `https://{siteserver}/AdminService/wmi/SMS_R_System`
  - Auth: Windows Authentication (current user credentials)
  - Protocol: HTTPS (port 443)
  
- **Fallback:** WMI (ConfigMgr SDK)
  - Namespace: `\\{siteserver}\root\sms\site_{sitecode}`
  - Auth: Windows Authentication
  - Protocol: DCOM/RPC (port 135 + dynamic)

**Microsoft Graph:**
- **Method:** Device Code Flow (OAuth 2.0)
- **Client ID:** 14d82eec-204b-4c2f-b7e8-296a70dab67e (Microsoft Graph CLI)
- **Scopes:** `https://graph.microsoft.com/.default`
- **Required Permissions:**
  - `DeviceManagementManagedDevices.Read.All`
  - `DeviceManagementConfiguration.Read.All`
  - `Directory.Read.All`

### Data Processing Pipeline

```
1. User Clicks "Refresh"
   ↓
2. IF ConfigMgr Connected:
     Query ConfigMgr Admin Service → SMS_R_System (Windows 10/11)
     Query ConfigMgr → SMS_Client_ComanagementState
   ↓
3. Query Microsoft Graph → managedDevices
   ↓
4. Cross-Reference Data:
     Match devices by hostname
     Determine enrollment status
     Calculate migration gap
   ↓
5. Query Additional Graph Endpoints:
     Device Compliance Policies
     Device Configurations
     Mobile Apps
   ↓
6. Calculate Derived Metrics:
     Workload completion %
     Compliance scores
     Velocity trends
   ↓
7. Generate AI Insights (if Azure OpenAI configured):
     Pass metrics to GPT-4
     Get recommendations
   ↓
8. Update Dashboard UI
```

---

## 📊 Data Privacy & Security

### Data Handling
- ✅ No data stored locally (all in-memory)
- ✅ No telemetry sent to Microsoft or third parties
- ✅ Read-only access (no write operations)
- ✅ Credentials cached by Azure.Identity SDK (encrypted)

### Network Communication
- ✅ ConfigMgr: HTTPS (TLS 1.2+) or WMI (encrypted RPC)
- ✅ Graph API: HTTPS (TLS 1.2+)
- ✅ Azure OpenAI: HTTPS (TLS 1.2+)

### Permissions Required
- **ConfigMgr:** Full Administrator or Read-only Analyst role
- **Intune:** Intune Administrator or Global Reader role
- **Azure AD:** Directory.Read.All (for user profile)

---

**Document Version:** 1.0  
**Tool Version:** 2.5.0  
**Last Updated:** December 21, 2025
