using System;
using System.Collections.Generic;
using ZeroTrustMigrationAddin.Services;

namespace ZeroTrustMigrationAddin.Models
{
    /// <summary>
    /// Represents the manifest file containing version information and file entries for updates.
    /// Generated during build process and included in GitHub Releases.
    /// </summary>
    public class UpdateManifest
    {
        /// <summary>
        /// Version number (e.g., "3.14.25")
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Build date in ISO 8601 format
        /// </summary>
        public DateTime BuildDate { get; set; }

        /// <summary>
        /// List of all files in the release package
        /// </summary>
        public List<FileEntry> Files { get; set; } = new List<FileEntry>();

        /// <summary>
        /// Total size of all files in bytes
        /// </summary>
        public long TotalSize { get; set; }
    }

    /// <summary>
    /// Represents a single file entry in the update manifest with hash and metadata.
    /// </summary>
    public class FileEntry
    {
        /// <summary>
        /// Relative path within the installation directory (e.g., "ZeroTrustMigrationAddin.exe")
        /// </summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>
        /// SHA256 hash for file integrity verification
        /// </summary>
        public string SHA256Hash { get; set; } = string.Empty;

        /// <summary>
        /// File size in bytes
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Last modified timestamp
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Indicates if this is a critical file (exe, core DLLs) that must be updated
        /// </summary>
        public bool IsCritical { get; set; }
    }

    /// <summary>
    /// User settings for auto-update behavior.
    /// Stored in %LocalAppData%\ZeroTrustMigrationAddin\update-settings.json
    /// SECURITY: GitHub token is encrypted using Windows DPAPI.
    /// </summary>
    public class UpdateSettings
    {
        /// <summary>
        /// Encrypted GitHub Personal Access Token (DPAPI protected).
        /// Use SetGitHubToken/GetGitHubToken methods for access.
        /// </summary>
        public string? EncryptedGitHubToken { get; set; }
        
        /// <summary>
        /// Legacy plaintext token property - for JSON deserialization migration only.
        /// </summary>
        [Obsolete("Use SetGitHubToken/GetGitHubToken instead. Exists only for migration.")]
        public string? GitHubToken 
        { 
            get => null; // Never expose plaintext
            set 
            {
                // If setting from JSON deserialization (migration), encrypt it
                if (!string.IsNullOrEmpty(value) && string.IsNullOrEmpty(EncryptedGitHubToken))
                {
                    EncryptedGitHubToken = SecureCredentialManager.Encrypt(value);
                }
            }
        }
        
        /// <summary>
        /// Sets and encrypts the GitHub token using DPAPI.
        /// </summary>
        public void SetGitHubToken(string token)
        {
            EncryptedGitHubToken = SecureCredentialManager.Encrypt(token);
        }
        
        /// <summary>
        /// Gets the decrypted GitHub token.
        /// </summary>
        public string GetGitHubToken()
        {
            return SecureCredentialManager.Decrypt(EncryptedGitHubToken ?? string.Empty);
        }
        
        /// <summary>
        /// Gets whether a GitHub token is configured.
        /// </summary>
        public bool HasGitHubToken => !string.IsNullOrEmpty(EncryptedGitHubToken);

        /// <summary>
        /// Timestamp of last update check
        /// </summary>
        public DateTime? LastUpdateCheck { get; set; }

        /// <summary>
        /// Whether to automatically check for updates on app startup
        /// </summary>
        public bool AutoCheckForUpdates { get; set; } = true;

        /// <summary>
        /// Versions the user has chosen to skip
        /// </summary>
        public List<string> SkippedVersions { get; set; } = new List<string>();

        /// <summary>
        /// Local manifest path for comparison with remote manifest
        /// </summary>
        public string? LocalManifestPath { get; set; }
    }

    /// <summary>
    /// Represents the result of an update check.
    /// </summary>
    public class UpdateCheckResult
    {
        /// <summary>
        /// Whether an update is available
        /// </summary>
        public bool IsUpdateAvailable { get; set; }

        /// <summary>
        /// Current installed version
        /// </summary>
        public string CurrentVersion { get; set; } = string.Empty;

        /// <summary>
        /// Latest available version
        /// </summary>
        public string LatestVersion { get; set; } = string.Empty;

        /// <summary>
        /// Download URL for the full ZIP package
        /// </summary>
        public string? DownloadUrl { get; set; }

        /// <summary>
        /// URL to the manifest.json file
        /// </summary>
        public string? ManifestUrl { get; set; }

        /// <summary>
        /// Release notes URL or content
        /// </summary>
        public string? ReleaseNotes { get; set; }

        /// <summary>
        /// List of files that changed (for delta updates)
        /// </summary>
        public List<FileEntry> ChangedFiles { get; set; } = new List<FileEntry>();

        /// <summary>
        /// Total size of delta update in bytes
        /// </summary>
        public long DeltaSize { get; set; }

        /// <summary>
        /// Total size of full package in bytes (for bandwidth savings calculation)
        /// </summary>
        public long TotalSize { get; set; }

        /// <summary>
        /// Release date
        /// </summary>
        public DateTime? ReleaseDate { get; set; }

        /// <summary>
        /// Error message if update check failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
