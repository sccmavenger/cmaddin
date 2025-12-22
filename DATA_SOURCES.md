# Data Sources Reference Guide

This document provides detailed information about all data sources used in the Cloud Journey Progress Dashboard, including Microsoft Graph API queries, ConfigMgr PowerShell queries, and external references.

## Table of Contents
1. [Data Source Overview](#data-source-overview)
2. [Microsoft Graph API Queries](#microsoft-graph-api-queries)
3. [ConfigMgr PowerShell Queries](#configmgr-powershell-queries)
4. [Device State Definitions](#device-state-definitions)
5. [Workload Migration Order Rationale](#workload-migration-order-rationale)
6. [External References](#external-references)

---

## Data Source Overview

### Current Implementation Status

| Dashboard Section | Data Source | Status | Query Type |
|------------------|-------------|--------|------------|
| Overall Migration Status | Calculated from Workloads | ✅ Real | In-App Calculation |
| Device Enrollment | Microsoft Graph API | ✅ Real | REST API |
| Workload Status | Microsoft Graph API | ✅ Real | REST API |
| Security & Compliance | Microsoft Graph API | ✅ Real | REST API |
| ROI & Savings | Industry Averages | ⏳ Estimated | Static Calculation |
| Blockers & Health | Predefined List | ⏳ Estimated | Static Data |
| Peer Benchmarking | Microsoft Statistics | ⏳ Estimated | Static Data |
| Alerts & Recommendations | Microsoft Graph API | ✅ Real | REST API |
| Recent Milestones | Predefined Dates | ⏳ Estimated | Static Data |
| Support & Engagement | Static Links | ✅ Real | Static Data |

---

## Microsoft Graph API Queries

All Microsoft Graph API calls are made using the Microsoft Graph .NET SDK v5.36.0.

### Authentication
```csharp
// Device Code Flow Authentication
var options = new DeviceCodeCredentialOptions
{
    ClientId = "14d82eec-204b-4c2f-b7e8-296a70dab67e", // Microsoft Graph Command Line Tools
    TenantId = "organizations",
};
var credential = new DeviceCodeCredential(options);
var graphClient = new GraphServiceClient(credential, scopes);
```

**Required Permissions:**
- `DeviceManagementManagedDevices.Read.All`
- `DeviceManagementConfiguration.Read.All`
- `DeviceManagementApps.Read.All`

**API Documentation:** https://learn.microsoft.com/en-us/graph/api/overview

---

### 1. Device Enrollment Data

#### Total Devices Query
```csharp
// C# SDK Call
var devices = await _graphClient.DeviceManagement.ManagedDevices.GetAsync();
int totalDevices = devices?.Value?.Count ?? 0;
```

**REST API Equivalent:**
```http
GET https://graph.microsoft.com/v1.0/deviceManagement/managedDevices
```

**PowerShell Equivalent:**
```powershell
# Using Microsoft.Graph PowerShell Module
Connect-MgGraph -Scopes "DeviceManagementManagedDevices.Read.All"
$devices = Get-MgDeviceManagementManagedDevice
$totalDevices = $devices.Count
```

**API Reference:** https://learn.microsoft.com/en-us/graph/api/intune-devices-manageddevice-list

---

#### Intune-Enrolled Devices Query
```csharp
// Devices managed by Intune (MDM) or Co-managed (ConfigMgr + MDM)
int intuneDevices = allDevices.Count(d => 
    d.ManagementAgent == Microsoft.Graph.Models.ManagementAgentType.Mdm ||
    d.ManagementAgent == Microsoft.Graph.Models.ManagementAgentType.ConfigurationManagerClientMdm);
```

**REST API Equivalent:**
```http
GET https://graph.microsoft.com/v1.0/deviceManagement/managedDevices
?$filter=managementAgent eq 'mdm' or managementAgent eq 'configurationManagerClientMdm'
```

**PowerShell Equivalent:**
```powershell
$intuneDevices = Get-MgDeviceManagementManagedDevice | 
    Where-Object { $_.ManagementAgent -in @('mdm', 'configurationManagerClientMdm') }
$intuneDeviceCount = $intuneDevices.Count
```

**What This Includes:**
- **Intune-only devices:** Pure MDM management (no ConfigMgr client)
- **Co-managed devices:** ConfigMgr client + Intune MDM enrollment

---

#### ConfigMgr-Only Devices Query
```csharp
// Devices managed ONLY by ConfigMgr (no Intune enrollment)
int configMgrOnly = allDevices.Count(d => 
    d.ManagementAgent == Microsoft.Graph.Models.ManagementAgentType.ConfigurationManagerClient);
```

**REST API Equivalent:**
```http
GET https://graph.microsoft.com/v1.0/deviceManagement/managedDevices
?$filter=managementAgent eq 'configurationManagerClient'
```

**PowerShell Equivalent:**
```powershell
$configMgrOnlyDevices = Get-MgDeviceManagementManagedDevice | 
    Where-Object { $_.ManagementAgent -eq 'configurationManagerClient' }
$configMgrOnlyCount = $configMgrOnlyDevices.Count
```

**What This Includes:**
- Devices with ConfigMgr client installed
- NOT enrolled in Intune MDM
- Requires Tenant Attach to appear in Graph API

**Important Note:** ConfigMgr-only devices require **Tenant Attach** to be visible in Microsoft Graph API. Without Tenant Attach, these devices will not appear in the API results.

---

#### Co-Managed Devices Query (NEW)
```csharp
// Devices with BOTH ConfigMgr client AND Intune MDM enrollment
int coManagedDevices = allDevices.Count(d => 
    d.ManagementAgent == Microsoft.Graph.Models.ManagementAgentType.ConfigurationManagerClientMdm);
```

**REST API Equivalent:**
```http
GET https://graph.microsoft.com/v1.0/deviceManagement/managedDevices
?$filter=managementAgent eq 'configurationManagerClientMdm'
```

**PowerShell Equivalent:**
```powershell
$coManagedDevices = Get-MgDeviceManagementManagedDevice | 
    Where-Object { $_.ManagementAgent -eq 'configurationManagerClientMdm' }
$coManagedCount = $coManagedDevices.Count
```

**What This Includes:**
- Devices with ConfigMgr client installed
- AND enrolled in Intune MDM
- This is the "hybrid" state during migration

---

### 2. Month-Over-Month Trend Data

**Current Implementation:** ESTIMATED (Not Real Historical Data)

The current implementation does NOT query historical data. It generates estimated trend data based on current device counts:

```csharp
private EnrollmentTrend[] GenerateTrendData(int currentTotal, int currentIntune)
{
    // Generate 6 months of trend data (simplified estimation)
    var trends = new List<EnrollmentTrend>();
    var baseDate = DateTime.Now.AddMonths(-6);

    for (int i = 0; i <= 6; i++)
    {
        double progress = i / 6.0;
        trends.Add(new EnrollmentTrend
        {
            Month = baseDate.AddMonths(i),
            IntuneDevices = (int)(currentIntune * progress * 0.7), // Estimate growth
            ConfigMgrDevices = currentTotal - (int)(currentIntune * progress * 0.7)
        });
    }

    return trends.ToArray();
}
```

**Limitation:** This assumes linear growth and back-fills historical data as an estimation. It does NOT reflect actual historical enrollment numbers.

**To Get REAL Month-Over-Month Data:**

You would need to:
1. **Store historical snapshots** of device counts in a database
2. **Query daily/weekly** and store results
3. **Use Azure Log Analytics** (if available) to query historical Intune data

**Azure Log Analytics Query (if configured):**
```kusto
// Query Intune device enrollment trends from Log Analytics
IntuneDevices
| where TimeGenerated >= ago(180d)  // Last 6 months
| summarize 
    TotalDevices = dcount(DeviceId),
    IntuneDevices = dcountif(DeviceId, ManagementAgent in ("mdm", "configurationManagerClientMdm")),
    ConfigMgrOnly = dcountif(DeviceId, ManagementAgent == "configurationManagerClient"),
    CoManagedDevices = dcountif(DeviceId, ManagementAgent == "configurationManagerClientMdm")
  by Month = startofmonth(TimeGenerated)
| order by Month asc
```

**Requires:** Azure Monitor integration with Intune

**API Reference:** https://learn.microsoft.com/en-us/azure/azure-monitor/logs/log-analytics-tutorial

---

### 3. Compliance Data

#### Overall Compliance Rate Query
```csharp
var complianceStatus = await _graphClient.DeviceManagement.ManagedDevices.GetAsync(
    config => config.QueryParameters.Select = new[] { "id", "complianceState" }
);

int compliantDevices = devices.Count(d => 
    d.ComplianceState == Microsoft.Graph.Models.ComplianceState.Compliant);
    
double complianceRate = (compliantDevices / (double)totalDevices) * 100;
```

**REST API Equivalent:**
```http
GET https://graph.microsoft.com/v1.0/deviceManagement/managedDevices
?$select=id,complianceState
```

**PowerShell Equivalent:**
```powershell
$devices = Get-MgDeviceManagementManagedDevice -Property id,complianceState
$compliantDevices = ($devices | Where-Object { $_.ComplianceState -eq 'compliant' }).Count
$complianceRate = ($compliantDevices / $devices.Count) * 100
```

**Compliance States:**
- `compliant` - Device passes all compliance policies
- `noncompliant` - Device fails one or more policies
- `conflict` - Multiple policies conflict
- `error` - Error evaluating compliance
- `unknown` - Compliance state not yet evaluated
- `inGracePeriod` - Non-compliant but within grace period

**"Whose Security Checks?"** - These are YOUR organization's compliance policies defined in Microsoft Intune. The dashboard shows compliance against policies YOU created.

**API Reference:** https://learn.microsoft.com/en-us/graph/api/resources/intune-devices-manageddevice

---

#### Compliance Policy List Query
```csharp
var policies = await _graphClient.DeviceManagement.DeviceCompliancePolicies.GetAsync();
```

**REST API Equivalent:**
```http
GET https://graph.microsoft.com/v1.0/deviceManagement/deviceCompliancePolicies
```

**PowerShell Equivalent:**
```powershell
$compliancePolicies = Get-MgDeviceManagementDeviceCompliancePolicy
```

**API Reference:** https://learn.microsoft.com/en-us/graph/api/intune-deviceconfig-devicecompliancepolicy-list

---

### 4. Alerts Data

#### Stale Devices Alert (7+ days no sync)
```csharp
var staleDevices = devices.Value.Where(d => 
    d.LastSyncDateTime.HasValue && 
    (DateTime.Now - d.LastSyncDateTime.Value).TotalDays > 7).ToList();
```

**REST API Equivalent:**
```http
GET https://graph.microsoft.com/v1.0/deviceManagement/managedDevices
?$filter=lastSyncDateTime lt {7_days_ago_ISO8601}
&$select=deviceName,lastSyncDateTime
```

**PowerShell Equivalent:**
```powershell
$sevenDaysAgo = (Get-Date).AddDays(-7)
$staleDevices = Get-MgDeviceManagementManagedDevice | 
    Where-Object { $_.LastSyncDateTime -lt $sevenDaysAgo }
```

**API Reference:** https://learn.microsoft.com/en-us/graph/api/resources/intune-devices-manageddevice

---

#### Non-Compliant Devices Alert
```csharp
var nonCompliant = devices.Value.Where(d => 
    d.ComplianceState == Microsoft.Graph.Models.ComplianceState.Noncompliant).ToList();
```

**REST API Equivalent:**
```http
GET https://graph.microsoft.com/v1.0/deviceManagement/managedDevices
?$filter=complianceState eq 'noncompliant'
&$select=deviceName,complianceState
```

**PowerShell Equivalent:**
```powershell
$nonCompliantDevices = Get-MgDeviceManagementManagedDevice | 
    Where-Object { $_.ComplianceState -eq 'noncompliant' }
```

---

#### Recent Enrollments Alert (Last 7 days)
```csharp
var recentEnrollments = devices.Value.Where(d => 
    d.EnrolledDateTime.HasValue && 
    (DateTime.Now - d.EnrolledDateTime.Value).TotalDays <= 7).ToList();
```

**REST API Equivalent:**
```http
GET https://graph.microsoft.com/v1.0/deviceManagement/managedDevices
?$filter=enrolledDateTime ge {7_days_ago_ISO8601}
&$select=deviceName,enrolledDateTime
```

**PowerShell Equivalent:**
```powershell
$sevenDaysAgo = (Get-Date).AddDays(-7)
$recentEnrollments = Get-MgDeviceManagementManagedDevice | 
    Where-Object { $_.EnrolledDateTime -ge $sevenDaysAgo }
```

---

### 5. Workload Status Detection

#### Compliance Policies Workload
```csharp
var compliancePolicies = await _graphClient.DeviceManagement.DeviceCompliancePolicies.GetAsync();
bool hasCompliancePolicies = compliancePolicies?.Value?.Any() == true;
// Status: Completed if policies exist, NotStarted if none
```

**REST API Equivalent:**
```http
GET https://graph.microsoft.com/v1.0/deviceManagement/deviceCompliancePolicies
?$top=1
```

**PowerShell Equivalent:**
```powershell
$compliancePolicies = Get-MgDeviceManagementDeviceCompliancePolicy
$hasCompliance = $compliancePolicies.Count -gt 0
```

**API Reference:** https://learn.microsoft.com/en-us/graph/api/intune-deviceconfig-devicecompliancepolicy-list

---

#### Device Configuration Workload
```csharp
var deviceConfigs = await _graphClient.DeviceManagement.DeviceConfigurations.GetAsync();
bool hasDeviceConfigs = deviceConfigs?.Value?.Any() == true;
```

**REST API Equivalent:**
```http
GET https://graph.microsoft.com/v1.0/deviceManagement/deviceConfigurations
?$top=1
```

**PowerShell Equivalent:**
```powershell
$deviceConfigs = Get-MgDeviceManagementDeviceConfiguration
$hasDeviceConfig = $deviceConfigs.Count -gt 0
```

**API Reference:** https://learn.microsoft.com/en-us/graph/api/intune-deviceconfig-deviceconfiguration-list

---

#### Client Apps Workload
```csharp
var appPolicies = await _graphClient.DeviceAppManagement.ManagedAppPolicies.GetAsync();
bool hasManagedApps = appPolicies?.Value?.Any() == true;
```

**REST API Equivalent:**
```http
GET https://graph.microsoft.com/v1.0/deviceAppManagement/managedAppPolicies
?$top=1
```

**PowerShell Equivalent:**
```powershell
$appPolicies = Get-MgDeviceAppManagementManagedAppPolicy
$hasManagedApps = $appPolicies.Count -gt 0
```

**API Reference:** https://learn.microsoft.com/en-us/graph/api/intune-mam-managedapppolicy-list

---

## ConfigMgr PowerShell Queries

**Current Implementation:** NOT IMPLEMENTED

The dashboard currently does NOT query ConfigMgr directly. All device data comes from Microsoft Graph API, which requires **Tenant Attach** to see ConfigMgr-managed devices.

### To Query ConfigMgr Directly (Future Enhancement):

#### Connect to ConfigMgr
```powershell
# Import ConfigMgr Module
Import-Module "$($ENV:SMS_ADMIN_UI_PATH)\..\ConfigurationManager.psd1"

# Connect to site
$SiteCode = "PS1"  # Your site code
Set-Location "$SiteCode:"
```

#### Get All Devices from ConfigMgr
```powershell
# Get all devices in ConfigMgr
$allDevices = Get-CMDevice

# Get co-management enabled devices
$coManagedDevices = Get-CMDevice | Where-Object { $_.CoManagementFlags -ne 0 }

# Get devices not enrolled in Intune
$configMgrOnlyDevices = Get-CMDevice | Where-Object { 
    $_.CoManagementFlags -eq 0 -and $_.IsActive -eq $true 
}
```

**Documentation:** https://learn.microsoft.com/en-us/powershell/module/configurationmanager/

---

## Device State Definitions

### Intune-Enrolled Devices

**Definition:** Devices that are registered with Microsoft Intune MDM and can receive cloud-based policies.

**Includes:**
1. **Intune-Only Devices**
   - Only Intune MDM enrollment
   - No ConfigMgr client installed
   - Pure cloud management
   - **Example:** New Windows devices enrolled via Autopilot

2. **Co-Managed Devices**
   - ConfigMgr client installed
   - AND Intune MDM enrolled
   - Hybrid management (workloads can be split)
   - **Example:** Existing ConfigMgr devices enrolled in Intune

**Management Agent Values:**
- `mdm` = Intune-only
- `configurationManagerClientMdm` = Co-managed

**How a Device Becomes Intune-Enrolled:**
1. Manual enrollment via Settings > Accounts > Access work or school
2. Group Policy enrollment (domain-joined devices)
3. Automatic enrollment via Azure AD join
4. Co-management enablement from ConfigMgr
5. Windows Autopilot provisioning

---

### ConfigMgr-Only Devices

**Definition:** Devices managed exclusively by Configuration Manager with NO Intune MDM enrollment.

**Characteristics:**
- ConfigMgr client installed and active
- NOT enrolled in Intune MDM
- On-premises management only
- Requires Tenant Attach to appear in Graph API

**Management Agent Value:**
- `configurationManagerClient` = ConfigMgr-only

**How a Device Becomes ConfigMgr-Only:**
1. ConfigMgr client pushed/installed
2. Co-management NOT enabled
3. Never enrolled in Intune

**Visibility Requirements:**
- **Tenant Attach must be configured** for these devices to appear in Microsoft Graph API
- Without Tenant Attach, dashboard will show 0 ConfigMgr-only devices

---

### Co-Managed Devices (Hybrid State)

**Definition:** Devices with BOTH ConfigMgr client AND Intune MDM enrollment. This is the transitional state during migration.

**Management Agent Value:**
- `configurationManagerClientMdm`

**Workload Control:**
Co-management allows you to control which workloads are managed by ConfigMgr vs Intune:
- **Slider at ConfigMgr:** Workload managed by ConfigMgr
- **Slider at Intune:** Workload managed by Intune
- **Slider at Pilot:** Workload managed by Intune for pilot group only

**The 7 Workload Sliders:**
1. Compliance Policies
2. Device Configuration
3. Resource Access
4. Endpoint Protection
5. Windows Update for Business
6. Office Click-to-Run
7. Client Apps

**Documentation:** https://learn.microsoft.com/en-us/mem/configmgr/comanage/overview

---

## Workload Migration Order Rationale

### Recommended Order: Why This Sequence?

The dashboard recommends migrating workloads in this order:

**1. Compliance Policies (FIRST)**
- **Why First:** Foundation for security. Must establish device health baseline before other migrations.
- **Rationale:** Compliance policies define what a "healthy device" looks like. Other workloads depend on this.
- **Risk:** Low - policies are evaluative, not enforcing actions
- **Complexity:** Low
- **Source:** [Microsoft Best Practices](https://learn.microsoft.com/en-us/mem/configmgr/comanage/workloads#compliance-policies)

**2. Endpoint Protection (EARLY)**
- **Why Early:** Security cannot wait. Windows Defender, firewall, and BitLocker policies are critical.
- **Rationale:** Security should be established early in migration to avoid gaps.
- **Risk:** Low-Medium - policies are largely compatible
- **Complexity:** Low-Medium
- **Source:** [Microsoft Best Practices](https://learn.microsoft.com/en-us/mem/configmgr/comanage/workloads#endpoint-protection)

**3. Device Configuration (AFTER SECURITY)**
- **Why Third:** Once security baseline is set, configure device settings, restrictions, and profiles.
- **Rationale:** Settings affect user experience - ensure security is solid first.
- **Risk:** Medium - settings can impact users
- **Complexity:** Medium
- **Source:** [Microsoft Best Practices](https://learn.microsoft.com/en-us/mem/configmgr/comanage/workloads#device-configuration)

**4. Resource Access (MID-MIGRATION)**
- **Why Mid-Point:** Wi-Fi, VPN, certificates are critical for remote users but require infrastructure.
- **Rationale:** Users need connectivity - migrate once device management is stable.
- **Risk:** Medium-High - connectivity issues affect productivity
- **Complexity:** Medium-High (certificate infrastructure)
- **Source:** [Microsoft Best Practices](https://learn.microsoft.com/en-us/mem/configmgr/comanage/workloads#resource-access-policies)

**5. Windows Update for Business (MID-LATE)**
- **Why Mid-Late:** Patching is critical but ConfigMgr patching works well - no rush to change.
- **Rationale:** Migrate once device and policy management is proven.
- **Risk:** Medium - patching issues cause disruption
- **Complexity:** Low-Medium
- **Source:** [Microsoft Best Practices](https://learn.microsoft.com/en-us/mem/configmgr/comanage/workloads#windows-update-policies)

**6. Office Click-to-Run (LATE)**
- **Why Late:** Office deployment is mature in ConfigMgr - migrate near end.
- **Rationale:** Office is stable, no urgency to migrate.
- **Risk:** Low - Office updates are resilient
- **Complexity:** Low
- **Source:** [Microsoft Best Practices](https://learn.microsoft.com/en-us/mem/configmgr/comanage/workloads#office-click-to-run-apps)

**7. Client Apps (LAST)**
- **Why Last:** Most complex, many dependencies, highest risk.
- **Rationale:** Applications are unique per organization, require thorough testing, and have the most "gotchas."
- **Risk:** HIGH - app deployment failures affect business operations
- **Complexity:** HIGH
- **Source:** [Microsoft Best Practices](https://learn.microsoft.com/en-us/mem/configmgr/comanage/workloads#client-apps)

**Key Principle:** Migrate in order of increasing complexity and decreasing criticality. Start with foundational, low-risk workloads and end with complex, high-risk ones.

**Reference:** [Microsoft Co-management Workloads Best Practices](https://learn.microsoft.com/en-us/mem/configmgr/comanage/workloads)

---

## Common Risk Areas - Data Sources

### Where Does This Information Come From?

**Current Implementation:** The "Common Risk Areas" shown in the Security & Compliance section are derived from:

1. **Microsoft Intune Compliance Policy Templates**
   - Microsoft provides built-in compliance templates that check for common issues
   - Reference: https://learn.microsoft.com/en-us/mem/intune/protect/compliance-policy-create-windows

2. **Industry Security Baselines**
   - CIS Benchmarks: https://www.cisecurity.org/cis-benchmarks
   - Microsoft Security Baselines: https://learn.microsoft.com/en-us/windows/security/threat-protection/windows-security-baselines

3. **Common Compliance Failures from Microsoft Data**
   - Microsoft publishes common compliance failures seen across customers
   - Reference: https://learn.microsoft.com/en-us/mem/intune/protect/compliance-policy-monitor

**Specific Risk Area Sources:**

| Risk Area | How Detected | API/Query | Reference |
|-----------|-------------|-----------|-----------|
| Outdated OS versions | Query `osVersion` property | `Get-MgDeviceManagementManagedDevice \| Select osVersion` | [Intune Device Properties](https://learn.microsoft.com/en-us/graph/api/resources/intune-devices-manageddevice) |
| Missing encryption | Compliance policy: `bitLockerEnabled` | Check compliance state for BitLocker policy | [BitLocker Compliance](https://learn.microsoft.com/en-us/mem/intune/protect/encrypt-devices) |
| Weak passwords | Compliance policy: `passwordMinimumLength`, `passwordComplexity` | Check compliance state for password policy | [Password Compliance](https://learn.microsoft.com/en-us/mem/intune/protect/compliance-policy-create-windows#password) |
| Disabled firewall | Compliance policy: `firewallEnabled` | Check compliance state for firewall policy | [Firewall Compliance](https://learn.microsoft.com/en-us/mem/intune/protect/endpoint-security-firewall-policy) |
| Outdated antivirus | Query `lastSyncDateTime` for Defender updates | `Get-MgDeviceManagementManagedDevice \| Select lastSyncDateTime` | [Defender Management](https://learn.microsoft.com/en-us/mem/intune/protect/antivirus-microsoft-defender-settings-windows) |

---

## ROI & Savings - Data Sources

### Where Do These Numbers Come From?

**Current Implementation:** ROI and savings estimates are based on:

1. **Gartner Research: Total Cost of Ownership for Endpoint Management**
   - Report: "Magic Quadrant for Unified Endpoint Management Tools"
   - Link: https://www.gartner.com/en/documents/4010766
   - **Note:** Gartner reports require subscription

2. **Microsoft Case Studies**
   - Link: https://customers.microsoft.com/en-us/search?sq=%22microsoft%20endpoint%20manager%22&ff=&p=0
   - Average savings reported: $500K-$2M annually for enterprise customers

3. **Forrester Total Economic Impact Study**
   - Report: "The Total Economic Impact™ Of Microsoft Endpoint Manager"
   - Link: https://tools.totaleconomicimpact.com/go/microsoft/MEM/
   - **Key Findings:**
     - 151% ROI over 3 years
     - Payback period: <6 months
     - $4.3M benefit over 3 years (composite organization)

4. **IDC Business Value Study**
   - Report: "The Business Value of Microsoft Endpoint Manager"
   - Link: https://www.microsoft.com/en-us/microsoft-365/blog/2021/02/04/new-idc-study-microsoft-endpoint-manager-delivers-184-roi/
   - **Key Findings:**
     - 184% 5-year ROI
     - 60% reduction in endpoint management costs
     - $1.76M annual benefit per organization

### Specific ROI Calculations - Sources

**Infrastructure Cost Reduction ($30K-$75K/year)**
- Source: Forrester TEI Study - Infrastructure cost avoidance
- Calculation basis:
  - ConfigMgr site server hardware: $15K-$30K/year (server costs + Windows Server licensing)
  - SQL Server licensing: $8K-$12K/year (Standard Edition per core)
  - Distribution points: $3K-$5K/year per DP (5-10 DPs typical)
  - Maintenance & support: $12K-$20K/year
- Link: https://tools.totaleconomicimpact.com/go/microsoft/MEM/

**Admin Time Reduction (15-30%)**
- Source: IDC Business Value Study - IT staff productivity gains
- Calculation basis:
  - ConfigMgr requires 30-40% of admin time for maintenance
  - Intune requires 10-15% of admin time (cloud-managed)
  - Savings: 20-25% of FTE time
- Link: https://www.microsoft.com/en-us/microsoft-365/blog/2021/02/04/new-idc-study-microsoft-endpoint-manager-delivers-184-roi/

**Patch Cycle Time Reduced (20-30 days/month)**
- Source: Microsoft customer data (aggregated)
- Calculation basis:
  - ConfigMgr typical patch cycle: 30-45 days (test → approve → deploy → verify)
  - Intune typical patch cycle: 5-10 days (automatic approval, faster deployment)
  - Time savings: 20-35 days per month
- Link: https://learn.microsoft.com/en-us/mem/intune/protect/windows-update-for-business-configure

**Reference Links for ROI Section:**
- Forrester TEI Study: https://tools.totaleconomicimpact.com/go/microsoft/MEM/
- IDC Business Value Study: https://www.microsoft.com/en-us/microsoft-365/blog/2021/02/04/new-idc-study-microsoft-endpoint-manager-delivers-184-roi/
- Gartner TCO Analysis: https://www.gartner.com/en/documents/4010766 (subscription required)
- Microsoft Case Studies: https://customers.microsoft.com/en-us/search?sq=%22microsoft%20endpoint%20manager%22

---

## External References

### Microsoft Official Documentation
- **Intune Documentation:** https://learn.microsoft.com/en-us/mem/intune/
- **ConfigMgr Documentation:** https://learn.microsoft.com/en-us/mem/configmgr/
- **Co-management Documentation:** https://learn.microsoft.com/en-us/mem/configmgr/comanage/
- **Microsoft Graph API:** https://learn.microsoft.com/en-us/graph/api/overview

### API References
- **Intune Graph API:** https://learn.microsoft.com/en-us/graph/api/resources/intune-graph-overview
- **Managed Device Resource:** https://learn.microsoft.com/en-us/graph/api/resources/intune-devices-manageddevice
- **Compliance Policy Resource:** https://learn.microsoft.com/en-us/graph/api/resources/intune-deviceconfig-devicecompliancepolicy

### Industry Research
- **Forrester TEI Study (MEM):** https://tools.totaleconomicimpact.com/go/microsoft/MEM/
- **IDC Business Value Study:** https://www.microsoft.com/en-us/microsoft-365/blog/2021/02/04/new-idc-study-microsoft-endpoint-manager-delivers-184-roi/
- **Gartner Magic Quadrant:** https://www.gartner.com/en/documents/4010766

### Community Resources
- **Microsoft Tech Community (Intune):** https://techcommunity.microsoft.com/t5/microsoft-intune/bd-p/Microsoft-Intune
- **ConfigMgr Community:** https://techcommunity.microsoft.com/t5/configuration-manager/bd-p/ConfigurationManagerBlog

---

## Summary: Data Quality by Section

| Section | Real Data | Estimated | Data Source | Query Available |
|---------|-----------|-----------|-------------|----------------|
| Device Enrollment | ✅ Yes | ❌ No | Microsoft Graph API | ✅ Yes |
| Trend Graph | ❌ No | ✅ Yes | Estimated (no historical storage) | ❌ No |
| Co-Management Count | ✅ Yes | ❌ No | Microsoft Graph API | ✅ Yes |
| Compliance Score | ✅ Yes | ❌ No | Microsoft Graph API | ✅ Yes |
| Risk Areas | ✅ Yes | ❌ No | Compliance policy results | ✅ Yes |
| Workload Status | ✅ Yes | ❌ No | Microsoft Graph API | ✅ Yes |
| Alerts | ✅ Yes | ❌ No | Microsoft Graph API | ✅ Yes |
| ROI Savings | ❌ No | ✅ Yes | Industry research (Forrester/IDC) | ❌ No |
| Peer Benchmarking | ❌ No | ✅ Yes | Microsoft published statistics | ❌ No |
| Milestones | ❌ No | ✅ Yes | Predefined list | ❌ No |

---

## Future Enhancements

### To Get Real Historical Trend Data:
1. Implement database storage (SQL Server, Azure SQL)
2. Schedule daily queries to store device counts
3. Build historical reporting from stored data

### To Get Real ROI Data:
1. Integrate with Azure Cost Management API
2. Track actual infrastructure costs
3. Calculate real savings based on decommissioned servers

### To Get Real Peer Benchmarking:
1. Partner with third-party data provider
2. Aggregate anonymized customer data
3. Build industry comparison reports

---

**Document Version:** 1.0  
**Last Updated:** December 16, 2025  
**Maintained By:** Cloud Journey Dashboard Team
