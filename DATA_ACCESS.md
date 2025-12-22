# Cloud Journey Progress Dashboard - Data Access Documentation

## Overview
This document details all data accessed by the Cloud Journey Progress Dashboard from Configuration Manager (ConfigMgr) and Microsoft Intune (Microsoft Graph API) environments.

---

## System Requirements

### Prerequisites
- **Configuration Manager Console** must be installed on the machine
  - Detected via registry: `HKLM\SOFTWARE\Microsoft\ConfigMgr10\Setup`
  - Detected via file system: `Microsoft.ConfigurationManagement.exe`
- **Network connectivity** to Microsoft Graph API (graph.microsoft.com)
- **Microsoft 365 credentials** with appropriate permissions

---

## Data Access Summary

| Data Category | Source System | API/Method | Permissions Required | Purpose |
|--------------|---------------|------------|---------------------|---------|
| **Device Enrollment** | Microsoft Intune | Microsoft Graph API | `DeviceManagementManagedDevices.Read.All` | Track total devices, Intune vs ConfigMgr enrollment |
| **Device Compliance** | Microsoft Intune | Microsoft Graph API | `DeviceManagementConfiguration.Read.All` | Monitor compliance policies and device compliance status |
| **User Information** | Azure AD | Microsoft Graph API | `Directory.Read.All` | Verify authentication and user context |
| **ConfigMgr Console** | Local System | File System | Local Admin (for installation) | Detect console installation, integrate with console UI |

---

## Detailed Data Access by Dashboard Section

### 1. Overall Migration Status
**Data Source:** Microsoft Intune (Microsoft Graph API)
**Endpoint:** `/deviceManagement/managedDevices`

| Data Element | API Property | Purpose |
|--------------|--------------|---------|
| Total Workloads | Static/Configuration | Display workload count (7 workloads) |
| Workloads Transitioned | Calculated from device enrollment | Count devices moved to Intune management |
| Completion Percentage | Calculated | Overall migration progress |

**Graph API Call:**
```http
GET https://graph.microsoft.com/v1.0/deviceManagement/managedDevices
```

**Properties Accessed:**
- `managementAgent` - Identifies management type (MDM, ConfigMgr, Co-management)
- `id` - Device identifier

---

### 2. Device Enrollment
**Data Source:** Microsoft Intune (Microsoft Graph API)
**Endpoint:** `/deviceManagement/managedDevices`

| Data Element | API Property | Purpose |
|--------------|--------------|---------|
| Total Devices | Count of all devices | Total managed device inventory |
| Intune Enrolled Devices | Where `managementAgent` = MDM or ConfigMgr+MDM | Count pure Intune devices |
| ConfigMgr Only Devices | Where `managementAgent` = ConfigMgr | Count ConfigMgr-only devices |
| Enrollment Trend (6 months) | Historical simulation | Show enrollment trajectory |

**Graph API Call:**
```http
GET https://graph.microsoft.com/v1.0/deviceManagement/managedDevices
```

**Properties Accessed:**
- `managementAgent` - Management type
- `enrolledDateTime` - When device was enrolled
- `deviceName` - Device identifier
- `operatingSystem` - OS type (Windows, iOS, Android, etc.)

**Filters Applied:**
```csharp
// Intune devices
d.ManagementAgent == ManagementAgentType.Mdm || 
d.ManagementAgent == ManagementAgentType.ConfigurationManagerClientMdm

// ConfigMgr only devices  
d.ManagementAgent == ManagementAgentType.ConfigurationManagerClient
```

---

### 3. Workload Status
**Data Source:** Microsoft Intune (Microsoft Graph API) + Static Configuration
**Endpoint:** `/deviceManagement/managedDevices`

| Workload | Status Determination | Data Source |
|----------|---------------------|-------------|
| Compliance Policies | Device enrollment + compliance | Graph API |
| Resource Access | Static/Configuration | Not yet implemented |
| Device Configuration | Static/Configuration | Not yet implemented |
| Windows Update for Business | Static/Configuration | Not yet implemented |
| Endpoint Protection | Static/Configuration | Not yet implemented |
| Office Click-to-Run | Static/Configuration | Not yet implemented |
| Client Apps | Static/Configuration | Not yet implemented |

**Current Implementation:**
- First 3 workloads marked "Completed" based on device enrollment threshold
- Remaining 4 workloads "Not Started" (placeholder)

**Future Enhancement:** Query workload-specific policies via Graph API

---

### 4. Compliance Score
**Data Source:** Microsoft Intune (Microsoft Graph API)
**Endpoints:** 
- `/deviceManagement/deviceCompliancePolicies`
- `/deviceManagement/managedDevices?$select=complianceState`

| Data Element | API Property | Purpose |
|--------------|--------------|---------|
| Intune Compliance Score | Calculated from `complianceState` | Overall Intune compliance percentage |
| ConfigMgr Compliance Score | Static (mock data) | ConfigMgr compliance (future: PowerShell) |
| Total Devices | Count of all devices | Denominator for compliance calculation |
| Compliant Devices | Where `complianceState` = Compliant | Numerator for compliance calculation |
| Non-Compliant Devices | Where `complianceState` != Compliant | Devices needing attention |
| Policy Violations | Count of policies with violations | Compliance issues to address |

**Graph API Calls:**
```http
GET https://graph.microsoft.com/v1.0/deviceManagement/deviceCompliancePolicies
GET https://graph.microsoft.com/v1.0/deviceManagement/managedDevices?$select=id,complianceState
```

**Properties Accessed:**
- `deviceCompliancePolicies.displayName` - Policy names
- `deviceCompliancePolicies.id` - Policy identifiers
- `managedDevices.complianceState` - Compliance, NonCompliant, InGracePeriod, etc.

**Compliance Calculation:**
```csharp
OverallComplianceRate = (CompliantDevices / TotalDevices) * 100
```

---

### 5. Peer Benchmarking
**Data Source:** Static/Configuration (No external API)

| Data Element | Source | Purpose |
|--------------|--------|---------|
| Your Progress Percentage | Calculated from migration status | Your organization's progress |
| Peer Average | Static (mock data) | Industry benchmark comparison |

**Note:** This section uses mock data. Future enhancement could integrate Microsoft Adoption Score API.

---

### 6. Alerts & Recommendations
**Data Source:** Static/Configuration (No external API)

| Data Element | Source | Purpose |
|--------------|--------|---------|
| Alerts | Static configuration | Predefined migration alerts |
| Severity Levels | Static | Critical, Warning, Info |

**Note:** This section uses predefined alerts. Future enhancement could analyze real compliance/enrollment data.

---

### 7. Recent Milestones
**Data Source:** Static/Configuration (No external API)

| Data Element | Source | Purpose |
|--------------|--------|---------|
| Milestone Events | Static configuration | Track migration achievements |
| Dates | Hardcoded | Milestone timeline |

**Note:** Future enhancement could integrate with ConfigMgr/Intune change logs.

---

### 8. Migration Blockers
**Data Source:** Static/Configuration (No external API)

| Data Element | Source | Purpose |
|--------------|--------|---------|
| Blocker Descriptions | Static configuration | Known migration obstacles |
| Severity Levels | Static | Critical, High, Medium, Low |

**Note:** Future enhancement could analyze device inventory for actual blockers (legacy OS, incompatible apps).

---

### 9. ROI Calculator
**Data Source:** Static/Configuration (No external API)

| Data Element | Source | Purpose |
|--------------|--------|---------|
| Cost Savings | Static calculation | Estimated financial benefits |
| Productivity Gains | Static calculation | Estimated time savings |

**Note:** This section uses estimated values. Future enhancement could integrate actual licensing costs.

---

### 10. Get Help & Resources
**Data Source:** Static/Configuration (No external API)

| Data Element | Source | Purpose |
|--------------|--------|---------|
| Resource Links | Static configuration | Microsoft documentation links |
| Support Options | Static configuration | Contact information |

---

## Microsoft Graph API Authentication

### Authentication Method
**Device Code Flow (Interactive)**

### Client Application
- **Client ID:** `14d82eec-204b-4c2f-b7e8-296a70dab67e`
- **Client Type:** Public Client (Microsoft Graph Command Line Tools)
- **Tenant:** `organizations` (Multi-tenant, work/school accounts)

### Authentication Flow
1. User clicks "Connect to Microsoft Graph" button
2. Device code displayed in message box
3. User navigates to https://microsoft.com/devicelogin
4. User enters device code and authenticates
5. User consents to requested permissions
6. App receives access token
7. Graph API calls made with delegated permissions

### Required Permissions (Delegated)

| Permission | Type | Purpose | Admin Consent Required |
|------------|------|---------|----------------------|
| `DeviceManagementManagedDevices.Read.All` | Delegated | Read Intune managed devices | Yes |
| `DeviceManagementConfiguration.Read.All` | Delegated | Read device compliance policies | Yes |
| `Directory.Read.All` | Delegated | Read directory data (user info) | Yes |

**Scope Used:** `https://graph.microsoft.com/.default`

---

## Configuration Manager Console Access

### Installation Detection

| Detection Method | Location | Purpose |
|-----------------|----------|---------|
| Registry Check | `HKLM\SOFTWARE\Microsoft\ConfigMgr10\Setup` | Find console install path |
| File System Check | Common install locations (C:, D:, E:, F: drives) | Detect console executable |
| Executable Check | `Microsoft.ConfigurationManagement.exe` | Verify working installation |

### Integration Points

| Integration Type | Location | Purpose |
|-----------------|----------|---------|
| XML Manifest | `XmlStorage\Extensions\Actions\` | Register console extension |
| Console Button | AdminConsole UI (attempted) | Launch dashboard from console |
| Standalone Launch | Desktop/Start Menu shortcut | Direct application launch |

**Note:** Current implementation uses standalone mode. Console integration requires GUID-based registration.

---

## Current vs Future Data Sources

### Currently Implemented (Live Data)
✅ Device enrollment counts (Intune)
✅ Device management agent types (Intune)
✅ Compliance policy data (Intune)
✅ Device compliance states (Intune)
✅ User authentication (Azure AD)

### Using Mock Data (Future Enhancement)
⏳ ConfigMgr-specific compliance scores
⏳ Workload-specific policy assignments
⏳ Historical trend data (6+ months)
⏳ Application deployment status
⏳ Update compliance details
⏳ Real-time migration blockers
⏳ Actual ROI calculations
⏳ Peer benchmarking data

### Planned Future Enhancements
🔮 ConfigMgr PowerShell integration for on-premises data
🔮 Additional Graph API endpoints (policies, applications)
🔮 Microsoft Adoption Score integration
🔮 Historical data storage and trending
🔮 Custom blocker detection algorithms
🔮 Real-time alert generation

---

## Data Privacy & Security

### Data Handling
- **No data is stored locally** - All data retrieved on-demand
- **No telemetry collection** - Tool does not send usage data to Microsoft or third parties
- **No data modification** - Read-only access to all systems
- **Credentials not stored** - OAuth tokens cached by Azure.Identity SDK (encrypted)

### Network Communication
- **Microsoft Graph API:** `https://graph.microsoft.com` (TLS 1.2+)
- **Azure AD Authentication:** `https://login.microsoftonline.com` (TLS 1.2+)
- **Device Code Flow:** `https://microsoft.com/devicelogin` (Browser-based)

### Permissions Model
- **Least Privilege:** Only reads device and compliance data
- **Delegated Permissions:** Acts on behalf of signed-in user
- **Admin Consent:** Required for organization-wide data access
- **Token Expiration:** Access tokens expire after 1 hour (automatic refresh)

---

## Troubleshooting Data Access Issues

### Issue: "Authentication failed: DeviceCodeCredential authentication failed"
**Cause:** Network connectivity, credentials, or permission issues
**Resolution:**
1. Verify internet connectivity to graph.microsoft.com
2. Ensure user has Intune admin permissions
3. Check Azure AD tenant allows device code flow
4. Verify permissions granted in Azure AD

### Issue: "Not authenticated. Call AuthenticateAsync first"
**Cause:** Graph API called before authentication
**Resolution:**
1. Click "Connect to Microsoft Graph" button
2. Complete device code authentication flow
3. Verify green button disappears after success

### Issue: Empty/zero device counts
**Cause:** No devices enrolled in Intune, or insufficient permissions
**Resolution:**
1. Verify devices exist in Intune Admin Center
2. Check user has `DeviceManagementManagedDevices.Read.All` permission
3. Verify tenant has Intune licenses assigned

### Issue: ConfigMgr Console not detected
**Cause:** Console not installed or non-standard install location
**Resolution:**
1. Install ConfigMgr Console
2. Or click "Yes" to run tool anyway (standalone mode)
3. Check registry: `HKLM\SOFTWARE\Microsoft\ConfigMgr10\Setup`

---

## API Rate Limits & Throttling

### Microsoft Graph API Limits
- **Requests per app:** 2,000 requests per second
- **Requests per user:** 1,000 requests per second
- **Token lifetime:** 1 hour (automatically refreshed)

### Dashboard Refresh Strategy
- **Manual refresh only** - User clicks "Refresh" button
- **No automatic polling** - Prevents unnecessary API calls
- **Cached display** - Last refresh time shown to user

---

## Compliance & Auditing

### Audit Trail
All Microsoft Graph API calls are logged in Azure AD Sign-in Logs:
- **User:** Signed-in user performing the action
- **Application:** Microsoft Graph Command Line Tools (14d82eec-204b-4c2f-b7e8-296a70dab67e)
- **Resource:** Microsoft Graph
- **Actions:** Read device data, read compliance policies

### Viewing Audit Logs
1. Navigate to **Azure AD Portal** → **Sign-in logs**
2. Filter by **Application:** "Microsoft Graph Command Line Tools"
3. Filter by **User:** Your admin account
4. Review **Resource** and **Activity** columns

---

## Support & Feedback

For questions about data access or permissions:
- Review this document
- Check Microsoft Graph API documentation: https://learn.microsoft.com/graph/
- Contact your Azure AD administrator for permission issues

---

**Document Version:** 1.0
**Last Updated:** December 15, 2025
**Tool Version:** 1.0.0
