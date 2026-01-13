using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CloudJourneyAddin.Models;
using Newtonsoft.Json;
using Octokit;
using static CloudJourneyAddin.Services.FileLogger;

namespace CloudJourneyAddin.Services
{
    /// <summary>
    /// Service for checking GitHub Releases for CloudJourneyAddin updates.
    /// Uses Octokit to query sccmavenger/cmaddin repository.
    /// </summary>
    public class GitHubUpdateService
    {
        private const string RepoOwner = "sccmavenger";
        private const string RepoName = "cmaddin";
        private readonly GitHubClient _client;
        private readonly string _settingsPath;
        private UpdateSettings? _settings;

        public GitHubUpdateService()
        {
            _client = new GitHubClient(new ProductHeaderValue("CloudJourneyAddin"));
            
            // Load settings and configure authentication if token exists
            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CloudJourneyAddin",
                "update-settings.json");
            
            LoadSettings();

            if (!string.IsNullOrEmpty(_settings?.GitHubToken))
            {
                _client.Credentials = new Credentials(_settings.GitHubToken);
                Instance.Info("GitHub API authenticated with Personal Access Token");
            }
            else
            {
                Instance.Info("GitHub API using anonymous access (60 req/hr limit)");
            }
        }

        /// <summary>
        /// Loads update settings from local storage.
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    _settings = JsonConvert.DeserializeObject<UpdateSettings>(json);
                    Instance.Info($"Update settings loaded from {_settingsPath}");
                }
                else
                {
                    _settings = new UpdateSettings();
                    Instance.Info("No update settings found, using defaults");
                }
            }
            catch (Exception ex)
            {
                Instance.Warning($"Failed to load update settings: {ex.Message}");
                _settings = new UpdateSettings();
            }
        }

        /// <summary>
        /// Saves update settings to local storage.
        /// </summary>
        private void SaveSettings()
        {
            try
            {
                var directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                File.WriteAllText(_settingsPath, json);
                Instance.Info("Update settings saved successfully");
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to save update settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the current application version from assembly metadata.
        /// </summary>
        public string GetCurrentVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.0.0";
        }

        /// <summary>
        /// Gets the latest release from GitHub.
        /// </summary>
        public async Task<Release?> GetLatestReleaseAsync()
        {
            try
            {
                Instance.Info($"Checking GitHub for latest release: {RepoOwner}/{RepoName}");
                var release = await _client.Repository.Release.GetLatest(RepoOwner, RepoName);
                Instance.Info($"Latest release found: {release.TagName} (Published: {release.PublishedAt})");
                return release;
            }
            catch (NotFoundException)
            {
                Instance.Warning($"No releases found in repository {RepoOwner}/{RepoName}");
                return null;
            }
            catch (RateLimitExceededException ex)
            {
                Instance.Error($"GitHub API rate limit exceeded. Resets at: {ex.Reset}");
                return null;
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to get latest release: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Checks if an update is available compared to current version.
        /// </summary>
        public async Task<bool> IsUpdateAvailableAsync(string currentVersion)
        {
            var release = await GetLatestReleaseAsync();
            if (release == null) return false;

            // Parse version from tag (e.g., "v3.14.25" -> "3.14.25")
            var latestVersion = release.TagName.TrimStart('v');
            
            try
            {
                var current = Version.Parse(currentVersion);
                var latest = Version.Parse(latestVersion);
                
                var isNewer = latest > current;
                Instance.Info($"Version comparison: Current={currentVersion}, Latest={latestVersion}, UpdateAvailable={isNewer}");
                return isNewer;
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to parse versions: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Performs a comprehensive update check and returns detailed results.
        /// </summary>
        public async Task<UpdateCheckResult> CheckForUpdatesAsync()
        {
            var result = new UpdateCheckResult
            {
                CurrentVersion = GetCurrentVersion()
            };

            try
            {
                // Update last check timestamp
                if (_settings != null)
                {
                    _settings.LastUpdateCheck = DateTime.UtcNow;
                    SaveSettings();
                }

                var release = await GetLatestReleaseAsync();
                if (release == null)
                {
                    result.ErrorMessage = "Failed to retrieve release information from GitHub";
                    return result;
                }

                result.LatestVersion = release.TagName.TrimStart('v');
                result.ReleaseDate = release.PublishedAt?.UtcDateTime;
                result.ReleaseNotes = release.Body;

                // Check if user skipped this version
                if (_settings?.SkippedVersions?.Contains(result.LatestVersion) == true)
                {
                    Instance.Info($"Version {result.LatestVersion} was skipped by user");
                    result.IsUpdateAvailable = false;
                    return result;
                }

                // Compare versions
                try
                {
                    var current = Version.Parse(result.CurrentVersion);
                    var latest = Version.Parse(result.LatestVersion);
                    result.IsUpdateAvailable = latest > current;
                }
                catch (Exception ex)
                {
                    Instance.Error($"Failed to parse versions: {ex.Message}");
                    result.ErrorMessage = $"Version parsing failed: {ex.Message}";
                    return result;
                }

                if (!result.IsUpdateAvailable)
                {
                    Instance.Info("No update available - current version is up to date");
                    return result;
                }

                // Find ZIP and manifest assets
                foreach (var asset in release.Assets)
                {
                    if (asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        result.DownloadUrl = asset.BrowserDownloadUrl;
                        Instance.Info($"Found ZIP asset: {asset.Name} ({asset.Size:N0} bytes)");
                    }
                    else if (asset.Name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                    {
                        result.ManifestUrl = asset.BrowserDownloadUrl;
                        Instance.Info($"Found manifest asset: {asset.Name}");
                    }
                }

                if (string.IsNullOrEmpty(result.DownloadUrl))
                {
                    result.ErrorMessage = "No ZIP package found in release assets";
                    Instance.Warning(result.ErrorMessage);
                }

                Instance.Info($"✅ Update available: {result.CurrentVersion} → {result.LatestVersion}");
                return result;
            }
            catch (Exception ex)
            {
                Instance.Error($"Update check failed: {ex.Message}");
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Marks a specific version as skipped by the user.
        /// </summary>
        public void SkipVersion(string version)
        {
            if (_settings == null) return;

            if (!_settings.SkippedVersions.Contains(version))
            {
                _settings.SkippedVersions.Add(version);
                SaveSettings();
                Instance.Info($"Version {version} marked as skipped");
            }
        }

        /// <summary>
        /// Checks if update check should be performed.
        /// Always returns true to check on every launch.
        /// </summary>
        public bool ShouldCheckForUpdates()
        {
            if (_settings?.AutoCheckForUpdates == false)
            {
                Instance.Info("Auto-update check is disabled");
                return false;
            }

            Instance.Info("Checking for updates on every launch");
            return true;
        }

        /// <summary>
        /// Gets the current update settings.
        /// </summary>
        public UpdateSettings GetSettings()
        {
            return _settings ?? new UpdateSettings();
        }

        /// <summary>
        /// Updates and saves settings.
        /// </summary>
        public void UpdateSettings(UpdateSettings settings)
        {
            _settings = settings;
            SaveSettings();

            // Update GitHub client credentials if token changed
            if (!string.IsNullOrEmpty(settings.GitHubToken))
            {
                _client.Credentials = new Credentials(settings.GitHubToken);
                Instance.Info("GitHub API credentials updated");
            }
        }
    }
}
