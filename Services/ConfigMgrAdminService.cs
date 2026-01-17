using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Management;
using Azure.Identity;
using Azure.Core;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Services
{
    /// <summary>
    /// ConfigMgr Admin Service integration for querying device inventory
    /// Supports both Admin Service (REST API) and WMI (SDK fallback)
    /// Documentation: https://learn.microsoft.com/en-us/mem/configmgr/develop/adminservice/
    /// </summary>
    public class ConfigMgrAdminService
    {
        private readonly HttpClient _httpClient;
        private string? _adminServiceUrl;
        private string? _siteServer;
        private string? _siteCode;
        private bool _isAuthenticated = false;
        private bool _useWmiFallback = false;
        
        // Connection diagnostics
        private string _lastConnectionError = string.Empty;
        private string _connectionMethod = "None";
        
        public string ConnectionMethod => _connectionMethod;
        public string LastConnectionError => _lastConnectionError;
        public bool IsUsingWmiFallback => _useWmiFallback;

        public ConfigMgrAdminService()
        {
            // Use HttpClientHandler with Windows authentication for Admin Service
            var handler = new HttpClientHandler
            {
                UseDefaultCredentials = true, // Use current Windows credentials
                PreAuthenticate = true,
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true // Accept self-signed certs
            };
            
            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.Timeout = TimeSpan.FromSeconds(30); // Longer timeout for network calls
        }

        /// <summary>
        /// Auto-detect Admin Service URL from ConfigMgr Console installation
        /// Returns tuple of (URL, DebugInfo) for troubleshooting
        /// </summary>
        public (string? url, string debugInfo) DetectAdminServiceUrl()
        {
            var debugInfo = new System.Text.StringBuilder("ConfigMgr Console Detection:\n");
            
            try
            {
                // Check multiple registry locations
                var registryPaths = new[]
                {
                    @"Software\Microsoft\ConfigMgr10\AdminUI\Connection",
                    @"Software\Microsoft\SMS\AdminUI\Connection",
                    @"Software\Wow6432Node\Microsoft\ConfigMgr10\AdminUI\Connection",
                    @"Software\Wow6432Node\Microsoft\SMS\AdminUI\Connection"
                };

                foreach (var path in registryPaths)
                {
                    debugInfo.AppendLine($"  Checking: HKCU\\{path}");
                    
                    try
                    {
                        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(path))
                        {
                            if (key != null)
                            {
                                var server = key.GetValue("Server") as string;
                                debugInfo.AppendLine($"    ✓ Key exists, Server value: {server ?? "(null)"}");
                                
                                if (!string.IsNullOrEmpty(server))
                                {
                                    // Remove any port or protocol if present
                                    server = server.Split(':')[0].Split('/')[0];
                                    var url = $"https://{server}/AdminService";
                                    debugInfo.AppendLine($"    ✓ Detected URL: {url}");
                                    return (url, debugInfo.ToString());
                                }
                            }
                            else
                            {
                                debugInfo.AppendLine($"    ✗ Key not found");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        debugInfo.AppendLine($"    ✗ Error: {ex.Message}");
                    }
                }

                // Try LocalMachine registry (Console installed for all users)
                debugInfo.AppendLine($"\n  Checking: HKLM\\Software\\Microsoft\\SMS\\Setup");
                try
                {
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\SMS\Setup"))
                    {
                        if (key != null)
                        {
                            var installDir = key.GetValue("UI Installation Directory") as string;
                            var siteServer = key.GetValue("Site Server") as string;
                            
                            debugInfo.AppendLine($"    Install Dir: {installDir ?? "(not found)"}");
                            debugInfo.AppendLine($"    Site Server: {siteServer ?? "(not found)"}");
                            
                            if (!string.IsNullOrEmpty(siteServer))
                            {
                                var url = $"https://{siteServer}/AdminService";
                                debugInfo.AppendLine($"    ✓ Detected URL: {url}");
                                return (url, debugInfo.ToString());
                            }
                        }
                        else
                        {
                            debugInfo.AppendLine($"    ✗ Key not found");
                        }
                    }
                }
                catch (Exception ex)
                {
                    debugInfo.AppendLine($"    ✗ Error: {ex.Message}");
                }

                debugInfo.AppendLine($"\n  Result: ConfigMgr Console not detected");
            }
            catch (Exception ex)
            {
                debugInfo.AppendLine($"\n  Fatal error: {ex.Message}");
            }

            return (null, debugInfo.ToString());
        }

        /// <summary>
        /// Configure the Admin Service connection with WMI fallback
        /// </summary>
        /// <param name="adminServiceUrl">Admin Service URL (e.g., https://CM01.contoso.com/AdminService)</param>
        /// <summary>
        /// Log ConfigMgr environment details for troubleshooting
        /// </summary>
        private void LogEnvironmentInfo()
        {
            try
            {
                FileLogger.Instance.Info("=== CONFIGMGR ENVIRONMENT INFO ===");
                FileLogger.Instance.Info($"   Server: {_siteServer ?? "(not set)"}");
                FileLogger.Instance.Info($"   Site Code: {_siteCode ?? "(not set)"}");
                FileLogger.Instance.Info($"   Admin Service URL: {_adminServiceUrl ?? "(not set)"}");
                FileLogger.Instance.Info($"   Connection Method: {_connectionMethod}");
                FileLogger.Instance.Info($"   Using WMI Fallback: {_useWmiFallback}");
                FileLogger.Instance.Info($"   Current User: {Environment.UserName}");
                FileLogger.Instance.Info($"   Domain: {Environment.UserDomainName}");
                FileLogger.Instance.Info($"   Machine: {Environment.MachineName}");
                
                // Try to get ConfigMgr build version from registry
                try
                {
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\SMS\Setup"))
                    {
                        if (key != null)
                        {
                            var version = key.GetValue("Full Version") as string;
                            var buildNumber = key.GetValue("Build") as string;
                            FileLogger.Instance.Info($"   ConfigMgr Version: {version ?? "Unknown"}");
                            FileLogger.Instance.Info($"   ConfigMgr Build: {buildNumber ?? "Unknown"}");
                        }
                    }
                }
                catch { /* Ignore registry errors */ }
                
                FileLogger.Instance.Info("======================================");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"Failed to log environment info: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Get ConfigMgr site code from Admin Service
        /// </summary>
        private async Task<string?> GetSiteCodeAsync()
        {
            try
            {
                if (_useWmiFallback || string.IsNullOrEmpty(_adminServiceUrl))
                    return null;
                    
                var response = await _httpClient.GetAsync($"{_adminServiceUrl}/wmi/SMS_Site");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<ConfigMgrSiteResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    return result?.Value?.FirstOrDefault()?.SiteCode;
                }
            }
            catch { /* Ignore errors */ }
            
            return null;
        }
        
        /// <summary>
        /// Get co-management details for a device from SMS_Client (Option 2)
        /// This provides workload assignments and co-management flags
        /// </summary>
        public async Task<CoManagementDetails?> GetCoManagementDetailsAsync(int resourceId)
        {
            if (!_isAuthenticated)
            {
                throw new InvalidOperationException("Not configured. Call ConfigureAsync first.");
            }

            try
            {
                var query = $"{_adminServiceUrl}/wmi/SMS_Client?$filter=ResourceID eq {resourceId}";
                var response = await _httpClient.GetAsync(query);
                
                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ConfigMgrClientResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Value != null && result.Value.Any())
                {
                    var client = result.Value.First();
                    return new CoManagementDetails
                    {
                        ResourceId = resourceId,
                        IsCoManaged = client.CoManagementFlags > 0,
                        CoManagementFlags = client.CoManagementFlags,
                        // Workload flags (bitfield):
                        // 1 = Compliance Policies
                        // 2 = Resource Access
                        // 4 = Device Configuration
                        // 8 = Windows Update
                        // 16 = Endpoint Protection
                        // 32 = Client Apps
                        // 64 = Office Click-to-Run
                        WorkloadFlags = client.CoManagementFlags
                    };
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"Failed to get co-management details for ResourceId {resourceId}: {ex.Message}");
            }

            return null;
        }
        
        public async Task<bool> ConfigureAsync(string adminServiceUrl)
        {
            try
            {
                _adminServiceUrl = adminServiceUrl?.TrimEnd('/');
                
                if (string.IsNullOrEmpty(_adminServiceUrl))
                {
                    return false;
                }

                // Extract site server from URL
                var uri = new Uri(_adminServiceUrl);
                _siteServer = uri.Host;

                // Test Admin Service connection first
                System.Diagnostics.Debug.WriteLine($"Testing Admin Service: {_adminServiceUrl}");
                var testUrl = $"{_adminServiceUrl}/wmi/SMS_Site";
                
                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.GetAsync(testUrl);
                    System.Diagnostics.Debug.WriteLine($"Admin Service response: {response.StatusCode}");
                }
                catch (HttpRequestException httpEx)
                {
                    _lastConnectionError = $"Admin Service HTTP error: {httpEx.Message}";
                    System.Diagnostics.Debug.WriteLine($"❌ Admin Service HTTP error: {httpEx.Message}");
                    System.Diagnostics.Debug.WriteLine($"   This usually means: Admin Service not enabled, HTTPS not configured, or firewall blocking port 443");
                    return await TryWmiFallbackAsync();
                }
                catch (TaskCanceledException timeoutEx)
                {
                    _lastConnectionError = $"Admin Service timeout: {timeoutEx.Message}";
                    System.Diagnostics.Debug.WriteLine($"❌ Admin Service timeout: {timeoutEx.Message}");
                    System.Diagnostics.Debug.WriteLine($"   This usually means: Site server unreachable or network issues");
                    return await TryWmiFallbackAsync();
                }
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"✅ Admin Service connected successfully, response length: {content.Length}");
                    _isAuthenticated = true;
                    _useWmiFallback = false;
                    _connectionMethod = "Admin Service (REST API)";
                    _lastConnectionError = string.Empty;
                    
                    // Get site code
                    _siteCode = await GetSiteCodeAsync();
                    
                    // Log environment details
                    LogEnvironmentInfo();
                    
                    return true;
                }
                else
                {
                    // Admin Service failed, try WMI fallback
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _lastConnectionError = $"Admin Service returned {(int)response.StatusCode} {response.StatusCode}: {response.ReasonPhrase}";
                    if (!string.IsNullOrEmpty(errorBody) && errorBody.Length < 500)
                    {
                        _lastConnectionError += $" | Response: {errorBody}";
                    }
                    System.Diagnostics.Debug.WriteLine($"❌ Admin Service failed: {_lastConnectionError}");
                    
                    // Provide helpful hints based on status code
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        System.Diagnostics.Debug.WriteLine($"   Hint: Authentication failed. Check if your account has SMS Provider permissions.");
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        System.Diagnostics.Debug.WriteLine($"   Hint: Admin Service endpoint not found. May not be enabled (requires ConfigMgr 1810+).");
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        System.Diagnostics.Debug.WriteLine($"   Hint: Access forbidden. Check RBAC permissions in ConfigMgr.");
                    }
                    
                    return await TryWmiFallbackAsync();
                }
            }
            catch (Exception ex)
            {
                _lastConnectionError = $"Admin Service unexpected error: {ex.GetType().Name}: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"❌ Admin Service unexpected error: {ex}");
                // Try WMI as fallback
                return await TryWmiFallbackAsync();
            }
        }

        /// <summary>
        /// Try to connect via WMI (ConfigMgr SDK) as fallback
        /// </summary>
        private async Task<bool> TryWmiFallbackAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Attempting WMI fallback...");
                    
                    if (string.IsNullOrEmpty(_siteServer))
                    {
                        // Try to detect site server from registry
                        System.Diagnostics.Debug.WriteLine($"No site server specified, attempting detection...");
                        var (url, debugInfo) = DetectAdminServiceUrl();
                        if (!string.IsNullOrEmpty(url))
                        {
                            var uri = new Uri(url);
                            _siteServer = uri.Host;
                            System.Diagnostics.Debug.WriteLine($"Detected site server: {_siteServer}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Site server detection failed:\n{debugInfo}");
                        }
                    }

                    if (string.IsNullOrEmpty(_siteServer))
                    {
                        _lastConnectionError += " | WMI: Site server not detected";
                        System.Diagnostics.Debug.WriteLine("❌ WMI: Cannot proceed without site server");
                        return false;
                    }

                    System.Diagnostics.Debug.WriteLine($"Testing WMI connection to: \\\\{_siteServer}\\root\\sms");
                    
                    // Test WMI connection by getting site code
                    var scope = new ManagementScope($"\\\\{_siteServer}\\root\\sms");
                    var options = new ConnectionOptions
                    {
                        Impersonation = ImpersonationLevel.Impersonate,
                        Authentication = AuthenticationLevel.PacketPrivacy,
                        EnablePrivileges = true,
                        Timeout = TimeSpan.FromSeconds(30)
                    };
                    scope.Options = options;
                    
                    try
                    {
                        scope.Connect();
                        System.Diagnostics.Debug.WriteLine($"✓ WMI connection established to {_siteServer}");
                    }
                    catch (UnauthorizedAccessException authEx)
                    {
                        _lastConnectionError += $" | WMI: Access denied - {authEx.Message}";
                        System.Diagnostics.Debug.WriteLine($"❌ WMI Access Denied: {authEx.Message}");
                        System.Diagnostics.Debug.WriteLine($"   Hint: Your account needs SMS Provider permissions in ConfigMgr");
                        return false;
                    }
                    catch (System.Runtime.InteropServices.COMException comEx)
                    {
                        _lastConnectionError += $" | WMI: Connection failed - {comEx.Message}";
                        System.Diagnostics.Debug.WriteLine($"❌ WMI Connection Failed: {comEx.Message}");
                        System.Diagnostics.Debug.WriteLine($"   Hint: Check if WMI service is running and firewall allows WMI (port 135, dynamic RPC)");
                        return false;
                    }

                    var wqlQuery = "SELECT SiteCode FROM SMS_ProviderLocation WHERE ProviderForLocalSite = true";
                    Instance.LogWmiQuery("GetSiteCode", wqlQuery, $"\\\\{_siteServer}\\root\\sms");
                    
                    var query = new ObjectQuery(wqlQuery);
                    var searcher = new ManagementObjectSearcher(scope, query);
                    
                    System.Diagnostics.Debug.WriteLine($"Querying for site code...");
                    var results = searcher.Get();
                    
                    Instance.LogWmiQuery("GetSiteCode (Result)", wqlQuery, $"\\\\{_siteServer}\\root\\sms", results.Count);
                    
                    if (results.Count == 0)
                    {
                        _lastConnectionError += " | WMI: No site code found in SMS_ProviderLocation";
                        System.Diagnostics.Debug.WriteLine("❌ WMI: Query returned no results");
                        System.Diagnostics.Debug.WriteLine($"   Hint: ConfigMgr SMS Provider may not be installed on {_siteServer}");
                        return false;
                    }
                    
                    foreach (ManagementObject obj in results)
                    {
                        _siteCode = obj["SiteCode"]?.ToString();
                        System.Diagnostics.Debug.WriteLine($"Found site code: {_siteCode}");
                        
                        if (!string.IsNullOrEmpty(_siteCode))
                        {
                            // Update scope with site code
                            System.Diagnostics.Debug.WriteLine($"Connecting to site-specific namespace: site_{_siteCode}");
                            scope = new ManagementScope($"\\\\{_siteServer}\\root\\sms\\site_{_siteCode}", options);
                            scope.Connect();
                            
                            // Test query to verify access
                            var testWql = "SELECT TOP 1 ResourceID FROM SMS_R_System";
                            Instance.LogWmiQuery("TestConnection", testWql, $"\\\\{_siteServer}\\root\\sms\\site_{_siteCode}");
                            
                            var testQuery = new ObjectQuery(testWql);
                            var testSearcher = new ManagementObjectSearcher(scope, testQuery);
                            var testResults = testSearcher.Get();
                            
                            Instance.LogWmiQuery("TestConnection (Result)", testWql, $"\\\\{_siteServer}\\root\\sms\\site_{_siteCode}", testResults.Count);
                            System.Diagnostics.Debug.WriteLine($"✓ Successfully queried SMS_R_System, found {testResults.Count} devices");
                            
                            _isAuthenticated = true;
                            _useWmiFallback = true;
                            _connectionMethod = "WMI Fallback (ConfigMgr SDK)";
                            _lastConnectionError = string.Empty; // Clear error - WMI worked
                            System.Diagnostics.Debug.WriteLine($"✅ WMI fallback SUCCESSFUL: {_siteServer}, Site: {_siteCode}");
                            return true;
                        }
                    }

                    _lastConnectionError += " | WMI: Site code was null or empty";
                    System.Diagnostics.Debug.WriteLine("❌ WMI: Site code was null or empty");
                    return false;
                }
                catch (Exception ex)
                {
                    _lastConnectionError += $" | WMI failed: {ex.GetType().Name}: {ex.Message}";
                    System.Diagnostics.Debug.WriteLine($"❌ WMI fallback failed: {ex.GetType().Name}: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"   Stack trace: {ex.StackTrace}");
                    return false;
                }
            });
        }

        /// <summary>
        /// Get Windows 10/11 devices from ConfigMgr that are eligible for Intune enrollment
        /// </summary>
        public async Task<List<ConfigMgrDevice>> GetWindows1011DevicesAsync()
        {
            if (!_isAuthenticated)
            {
                throw new InvalidOperationException("Not configured. Call ConfigureAsync first.");
            }

            if (_useWmiFallback)
            {
                return await GetDevicesViaWmiAsync();
            }
            else
            {
                return await GetDevicesViaRestApiAsync();
            }
        }

        /// <summary>
        /// Get devices via Admin Service REST API
        /// </summary>
        private async Task<List<ConfigMgrDevice>> GetDevicesViaRestApiAsync()
        {
            try
            {
                string query;
                HttpResponseMessage response;
                
                // Try query with $select first (preferred - less data transfer)
                query = $"{_adminServiceUrl}/wmi/SMS_R_System?$filter=" +
                    "contains(OperatingSystemNameandVersion,'Microsoft Windows NT Workstation 10') or " +
                    "contains(OperatingSystemNameandVersion,'Microsoft Windows NT Workstation 11')" +
                    "&$select=ResourceId,Name,OperatingSystemNameandVersion,LastActiveTime,ClientVersion,ResourceDomainORWorkgroup";

                Instance.LogAdminServiceQuery("GetWindows1011Devices", query);
                Instance.Info("=== ConfigMgr Admin Service REST API Query ===");
                Instance.Info($"   Query URL: {query}");
                Instance.Info($"   Method: GET");
                Instance.Info($"   Authentication: Windows Integrated (UseDefaultCredentials)");
                
                response = await _httpClient.GetAsync(query);
                
                Instance.Info($"   Response Status: {(int)response.StatusCode} {response.StatusCode}");
                
                // If 404, try without $select (field might not exist)
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Instance.Warning("   ⚠️ Query with $select failed (404), trying without $select parameter...");
                    query = $"{_adminServiceUrl}/wmi/SMS_R_System?$filter=" +
                        "contains(OperatingSystemNameandVersion,'Microsoft Windows NT Workstation 10') or " +
                        "contains(OperatingSystemNameandVersion,'Microsoft Windows NT Workstation 11')";
                    
                    Instance.LogAdminServiceQuery("GetWindows1011Devices (Retry)", query);
                    Instance.Info($"   Retry Query URL: {query}");
                    response = await _httpClient.GetAsync(query);
                    Instance.Info($"   Retry Response Status: {(int)response.StatusCode} {response.StatusCode}");
                }
                
                // If still 404, try simple query without filter (get all devices, filter client-side)
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Instance.Warning("   ⚠️ Query with contains() failed (404), trying simple query with $top limit...");
                    query = $"{_adminServiceUrl}/wmi/SMS_R_System?$top=5000";
                    
                    Instance.LogAdminServiceQuery("GetWindows1011Devices (Fallback)", query);
                    Instance.Info($"   Fallback Query URL: {query}");
                    response = await _httpClient.GetAsync(query);
                    Instance.Info($"   Fallback Response Status: {(int)response.StatusCode} {response.StatusCode}");
                }
                
                Instance.Info($"   Response Headers: {response.Headers}");
                
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                FileLogger.Instance.Info($"   Response Length: {content.Length} bytes");
                
                var result = JsonSerializer.Deserialize<ConfigMgrResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var devices = new List<ConfigMgrDevice>();

                if (result?.Value != null)
                {
                    FileLogger.Instance.Info($"   Total devices returned: {result.Value.Count}");
                    
                    // Filter for Windows 10/11 if we got all devices (fallback query)
                    var filteredDevices = result.Value.Where(d => 
                        d.OperatingSystemNameandVersion != null && 
                        (d.OperatingSystemNameandVersion.Contains("Workstation 10") || 
                         d.OperatingSystemNameandVersion.Contains("Workstation 11"))).ToList();
                    
                    FileLogger.Instance.Info($"   Windows 10/11 workstations: {filteredDevices.Count}");
                    
                    // Create device list - co-management will be determined by cross-referencing with Intune
                    foreach (var device in filteredDevices)
                    {
                        devices.Add(new ConfigMgrDevice
                        {
                            ResourceId = device.ResourceId,
                            Name = device.Name ?? "Unknown",
                            OperatingSystem = device.OperatingSystemNameandVersion ?? "Unknown",
                            LastActiveTime = device.LastActiveTime,
                            ClientVersion = device.ClientVersion,
                            IsCoManaged = false, // Will be set by cross-referencing with Intune
                            CoManagementFlags = 0, // Will be populated from SMS_Client if needed
                            DomainOrWorkgroup = device.ResourceDomainORWorkgroup
                        });
                    }
                    
                    FileLogger.Instance.Info($"   📋 Note: Co-management status will be determined by cross-checking with Intune");
                    FileLogger.Instance.Info($"      SMS_R_System doesn't contain co-management data");
                    FileLogger.Instance.Info($"      Use GetCoManagementDetailsAsync() for workload assignments");
                }
                else
                {
                    FileLogger.Instance.Warning("   ⚠️ Response contained no devices (Value was null)");
                }
                
                FileLogger.Instance.Info("=============================================");

                return devices;
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Failed to query ConfigMgr Admin Service: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error processing ConfigMgr device data: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get devices via WMI (SDK fallback)
        /// </summary>
        private async Task<List<ConfigMgrDevice>> GetDevicesViaWmiAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var devices = new List<ConfigMgrDevice>();
                    var scope = new ManagementScope($"\\\\{_siteServer}\\root\\sms\\site_{_siteCode}");
                    scope.Connect();

                    // Query for Windows 10/11 workstations
                    var query = new SelectQuery("SMS_R_System", 
                        "OperatingSystemNameandVersion LIKE 'Microsoft Windows NT Workstation 10%' OR " +
                        "OperatingSystemNameandVersion LIKE 'Microsoft Windows NT Workstation 11%'");
                    
                    var searcher = new ManagementObjectSearcher(scope, query);
                    
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var device = new ConfigMgrDevice
                        {
                            ResourceId = Convert.ToInt32(obj["ResourceId"]),
                            Name = obj["Name"]?.ToString() ?? "Unknown",
                            OperatingSystem = obj["OperatingSystemNameandVersion"]?.ToString() ?? "Unknown",
                            ClientVersion = obj["ClientVersion"]?.ToString(),
                            IsCoManaged = false, // Will check separately
                            CoManagementFlags = 0
                        };

                        // Check co-management status
                        try
                        {
                            var coMgmtQuery = new SelectQuery("SMS_Client_ComanagementState",
                                $"ResourceID = {device.ResourceId}");
                            var coMgmtSearcher = new ManagementObjectSearcher(scope, coMgmtQuery);
                            
                            foreach (ManagementObject coMgmtObj in coMgmtSearcher.Get())
                            {
                                var flags = coMgmtObj["CoManagementFlags"];
                                if (flags != null)
                                {
                                    device.CoManagementFlags = Convert.ToInt32(flags);
                                    device.IsCoManaged = device.CoManagementFlags > 0;
                                }
                                break;
                            }
                        }
                        catch
                        {
                            // Co-management data not available, continue with default values
                        }

                        devices.Add(device);
                    }

                    return devices;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to query ConfigMgr via WMI: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Get co-management status for devices
        /// </summary>
        public async Task<Dictionary<string, int>> GetCoManagementStatusAsync()
        {
            if (!_isAuthenticated || string.IsNullOrEmpty(_adminServiceUrl))
            {
                throw new InvalidOperationException("Not configured. Call ConfigureAsync first.");
            }

            try
            {
                var devices = await GetWindows1011DevicesAsync();
                
                var status = new Dictionary<string, int>
                {
                    ["TotalWindows1011"] = devices.Count,
                    ["CoManaged"] = devices.Count(d => d.IsCoManaged),
                    ["ConfigMgrOnly"] = devices.Count(d => !d.IsCoManaged)
                };

                return status;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting co-management status: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get ConfigMgr application inventory
        /// </summary>
        public async Task<List<ConfigMgrApplication>> GetApplicationsAsync()
        {
            if (!_isAuthenticated)
            {
                throw new InvalidOperationException("Not configured. Call ConfigureAsync first.");
            }

            if (_useWmiFallback)
            {
                return await GetApplicationsViaWmiAsync();
            }
            else
            {
                return await GetApplicationsViaRestApiAsync();
            }
        }

        private async Task<List<ConfigMgrApplication>> GetApplicationsViaRestApiAsync()
        {
            try
            {
                var query = $"{_adminServiceUrl}/wmi/SMS_Application?$select=LocalizedDisplayName,SoftwareVersion,NumberOfDeploymentTypes,IsDeployed,IsSuperseded,DateCreated,DateLastModified";

                var response = await _httpClient.GetAsync(query);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ConfigMgrApplicationResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var apps = new List<ConfigMgrApplication>();
                if (result?.Value != null)
                {
                    foreach (var app in result.Value)
                    {
                        apps.Add(new ConfigMgrApplication
                        {
                            Name = app.LocalizedDisplayName ?? "Unknown",
                            Version = app.SoftwareVersion ?? "",
                            DeploymentTypeCount = app.NumberOfDeploymentTypes,
                            IsDeployed = app.IsDeployed,
                            IsSuperseded = app.IsSuperseded,
                            DateCreated = app.DateCreated,
                            DateLastModified = app.DateLastModified
                        });
                    }
                }

                return apps;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get applications via REST: {ex.Message}", ex);
            }
        }

        private async Task<List<ConfigMgrApplication>> GetApplicationsViaWmiAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var apps = new List<ConfigMgrApplication>();
                    var scope = new ManagementScope($"\\\\{_siteServer}\\root\\sms\\site_{_siteCode}");
                    scope.Connect();

                    var query = new SelectQuery("SMS_Application");
                    var searcher = new ManagementObjectSearcher(scope, query);

                    foreach (ManagementObject obj in searcher.Get())
                    {
                        apps.Add(new ConfigMgrApplication
                        {
                            Name = obj["LocalizedDisplayName"]?.ToString() ?? "Unknown",
                            Version = obj["SoftwareVersion"]?.ToString() ?? "",
                            DeploymentTypeCount = obj["NumberOfDeploymentTypes"] != null ? Convert.ToInt32(obj["NumberOfDeploymentTypes"]) : 0,
                            IsDeployed = obj["IsDeployed"] != null && Convert.ToBoolean(obj["IsDeployed"]),
                            IsSuperseded = obj["IsSuperseded"] != null && Convert.ToBoolean(obj["IsSuperseded"]),
                            DateCreated = obj["DateCreated"] != null ? ManagementDateTimeConverter.ToDateTime(obj["DateCreated"].ToString()) : null,
                            DateLastModified = obj["DateLastModified"] != null ? ManagementDateTimeConverter.ToDateTime(obj["DateLastModified"].ToString()) : null
                        });
                    }

                    return apps;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to get applications via WMI: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Get hardware inventory for devices (model, manufacturer, age)
        /// </summary>
        public async Task<List<ConfigMgrHardwareInfo>> GetHardwareInventoryAsync()
        {
            if (!_isAuthenticated)
            {
                throw new InvalidOperationException("Not configured. Call ConfigureAsync first.");
            }

            if (_useWmiFallback)
            {
                return await GetHardwareInventoryViaWmiAsync();
            }
            else
            {
                return await GetHardwareInventoryViaRestApiAsync();
            }
        }

        private async Task<List<ConfigMgrHardwareInfo>> GetHardwareInventoryViaRestApiAsync()
        {
            try
            {
                var query = $"{_adminServiceUrl}/wmi/SMS_G_System_COMPUTER_SYSTEM?$select=ResourceID,Manufacturer,Model,SystemType";

                var response = await _httpClient.GetAsync(query);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ConfigMgrHardwareResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var hardware = new List<ConfigMgrHardwareInfo>();
                if (result?.Value != null)
                {
                    foreach (var hw in result.Value)
                    {
                        hardware.Add(new ConfigMgrHardwareInfo
                        {
                            ResourceId = hw.ResourceID,
                            Manufacturer = hw.Manufacturer ?? "Unknown",
                            Model = hw.Model ?? "Unknown",
                            SystemType = hw.SystemType ?? "Unknown"
                        });
                    }
                }

                return hardware;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get hardware inventory via REST: {ex.Message}", ex);
            }
        }

        private async Task<List<ConfigMgrHardwareInfo>> GetHardwareInventoryViaWmiAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var hardware = new List<ConfigMgrHardwareInfo>();
                    var scope = new ManagementScope($"\\\\{_siteServer}\\root\\sms\\site_{_siteCode}");
                    scope.Connect();

                    var query = new SelectQuery("SMS_G_System_COMPUTER_SYSTEM");
                    var searcher = new ManagementObjectSearcher(scope, query);

                    foreach (ManagementObject obj in searcher.Get())
                    {
                        hardware.Add(new ConfigMgrHardwareInfo
                        {
                            ResourceId = Convert.ToInt32(obj["ResourceID"]),
                            Manufacturer = obj["Manufacturer"]?.ToString() ?? "Unknown",
                            Model = obj["Model"]?.ToString() ?? "Unknown",
                            SystemType = obj["SystemType"]?.ToString() ?? "Unknown"
                        });
                    }

                    return hardware;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to get hardware inventory via WMI: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Get software update compliance status
        /// </summary>
        public async Task<List<ConfigMgrUpdateCompliance>> GetSoftwareUpdateComplianceAsync()
        {
            if (!_isAuthenticated)
            {
                throw new InvalidOperationException("Not configured. Call ConfigureAsync first.");
            }

            if (_useWmiFallback)
            {
                return await GetUpdateComplianceViaWmiAsync();
            }
            else
            {
                return await GetUpdateComplianceViaRestApiAsync();
            }
        }

        private async Task<List<ConfigMgrUpdateCompliance>> GetUpdateComplianceViaRestApiAsync()
        {
            try
            {
                var query = $"{_adminServiceUrl}/wmi/SMS_UpdateComplianceStatus?$select=ResourceID,Status,LastStatusCheckTime";

                var response = await _httpClient.GetAsync(query);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ConfigMgrUpdateComplianceResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var compliance = new List<ConfigMgrUpdateCompliance>();
                if (result?.Value != null)
                {
                    foreach (var item in result.Value)
                    {
                        compliance.Add(new ConfigMgrUpdateCompliance
                        {
                            ResourceId = item.ResourceID,
                            ComplianceStatus = item.Status,
                            LastCheckTime = item.LastStatusCheckTime
                        });
                    }
                }

                return compliance;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get update compliance via REST: {ex.Message}", ex);
            }
        }

        private async Task<List<ConfigMgrUpdateCompliance>> GetUpdateComplianceViaWmiAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var compliance = new List<ConfigMgrUpdateCompliance>();
                    var scope = new ManagementScope($"\\\\{_siteServer}\\root\\sms\\site_{_siteCode}");
                    scope.Connect();

                    var query = new SelectQuery("SMS_UpdateComplianceStatus");
                    var searcher = new ManagementObjectSearcher(scope, query);

                    foreach (ManagementObject obj in searcher.Get())
                    {
                        compliance.Add(new ConfigMgrUpdateCompliance
                        {
                            ResourceId = Convert.ToInt32(obj["ResourceID"]),
                            ComplianceStatus = obj["Status"] != null ? Convert.ToInt32(obj["Status"]) : 0,
                            LastCheckTime = obj["LastStatusCheckTime"] != null ? ManagementDateTimeConverter.ToDateTime(obj["LastStatusCheckTime"].ToString()) : null
                        });
                    }

                    return compliance;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to get update compliance via WMI: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Get collection membership for devices
        /// </summary>
        public async Task<List<ConfigMgrCollectionMembership>> GetCollectionMembershipsAsync(int resourceId)
        {
            if (!_isAuthenticated)
            {
                throw new InvalidOperationException("Not configured. Call ConfigureAsync first.");
            }

            if (_useWmiFallback)
            {
                return await GetCollectionMembershipsViaWmiAsync(resourceId);
            }
            else
            {
                return await GetCollectionMembershipsViaRestApiAsync(resourceId);
            }
        }

        private async Task<List<ConfigMgrCollectionMembership>> GetCollectionMembershipsViaRestApiAsync(int resourceId)
        {
            try
            {
                var query = $"{_adminServiceUrl}/wmi/SMS_FullCollectionMembership?$filter=ResourceID eq {resourceId}&$select=CollectionID";

                var response = await _httpClient.GetAsync(query);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ConfigMgrCollectionMembershipResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var memberships = new List<ConfigMgrCollectionMembership>();
                if (result?.Value != null)
                {
                    foreach (var item in result.Value)
                    {
                        memberships.Add(new ConfigMgrCollectionMembership
                        {
                            ResourceId = resourceId,
                            CollectionId = item.CollectionID ?? ""
                        });
                    }
                }

                return memberships;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get collection memberships via REST: {ex.Message}", ex);
            }
        }

        private async Task<List<ConfigMgrCollectionMembership>> GetCollectionMembershipsViaWmiAsync(int resourceId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var memberships = new List<ConfigMgrCollectionMembership>();
                    var scope = new ManagementScope($"\\\\{_siteServer}\\root\\sms\\site_{_siteCode}");
                    scope.Connect();

                    var query = new SelectQuery("SMS_FullCollectionMembership", $"ResourceID = {resourceId}");
                    var searcher = new ManagementObjectSearcher(scope, query);

                    foreach (ManagementObject obj in searcher.Get())
                    {
                        memberships.Add(new ConfigMgrCollectionMembership
                        {
                            ResourceId = resourceId,
                            CollectionId = obj["CollectionID"]?.ToString() ?? ""
                        });
                    }

                    return memberships;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to get collection memberships via WMI: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Get client health metrics beyond basic version
        /// </summary>
        public async Task<List<ConfigMgrClientHealth>> GetClientHealthMetricsAsync()
        {
            if (!_isAuthenticated)
            {
                throw new InvalidOperationException("Not configured. Call ConfigureAsync first.");
            }

            if (_useWmiFallback)
            {
                return await GetClientHealthViaWmiAsync();
            }
            else
            {
                return await GetClientHealthViaRestApiAsync();
            }
        }

        private async Task<List<ConfigMgrClientHealth>> GetClientHealthViaRestApiAsync()
        {
            try
            {
                var query = $"{_adminServiceUrl}/wmi/SMS_CH_Summary?$select=ResourceID,ClientActiveStatus,LastActiveTime,LastPolicyRequest,LastDDR,LastHardwareScan,LastSoftwareScan";

                var response = await _httpClient.GetAsync(query);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ConfigMgrClientHealthResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var healthMetrics = new List<ConfigMgrClientHealth>();
                if (result?.Value != null)
                {
                    foreach (var item in result.Value)
                    {
                        healthMetrics.Add(new ConfigMgrClientHealth
                        {
                            ResourceId = item.ResourceID,
                            ClientActiveStatus = item.ClientActiveStatus,
                            LastActiveTime = item.LastActiveTime,
                            LastPolicyRequest = item.LastPolicyRequest,
                            LastDDR = item.LastDDR,
                            LastHardwareScan = item.LastHardwareScan,
                            LastSoftwareScan = item.LastSoftwareScan
                        });
                    }
                }

                return healthMetrics;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get client health metrics via REST: {ex.Message}", ex);
            }
        }

        private async Task<List<ConfigMgrClientHealth>> GetClientHealthViaWmiAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var healthMetrics = new List<ConfigMgrClientHealth>();
                    var scope = new ManagementScope($"\\\\{_siteServer}\\root\\sms\\site_{_siteCode}");
                    scope.Connect();

                    var query = new SelectQuery("SMS_CH_Summary");
                    var searcher = new ManagementObjectSearcher(scope, query);

                    foreach (ManagementObject obj in searcher.Get())
                    {
                        healthMetrics.Add(new ConfigMgrClientHealth
                        {
                            ResourceId = Convert.ToInt32(obj["ResourceID"]),
                            ClientActiveStatus = obj["ClientActiveStatus"] != null ? Convert.ToInt32(obj["ClientActiveStatus"]) : 0,
                            LastActiveTime = obj["LastActiveTime"] != null ? ManagementDateTimeConverter.ToDateTime(obj["LastActiveTime"].ToString()) : null,
                            LastPolicyRequest = obj["LastPolicyRequest"] != null ? ManagementDateTimeConverter.ToDateTime(obj["LastPolicyRequest"].ToString()) : null,
                            LastDDR = obj["LastDDR"] != null ? ManagementDateTimeConverter.ToDateTime(obj["LastDDR"].ToString()) : null,
                            LastHardwareScan = obj["LastHardwareScan"] != null ? ManagementDateTimeConverter.ToDateTime(obj["LastHardwareScan"].ToString()) : null,
                            LastSoftwareScan = obj["LastSoftwareScan"] != null ? ManagementDateTimeConverter.ToDateTime(obj["LastSoftwareScan"].ToString()) : null
                        });
                    }

                    return healthMetrics;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to get client health metrics via WMI: {ex.Message}", ex);
                }
            });
        }

        #region Security Inventory for Enrollment Simulator

        /// <summary>
        /// Get BitLocker encryption status for all devices.
        /// Uses SMS_G_System_ENCRYPTABLE_VOLUME for drive-level encryption info.
        /// </summary>
        public async Task<List<BitLockerStatus>> GetBitLockerStatusAsync()
        {
            if (!_isAuthenticated)
            {
                throw new InvalidOperationException("Not configured. Call ConfigureAsync first.");
            }

            Instance.LogAdminServiceQuery("BitLocker Status", "SMS_G_System_ENCRYPTABLE_VOLUME - Drive encryption status");

            if (_useWmiFallback)
            {
                return await GetBitLockerStatusViaWmiAsync();
            }
            else
            {
                return await GetBitLockerStatusViaRestApiAsync();
            }
        }

        private async Task<List<BitLockerStatus>> GetBitLockerStatusViaRestApiAsync()
        {
            try
            {
                // Query ENCRYPTABLE_VOLUME for BitLocker status per drive
                var query = $"{_adminServiceUrl}/wmi/SMS_G_System_ENCRYPTABLE_VOLUME?$select=ResourceID,DriveLetter,ProtectionStatus,ConversionStatus,EncryptionMethod";
                var response = await _httpClient.GetAsync(query);

                if (!response.IsSuccessStatusCode)
                {
                    Instance.Warning($"BitLocker query failed: {response.StatusCode}. This class may not be inventoried.");
                    return new List<BitLockerStatus>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<BitLockerResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var statuses = new List<BitLockerStatus>();
                if (result?.Value != null)
                {
                    // Group by ResourceID, focus on OS drive (usually C:)
                    var grouped = result.Value.GroupBy(v => v.ResourceID);
                    foreach (var group in grouped)
                    {
                        var osDrive = group.FirstOrDefault(d => d.DriveLetter == "C:") ?? group.First();
                        statuses.Add(new BitLockerStatus
                        {
                            ResourceId = group.Key,
                            DriveLetter = osDrive.DriveLetter ?? "C:",
                            ProtectionStatus = osDrive.ProtectionStatus,
                            ConversionStatus = osDrive.ConversionStatus,
                            EncryptionMethod = osDrive.EncryptionMethod,
                            IsProtected = osDrive.ProtectionStatus == 1 || osDrive.ProtectionStatus == 2
                        });
                    }
                }

                Instance.Info($"[CONFIGMGR] Retrieved BitLocker status for {statuses.Count} devices");
                return statuses;
            }
            catch (Exception ex)
            {
                Instance.Warning($"Failed to get BitLocker status via REST: {ex.Message}");
                return new List<BitLockerStatus>();
            }
        }

        private async Task<List<BitLockerStatus>> GetBitLockerStatusViaWmiAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var statuses = new List<BitLockerStatus>();
                    var scope = new ManagementScope($"\\\\{_siteServer}\\root\\sms\\site_{_siteCode}");
                    scope.Connect();

                    var query = new SelectQuery("SMS_G_System_ENCRYPTABLE_VOLUME", "", 
                        new[] { "ResourceID", "DriveLetter", "ProtectionStatus", "ConversionStatus", "EncryptionMethod" });
                    var searcher = new ManagementObjectSearcher(scope, query);

                    var grouped = new Dictionary<int, BitLockerStatus>();
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var resourceId = Convert.ToInt32(obj["ResourceID"]);
                        var driveLetter = obj["DriveLetter"]?.ToString() ?? "";
                        
                        // Only take C: drive or first drive if C: not found
                        if (!grouped.ContainsKey(resourceId) || driveLetter == "C:")
                        {
                            var protectionStatus = obj["ProtectionStatus"] != null ? Convert.ToInt32(obj["ProtectionStatus"]) : 0;
                            grouped[resourceId] = new BitLockerStatus
                            {
                                ResourceId = resourceId,
                                DriveLetter = driveLetter,
                                ProtectionStatus = protectionStatus,
                                ConversionStatus = obj["ConversionStatus"] != null ? Convert.ToInt32(obj["ConversionStatus"]) : 0,
                                EncryptionMethod = obj["EncryptionMethod"]?.ToString(),
                                IsProtected = protectionStatus == 1 || protectionStatus == 2
                            };
                        }
                    }

                    return grouped.Values.ToList();
                }
                catch (Exception ex)
                {
                    Instance.Warning($"Failed to get BitLocker status via WMI: {ex.Message}");
                    return new List<BitLockerStatus>();
                }
            });
        }

        /// <summary>
        /// Get Windows Firewall status for all devices.
        /// </summary>
        public async Task<List<FirewallStatus>> GetFirewallStatusAsync()
        {
            if (!_isAuthenticated)
            {
                throw new InvalidOperationException("Not configured. Call ConfigureAsync first.");
            }

            Instance.LogAdminServiceQuery("Firewall Status", "SMS_G_System_FIREWALL_PRODUCT - Windows Firewall state");

            if (_useWmiFallback)
            {
                return await GetFirewallStatusViaWmiAsync();
            }
            else
            {
                return await GetFirewallStatusViaRestApiAsync();
            }
        }

        private async Task<List<FirewallStatus>> GetFirewallStatusViaRestApiAsync()
        {
            try
            {
                // Try FIREWALL_PRODUCT first
                var query = $"{_adminServiceUrl}/wmi/SMS_G_System_FIREWALL_PRODUCT?$select=ResourceID,ProductState";
                var response = await _httpClient.GetAsync(query);

                var statuses = new List<FirewallStatus>();

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<FirewallResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result?.Value != null)
                    {
                        var grouped = result.Value.GroupBy(f => f.ResourceID);
                        foreach (var group in grouped)
                        {
                            var first = group.First();
                            // ProductState bit 4 (0x10) indicates firewall is on
                            var isEnabled = (first.ProductState & 0x10) != 0 || first.ProductState >= 262144;
                            statuses.Add(new FirewallStatus
                            {
                                ResourceId = group.Key,
                                ProductState = first.ProductState,
                                IsEnabled = isEnabled
                            });
                        }
                    }
                }
                else
                {
                    Instance.Warning($"Firewall query failed: {response.StatusCode}. Trying alternate class.");
                }

                Instance.Info($"[CONFIGMGR] Retrieved Firewall status for {statuses.Count} devices");
                return statuses;
            }
            catch (Exception ex)
            {
                Instance.Warning($"Failed to get Firewall status via REST: {ex.Message}");
                return new List<FirewallStatus>();
            }
        }

        private async Task<List<FirewallStatus>> GetFirewallStatusViaWmiAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var statuses = new List<FirewallStatus>();
                    var scope = new ManagementScope($"\\\\{_siteServer}\\root\\sms\\site_{_siteCode}");
                    scope.Connect();

                    var query = new SelectQuery("SMS_G_System_FIREWALL_PRODUCT", "", new[] { "ResourceID", "ProductState" });
                    var searcher = new ManagementObjectSearcher(scope, query);

                    var grouped = new Dictionary<int, FirewallStatus>();
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var resourceId = Convert.ToInt32(obj["ResourceID"]);
                        if (!grouped.ContainsKey(resourceId))
                        {
                            var productState = obj["ProductState"] != null ? Convert.ToInt32(obj["ProductState"]) : 0;
                            grouped[resourceId] = new FirewallStatus
                            {
                                ResourceId = resourceId,
                                ProductState = productState,
                                IsEnabled = (productState & 0x10) != 0 || productState >= 262144
                            };
                        }
                    }

                    return grouped.Values.ToList();
                }
                catch (Exception ex)
                {
                    Instance.Warning($"Failed to get Firewall status via WMI: {ex.Message}");
                    return new List<FirewallStatus>();
                }
            });
        }

        /// <summary>
        /// Get Antivirus/Defender status for all devices.
        /// </summary>
        public async Task<List<AntivirusStatus>> GetAntivirusStatusAsync()
        {
            if (!_isAuthenticated)
            {
                throw new InvalidOperationException("Not configured. Call ConfigureAsync first.");
            }

            Instance.LogAdminServiceQuery("Antivirus Status", "SMS_G_System_AntimalwareHealthStatus - Defender/AV status");

            if (_useWmiFallback)
            {
                return await GetAntivirusStatusViaWmiAsync();
            }
            else
            {
                return await GetAntivirusStatusViaRestApiAsync();
            }
        }

        private async Task<List<AntivirusStatus>> GetAntivirusStatusViaRestApiAsync()
        {
            try
            {
                var query = $"{_adminServiceUrl}/wmi/SMS_G_System_AntimalwareHealthStatus?$select=ResourceID,ProtectionEnabled,RealTimeProtectionEnabled,AntispywareEnabled,LastQuickScanDateTimeStart,SignatureUpToDate,SignatureAge";
                var response = await _httpClient.GetAsync(query);

                var statuses = new List<AntivirusStatus>();

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<AntivirusResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result?.Value != null)
                    {
                        foreach (var av in result.Value)
                        {
                            statuses.Add(new AntivirusStatus
                            {
                                ResourceId = av.ResourceID,
                                ProtectionEnabled = av.ProtectionEnabled,
                                RealTimeProtectionEnabled = av.RealTimeProtectionEnabled,
                                AntispywareEnabled = av.AntispywareEnabled,
                                LastQuickScanDate = av.LastQuickScanDateTimeStart,
                                SignaturesUpToDate = av.SignatureUpToDate,
                                SignatureAgeDays = av.SignatureAge
                            });
                        }
                    }
                }
                else
                {
                    Instance.Warning($"Antivirus query failed: {response.StatusCode}");
                }

                Instance.Info($"[CONFIGMGR] Retrieved Antivirus status for {statuses.Count} devices");
                return statuses;
            }
            catch (Exception ex)
            {
                Instance.Warning($"Failed to get Antivirus status via REST: {ex.Message}");
                return new List<AntivirusStatus>();
            }
        }

        private async Task<List<AntivirusStatus>> GetAntivirusStatusViaWmiAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var statuses = new List<AntivirusStatus>();
                    var scope = new ManagementScope($"\\\\{_siteServer}\\root\\sms\\site_{_siteCode}");
                    scope.Connect();

                    var query = new SelectQuery("SMS_G_System_AntimalwareHealthStatus");
                    var searcher = new ManagementObjectSearcher(scope, query);

                    foreach (ManagementObject obj in searcher.Get())
                    {
                        statuses.Add(new AntivirusStatus
                        {
                            ResourceId = Convert.ToInt32(obj["ResourceID"]),
                            ProtectionEnabled = obj["ProtectionEnabled"] != null && Convert.ToBoolean(obj["ProtectionEnabled"]),
                            RealTimeProtectionEnabled = obj["RealTimeProtectionEnabled"] != null && Convert.ToBoolean(obj["RealTimeProtectionEnabled"]),
                            AntispywareEnabled = obj["AntispywareEnabled"] != null && Convert.ToBoolean(obj["AntispywareEnabled"]),
                            LastQuickScanDate = obj["LastQuickScanDateTimeStart"] != null 
                                ? ManagementDateTimeConverter.ToDateTime(obj["LastQuickScanDateTimeStart"].ToString()) 
                                : null,
                            SignaturesUpToDate = obj["SignatureUpToDate"] != null && Convert.ToBoolean(obj["SignatureUpToDate"]),
                            SignatureAgeDays = obj["SignatureAge"] != null ? Convert.ToInt32(obj["SignatureAge"]) : null
                        });
                    }

                    return statuses;
                }
                catch (Exception ex)
                {
                    Instance.Warning($"Failed to get Antivirus status via WMI: {ex.Message}");
                    return new List<AntivirusStatus>();
                }
            });
        }

        /// <summary>
        /// Get TPM status for all devices.
        /// </summary>
        public async Task<List<TpmStatus>> GetTpmStatusAsync()
        {
            if (!_isAuthenticated)
            {
                throw new InvalidOperationException("Not configured. Call ConfigureAsync first.");
            }

            Instance.LogAdminServiceQuery("TPM Status", "SMS_G_System_TPM - Trusted Platform Module status");

            if (_useWmiFallback)
            {
                return await GetTpmStatusViaWmiAsync();
            }
            else
            {
                return await GetTpmStatusViaRestApiAsync();
            }
        }

        private async Task<List<TpmStatus>> GetTpmStatusViaRestApiAsync()
        {
            try
            {
                var query = $"{_adminServiceUrl}/wmi/SMS_G_System_TPM?$select=ResourceID,IsEnabled_InitialValue,IsActivated_InitialValue,IsOwned_InitialValue,SpecVersion";
                var response = await _httpClient.GetAsync(query);

                var statuses = new List<TpmStatus>();

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<TpmResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result?.Value != null)
                    {
                        foreach (var tpm in result.Value)
                        {
                            statuses.Add(new TpmStatus
                            {
                                ResourceId = tpm.ResourceID,
                                IsPresent = true, // If we have a record, TPM is present
                                IsEnabled = tpm.IsEnabled_InitialValue,
                                IsActivated = tpm.IsActivated_InitialValue,
                                IsOwned = tpm.IsOwned_InitialValue,
                                SpecVersion = tpm.SpecVersion
                            });
                        }
                    }
                }
                else
                {
                    Instance.Warning($"TPM query failed: {response.StatusCode}");
                }

                Instance.Info($"[CONFIGMGR] Retrieved TPM status for {statuses.Count} devices");
                return statuses;
            }
            catch (Exception ex)
            {
                Instance.Warning($"Failed to get TPM status via REST: {ex.Message}");
                return new List<TpmStatus>();
            }
        }

        private async Task<List<TpmStatus>> GetTpmStatusViaWmiAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var statuses = new List<TpmStatus>();
                    var scope = new ManagementScope($"\\\\{_siteServer}\\root\\sms\\site_{_siteCode}");
                    scope.Connect();

                    var query = new SelectQuery("SMS_G_System_TPM");
                    var searcher = new ManagementObjectSearcher(scope, query);

                    foreach (ManagementObject obj in searcher.Get())
                    {
                        statuses.Add(new TpmStatus
                        {
                            ResourceId = Convert.ToInt32(obj["ResourceID"]),
                            IsPresent = true,
                            IsEnabled = obj["IsEnabled_InitialValue"] != null && Convert.ToBoolean(obj["IsEnabled_InitialValue"]),
                            IsActivated = obj["IsActivated_InitialValue"] != null && Convert.ToBoolean(obj["IsActivated_InitialValue"]),
                            IsOwned = obj["IsOwned_InitialValue"] != null && Convert.ToBoolean(obj["IsOwned_InitialValue"]),
                            SpecVersion = obj["SpecVersion"]?.ToString()
                        });
                    }

                    return statuses;
                }
                catch (Exception ex)
                {
                    Instance.Warning($"Failed to get TPM status via WMI: {ex.Message}");
                    return new List<TpmStatus>();
                }
            });
        }

        /// <summary>
        /// Get detailed OS information for all devices.
        /// </summary>
        public async Task<List<OSDetails>> GetOSDetailsAsync()
        {
            if (!_isAuthenticated)
            {
                throw new InvalidOperationException("Not configured. Call ConfigureAsync first.");
            }

            Instance.LogAdminServiceQuery("OS Details", "SMS_G_System_OPERATING_SYSTEM - Detailed OS version info");

            if (_useWmiFallback)
            {
                return await GetOSDetailsViaWmiAsync();
            }
            else
            {
                return await GetOSDetailsViaRestApiAsync();
            }
        }

        private async Task<List<OSDetails>> GetOSDetailsViaRestApiAsync()
        {
            try
            {
                var query = $"{_adminServiceUrl}/wmi/SMS_G_System_OPERATING_SYSTEM?$select=ResourceID,Caption,Version,BuildNumber,OSArchitecture";
                var response = await _httpClient.GetAsync(query);

                var statuses = new List<OSDetails>();

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<OSDetailsResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result?.Value != null)
                    {
                        foreach (var os in result.Value)
                        {
                            statuses.Add(new OSDetails
                            {
                                ResourceId = os.ResourceID,
                                Caption = os.Caption,
                                Version = os.Version,
                                BuildNumber = os.BuildNumber,
                                Architecture = os.OSArchitecture
                            });
                        }
                    }
                }
                else
                {
                    Instance.Warning($"OS Details query failed: {response.StatusCode}");
                }

                Instance.Info($"[CONFIGMGR] Retrieved OS details for {statuses.Count} devices");
                return statuses;
            }
            catch (Exception ex)
            {
                Instance.Warning($"Failed to get OS details via REST: {ex.Message}");
                return new List<OSDetails>();
            }
        }

        private async Task<List<OSDetails>> GetOSDetailsViaWmiAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var statuses = new List<OSDetails>();
                    var scope = new ManagementScope($"\\\\{_siteServer}\\root\\sms\\site_{_siteCode}");
                    scope.Connect();

                    var query = new SelectQuery("SMS_G_System_OPERATING_SYSTEM");
                    var searcher = new ManagementObjectSearcher(scope, query);

                    foreach (ManagementObject obj in searcher.Get())
                    {
                        statuses.Add(new OSDetails
                        {
                            ResourceId = Convert.ToInt32(obj["ResourceID"]),
                            Caption = obj["Caption"]?.ToString(),
                            Version = obj["Version"]?.ToString(),
                            BuildNumber = obj["BuildNumber"]?.ToString(),
                            Architecture = obj["OSArchitecture"]?.ToString()
                        });
                    }

                    return statuses;
                }
                catch (Exception ex)
                {
                    Instance.Warning($"Failed to get OS details via WMI: {ex.Message}");
                    return new List<OSDetails>();
                }
            });
        }

        /// <summary>
        /// Get combined security inventory for all devices (for Enrollment Simulator).
        /// This combines BitLocker, Firewall, AV, TPM, and OS data into a single view.
        /// </summary>
        public async Task<List<Models.DeviceSecurityStatus>> GetDeviceSecurityInventoryAsync()
        {
            if (!_isAuthenticated)
            {
                throw new InvalidOperationException("Not configured. Call ConfigureAsync first.");
            }

            Instance.Info("[CONFIGMGR] Gathering comprehensive security inventory for enrollment simulation...");

            // Gather all data in parallel
            var devicesTask = GetWindows1011DevicesAsync();
            var bitlockerTask = GetBitLockerStatusAsync();
            var firewallTask = GetFirewallStatusAsync();
            var antivirusTask = GetAntivirusStatusAsync();
            var tpmTask = GetTpmStatusAsync();
            var osTask = GetOSDetailsAsync();
            var healthTask = GetClientHealthMetricsAsync();

            await Task.WhenAll(devicesTask, bitlockerTask, firewallTask, antivirusTask, tpmTask, osTask, healthTask);

            var devices = await devicesTask;
            var bitlocker = (await bitlockerTask).ToDictionary(b => b.ResourceId, b => b);
            var firewall = (await firewallTask).ToDictionary(f => f.ResourceId, f => f);
            var antivirus = (await antivirusTask).ToDictionary(a => a.ResourceId, a => a);
            var tpm = (await tpmTask).ToDictionary(t => t.ResourceId, t => t);
            var os = (await osTask).ToDictionary(o => o.ResourceId, o => o);
            var health = (await healthTask).ToDictionary(h => h.ResourceId, h => h);

            var results = new List<Models.DeviceSecurityStatus>();

            foreach (var device in devices)
            {
                var status = new Models.DeviceSecurityStatus
                {
                    ResourceId = device.ResourceId,
                    DeviceName = device.Name,
                    IsCoManaged = device.IsCoManaged,
                    OperatingSystem = device.OperatingSystem
                };

                // BitLocker
                if (bitlocker.TryGetValue(device.ResourceId, out var bl))
                {
                    status.BitLockerEnabled = bl.IsProtected;
                    status.BitLockerProtectionStatus = bl.ProtectionStatus;
                    status.EncryptionMethod = bl.EncryptionMethod;
                }

                // Firewall
                if (firewall.TryGetValue(device.ResourceId, out var fw))
                {
                    status.FirewallEnabled = fw.IsEnabled;
                }

                // Antivirus
                if (antivirus.TryGetValue(device.ResourceId, out var av))
                {
                    status.DefenderEnabled = av.ProtectionEnabled;
                    status.RealTimeProtectionEnabled = av.RealTimeProtectionEnabled;
                    status.SignaturesUpToDate = av.SignaturesUpToDate;
                    status.SignatureAgeDays = av.SignatureAgeDays;
                    status.LastScanDate = av.LastQuickScanDate;
                }

                // TPM
                if (tpm.TryGetValue(device.ResourceId, out var tp))
                {
                    status.TpmPresent = tp.IsPresent;
                    status.TpmEnabled = tp.IsEnabled;
                    status.TpmActivated = tp.IsActivated;
                    status.TpmVersion = tp.SpecVersion;
                }

                // OS Details
                if (os.TryGetValue(device.ResourceId, out var osInfo))
                {
                    status.OSVersion = osInfo.Version;
                    status.OSBuild = osInfo.BuildNumber;
                }

                // Health / Last Scan
                if (health.TryGetValue(device.ResourceId, out var h))
                {
                    status.LastHardwareScan = h.LastHardwareScan;
                }

                results.Add(status);
            }

            Instance.Info($"[CONFIGMGR] Compiled security inventory for {results.Count} devices");
            return results;
        }

        #endregion

        public bool IsConfigured => _isAuthenticated && !string.IsNullOrEmpty(_adminServiceUrl);
    }

    // Data models for ConfigMgr Admin Service responses
    public class ConfigMgrResponse
    {
        public List<ConfigMgrSystemResource> Value { get; set; } = new List<ConfigMgrSystemResource>();
    }
    
    public class ConfigMgrSiteResponse
    {
        public List<ConfigMgrSiteResource> Value { get; set; } = new List<ConfigMgrSiteResource>();
    }
    
    public class ConfigMgrSiteResource
    {
        public string? SiteCode { get; set; }
        public string? SiteName { get; set; }
    }
    
    public class ConfigMgrClientResponse
    {
        public List<ConfigMgrClientResource> Value { get; set; } = new List<ConfigMgrClientResource>();
    }
    
    public class ConfigMgrClientResource
    {
        public int ResourceID { get; set; }
        public int CoManagementFlags { get; set; }
    }

    public class ConfigMgrApplicationResponse
    {
        public List<ConfigMgrApplicationResource> Value { get; set; } = new List<ConfigMgrApplicationResource>();
    }

    public class ConfigMgrHardwareResponse
    {
        public List<ConfigMgrHardwareResource> Value { get; set; } = new List<ConfigMgrHardwareResource>();
    }

    public class ConfigMgrUpdateComplianceResponse
    {
        public List<ConfigMgrUpdateComplianceResource> Value { get; set; } = new List<ConfigMgrUpdateComplianceResource>();
    }

    public class ConfigMgrCollectionMembershipResponse
    {
        public List<ConfigMgrCollectionMembershipResource> Value { get; set; } = new List<ConfigMgrCollectionMembershipResource>();
    }

    public class ConfigMgrClientHealthResponse
    {
        public List<ConfigMgrClientHealthResource> Value { get; set; } = new List<ConfigMgrClientHealthResource>();
    }

    public class ConfigMgrSystemResource
    {
        public int ResourceId { get; set; }
        public string? Name { get; set; }
        public string? OperatingSystemNameandVersion { get; set; }
        public DateTime? LastActiveTime { get; set; }
        public string? ClientVersion { get; set; }
        public string? ResourceDomainORWorkgroup { get; set; }
        // Note: SMS_R_System doesn't have CoManagementFlags
        // Use SMS_Client for co-management details
    }

    public class ConfigMgrApplicationResource
    {
        public string? LocalizedDisplayName { get; set; }
        public string? SoftwareVersion { get; set; }
        public int NumberOfDeploymentTypes { get; set; }
        public bool IsDeployed { get; set; }
        public bool IsSuperseded { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateLastModified { get; set; }
    }

    public class ConfigMgrHardwareResource
    {
        public int ResourceID { get; set; }
        public string? Manufacturer { get; set; }
        public string? Model { get; set; }
        public string? SystemType { get; set; }
    }

    public class ConfigMgrUpdateComplianceResource
    {
        public int ResourceID { get; set; }
        public int Status { get; set; }
        public DateTime? LastStatusCheckTime { get; set; }
    }

    public class ConfigMgrCollectionMembershipResource
    {
        public string? CollectionID { get; set; }
    }

    public class ConfigMgrClientHealthResource
    {
        public int ResourceID { get; set; }
        public int ClientActiveStatus { get; set; }
        public DateTime? LastActiveTime { get; set; }
        public DateTime? LastPolicyRequest { get; set; }
        public DateTime? LastDDR { get; set; }
        public DateTime? LastHardwareScan { get; set; }
        public DateTime? LastSoftwareScan { get; set; }
    }

    public class ConfigMgrDevice
    {
        public int ResourceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string OperatingSystem { get; set; } = string.Empty;
        public DateTime? LastActiveTime { get; set; }
        public string? ClientVersion { get; set; }
        public bool IsCoManaged { get; set; }
        public int CoManagementFlags { get; set; }
        public string? DomainOrWorkgroup { get; set; }
    }

    public class ConfigMgrApplication
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public int DeploymentTypeCount { get; set; }
        public bool IsDeployed { get; set; }
        public bool IsSuperseded { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateLastModified { get; set; }
    }

    public class ConfigMgrHardwareInfo
    {
        public int ResourceId { get; set; }
        public string Manufacturer { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string SystemType { get; set; } = string.Empty;
    }

    public class ConfigMgrUpdateCompliance
    {
        public int ResourceId { get; set; }
        public int ComplianceStatus { get; set; }
        public DateTime? LastCheckTime { get; set; }
    }

    public class ConfigMgrCollectionMembership
    {
        public int ResourceId { get; set; }
        public string CollectionId { get; set; } = string.Empty;
    }
    
    public class CoManagementDetails
    {
        public int ResourceId { get; set; }
        public bool IsCoManaged { get; set; }
        public int CoManagementFlags { get; set; }
        public int WorkloadFlags { get; set; }
        
        public bool HasCompliancePolicies => (WorkloadFlags & 1) != 0;
        public bool HasResourceAccess => (WorkloadFlags & 2) != 0;
        public bool HasDeviceConfiguration => (WorkloadFlags & 4) != 0;
        public bool HasWindowsUpdate => (WorkloadFlags & 8) != 0;
        public bool HasEndpointProtection => (WorkloadFlags & 16) != 0;
        public bool HasClientApps => (WorkloadFlags & 32) != 0;
        public bool HasOfficeClickToRun => (WorkloadFlags & 64) != 0;
    }

    public class ConfigMgrClientHealth
    {
        public int ResourceId { get; set; }
        public int ClientActiveStatus { get; set; }
        public DateTime? LastActiveTime { get; set; }
        public DateTime? LastPolicyRequest { get; set; }
        public DateTime? LastDDR { get; set; }
        public DateTime? LastHardwareScan { get; set; }
        public DateTime? LastSoftwareScan { get; set; }
    }

    #region Security Inventory Models

    // Response classes for JSON deserialization
    public class BitLockerResponse
    {
        public List<BitLockerResource> Value { get; set; } = new();
    }

    public class BitLockerResource
    {
        public int ResourceID { get; set; }
        public string? DriveLetter { get; set; }
        public int ProtectionStatus { get; set; }
        public int ConversionStatus { get; set; }
        public string? EncryptionMethod { get; set; }
    }

    public class FirewallResponse
    {
        public List<FirewallResource> Value { get; set; } = new();
    }

    public class FirewallResource
    {
        public int ResourceID { get; set; }
        public int ProductState { get; set; }
    }

    public class AntivirusResponse
    {
        public List<AntivirusResource> Value { get; set; } = new();
    }

    public class AntivirusResource
    {
        public int ResourceID { get; set; }
        public bool ProtectionEnabled { get; set; }
        public bool RealTimeProtectionEnabled { get; set; }
        public bool AntispywareEnabled { get; set; }
        public DateTime? LastQuickScanDateTimeStart { get; set; }
        public bool SignatureUpToDate { get; set; }
        public int? SignatureAge { get; set; }
    }

    public class TpmResponse
    {
        public List<TpmResource> Value { get; set; } = new();
    }

    public class TpmResource
    {
        public int ResourceID { get; set; }
        public bool IsEnabled_InitialValue { get; set; }
        public bool IsActivated_InitialValue { get; set; }
        public bool IsOwned_InitialValue { get; set; }
        public string? SpecVersion { get; set; }
    }

    public class OSDetailsResponse
    {
        public List<OSDetailsResource> Value { get; set; } = new();
    }

    public class OSDetailsResource
    {
        public int ResourceID { get; set; }
        public string? Caption { get; set; }
        public string? Version { get; set; }
        public string? BuildNumber { get; set; }
        public string? OSArchitecture { get; set; }
    }

    // Data models for security inventory
    public class BitLockerStatus
    {
        public int ResourceId { get; set; }
        public string DriveLetter { get; set; } = "C:";
        public int ProtectionStatus { get; set; }
        public int ConversionStatus { get; set; }
        public string? EncryptionMethod { get; set; }
        public bool IsProtected { get; set; }
    }

    public class FirewallStatus
    {
        public int ResourceId { get; set; }
        public int ProductState { get; set; }
        public bool IsEnabled { get; set; }
    }

    public class AntivirusStatus
    {
        public int ResourceId { get; set; }
        public bool ProtectionEnabled { get; set; }
        public bool RealTimeProtectionEnabled { get; set; }
        public bool AntispywareEnabled { get; set; }
        public DateTime? LastQuickScanDate { get; set; }
        public bool SignaturesUpToDate { get; set; }
        public int? SignatureAgeDays { get; set; }
    }

    public class TpmStatus
    {
        public int ResourceId { get; set; }
        public bool IsPresent { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsActivated { get; set; }
        public bool IsOwned { get; set; }
        public string? SpecVersion { get; set; }
    }

    public class OSDetails
    {
        public int ResourceId { get; set; }
        public string? Caption { get; set; }
        public string? Version { get; set; }
        public string? BuildNumber { get; set; }
        public string? Architecture { get; set; }
    }

    #endregion
}
