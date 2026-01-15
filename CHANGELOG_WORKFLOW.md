# Changelog-Driven Documentation Workflow

## Overview

The build system now **automatically generates** the AdminUserGuide.html alert box from CHANGELOG.md. You maintain documentation in **one place** (CHANGELOG.md) and the build script generates the user-facing HTML.

## How It Works

### **Before Building (You Write This):**

Edit `CHANGELOG.md` and fill in the `[Unreleased]` section:

```markdown
## [Unreleased]

### Added
- **Azure Telemetry** 📊 Track feature usage and errors with Application Insights
- **Privacy-Safe Logging** 🔒 All PII automatically sanitized

### Changed
- **Dynamic Window Title** 🪟 Version now reads from assembly automatically

### Fixed
- **Title Bar Version** Fixed hardcoded version display
```

**Formatting Rules:**
- Use `**Feature Name**` for bold titles
- Add emoji after title (optional but recommended)
- Write user-facing descriptions (not technical commit messages)
- Keep descriptions concise (one line per item)

### **During Build:**

Run `.\Build-And-Distribute.ps1`

The script automatically:
1. ✅ Reads `[Unreleased]` section from CHANGELOG.md
2. ✅ Converts `[Unreleased]` → `[3.16.7] - 2026-01-14`
3. ✅ Generates HTML alert box for AdminUserGuide.html
4. ✅ Updates footer version in AdminUserGuide.html
5. ✅ Creates new `[Unreleased]` template for next version

### **After Build:**

**CHANGELOG.md:**
```markdown
## [Unreleased]

### Added
- 

### Changed
- 

### Fixed
- 


## [3.16.7] - 2026-01-14

### Added
- **Azure Telemetry** 📊 Track feature usage and errors with Application Insights
- **Privacy-Safe Logging** 🔒 All PII automatically sanitized

### Changed
- **Dynamic Window Title** 🪟 Version now reads from assembly automatically

### Fixed
- **Title Bar Version** Fixed hardcoded version display
```

**AdminUserGuide.html:**
```html
<div class="alert alert-success">
    <strong>🎉 Version 3.16.7 - January 14, 2026</strong>
    <ul>
        <li><strong>Azure Telemetry</strong> 📊 Track feature usage and errors with Application Insights</li>
        <li><strong>Privacy-Safe Logging</strong> 🔒 All PII automatically sanitized</li>
        <li><strong>Dynamic Window Title</strong> 🪟 Version now reads from assembly automatically</li>
        <li><strong>Title Bar Version</strong> Fixed hardcoded version display</li>
    </ul>
</div>
```

## Benefits

✅ **Single Source of Truth** - Update CHANGELOG.md only  
✅ **Automatic HTML Generation** - No manual HTML editing  
✅ **Version History Preserved** - Old versions stay intact  
✅ **Consistent Formatting** - Script ensures proper structure  
✅ **Less Work** - Write once, generates everywhere  

## Complete Workflow

### 1. **Work on Features**
```bash
# Make code changes
# Add new features
# Fix bugs
```

### 2. **Update CHANGELOG.md**
```markdown
## [Unreleased]

### Added
- **New Feature** 🎉 What it does for users

### Changed
- **Updated Feature** 🔄 What changed

### Fixed
- **Bug Name** 🐛 What was broken and how it's fixed
```

### 3. **Build & Release**
```powershell
.\Build-And-Distribute.ps1 -Force
```

### 4. **Commit Everything**
```bash
git add .
git commit -m "Release v3.16.7"
git push
```

Done! ✅

## Tips

### Good Changelog Entries:
```markdown
- **Azure Telemetry** 📊 Track usage with Application Insights (anonymous, PII-safe)
- **Auto-Update** 🔄 Delta updates download only changed files (97% bandwidth savings)
- **Title Bar Fix** 🐛 Version now displays correctly in window title
```

### Bad Changelog Entries:
```markdown
- Added telemetry
- Fixed bug
- Updated code
```

### Recommended Emojis:
- 🎉 New major feature
- 📊 Analytics/reporting
- 🔒 Security/privacy
- 🔄 Updates/changes
- 🐛 Bug fixes
- ⚡ Performance improvements
- 🪟 UI changes
- 📝 Documentation
- 🔧 Configuration/settings

## Troubleshooting

### **"[Unreleased] section empty" Warning**

**Cause:** You ran the build without updating CHANGELOG.md first.

**Fix:** Before building, edit CHANGELOG.md and add your changes to `[Unreleased]`.

**What Happens:** Build script creates a placeholder "Version update" entry.

---

### **Alert Box Shows "Version update" Only**

**Cause:** CHANGELOG.md had empty/placeholder entries.

**Fix:** 
1. Manually edit AdminUserGuide.html (one time)
2. Next time, update CHANGELOG.md before building

---

### **HTML Formatting Broken**

**Cause:** Special characters in changelog entry (e.g., `<`, `>`, `&`).

**Fix:** Use plain text in CHANGELOG.md, avoid HTML characters.

---

## What Gets Updated Automatically

| File | What Changes | How |
|------|-------------|-----|
| **ZeroTrustMigrationAddin.csproj** | Version number | Auto-increment |
| **README.md** | Version references | String replacement |
| **AdminUserGuide.html** | Alert box + footer | Generated from CHANGELOG.md |
| **USER_GUIDE.md** | Version references | String replacement |
| **DashboardWindow.xaml** | Version in Title | String replacement (but dynamic now) |
| **DashboardViewModel.cs** | Version constant | String replacement |
| **CHANGELOG.md** | [Unreleased] → [Version] | Parsed and converted |

## Manual Steps (Still Required)

- ❌ Screenshots in user guide (can't automate)
- ❌ Detailed feature documentation sections
- ❌ Complex HTML layouts in user guide
- ❌ GitHub release notes (can use -ReleaseNotes parameter)

---

## Example: Complete Release Cycle

**Monday: Start new feature**
```powershell
# Code: Add Azure telemetry
# Edit CHANGELOG.md:
## [Unreleased]
### Added
- **Azure Telemetry** 📊 Anonymous usage tracking with Application Insights
```

**Tuesday: More work**
```powershell
# Code: Fix title bar bug
# Update CHANGELOG.md:
### Fixed
- **Title Bar Version** 🐛 Fixed hardcoded version display
```

**Friday: Release**
```powershell
# Build
.\Build-And-Distribute.ps1 -Force

# Result:
# - CHANGELOG.md: [Unreleased] → [3.16.7]
# - AdminUserGuide.html: Alert box generated from changelog
# - All version numbers updated to 3.16.7
# - Package created: ZeroTrustMigrationAddin-v3.16.7-COMPLETE.zip
```

**Next Monday: Start fresh**
```markdown
## [Unreleased]

### Added
- **New Feature** 🎉 Description

# Previous versions preserved below
## [3.16.7] - 2026-01-14
...
```

---

## Advanced: Multiline Descriptions

If you need longer descriptions, keep them concise or edit AdminUserGuide.html manually for complex features:

**CHANGELOG.md (keep short):**
```markdown
- **Device Enrollment** 📱 Track enrollment status across Intune and ConfigMgr
```

**AdminUserGuide.html (detailed section):**
```html
<section id="enrollment">
    <h2>Device Enrollment</h2>
    <p>Detailed explanation with screenshots...</p>
</section>
```

The alert box is auto-generated, but feature sections remain manual for detailed docs.
