# Tab Visibility Command-Line Arguments

## Overview

The Zero Trust Migration Journey now supports command-line arguments to control which tabs are visible when the application starts. This allows you to create customized views of the dashboard based on your specific needs.

## Usage Examples

### Hide Specific Tabs

Hide individual tabs while showing all others:

```powershell
# Hide the Enrollment tab
ZeroTrustMigrationAddin.exe /hidetabs:enrollment

# Hide multiple tabs (comma-separated)
ZeroTrustMigrationAddin.exe /hidetabs:enrollment,workloads

# Hide all AI-related tabs
ZeroTrustMigrationAddin.exe /hidetabs:aiactions,brainstorm
```

### Show Only Specific Tabs

Show only the specified tabs (hides all others, but Overview is always visible):

```powershell
# Show only Overview and Enrollment tabs
ZeroTrustMigrationAddin.exe /showtabs:enrollment

# Show only Overview, Enrollment, and Workloads
ZeroTrustMigrationAddin.exe /showtabs:enrollment,workloads

# Show only Overview and AI Actions
ZeroTrustMigrationAddin.exe /showtabs:ai
```

## Available Tab Names

Use these names in your command-line arguments:

| Tab Name | Argument Value | Alternative |
|----------|---------------|-------------|
| 📊 Overview | N/A | Always visible |
| 📱 Enrollment | `enrollment` | - |
| 🔄 Workloads | `workloads` | - |
| 💡 Workload Brainstorm | `workloadbrainstorm` | `brainstorm` |
| 📦 Applications | `applications` | `apps` |
| 🤖 AI Actions | `aiactions` | `ai` |

## Creating Desktop Shortcuts

You can create desktop shortcuts with specific tab configurations:

### Example 1: Enrollment-Focused Shortcut

```powershell
# Create a shortcut for enrollment tracking only
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$Home\Desktop\Zero Trust Migration Journey - Enrollment.lnk")
$Shortcut.TargetPath = "C:\Path\To\ZeroTrustMigrationAddin.exe"
$Shortcut.Arguments = "/showtabs:enrollment"
$Shortcut.Description = "Zero Trust Migration Journey - Enrollment Focus"
$Shortcut.Save()
```

### Example 2: Workload Migration Shortcut

```powershell
# Create a shortcut for workload planning
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$Home\Desktop\Zero Trust Migration Journey - Workloads.lnk")
$Shortcut.TargetPath = "C:\Path\To\ZeroTrustMigrationAddin.exe"
$Shortcut.Arguments = "/showtabs:workloads,brainstorm"
$Shortcut.Description = "Zero Trust Migration Journey - Workload Focus"
$Shortcut.Save()
```

### Example 3: Hide Experimental Features

```powershell
# Create a shortcut without experimental AI features
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$Home\Desktop\Zero Trust Migration Journey - Core.lnk")
$Shortcut.TargetPath = "C:\Path\To\ZeroTrustMigrationAddin.exe"
$Shortcut.Arguments = "/hidetabs:brainstorm,aiactions"
$Shortcut.Description = "Zero Trust Migration Journey - Core Features Only"
$Shortcut.Save()
```

## Use Cases

### 1. Executive Dashboards
Show only high-level overview and key metrics:
```powershell
ZeroTrustMigrationAddin.exe /hidetabs:brainstorm,aiactions,applications
```

### 2. Technical Team View
Show enrollment and technical details:
```powershell
ZeroTrustMigrationAddin.exe /showtabs:enrollment,workloads,applications
```

### 3. Planning Sessions
Focus on workload brainstorming:
```powershell
ZeroTrustMigrationAddin.exe /showtabs:workloads,brainstorm
```

### 4. Demo Mode
Hide tabs still under development:
```powershell
ZeroTrustMigrationAddin.exe /hidetabs:aiactions
```

## Notes

- The **Overview** tab is always visible and cannot be hidden
- Arguments are case-insensitive (`/hidetabs:ENROLLMENT` works the same as `/hidetabs:enrollment`)
- Use either forward slash `/` or hyphen `-` as argument prefix (`/hidetabs:` or `-hidetabs:`)
- Multiple tab names are separated by commas with no spaces
- Invalid tab names are silently ignored
- Without any arguments, all tabs are visible (default behavior)

## Hidden Sections in Overview Tab

The following sections have been hidden from the Overview tab:

1. **🔒 Security & Compliance Scorecard** - Hidden permanently
2. **💰 Unlock Savings with Action** - Hidden permanently

To unhide these sections, modify the `Visibility` property in [DashboardWindow.xaml](Views/DashboardWindow.xaml):
- Line ~968: Security & Compliance Scorecard
- Line ~996: Unlock Savings with Action

Change `Visibility="Collapsed"` to `Visibility="Visible"` to restore these sections.
