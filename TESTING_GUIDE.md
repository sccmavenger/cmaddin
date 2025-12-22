# 🧪 Step-by-Step Testing Instructions

## Testing the Cloud Journey Progress Add-in on ConfigMgr Console

---

## Option A: Automated Installation (Recommended)

### Step 1: Prepare
Open PowerShell **as Administrator** and navigate to the add-in folder:
```powershell
cd "C:\Users\dannygu\Downloads\GitHub Copilot\cmaddin"
```

### Step 2: Run the Automated Installer
```powershell
.\Install-CloudJourneyAddin.ps1
```

The installer will:
- Check for administrator privileges
- Detect your ConfigMgr Console installation
- Install .NET 8.0 Runtime if needed
- Build and deploy the add-in
- Validate everything

**Expected Time:** 2-5 minutes

### Step 3: Launch ConfigMgr Console
After installation completes:
1. Start Menu → Search for "Configuration Manager Console"
2. Launch it
3. Look for **"Cloud Journey Progress"** in the ribbon/toolbar
4. Click it to open the dashboard

---

## Option B: Manual Testing (If No ConfigMgr Console on This Machine)

If ConfigMgr Console is not installed on this machine, you have two options:

### Option B1: Test on a Different Machine

**1. Create a deployment package:**
```powershell
cd "C:\Users\dannygu\Downloads\GitHub Copilot\cmaddin"
.\Build-Standalone.ps1 -CreateZip
```

This creates: `bin\CloudJourneyAddin-Standalone.zip` (~233 MB)

**2. Copy to target machine:**
- Copy the ZIP file to a machine with ConfigMgr Console
- Extract it
- Run `Install-CloudJourneyAddin.ps1` as Administrator

### Option B2: Install ConfigMgr Console on This Machine

You would need to install the ConfigMgr Console from your ConfigMgr site server or installation media.

---

## Option C: Quick Test Without Full Installation (Local Testing)

If you just want to test the dashboard UI without ConfigMgr integration:

### Step 1: Run the Application Directly
```powershell
cd "C:\Users\dannygu\Downloads\GitHub Copilot\cmaddin"

# If not already built, build it first:
.\Build-Standalone.ps1

# Run the application directly:
.\bin\Release\net8.0-windows\win-x64\publish\CloudJourneyAddin.exe
```

This will launch the dashboard as a standalone window with placeholder data.

**Note:** This won't integrate with ConfigMgr Console, but you can see the full UI and all features.

---

## Detailed Manual Installation Steps (If Needed)

If the automated installer has issues, here's the manual process:

### Step 1: Build the Self-Contained Package
```powershell
cd "C:\Users\dannygu\Downloads\GitHub Copilot\cmaddin"
.\Build-Standalone.ps1
```

### Step 2: Find ConfigMgr Console Path
```powershell
# Common locations:
# C:\Program Files (x86)\Microsoft Configuration Manager\AdminConsole
# C:\Program Files\Microsoft Configuration Manager\AdminConsole

# Test if it exists:
Test-Path "${env:ProgramFiles(x86)}\Microsoft Configuration Manager\AdminConsole\bin\Microsoft.ConfigurationManagement.exe"
```

### Step 3: Close ConfigMgr Console (If Running)
```powershell
Get-Process -Name "Microsoft.ConfigurationManagement" -ErrorAction SilentlyContinue | Stop-Process -Force
```

### Step 4: Deploy XML Manifest
```powershell
# Set your ConfigMgr path
$configMgrPath = "${env:ProgramFiles(x86)}\Microsoft Configuration Manager\AdminConsole"

# Create extensions folder if needed
$extensionsPath = "$configMgrPath\XmlStorage\Extensions\Actions"
New-Item -ItemType Directory -Path $extensionsPath -Force -ErrorAction SilentlyContinue

# Copy XML manifest
Copy-Item "CloudJourneyAddin.xml" -Destination "$extensionsPath\CloudJourneyAddin.xml" -Force
```

### Step 5: Deploy Application Files
```powershell
# Create add-in folder
$addInPath = "$configMgrPath\bin\CloudJourneyAddin"
New-Item -ItemType Directory -Path $addInPath -Force -ErrorAction SilentlyContinue

# Copy all published files
$publishPath = "bin\Release\net8.0-windows\win-x64\publish"
Copy-Item "$publishPath\*" -Destination $addInPath -Recurse -Force
```

### Step 6: Update XML Manifest Path
```powershell
$xmlPath = "$extensionsPath\CloudJourneyAddin.xml"
$xml = Get-Content $xmlPath -Raw
$xml = $xml -replace '<FilePath>CloudJourneyAddin\.exe</FilePath>', '<FilePath>CloudJourneyAddin\CloudJourneyAddin.exe</FilePath>'
Set-Content -Path $xmlPath -Value $xml
```

### Step 7: Launch ConfigMgr Console
- Start the Configuration Manager Console
- Look for "Cloud Journey Progress" in the ribbon

---

## Verification Checklist

After installation, verify these files exist:

### XML Manifest:
```powershell
Test-Path "${env:ProgramFiles(x86)}\Microsoft Configuration Manager\AdminConsole\XmlStorage\Extensions\Actions\CloudJourneyAddin.xml"
```

### Application Files:
```powershell
$binPath = "${env:ProgramFiles(x86)}\Microsoft Configuration Manager\AdminConsole\bin\CloudJourneyAddin"
Test-Path "$binPath\CloudJourneyAddin.exe"
Test-Path "$binPath\CloudJourneyAddin.dll"
Test-Path "$binPath\coreclr.dll"  # .NET Runtime
```

If all return `True`, installation is successful.

---

## Testing the Dashboard

Once the ConfigMgr Console is open:

### 1. Locate the Add-in
- Look in the ribbon/toolbar for **"Cloud Journey Progress"** or **"Cloud Journey Dashboard"**
- It may appear in:
  - Home tab
  - Assets and Compliance workspace
  - Right-click context menu (on some nodes)

### 2. Open the Dashboard
- Click the "Cloud Journey Progress" button
- The dashboard window should open

### 3. Verify Dashboard Sections
The dashboard should display all 10 sections with placeholder data:

✓ Overall Migration Status (progress bar showing 3 of 7 workloads)
✓ Device Enrollment (trend chart showing 6 months)
✓ Workload Status (7 workloads with status badges)
✓ Security & Compliance Scorecard (comparison chart)
✓ Peer Benchmarking (percentile ranking)
✓ ROI & Savings (cost projections)
✓ Alerts & Recommendations (colored alert cards)
✓ Recent Milestones (achievement timeline)
✓ Blockers & Health Indicators (issue list with remediation)
✓ Get Help & Resources (engagement options)

### 4. Test Interactive Features
- Click **"Refresh"** button (top-right) - should reload data
- Click **"Start"** button on workloads - should open browser to documentation
- Click **"Learn More"** buttons - should open relevant Microsoft Docs
- Click **"Fix"** buttons on blockers - should open remediation guides
- Click engagement option cards - should open FastTrack, community, or docs

### 5. Verify Charts
- Enrollment trend chart should show a line graph with two series
- Compliance scorecard should show column chart comparing scores

---

## Troubleshooting

### "Add-in doesn't appear in ConfigMgr Console"

**Check 1:** Verify files are in the right location
```powershell
$consolePath = "${env:ProgramFiles(x86)}\Microsoft Configuration Manager\AdminConsole"
Get-ChildItem "$consolePath\XmlStorage\Extensions\Actions" | Where-Object { $_.Name -like "*Cloud*" }
Get-ChildItem "$consolePath\bin\CloudJourneyAddin" | Select-Object -First 5
```

**Check 2:** Completely close and restart ConfigMgr Console
- Use Task Manager to ensure no `Microsoft.ConfigurationManagement.exe` processes are running
- Restart the console

**Check 3:** Check XML manifest syntax
```powershell
[xml](Get-Content "$consolePath\XmlStorage\Extensions\Actions\CloudJourneyAddin.xml")
```
Should load without errors.

### "Application won't launch"

**Check 1:** Verify .NET 8.0 Runtime is installed (only needed if not using self-contained)
```powershell
dotnet --list-runtimes | Where-Object { $_ -like "*WindowsDesktop*8.0*" }
```

**Check 2:** Run directly to see error message
```powershell
& "${env:ProgramFiles(x86)}\Microsoft Configuration Manager\AdminConsole\bin\CloudJourneyAddin\CloudJourneyAddin.exe"
```

**Check 3:** Check Windows Event Viewer
- Event Viewer → Windows Logs → Application
- Look for .NET Runtime or Application errors

### "Charts don't display"

This usually means LiveCharts.Wpf.dll is missing:
```powershell
Test-Path "${env:ProgramFiles(x86)}\Microsoft Configuration Manager\AdminConsole\bin\CloudJourneyAddin\LiveCharts.Wpf.dll"
```

If false, re-run the installer or manually copy all files from the publish folder.

---

## Quick Test Commands

Run these to quickly verify everything:

```powershell
# Check if installed
$consolePath = "${env:ProgramFiles(x86)}\Microsoft Configuration Manager\AdminConsole"
Write-Host "XML Manifest: $(Test-Path "$consolePath\XmlStorage\Extensions\Actions\CloudJourneyAddin.xml")" -ForegroundColor $(if(Test-Path "$consolePath\XmlStorage\Extensions\Actions\CloudJourneyAddin.xml"){"Green"}else{"Red"})
Write-Host "Executable: $(Test-Path "$consolePath\bin\CloudJourneyAddin\CloudJourneyAddin.exe")" -ForegroundColor $(if(Test-Path "$consolePath\bin\CloudJourneyAddin\CloudJourneyAddin.exe"){"Green"}else{"Red"})
Write-Host "Runtime: $(Test-Path "$consolePath\bin\CloudJourneyAddin\coreclr.dll")" -ForegroundColor $(if(Test-Path "$consolePath\bin\CloudJourneyAddin\coreclr.dll"){"Green"}else{"Red"})

# Count installed files
$fileCount = (Get-ChildItem "$consolePath\bin\CloudJourneyAddin" -Recurse -File -ErrorAction SilentlyContinue).Count
Write-Host "Total Files: $fileCount (should be 489)" -ForegroundColor $(if($fileCount -eq 489){"Green"}else{"Yellow"})
```

---

## What You Should See

### 1. After Running Installer:
```
═══════════════════════════════════════════════════
   ConfigMgr Cloud Journey Progress Add-in Installer
═══════════════════════════════════════════════════

ℹ Checking administrator privileges...
✓ Running with administrator privileges
ℹ Detecting ConfigMgr Console installation...
✓ Found ConfigMgr Console at: C:\Program Files (x86)\Microsoft Configuration Manager\AdminConsole
ℹ Checking .NET 8.0 Runtime...
✓ .NET 8.0 Desktop Runtime is already installed
ℹ Building Cloud Journey Add-in with all dependencies...
✓ Build completed successfully
ℹ Deploying XML manifest...
✓ XML manifest deployed
ℹ Deploying application binaries...
✓ Deployed 489 files
ℹ Validating installation...
✓ Installation validation passed

═══════════════════════════════════════════════════
   Installation Completed Successfully! ✓
═══════════════════════════════════════════════════
```

### 2. In ConfigMgr Console:
A new button/menu item labeled **"Cloud Journey Progress"** or **"Cloud Journey Dashboard"**

### 3. When Clicking the Button:
A full-screen dashboard window with:
- Blue header with title and refresh button
- 10 distinct sections with cards, charts, and interactive elements
- Scrollable content
- Professional Microsoft-style design

---

## Next Steps After Successful Testing

1. **Test with Real Data**: Update `TelemetryService.cs` to pull from real ConfigMgr/Intune APIs
2. **Customize**: Modify colors, layout, or add additional sections
3. **Deploy**: Use the installer on other ConfigMgr admin machines
4. **Distribute**: Create ZIP package for wider distribution

---

## Need Help?

If you encounter issues:
1. Check the troubleshooting section above
2. Review [PREREQUISITE_FREE_DEPLOYMENT.md](PREREQUISITE_FREE_DEPLOYMENT.md)
3. Check Windows Event Viewer for detailed errors
4. Run the verification commands to identify missing components

---

## Uninstalling After Testing

To remove the add-in:
```powershell
.\Uninstall-CloudJourneyAddin.ps1
```

Or manually:
```powershell
$consolePath = "${env:ProgramFiles(x86)}\Microsoft Configuration Manager\AdminConsole"
Remove-Item "$consolePath\XmlStorage\Extensions\Actions\CloudJourneyAddin.xml" -Force
Remove-Item "$consolePath\bin\CloudJourneyAddin" -Recurse -Force
```

Then restart ConfigMgr Console.
