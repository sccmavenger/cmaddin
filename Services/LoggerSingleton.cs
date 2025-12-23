using System;
using System.IO;

namespace CloudJourneyAddin.Services
{
    /// <summary>
    /// Global Logger singleton for use across all services (Phase 3 requirement)
    /// </summary>
    public sealed class Logger
    {
        private static readonly Lazy<Logger> _instance = new Lazy<Logger>(() => new Logger());
        private readonly FileLogger _fileLogger;

        private Logger()
        {
            _fileLogger = FileLogger.Instance;
        }

        public static Logger Instance => _instance.Value;

        public void Info(string message)
        {
            _fileLogger.Info(message);
        }

        public void Warning(string message)
        {
            _fileLogger.Warning(message);
        }

        public void Error(string message)
        {
            _fileLogger.Error(message);
        }

        public void LogException(Exception ex, string context = "")
        {
            _fileLogger.LogException(ex, context);
        }
    }
}
