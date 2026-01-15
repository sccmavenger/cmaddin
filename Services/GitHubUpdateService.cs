using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ZeroTrustMigrationAddin.Models;
using Newtonsoft.Json;
using Octokit;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Services
{
    /// <summary>
    /// Service for checking GitHub Releases for CloudJourneyAddin updates.
    /// Uses Octokit to query sccmavenger/cmaddin repository.
    /// </summary>
    public class GitHubUpdateService
    {
        private const string RepoOwner = "sccmavenger";
        private const string RepoName = "cmaddin";
        
        // Token should be set via environment variable or update-settings.json for security
        // Note: Public repositories don't require authentication for releases
        private const string EmbeddedToken = ""; // Removed for security - use GITHUB_TOKEN env var if needed
        
        private readonly GitHubClient _client;
        private readonly string _settingsPath;
        private UpdateSettings? _settings;

        public GitHubUpdateService()
        {
            _client = new GitHubClient(new ProductHeaderValue("ZeroTrustMigrationAddin"));
            
            // Load settings and configure authentication if token exists
            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ZeroTrustMigrationAddin",
                "update-settings.json");
            
            LoadSettings();

            // Priority: User's token > Embedded token > Anonymous
            string? tokenToUse = null;
            string authSource = "Anonymous (60 req/hr)";
            
            if (!string.IsNullOrEmpty(_settings?.GitHubToken))
            {
                tokenToUse = _settings.GitHubToken;
                authSource = "User-configured token";
            }
            else if (!string.IsNullOrEmpty(EmbeddedToken))
            {
                tokenToUse = EmbeddedToken;
                authSource = "Embedded token (5,000 req/hr)";
            }
            
            if (!string.IsNullOrEmpty(tokenToUse))
            {
                _client.Credentials = new Credentials(tokenToUse);
                Instance.Info($"GitHub API authenticated with {authSource}");
            }
            else
            {
                Instance.Info($"GitHub API using {authSource}");
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
                var isAuthenticated = _client.Credentials != Credentials.Anonymous;
                
                // Track authentication status in telemetry
                AzureTelemetryService.Instance.TrackEvent("UpdateCheckStarted", new System.Collections.Generic.Dictionary<string, string>
                {
                    { "AuthenticationMethod", isAuthenticated ? "Authenticated" : "Anonymous" },
                    { "Repository", $"{RepoOwner}/{RepoName}" },
                    { "HasGitHubToken", isAuthenticated.ToString() }
                });
                
                Instance.Info($"🔍 [DEBUG] Checking GitHub for latest release: {RepoOwner}/{RepoName}");
                Instance.Info($"🔍 [DEBUG] API Endpoint: https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest");
                Instance.Info($"🔍 [DEBUG] Authentication: {(isAuthenticated ? "Authenticated" : "Anonymous (60/hr)")}");
                
                // Try to get all releases first to debug
                Instance.Info($"🔍 [DEBUG] Fetching ALL releases to diagnose issue...");
                var allReleases = await _client.Repository.Release.GetAll(RepoOwner, RepoName);
                Instance.Info($"🔍 [DEBUG] Total releases found: {allReleases.Count}");
                
                foreach (var r in allReleases.Take(5))
                {
                    Instance.Info($"🔍 [DEBUG]   - {r.TagName} | Draft={r.Draft} | Prerelease={r.Prerelease} | Assets={r.Assets.Count} | Published={r.PublishedAt}");
                }
                
                // Now try GetLatest
                Instance.Info($"🔍 [DEBUG] Now calling GetLatest()...");
                var release = await _client.Repository.Release.GetLatest(RepoOwner, RepoName);
                Instance.Info($"✅ [DEBUG] GetLatest() SUCCESS: {release.TagName} (Published: {release.PublishedAt})");
                Instance.Info($"🔍 [DEBUG] Release has {release.Assets.Count} assets");
                
                foreach (var asset in release.Assets)
                {
                    Instance.Info($"🔍 [DEBUG]   - Asset: {asset.Name} ({asset.Size:N0} bytes)");
                }
                
                // Track successful update check
                AzureTelemetryService.Instance.TrackEvent("UpdateCheckSuccess", new System.Collections.Generic.Dictionary<string, string>
                {
                    { "LatestVersion", release.TagName },
                    { "AssetCount", release.Assets.Count.ToString() },
                    { "PublishedDate", release.PublishedAt?.ToString("yyyy-MM-dd") ?? "unknown" }
                });
                
                return release;
            }
            catch (NotFoundException ex)
            {
                Instance.Warning($"❌ [DEBUG] NotFoundException caught!");
                Instance.Warning($"🔍 [DEBUG] Exception message: {ex.Message}");
                Instance.Warning($"🔍 [DEBUG] Status code: {ex.StatusCode}");
                Instance.Warning($"No releases found in repository {RepoOwner}/{RepoName}");
                
                // Track 404 error - likely private repo without token
                AzureTelemetryService.Instance.TrackEvent("UpdateCheckFailed", new System.Collections.Generic.Dictionary<string, string>
                {
                    { "ErrorType", "NotFoundException" },
                    { "StatusCode", ex.StatusCode.ToString() },
                    { "IsAuthenticated", (_client.Credentials != Credentials.Anonymous).ToString() },
                    { "Message", "Repository not found or inaccessible (likely private repo without token)" }
                });
                
                return null;
            }
            catch (RateLimitExceededException ex)
            {
                Instance.Error($"❌ [DEBUG] Rate limit exceeded!");
                Instance.Error($"GitHub API rate limit exceeded. Resets at: {ex.Reset}");
                Instance.Error($"🔍 [DEBUG] Limit: {ex.Limit}, Remaining: {ex.Remaining}");
                
                // Track rate limit error
                AzureTelemetryService.Instance.TrackEvent("UpdateCheckFailed", new System.Collections.Generic.Dictionary<string, string>
                {
                    { "ErrorType", "RateLimitExceeded" },
                    { "ResetTime", ex.Reset.ToString() },
                    { "Limit", ex.Limit.ToString() },
                    { "Remaining", ex.Remaining.ToString() },
                    { "IsAuthenticated", (_client.Credentials != Credentials.Anonymous).ToString() }
                });
                
                return null;
            }
            catch (Exception ex)
            {
                Instance.Error($"❌ [DEBUG] Unexpected error in GetLatestReleaseAsync()");
                Instance.Error($"🔍 [DEBUG] Exception type: {ex.GetType().Name}");
                Instance.Error($"🔍 [DEBUG] Message: {ex.Message}");
                Instance.Error($"🔍 [DEBUG] Stack trace: {ex.StackTrace}");
                Instance.Error($"Failed to get latest release: {ex.Message}");
                
                // Track unexpected error
                AzureTelemetryService.Instance.TrackEvent("UpdateCheckFailed", new System.Collections.Generic.Dictionary<string, string>
                {
                    { "ErrorType", ex.GetType().Name },
                    { "Message", ex.Message },
                    { "IsAuthenticated", (_client.Credentials != Credentials.Anonymous).ToString() }
                });
                
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
                        // Use API URL for private repos (requires authentication), BrowserDownloadUrl for public
                        result.DownloadUrl = !string.IsNullOrEmpty(_settings?.GitHubToken) 
                            ? asset.Url  // API endpoint (authenticated)
                            : asset.BrowserDownloadUrl;  // Direct download (public repos)
                        result.TotalSize = asset.Size;  // Store ZIP size for bandwidth savings calculation
                        Instance.Info($"Found ZIP asset: {asset.Name} ({asset.Size:N0} bytes)");
                    }
                    else if (asset.Name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                    {
                        // Use API URL for private repos (requires authentication), BrowserDownloadUrl for public
                        result.ManifestUrl = !string.IsNullOrEmpty(_settings?.GitHubToken) 
                            ? asset.Url  // API endpoint (authenticated)
                            : asset.BrowserDownloadUrl;  // Direct download (public repos)
                        Instance.Info($"Found manifest asset: {asset.Name}");
                        Instance.Info($"🔍 [DEBUG] Manifest URL: {result.ManifestUrl}");
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
