using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Octokit;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Services
{
    /// <summary>
    /// Service for capturing feedback and creating GitHub Issues.
    /// Uses GitHub OAuth Device Flow for user authentication.
    /// </summary>
    public class FeedbackService
    {
        private const string RepoOwner = "sccmavenger";
        private const string RepoName = "cmaddin";
        
        // GitHub OAuth App - Device Flow
        // Create your own at: https://github.com/settings/applications/new
        // Callback URL: Leave empty for Device Flow
        // Required scope: public_repo
        private const string GitHubClientId = "Ov23liLYexklh7vAjuO9";
        
        private GitHubClient? _authenticatedClient;
        private string? _accessToken;
        private string? _authenticatedUser;
        
        private readonly string _tokenPath;
        
        public bool IsAuthenticated => _authenticatedClient != null && !string.IsNullOrEmpty(_accessToken);
        public string? AuthenticatedUser => _authenticatedUser;

        public FeedbackService()
        {
            _tokenPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ZeroTrustMigrationAddin",
                "github-feedback-token.json");
            
            LoadSavedToken();
        }

        private void LoadSavedToken()
        {
            try
            {
                if (File.Exists(_tokenPath))
                {
                    var json = File.ReadAllText(_tokenPath);
                    var tokenData = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    
                    if (tokenData != null && 
                        tokenData.TryGetValue("access_token", out var token) && 
                        tokenData.TryGetValue("username", out var username))
                    {
                        _accessToken = token;
                        _authenticatedUser = username;
                        
                        _authenticatedClient = new GitHubClient(new ProductHeaderValue("ZeroTrustMigrationAddin-Feedback"));
                        _authenticatedClient.Credentials = new Credentials(_accessToken);
                        
                        Instance.Info($"[FEEDBACK] Loaded saved GitHub token for user: {_authenticatedUser}");
                    }
                }
            }
            catch (Exception ex)
            {
                Instance.Warning($"[FEEDBACK] Failed to load saved token: {ex.Message}");
            }
        }

        private void SaveToken()
        {
            try
            {
                var dir = Path.GetDirectoryName(_tokenPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var tokenData = new Dictionary<string, string>
                {
                    ["access_token"] = _accessToken ?? "",
                    ["username"] = _authenticatedUser ?? ""
                };

                File.WriteAllText(_tokenPath, JsonSerializer.Serialize(tokenData, new JsonSerializerOptions { WriteIndented = true }));
                Instance.Info("[FEEDBACK] GitHub token saved");
            }
            catch (Exception ex)
            {
                Instance.Warning($"[FEEDBACK] Failed to save token: {ex.Message}");
            }
        }

        /// <summary>
        /// Authenticate user via GitHub Device Flow - opens browser for authorization
        /// </summary>
        public async Task<(bool success, string message, string? userCode)> StartAuthenticationAsync()
        {
            try
            {
                Instance.Info("[FEEDBACK] Starting GitHub Device Flow authentication...");

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                
                var deviceCodeRequest = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", GitHubClientId),
                    new KeyValuePair<string, string>("scope", "public_repo")
                });

                var deviceResponse = await httpClient.PostAsync("https://github.com/login/device/code", deviceCodeRequest);

                if (!deviceResponse.IsSuccessStatusCode)
                {
                    var errorContent = await deviceResponse.Content.ReadAsStringAsync();
                    Instance.Error($"[FEEDBACK] Device code request failed: {errorContent}");
                    return (false, $"Failed to start authentication: {deviceResponse.StatusCode}", null);
                }

                var deviceContent = await deviceResponse.Content.ReadAsStringAsync();
                Instance.Info($"[FEEDBACK] Device response: {deviceContent}");
                
                var deviceData = JsonSerializer.Deserialize<JsonElement>(deviceContent);

                var deviceCode = deviceData.GetProperty("device_code").GetString();
                var userCode = deviceData.GetProperty("user_code").GetString();
                var verificationUri = deviceData.GetProperty("verification_uri").GetString();
                var interval = deviceData.TryGetProperty("interval", out var intervalProp) ? intervalProp.GetInt32() : 5;
                var expiresIn = deviceData.TryGetProperty("expires_in", out var expiresProp) ? expiresProp.GetInt32() : 900;

                Instance.Info($"[FEEDBACK] User code: {userCode}, Verification URI: {verificationUri}");

                // Copy code to clipboard and open browser
                if (!string.IsNullOrEmpty(userCode))
                {
                    try
                    {
                        Clipboard.SetText(userCode);
                        Instance.Info("[FEEDBACK] User code copied to clipboard");
                    }
                    catch (Exception ex)
                    {
                        Instance.Warning($"[FEEDBACK] Failed to copy to clipboard: {ex.Message}");
                    }
                }
                
                if (!string.IsNullOrEmpty(verificationUri))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = verificationUri,
                        UseShellExecute = true
                    });
                    Instance.Info("[FEEDBACK] Opened browser for verification");
                }

                // Start polling in background
                _ = PollForTokenAsync(deviceCode!, interval, expiresIn);

                return (true, $"Please enter code: {userCode}\n\nA browser window has opened. The code has been copied to your clipboard.", userCode);
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "FeedbackService.StartAuthenticationAsync");
                return (false, $"Authentication error: {ex.Message}", null);
            }
        }

        private async Task PollForTokenAsync(string deviceCode, int interval, int expiresIn)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            
            var startTime = DateTime.Now;
            var maxWaitTime = TimeSpan.FromSeconds(expiresIn);

            Instance.Info($"[FEEDBACK] Starting token polling (interval: {interval}s, expires: {expiresIn}s)");

            while (DateTime.Now - startTime < maxWaitTime)
            {
                await Task.Delay(TimeSpan.FromSeconds(interval));

                try
                {
                    var tokenRequest = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("client_id", GitHubClientId),
                        new KeyValuePair<string, string>("device_code", deviceCode),
                        new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:device_code")
                    });

                    var tokenResponse = await httpClient.PostAsync("https://github.com/login/oauth/access_token", tokenRequest);
                    var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
                    
                    Instance.Debug($"[FEEDBACK] Token poll response: {tokenContent}");
                    
                    var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenContent);

                    if (tokenData.TryGetProperty("access_token", out var accessTokenProp))
                    {
                        _accessToken = accessTokenProp.GetString();
                        _authenticatedClient = new GitHubClient(new ProductHeaderValue("ZeroTrustMigrationAddin-Feedback"));
                        _authenticatedClient.Credentials = new Credentials(_accessToken);

                        try
                        {
                            var user = await _authenticatedClient.User.Current();
                            _authenticatedUser = user.Login;
                            Instance.Info($"[FEEDBACK] Got user info: {_authenticatedUser}");
                        }
                        catch (Exception ex)
                        {
                            Instance.Warning($"[FEEDBACK] Failed to get user info: {ex.Message}");
                            _authenticatedUser = "GitHub User";
                        }

                        SaveToken();
                        Instance.Info($"[FEEDBACK] GitHub authentication successful for user: {_authenticatedUser}");
                        
                        // Notify via event
                        AuthenticationCompleted?.Invoke(this, new AuthenticationCompletedEventArgs(true, $"Signed in as {_authenticatedUser}"));
                        return;
                    }

                    if (tokenData.TryGetProperty("error", out var errorProp))
                    {
                        var error = errorProp.GetString();
                        Instance.Debug($"[FEEDBACK] Token poll error: {error}");
                        
                        if (error == "authorization_pending") continue;
                        if (error == "slow_down") { interval += 5; continue; }
                        if (error == "expired_token" || error == "access_denied")
                        {
                            Instance.Warning($"[FEEDBACK] Authentication failed: {error}");
                            AuthenticationCompleted?.Invoke(this, new AuthenticationCompletedEventArgs(false, error == "expired_token" ? "Authentication expired" : "Authorization denied"));
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Instance.Warning($"[FEEDBACK] Poll error: {ex.Message}");
                }
            }

            Instance.Warning("[FEEDBACK] Authentication timed out");
            AuthenticationCompleted?.Invoke(this, new AuthenticationCompletedEventArgs(false, "Authentication timed out"));
        }

        public event EventHandler<AuthenticationCompletedEventArgs>? AuthenticationCompleted;

        public void SignOut()
        {
            _authenticatedClient = null;
            _accessToken = null;
            _authenticatedUser = null;

            try { if (File.Exists(_tokenPath)) File.Delete(_tokenPath); } catch { }
            Instance.Info("[FEEDBACK] Signed out from GitHub");
        }

        /// <summary>
        /// Capture a screenshot of a WPF window
        /// </summary>
        public static byte[]? CaptureWindowScreenshot(Window window)
        {
            try
            {
                if (window == null) return null;

                var width = (int)window.ActualWidth;
                var height = (int)window.ActualHeight;
                if (width <= 0 || height <= 0) return null;

                var renderTarget = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                renderTarget.Render(window);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderTarget));

                using var stream = new MemoryStream();
                encoder.Save(stream);
                
                Instance.Info($"[FEEDBACK] Screenshot captured: {width}x{height}, {stream.Length} bytes");
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "CaptureWindowScreenshot");
                return null;
            }
        }

        /// <summary>
        /// Create a GitHub issue with feedback
        /// </summary>
        public async Task<(bool success, string message, string? issueUrl)> CreateFeedbackIssueAsync(
            FeedbackType feedbackType, string title, string description,
            byte[]? screenshot = null, bool includeSystemInfo = true)
        {
            if (_authenticatedClient == null)
                return (false, "Not authenticated. Please sign in with GitHub first.", null);

            try
            {
                Instance.Info($"[FEEDBACK] Creating {feedbackType} issue: {title}");

                var bodyBuilder = new StringBuilder();
                bodyBuilder.AppendLine($"## {GetFeedbackTypeEmoji(feedbackType)} {feedbackType}");
                bodyBuilder.AppendLine();
                bodyBuilder.AppendLine("### Description");
                bodyBuilder.AppendLine(description);
                bodyBuilder.AppendLine();

                if (includeSystemInfo)
                {
                    bodyBuilder.AppendLine("### System Information");
                    bodyBuilder.AppendLine("```");
                    bodyBuilder.AppendLine($"App Version: {GetAppVersion()}");
                    bodyBuilder.AppendLine($"OS: {Environment.OSVersion}");
                    bodyBuilder.AppendLine($".NET Version: {Environment.Version}");
                    bodyBuilder.AppendLine($"Submitted by: @{_authenticatedUser}");
                    bodyBuilder.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                    bodyBuilder.AppendLine("```");
                    bodyBuilder.AppendLine();
                }

                if (screenshot != null && screenshot.Length > 0)
                {
                    bodyBuilder.AppendLine("### Screenshot");
                    bodyBuilder.AppendLine("> 📸 A screenshot was captured and copied to your clipboard. Edit this issue and paste (Ctrl+V) to attach it.");
                    bodyBuilder.AppendLine();
                }

                bodyBuilder.AppendLine("---");
                bodyBuilder.AppendLine("*Submitted via Zero Trust Migration Journey Dashboard*");

                var newIssue = new NewIssue($"[{feedbackType}] {title}") { Body = bodyBuilder.ToString() };
                
                // Try to add labels (may fail if labels don't exist)
                try
                {
                    foreach (var label in GetLabelsForFeedbackType(feedbackType))
                        newIssue.Labels.Add(label);
                }
                catch (Exception ex)
                {
                    Instance.Warning($"[FEEDBACK] Could not add labels: {ex.Message}");
                }

                var issue = await _authenticatedClient.Issue.Create(RepoOwner, RepoName, newIssue);
                Instance.Info($"[FEEDBACK] Issue created: #{issue.Number} - {issue.HtmlUrl}");

                // Copy screenshot to clipboard if available
                if (screenshot != null && screenshot.Length > 0)
                {
                    try
                    {
                        using var ms = new MemoryStream(screenshot);
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        Clipboard.SetImage(bitmap);
                        Instance.Info("[FEEDBACK] Screenshot copied to clipboard for pasting into issue");
                    }
                    catch (Exception ex)
                    {
                        Instance.Warning($"[FEEDBACK] Failed to copy screenshot to clipboard: {ex.Message}");
                    }
                }

                return (true, $"Issue #{issue.Number} created!", issue.HtmlUrl);
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "CreateFeedbackIssueAsync");
                return (false, $"Failed to create issue: {ex.Message}", null);
            }
        }

        private static string GetFeedbackTypeEmoji(FeedbackType type) => type switch
        {
            FeedbackType.Bug => "🐛",
            FeedbackType.Feature => "💡",
            FeedbackType.Question => "❓",
            _ => "💬"
        };

        private static string[] GetLabelsForFeedbackType(FeedbackType type) => type switch
        {
            FeedbackType.Bug => new[] { "bug", "alpha-feedback" },
            FeedbackType.Feature => new[] { "enhancement", "alpha-feedback" },
            FeedbackType.Question => new[] { "question", "alpha-feedback" },
            _ => new[] { "feedback", "alpha-feedback" }
        };

        private static string GetAppVersion()
        {
            try { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown"; }
            catch { return "Unknown"; }
        }
    }

    /// <summary>
    /// Types of feedback that can be submitted
    /// </summary>
    public enum FeedbackType 
    { 
        Bug, 
        Feature, 
        Question, 
        Feedback 
    }

    /// <summary>
    /// Event args for authentication completion
    /// </summary>
    public class AuthenticationCompletedEventArgs : EventArgs
    {
        public bool Success { get; }
        public string Message { get; }
        public AuthenticationCompletedEventArgs(bool success, string message) 
        { 
            Success = success; 
            Message = message; 
        }
    }
}
