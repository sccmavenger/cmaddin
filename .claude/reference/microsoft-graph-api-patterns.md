# Microsoft Graph API Patterns - Cloud Native Assessment

## Authentication

### Flows Supported
- **Interactive Browser** (`InteractiveBrowserCredential`): Opens system browser for AAD login. Primary flow.
- **Device Code** (`DeviceCodeCredential`): Displays code for auth on another device. Fallback for restricted environments.
- Settings stored in `GraphAuthSettings` model, persisted to user preferences.

### Graph Scopes (Read-Only)
```
DeviceManagementManagedDevices.Read.All
DeviceManagementConfiguration.Read.All
DeviceManagementApps.Read.All
DeviceManagementServiceConfig.Read.All
Directory.Read.All
Group.Read.All
User.Read
```

## GraphDataService Patterns

### Device Cache
- `GetCachedManagedDevicesAsync()` is the primary device retrieval method
- 5-minute TTL on managed devices (`_cacheLifetime = TimeSpan.FromMinutes(5)`)
- Full pagination on cache miss (iterates all pages)
- License data cached separately with 30-minute TTL

### Key Methods
- `GetDeviceEnrollmentAsync()` — Returns `DeviceEnrollmentProgress` with counts by join type and management state
- `DetectJoinType()` — Classifies devices: HybridEntraJoined, EntraJoined, EntraRegistered, ADOnly, Unknown
- `GetWorkloadsAsync()` — Returns co-management workload authority status and adoption percentages
- `GetComplianceAsync()` — Returns Intune compliance policy status
- `GetRealAlertsAsync()` — Extension method generating 5 alert types from live data

### Pagination
```csharp
// Full pagination pattern used for managed devices
var page = await _graphClient.DeviceManagement.ManagedDevices.GetAsync(config => {
    config.QueryParameters.Select = new[] { "id", "deviceName", ... };
    config.QueryParameters.Top = 999;
});
while (page?.Value != null) {
    allDevices.AddRange(page.Value);
    if (page.OdataNextLink != null)
        page = await _graphClient.DeviceManagement.ManagedDevices
            .WithUrl(page.OdataNextLink).GetAsync();
    else break;
}
```

### Error Handling
- `ServiceException` from Graph SDK caught and logged
- Graceful fallback to mock data on auth failure
- 429 (rate limiting) handled with exponential backoff

## Caching Strategy
| Data Type | TTL | Reason |
|-----------|-----|--------|
| Managed devices | 5 min | Balances freshness with API rate limits |
| License summary | 30 min | License data changes rarely |
| ConfigMgr devices | 5 min | Matches Graph device cache |
| AI responses | 30 min | Expensive API calls, slow to change |

## Logging
- All Graph queries logged via `FileLogger.Instance.LogGraphQuery()`
- Parameters: caller, endpoint, selectFields, filterExpression, resultCount
- Logs to `%LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs\`
