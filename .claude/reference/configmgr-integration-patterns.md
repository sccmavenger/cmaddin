# ConfigMgr Integration Patterns - Cloud Native Assessment

## Data Sources

### Admin Service REST API (Primary)
- ConfigMgr's built-in REST API at `https://<server>/AdminService/`
- Endpoints used:
  - `/wmi/SMS_R_System` — Device inventory
  - `/wmi/SMS_Client` — Client health
  - `/wmi/SMS_G_System_CH_ClientSummary` — Client summary
- Authentication: Current Windows user (NTLM/Kerberos) or alternate credentials
- SSL certificate pinning via SHA256 thumbprint for self-signed certs
- 30-second HTTP timeout per request

### WMI Fallback
- Used when Admin Service is unavailable
- Connects to `\\<server>\root\sms\site_<sitecode>` namespace
- Same WQL queries as Admin Service but over WMI protocol
- Logged via `FileLogger.Instance.LogWmiQuery()`

## ConfigMgrAdminService Patterns

### Device Cache
- 5-minute TTL: `_deviceCacheLifetime = TimeSpan.FromMinutes(5)`
- Caches Windows 10/11 devices from ConfigMgr
- Cache invalidated on new connection

### Connection Flow
```
User enters server URL + site code
    ↓
Test connection to Admin Service
    ↓
If success → use REST API
If failure → prompt for WMI fallback
    ↓
Load device inventory into cache
```

### Credential Management
- Default: Current Windows user credentials
- Alternate: Username + password stored encrypted via `SecureCredentialManager` (DPAPI)
- Certificate: Optional SHA256 thumbprint pinning for self-signed SSL

### Key Methods
- `GetDevicesAsync()` — Loads ConfigMgr device inventory
- `GetCoManagementWorkloadsAsync()` — Reads co-management policy settings
- `TestConnectionAsync()` — Validates Admin Service reachability

## Co-Management Workloads (7 Total)
These are the workloads that can be transitioned from ConfigMgr to Intune authority:

1. **Compliance Policies** — Device compliance rules and enforcement
2. **Device Configuration** — Configuration profiles and settings
3. **Endpoint Protection** — Antivirus, firewall, encryption policies
4. **Windows Update for Business** — Update rings and feature updates
5. **Client Apps** — Application deployment and management
6. **Office Click-to-Run Apps** — Microsoft 365 app deployment
7. **Resource Access Policies** — VPN, Wi-Fi, email, certificate profiles

### Workload Authority Detection
- Each workload has a ConfigMgr policy flag indicating Intune or ConfigMgr authority
- `IntuneAdoptionPercentage` tracks what percentage of devices have transitioned per workload
- Bottleneck math: A device can only uninstall ConfigMgr when ALL 7 workloads are on Intune

## Cross-Referencing with Graph
- Devices matched between ConfigMgr and Graph by device name / Azure AD device ID
- Orphaned devices: In ConfigMgr but not in Intune (or vice versa)
- Ghost records: In both but with mismatched state
- Stale devices: Last contact beyond threshold in either system

## Logging
- Admin Service queries: `FileLogger.Instance.LogAdminServiceQuery()`
- WMI queries: `FileLogger.Instance.LogWmiQuery()`
- All queries include endpoint, parameters, and result count
