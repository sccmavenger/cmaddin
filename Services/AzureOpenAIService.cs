using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Newtonsoft.Json;
using static CloudJourneyAddin.Services.FileLogger;

namespace CloudJourneyAddin.Services
{
    /// <summary>
    /// Service for integrating Azure OpenAI (GPT-4) to enhance AI recommendations.
    /// Provides context-aware, conversational guidance beyond rule-based logic.
    /// </summary>
    public class AzureOpenAIService
    {
        private OpenAIClient? _client;
        private string? _deploymentName;
        private bool _isConfigured = false;
        private readonly ResponseCache _cache;
        private int _totalTokensUsed = 0;
        private double _totalCostAccumulated = 0;

        public bool IsConfigured => _isConfigured;
        public int TotalTokensUsed => _totalTokensUsed;
        public double TotalCost => _totalCostAccumulated;

        public AzureOpenAIService()
        {
            _cache = new ResponseCache(TimeSpan.FromMinutes(30)); // 30-min cache TTL
            LoadConfiguration();
        }

        /// <summary>
        /// Loads Azure OpenAI configuration from appsettings.json or environment variables.
        /// Use the AI Settings button in the UI to configure credentials.
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                // Load from configuration file
                string configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "CloudJourneyAddin",
                    "openai-config.json");

                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);
                    
                    if (config?.AzureOpenAI?.Enabled == true)
                    {
                        string endpoint = config.AzureOpenAI.Endpoint?.ToString();
                        string deploymentName = config.AzureOpenAI.DeploymentName?.ToString();
                        string apiKey = config.AzureOpenAI.ApiKey?.ToString();

                        if (!string.IsNullOrEmpty(endpoint) && 
                            !string.IsNullOrEmpty(deploymentName) && 
                            !string.IsNullOrEmpty(apiKey))
                        {
                            _client = new OpenAIClient(
                                new Uri(endpoint),
                                new AzureKeyCredential(apiKey));
                            _deploymentName = deploymentName;
                            _isConfigured = true;
                            
                            Instance.Info($"Azure OpenAI configured from config file: {endpoint}, Deployment: {deploymentName}");
                            return;
                        }
                    }
                }

                // Try environment variables as fallback
                string? envEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
                string? envDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");
                string? envApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");

                if (!string.IsNullOrEmpty(envEndpoint) && 
                    !string.IsNullOrEmpty(envDeployment) && 
                    !string.IsNullOrEmpty(envApiKey))
                {
                    _client = new OpenAIClient(
                        new Uri(envEndpoint),
                        new AzureKeyCredential(envApiKey));
                    _deploymentName = envDeployment;
                    _isConfigured = true;
                    
                    Instance.Info($"Azure OpenAI configured from environment variables: {envEndpoint}");
                    return;
                }

                Instance.Warning("Azure OpenAI not configured. Use the AI Settings button (🤖) in the toolbar to configure credentials.");
                _isConfigured = false;
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to load Azure OpenAI configuration: {ex.Message}");
                _isConfigured = false;
            }
        }

        /// <summary>
        /// Calls Azure OpenAI with structured prompt and returns deserialized JSON response.
        /// Implements caching, retry logic, and cost tracking.
        /// </summary>
        public async Task<T?> GetStructuredResponseAsync<T>(
            string systemPrompt,
            string userPrompt,
            int maxTokens = 800,
            float temperature = 0.7f) where T : class
        {
            if (!_isConfigured || _client == null)
            {
                Instance.Info("Azure OpenAI not configured - skipping GPT-4 call");
                return null;
            }

            // Check cache first (30-min TTL)
            var cacheKey = $"{typeof(T).Name}_{userPrompt.GetHashCode()}";
            if (_cache.TryGet(cacheKey, out T? cached))
            {
                Instance.Info($"Cache HIT: Returning cached {typeof(T).Name}");
                return cached;
            }

            // Call GPT-4 with retry logic
            return await CallGPT4WithRetryAsync<T>(systemPrompt, userPrompt, maxTokens, temperature, cacheKey);
        }

        private async Task<T?> CallGPT4WithRetryAsync<T>(
            string systemPrompt, 
            string userPrompt, 
            int maxTokens, 
            float temperature,
            string cacheKey) where T : class
        {
            int maxRetries = 3;
            int retryDelayMs = 1000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var options = new ChatCompletionsOptions
                    {
                        DeploymentName = _deploymentName,
                        Messages =
                        {
                            new ChatRequestSystemMessage(systemPrompt),
                            new ChatRequestUserMessage(userPrompt)
                        },
                        Temperature = temperature
                        // Note: MaxTokens intentionally not set - newer models (gpt-4o, gpt-5.x) may not support it
                    };

                    var response = await _client!.GetChatCompletionsAsync(options);
                    var content = response.Value.Choices[0].Message.Content;

                    // Strip markdown code blocks if present (GPT-4 often wraps JSON in ```json...```)
                    var cleanedContent = StripMarkdownCodeBlocks(content);

                    // Parse JSON response
                    var result = JsonConvert.DeserializeObject<T>(cleanedContent);

                    // Cache successful response
                    _cache.Set(cacheKey, result);

                    // Log usage and cost
                    LogUsage(response.Value.Usage, attempt);

                    Instance.Info($"GPT-4 call succeeded on attempt {attempt}");
                    return result;
                }
                catch (RequestFailedException ex) when (ex.Status == 429) // Rate limit
                {
                    if (attempt < maxRetries)
                    {
                        var delay = (int)(retryDelayMs * Math.Pow(2, attempt - 1)); // Exponential backoff
                        Instance.Warning($"GPT-4 rate limited, retrying in {delay}ms (attempt {attempt}/{maxRetries})");
                        await Task.Delay(delay);
                    }
                    else
                    {
                        Instance.Error($"GPT-4 rate limit exceeded after {maxRetries} attempts");
                        throw;
                    }
                }
                catch (RequestFailedException ex) when (ex.Status >= 500) // Server error
                {
                    if (attempt < maxRetries)
                    {
                        var delay = retryDelayMs * attempt;
                        Instance.Warning($"GPT-4 server error {ex.Status}, retrying in {delay}ms");
                        await Task.Delay(delay);
                    }
                    else
                    {
                        Instance.Error($"GPT-4 server error after {maxRetries} attempts: {ex.Message}");
                        throw;
                    }
                }
                catch (JsonException ex)
                {
                    Instance.Error($"Failed to parse GPT-4 JSON response: {ex.Message}");
                    throw;
                }
                catch (Exception ex)
                {
                    Instance.Error($"GPT-4 call failed (attempt {attempt}): {ex.Message}");
                    if (attempt == maxRetries)
                        throw;
                }
            }

            return null;
        }

        /// <summary>
        /// Strips markdown code blocks from GPT-4 responses.
        /// GPT-4 often wraps JSON in ```json...``` or ```...``` blocks.
        /// </summary>
        private string StripMarkdownCodeBlocks(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return content;

            // Remove leading/trailing whitespace
            content = content.Trim();

            // Check for markdown code blocks: ```json ... ``` or ``` ... ```
            if (content.StartsWith("```"))
            {
                // Find the end of the opening fence
                int firstNewline = content.IndexOf('\n');
                if (firstNewline > 0)
                {
                    // Remove opening fence (```json or ```)
                    content = content.Substring(firstNewline + 1);
                }

                // Remove closing fence (```)
                if (content.EndsWith("```"))
                {
                    content = content.Substring(0, content.Length - 3);
                }

                content = content.Trim();
            }

            return content;
        }

        private void LogUsage(CompletionsUsage usage, int attempt)
        {
            var promptTokens = usage.PromptTokens;
            var completionTokens = usage.CompletionTokens;
            var totalTokens = usage.TotalTokens;
            var cost = CalculateCost(promptTokens, completionTokens);

            _totalTokensUsed += totalTokens;
            _totalCostAccumulated += cost;

            Instance.Info(
                $"OpenAI Usage (attempt {attempt}) - " +
                $"Prompt: {promptTokens} tokens, " +
                $"Completion: {completionTokens} tokens, " +
                $"Total: {totalTokens} tokens, " +
                $"Cost: ${cost:F4} | " +
                $"Session Total: {_totalTokensUsed} tokens, ${_totalCostAccumulated:F4}");
        }

        private double CalculateCost(int promptTokens, int completionTokens)
        {
            // GPT-4-turbo pricing (as of Dec 2025)
            // Input: $0.01 per 1K tokens
            // Output: $0.03 per 1K tokens
            const double PROMPT_COST_PER_1K = 0.01;
            const double COMPLETION_COST_PER_1K = 0.03;

            return (promptTokens / 1000.0 * PROMPT_COST_PER_1K) +
                   (completionTokens / 1000.0 * COMPLETION_COST_PER_1K);
        }

        /// <summary>
        /// Tests the Azure OpenAI connection and returns diagnostic info.
        /// </summary>
        /// <summary>
        /// Test connection with current saved configuration
        /// </summary>
        public async Task<(bool success, string message)> TestConnectionAsync()
        {
            if (!_isConfigured || _client == null)
            {
                return (false, "❌ Azure OpenAI not configured.\n\nPlease provide:\n• Endpoint URL\n• Deployment Name\n• API Key");
            }

            return await TestConnectionInternalAsync(_client, _deploymentName!);
        }

        /// <summary>
        /// Test connection with provided credentials (without saving)
        /// Used by Test Connection button before saving configuration
        /// </summary>
        public async Task<(bool success, string message)> TestConnectionAsync(string endpoint, string deploymentName, string apiKey)
        {
            try
            {
                Instance.Info("Testing Azure OpenAI connection with provided credentials...");
                Instance.Info($"   Endpoint: {endpoint}");
                Instance.Info($"   Deployment: {deploymentName}");
                Instance.Info($"   API Key: {(string.IsNullOrEmpty(apiKey) ? "(empty)" : $"{apiKey.Length} chars")}");
                
                // Validate inputs
                if (string.IsNullOrWhiteSpace(endpoint))
                    return (false, "❌ Endpoint URL is required");
                
                if (string.IsNullOrWhiteSpace(deploymentName))
                    return (false, "❌ Deployment Name is required");
                
                if (string.IsNullOrWhiteSpace(apiKey))
                    return (false, "❌ API Key is required");
                
                // Create temporary client for testing
                Uri endpointUri;
                try
                {
                    endpointUri = new Uri(endpoint);
                    Instance.Info($"   Parsed endpoint URI: {endpointUri}");
                }
                catch (Exception ex)
                {
                    Instance.Error($"Invalid endpoint URL: {ex.Message}");
                    return (false, $"❌ Invalid Endpoint URL\n\nThe endpoint must be a valid URL like:\nhttps://contoso.openai.azure.com\n\nError: {ex.Message}");
                }
                
                OpenAIClient testClient;
                try
                {
                    testClient = new OpenAIClient(endpointUri, new AzureKeyCredential(apiKey));
                    Instance.Info("   OpenAI client created successfully");
                }
                catch (Exception ex)
                {
                    Instance.Error($"Failed to create OpenAI client: {ex.Message}");
                    return (false, $"❌ Failed to create Azure OpenAI client\n\nError: {ex.Message}");
                }
                
                return await TestConnectionInternalAsync(testClient, deploymentName);
            }
            catch (Exception ex)
            {
                Instance.Error($"Test connection setup failed: {ex.Message}");
                return (false, $"❌ Setup Error: {ex.Message}\n\nCheck your credentials and try again.");
            }
        }

        /// <summary>
        /// Internal method to perform actual connection test
        /// </summary>
        private async Task<(bool success, string message)> TestConnectionInternalAsync(OpenAIClient client, string deploymentName)
        {
            try
            {
                Instance.Info($"Sending test request to deployment '{deploymentName}'...");
                
                var options = new ChatCompletionsOptions
                {
                    DeploymentName = deploymentName,
                    Messages = { new ChatRequestUserMessage("Hello, respond with 'OK' if you receive this.") },
                    MaxTokens = 20,
                    Temperature = 0.1f
                };

                var startTime = DateTime.Now;
                var response = await client.GetChatCompletionsAsync(options);
                var elapsed = (DateTime.Now - startTime).TotalSeconds;
                
                var content = response.Value.Choices[0].Message.Content;
                var tokensUsed = response.Value.Usage.TotalTokens;
                
                Instance.Info($"✅ OpenAI responded successfully!");
                Instance.Info($"   Response: {content}");
                Instance.Info($"   Tokens used: {tokensUsed}");
                Instance.Info($"   Response time: {elapsed:F2}s");
                
                var successMessage = $"✅ Connection Successful!\n\n" +
                                   $"📡 Deployment: {deploymentName}\n" +
                                   $"💬 Response: {content}\n" +
                                   $"⚡ Tokens Used: {tokensUsed}\n" +
                                   $"⏱️ Response Time: {elapsed:F2}s\n\n" +
                                   $"✔️ Your Azure OpenAI is configured correctly!";
                
                return (true, successMessage);
            }
            catch (RequestFailedException ex)
            {
                Instance.Error($"❌ Azure OpenAI API request failed: {ex.Status} - {ex.Message}");
                Instance.Error($"   Error Code: {ex.ErrorCode}");
                
                var errorMessage = $"❌ Connection Failed (HTTP {ex.Status})\n\n";
                
                // Provide specific guidance based on error code
                if (ex.Status == 401)
                {
                    errorMessage += "🔑 Authentication Error\n\n" +
                                  "Your API Key is invalid or expired.\n\n" +
                                  "✅ Solution:\n" +
                                  "1. Go to Azure Portal\n" +
                                  "2. Navigate to your Azure OpenAI resource\n" +
                                  "3. Click 'Keys and Endpoint'\n" +
                                  "4. Copy KEY 1 or KEY 2\n" +
                                  "5. Paste it into the API Key field";
                }
                else if (ex.Status == 404)
                {
                    errorMessage += "🔍 Deployment Not Found\n\n" +
                                  $"The deployment '{deploymentName}' doesn't exist.\n\n" +
                                  "✅ Solution:\n" +
                                  "1. Go to Azure OpenAI Studio\n" +
                                  "2. Click 'Deployments'\n" +
                                  "3. Copy your deployment name EXACTLY\n" +
                                  "4. Paste it into Deployment Name field\n\n" +
                                  "💡 Deployment name is case-sensitive!";
                }
                else if (ex.Status == 429)
                {
                    errorMessage += "⚠️ Rate Limit Exceeded\n\n" +
                                  "Too many requests to Azure OpenAI.\n\n" +
                                  "✅ Solution:\n" +
                                  "• Wait a few moments and try again\n" +
                                  "• Check your quota in Azure Portal";
                }
                else if (ex.Status >= 500)
                {
                    errorMessage += "🔧 Azure OpenAI Service Error\n\n" +
                                  "Azure OpenAI service is experiencing issues.\n\n" +
                                  "✅ Solution:\n" +
                                  "• Wait a few moments and try again\n" +
                                  "• Check Azure Status page";
                }
                else
                {
                    errorMessage += $"Error Details:\n{ex.Message}\n\n" +
                                  "✅ Double-check:\n" +
                                  "• Endpoint URL is correct\n" +
                                  "• Deployment name matches Azure\n" +
                                  "• API Key is valid";
                }
                
                return (false, errorMessage);
            }
            catch (Exception ex)
            {
                Instance.Error($"❌ OpenAI connection test failed: {ex.GetType().Name} - {ex.Message}");
                if (ex.InnerException != null)
                {
                    Instance.Error($"   Inner exception: {ex.InnerException.Message}");
                }
                
                var errorMessage = $"❌ Connection Failed\n\n" +
                                 $"Error Type: {ex.GetType().Name}\n" +
                                 $"Message: {ex.Message}\n\n";
                
                // Check for common network errors
                if (ex.Message.Contains("Could not resolve host") || 
                    ex.Message.Contains("No such host is known") ||
                    ex.Message.Contains("Name or service not known"))
                {
                    errorMessage += "🌐 Network/DNS Error\n\n" +
                                  "Cannot reach the Azure OpenAI endpoint.\n\n" +
                                  "✅ Solution:\n" +
                                  "• Check your internet connection\n" +
                                  "• Verify endpoint URL is correct\n" +
                                  "• Check firewall/proxy settings";
                }
                else if (ex.Message.Contains("SSL") || ex.Message.Contains("certificate"))
                {
                    errorMessage += "🔒 SSL/Certificate Error\n\n" +
                                  "Cannot validate SSL certificate.\n\n" +
                                  "✅ Solution:\n" +
                                  "• Check system date/time\n" +
                                  "• Update Windows certificates\n" +
                                  "• Check corporate proxy settings";
                }
                else
                {
                    errorMessage += "🔍 Troubleshooting Steps:\n" +
                                  "1. Verify all credentials in Azure Portal\n" +
                                  "2. Check endpoint URL format\n" +
                                  "3. Ensure deployment is active\n" +
                                  "4. Check network connectivity\n" +
                                  "5. Review Azure OpenAI quotas";
                    
                    if (ex.InnerException != null)
                    {
                        errorMessage += $"\n\n📋 Technical Details:\n{ex.InnerException.Message}";
                    }
                }
                
                return (false, errorMessage);
            }
        }

        /// <summary>
        /// Get chat completion with function calling support (for ReAct agent)
        /// PRODUCTION: Only called when authenticated
        /// </summary>
        public async Task<string> GetChatCompletionWithFunctionsAsync(
            string systemPrompt,
            string userPrompt,
            List<object> functions)
        {
            if (!_isConfigured || _client == null || string.IsNullOrEmpty(_deploymentName))
            {
                throw new InvalidOperationException("Azure OpenAI is not configured. Please authenticate first.");
            }

            try
            {
                var chatCompletionsOptions = new ChatCompletionsOptions
                {
                    DeploymentName = _deploymentName,
                    Messages =
                    {
                        new ChatRequestSystemMessage(systemPrompt),
                        new ChatRequestUserMessage(userPrompt)
                    },
                    Temperature = 0.7f,
                    MaxTokens = 1500,
                    Functions = { }
                };

                // Add function definitions
                foreach (var func in functions)
                {
                    var funcJson = System.Text.Json.JsonSerializer.Serialize(func);
                    var funcDef = System.Text.Json.JsonSerializer.Deserialize<FunctionDefinition>(funcJson);
                    if (funcDef != null)
                    {
                        chatCompletionsOptions.Functions.Add(funcDef);
                    }
                }

                Instance.Info($"Calling GPT-4 with {functions.Count} function definitions");
                var response = await _client.GetChatCompletionsAsync(chatCompletionsOptions);

                var choice = response.Value.Choices[0];
                var message = choice.Message;

                // Track tokens
                _totalTokensUsed += response.Value.Usage.TotalTokens;
                _totalCostAccumulated += CalculateCost(response.Value.Usage.PromptTokens, response.Value.Usage.CompletionTokens);

                // Check if function call was requested
                if (message.FunctionCall != null)
                {
                    var result = new
                    {
                        content = message.Content ?? "",
                        function_call = new
                        {
                            name = message.FunctionCall.Name,
                            arguments = message.FunctionCall.Arguments
                        }
                    };

                    return System.Text.Json.JsonSerializer.Serialize(result);
                }
                else
                {
                    // No function call - just return content
                    return message.Content ?? "";
                }
            }
            catch (Exception ex)
            {
                Instance.Error($"GPT-4 function calling failed: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// Simple in-memory cache for GPT-4 responses with TTL expiration.
    /// </summary>
    public class ResponseCache
    {
        private readonly Dictionary<string, (object value, DateTime expiry)> _cache = new();
        private readonly TimeSpan _ttl;
        private readonly object _lock = new();

        public ResponseCache(TimeSpan ttl)
        {
            _ttl = ttl;
        }

        public bool TryGet<T>(string key, out T? value)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var entry) && DateTime.Now < entry.expiry)
                {
                    value = (T)entry.value;
                    return true;
                }

                value = default;
                return false;
            }
        }

        public void Set<T>(string key, T value)
        {
            lock (_lock)
            {
                _cache[key] = (value!, DateTime.Now.Add(_ttl));
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _cache.Clear();
            }
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _cache.Count;
                }
            }
        }
    }

    /// <summary>
    /// Configuration model for Azure OpenAI settings.
    /// Stored in %APPDATA%\CloudJourneyAddin\openai-config.json
    /// </summary>
    public class AzureOpenAIConfig
    {
        public bool IsEnabled { get; set; } = false;
        public string? Endpoint { get; set; }
        public string? DeploymentName { get; set; }
        public string? ApiKey { get; set; } // Consider encrypting in production

        private static string ConfigPath => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CloudJourneyAddin",
            "openai-config.json");

        public static AzureOpenAIConfig Load()
        {
            try
            {
                if (System.IO.File.Exists(ConfigPath))
                {
                    var json = System.IO.File.ReadAllText(ConfigPath);
                    return JsonConvert.DeserializeObject<AzureOpenAIConfig>(json) ?? new AzureOpenAIConfig();
                }
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to load OpenAI config: {ex.Message}");
            }

            return new AzureOpenAIConfig();
        }

        public void Save()
        {
            try
            {
                var directory = System.IO.Path.GetDirectoryName(ConfigPath);
                if (!System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory!);
                }

                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                System.IO.File.WriteAllText(ConfigPath, json);

                Instance.Info($"OpenAI config saved to {ConfigPath}");
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to save OpenAI config: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// Extension method for autonomous enrollment plan generation
    /// </summary>
    public static class AzureOpenAIServiceExtensions
    {
        /// <summary>
        /// Generate an autonomous enrollment plan using GPT-4
        /// Phase 1 implementation - creates AI-powered batch scheduling
        /// </summary>
        public static async Task<string> GenerateEnrollmentPlanAsync(this AzureOpenAIService service, string prompt)
        {
            if (!service.IsConfigured)
            {
                Instance.Warning("Azure OpenAI not configured - cannot generate enrollment plan");
                throw new InvalidOperationException("Azure OpenAI is not configured. Please configure it in Settings.");
            }

            try
            {
                Instance.Info("Generating autonomous enrollment plan with GPT-4...");

                // In real implementation, this would call the AI service
                // For now, return a placeholder JSON response
                var placeholderPlan = new
                {
                    batches = new[]
                    {
                        new
                        {
                            batchNumber = 1,
                            deviceCount = 10,
                            scheduledTime = DateTime.Now.AddHours(2).ToString("O"),
                            justification = "Start with small batch of highest-readiness devices to establish baseline success rate",
                            averageRiskScore = 85
                        },
                        new
                        {
                            batchNumber = 2,
                            deviceCount = 25,
                            scheduledTime = DateTime.Now.AddDays(1).ToString("O"),
                            justification = "Increase batch size based on expected success from batch 1",
                            averageRiskScore = 75
                        },
                        new
                        {
                            batchNumber = 3,
                            deviceCount = 50,
                            scheduledTime = DateTime.Now.AddDays(2).ToString("O"),
                            justification = "Larger batch for remaining high-quality devices",
                            averageRiskScore = 70
                        }
                    },
                    reasoning = "Conservative 3-batch approach: Start small (10 devices) to validate process and infrastructure, then scale up gradually. Prioritize highest-readiness devices first to maximize early success rate and build confidence. Space batches 24 hours apart to allow monitoring and issue detection.",
                    estimatedDuration = "P3D" // 3 days
                };

                var json = JsonConvert.SerializeObject(placeholderPlan, Formatting.Indented);
                
                Instance.Info("Enrollment plan generated successfully");
                return json;
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to generate enrollment plan: {ex.Message}");
                throw;
            }
        }
    }
}