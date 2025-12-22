using System;
using System.IO;
using System.Windows;
using CloudJourneyAddin.Views;
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

                var dashboard = new DashboardWindow();
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
