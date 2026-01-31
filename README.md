# Cloud Native Assessment

**Version 3.17.87** | January 30, 2026

> **📋 Complete Documentation** - This README is the single source of truth for all product information, combining user guide, installation, development, testing, and reference documentation.

---

## 📋 Table of Contents

### Getting Started
- [Quick Start (2 Steps)](#-quick-start-2-steps)
- [What's New](#-whats-new)
- [Dashboard Overview](#-dashboard-overview)
- [System Requirements](#-system-requirements)

### User Guide
- [First Time Setup](#-first-time-setup)
- [ConfigMgr Admin Service Setup](#-configmgr-admin-service-setup)
- [Understanding the Dashboard](#-understanding-your-dashboard)
- [Azure OpenAI Setup (Required for AI Recommendations)](#-azure-openai-setup-required-for-ai-recommendations)
- [Troubleshooting](#-troubleshooting)

### Installation & Deployment
- [Automated Installation](#-automated-installation-recommended)
- [Manual Installation](#-manual-installation)
- [Offline Installation](#-offline-installation)
- [Uninstallation](#-uninstallation)

### Developer Documentation
- [Architecture](#-architecture)
- [Project Structure](#-project-structure)
- [Development Setup](#-development-setup)

### Reference
- [Data Privacy & Security](PRIVACY.md)
- [Data Sources](#-data-sources-reference)
- [Changelog](#-changelog-highlights)
- [License](#license)

---

## 🚀 Quick Start (2 Steps)

### For ConfigMgr Administrators - Zero Setup Required!

1. **Download and run** the MSI installer from [GitHub Releases](https://github.com/sccmavenger/cmaddin/releases/latest)
2. **Launch ConfigMgr Console** and look for "Cloud Native Assessment" in the ribbon

**That's it!** The MSI installer automatically:
- ✅ Installs to the correct ConfigMgr Console location
- ✅ Registers the add-in with ConfigMgr
- ✅ Creates Start Menu and Desktop shortcuts
- ✅ Supports standard Add/Remove Programs uninstall

> **Note:** .NET 8.0 Desktop Runtime is required. Download from [Microsoft](https://dotnet.microsoft.com/download/dotnet/8.0) if not already installed.

### Alternative: PowerShell Script Installation

If you prefer the PowerShell installer (auto-downloads .NET runtime):

1. **Extract the ZIP** to any folder
2. **Right-click** `Install-ZeroTrustMigrationAddin.ps1` → **Run with PowerShell**
3. **Launch ConfigMgr Console**

The PowerShell installer:
- ✅ Checks and elevates to admin if needed
- ✅ Downloads and installs .NET 8.0 Runtime if missing (~55MB)
- ✅ Validates the installation
- ✅ Creates an uninstaller script

### What Gets Installed

**Application Files:**
- Core .NET 8.0 runtime libraries
- WPF framework components
- LiveCharts visualization library
- Microsoft Graph SDK
- All supporting assemblies

**Total Deployment Size:** ~72MB (MSI) or 233MB (ZIP with dependencies)

**Installation Location:**
```
C:\Program Files (x86)\Microsoft Configuration Manager\AdminConsole\
├── XmlStorage\Extensions\Actions\ZeroTrustMigrationAddin.xml  (manifest)
└── bin\ZeroTrustMigrationAddin\                               (all app files)
    ├── ZeroTrustMigrationAddin.exe                            (main executable)
    ├── ZeroTrustMigrationAddin.dll                            (app logic)
    └── ... (additional dependencies)
```

---

## 🆕 What's New































### Version 3.17.86 (January 30, 2026)

### Changed
- Updated main header banner and window title bar to "Cloud Native Assessment"
- Updated tagline to "Assess your readiness for cloud-native device management"

---

### Version 3.17.85 (January 30, 2026)

### Changed
- **Tool Renamed to "Cloud Native Assessment"** per Rob's feedback
  - Window title: "Cloud Native Assessment"
  - ConfigMgr Console menu: "Cloud Native Assessment"
  - All documentation, scripts, and installers updated
  - Replaced all legacy "Zero Trust Migration Journey" branding (~56 references)
  - Replaced all "Cloud Native Readiness Tool" branding

---

### Version 3.17.84 (January 30, 2026)

### Added
- New `scripts/` folder with helper scripts for testers
- `Reset-AutoUpdate.ps1` - Clears cached manifests and temp files when auto-update fails
- `Get-Diagnostics.ps1` - Shows version info, log locations, and recent activity
- **Stale/Orphaned Device Detection** - Identifies devices in Intune that don't exist in ConfigMgr
  - Logs device name, last Intune sync date, days since sync
  - Provides recommendation to remove from Intune or re-enroll ConfigMgr client
  - Addresses tester feedback about confusing ConfigMgr vs Co-managed count mismatch

---

### Version 3.17.83 (January 29, 2026)

### Changed - Cloud Readiness Signals Updated + Published to GitHub
- Hidden Identity, WUfB, and Endpoint Security readiness signals
- Added new Autopatch Readiness signal
- Published to GitHub releases

---

### Version 3.17.81 (January 29, 2026)

### Changed - Cloud Readiness Signals Updated per Rob's Feedback

**Summary:**
- Hidden Identity, WUfB, and Endpoint Security readiness signals
- Added new **Autopatch Readiness** signal
- Cloud Readiness tab now shows: Autopilot, Cloud-Native, Autopatch (3 signals)

**New Autopatch Readiness Signal:**
Assesses device readiness for Windows Autopatch automated updates.

**What it checks (via Graph API):**
1. **OS Edition** - Enterprise, Education, or Pro required (Home not supported)
2. **Intune Enrollment** - Required for Autopatch policy delivery
3. **Windows Update Workload** - Must be managed by Intune (not ConfigMgr) for co-managed devices
4. **Entra ID Join Status** - Devices must have cloud identity (AAD or Hybrid joined)

**Requirements Research:**
Based on official Microsoft documentation:
- https://learn.microsoft.com/windows/deployment/windows-autopatch/prepare/windows-autopatch-prerequisites
- https://learn.microsoft.com/graph/windowsupdates-concept-overview

**What we CAN check via Graph API:**
- ✅ User/tenant licenses (GET /users/{id}/licenseDetails, GET /subscribedSkus)
- ✅ Tenant MDM auto-enrollment config (GET /policies/mobileDeviceManagementPolicies - beta)
- ✅ Autopatch enrollment status (GET /admin/windows/updates/updatableAssets - beta)
- ✅ OS edition from ConfigMgr device caption
- ✅ Co-management workload authority

**What we CANNOT check:**
- ❌ Windows diagnostic data level (policy config only, not actual device state)
- ❌ Network connectivity to Microsoft Update endpoints

**Files Modified:**
- `Services/CloudReadinessService.cs` - Hidden Identity/WUfB/EndpointSecurity, added GetAutopatchReadinessSignalAsync()
- `Views/CloudReadinessTab.xaml.cs` - Updated demo data to match new signal set

---

---

> 📋 **[View Complete Changelog](#-changelog-highlights)** for all version history


---

## 📊 Dashboard Overview

The Cloud Native Assessment is your intelligent command center for migrating from ConfigMgr to Microsoft Intune. It combines real-time data from **both ConfigMgr and Intune** with AI-powered insights to accelerate your cloud migration journey.

### Key Features

- **Dual-Source Data** from ConfigMgr Admin Service AND Microsoft Graph (Intune)
- **Complete Visibility** - See total eligible devices, not just enrolled ones
- **AI-Powered Recommendations** that prevent stalls and accelerate progress (requires Azure OpenAI GPT-4)
- **Autonomous Enrollment Agent** - AI plans and executes device enrollments with human oversight
- **5 Specialized Tabs** - Overview, Enrollment, Workloads, Applications, Executive
- **Actionable Guidance** with buttons to take immediate action
- **Visual Trends** showing velocity and momentum
- **Real-Time Monitoring** of enrollment progress and agent execution

### At a Glance

**Data Integration:**
- ✅ Device enrollment counts (ConfigMgr + Intune)
- ✅ Co-management status
- ✅ Compliance scores
- ✅ Workload migration status
- ✅ Device health alerts
- ✅ Application migration analysis
- ✅ Enrollment Impact Simulator (100% data-driven predictions)

---

## 💻 System Requirements

### Minimum
- **OS:** Windows 10 (version 1809+) or Windows Server 2019+
- **ConfigMgr:** Console 2103 or later
- **Permissions:** Administrator privileges (script will request elevation)
- **Network:** Internet connection (only for .NET Runtime download if needed)

### Disk Space
- Application: 233MB
- .NET Runtime: 55MB (if not already installed)
- **Total:** ~300MB

### No Pre-Installation Required
- ❌ No need to install .NET manually
- ❌ No need to install Visual Studio or SDK
- ❌ No need to configure paths or environment variables
- ❌ No need to register components
- ❌ No need to modify registry

### ConfigMgr Admin Service Requirements (Optional but Recommended)
- ConfigMgr version CB 1810 or later
- Admin Service enabled on site server
- Full Administrator or Read-only Analyst role
- Network access to site server (HTTPS port 443)
- Falls back to Graph API if unavailable

---

## 🎯 Getting Started - First Time Using the Dashboard

### What You Need to Do (Takes 5 Minutes)

**Step 1: Open the Dashboard**
- Find "Cloud Native Assessment" in your ConfigMgr Console ribbon
- Or double-click the desktop shortcut

**Step 2: Connect to Your Intune Data**
1. Click the green **"🔗 Graph"** button at the top
2. A popup shows a code (like "ABC-DEF-123")
3. Copy that code
4. Open your web browser → go to https://microsoft.com/devicelogin
5. Paste the code and click Next
6. Sign in with your Microsoft 365 admin account
7. Click "Accept" when it asks for permissions
8. Come back to the dashboard - it'll say "Connected" with a green checkmark

**Step 3: Connect to Your ConfigMgr Data (Optional but Recommended)**
1. Click the blue **"🖥️ ConfigMgr"** button at the top
2. If it finds your ConfigMgr automatically: Great! You're done
3. If it asks for your site server: Type it in (like "CM01" or "CM01.contoso.com")
4. It connects automatically using your Windows login

**Step 4: Let It Load**
- Takes 30-60 seconds to load all your data
- You'll see numbers start appearing in the sections
- Green checkmarks at the top = everything's working

**Step 5: Look Around**
- Click through the 5 tabs at the top (Overview, Enrollment, Workloads, Applications, Executive)
- Get familiar with where things are
- Don't worry - you can't break anything by clicking around

### What If Something Doesn't Work?

**"Permission Error" or "Authorization_RequestDenied":**
This means your account doesn't have the required Intune permissions.

**REQUIRED ROLE (one of these):**
- **Intune Administrator** ✅ (Recommended - full Intune access)
- **Global Reader** (Read-only access to all Microsoft 365 services)
- **Global Administrator** (Full access to everything)

**REQUIRED API PERMISSIONS:**
- `DeviceManagementManagedDevices.Read.All` - Read Intune device data
- `DeviceManagementConfiguration.Read.All` - Read Intune configurations
- `DeviceManagementApps.Read.All` - Read app deployment data
- `Directory.Read.All` - Read user/directory information

**HOW TO FIX:**
1. Ask your **Global Administrator** to assign you the **Intune Administrator** role:
   - Go to Entra ID (Azure AD) → Users → [Your Account]
   - Click "Assigned roles" → "Add assignments"
   - Select "Intune Administrator" → Assign
2. Sign out and sign back in to the dashboard
3. The permissions will take effect immediately

**Alternative - Admin Consent (for entire organization):**
A Global Administrator can pre-consent to these permissions for all users:
```
https://login.microsoftonline.com/{tenant-id}/adminconsent?client_id=14d82eec-204b-4c2f-b7e8-296a70dab67e
```
Replace `{tenant-id}` with your actual tenant ID.

**"Connection failed" error (other reasons):**
- Click **🔍 Diagnostics** button (orange) to see what's wrong
- Network connectivity issues
- Azure AD authentication problems

**"No data showing" error:**
- Click **🔄 Refresh** button (blue) to reload
- Check if the Graph and ConfigMgr buttons show green checkmarks
- Click **📋 Logs** button (gray) to see detailed error messages

**"Can't find the dashboard in ConfigMgr Console":**
- The installation might not have worked
- Try running `Install-ZeroTrustMigrationAddin.ps1` again
- Or ask whoever installed it to check

---

## 🖥️ ConfigMgr Admin Service Setup

### Why Connect to ConfigMgr?

**Without ConfigMgr (Graph API only):**
- ❌ Only see devices already enrolled in Intune
- ❌ Can't determine true migration gap
- ❌ Missing co-management workload status
- ❌ Incomplete device inventory

**With ConfigMgr + Graph API:**
- ✅ Complete Windows 10/11 device inventory
- ✅ True migration gap (total - enrolled = remaining)
- ✅ Real co-management workload data
- ✅ Accurate progress metrics
- ✅ Better AI recommendations with full context

### Setup Instructions

#### Step 1: Enable ConfigMgr Admin Service

The Admin Service must be enabled on your ConfigMgr site server.

**Check if it's already enabled:**
1. Open **ConfigMgr Console**
2. Go to **Administration > Site Configuration > Sites**
3. Right-click your site → **Hierarchy Settings**
4. Look for "Administration Service" tab

**If not enabled:**
Follow Microsoft's guide: https://learn.microsoft.com/mem/configmgr/develop/adminservice/overview

#### Step 2: Verify Your Permissions

You need **one of these roles** in ConfigMgr:
- ✅ Full Administrator
- ✅ Read-only Analyst

**To check:**
1. ConfigMgr Console → **Administration > Security > Administrative Users**
2. Find your user account
3. Verify role assignment

#### Step 3: Find Your Admin Service URL

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

#### Step 4: Test the Connection (Optional)

Open PowerShell and run:

```powershell
$url = "https://CM01.contoso.com/AdminService/wmi/SMS_Site"
Invoke-RestMethod -Uri $url -UseDefaultCredentials
```

If you see site information, the Admin Service is working!

### Connection Status

**Click the 🔍 Diagnostics button** to see:

**ConfigMgr Connection Status Shows:**
- **"Admin Service (REST API)"** - ✅ Using preferred method (HTTPS REST API)
  - Fastest and most efficient
  - Modern authentication
  - Recommended method

- **"WMI Fallback (ConfigMgr SDK)"** - ⚠️ Using fallback method
  - Admin Service failed or unavailable
  - Automatically fell back to WMI queries
  - Still functional but slightly slower

- **"None"** - ❌ Not connected to ConfigMgr
  - Device counts incomplete (Intune-only)
  - Need to fix connection issues

**Why Admin Service Might Fail:**
1. Admin Service not enabled on site server
2. HTTPS certificate issues
3. Firewall blocking port 443
4. Insufficient permissions
5. Site server not detected properly

---

## 🤖 Azure OpenAI Setup (Required for AI Recommendations)

### GPT-4 Powered Recommendations

Azure OpenAI (GPT-4) provides intelligent, context-aware recommendations focused on the two most critical success factors for migration:

**What GPT-4 Analyzes:**
- 🎯 **Device Enrollment Progress** - Velocity trends, acceleration strategies, batch planning
- 📋 **Workload Transition Planning** - Optimal sequencing, completion guidance, risk assessment
- 🧠 **Stall Detection & Recovery** - Root cause analysis when progress stops >30 days
- 📊 **Contextual Intelligence** - Incorporates your phased plan, velocity data, Microsoft FastTrack best practices

**Technical Features:**
- 🤖 **Single Comprehensive Analysis** - One GPT-4 call analyzes complete migration state
- ⚡ **Smart Caching** - 30-minute response caching reduces costs by ~65%
- 💰 **Cost Efficient** - ~$0.03-0.05 per recommendation vs multiple separate calls
- 🎨 **No Rule-Based Fallback** - Pure GPT-4 intelligence (shows setup instructions if not configured)

### 🔒 Data Privacy & What's Sent to Azure OpenAI

**Complete Transparency:** We believe you should know exactly what data is shared with AI services.

**Data Sent to Azure OpenAI (Aggregated Metrics Only):**
```
MIGRATION STATE:
- Total Devices: 500
- Intune Enrolled: 120 (24%)
- ConfigMgr Only: 380
- Days Since Last Progress: 45
- Stalled: YES

WORKLOAD STATUS:
- Completed: 2/7 (Compliance Policies, Endpoint Protection)
- In Progress: 1 (Device Configuration)
- Not Started: 4

VELOCITY & TRENDS:
- Enrollment velocity has slowed in past 30 days

MIGRATION PLAN:
- Phase 2: Pilot Expansion (25-50% enrollment target)
```

**What is NOT Sent (Privacy Protected):**
- ❌ Device names, hostnames, computer names
- ❌ User names, email addresses, identities
- ❌ IP addresses or network information
- ❌ Serial numbers or hardware IDs
- ❌ Organization/tenant names
- ❌ Configuration details or policies
- ❌ Any personally identifiable information (PII)

**Privacy Safeguards:**
1. **Aggregated Only** - Only statistical summaries (counts, percentages, generic names)
2. **Your Azure Instance** - Data goes to YOUR Azure OpenAI (not shared with others)
3. **No Training** - Azure OpenAI doesn't use your data to train models ([Microsoft Data Privacy](https://learn.microsoft.com/legal/cognitive-services/openai/data-privacy))
4. **Local Caching** - Responses cached locally (30 min) to minimize calls
5. **Audit Logging** - All API calls logged to `%APPDATA%\ZeroTrustMigrationAddin\logs`

### Azure Setup Required

1. **Azure subscription** with Azure OpenAI access
2. **GPT-4o deployment** (recommended) or GPT-4
3. Estimated cost: ~$1,200/month for 1000 users with caching

### Configuration Steps

#### Option 1: Use AI Settings Dialog (Recommended)

1. Click **🤖 AI Settings** button in dashboard
2. Enter your Azure OpenAI credentials:
   - **Endpoint:** `https://YOUR-RESOURCE.openai.azure.com/`
   - **Deployment Name:** Your GPT-4 deployment name
   - **API Key:** Your Azure OpenAI API key
3. Click **Test Connection** to verify
4. Click **Save** to store configuration

Configuration saved to: `%APPDATA%\ZeroTrustMigrationAddin\openai-config.json`

#### Option 2: Environment Variables

Set these environment variables:
```powershell
$env:AZURE_OPENAI_ENDPOINT = "https://YOUR-RESOURCE.openai.azure.com/"
$env:AZURE_OPENAI_DEPLOYMENT = "gpt-4o"
$env:AZURE_OPENAI_KEY = "your-api-key-here"
```

### Azure Portal Setup

1. **Create Azure OpenAI Resource:**
   - Go to https://portal.azure.com
   - Create new resource → Search "Azure OpenAI"
   - Choose region (East US, West Europe, etc.)
   - Select pricing tier (Standard S0)

2. **Deploy GPT-4 Model:**
   - Go to your Azure OpenAI resource
   - Navigate to "Model deployments"
   - Click "Create new deployment"
   - Select "gpt-4" or "gpt-4o" (preferred)
   - Give it a name (e.g., "gpt-4o-deployment")

3. **Get API Key:**
   - In Azure OpenAI resource → "Keys and Endpoint"
   - Copy "KEY 1" or "KEY 2"
   - Copy "Endpoint" URL

### Cost Estimates

**With Smart Caching (30-minute TTL):**
- Enrollment momentum analysis: ~$0.01-0.02 per query
- Stall analysis: ~$0.03-0.05 per query
- Agent planning: ~$0.05-0.10 per plan

**Typical Monthly Usage (1000 users):**
- Daily momentum checks: ~$20/month
- Weekly stall detection: ~$10/month
- Agent plan generation: ~$50/month
- **Total:** ~$80-150/month

### Using Without Azure OpenAI

The dashboard works fully without Azure OpenAI:
- AI Recommendations section shows setup instructions (not recommendations)
- All other features remain functional (enrollment data, workloads, compliance, alerts)
- Configure Azure OpenAI anytime to enable GPT-4 powered recommendations
- Estimated setup time: 5-10 minutes

---

## � How to Use the Dashboard

### The Dashboard Has 5 Tabs - Here's What Each One Does

#### 📊 Tab 1: Overview
**See your overall migration progress**

What you'll find here:
- **Device counts** - How many devices are enrolled in Intune vs still on ConfigMgr
- **Enrollment automation** - Let AI plan and execute device enrollments automatically
- **Migration timeline** - Week-by-week plan to complete your migration
- **Compliance status** - Are your devices meeting security requirements?
- **Alerts** - Things that need your attention right now

**What to do:** This is your starting point. Check it daily to see progress and handle any alerts.

#### 📱 Tab 2: Enrollment
**Speed up device enrollments**

What you'll find here:
- **Progress ring** - Visual showing how many devices are enrolled
- **Enrollment timeline** - Current phase of your enrollment journey
- **Device readiness** - Which devices are ready to enroll right now
- **AI recommendations** - Smart suggestions to speed things up (optional)

**What to do:** Use the Autonomous Agent to automatically enroll ready devices, or manually select batches to enroll.

#### 🔄 Tab 3: Workloads
**Migrate management responsibilities to Intune**

What you'll find here:
- **7 workload areas** - Compliance, Configuration, Updates, Protection, etc.
- **Status for each** - Which ones are done, in progress, or not started
- **Velocity tracking** - How fast you're making progress

**What to do:** Work through each workload one at a time. Click "Start" for guidance on how to migrate that workload.

#### 📦 Tab 4: Applications
**Plan your app migration**

What you'll find here:
- **App inventory** - All your ConfigMgr applications
- **Complexity scores** - Which apps are easy vs hard to migrate
- **Migration recommendations** - Best way to move each app to Intune

**What to do:** Start with low-complexity apps (easiest wins), then tackle harder ones.

#### 📊 Tab 5: Executive
**High-level metrics for leadership**

What you'll find here:
- **Overall completion %** - How far along you are
- **Enrollment progress** - Devices migrated over time
- **Workload status** - Which management workloads are complete
- **Milestones** - Major achievements you've completed

**What to do:** Use this for status reports to management. Shows the big picture.

---

## 🎯 Understanding What You See

### Device Counts (The Numbers)

**Total Windows 10/11 Devices** = All your Windows 10/11 computers  
**Intune-Enrolled** = Computers already moved to cloud management  
**ConfigMgr-Only** = Computers still waiting to migrate  

**What counts?** Only Windows 10 version 1607+ and Windows 11. Servers, older Windows, Macs, phones don't count (they're managed differently).

### The Autonomous Enrollment Agent (The Robot)

This is your AI assistant that can automatically enroll devices:

**How it works:**
1. Tell it your goal (e.g., "Enroll 100 devices by next Friday")
2. It creates a plan with specific batches and timing
3. You review and approve the plan (one button click)
4. It executes automatically - you just monitor progress
5. Hit the STOP button if you need to pause

**Safety features:**
- Only enrolls "ready" devices (scored 60+ out of 100)
- Small batches (25-50 devices at a time)
- Pauses if too many failures
- You approve every plan before it runs
- Emergency stop button always available

### Device Readiness Scores (The Grades)

Each device gets a score from 0-100 based on how ready it is to enroll:

- **80-100 (Excellent)** ✅ - Perfect! Enroll these now
- **60-79 (Good)** 👍 - Ready to go, minor things to watch
- **40-59 (Fair)** ⚠️ - Needs some prep work first
- **0-39 (Poor)** ❌ - Not ready, fix issues first

**What makes a device "ready"?**
- Has Windows 10/11 (not Windows 7)
- Joined to Azure AD (cloud identity)
- Online recently (last 7 days)
- Compliant with policies

### Workload Status (The Colored Dots)

Each workload shows a status color:

- 🟢 **Green = Done** - This workload is fully migrated to Intune
- 🟡 **Yellow = In Progress** - Migration started but not finished
- 🔴 **Red = Not Started** - Still using ConfigMgr for this workload
- 🔵 **Blue = Pilot** - Testing with a few devices first

**The 7 workloads you'll migrate:**
1. Compliance Policies (security requirements)
2. Device Configuration (settings and profiles)
3. Windows Updates (patch management)
4. Endpoint Protection (antivirus, firewall)
5. Resource Access (VPN, WiFi, email)
6. Office Click-to-Run (Microsoft 365 apps)
7. Client Apps (application deployment)

### Alert Colors (The Warnings)

Alerts show different colors based on urgency:

- 🔴 **Red = Critical** - Deal with this today (devices offline, compliance dropped, migration stalled)
- 🟡 **Yellow = Warning** - Address this week (non-compliant devices, running low on licenses)
- 🔵 **Blue = Info** - Good news or FYI (new enrollments, milestones reached)

---

## 🚀 Common Tasks - How to Actually Use This Thing

### Task 1: Enroll Your First Batch of Devices

**The Easy Way (Using the Agent):**
1. Go to **Overview tab**
2. Scroll to **Smart Enrollment Management** section
3. Click **Enable Agent** toggle
4. Set your goal (e.g., "100 devices by end of month")
5. Click **✨ Generate Plan**
6. Review the plan (shows which devices, when, how many)
7. Click **✅ Approve & Start**
8. Watch it go! Monitor the progress section

**The Manual Way:**
1. Go to **Enrollment tab**
2. Look at **Device Readiness** section
3. See the count of "Excellent" and "Good" devices
4. Click **📋 Export Device List** to get names
5. Manually enroll those devices through Intune (outside this tool)

### Task 2: Migrate a Workload (e.g., Compliance Policies)

1. Go to **Workloads tab**
2. Find **Compliance Policies** (should be first)
3. If it's red, click **Start** button
4. This opens Microsoft Learn docs with step-by-step instructions
5. Follow those instructions in Intune admin center
6. Come back to dashboard and click **🔄 Refresh**
7. Workload should now show green if policies are detected

### Task 3: Check if You're On Track

1. Go to **Executive tab**
2. Look at **Overall Migration Status**
3. See your completion % and projected finish date
4. If you're behind schedule, go back to Overview tab and check alerts for guidance

### Task 4: Handle an Alert

1. Go to **Overview tab**
2. Scroll to **Alerts & Recommendations** section
3. Click on any red or yellow alert
4. Read the description of the problem
5. Click the button shown (e.g., "Fix", "View Details", "Take Action")
6. Follow the guidance provided
7. After fixing, click **🔄 Refresh** to see if alert clears

### Task 5: Plan App Migration

1. Go to **Applications tab**
2. See your apps sorted by complexity
3. **Start with green (Low complexity)** - These are easiest
4. Click on an app to see migration recommendation
5. Follow the suggested approach (e.g., "Use Intune's built-in Office 365 deployment")
6. After migrating an app, come back and check off the next one

---

## 💡 Tips & Tricks

### For Your First Week

**Day 1:** Connect to Graph and ConfigMgr, let data load, just look around  
**Day 2:** Check Overview tab daily - get familiar with your numbers  
**Day 3:** Try the Autonomous Agent with 10 test devices  
**Day 4:** Migrate your first workload (Compliance Policies - easiest one)  
**Day 5:** Show the Executive tab to your manager

### Speed Up Your Migration

1. **Use the Agent** - It's way faster than manual enrollment
2. **Start with high-readiness devices** - 80+ scores are slam dunks
3. **Do workloads in order** - Compliance → Configuration → Updates → Protection → Resource Access → Office → Apps
4. **Batch wisely** - 25-50 devices at a time is the sweet spot
5. **Check alerts daily** - Small problems become big problems fast

### If Things Go Wrong

- **Click 📋 Logs** - See exactly what happened
- **Click 🔍 Diagnostics** - Check if you're still connected to everything
- **Hit 🛑 STOP** - If the Agent is enrolling devices and something's wrong
- **Click 🔄 Refresh** - Sometimes data just needs to update
- **Check alerts** - They often tell you exactly what's wrong

---

## ❓ Quick Questions Answered

**Q: Do I need Azure OpenAI for AI recommendations?**  
A: Yes - AI-Powered Recommendations require Azure OpenAI (GPT-4) to be configured. Without it, the dashboard will show setup instructions. All other features (device enrollment data, workload status, compliance, alerts) work without Azure OpenAI. The AI recommendations are a premium feature that provides context-aware guidance.

**Q: What data is sent to Azure OpenAI? Is it private?**  
A: Only aggregated migration metrics (device counts, percentages, workload names, days since progress). NO device names, user names, IP addresses, or any personally identifiable information (PII) is sent. Data goes to YOUR Azure OpenAI instance only. Microsoft doesn't use your data to train models. All API calls are logged locally for audit.

**Q: Will the Agent break my environment?**  
A: No - it has multiple safety checks. It only enrolls "ready" devices, does small batches, requires your approval, pauses on failures, and you can stop it anytime.

**Q: How do I know if my data is real or fake?**  
A: After you connect to Graph and ConfigMgr, all data is real from YOUR environment - device counts, compliance, workload status, alerts, and the Enrollment Impact Simulator results. The status bar shows your current data source (Graph, ConfigMgr, or Demo mode).

**Q: What if I don't have ConfigMgr Admin Service?**  
A: The tool automatically falls back to Graph API only. You'll see devices already enrolled in Intune, but won't see the complete count of devices still on ConfigMgr. Still useful, just less complete.

**Q: How often should I check this?**  
A: Daily for Overview tab (5 minutes). Weekly for Workloads and Executive tabs (15 minutes). Apps tab as needed when planning migrations.

**Q: Can I break something by clicking buttons?**  
A: Buttons either open documentation (safe), show information (safe), export lists (safe), or let you approve agent plans (requires your approval first). No "delete all devices" buttons here.

**Q: Where can I get help?**  
A: Click the **📖 Guide** button in the top toolbar - opens full documentation. Click **📋 Logs** to see what's happening under the hood. Click **🔍 Diagnostics** to check connections.

---

## 📦 Automated Installation (Recommended)

### One-Command Install

```powershell
.\Install-ZeroTrustMigrationAddin.ps1
```

The automated installer handles everything:
- ✓ Checks for administrator privileges (elevates if needed)
- ✓ Detects ConfigMgr Console installation
- ✓ Downloads and installs .NET 8.0 Runtime if missing
- ✓ Builds application with all dependencies
- ✓ Deploys to ConfigMgr Console
- ✓ Validates installation
- ✓ Creates uninstaller

**Expected Time:** 2-5 minutes

### Installation Verification

After installation:
1. Launch ConfigMgr Console
2. Look for "Cloud Native Assessment" in ribbon/toolbar
3. Click to open dashboard

---

## 🔧 Manual Installation

If automated installation fails, use manual steps:

### Step 1: Build the Project

```powershell
cd "c:\Users\dannygu\Downloads\GitHub Copilot\cmaddin"
dotnet build -c Release
```

### Step 2: Deploy to ConfigMgr Console

Copy XML manifest:
```powershell
$extensionsPath = "${env:ProgramFiles(x86)}\Microsoft Configuration Manager\AdminConsole\XmlStorage\Extensions\Actions"
Copy-Item "ZeroTrustMigrationAddin.xml" -Destination $extensionsPath
```

Copy executable and dependencies:
```powershell
$binPath = "${env:ProgramFiles(x86)}\Microsoft Configuration Manager\AdminConsole\bin"
Copy-Item "bin\Release\net8.0-windows\*" -Destination $binPath -Recurse -Force
```

### Step 3: Restart ConfigMgr Console

Close and reopen the ConfigMgr Console.

---

## 💾 Offline Installation

For machines without internet access:

### Step 1: Download Prerequisites (on connected machine)

```powershell
# Download .NET Runtime installer
$url = "https://download.visualstudio.microsoft.com/download/pr/6224f00f-08da-4e7f-85b1-00d42c2bb3d3/b775de636b91e023574a0bbc291f705a/windowsdesktop-runtime-8.0.11-win-x64.exe"
Invoke-WebRequest -Uri $url -OutFile "windowsdesktop-runtime-8.0.11-win-x64.exe"
```

### Step 2: On offline machine

1. Copy application folder and .NET Runtime installer
2. Install .NET Runtime manually:
   ```powershell
   .\windowsdesktop-runtime-8.0.11-win-x64.exe /install /quiet
   ```
3. Run installer:
   ```powershell
   .\Install-ZeroTrustMigrationAddin.ps1 -SkipBuild
   ```

---

## 🗑️ Uninstallation

After installation, an uninstaller is automatically created:

```powershell
.\Uninstall-ZeroTrustMigrationAddin.ps1
```

The uninstaller removes:
- All application files from ConfigMgr Console
- XML manifest
- Desktop and Start Menu shortcuts
- Does NOT remove .NET Runtime (other apps may use it)

---

## 🛠️ Troubleshooting

### Common Issues

#### "Script is not digitally signed"
Run PowerShell as Administrator:
```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

#### "Add-in not appearing in Console"
- Verify XML file is in Extensions folder
- Check XML format is valid
- Ensure ConfigMgr Console restarted
- Check Windows Event Viewer for errors

#### "Data not loading"
- Verify Graph API authentication completed
- Check Tenant Attach configured in ConfigMgr
- Test Graph API connectivity manually
- Click 🔍 Diagnostics button for connection status
- Click 📋 Open Logs button to view detailed logs

#### "Zero device counts from ConfigMgr"
- Verify Admin Service enabled (ConfigMgr 1810+)
- Check HTTPS certificate valid on site server
- Verify firewall allows port 443
- Check permissions (Full Admin or Read-only Analyst required)
- View logs to see if WMI fallback engaged

#### "Chart display issues"
- Verify LiveCharts.Wpf package installed
- Check .NET runtime errors in Event Viewer
- Ensure all DLLs in bin folder

#### "Azure.Identity.dll not found"
- Ensure complete package extracted (all 489 files)
- Run `.\Diagnose-Installation.ps1` to check for missing DLLs
- Rebuild and redeploy complete package

### Diagnostic Tools

#### Open Logs Button (📋)
Click to open log directory: `%LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs\`

**Log Files:**
- `ZeroTrustMigrationAddin_YYYYMMDD.log` - Daily log file
- Automatic cleanup (keeps last 7 days)
- Timestamped entries with log levels

**What Gets Logged:**
- Application startup/shutdown
- Microsoft Graph authentication
- ConfigMgr connection attempts
- API operations with result counts
- Exceptions with stack traces
- HTTP requests/responses

#### Diagnostics Button (🔍)
Click to view:
- Microsoft Graph connection status
- ConfigMgr connection status (Admin Service vs WMI)
- Data source for each metric
- Error details
- API endpoint health

#### Diagnose-Installation.ps1
Run to check:
- .NET Runtime installed
- All required DLLs present
- ConfigMgr Console detected
- File permissions
- Registry keys

```powershell
.\Diagnose-Installation.ps1
```

---

## 👨‍💻 Architecture

### MVVM Pattern

The application follows Model-View-ViewModel pattern:

**Models** - Data structures
- `DashboardModels.cs` - Dashboard entities
- `AgentModels.cs` - Enrollment agent data
- `EnrollmentPlan.cs`, `EnrollmentGoals.cs`, `EnrollmentProgress.cs`

**Views** - WPF XAML UI
- `DashboardWindow.xaml` - Main dashboard with TabControl
- 5 focused tabs: Overview, Enrollment, Workloads, Applications, Executive

**ViewModels** - Business logic
- `DashboardViewModel.cs` - Main dashboard logic and commands
- Data binding and property change notifications

**Services** - Data retrieval and integration
- `TelemetryService.cs` - Main data service
- `AzureOpenAIService.cs` - GPT-4 integration
- `EnrollmentMomentumService.cs` - Velocity analysis
- `AutonomousEnrollmentService.cs` - Agent orchestration
- `AgentMemoryService.cs` - Agent state management

**Converters** - Data binding
- `ValueConverters.cs` - UI value conversions
- `RecommendationPriorityConverter.cs` - Priority color mapping

---

## 📁 Project Structure

```
ZeroTrustMigrationAddin/
├── Models/
│   ├── DashboardModels.cs          # Dashboard data structures
│   ├── AgentModels.cs              # Enrollment agent models
│   ├── EnrollmentPlan.cs           # Agent plan structure
│   ├── EnrollmentGoals.cs          # Agent goal configuration
│   └── EnrollmentProgress.cs       # Agent progress tracking
├── Services/
│   ├── TelemetryService.cs         # Main data service
│   ├── AzureOpenAIService.cs       # GPT-4 integration
│   ├── EnrollmentMomentumService.cs # Velocity analysis
│   ├── AutonomousEnrollmentService.cs # Agent orchestration
│   └── AgentMemoryService.cs       # Agent state persistence
├── ViewModels/
│   ├── ViewModelBase.cs            # Base with INotifyPropertyChanged
│   └── DashboardViewModel.cs       # Main dashboard logic
├── Views/
│   ├── DashboardWindow.xaml        # Main UI with 5 tabs
│   └── DashboardWindow.xaml.cs     # Code-behind
├── Converters/
│   ├── ValueConverters.cs          # Data binding converters
│   └── RecommendationPriorityConverter.cs # Priority colors
├── App.xaml                         # Application resources
├── App.xaml.cs                      # Application entry point
├── ZeroTrustMigrationAddin.xml           # ConfigMgr manifest
└── ZeroTrustMigrationAddin.csproj        # Project file
```

---

## 🔨 Development Setup

### Prerequisites

- Visual Studio 2022 or VS Code
- .NET 8.0 SDK
- Windows 10/11 or Windows Server 2019+
- ConfigMgr Console (for integration testing)

### Clone and Build

```powershell
git clone https://github.com/sccmavenger/cmaddin.git
cd cmaddin
dotnet restore
dotnet build -c Release
```

### Run Locally (Standalone Mode)

```powershell
dotnet run
```

This launches dashboard as standalone window with mock data.

### Testing with ConfigMgr Integration

Use automated installer:
```powershell
.\Install-ZeroTrustMigrationAddin.ps1
```

Or manual deployment:
```powershell
# Build
dotnet build -c Release

# Deploy
$extensionsPath = "${env:ProgramFiles(x86)}\Microsoft Configuration Manager\AdminConsole\XmlStorage\Extensions\Actions"
$binPath = "${env:ProgramFiles(x86)}\Microsoft Configuration Manager\AdminConsole\bin"

Copy-Item "ZeroTrustMigrationAddin.xml" -Destination $extensionsPath -Force
Copy-Item "bin\Release\net8.0-windows\*" -Destination $binPath -Recurse -Force

# Restart ConfigMgr Console
```

---

##  Data Sources Reference

### Real Data (From Your Tenant)

#### 1. Device Enrollment (Dual-Source)

**Primary Source:** ConfigMgr Admin Service
```http
GET https://[SiteServer]/AdminService/wmi/SMS_R_System
?$filter=contains(OperatingSystemNameandVersion,'Windows NT Workstation 10')
```

**Secondary Source:** Microsoft Graph API
```http
GET https://graph.microsoft.com/v1.0/deviceManagement/managedDevices
```

**Properties Accessed:**
- `managementAgent` - MDM, ConfigMgr, Co-managed
- `enrolledDateTime` - Enrollment date
- `deviceName`, `operatingSystem` - Device info
- `azureADDeviceId` - Cloud identity

**Device Counting:**
```
Total Win10/11 = ConfigMgr (all workstations)
Intune-Enrolled = Graph (MDM + co-managed)
ConfigMgr-Only = Total - Enrolled
```

#### 2. Compliance Scorecard

**Source:** Microsoft Graph API
```http
GET https://graph.microsoft.com/v1.0/deviceManagement/deviceCompliancePolicies
GET https://graph.microsoft.com/v1.0/deviceManagement/managedDevices?$select=complianceState
```

**Properties:**
- `complianceState` - Compliant, NonCompliant, InGracePeriod
- `deviceCompliancePolicies.displayName` - Policy names

**Calculation:**
```
ComplianceRate = (CompliantDevices / TotalDevices) * 100
```

#### 3. Workload Status

**Source:** Microsoft Graph API + ConfigMgr

**Detection Logic:**
- **Compliance:** Query `DeviceManagement.DeviceCompliancePolicies`
- **Device Configuration:** Query `DeviceManagement.DeviceConfigurations`
- **Client Apps:** Query `DeviceAppManagement.ManagedAppPolicies`
- **Co-Management Sliders:** Query ConfigMgr workload authority

**If policies found:** Workload marked "Completed"

#### 4. Alerts & Recommendations

**Source:** Analyzed from tenant data

**Real Alerts:**
- Devices not synced 7+ days (actual device list)
- Recent enrollments (last 7 days, actual count)
- Non-compliant devices (actual affected devices)
- Policy deployment status
- ConfigMgr-only devices needing enrollment

### Data Access Permissions

**Microsoft Graph API:**
- `DeviceManagementManagedDevices.Read.All`
- `DeviceManagementConfiguration.Read.All`
- `DeviceManagementApps.Read.All`
- `Directory.Read.All` (user verification)

**ConfigMgr:**
- Full Administrator role
- Or Read-only Analyst role
- Admin Service enabled (CB 1810+)

---

## 📝 Changelog Highlights

### Version 2.5.0 (December 21, 2025)
- Dual-source integration (ConfigMgr + Graph)
- Tab reorganization (Workloads/Executive moved)
- Auto-detection of ConfigMgr console and site server
- Enhanced diagnostics showing data sources

### Version 2.4.2 (December 22, 2025)
- Fixed GPT-4 JSON response parsing

### Version 2.4.0 (December 20, 2025)
- Merged Device Readiness + Enrollment Agent
- Progressive disclosure UI
- Cross-reference between sections

### Version 2.0.0 (December 19, 2025)
- Autonomous Enrollment Agent (AI-powered)
- Device readiness scoring (0-100)
- Automated planning with GPT-4
- Safety controls (emergency stop, rollback)
- Agent reasoning panel

### Version 1.7.0 (December 18, 2025)
- 5 specialized tabs
- Enrollment momentum (GPT-4 velocity analysis)
- Horizontal button layout
- Batch size recommendations

### Version 1.6.0 (December, 2025)
- Application migration intelligence
- Complexity scoring (0-100)
- Migration path recommendations
- Effort estimation
- WQL to Azure AD translation

### Version 1.5.0 (December, 2025)
- Phased migration planner
- Device selection intelligence
- Workload trend tracking
- AI-powered recommendations

### Version 1.4.0 (December, 2025)
- Strict enrollment blocker detection
- Legacy OS detection
- Azure AD join check
- Green success state

### Version 1.3.10 (December, 2025)
- Fixed OData v4 query syntax for Admin Service
- Windows 10/11 device filtering works correctly

### Version 1.3.9 (December, 2025)
- File-based logging system
- Open Logs button
- 7-day automatic cleanup

### Version 1.3.8 (December, 2025)
- Zero tolerance for mock data after authentication
- Honest empty states
- Data source transparency

### Version 1.2.2 (December, 2025)
- Windows 10/11 only filtering
- Multi-OS filtering (excludes macOS, iOS, Android, Linux)
- Legacy Windows excluded (7/8/8.1)
- Server filtering (Windows Server)

### Version 1.2.0 (December, 2025)
- AI-powered recommendations engine
- Priority-based guidance
- Stall prevention
- Enrollment momentum focus

[See CHANGELOG.md for complete version history]

---

## 📄 License

Microsoft Internal Use

---

## 🆘 Support & Resources

### Documentation Files (Archived)

Historical documentation moved to `/documents` folder:
- Configuration Manager Customer - Cloud Native Assessment.docx
- Configuration Manager Customer - Middle-Stage.docx
- Configuration Manager Middle View.docx

### Getting Help

1. **Check Logs:** Click 📋 Open Logs button
2. **Run Diagnostics:** Click 🔍 Diagnostics button
3. **View This README:** Comprehensive guide for all scenarios
4. **Contact:** [Support information]

### External Resources

- **Microsoft Graph API:** https://learn.microsoft.com/graph/api/overview
- **ConfigMgr Admin Service:** https://learn.microsoft.com/mem/configmgr/develop/adminservice/overview
- **Co-Management:** https://learn.microsoft.com/mem/configmgr/comanage/overview
- **Azure OpenAI:** https://learn.microsoft.com/azure/ai-services/openai/
- **Intune Documentation:** https://learn.microsoft.com/mem/intune/

---

**Last Updated**: 2026-01-30  
**Version**: 3.17.87  
**Maintainer:** Cloud Native Assessment Team
