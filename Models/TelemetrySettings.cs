using System;
using System.IO;
using System.Text.Json;

namespace ZeroTrustMigrationAddin.Models
{
    /// <summary>
    /// Telemetry settings with persistence to local file.
    /// Stored in %LOCALAPPDATA%\ZeroTrustMigrationAddin\telemetry-settings.json
    /// </summary>
    public class TelemetrySettings
    {
        private static TelemetrySettings? _instance;
        private static readonly object _lock = new();
        private static readonly string SettingsFilePath;

        static TelemetrySettings()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var settingsDir = Path.Combine(appDataPath, "ZeroTrustMigrationAddin");
            if (!Directory.Exists(settingsDir))
                Directory.CreateDirectory(settingsDir);
            SettingsFilePath = Path.Combine(settingsDir, "telemetry-settings.json");
        }

        /// <summary>Telemetry enabled (default: true)</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>Whether user has acknowledged the first-run telemetry notice</summary>
        public bool HasAcknowledgedNotice { get; set; } = false;

        /// <summary>Timestamp when setting was last changed</summary>
        public DateTime LastChanged { get; set; } = DateTime.UtcNow;

        /// <summary>Get singleton instance (loads from disk)</summary>
        public static TelemetrySettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= Load();
                    }
                }
                return _instance;
            }
        }

        /// <summary>Save settings to disk</summary>
        public void Save()
        {
            try
            {
                LastChanged = DateTime.UtcNow;
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch { /* Ignore save errors */ }
        }

        private static TelemetrySettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    return JsonSerializer.Deserialize<TelemetrySettings>(json) ?? new TelemetrySettings();
                }
            }
            catch { /* Ignore load errors */ }
            return new TelemetrySettings();
        }
    }
}
