# Data Privacy & Security

**Last Updated:** January 9, 2026  
**Version:** 3.14.0

## Overview

The Cloud Journey Dashboard is designed with privacy and security as core principles. This document provides complete transparency about what data is collected, transmitted, and stored by the application.

---

## Data Collection & Transmission

### Microsoft Graph API (Intune Data)

**Purpose:** Retrieve device enrollment, compliance, and workload status from your Intune tenant.

**What's Retrieved:**
- Device counts and enrollment percentages
- Compliance policy status
- Workload co-management slider positions
- Device operating system versions (aggregated)

**What's NOT Retrieved:**
- Individual device names or hostnames
- User names or email addresses
- Device configuration details
- Personal files or user data

**Authentication:** Uses delegated permissions (you authenticate as yourself) via Microsoft Authentication Library (MSAL). Your credentials are never stored by the application.

**Permissions Required:**
- `DeviceManagementManagedDevices.Read.All` - Read device information
- `DeviceManagementConfiguration.Read.All` - Read policies
- `User.Read` - Basic profile information

---

### ConfigMgr Admin Service

**Purpose:** Retrieve device inventory from your on-premises ConfigMgr infrastructure.

**What's Retrieved:**
- Total Windows device count
- Operating system versions
- Last seen dates (for velocity calculations)

**What's NOT Retrieved:**
- Device names or hostnames
- User information
- Software inventory
- Hardware inventory details

**Authentication:** Uses Windows Authentication (your current logged-in identity). No credentials stored.

**Network:** Connects only to your ConfigMgr SMS Provider on your local network. No external transmission.

---

### Azure OpenAI (GPT-4) - AI Recommendations

**Purpose:** Generate intelligent migration recommendations based on your progress.

**Configuration:** Optional - requires manual setup with your own Azure OpenAI resource.

#### What Data is Sent to Azure OpenAI

**Aggregated Metrics Only - NO Personal Identifiers**

```yaml
Data Sent Example:
  Migration State:
    - Total Devices: 500
    - Intune Enrolled: 120 (24%)
    - ConfigMgr Only: 380
    - Days Since Last Progress: 45
    - Stalled: YES
    
  Workload Status:
    - Completed: 2/7 (Compliance Policies, Endpoint Protection)
    - In Progress: 1 (Device Configuration)
    - Not Started: 4
    
  Velocity Trends:
    - "Enrollment velocity has slowed in past 30 days"
    
  Migration Plan:
    - "Phase 2: Pilot Expansion (25-50% enrollment target)"
```

#### What is NEVER Sent to Azure OpenAI

- ❌ Device names, hostnames, or computer names
- ❌ User names, email addresses, or user identities
- ❌ IP addresses or network information
- ❌ Serial numbers or hardware identifiers
- ❌ Your organization name or tenant name
- ❌ Configuration details or policy settings
- ❌ Application lists or software inventory
- ❌ Any personally identifiable information (PII)
- ❌ Any credentials or secrets

#### Privacy Safeguards

1. **Your Azure Instance Only**
   - Data is sent to YOUR Azure OpenAI resource
   - Not shared with other customers or Microsoft's training data
   - You control the Azure region and data residency

2. **Microsoft Azure OpenAI Data Privacy**
   - Azure OpenAI does NOT use customer data to train models
   - Data is NOT shared with OpenAI (the company)
   - See: [Azure OpenAI Data Privacy Policy](https://learn.microsoft.com/legal/cognitive-services/openai/data-privacy)

3. **Smart Caching**
   - Responses cached locally for 30 minutes
   - Reduces redundant API calls by ~65%
   - Cache stored in: `%APPDATA%\CloudJourneyAddin\cache`

4. **Audit Logging**
   - All Azure OpenAI API calls logged locally
   - Includes timestamp, prompt summary, response status
   - Logs stored in: `%APPDATA%\CloudJourneyAddin\logs`
   - Never transmitted externally

5. **Optional Feature**
   - AI Recommendations are entirely optional
   - Dashboard fully functional without Azure OpenAI
   - You control when/if to enable this feature

---

## Local Data Storage

### Configuration Files

**Location:** `%APPDATA%\CloudJourneyAddin\`

**Files Stored:**
- `openai-config.json` - Azure OpenAI credentials (endpoint, deployment name, API key)
- `cache\*.json` - Cached API responses (30-minute TTL)
- `logs\*.log` - Application event logs

**Security:**
- Stored in user-specific AppData folder (protected by Windows ACLs)
- Only accessible by your user account
- Configuration file uses Windows file encryption (when available)

**Sensitive Data:**
- Azure OpenAI API key stored encrypted
- Never transmitted except to your Azure OpenAI endpoint
- Can be deleted anytime by removing folder

### No Database

- **No database** - Application does not maintain a local database
- **No historical data** - Does not store historical device or user information
- **Session only** - Data displayed is retrieved fresh each session

---

## Network Connections

The application ONLY connects to:

1. **Microsoft Graph API**
   - `https://graph.microsoft.com`
   - Microsoft's official Intune API
   - Always HTTPS encrypted

2. **ConfigMgr Admin Service** (optional)
   - Your on-premises ConfigMgr SMS Provider
   - Typically: `https://YOUR-SCCM-SERVER/AdminService`
   - Local network connection only

3. **Azure OpenAI** (optional, if configured)
   - Your Azure OpenAI endpoint: `https://YOUR-RESOURCE.openai.azure.com`
   - Always HTTPS encrypted
   - Direct connection to your Azure resource

**The application does NOT connect to:**
- ❌ Any third-party services
- ❌ Telemetry or analytics services
- ❌ Update servers (updates are manual)
- ❌ External databases or cloud storage

---

## Compliance & Standards

### Data Residency

- **Graph API:** Data residency follows your Microsoft 365/Intune tenant location
- **ConfigMgr:** Data never leaves your on-premises network
- **Azure OpenAI:** You choose the Azure region (controls data residency)

### GDPR Compliance

- **No PII Collected:** Application does not collect personally identifiable information
- **No User Tracking:** No analytics, telemetry, or user behavior tracking
- **Right to Delete:** All local data can be deleted by removing AppData folder
- **Minimal Data:** Only aggregated metrics used for AI recommendations

### Industry Standards

- **Least Privilege:** Uses minimum required API permissions
- **Encryption in Transit:** All network connections use HTTPS/TLS
- **No Backdoors:** Open source architecture (available for security review)
- **Audit Logging:** All sensitive operations logged for compliance

---

## Security Best Practices

### For Administrators

1. **Protect API Keys**
   - Azure OpenAI API keys stored in `%APPDATA%\CloudJourneyAddin\openai-config.json`
   - Use environment variables for shared deployments
   - Rotate keys regularly (Azure Portal → Keys and Endpoint)

2. **Network Security**
   - ConfigMgr Admin Service should use HTTPS (not HTTP)
   - Consider firewall rules for ConfigMgr SMS Provider access
   - Azure OpenAI connections always HTTPS (enforced by Azure)

3. **User Education**
   - Users should understand what data is accessed (device counts, not PII)
   - Azure OpenAI is optional - disable if not needed
   - Review logs periodically for audit purposes

4. **Credential Management**
   - Application uses Windows Authentication (for ConfigMgr)
   - Microsoft Graph uses MSAL (no password storage)
   - Azure OpenAI key is the only stored credential

---

## Frequently Asked Questions

### Q: Does the dashboard send telemetry to Microsoft?
**A:** No. The application does not send any telemetry, usage data, or diagnostics to Microsoft or any third party.

### Q: Can I use this without Azure OpenAI?
**A:** Yes. Azure OpenAI is entirely optional. All core features work without it. You simply won't see AI-powered recommendations.

### Q: Where is my Azure OpenAI API key stored?
**A:** In `%APPDATA%\CloudJourneyAddin\openai-config.json` (your user profile folder). It's protected by Windows file permissions and only accessible to your user account.

### Q: Can I review what data is sent to Azure OpenAI?
**A:** Yes. All API calls are logged to `%APPDATA%\CloudJourneyAddin\logs`. You can review the exact prompts sent and responses received.

### Q: Does this work in air-gapped/disconnected environments?
**A:** Partially. ConfigMgr data works (local network only). Microsoft Graph and Azure OpenAI require internet connectivity.

### Q: How do I delete all local data?
**A:** Delete the folder: `%APPDATA%\CloudJourneyAddin\`. This removes all configuration, cache, and logs.

### Q: Is the source code available for security review?
**A:** Yes. The code is available for review to verify data handling practices.

---

## Contact & Reporting

### Security Issues

If you discover a security issue or privacy concern:

1. **Do not** open a public GitHub issue
2. Review the code in `Services\AIRecommendationService.cs` (GenerateGPT4RecommendationsAsync method) to see exact data sent
3. Contact your organization's security team if concerned about deployment

### Privacy Questions

For questions about data handling:
- Review this PRIVACY.md document
- Check logs in `%APPDATA%\CloudJourneyAddin\logs`
- Review API permissions in Azure Portal (if using Azure OpenAI)

---

## Changes to This Policy

This privacy document is updated with each version release. Material changes will be noted in [CHANGELOG.md](CHANGELOG.md).

**Version History:**
- **3.14.0** (Jan 9, 2026) - Initial privacy documentation, GPT-4 exclusive recommendations
- **3.13.4** (Jan 10, 2026) - Added dual-source enrollment calculation details
- **3.13.3** (Dec 2025) - Added Azure OpenAI integration

---

## Summary

**What You Need to Know:**
✅ Only aggregated metrics sent to Azure OpenAI (no PII)  
✅ Your Azure OpenAI instance only (not shared)  
✅ Microsoft doesn't train models on your data  
✅ All connections encrypted (HTTPS)  
✅ Local audit logs for transparency  
✅ Optional feature - can disable anytime  
✅ No telemetry or tracking  
✅ Full control over your data  
