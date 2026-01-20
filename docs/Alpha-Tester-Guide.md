# Zero Trust Migration Journey Dashboard
## Alpha Tester Guide

---

**Version:** 3.17.6  
**Date:** January 2026  
**Document Type:** Alpha Testing Guide

---

## 🙏 Thank You for Being an Alpha Tester!

Thank you for volunteering to be part of our alpha testing program! Your feedback and testing efforts are invaluable in helping us build a tool that truly meets the needs of IT administrators managing the transition from Configuration Manager to Microsoft Intune.

As an early tester, you have a unique opportunity to shape the direction of this tool. Every bug you find, every feature suggestion you make, and every piece of feedback you share helps us improve the experience for the broader ConfigMgr community.

We genuinely appreciate your time and expertise. Let's build something great together!

---

## ⚠️ CONFIDENTIALITY NOTICE - PLEASE READ

> **THIS DOCUMENT AND SOFTWARE ARE 100% UNDER NDA**

By participating in this alpha testing program, you agree to the following:

### Non-Disclosure Agreement

- **DO NOT** share this tool, documentation, or any information about this project with peers, colleagues outside the testing group, or any third parties
- **DO NOT** post screenshots, descriptions, or discussions about this tool on social media, forums, blogs, or public channels
- **DO NOT** redistribute the installer, source code, or any related materials
- This is a **private alpha** - treat all materials as confidential

### Testing Disclaimer

- You may test this tool on **your own environments** at your own risk
- You may test on **customer environments** at your own risk
- Neither the developer nor Microsoft assumes any liability for issues arising from use of this pre-release software
- **Always test in non-production environments first when possible**

### Tool Safety Assurances

🔒 **Read-Only Operation**: This tool is designed as a **read-only tool**. It queries data from Microsoft Graph API and ConfigMgr Admin Service but does not create, modify, or delete any objects in your environment.

🛡️ **Smart Enrollment Feature**: You may notice a "Smart Enrollment" or enrollment simulation feature that *appears* as if it could make changes to your environment. **Rest assured: this feature is NOT wired up to make any actual changes.** It is a simulation/preview feature only and cannot enroll devices, modify policies, or alter your Intune or ConfigMgr configuration.

### Privacy & AI Statement

🔐 **No AI Integrated by Default**: This tool does **not** have any AI features enabled by default. There is no integration with Azure OpenAI, Copilot, or any other AI service that processes your organizational data unless you explicitly enable it in settings.

📊 **Privacy-Respecting Telemetry**: The only information collected is anonymous usage telemetry (app launches, feature usage, errors) to help improve the product. **No device names, user names, organizational data, or personally identifiable information is collected or transmitted.** You can review the privacy documentation for full details.

---

## 📋 What is the Zero Trust Migration Journey Dashboard?

### Overview

The **Zero Trust Migration Journey Dashboard** is a WPF add-in for the Microsoft Configuration Manager (ConfigMgr/SCCM) console that helps IT administrators plan, track, and execute migrations to Microsoft Intune cloud-native management.

### The Problem We're Solving

Many organizations are transitioning from on-premises Configuration Manager to cloud-native Intune management, but this journey is complex:

- **Visibility Gap**: Admins lack clear visibility into how ready their environment is for cloud management
- **Planning Challenges**: It's hard to know which devices can transition and which have blockers
- **Progress Tracking**: Organizations need to track migration progress and enrollment status
- **Decision Support**: Admins need data-driven insights to make migration decisions

### Our Goals

1. **Provide Visibility** - Show real-time data about your ConfigMgr and Intune environments side-by-side
2. **Assess Readiness** - Identify which devices are ready for cloud management and which have blockers
3. **Track Progress** - Monitor enrollment status, compliance, and migration milestones
4. **Recommend Actions** - Provide actionable recommendations to improve your cloud posture
5. **Simplify Planning** - Help you plan pilot groups and phased rollouts

### Key Features

| Feature | Description |
|---------|-------------|
| **Dashboard Overview** | See ConfigMgr and Intune device counts, compliance rates, and enrollment trends |
| **Enrollment Confidence Score** | AI-powered score showing likelihood of successful Intune enrollment |
| **Cloud Readiness Signals** | Assess readiness for Autopilot, Windows 11, cloud-native management, and more |
| **Enrollment Readiness Analyzer** | Simulate compliance policies against unenrolled devices |
| **Migration Impact Analysis** | Project benefits of completing your migration |
| **Workload Transition Tracker** | Track which workloads have moved to Intune |

---

## 🧪 Test Cases for Alpha Testers

Please complete **at least 10** of the following 20 test cases. Feel free to complete all 20 if time permits! For each test case, note whether it **Passed**, **Failed**, or was **Blocked**, along with any observations.

### Installation & Launch (Test Cases 1-4)

#### Test Case 1: Fresh Installation
| Item | Details |
|------|---------|
| **Objective** | Verify the application installs correctly |
| **Prerequisites** | ConfigMgr Console installed, .NET 8.0 Desktop Runtime |
| **Steps** | 1. Download the ZIP file<br>2. Extract to a folder<br>3. Run `ZeroTrustMigrationAddin.exe` |
| **Expected Result** | Application launches without errors |
| **Your Result** | ☐ Pass ☐ Fail ☐ Blocked |
| **Notes** | |

#### Test Case 2: MSI Installation
| Item | Details |
|------|---------|
| **Objective** | Verify MSI installer works correctly |
| **Prerequisites** | ConfigMgr Console installed |
| **Steps** | 1. Download the MSI file<br>2. Run the installer<br>3. Complete installation wizard<br>4. Launch from Start Menu or ConfigMgr Console |
| **Expected Result** | Installation completes, app appears in ConfigMgr Console ribbon |
| **Your Result** | ☐ Pass ☐ Fail ☐ Blocked |
| **Notes** | |

#### Test Case 3: Auto-Update Check
| Item | Details |
|------|---------|
| **Objective** | Verify automatic update checking works |
| **Prerequisites** | Application installed, internet connection |
| **Steps** | 1. Launch the application<br>2. Observe notification area for update check<br>3. Check Help > About for version info |
| **Expected Result** | Update check completes (may show "up to date" or offer update) |
| **Your Result** | ☐ Pass ☐ Fail ☐ Blocked |
| **Notes** | |

#### Test Case 4: Application Exit and Restart
| Item | Details |
|------|---------|
| **Objective** | Verify clean shutdown and restart |
| **Prerequisites** | Application running |
| **Steps** | 1. Close the application<br>2. Relaunch the application<br>3. Verify settings are preserved |
| **Expected Result** | Application closes cleanly and relaunches without issues |
| **Your Result** | ☐ Pass ☐ Fail ☐ Blocked |
| **Notes** | |

### Authentication & Connectivity (Test Cases 5-8)

#### Test Case 5: Graph API Authentication
| Item | Details |
|------|---------|
| **Objective** | Verify Microsoft Graph API sign-in works |
| **Prerequisites** | Azure AD account with Intune permissions |
| **Steps** | 1. Click "Sign in with Graph API"<br>2. Complete authentication flow<br>3. Verify Intune data loads |
| **Expected Result** | Authentication succeeds, Intune device data appears |
| **Your Result** | ☐ Pass ☐ Fail ☐ Blocked |
| **Notes** | |

#### Test Case 6: ConfigMgr Admin Service Connection
| Item | Details |
|------|---------|
| **Objective** | Verify ConfigMgr Admin Service connection |
| **Prerequisites** | ConfigMgr Admin Service URL, valid credentials |
| **Steps** | 1. Enter Admin Service URL (e.g., `https://sccm.domain.com/AdminService`)<br>2. Authenticate<br>3. Verify ConfigMgr data loads |
| **Expected Result** | Connection succeeds, ConfigMgr device data appears |
| **Your Result** | ☐ Pass ☐ Fail ☐ Blocked |
| **Notes** | |

#### Test Case 7: Disconnected Mode (Mock Data)
| Item | Details |
|------|---------|
| **Objective** | Verify app works without connections |
| **Prerequisites** | Application running, NOT signed in |
| **Steps** | 1. Launch application without signing in<br>2. Navigate through all tabs<br>3. Observe sample/mock data |
| **Expected Result** | All tabs display sample data, no crashes |
| **Your Result** | ☐ Pass ☐ Fail ☐ Blocked |
| **Notes** | |

#### Test Case 8: Session Persistence
| Item | Details |
|------|---------|
| **Objective** | Verify authentication persists across restarts |
| **Prerequisites** | Successfully authenticated to Graph API |
| **Steps** | 1. Sign in to Graph API<br>2. Close application<br>3. Relaunch application<br>4. Check if still authenticated |
| **Expected Result** | Token is cached, re-authentication may not be required |
| **Your Result** | ☐ Pass ☐ Fail ☐ Blocked |
| **Notes** | |

### Dashboard Features (Test Cases 9-14)

#### Test Case 9: Dashboard Overview Tab
| Item | Details |
|------|---------|
| **Objective** | Verify main dashboard displays correctly |
| **Prerequisites** | Connected to Graph API and/or ConfigMgr |
| **Steps** | 1. Navigate to Dashboard tab<br>2. Review device counts, charts, and metrics<br>3. Verify data accuracy against admin consoles |
| **Expected Result** | Dashboard shows accurate device counts and trends |
| **Your Result** | ☐ Pass ☐ Fail ☐ Blocked |
| **Notes** | |

#### Test Case 10: Enrollment Confidence Score
| Item | Details |
|------|---------|
| **Objective** | Verify Enrollment Confidence calculation |
| **Prerequisites** | Connected to both data sources |
| **Steps** | 1. View the Enrollment Confidence card<br>2. Click for detailed breakdown<br>3. Review factors contributing to score |
| **Expected Result** | Score displays (0-100), breakdown shows contributing factors |
| **Your Result** | ☐ Pass ☐ Fail ☐ Blocked |
| **Notes** | |

#### Test Case 11: Cloud Readiness Signals Tab
| Item | Details |
|------|---------|
| **Objective** | Verify Cloud Readiness assessment works |
| **Prerequisites** | Connected to ConfigMgr |
| **Steps** | 1. Navigate to Cloud Readiness Signals tab<br>2. Click Refresh to run assessment<br>3. Review each readiness signal (Autopilot, Windows 11, etc.)<br>4. Review blockers identified |
| **Expected Result** | Readiness percentages display for each signal with blockers listed |
| **Your Result** | ☐ Pass ☐ Fail ☐ Blocked |
| **Notes** | |

#### Test Case 12: Enrollment Readiness Analyzer
| Item | Details |
|------|---------|
| **Objective** | Verify compliance simulation feature |
| **Prerequisites** | Connected to both data sources |
| **Steps** | 1. Navigate to the Enrollment Readiness section<br>2. Run the analyzer<br>3. Review which devices would pass/fail compliance |
| **Expected Result** | Results show device breakdown with gap analysis |
| **Your Result** | ☐ Pass ☐ Fail ☐ Blocked |
| **Notes** | |

#### Test Case 13: Migration Impact Analysis
| Item | Details |
|------|---------|
| **Objective** | Verify impact projection feature |
| **Prerequisites** | Connected to data sources |
| **Steps** | 1. Find and open Migration Impact Analysis<br>2. Review current vs. projected scores<br>3. Check category breakdowns |
| **Expected Result** | Shows before/after comparison with improvement estimates |
| **Your Result** | ☐ Pass ☐ Fail ☐ Blocked |
| **Notes** | |

#### Test Case 14: Workloads Tab
| Item | Details |
|------|---------|
| **Objective** | Verify workload tracking displays correctly |
| **Prerequisites** | Connected to Intune |
| **Steps** | 1. Navigate to Workloads tab<br>2. Review workload transition status<br>3. Verify status matches your environment |
| **Expected Result** | Workloads show current management state (ConfigMgr/Intune/Hybrid) |
| **Your Result** | ☐ Pass ☐ Fail ☐ Blocked |
| **Notes** | |

### Data Accuracy & Reports (Test Cases 15-17)

#### Test Case 15: Device Count Accuracy
| Item | Details |
|------|---------|
| **Objective** | Verify device counts match source systems |
| **Prerequisites** | Access to ConfigMgr Console and Intune Portal |
| **Steps** | 1. Note device counts in the dashboard<br>2. Compare to ConfigMgr Console device count<br>3. Compare to Intune Portal device count |
| **Expected Result** | Counts match within reasonable margin (timing differences OK) |
| **Your Result** | ☐ Pass ☐ Fail ☐ Blocked |
| **Notes** | |

#### Test Case 16: Recommendations Feature
| Item | Details |
|------|---------|
| **Objective** | Verify recommendations are actionable |
| **Prerequisites** | Connected to data sources |
| **Steps** | 1. Find and open Recommendations<br>2. Review each recommendation<br>3. Assess if they're relevant to your environment |
| **Expected Result** | Recommendations are specific and actionable |
| **Your Result** | ☐ Pass ☐ Fail ☐ Blocked |
| **Notes** | |

#### Test Case 17: Diagnostics Window
| Item | Details |
|------|---------|
| **Objective** | Verify query logging and diagnostics |
| **Prerequisites** | Connected to data sources, queries executed |
| **Steps** | 1. Open Diagnostics window (Help menu or button)<br>2. Review query log<br>3. Try exporting the log |
| **Expected Result** | Query log shows recent API calls with timing |
| **Your Result** | ☐ Pass ☐ Fail ☐ Blocked |
| **Notes** | |

### Edge Cases & Error Handling (Test Cases 18-20)

#### Test Case 18: Network Disconnection
| Item | Details |
|------|---------|
| **Objective** | Verify graceful handling of network loss |
| **Prerequisites** | Application running and connected |
| **Steps** | 1. Disconnect from network<br>2. Try to refresh data<br>3. Observe error handling |
| **Expected Result** | Clear error message, no crash, graceful degradation |
| **Your Result** | ☐ Pass ☐ Fail ☐ Blocked |
| **Notes** | |

#### Test Case 19: Large Environment Performance
| Item | Details |
|------|---------|
| **Objective** | Verify performance with many devices |
| **Prerequisites** | Environment with 1,000+ devices |
| **Steps** | 1. Connect to your environment<br>2. Navigate through all tabs<br>3. Note any slowness or timeouts |
| **Expected Result** | Application remains responsive (may show loading indicators) |
| **Your Result** | ☐ Pass ☐ Fail ☐ Blocked |
| **Notes** | Device count: _____ |

#### Test Case 20: Window Resizing and Display
| Item | Details |
|------|---------|
| **Objective** | Verify UI adapts to different sizes |
| **Prerequisites** | Application running |
| **Steps** | 1. Resize window to various sizes<br>2. Maximize and restore<br>3. Try on different monitor resolutions if available |
| **Expected Result** | UI elements resize appropriately, no clipping or overflow |
| **Your Result** | ☐ Pass ☐ Fail ☐ Blocked |
| **Notes** | |

---

## 📝 Feedback Submission

After completing your testing, please send your results to: **[Your Email/Teams Channel]**

Include:
- Completed test case results (Pass/Fail/Blocked)
- Screenshots of any issues encountered
- Log files from: `%LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs\`
- Any feature suggestions or general feedback

---

## ❓ Frequently Asked Questions (FAQ)

### General Questions

**Q1: What permissions do I need to use this tool?**

A: For full functionality, you need:
- **Intune/Graph API**: At minimum, `DeviceManagementManagedDevices.Read.All` permission. For full features: `DeviceManagementConfiguration.Read.All`, `DeviceManagementApps.Read.All`
- **ConfigMgr**: Read access to the Admin Service (typically "Read-Only Analyst" role or higher)
- **Local**: The tool runs with your current Windows credentials

---

**Q2: Does this tool make any changes to my environment?**

A: No. This tool is **read-only**. It only queries data from Microsoft Graph API and ConfigMgr Admin Service. It does not create, modify, or delete any objects in Intune or ConfigMgr.

---

**Q3: Is my data sent anywhere outside my organization?**

A: The tool sends anonymous telemetry to Azure Application Insights to help us improve the product. This includes:
- App launch/exit events
- Feature usage (which tabs are visited)
- Error information

No device names, user names, or organizational data is transmitted. You can review our [Privacy Policy](PRIVACY.md) for details.

---

**Q4: Can I use this tool without ConfigMgr Console installed?**

A: Yes, but with limitations. The tool works best when run from a machine with ConfigMgr Console installed (for Admin Service connectivity). However, you can still connect to Graph API and view Intune data without ConfigMgr Console.

---

**Q5: What version of ConfigMgr is required?**

A: The tool uses the ConfigMgr Admin Service REST API, which is available in:
- ConfigMgr Current Branch 1810 or later (basic)
- ConfigMgr Current Branch 2006 or later (recommended for full features)

---

### Technical Questions

**Q6: The tool shows "0 devices" but I have devices in Intune. What's wrong?**

A: This usually means:
1. Authentication hasn't completed - ensure you've signed in via the Graph API button
2. Permissions issue - verify your account has `DeviceManagementManagedDevices.Read.All`
3. Tenant mismatch - ensure you're signing into the correct tenant

Check the Diagnostics window for API call details and error messages.

---

**Q7: What is the "Enrollment Confidence Score" and how is it calculated?**

A: The Enrollment Confidence Score (0-100) predicts how likely your unenrolled ConfigMgr devices are to successfully enroll in Intune. It considers factors like:
- Azure AD join status (Hybrid vs. AD-only)
- Windows version compatibility
- Hardware readiness (TPM, Secure Boot)
- Existing compliance policy coverage

A higher score means fewer barriers to enrollment.

---

**Q8: How often does the data refresh?**

A: Data is fetched when you:
- First connect to a data source
- Click any "Refresh" button
- Switch between certain tabs

Data is cached during your session to improve performance. Close and reopen the app for completely fresh data.

---

**Q9: Can multiple users run this tool simultaneously?**

A: Yes. Each user runs their own instance with their own credentials. Since the tool is read-only, there are no conflicts. However, each user will make their own API calls, so consider API throttling in very large deployments.

---

**Q10: The Cloud Readiness signals show different numbers than I expected. Why?**

A: Cloud Readiness uses data from ConfigMgr hardware inventory. Ensure:
- Hardware inventory is up-to-date (default: 7-day cycle)
- Devices have reported TPM, BIOS mode, and Secure Boot status
- The Admin Service connection is working (check Diagnostics)

Devices with missing inventory data are typically excluded from calculations.

---

### Troubleshooting Questions

**Q11: The application crashes on startup. What should I do?**

A: Try these steps:
1. Ensure .NET 8.0 Desktop Runtime is installed
2. Run the EXE directly (not from a ZIP without extracting)
3. Check if antivirus is blocking the application
4. Look for logs in `%LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs\`

If the issue persists, send us the log files.

---

**Q12: I get "Access Denied" when connecting to ConfigMgr Admin Service.**

A: This typically means:
1. Your account lacks permissions on the ConfigMgr site
2. The Admin Service URL is incorrect (should be `https://servername/AdminService`)
3. Windows Authentication isn't passing credentials correctly

Try accessing `https://yourserver/AdminService/v1.0/Device` in a browser first to verify access.

---

**Q13: Graph API authentication keeps failing or looping.**

A: Try:
1. Clear browser cache (authentication uses embedded browser)
2. Ensure you're not blocked by Conditional Access policies
3. Check if MFA is required and complete it
4. Try signing out of all Microsoft accounts in your browser and re-authenticating

---

**Q14: The application is very slow or freezes.**

A: For large environments (10,000+ devices):
1. Initial data load may take 30-60 seconds - wait for loading indicators
2. Check your network connection speed
3. Admin Service performance depends on your ConfigMgr SQL Server
4. Close and reopen if genuinely frozen - check logs for timeout errors

---

**Q15: Can I run this tool on a server or via Remote Desktop?**

A: Yes. The tool works over RDP. Ensure:
- .NET 8.0 Desktop Runtime is installed on the remote machine
- You have network access to both ConfigMgr and Azure endpoints
- The session has sufficient resources (WPF apps need graphics support)

---

### Feature Questions

**Q16: Will this tool support co-management workload recommendations?**

A: This is on our roadmap! Currently, the tool shows workload status but doesn't make specific recommendations about which workloads to transition. We're working on intelligent recommendations based on your environment's readiness.

---

**Q17: Can I export reports from this tool?**

A: Currently, you can:
- Copy text summaries to clipboard (various "Copy" buttons)
- Export query logs from the Diagnostics window
- Take screenshots

Full export to PDF/Excel is planned for a future release.

---

**Q18: Does this integrate with the ConfigMgr Console ribbon?**

A: Yes, when installed via the MSI installer, a button is added to the ConfigMgr Console ribbon under the Home tab. You can also run the tool standalone.

---

**Q19: Is there a way to track migration progress over time?**

A: The tool currently shows point-in-time snapshots. Historical tracking (showing enrollment growth over weeks/months) is on our roadmap. For now, we recommend taking periodic screenshots or notes.

---

**Q20: Will there be a version that runs as a web app or in Azure?**

A: We're exploring options for a cloud-hosted version. The current desktop app was chosen because:
- It can access ConfigMgr Admin Service (often on-premises)
- No additional Azure infrastructure required
- Runs with user context for security

Let us know if a cloud version would be valuable for your scenario!

---

## 📞 Contact & Support

For questions, issues, or feedback during the alpha:

- **GitHub Issues**: https://github.com/sccmavenger/cmaddin/issues
- **Email**: [Your contact email]
- **Teams**: [Your Teams channel if applicable]

**When reporting issues, please include:**
1. Steps to reproduce
2. Expected vs. actual behavior
3. Screenshots if applicable
4. Log files from `%LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs\`

---

## 🚀 What's Next?

After the alpha testing phase, we plan to:
1. Address bugs and issues identified during testing
2. Implement top-requested features
3. Move to beta with expanded testing group
4. Release v1.0 to the broader community

Your feedback directly influences our priorities. Thank you again for being part of this journey!

---

*Document Version: 1.0 | Last Updated: January 2026*
