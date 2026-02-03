using System;
using System.IO;
using Newtonsoft.Json;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Models
{
    /// <summary>
    /// Authentication method for Microsoft Graph.
    /// </summary>
    public enum GraphAuthMethod
    {
        /// <summary>
        /// Opens browser directly for authentication. Recommended for corporate environments.
        /// </summary>
        InteractiveBrowser,
        
        /// <summary>
        /// User enters a code at microsoft.com/devicelogin. Legacy method.
        /// </summary>
        DeviceCode
    }

    /// <summary>
    /// Configuration settings for Microsoft Graph authentication.
    /// Supports custom app registrations and authentication method selection.
    /// </summary>
    public class GraphAuthSettings
    {
        /// <summary>
        /// Default Client ID: Microsoft Graph Command Line Tools (public multi-tenant app)
        /// </summary>
        public const string DefaultClientId = "14d82eec-204b-4c2f-b7e8-296a70dab67e";
        
        /// <summary>
        /// Default Tenant ID: "organizations" allows any Azure AD tenant
        /// </summary>
        public const string DefaultTenantId = "organizations";

        /// <summary>
        /// Authentication method to use. Default is InteractiveBrowser.
        /// </summary>
        public GraphAuthMethod AuthMethod { get; set; } = GraphAuthMethod.InteractiveBrowser;

        /// <summary>
        /// Custom Azure AD Application (Client) ID. Leave null to use default.
        /// </summary>
        public string? CustomClientId { get; set; }

        /// <summary>
        /// Custom Tenant ID. Leave null for "organizations" (auto-detect).
        /// </summary>
        public string? CustomTenantId { get; set; }

        /// <summary>
        /// The detected tenant name after successful authentication.
        /// </summary>
        public string? DetectedTenantName { get; set; }

        /// <summary>
        /// The detected tenant ID after successful authentication.
        /// </summary>
        public string? DetectedTenantId { get; set; }

        /// <summary>
        /// Whether custom app registration is enabled.
        /// </summary>
        public bool UseCustomApp { get; set; } = false;

        /// <summary>
        /// Gets the effective Client ID to use for authentication.
        /// </summary>
        [JsonIgnore]
        public string EffectiveClientId => 
            UseCustomApp && !string.IsNullOrWhiteSpace(CustomClientId) 
                ? CustomClientId 
                : Environment.GetEnvironmentVariable("GRAPH_CLIENT_ID") ?? DefaultClientId;

        /// <summary>
        /// Gets the effective Tenant ID to use for authentication.
        /// </summary>
        [JsonIgnore]
        public string EffectiveTenantId => 
            UseCustomApp && !string.IsNullOrWhiteSpace(CustomTenantId) 
                ? CustomTenantId 
                : Environment.GetEnvironmentVariable("GRAPH_TENANT_ID") ?? DefaultTenantId;

        /// <summary>
        /// Path to the settings file.
        /// </summary>
        private static string ConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZeroTrustMigrationAddin",
            "graph-auth-settings.json");

        /// <summary>
        /// Loads settings from disk, or returns defaults if not found.
        /// </summary>
        public static GraphAuthSettings Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var settings = JsonConvert.DeserializeObject<GraphAuthSettings>(json);
                    if (settings != null)
                    {
                        Instance.Info($"Graph auth settings loaded: Method={settings.AuthMethod}, UseCustomApp={settings.UseCustomApp}");
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to load graph auth settings: {ex.Message}");
            }

            Instance.Info("Using default graph auth settings (InteractiveBrowser)");
            return new GraphAuthSettings();
        }

        /// <summary>
        /// Saves settings to disk.
        /// </summary>
        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
                Instance.Info($"Graph auth settings saved: Method={AuthMethod}, UseCustomApp={UseCustomApp}");
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to save graph auth settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the detected tenant information after successful authentication.
        /// </summary>
        public void UpdateDetectedTenant(string? tenantId, string? tenantName)
        {
            DetectedTenantId = tenantId;
            DetectedTenantName = tenantName;
            Save();
        }

        /// <summary>
        /// Resets to default settings.
        /// </summary>
        public void Reset()
        {
            AuthMethod = GraphAuthMethod.InteractiveBrowser;
            UseCustomApp = false;
            CustomClientId = null;
            CustomTenantId = null;
            DetectedTenantName = null;
            DetectedTenantId = null;
            Save();
        }
    }
}
