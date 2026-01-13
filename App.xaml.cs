using System;
using System.IO;
using System.Linq;
using System.Windows;
using CloudJourneyAddin.Views;
using CloudJourneyAddin.Models;
using Microsoft.Win32;

namespace CloudJourneyAddin
{
    public partial class App : Application
    {
        public App()
        {
            // Catch any unhandled exceptions
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                $"Unhandled Exception:\n\n" +
                $"Message: {e.Exception.Message}\n\n" +
                $"Type: {e.Exception.GetType().Name}\n\n" +
                $"Stack Trace:\n{e.Exception.StackTrace}\n\n" +
                $"Inner: {e.Exception.InnerException?.Message ?? "None"}",
                "Critical Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
            Shutdown();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show(
                $"Fatal Unhandled Exception:\n\n" +
                $"Message: {ex?.Message ?? "Unknown"}\n\n" +
                $"Type: {ex?.GetType().Name ?? "Unknown"}\n\n" +
                $"Stack: {ex?.StackTrace ?? "None"}",
                "Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                // Check for updates on startup (once per day)
                _ = CheckForUpdatesAsync();

                // Temporarily disable ConfigMgr Console check to diagnose startup issues
                // (Console detection can be re-enabled after verifying the app launches)
                
                /*
                // Check if ConfigMgr Console is installed
                if (!IsConfigMgrConsoleInstalled())
                {
                    var result = MessageBox.Show(
                        "Configuration Manager Console is not detected on this machine.\n\n" +
                        "This tool is designed to run on systems with the ConfigMgr Console installed.\n\n" +
                        "Do you want to continue anyway?",
                        "ConfigMgr Console Not Found",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.No)
                    {
                        Shutdown();
                        return;
                    }
                }
                */

                // Parse command-line arguments for tab visibility
                var tabVisibilityOptions = TabVisibilityOptions.ParseArguments(e.Args);

                var dashboard = new DashboardWindow(tabVisibilityOptions);
                dashboard.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to start dashboard:\n\n" +
                    $"Error: {ex.Message}\n\n" +
                    $"Type: {ex.GetType().Name}\n\n" +
                    $"Stack Trace:\n{ex.StackTrace}\n\n" +
                    $"Inner Exception: {ex.InnerException?.Message ?? "None"}", 
                    "Cloud Journey Add-in Startup Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
                Shutdown();
            }
        }

        /// <summary>
        /// Checks for updates from GitHub Releases on every startup.
        /// Automatically downloads and applies updates without user confirmation.
        /// Runs asynchronously and doesn't block application startup.
        /// </summary>
        private async System.Threading.Tasks.Task CheckForUpdatesAsync()
        {
            try
            {
                var updateService = new Services.GitHubUpdateService();
                
                // Check if we should perform an update check
                if (!updateService.ShouldCheckForUpdates())
                {
                    return;
                }

                Services.FileLogger.Instance.Info("Checking for updates from GitHub Releases...");
                
                var updateResult = await updateService.CheckForUpdatesAsync();
                
                if (!updateResult.IsUpdateAvailable)
                {
                    Services.FileLogger.Instance.Info($"No updates available (current version: {updateResult.CurrentVersion})");
                    return;
                }

                if (string.IsNullOrEmpty(updateResult.ManifestUrl))
                {
                    Services.FileLogger.Instance.Warning("Update available but no manifest URL found");
                    return;
                }

                // Download manifest to calculate delta
                var deltaService = new Services.DeltaUpdateService();
                var remoteManifest = await deltaService.DownloadRemoteManifestAsync(updateResult.ManifestUrl);
                
                if (remoteManifest != null)
                {
                    var changedFiles = deltaService.GetChangedFiles(remoteManifest);
                    updateResult.ChangedFiles = changedFiles;
                    updateResult.DeltaSize = changedFiles.Sum(f => f.FileSize);
                    
                    Services.FileLogger.Instance.Info($"Update available: {updateResult.CurrentVersion} → {updateResult.LatestVersion}");
                    Services.FileLogger.Instance.Info($"Delta: {changedFiles.Count} files, {updateResult.DeltaSize:N0} bytes");
                    Services.FileLogger.Instance.Info("Automatic update starting...");
                    
                    // Show progress notification on UI thread
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        var progressWindow = new Views.UpdateProgressWindow(updateResult);
                        progressWindow.Show();
                        
                        // Download and apply update automatically
                        var progress = new Progress<int>(percent =>
                        {
                            progressWindow.UpdateProgress(percent, $"Downloading update... {percent}%");
                        });

                        var success = await deltaService.DownloadDeltaFilesAsync(
                            updateResult.DownloadUrl!,
                            changedFiles,
                            progress);

                        if (success)
                        {
                            progressWindow.UpdateProgress(100, "Applying update...");
                            
                            var applier = new Services.UpdateApplier();
                            success = await applier.ApplyUpdateAsync(
                                deltaService.GetTempDownloadPath(),
                                changedFiles,
                                remoteManifest);

                            if (success)
                            {
                                progressWindow.UpdateProgress(100, "Update complete! Restarting...");
                                await System.Threading.Tasks.Task.Delay(2000);
                                // App will restart via PowerShell script
                            }
                            else
                            {
                                progressWindow.UpdateProgress(0, "Update failed. Please try again.");
                                await System.Threading.Tasks.Task.Delay(3000);
                                progressWindow.Close();
                            }
                        }
                        else
                        {
                            progressWindow.UpdateProgress(0, "Download failed. Please check your connection.");
                            await System.Threading.Tasks.Task.Delay(3000);
                            progressWindow.Close();
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                // Don't crash the app if update check fails
                Services.FileLogger.Instance.Warning($"Update check failed: {ex.Message}");
            }
        }

        private bool IsConfigMgrConsoleInstalled()
        {
            try
            {
                // Check for ConfigMgr Console installation in common locations
                string[] possiblePaths = new[]
                {
                    @"C:\Program Files (x86)\Microsoft Configuration Manager\AdminConsole",
                    @"C:\Program Files\Microsoft Configuration Manager\AdminConsole",
                    @"D:\Program Files (x86)\Microsoft Configuration Manager\AdminConsole",
                    @"D:\Program Files\Microsoft Configuration Manager\AdminConsole",
                    @"E:\Program Files (x86)\Microsoft Configuration Manager\AdminConsole",
                    @"E:\Program Files\Microsoft Configuration Manager\AdminConsole",
                    @"F:\Program Files (x86)\Microsoft Configuration Manager\AdminConsole",
                    @"F:\Program Files\Microsoft Configuration Manager\AdminConsole"
                };

                foreach (var path in possiblePaths)
                {
                    if (Directory.Exists(path))
                    {
                        // Check for the console executable
                        string consoleExe = Path.Combine(path, "bin", "Microsoft.ConfigurationManagement.exe");
                        if (File.Exists(consoleExe))
                        {
                            return true;
                        }
                    }
                }

                // Check registry for installation path
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\ConfigMgr10\Setup"))
                {
                    if (key != null)
                    {
                        var uiInstallPath = key.GetValue("UI Installation Directory") as string;
                        if (!string.IsNullOrEmpty(uiInstallPath) && Directory.Exists(uiInstallPath))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            catch
            {
                // If we can't determine, allow the app to start
                return true;
            }
        }
    }
}
