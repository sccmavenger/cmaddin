using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using ZeroTrustMigrationAddin.Models;
using Newtonsoft.Json;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Services
{
    /// <summary>
    /// Service for managing delta updates by comparing manifests and downloading only changed files.
    /// </summary>
    public class DeltaUpdateService
    {
        private readonly string _installPath;
        private readonly string _localManifestPath;
        private readonly string _tempDownloadPath;
        private readonly HttpClient _httpClient;

        public DeltaUpdateService()
        {
            // Determine installation path (where ZeroTrustMigrationAddin.exe is located)
            _installPath = AppDomain.CurrentDomain.BaseDirectory;
            
            // Local manifest storage
            _localManifestPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ZeroTrustMigrationAddin",
                "manifest.json");

            // Temp folder for downloads
            _tempDownloadPath = Path.Combine(
                Path.GetTempPath(),
                "CloudJourneyAddin-Update",
                Guid.NewGuid().ToString());

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10)
            };

            // Add GitHub authentication if token exists (for private repos)
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ZeroTrustMigrationAddin",
                "update-settings.json");

            if (File.Exists(settingsPath))
            {
                try
                {
                    var json = File.ReadAllText(settingsPath);
                    var settings = JsonConvert.DeserializeObject<dynamic>(json);
                    string? token = settings?.GitHubToken;

                    if (!string.IsNullOrEmpty(token))
                    {
                        // GitHub API asset downloads require token in header + specific Accept header
                        _httpClient.DefaultRequestHeaders.Add("Authorization", $"token {token}");
                        _httpClient.DefaultRequestHeaders.Add("Accept", "application/octet-stream");
                        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ZeroTrustMigrationAddin");
                        Instance.Info("🔑 GitHub authentication configured for asset downloads");
                    }
                }
                catch (Exception ex)
                {
                    Instance.Warning($"Could not load GitHub token for downloads: {ex.Message}");
                }
            }

            Instance.Info($"DeltaUpdateService initialized:");
            Instance.Info($"  Install path: {_installPath}");
            Instance.Info($"  Local manifest: {_localManifestPath}");
            Instance.Info($"  Temp downloads: {_tempDownloadPath}");
        }

        /// <summary>
        /// Loads the local manifest from storage.
        /// Returns null if this is the first install or manifest doesn't exist.
        /// Validates manifest entries and clears if corrupted.
        /// </summary>
        public UpdateManifest? LoadLocalManifest()
        {
            try
            {
                if (!File.Exists(_localManifestPath))
                {
                    Instance.Info("No local manifest found - this may be a first install");
                    return null;
                }

                var json = File.ReadAllText(_localManifestPath);
                var manifest = JsonConvert.DeserializeObject<UpdateManifest>(json);
                
                if (manifest != null)
                {
                    // Validate manifest entries - check for corrupted/empty RelativePath
                    var invalidEntries = manifest.Files.Count(f => string.IsNullOrWhiteSpace(f.RelativePath));
                    if (invalidEntries > 0)
                    {
                        Instance.Warning($"Local manifest has {invalidEntries} invalid entries with empty RelativePath");
                        Instance.Warning("Clearing corrupted local manifest - will perform full comparison on next update");
                        
                        // Delete the corrupted manifest
                        File.Delete(_localManifestPath);
                        return null;
                    }
                    
                    Instance.Info($"Local manifest loaded: v{manifest.Version}, {manifest.Files.Count} files (validated)");
                    return manifest;
                }

                Instance.Warning("Failed to deserialize local manifest");
                return null;
            }
            catch (Exception ex)
            {
                Instance.Warning($"Could not load local manifest: {ex.Message}");
                // If manifest is corrupted/unreadable, delete it
                try
                {
                    if (File.Exists(_localManifestPath))
                    {
                        File.Delete(_localManifestPath);
                        Instance.Info("Deleted corrupted local manifest");
                    }
                }
                catch { /* Ignore deletion errors */ }
                return null;
            }
        }

        /// <summary>
        /// Saves the manifest to local storage.
        /// Used after successful updates or to establish baseline for future updates.
        /// </summary>
        public void SaveManifest(UpdateManifest manifest)
        {
            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(_localManifestPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
                File.WriteAllText(_localManifestPath, json);
                
                Instance.Info($"Manifest saved: v{manifest.Version}, {manifest.Files.Count} files");
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to save manifest: {ex.Message}");
            }
        }

        /// <summary>
        /// Downloads and parses the remote manifest from GitHub Release assets.
        /// </summary>
        public async Task<UpdateManifest?> DownloadRemoteManifestAsync(string manifestUrl)
        {
            try
            {
                Instance.Info($"Downloading remote manifest from: {manifestUrl}");
                
                var json = await _httpClient.GetStringAsync(manifestUrl);
                var manifest = JsonConvert.DeserializeObject<UpdateManifest>(json);
                
                if (manifest != null)
                {
                    Instance.Info($"Remote manifest downloaded: v{manifest.Version}, {manifest.Files.Count} files, {manifest.TotalSize:N0} bytes");
                }
                
                return manifest;
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to download remote manifest: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Compares local and remote manifests to identify changed files.
        /// Returns list of files that need to be downloaded.
        /// Only includes files with valid RelativePath values.
        /// Uses full disk verification for users upgrading from old versions.
        /// </summary>
        public List<FileEntry> GetChangedFiles(UpdateManifest remoteManifest, IProgress<UpdateProgress>? progress = null)
        {
            var localManifest = LoadLocalManifest();
            
            // Check if full verification is required (old version or no manifest)
            if (RequiresFullVerification(localManifest))
            {
                Instance.Info("🔍 Using FULL FILE VERIFICATION mode (upgrading from old version)");
                return VerifyAllFilesOnDisk(remoteManifest, progress);
            }

            // Fast path: manifest-to-manifest comparison
            Instance.Info("⚡ Using FAST COMPARISON mode (manifest-to-manifest)");
            var changedFiles = new List<FileEntry>();

            // Filter out any invalid entries from remote manifest
            var validRemoteFiles = remoteManifest.Files
                .Where(f => !string.IsNullOrWhiteSpace(f.RelativePath))
                .ToList();
            
            if (validRemoteFiles.Count != remoteManifest.Files.Count)
            {
                Instance.Warning($"Remote manifest has {remoteManifest.Files.Count - validRemoteFiles.Count} invalid entries (filtered out)");
            }

            if (localManifest == null)
            {
                // First install or no manifest - all valid files are "new"
                Instance.Info("No local manifest - treating all valid files as changed");
                return validRemoteFiles;
            }

            Instance.Info($"Comparing manifests: Local v{localManifest.Version} vs Remote v{remoteManifest.Version}");

            foreach (var remoteFile in validRemoteFiles)
            {
                var localFile = localManifest.Files
                    .FirstOrDefault(f => !string.IsNullOrEmpty(f.RelativePath) && 
                                         f.RelativePath.Equals(remoteFile.RelativePath, StringComparison.OrdinalIgnoreCase));

                if (localFile == null)
                {
                    // New file that doesn't exist locally
                    changedFiles.Add(remoteFile);
                    Instance.Info($"  + NEW: {remoteFile.RelativePath} ({remoteFile.FileSize:N0} bytes)");
                    continue;
                }

                // Compare SHA256 hash (most reliable method)
                if (!localFile.SHA256Hash.Equals(remoteFile.SHA256Hash, StringComparison.OrdinalIgnoreCase))
                {
                    changedFiles.Add(remoteFile);
                    Instance.Info($"  ≠ CHANGED: {remoteFile.RelativePath} (hash mismatch)");
                    continue;
                }

                // Quick size check as backup verification
                if (localFile.FileSize != remoteFile.FileSize)
                {
                    changedFiles.Add(remoteFile);
                    Instance.Info($"  ≠ CHANGED: {remoteFile.RelativePath} (size: {localFile.FileSize:N0} → {remoteFile.FileSize:N0})");
                }
            }

            var totalSize = changedFiles.Sum(f => f.FileSize);
            Instance.Info($"Delta update: {changedFiles.Count} files changed, {totalSize:N0} bytes total");

            return changedFiles;
        }

        /// <summary>
        /// Downloads a specific file from the ZIP package to temp folder.
        /// Note: This requires the full ZIP download. For true delta updates,
        /// individual files would need to be hosted separately.
        /// </summary>
        public async Task<bool> DownloadFileAsync(string fileUrl, string destinationPath, string expectedHash)
        {
            try
            {
                Instance.Info($"Downloading: {Path.GetFileName(destinationPath)}");

                // Ensure destination directory exists
                var directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Download file
                using var response = await _httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                using var fileStream = File.Create(destinationPath);
                await response.Content.CopyToAsync(fileStream);
                fileStream.Close();

                // Verify SHA256 hash
                var actualHash = CalculateFileHash(destinationPath);
                if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    Instance.Error($"Hash mismatch for {Path.GetFileName(destinationPath)}");
                    Instance.Error($"  Expected: {expectedHash}");
                    Instance.Error($"  Actual:   {actualHash}");
                    File.Delete(destinationPath);
                    return false;
                }

                Instance.Info($"  ✅ Downloaded and verified: {Path.GetFileName(destinationPath)}");
                return true;
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to download {Path.GetFileName(destinationPath)}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Downloads the full ZIP package and extracts only the changed files to temp folder.
        /// Reports detailed progress including phase, file count, and bytes.
        /// </summary>
        public async Task<bool> DownloadDeltaFilesAsync(string zipUrl, List<FileEntry> changedFiles, IProgress<UpdateProgress>? detailedProgress)
        {
            try
            {
                // Create temp download directory
                if (!Directory.Exists(_tempDownloadPath))
                {
                    Directory.CreateDirectory(_tempDownloadPath);
                }

                Instance.Info($"Downloading ZIP package: {zipUrl}");
                var zipPath = Path.Combine(_tempDownloadPath, "update.zip");

                // Download ZIP with detailed progress
                using (var response = await _httpClient.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    
                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = File.Create(zipPath);
                    
                    var buffer = new byte[81920]; // 80 KB buffer
                    long totalRead = 0;
                    int bytesRead;
                    
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;
                        
                        if (totalBytes > 0)
                        {
                            var percentComplete = (int)((totalRead * 100) / totalBytes);
                            detailedProgress?.Report(new UpdateProgress
                            {
                                Phase = UpdatePhase.Downloading,
                                PercentComplete = percentComplete,
                                BytesDownloaded = totalRead,
                                TotalBytes = totalBytes,
                                StatusMessage = $"Downloading update... {percentComplete}%"
                            });
                        }
                    }
                }

                Instance.Info($"ZIP downloaded: {new FileInfo(zipPath).Length:N0} bytes");

                // Extract files
                detailedProgress?.Report(new UpdateProgress
                {
                    Phase = UpdatePhase.Extracting,
                    PercentComplete = 0,
                    TotalFiles = changedFiles.Count,
                    StatusMessage = "Extracting files..."
                });
                
                Instance.Info($"Extracting {changedFiles.Count} changed files from ZIP...");
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, _tempDownloadPath, overwriteFiles: true);

                // Verify extracted files
                detailedProgress?.Report(new UpdateProgress
                {
                    Phase = UpdatePhase.Validating,
                    PercentComplete = 0,
                    TotalFiles = changedFiles.Count,
                    StatusMessage = "Validating downloaded files..."
                });
                
                int verifiedCount = 0;
                int missingCount = 0;
                int invalidCount = 0;
                int processed = 0;
                
                foreach (var file in changedFiles)
                {
                    processed++;
                    
                    // Skip invalid entries
                    if (string.IsNullOrWhiteSpace(file.RelativePath))
                    {
                        invalidCount++;
                        Instance.Warning($"Skipping invalid entry with empty RelativePath (hash: {file.SHA256Hash?.Substring(0, 8) ?? "N/A"}...)");
                        continue;
                    }
                    
                    detailedProgress?.Report(new UpdateProgress
                    {
                        Phase = UpdatePhase.Validating,
                        PercentComplete = (processed * 100) / changedFiles.Count,
                        CurrentFile = file.RelativePath,
                        CurrentFileIndex = processed,
                        TotalFiles = changedFiles.Count,
                        StatusMessage = $"Validating {file.RelativePath}..."
                    });
                    
                    var extractedPath = Path.Combine(_tempDownloadPath, file.RelativePath);
                    
                    if (!File.Exists(extractedPath))
                    {
                        missingCount++;
                        Instance.Warning($"File not found in ZIP: '{file.RelativePath}' (size: {file.FileSize:N0} bytes, critical: {file.IsCritical})");
                        
                        // If too many files are missing, abort the update
                        if (missingCount > changedFiles.Count / 2)
                        {
                            Instance.Error($"Too many files missing ({missingCount}/{changedFiles.Count}) - aborting update to prevent corruption");
                            detailedProgress?.Report(new UpdateProgress
                            {
                                Phase = UpdatePhase.Failed,
                                StatusMessage = "Update failed: too many files missing from package",
                                ErrorMessage = $"Missing {missingCount} of {changedFiles.Count} files"
                            });
                            return false;
                        }
                        continue;
                    }

                    var actualHash = CalculateFileHash(extractedPath);
                    if (actualHash.Equals(file.SHA256Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        verifiedCount++;
                    }
                    else
                    {
                        Instance.Warning($"Hash mismatch: {file.RelativePath}");
                    }
                }

                if (invalidCount > 0)
                {
                    Instance.Warning($"Skipped {invalidCount} invalid manifest entries");
                }
                
                Instance.Info($"Verified {verifiedCount}/{changedFiles.Count} files (missing: {missingCount}, invalid: {invalidCount})");
                
                // Success if we verified most files (allow some missing for non-critical files like localization)
                var validFiles = changedFiles.Count - invalidCount;
                return validFiles > 0 && verifiedCount >= validFiles - missingCount;
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to download delta files: {ex.Message}");
                detailedProgress?.Report(new UpdateProgress
                {
                    Phase = UpdatePhase.Failed,
                    StatusMessage = "Download failed",
                    ErrorMessage = ex.Message
                });
                return false;
            }
        }

        /// <summary>
        /// Downloads the full ZIP package and extracts only the changed files to temp folder.
        /// </summary>
        public async Task<bool> DownloadDeltaFilesAsync(string zipUrl, List<FileEntry> changedFiles, IProgress<int>? progress = null)
        {
            // Convert simple progress to detailed progress
            IProgress<UpdateProgress>? detailedProgress = null;
            if (progress != null)
            {
                detailedProgress = new Progress<UpdateProgress>(p => progress.Report(p.PercentComplete));
            }
            return await DownloadDeltaFilesAsync(zipUrl, changedFiles, detailedProgress);
        }

        /// <summary>
        /// Calculates SHA256 hash of a file.
        /// </summary>
        public string CalculateFileHash(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Saves the remote manifest as the new local manifest after successful update.
        /// </summary>
        public void SaveLocalManifest(UpdateManifest manifest)
        {
            try
            {
                var directory = Path.GetDirectoryName(_localManifestPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
                File.WriteAllText(_localManifestPath, json);
                
                Instance.Info($"Local manifest updated to version {manifest.Version}");
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to save local manifest: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the path where delta files are downloaded.
        /// </summary>
        public string GetTempDownloadPath() => _tempDownloadPath;

        /// <summary>
        /// Version threshold for full file verification.
        /// Users upgrading FROM versions before this will get full disk verification.
        /// This catches old MSI installs or partial update failures.
        /// </summary>
        private const string FullVerificationThreshold = "3.17.207";

        /// <summary>
        /// Verifies all files on disk against the remote manifest by hashing each file.
        /// Returns list of files that are missing or have mismatched hashes.
        /// Used for users upgrading from old versions to ensure all files are correct.
        /// </summary>
        public List<FileEntry> VerifyAllFilesOnDisk(UpdateManifest remoteManifest, IProgress<UpdateProgress>? progress = null)
        {
            var mismatchedFiles = new List<FileEntry>();
            var validRemoteFiles = remoteManifest.Files
                .Where(f => !string.IsNullOrWhiteSpace(f.RelativePath))
                .ToList();

            Instance.Info($"🔍 Full verification mode: checking {validRemoteFiles.Count} files on disk");

            int processed = 0;
            foreach (var remoteFile in validRemoteFiles)
            {
                processed++;
                var localPath = Path.Combine(_installPath, remoteFile.RelativePath);

                // Report progress
                progress?.Report(new UpdateProgress
                {
                    Phase = UpdatePhase.Verifying,
                    PercentComplete = (processed * 100) / validRemoteFiles.Count,
                    CurrentFile = remoteFile.RelativePath,
                    CurrentFileIndex = processed,
                    TotalFiles = validRemoteFiles.Count,
                    StatusMessage = $"Verifying {remoteFile.RelativePath}..."
                });

                // Check if file exists
                if (!File.Exists(localPath))
                {
                    Instance.Info($"  ❌ MISSING: {remoteFile.RelativePath}");
                    mismatchedFiles.Add(remoteFile);
                    continue;
                }

                // Calculate hash and compare
                try
                {
                    var localHash = CalculateFileHash(localPath);
                    if (!localHash.Equals(remoteFile.SHA256Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        Instance.Info($"  ≠ MISMATCH: {remoteFile.RelativePath} (local: {localHash.Substring(0, 8)}... vs remote: {remoteFile.SHA256Hash.Substring(0, 8)}...)");
                        mismatchedFiles.Add(remoteFile);
                    }
                }
                catch (Exception ex)
                {
                    Instance.Warning($"  ⚠️ Cannot verify {remoteFile.RelativePath}: {ex.Message}");
                    // If we can't read the file, it needs to be replaced
                    mismatchedFiles.Add(remoteFile);
                }
            }

            var totalSize = mismatchedFiles.Sum(f => f.FileSize);
            Instance.Info($"✅ Full verification complete: {mismatchedFiles.Count} files need updating ({totalSize:N0} bytes)");

            return mismatchedFiles;
        }

        /// <summary>
        /// Determines if full file verification is required based on local version.
        /// </summary>
        public bool RequiresFullVerification(UpdateManifest? localManifest)
        {
            if (localManifest == null)
            {
                Instance.Info("📋 No local manifest - will use full verification for first update");
                return true;
            }

            // Compare versions
            if (Version.TryParse(localManifest.Version, out var localVersion) &&
                Version.TryParse(FullVerificationThreshold, out var thresholdVersion))
            {
                if (localVersion < thresholdVersion)
                {
                    Instance.Info($"📋 Local version {localManifest.Version} < threshold {FullVerificationThreshold} - using full verification");
                    return true;
                }
            }

            Instance.Info($"📋 Local version {localManifest.Version} >= threshold {FullVerificationThreshold} - using fast comparison");
            return false;
        }

        /// <summary>
        /// Cleans up temporary download files.
        /// </summary>
        public void CleanupTempFiles()
        {
            try
            {
                if (Directory.Exists(_tempDownloadPath))
                {
                    Directory.Delete(_tempDownloadPath, recursive: true);
                    Instance.Info("Temporary download files cleaned up");
                }
            }
            catch (Exception ex)
            {
                Instance.Warning($"Failed to cleanup temp files: {ex.Message}");
            }
        }

        /// <summary>
        /// Generates a manifest for the current installation by scanning all files.
        /// Used for first-time manifest creation after manual ZIP install.
        /// </summary>
        public UpdateManifest GenerateManifestFromInstallation(string version)
        {
            Instance.Info($"Generating manifest from current installation...");
            
            var manifest = new UpdateManifest
            {
                Version = version,
                BuildDate = DateTime.UtcNow,
                Files = new List<FileEntry>()
            };

            try
            {
                var files = Directory.GetFiles(_installPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => !f.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                    .Where(f => !f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || !f.Contains("config"));

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    var relativePath = Path.GetFileName(file);
                    
                    var entry = new FileEntry
                    {
                        RelativePath = relativePath,
                        SHA256Hash = CalculateFileHash(file),
                        FileSize = fileInfo.Length,
                        LastModified = fileInfo.LastWriteTimeUtc,
                        IsCritical = IsCriticalFile(relativePath)
                    };

                    manifest.Files.Add(entry);
                    manifest.TotalSize += entry.FileSize;
                }

                Instance.Info($"Generated manifest with {manifest.Files.Count} files, {manifest.TotalSize:N0} bytes");
                SaveLocalManifest(manifest);
                
                return manifest;
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to generate manifest: {ex.Message}");
                return manifest;
            }
        }

        /// <summary>
        /// Determines if a file is critical (exe or core DLL).
        /// </summary>
        private bool IsCriticalFile(string fileName)
        {
            var criticalFiles = new[]
            {
                "ZeroTrustMigrationAddin.exe",
                "ZeroTrustMigrationAddin.dll",
                "Azure.Identity.dll",
                "Microsoft.Graph.dll",
                "Microsoft.Graph.Core.dll",
                "Newtonsoft.Json.dll"
            };

            return criticalFiles.Any(f => f.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
