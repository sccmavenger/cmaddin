using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ZeroTrustMigrationAddin.Services
{
    /// <summary>
    /// File-based logger for comprehensive debug information.
    /// Logs to %LOCALAPPDATA%\ZeroTrustMigrationAddin\Logs\
    /// </summary>
    public class FileLogger
    {
        private static FileLogger? _instance;
        private static readonly object _lock = new object();
        private readonly string _logFilePath;
        private readonly string _queryLogFilePath;
        private readonly string _logDirectory;
        
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

            // Write startup header
            Log(LogLevel.Info, "=".PadRight(80, '='));
            Log(LogLevel.Info, $"CloudJourney Add-in Started - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Log(LogLevel.Info, $"Log File: {_logFilePath}");
            Log(LogLevel.Info, $"Query Log: {_queryLogFilePath}");
            Log(LogLevel.Info, "=".PadRight(80, '='));
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

        public void Log(LogLevel level, string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logEntry = $"[{timestamp}] [{level.ToString().ToUpper().PadRight(8)}] {message}";

                lock (_lock)
                {
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                }

                // Also write to Debug output for DebugView compatibility
                System.Diagnostics.Debug.WriteLine(logEntry);
            }
            catch (Exception ex)
            {
                // If logging fails, write to Debug output as fallback
                System.Diagnostics.Debug.WriteLine($"LOGGING ERROR: {ex.Message}");
            }
        }

        public void Debug(string message) => Log(LogLevel.Debug, message);
        public void Info(string message) => Log(LogLevel.Info, message);
        public void Warning(string message) => Log(LogLevel.Warning, message);
        public void Error(string message) => Log(LogLevel.Error, message);
        public void Critical(string message) => Log(LogLevel.Critical, message);

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

        public void LogException(Exception ex, string context = "")
        {
            var sb = new StringBuilder();
            sb.AppendLine($"EXCEPTION in {context}:");
            sb.AppendLine($"  Type: {ex.GetType().Name}");
            sb.AppendLine($"  Message: {ex.Message}");
            sb.AppendLine($"  Stack Trace: {ex.StackTrace}");
            
            if (ex.InnerException != null)
            {
                sb.AppendLine($"  Inner Exception: {ex.InnerException.GetType().Name}");
                sb.AppendLine($"  Inner Message: {ex.InnerException.Message}");
            }

            Log(LogLevel.Error, sb.ToString());
        }

        public void LogSeparator()
        {
            Log(LogLevel.Info, "".PadRight(80, '-'));
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
