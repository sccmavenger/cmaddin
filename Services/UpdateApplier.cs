using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CloudJourneyAddin.Models;
using static CloudJourneyAddin.Services.FileLogger;

namespace CloudJourneyAddin.Services
{
    /// <summary>
    /// Service for applying updates by replacing changed files and restarting the application.
    /// Handles file locking, process termination, and safe file replacement.
    /// </summary>
    public class UpdateApplier
    {
        private readonly string _installPath;
        private readonly string _executablePath;

        public UpdateApplier()
        {
            _installPath = AppDomain.CurrentDomain.BaseDirectory;
            _executablePath = Process.GetCurrentProcess().MainModule?.FileName ?? 
                            Path.Combine(_installPath, "CloudJourneyAddin.exe");

            Instance.Info($"UpdateApplier initialized:");
            Instance.Info($"  Install path: {_installPath}");
            Instance.Info($"  Executable: {_executablePath}");
        }

        /// <summary>
        /// Applies the update by replacing files and restarting the application.
        /// Returns true if successful, false otherwise.
        /// </summary>
        public async Task<bool> ApplyUpdateAsync(string tempDownloadPath, List<FileEntry> changedFiles, UpdateManifest newManifest)
        {
            try
            {
                Instance.Info($"=== Starting Update Application Process ===");
                Instance.Info($"Files to update: {changedFiles.Count}");
                Instance.Info($"Source: {tempDownloadPath}");
                Instance.Info($"Destination: {_installPath}");

                // Step 1: Validate all source files exist
                if (!ValidateSourceFiles(tempDownloadPath, changedFiles))
                {
                    Instance.Error("Source file validation failed");
                    return false;
                }

                // Step 2: Create backup of critical files
                var backupPath = CreateBackup(changedFiles);
                if (backupPath == null)
                {
                    Instance.Warning("Backup creation failed, but continuing with update");
                }

                // Step 3: Schedule update to run after app closes
                if (!ScheduleUpdateScript(tempDownloadPath, changedFiles, newManifest))
                {
                    Instance.Error("Failed to schedule update script");
                    return false;
                }

                Instance.Info("✅ Update scheduled successfully");
                Instance.Info("Application will restart to apply updates");
                
                return true;
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to apply update: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validates that all source files exist before attempting update.
        /// </summary>
        private bool ValidateSourceFiles(string sourcePath, List<FileEntry> files)
        {
            Instance.Info("Validating source files...");
            var missingFiles = new List<string>();

            foreach (var file in files)
            {
                var sourceFile = Path.Combine(sourcePath, file.RelativePath);
                if (!File.Exists(sourceFile))
                {
                    missingFiles.Add(file.RelativePath);
                    Instance.Warning($"  ❌ Missing: {file.RelativePath}");
                }
            }

            if (missingFiles.Any())
            {
                Instance.Error($"Validation failed: {missingFiles.Count} files missing");
                return false;
            }

            Instance.Info($"✅ All {files.Count} source files validated");
            return true;
        }

        /// <summary>
        /// Creates a backup of files that will be replaced.
        /// </summary>
        private string? CreateBackup(List<FileEntry> files)
        {
            try
            {
                var backupPath = Path.Combine(
                    Path.GetTempPath(),
                    "CloudJourneyAddin-Backup",
                    DateTime.Now.ToString("yyyyMMdd-HHmmss"));

                Directory.CreateDirectory(backupPath);
                Instance.Info($"Creating backup: {backupPath}");

                int backedUp = 0;
                foreach (var file in files)
                {
                    var sourceFile = Path.Combine(_installPath, file.RelativePath);
                    if (File.Exists(sourceFile))
                    {
                        var backupFile = Path.Combine(backupPath, file.RelativePath);
                        var backupDir = Path.GetDirectoryName(backupFile);
                        
                        if (!string.IsNullOrEmpty(backupDir))
                        {
                            Directory.CreateDirectory(backupDir);
                        }

                        File.Copy(sourceFile, backupFile, overwrite: true);
                        backedUp++;
                    }
                }

                Instance.Info($"✅ Backed up {backedUp} files to {backupPath}");
                return backupPath;
            }
            catch (Exception ex)
            {
                Instance.Warning($"Backup creation failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates and executes a PowerShell script to apply the update after the app closes.
        /// </summary>
        private bool ScheduleUpdateScript(string sourcePath, List<FileEntry> changedFiles, UpdateManifest newManifest)
        {
            try
            {
                var scriptPath = Path.Combine(Path.GetTempPath(), "CloudJourneyAddin-ApplyUpdate.ps1");
                Instance.Info($"Creating update script: {scriptPath}");

                var script = GenerateUpdateScript(sourcePath, changedFiles, newManifest);
                File.WriteAllText(scriptPath, script);

                // Start the script in a new process
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(psi);
                Instance.Info("✅ Update script started");

                return true;
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to schedule update script: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Generates the PowerShell script that will perform the actual file updates.
        /// </summary>
        private string GenerateUpdateScript(string sourcePath, List<FileEntry> changedFiles, UpdateManifest newManifest)
        {
            var manifestPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CloudJourneyAddin",
                "manifest.json");

            var manifestJson = Newtonsoft.Json.JsonConvert.SerializeObject(newManifest, Newtonsoft.Json.Formatting.Indented)
                .Replace("\"", "`\""); // Escape quotes for PowerShell

            var script = $@"
# CloudJourneyAddin Auto-Update Script
# Generated: {DateTime.Now}
# Version: {newManifest.Version}

$ErrorActionPreference = 'Stop'
$logFile = Join-Path $env:TEMP 'CloudJourneyAddin-Update.log'

function Write-Log {{
    param([string]$Message)
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    ""[$timestamp] $Message"" | Out-File -FilePath $logFile -Append
    Write-Host $Message
}}

Write-Log ""=== CloudJourneyAddin Update Script ===""
Write-Log ""Source: {sourcePath}""
Write-Log ""Destination: {_installPath}""
Write-Log ""Files to update: {changedFiles.Count}""

# Wait for main application to close (max 30 seconds)
Write-Log ""Waiting for application to close...""
$maxWaitSeconds = 30
$waitedSeconds = 0

while ($waitedSeconds -lt $maxWaitSeconds) {{
    $processes = Get-Process -Name 'CloudJourneyAddin' -ErrorAction SilentlyContinue
    if (-not $processes) {{
        Write-Log ""Application closed""
        break
    }}
    
    Start-Sleep -Seconds 1
    $waitedSeconds++
    
    if ($waitedSeconds -eq 10) {{
        Write-Log ""Application still running after 10s, attempting to close...""
        $processes | ForEach-Object {{ $_.CloseMainWindow() | Out-Null }}
    }}
    
    if ($waitedSeconds -eq 20) {{
        Write-Log ""Application still running after 20s, force closing...""
        $processes | Stop-Process -Force
        Start-Sleep -Seconds 2
    }}
}}

if ($waitedSeconds -ge $maxWaitSeconds) {{
    Write-Log ""ERROR: Application did not close in time""
    exit 1
}}

# Additional safety wait
Start-Sleep -Seconds 2

# Copy updated files
Write-Log ""Copying updated files...""
$successCount = 0
$failCount = 0

";

            foreach (var file in changedFiles)
            {
                var sourceFile = Path.Combine(sourcePath, file.RelativePath).Replace("\\", "\\\\");
                var destFile = Path.Combine(_installPath, file.RelativePath).Replace("\\", "\\\\");
                
                script += $@"
try {{
    Copy-Item -Path '{sourceFile}' -Destination '{destFile}' -Force
    Write-Log ""  ✅ Updated: {file.RelativePath}""
    $successCount++
}} catch {{
    Write-Log ""  ❌ Failed: {file.RelativePath} - $($_.Exception.Message)""
    $failCount++
}}
";
            }

            script += $@"

Write-Log ""Update complete: $successCount succeeded, $failCount failed""

# Update local manifest
try {{
    $manifestDir = Split-Path '{manifestPath}' -Parent
    if (-not (Test-Path $manifestDir)) {{
        New-Item -ItemType Directory -Path $manifestDir -Force | Out-Null
    }}
    
    $manifestJson = @""
{manifestJson}
""@
    
    $manifestJson | Out-File -FilePath '{manifestPath}' -Encoding UTF8
    Write-Log ""✅ Manifest updated to version {newManifest.Version}""
}} catch {{
    Write-Log ""⚠️ Failed to update manifest: $($_.Exception.Message)""
}}

# Cleanup temp files
try {{
    if (Test-Path '{sourcePath}') {{
        Remove-Item -Path '{sourcePath}' -Recurse -Force
        Write-Log ""✅ Cleaned up temporary files""
    }}
}} catch {{
    Write-Log ""⚠️ Failed to cleanup temp files: $($_.Exception.Message)""
}}

# Restart application
Write-Log ""Restarting application...""
Start-Sleep -Seconds 1

try {{
    Start-Process -FilePath '{_executablePath}'
    Write-Log ""✅ Application restarted successfully""
}} catch {{
    Write-Log ""❌ Failed to restart application: $($_.Exception.Message)""
}}

Write-Log ""=== Update script completed ===""
Write-Log ""Log file: $logFile""

# Keep script running for 2 seconds to ensure app starts
Start-Sleep -Seconds 2

# Self-delete the script
Remove-Item -Path $PSCommandPath -Force
";

            return script;
        }

        /// <summary>
        /// Restarts the application immediately (for manual restart after download).
        /// </summary>
        public void RestartApplication()
        {
            try
            {
                Instance.Info("Restarting application...");
                Process.Start(_executablePath);
                
                // Close current instance
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to restart application: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if there are any running instances of CloudJourneyAddin.
        /// </summary>
        public bool IsApplicationRunning()
        {
            var currentProcess = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName("CloudJourneyAddin")
                .Where(p => p.Id != currentProcess.Id)
                .ToList();

            return processes.Any();
        }

        /// <summary>
        /// Attempts to gracefully close all other instances of the application.
        /// </summary>
        public async Task<bool> CloseOtherInstancesAsync()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var processes = Process.GetProcessesByName("CloudJourneyAddin")
                    .Where(p => p.Id != currentProcess.Id)
                    .ToList();

                if (!processes.Any())
                {
                    return true;
                }

                Instance.Info($"Found {processes.Count} other running instance(s)");

                // Try graceful close first
                foreach (var process in processes)
                {
                    try
                    {
                        process.CloseMainWindow();
                        Instance.Info($"Sent close request to process {process.Id}");
                    }
                    catch (Exception ex)
                    {
                        Instance.Warning($"Failed to close process {process.Id}: {ex.Message}");
                    }
                }

                // Wait up to 10 seconds for processes to close
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(1000);
                    
                    var stillRunning = Process.GetProcessesByName("CloudJourneyAddin")
                        .Where(p => p.Id != currentProcess.Id)
                        .ToList();

                    if (!stillRunning.Any())
                    {
                        Instance.Info("All other instances closed successfully");
                        return true;
                    }
                }

                Instance.Warning("Some instances did not close gracefully");
                return false;
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to close other instances: {ex.Message}");
                return false;
            }
        }
    }
}
