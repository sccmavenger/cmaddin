using WixToolset.Dtf.WindowsInstaller;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace ZeroTrustMigrationAddin.Installer
{
    public class CustomActions
    {
        /// <summary>
        /// Custom Action: Check if .NET 8.0 Desktop Runtime is installed
        /// </summary>
        [CustomAction]
        public static ActionResult CheckDotNetRuntime(Session session)
        {
            session.Log("Begin CheckDotNetRuntime");

            try
            {
                // Check for .NET 8.0 runtime using dotnet --list-runtimes
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "--list-runtimes",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        string output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        session.Log($"dotnet --list-runtimes output:\n{output}");

                        // Check for Microsoft.WindowsDesktop.App 8.x
                        bool hasDesktopRuntime = output
                            .Split('\n')
                            .Any(line => line.Contains("Microsoft.WindowsDesktop.App") && line.Contains(" 8."));

                        if (hasDesktopRuntime)
                        {
                            session["DOTNET_RUNTIME_INSTALLED"] = "1";
                            session.Log(".NET 8.0 Desktop Runtime is installed");
                            return ActionResult.Success;
                        }
                        else
                        {
                            session.Log(".NET 8.0 Desktop Runtime NOT found");
                            session.Message(InstallMessage.Warning,
                                new Record
                                {
                                    FormatString = ".NET 8.0 Desktop Runtime is required but not detected. " +
                                                 "The bootstrapper should install it automatically. " +
                                                 "If installation fails, download it from: " +
                                                 "https://dotnet.microsoft.com/download/dotnet/8.0"
                                });
                            return ActionResult.Success; // Continue - bootstrapper handles it
                        }
                    }
                }

                session.Log("Could not execute 'dotnet' command");
                return ActionResult.Success; // Continue anyway
            }
            catch (Exception ex)
            {
                session.Log($"Error checking .NET runtime: {ex.Message}");
                return ActionResult.Success; // Continue - don't block installation
            }
        }

        /// <summary>
        /// Custom Action: Update ConfigMgr extension XML manifest with actual installation path
        /// </summary>
        [CustomAction]
        public static ActionResult UpdateXmlManifestPath(Session session)
        {
            session.Log("Begin UpdateXmlManifestPath");

            try
            {
                // Get custom action data (passed from CA_SetXmlPath)
                string customActionData = session.CustomActionData["XmlPath"];
                string exePath = session.CustomActionData["ExePath"];

                session.Log($"XML Path: {customActionData}");
                session.Log($"EXE Path: {exePath}");

                if (string.IsNullOrEmpty(customActionData) || string.IsNullOrEmpty(exePath))
                {
                    session.Log("ERROR: Custom action data is empty");
                    return ActionResult.Failure;
                }

                if (!File.Exists(customActionData))
                {
                    session.Log($"ERROR: XML manifest not found at: {customActionData}");
                    return ActionResult.Failure;
                }

                // Load XML
                XDocument doc = XDocument.Load(customActionData);
                XNamespace ns = doc.Root?.Name.Namespace ?? XNamespace.None;

                // Find <FilePath> element
                var filePathElement = doc.Descendants(ns + "FilePath").FirstOrDefault();

                if (filePathElement == null)
                {
                    session.Log("ERROR: Could not find <FilePath> element in XML");
                    return ActionResult.Failure;
                }

                // Update path
                string oldPath = filePathElement.Value;
                filePathElement.Value = exePath;

                // Save XML
                doc.Save(customActionData);

                session.Log($"Updated XML manifest:");
                session.Log($"  Old Path: {oldPath}");
                session.Log($"  New Path: {exePath}");
                session.Log($"SUCCESS: XML manifest updated");

                return ActionResult.Success;
            }
            catch (Exception ex)
            {
                session.Log($"ERROR in UpdateXmlManifestPath: {ex.Message}");
                session.Log($"Stack Trace: {ex.StackTrace}");
                return ActionResult.Failure;
            }
        }
    }
}
