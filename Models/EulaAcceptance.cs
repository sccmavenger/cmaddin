using System;
using System.IO;
using Newtonsoft.Json;

namespace ZeroTrustMigrationAddin.Models
{
    /// <summary>
    /// Stores EULA acceptance state for the application
    /// </summary>
    public class EulaAcceptance
    {
        /// <summary>
        /// Current EULA version - increment when EULA changes to require re-acceptance
        /// </summary>
        public const string CurrentEulaVersion = "1.0";

        /// <summary>
        /// Version of EULA that was accepted
        /// </summary>
        public string? AcceptedVersion { get; set; }

        /// <summary>
        /// Date/time when EULA was accepted
        /// </summary>
        public DateTime? AcceptedDate { get; set; }

        /// <summary>
        /// Machine name where EULA was accepted
        /// </summary>
        public string? MachineName { get; set; }

        /// <summary>
        /// Windows username who accepted
        /// </summary>
        public string? AcceptedBy { get; set; }

        /// <summary>
        /// Returns true if the current EULA version has been accepted
        /// </summary>
        [JsonIgnore]
        public bool IsAccepted => AcceptedVersion == CurrentEulaVersion && AcceptedDate.HasValue;

        private static string ConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZeroTrustMigrationAddin",
            "eula-acceptance.json");

        /// <summary>
        /// Load EULA acceptance state from disk
        /// </summary>
        public static EulaAcceptance Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var acceptance = JsonConvert.DeserializeObject<EulaAcceptance>(json);
                    return acceptance ?? new EulaAcceptance();
                }
            }
            catch (Exception ex)
            {
                Services.FileLogger.Instance.LogException(ex, "EulaAcceptance.Load");
            }

            return new EulaAcceptance();
        }

        /// <summary>
        /// Save EULA acceptance state to disk
        /// </summary>
        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);

                Services.FileLogger.Instance.Info($"[EULA] Acceptance saved - Version: {AcceptedVersion}, Date: {AcceptedDate}");
            }
            catch (Exception ex)
            {
                Services.FileLogger.Instance.LogException(ex, "EulaAcceptance.Save");
            }
        }

        /// <summary>
        /// Record acceptance of the current EULA version
        /// </summary>
        public static EulaAcceptance RecordAcceptance()
        {
            var acceptance = new EulaAcceptance
            {
                AcceptedVersion = CurrentEulaVersion,
                AcceptedDate = DateTime.UtcNow,
                MachineName = Environment.MachineName,
                AcceptedBy = Environment.UserName
            };

            acceptance.Save();

            // Track telemetry
            Services.AzureTelemetryService.Instance.TrackEvent("EulaAccepted", new System.Collections.Generic.Dictionary<string, string>
            {
                { "Version", CurrentEulaVersion },
                { "AcceptedAt", acceptance.AcceptedDate?.ToString("o") ?? "Unknown" }
            });

            return acceptance;
        }

        /// <summary>
        /// Clear acceptance (for testing or when EULA version changes)
        /// </summary>
        public static void ClearAcceptance()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    File.Delete(ConfigPath);
                    Services.FileLogger.Instance.Info("[EULA] Acceptance cleared");
                }
            }
            catch (Exception ex)
            {
                Services.FileLogger.Instance.LogException(ex, "EulaAcceptance.ClearAcceptance");
            }
        }
    }
}
