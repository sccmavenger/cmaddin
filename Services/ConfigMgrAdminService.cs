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

namespace CloudJourneyAddin.Services
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

                    var query = new ObjectQuery("SELECT SiteCode FROM SMS_ProviderLocation WHERE ProviderForLocalSite = true");
                    var searcher = new ManagementObjectSearcher(scope, query);
                    
                    System.Diagnostics.Debug.WriteLine($"Querying for site code...");
                    var results = searcher.Get();
                    
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
                            var testQuery = new ObjectQuery("SELECT TOP 1 ResourceID FROM SMS_R_System");
                            var testSearcher = new ManagementObjectSearcher(scope, testQuery);
                            var testResults = testSearcher.Get();
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
                // Query for Windows 10/11 workstations only (not servers)
                // OData v4 syntax: use contains() function instead of SQL 'like' operator
                var query = $"{_adminServiceUrl}/wmi/SMS_R_System?$filter=" +
                    "contains(OperatingSystemNameandVersion,'Microsoft Windows NT Workstation 10') or " +
                    "contains(OperatingSystemNameandVersion,'Microsoft Windows NT Workstation 11')";

                var response = await _httpClient.GetAsync(query);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ConfigMgrResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var devices = new List<ConfigMgrDevice>();

                if (result?.Value != null)
                {
                    foreach (var device in result.Value)
                    {
                        devices.Add(new ConfigMgrDevice
                        {
                            ResourceId = device.ResourceId,
                            Name = device.Name ?? "Unknown",
                            OperatingSystem = device.OperatingSystemNameandVersion ?? "Unknown",
                            LastActiveTime = device.LastActiveTime,
                            ClientVersion = device.ClientVersion,
                            IsCoManaged = device.CoManagementFlags > 0, // Non-zero means co-managed
                            CoManagementFlags = device.CoManagementFlags
                        });
                    }
                }

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

        public bool IsConfigured => _isAuthenticated && !string.IsNullOrEmpty(_adminServiceUrl);
    }

    // Data models for ConfigMgr Admin Service responses
    public class ConfigMgrResponse
    {
        public List<ConfigMgrSystemResource> Value { get; set; } = new List<ConfigMgrSystemResource>();
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
        public int CoManagementFlags { get; set; } // 0 = not co-managed, >0 = co-managed
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
}
