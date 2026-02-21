using System;
using System.IO;
using System.Text.Json;

namespace ZeroTrustMigrationAddin.Models
{
    /// <summary>
    /// User preferences with persistence to local file.
    /// Stored in %LOCALAPPDATA%\ZeroTrustMigrationAddin\user-preferences.json
    /// </summary>
    public class UserPreferencesSettings
    {
        private static UserPreferencesSettings? _instance;
        private static readonly object _lock = new();
        private static readonly string SettingsFilePath;

        static UserPreferencesSettings()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var settingsDir = Path.Combine(appDataPath, "ZeroTrustMigrationAddin");
            if (!Directory.Exists(settingsDir))
                Directory.CreateDirectory(settingsDir);
            SettingsFilePath = Path.Combine(settingsDir, "user-preferences.json");
        }

        /// <summary>UI scale percentage (default: 100, range: 80-150)</summary>
        public int UIScalePercent { get; set; } = 100;

        /// <summary>Minimum allowed scale percentage</summary>
        public const int MinScale = 80;

        /// <summary>Maximum allowed scale percentage</summary>
        public const int MaxScale = 150;

        /// <summary>Scale increment step</summary>
        public const int ScaleStep = 10;

        /// <summary>Get singleton instance (loads from disk)</summary>
        public static UserPreferencesSettings Instance
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

        /// <summary>Get scale as decimal (e.g., 1.0 for 100%)</summary>
        public double ScaleFactor => UIScalePercent / 100.0;

        /// <summary>Save settings to disk</summary>
        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch { /* Ignore save errors */ }
        }

        /// <summary>Increase zoom by one step</summary>
        public bool ZoomIn()
        {
            if (UIScalePercent < MaxScale)
            {
                UIScalePercent = Math.Min(UIScalePercent + ScaleStep, MaxScale);
                Save();
                return true;
            }
            return false;
        }

        /// <summary>Decrease zoom by one step</summary>
        public bool ZoomOut()
        {
            if (UIScalePercent > MinScale)
            {
                UIScalePercent = Math.Max(UIScalePercent - ScaleStep, MinScale);
                Save();
                return true;
            }
            return false;
        }

        /// <summary>Reset zoom to 100%</summary>
        public void ResetZoom()
        {
            UIScalePercent = 100;
            Save();
        }

        private static UserPreferencesSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<UserPreferencesSettings>(json);
                    if (settings != null)
                    {
                        // Clamp to valid range
                        settings.UIScalePercent = Math.Clamp(settings.UIScalePercent, MinScale, MaxScale);
                        return settings;
                    }
                }
            }
            catch { /* Ignore load errors */ }
            return new UserPreferencesSettings();
        }
    }
}
