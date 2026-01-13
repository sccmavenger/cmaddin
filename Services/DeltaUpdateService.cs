using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using CloudJourneyAddin.Models;
using Newtonsoft.Json;
using static CloudJourneyAddin.Services.FileLogger;

namespace CloudJourneyAddin.Services
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
            // Determine installation path (where CloudJourneyAddin.exe is located)
            _installPath = AppDomain.CurrentDomain.BaseDirectory;
            
            // Local manifest storage
            _localManifestPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CloudJourneyAddin",
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

            Instance.Info($"DeltaUpdateService initialized:");
            Instance.Info($"  Install path: {_installPath}");
            Instance.Info($"  Local manifest: {_localManifestPath}");
            Instance.Info($"  Temp downloads: {_tempDownloadPath}");
        }

        /// <summary>
        /// Loads the local manifest from storage.
        /// Returns null if this is the first install or manifest doesn't exist.
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
                    Instance.Info($"Local manifest loaded: v{manifest.Version}, {manifest.Files.Count} files");
                }
                
                return manifest;
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to load local manifest: {ex.Message}");
                return null;
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
        /// </summary>
        public List<FileEntry> GetChangedFiles(UpdateManifest remoteManifest)
        {
            var localManifest = LoadLocalManifest();
            var changedFiles = new List<FileEntry>();

            if (localManifest == null)
            {
                // First install or no manifest - all files are "new"
                Instance.Info("No local manifest - treating all files as changed");
                return remoteManifest.Files.ToList();
            }

            Instance.Info($"Comparing manifests: Local v{localManifest.Version} vs Remote v{remoteManifest.Version}");

            foreach (var remoteFile in remoteManifest.Files)
            {
                var localFile = localManifest.Files
                    .FirstOrDefault(f => f.RelativePath.Equals(remoteFile.RelativePath, StringComparison.OrdinalIgnoreCase));

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
        /// </summary>
        public async Task<bool> DownloadDeltaFilesAsync(string zipUrl, List<FileEntry> changedFiles, IProgress<int>? progress = null)
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

                // Download ZIP with progress
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
                            progress?.Report(percentComplete);
                        }
                    }
                }

                Instance.Info($"ZIP downloaded: {new FileInfo(zipPath).Length:N0} bytes");

                // Extract only changed files
                Instance.Info($"Extracting {changedFiles.Count} changed files from ZIP...");
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, _tempDownloadPath, overwriteFiles: true);

                // Verify extracted files
                int verifiedCount = 0;
                foreach (var file in changedFiles)
                {
                    var extractedPath = Path.Combine(_tempDownloadPath, file.RelativePath);
                    
                    if (!File.Exists(extractedPath))
                    {
                        Instance.Warning($"File not found in ZIP: {file.RelativePath}");
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

                Instance.Info($"Verified {verifiedCount}/{changedFiles.Count} files");
                return verifiedCount == changedFiles.Count;
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to download delta files: {ex.Message}");
                return false;
            }
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
                "CloudJourneyAddin.exe",
                "CloudJourneyAddin.dll",
                "Azure.Identity.dll",
                "Microsoft.Graph.dll",
                "Microsoft.Graph.Core.dll",
                "Newtonsoft.Json.dll"
            };

            return criticalFiles.Any(f => f.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
