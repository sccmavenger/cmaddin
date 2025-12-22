using System;
using System.IO;
using System.Text;

namespace CloudJourneyAddin.Services
{
    /// <summary>
    /// File-based logger for comprehensive debug information.
    /// Logs to %LOCALAPPDATA%\CloudJourneyAddin\Logs\
    /// </summary>
    public class FileLogger
    {
        private static FileLogger? _instance;
        private static readonly object _lock = new object();
        private readonly string _logFilePath;
        private readonly string _logDirectory;

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
            // Log to %LOCALAPPDATA%\CloudJourneyAddin\Logs\
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _logDirectory = Path.Combine(appDataPath, "CloudJourneyAddin", "Logs");
            
            // Create directory if it doesn't exist
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            // Log file name includes date for easy organization
            var logFileName = $"CloudJourneyAddin_{DateTime.Now:yyyyMMdd}.log";
            _logFilePath = Path.Combine(_logDirectory, logFileName);

            // Write startup header
            Log(LogLevel.Info, "=".PadRight(80, '='));
            Log(LogLevel.Info, $"CloudJourney Add-in Started - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Log(LogLevel.Info, $"Log File: {_logFilePath}");
            Log(LogLevel.Info, "=".PadRight(80, '='));
        }

        public string LogFilePath => _logFilePath;
        public string LogDirectory => _logDirectory;

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
                var logFiles = Directory.GetFiles(_logDirectory, "CloudJourneyAddin_*.log");

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
}
