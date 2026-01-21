using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Runtime.CompilerServices;

namespace ZeroTrustMigrationAddin.Services
{
    /// <summary>
    /// File-based logger for comprehensive debug information.
    /// Logs to %LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs\ in CMTrace-compatible format.
    /// 
    /// CMTrace Format Reference (Microsoft Configuration Manager Trace Log Tool):
    /// <![LOG[Message]LOG]!><time="HH:mm:ss.mmm+TZO" date="MM-DD-YYYY" component="Component" context="" type="N" thread="ThreadID" file="File:Line">
    /// 
    /// Type values:
    ///   1 = Information
    ///   2 = Warning
    ///   3 = Error
    /// 
    /// Source: https://learn.microsoft.com/mem/configmgr/core/support/cmtrace
    /// </summary>
    public class FileLogger
    {
        private static FileLogger? _instance;
        private static readonly object _lock = new object();
        private readonly string _logFilePath;
        private readonly string _queryLogFilePath;
        private readonly string _logDirectory;
        private const string ComponentName = "CloudJourneyAddin";
        
        // In-memory query log for UI display (last 100 queries)
        private readonly List<QueryLogEntry> _recentQueries = new();
        private const int MaxRecentQueries = 100;

        public static FileLogger Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new FileLogger();
                    }
                }
                return _instance;
            }
        }

        private FileLogger()
        {
            // Log to %LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs\
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _logDirectory = Path.Combine(appDataPath, "ZeroTrustMigrationAddin", "Logs");
            
            // Create directory if it doesn't exist
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            // Log file name includes date for easy organization
            var logFileName = $"ZeroTrustMigrationAddin_{DateTime.Now:yyyyMMdd}.log";
            _logFilePath = Path.Combine(_logDirectory, logFileName);
            
            // Separate query log file for easy access
            var queryLogFileName = $"QueryLog_{DateTime.Now:yyyyMMdd}.log";
            _queryLogFilePath = Path.Combine(_logDirectory, queryLogFileName);

            // Write startup header (CMTrace will display these as info entries)
            Log(LogLevel.Info, "========== CloudJourney Add-in Session Started ==========");
            Log(LogLevel.Info, $"Session Start: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Log(LogLevel.Info, $"Log Format: CMTrace compatible (use CMTrace.exe to view)");
            Log(LogLevel.Info, $"Log File: {_logFilePath}");
            Log(LogLevel.Info, $"Query Log: {_queryLogFilePath}");
            Log(LogLevel.Info, "==========================================================");
        }

        public string LogFilePath => _logFilePath;
        public string QueryLogFilePath => _queryLogFilePath;
        public string LogDirectory => _logDirectory;
        
        /// <summary>
        /// Get recent queries for UI display
        /// </summary>
        public List<QueryLogEntry> GetRecentQueries()
        {
            lock (_lock)
            {
                return new List<QueryLogEntry>(_recentQueries);
            }
        }

        public enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error,
            Critical
        }

        /// <summary>
        /// Converts LogLevel to CMTrace type value.
        /// CMTrace uses: 1=Info, 2=Warning, 3=Error
        /// </summary>
        private static int GetCMTraceType(LogLevel level) => level switch
        {
            LogLevel.Debug => 1,
            LogLevel.Info => 1,
            LogLevel.Warning => 2,
            LogLevel.Error => 3,
            LogLevel.Critical => 3,
            _ => 1
        };

        /// <summary>
        /// Formats a log entry in CMTrace-compatible format.
        /// Format: <![LOG[Message]LOG]!><time="HH:mm:ss.mmm+TZO" date="MM-DD-YYYY" component="Component" context="" type="N" thread="ThreadID" file="File:Line">
        /// </summary>
        private static string FormatCMTraceEntry(LogLevel level, string message, string component = ComponentName, 
            [CallerFilePath] string sourceFile = "", [CallerLineNumber] int sourceLine = 0)
        {
            var now = DateTime.Now;
            
            // Time format: HH:mm:ss.mmm+TZO (e.g., "14:30:45.123+000")
            var timeZoneOffset = TimeZoneInfo.Local.GetUtcOffset(now);
            var tzOffsetMinutes = (int)timeZoneOffset.TotalMinutes;
            var timeString = $"{now:HH:mm:ss.fff}{tzOffsetMinutes:+000;-000;+000}";
            
            // Date format: MM-DD-YYYY
            var dateString = now.ToString("MM-dd-yyyy");
            
            // Thread ID
            var threadId = Thread.CurrentThread.ManagedThreadId;
            
            // File name (just the filename, not full path)
            var fileName = string.IsNullOrEmpty(sourceFile) ? "Unknown" : Path.GetFileName(sourceFile);
            
            // CMTrace type: 1=Info, 2=Warning, 3=Error
            var cmTraceType = GetCMTraceType(level);
            
            // Escape special characters in message for XML compatibility
            var escapedMessage = message
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("\r\n", " ")
                .Replace("\n", " ")
                .Replace("\r", " ");
            
            // CMTrace format
            return $"<![LOG[{escapedMessage}]LOG]!><time=\"{timeString}\" date=\"{dateString}\" component=\"{component}\" context=\"\" type=\"{cmTraceType}\" thread=\"{threadId}\" file=\"{fileName}:{sourceLine}\">";
        }

        public void Log(LogLevel level, string message, 
            [CallerFilePath] string sourceFile = "", [CallerLineNumber] int sourceLine = 0)
        {
            try
            {
                var logEntry = FormatCMTraceEntry(level, message, ComponentName, sourceFile, sourceLine);

                lock (_lock)
                {
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                }

                // Also write to Debug output for DebugView compatibility (plain text for readability)
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var plainTextEntry = $"[{timestamp}] [{level.ToString().ToUpper().PadRight(8)}] {message}";
                System.Diagnostics.Debug.WriteLine(plainTextEntry);
            }
            catch (Exception ex)
            {
                // If logging fails, write to Debug output as fallback
                System.Diagnostics.Debug.WriteLine($"LOGGING ERROR: {ex.Message}");
            }
        }

        public void Debug(string message, [CallerFilePath] string f = "", [CallerLineNumber] int l = 0) => Log(LogLevel.Debug, message, f, l);
        public void Info(string message, [CallerFilePath] string f = "", [CallerLineNumber] int l = 0) => Log(LogLevel.Info, message, f, l);
        public void Warning(string message, [CallerFilePath] string f = "", [CallerLineNumber] int l = 0) => Log(LogLevel.Warning, message, f, l);
        public void Error(string message, [CallerFilePath] string f = "", [CallerLineNumber] int l = 0) => Log(LogLevel.Error, message, f, l);
        public void Critical(string message, [CallerFilePath] string f = "", [CallerLineNumber] int l = 0) => Log(LogLevel.Critical, message, f, l);

        /// <summary>
        /// Log a data query (Graph API, WMI, Admin Service) for transparency
        /// </summary>
        /// <param name="source">QuerySource enum indicating where the query is going</param>
        /// <param name="operation">What operation is being performed (e.g., "GetManagedDevices")</param>
        /// <param name="query">The actual query string (URL, WQL, etc.)</param>
        /// <param name="parameters">Optional parameters or select fields</param>
        /// <param name="resultCount">Number of results returned (-1 if not yet executed)</param>
        public void LogQuery(QuerySource source, string operation, string query, string? parameters = null, int resultCount = -1)
        {
            try
            {
                var timestamp = DateTime.Now;
                var entry = new QueryLogEntry
                {
                    Timestamp = timestamp,
                    Source = source,
                    Operation = operation,
                    Query = query,
                    Parameters = parameters,
                    ResultCount = resultCount
                };

                // Add to in-memory list for UI
                lock (_lock)
                {
                    _recentQueries.Add(entry);
                    if (_recentQueries.Count > MaxRecentQueries)
                    {
                        _recentQueries.RemoveAt(0);
                    }
                }

                // Format for file logging
                var sb = new StringBuilder();
                sb.AppendLine($"[{timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{source}] {operation}");
                sb.AppendLine($"   Query: {query}");
                if (!string.IsNullOrEmpty(parameters))
                {
                    sb.AppendLine($"   Parameters: {parameters}");
                }
                if (resultCount >= 0)
                {
                    sb.AppendLine($"   Results: {resultCount} records");
                }
                sb.AppendLine();

                // Write to query log file
                lock (_lock)
                {
                    File.AppendAllText(_queryLogFilePath, sb.ToString());
                }

                // Also log to main log at Debug level
                Log(LogLevel.Debug, $"[QUERY] [{source}] {operation}: {query.Substring(0, Math.Min(query.Length, 100))}...");
                
                System.Diagnostics.Debug.WriteLine($"[QUERY] [{source}] {operation}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"QUERY LOGGING ERROR: {ex.Message}");
            }
        }

        /// <summary>
        /// Log a Graph API query specifically
        /// </summary>
        public void LogGraphQuery(string operation, string endpoint, string[]? selectFields = null, string? filter = null, int resultCount = -1)
        {
            var query = $"https://graph.microsoft.com/v1.0{endpoint}";
            var paramList = new List<string>();
            
            if (selectFields != null && selectFields.Length > 0)
            {
                paramList.Add($"$select={string.Join(",", selectFields)}");
            }
            if (!string.IsNullOrEmpty(filter))
            {
                paramList.Add($"$filter={filter}");
            }
            
            var parameters = paramList.Count > 0 ? string.Join("&", paramList) : null;
            LogQuery(QuerySource.GraphAPI, operation, query, parameters, resultCount);
        }

        /// <summary>
        /// Log a WMI/WQL query specifically
        /// </summary>
        public void LogWmiQuery(string operation, string wqlQuery, string? @namespace = null, int resultCount = -1)
        {
            var query = wqlQuery;
            var parameters = @namespace != null ? $"Namespace: {@namespace}" : null;
            LogQuery(QuerySource.WMI, operation, query, parameters, resultCount);
        }

        /// <summary>
        /// Log a ConfigMgr Admin Service REST API query
        /// </summary>
        public void LogAdminServiceQuery(string operation, string url, int resultCount = -1)
        {
            LogQuery(QuerySource.AdminService, operation, url, null, resultCount);
        }

        /// <summary>
        /// Export all queries from today to a file for customer review
        /// </summary>
        public string ExportQueryLog()
        {
            try
            {
                var exportPath = Path.Combine(_logDirectory, $"QueryExport_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                var sb = new StringBuilder();
                
                sb.AppendLine("=".PadRight(80, '='));
                sb.AppendLine("Zero Trust Migration Journey - Query Log Export");
                sb.AppendLine($"Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine("=".PadRight(80, '='));
                sb.AppendLine();
                sb.AppendLine("This file contains all data queries executed by the add-in.");
                sb.AppendLine("You can use these queries in Graph Explorer, PowerShell, or WMI tools to reproduce the data.");
                sb.AppendLine();
                sb.AppendLine("-".PadRight(80, '-'));
                sb.AppendLine();

                lock (_lock)
                {
                    foreach (var entry in _recentQueries)
                    {
                        sb.AppendLine($"[{entry.Timestamp:HH:mm:ss}] [{entry.Source}] {entry.Operation}");
                        sb.AppendLine($"   Query: {entry.Query}");
                        if (!string.IsNullOrEmpty(entry.Parameters))
                        {
                            sb.AppendLine($"   Parameters: {entry.Parameters}");
                        }
                        if (entry.ResultCount >= 0)
                        {
                            sb.AppendLine($"   Results: {entry.ResultCount} records");
                        }
                        sb.AppendLine();
                    }
                }

                File.WriteAllText(exportPath, sb.ToString());
                Info($"Query log exported to: {exportPath}");
                return exportPath;
            }
            catch (Exception ex)
            {
                Error($"Failed to export query log: {ex.Message}");
                return string.Empty;
            }
        }

        public void LogException(Exception ex, string context = "", 
            [CallerFilePath] string sourceFile = "", [CallerLineNumber] int sourceLine = 0)
        {
            // Log exception details in CMTrace-friendly format (single line per entry for proper parsing)
            var exceptionType = ex.GetType().Name;
            var message = ex.Message.Replace("\r\n", " ").Replace("\n", " ");
            
            Log(LogLevel.Error, $"EXCEPTION in {context}: [{exceptionType}] {message}", sourceFile, sourceLine);
            
            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                // Log stack trace as separate warning entries for better CMTrace viewing
                var stackLines = ex.StackTrace.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in stackLines.Take(10)) // Limit to first 10 stack frames
                {
                    Log(LogLevel.Warning, $"  Stack: {line.Trim()}", sourceFile, sourceLine);
                }
            }
            
            if (ex.InnerException != null)
            {
                Log(LogLevel.Error, $"  Inner Exception: [{ex.InnerException.GetType().Name}] {ex.InnerException.Message.Replace("\r\n", " ").Replace("\n", " ")}", sourceFile, sourceLine);
            }
        }

        public void LogSeparator([CallerFilePath] string f = "", [CallerLineNumber] int l = 0)
        {
            Log(LogLevel.Info, "--------------------------------------------------------------------------------", f, l);
        }

        /// <summary>
        /// Clean up old log files (keep last 7 days)
        /// </summary>
        public void CleanupOldLogs(int daysToKeep = 7)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                var logFiles = Directory.GetFiles(_logDirectory, "ZeroTrustMigrationAddin_*.log");

                foreach (var file in logFiles)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime < cutoffDate)
                    {
                        File.Delete(file);
                        Log(LogLevel.Info, $"Deleted old log file: {fileInfo.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Warning, $"Failed to cleanup old logs: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the current log file size in MB
        /// </summary>
        public double GetLogFileSizeMB()
        {
            try
            {
                var fileInfo = new FileInfo(_logFilePath);
                return fileInfo.Exists ? fileInfo.Length / 1024.0 / 1024.0 : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Open the log directory in Windows Explorer
        /// </summary>
        public void OpenLogDirectory()
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", _logDirectory);
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to open log directory: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Types of data sources that can be queried
    /// </summary>
    public enum QuerySource
    {
        /// <summary>Microsoft Graph API (Intune, Azure AD, etc.)</summary>
        GraphAPI,
        /// <summary>ConfigMgr Admin Service REST API</summary>
        AdminService,
        /// <summary>WMI/WQL queries to ConfigMgr</summary>
        WMI,
        /// <summary>Local file system or registry</summary>
        Local
    }

    /// <summary>
    /// Represents a single query log entry for transparency/debugging
    /// </summary>
    public class QueryLogEntry
    {
        public DateTime Timestamp { get; set; }
        public QuerySource Source { get; set; }
        public string Operation { get; set; } = string.Empty;
        public string Query { get; set; } = string.Empty;
        public string? Parameters { get; set; }
        public int ResultCount { get; set; } = -1;

        /// <summary>
        /// Source displayed as friendly string with icon
        /// </summary>
        public string SourceDisplay => Source switch
        {
            QuerySource.GraphAPI => "🌐 Graph API",
            QuerySource.AdminService => "🔧 Admin Service",
            QuerySource.WMI => "💻 WMI",
            QuerySource.Local => "📁 Local",
            _ => Source.ToString()
        };

        /// <summary>
        /// Formatted query for display
        /// </summary>
        public string FormattedQuery
        {
            get
            {
                var sb = new StringBuilder();
                sb.AppendLine(Query);
                if (!string.IsNullOrEmpty(Parameters))
                {
                    sb.AppendLine($"  {Parameters}");
                }
                return sb.ToString().TrimEnd();
            }
        }

        /// <summary>
        /// Copy-friendly version for Graph Explorer or PowerShell
        /// </summary>
        public string CopyableQuery
        {
            get
            {
                if (Source == QuerySource.GraphAPI && !string.IsNullOrEmpty(Parameters))
                {
                    return $"{Query}?{Parameters}";
                }
                return Query;
            }
        }
    }
}
