================================================================================
Zero Trust Migration Journey Add-in - Log File Viewer Guide
================================================================================

LOG FILE LOCATION
-----------------
Log files are stored in:
%LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs\

Full path example:
C:\Users\[YourUsername]\AppData\Local\ZeroTrustMigrationAddin\Logs\

Log files are named by date:
ZeroTrustMigrationAddin_20260114.log


HOW TO VIEW LOGS WITH CMTRACE
------------------------------
CMTrace is the best tool for viewing Configuration Manager log files.
It provides real-time monitoring, color-coded messages, and search features.

1. FIND CMTRACE.EXE
   CMTrace is installed with the Configuration Manager Console.
   
   Common locations:
   - C:\Program Files (x86)\Microsoft Configuration Manager\AdminConsole\bin\CMTrace.exe
   - C:\Program Files\Microsoft Endpoint Manager\CMTrace.exe
   
   You can also copy CMTrace.exe to this tools\ folder for convenience.

2. OPEN LOG FILES WITH CMTRACE
   
   Option A - Drag and drop:
   - Drag ZeroTrustMigrationAddin_YYYYMMDD.log onto CMTrace.exe
   
   Option B - Right-click:
   - Right-click the .log file → Open With → Choose CMTrace.exe
   - Check "Always use this app" to set as default
   
   Option C - Command line:
   - CMTrace.exe "C:\Users\[User]\AppData\Local\ZeroTrustMigrationAddin\Logs\ZeroTrustMigrationAddin_20260114.log"

3. CMTRACE FEATURES
   - Real-time tail: Automatically shows new log entries as they're written
   - Color coding:
     * Yellow = Warning
     * Red = Error
     * White/Black = Info/Debug
   - Search: Ctrl+F to search for specific text
   - Filtering: Tools → Find → Filter to show only matching lines
   - Highlighting: Tools → Highlight to mark specific patterns


ALTERNATIVE VIEWING OPTIONS
----------------------------
If CMTrace is not available:

1. NOTEPAD++
   - Free text editor with syntax highlighting
   - Download: https://notepad-plus-plus.org/
   - Supports real-time file monitoring (View → Monitoring)

2. WINDOWS NOTEPAD
   - Built-in but basic
   - No real-time updates (must close and reopen)
   - Works for quick viewing

3. POWERSHELL
   - Real-time monitoring:
     Get-Content "ZeroTrustMigrationAddin_20260114.log" -Wait -Tail 50
   
   - Search for errors:
     Select-String -Path "ZeroTrustMigrationAddin_*.log" -Pattern "ERROR"


LOG LEVELS
----------
[DEBUG]    - Detailed diagnostic information
[INFO]     - General informational messages
[WARNING]  - Something unexpected but not critical
[ERROR]    - Error that prevented an operation
[CRITICAL] - Severe error that may cause application failure


TROUBLESHOOTING TIPS
--------------------
1. Application won't start:
   - Look for [CRITICAL] or [ERROR] messages near the timestamp when you tried to launch

2. Update issues:
   - Search for "update" or "GitHub" to see update check activity
   
3. Authentication problems:
   - Search for "auth" or "token" or "Graph"

4. Performance issues:
   - Look at timestamps to find slow operations
   - Search for "duration" or "took"

5. Telemetry tracking:
   - Search for "[TELEMETRY]" to see what usage data is being collected


LOG FILE RETENTION
------------------
- Logs are kept for 7 days by default
- Old logs are automatically deleted
- Each log file is dated: ZeroTrustMigrationAddin_YYYYMMDD.log
- You can manually archive important logs before they're deleted


PRIVACY NOTICE
--------------
Log files may contain:
- API endpoints and response codes
- File paths and version numbers
- Error messages and stack traces
- Telemetry events (feature usage, not personal data)

Log files do NOT contain:
- Passwords or authentication tokens (these are redacted)
- Personal device data
- Email addresses or usernames (sanitized before logging)

Logs are stored locally only - never automatically uploaded.


TELEMETRY (AZURE APPLICATION INSIGHTS)
---------------------------------------
The application sends anonymous usage data to Azure Application Insights:
- Feature usage (button clicks, menu actions)
- Performance metrics (API call durations, success rates)
- Error rates (exception types and frequencies)

Telemetry does NOT include:
- Device names
- Usernames
- Email addresses
- Tenant IDs
- IP addresses
- UNC paths

All PII is sanitized before sending to Azure.
You can see telemetry events in logs by searching for "[TELEMETRY]".


FOR SUPPORT
-----------
When reporting issues, please:
1. Open the log file for the day the issue occurred
2. Copy relevant log entries (30-50 lines before and after the error)
3. Remove any sensitive information if sharing externally
4. Include the application version (shown at app startup in logs)


================================================================================
