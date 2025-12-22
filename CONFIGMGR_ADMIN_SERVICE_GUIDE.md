# ConfigMgr Admin Service Integration Guide

## Version 1.3.0 - December 16, 2025

### What's New: Dual-Source Device Counting

The dashboard now queries **BOTH** data sources to give you the complete picture:

1. **ConfigMgr Admin Service** - Full inventory of Windows 10/11 devices
2. **Microsoft Graph (Intune)** - Enrollment status for each device

### Why This Matters

**Previous Behavior (v1.2.2 and earlier):**
- ❌ Only queried Microsoft Graph (Intune)
- ❌ Could only see devices that were already enrolled
- ❌ Missing: ConfigMgr-only devices not yet enrolled in Intune
- ❌ Incomplete co-management visibility

**New Behavior (v1.3.0):**
- ✅ Queries ConfigMgr Admin Service for full Windows 10/11 inventory
- ✅ Queries Microsoft Graph for enrollment status
- ✅ Cross-references both sources for accurate counts
- ✅ Shows true co-managed device counts
- ✅ Shows ConfigMgr-only devices waiting to be enrolled

### Device Counting Logic

```
Total Windows 10/11 Devices = ConfigMgr Admin Service (all Win10/11 workstations)
Intune-Enrolled = Microsoft Graph (MDM + co-managed devices)
ConfigMgr-Only = Total - Intune-Enrolled (devices not yet migrated)
```

---

## Setup Instructions

### Step 1: Enable ConfigMgr Admin Service

The Admin Service must be enabled on your ConfigMgr site server.

**Check if it's already enabled:**
1. Open **ConfigMgr Console**
2. Go to **Administration > Site Configuration > Sites**
3. Right-click your site → **Hierarchy Settings**
4. Look for "Administration Service" tab

**If not enabled:**
Follow Microsoft's guide: https://learn.microsoft.com/mem/configmgr/develop/adminservice/overview

### Step 2: Verify Your Permissions

You need **one of these roles** in ConfigMgr:
- ✅ Full Administrator
- ✅ Read-only Analyst

**To check:**
1. ConfigMgr Console → **Administration > Security > Administrative Users**
2. Find your user account
3. Verify role assignment

### Step 3: Find Your Admin Service URL

The URL format is:
```
https://[YourSiteServer]/AdminService
```

**Examples:**
- `https://CM01.contoso.com/AdminService`
- `https://sccm.corp.contoso.com/AdminService`
- `https://cm01.contoso.local/AdminService`

**To find it:**
1. Your site server name is in **Administration > Site Configuration > Sites**
2. The Admin Service uses HTTPS on the default site server

### Step 4: Test the Connection (Optional)

Open PowerShell and run:

```powershell
$url = "https://CM01.contoso.com/AdminService/wmi/SMS_Site"
Invoke-RestMethod -Uri $url -UseDefaultCredentials
```

If you see site information, the Admin Service is working!

---

## Using the Dashboard

### First Launch (New Workflow)

1. **Launch CloudJourneyAddin.exe**

2. **Connect to Microsoft Graph (Intune)**
   - Click "Connect to Microsoft Graph" button
   - Sign in with your Intune admin account
   - This gets enrollment status for devices

3. **Connect to ConfigMgr Admin Service** ⭐ NEW
   - Click "Connect to ConfigMgr" button
   - Enter your Admin Service URL: `https://CM01.contoso.com/AdminService`
   - Dashboard will query ConfigMgr for Windows 10/11 inventory

4. **View Complete Data**
   - Total Windows 10/11 devices (from ConfigMgr)
   - Intune-enrolled devices (from Graph)
   - ConfigMgr-only devices (calculated: Total - Enrolled)
   - Co-managed device count (from ConfigMgr flags)

### What You'll See

**Device Enrollment Section:**
- **Total Windows 10/11:** All Windows 10/11 workstations in ConfigMgr (the migration pool)
- **Intune Enrolled:** Devices successfully enrolled in Intune (MDM + co-managed)
- **ConfigMgr Only:** Devices not yet enrolled (eligible but waiting)

**Accurate Percentages:**
```
Enrollment % = (Intune Enrolled) / (Total Windows 10/11) × 100
```

This is your TRUE migration progress!

---

## Troubleshooting

### "Failed to connect to Admin Service"

**Check:**
1. Is the URL correct? `https://[SiteServer]/AdminService`
2. Is Admin Service enabled on the site server?
3. Are you running the app as a domain user with ConfigMgr access?
4. Can you ping the site server?

**Test manually:**
```powershell
Test-NetConnection -ComputerName CM01.contoso.com -Port 443
```

### "Authentication Required"

You need ConfigMgr permissions. Verify:
1. You have Full Administrator or Read-only Analyst role
2. You're logged in as a domain user (not local admin)
3. Your account has not expired

### "No devices returned"

**Check:**
1. Do you have Windows 10/11 devices in ConfigMgr?
2. Run this query in ConfigMgr Reports:

```sql
SELECT Name, OperatingSystemNameandVersion 
FROM v_R_System 
WHERE OperatingSystemNameandVersion LIKE '%Workstation 10%' 
   OR OperatingSystemNameandVersion LIKE '%Workstation 11%'
```

### Dashboard shows different numbers

If ConfigMgr Admin Service is NOT configured:
- Dashboard falls back to Intune-only data (incomplete view)
- You'll only see devices already enrolled
- ConfigMgr-only devices won't appear

**Solution:** Configure the Admin Service URL to get the complete picture.

---

## Technical Details

### API Endpoints Used

**ConfigMgr Admin Service:**
```
GET https://[SiteServer]/AdminService/wmi/SMS_R_System
Filter: OperatingSystemNameandVersion like 'Microsoft Windows NT Workstation 10%' 
     or OperatingSystemNameandVersion like 'Microsoft Windows NT Workstation 11%'
```

**Microsoft Graph:**
```
GET https://graph.microsoft.com/v1.0/deviceManagement/managedDevices
Filter: operatingSystem contains 'Windows 10' or 'Windows 11'
     AND not contains 'Server'
```

### Device Properties Queried

**From ConfigMgr:**
- ResourceId (unique device ID)
- Name (device name)
- OperatingSystemNameandVersion
- LastActiveTime
- ClientVersion
- CoManagementFlags (0 = not co-managed, >0 = co-managed)

**From Intune:**
- id
- deviceName
- operatingSystem
- managementAgent (Mdm, ConfigurationManagerClientMdm, ConfigurationManagerClient)

### Co-Management Detection

A device is co-managed when:
- **ConfigMgr:** CoManagementFlags > 0
- **Intune:** managementAgent = ConfigurationManagerClientMdm

---

## Security & Privacy

### Authentication
- **ConfigMgr:** Uses your Windows credentials (integrated authentication)
- **Intune:** Uses Azure AD device code flow

### Data Storage
- No credentials are stored
- Admin Service URL is stored in memory only (lost on app restart)
- Must re-enter URL each time you launch the app

### Permissions Required
- **ConfigMgr:** Read-only access to device inventory
- **Intune:** DeviceManagementManagedDevices.Read.All

### Network Requirements
- HTTPS access to ConfigMgr site server (port 443)
- HTTPS access to Microsoft Graph (graph.microsoft.com)
- No inbound firewall rules required

---

## FAQ

**Q: Do I need to configure the Admin Service every time?**
A: Yes, currently the URL is not persisted. You'll need to enter it each time you launch the dashboard. (Future enhancement: Save to user settings)

**Q: Can I use this without ConfigMgr Admin Service?**
A: Yes, but you'll only see devices already enrolled in Intune. You won't see ConfigMgr-only devices waiting to be migrated.

**Q: Will this work with co-management?**
A: Yes! That's exactly what it's designed for. It shows both co-managed devices and ConfigMgr-only devices.

**Q: What if I don't have Full Administrator role?**
A: Read-only Analyst role works too. You only need read access to device inventory.

**Q: Does this query on-premises ConfigMgr or cloud-attached?**
A: On-premises ConfigMgr via the Admin Service. It does NOT require tenant attach.

**Q: Can I see historical enrollment data?**
A: Not yet. The Admin Service query is real-time only. Historical trending is a future enhancement.

---

## Version History

### v1.3.0 (December 16, 2025)
- ✅ ConfigMgr Admin Service integration
- ✅ Dual-source device counting (ConfigMgr + Intune)
- ✅ Co-managed device detection
- ✅ Accurate ConfigMgr-only device counts
- ✅ True migration progress percentages

### v1.2.2 (December 16, 2025)
- Enhanced filtering to Windows 10/11 only
- Documented Intune enrollment requirements

### v1.2.1 (December 16, 2025)
- Server filtering (exclude Windows Server)

### v1.2.0 (December 16, 2025)
- AI-powered migration recommendations

---

## Support

For issues with:
- **Admin Service setup:** https://learn.microsoft.com/mem/configmgr/develop/adminservice/
- **ConfigMgr permissions:** Contact your ConfigMgr administrator
- **Intune connectivity:** Verify Azure AD permissions
- **Dashboard bugs:** Review logs in Windows Event Viewer (Application logs)

