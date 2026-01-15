using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroTrustMigrationAddin.Models;

namespace ZeroTrustMigrationAddin.Services.AgentTools
{
    /// <summary>
    /// Tool for enrolling devices in batches
    /// </summary>
    public class EnrollDevicesTool : AgentTool
    {
        private readonly GraphDataService _graphService;

        public EnrollDevicesTool(GraphDataService graphService)
        {
            _graphService = graphService;
        }

        public override string Name => "enroll_devices";

        public override string Description =>
            "Enroll a batch of devices into Intune. Returns enrollment results with success/failure status.";

        public override Dictionary<string, object> Parameters => new()
        {
            ["device_ids"] = new
            {
                type = "array",
                description = "Array of device IDs to enroll",
                items = new { type = "string" }
            },
            ["batch_name"] = new
            {
                type = "string",
                description = "Name for this enrollment batch (for tracking)"
            },
            ["priority"] = new
            {
                type = "string",
                description = "Enrollment priority level",
                @enum = new[] { "low", "normal", "high" }
            }
        };

        protected override string[] GetRequiredParameters() => new[] { "device_ids" };

        public override async Task<AgentToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var deviceIdsObj = parameters.GetValueOrDefault("device_ids");
                var deviceIds = deviceIdsObj is JsonElement element && element.ValueKind == JsonValueKind.Array
                    ? element.EnumerateArray().Select(e => e.GetString()).Where(s => s != null).Cast<string>().ToList()
                    : new List<string>();

                var batchName = parameters.GetValueOrDefault("batch_name")?.ToString() ?? $"Batch-{DateTime.Now:yyyyMMddHHmmss}";
                var priority = parameters.GetValueOrDefault("priority")?.ToString() ?? "normal";

                // Check if authenticated
                if (!_graphService.IsAuthenticated)
                {
                    // UNAUTHENTICATED: Simulate enrollment for demo
                    var mockResults = new List<object>();
                    var mockSuccessCount = (int)(deviceIds.Count * 0.85); // 85% success rate
                    
                    for (int i = 0; i < deviceIds.Count; i++)
                    {
                        var success = i < mockSuccessCount;
                        mockResults.Add(new
                        {
                            device_id = deviceIds[i],
                            status = success ? "success" : "failed",
                            error = success ? null : "Mock failure - authenticate to perform real enrollment",
                            enrolled_at = success ? DateTime.UtcNow : (DateTime?)null
                        });
                    }

                    var mockSummary = new
                    {
                        batch_name = batchName,
                        total_devices = deviceIds.Count,
                        successful = mockSuccessCount,
                        failed = deviceIds.Count - mockSuccessCount,
                        success_rate = 85.0,
                        results = mockResults,
                        note = "Mock data - authenticate to perform real enrollments"
                    };

                    return new AgentToolResult
                    {
                        Success = true,
                        Data = JsonSerializer.Serialize(mockSummary, new JsonSerializerOptions { WriteIndented = true }),
                        Metadata = new Dictionary<string, object>
                        {
                            ["success_count"] = mockSuccessCount,
                            ["fail_count"] = deviceIds.Count - mockSuccessCount,
                            ["mock_data"] = true
                        }
                    };
                }

                // PRODUCTION: Real enrollment via Graph API
                var results = new List<object>();
                var successCount = 0;
                var failCount = 0;

                foreach (var deviceId in deviceIds)
                {
                    try
                    {
                        // PHASE 1: Real enrollment implementation
                        // Get device details first to have proper name
                        var deviceDetails = await _graphService.GetDeviceByIdAsync(deviceId);
                        var deviceName = deviceDetails?.DeviceName ?? $"Device-{deviceId.Substring(Math.Max(0, deviceId.Length - 4))}";
                        
                        // Perform real enrollment
                        var enrollResult = await _graphService.EnrollDeviceAsync(deviceId, deviceName);

                        if (enrollResult.Success)
                        {
                            successCount++;
                            results.Add(new
                            {
                                device_id = deviceId,
                                device_name = deviceName,
                                status = "success",
                                enrolled_at = enrollResult.EnrolledAt,
                                message = enrollResult.Message
                            });
                        }
                        else
                        {
                            failCount++;
                            results.Add(new
                            {
                                device_id = deviceId,
                                device_name = deviceName,
                                status = "failed",
                                error = enrollResult.ErrorMessage
                            });
                        }

                        await Task.Delay(100); // Small delay between enrollments
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        results.Add(new
                        {
                            device_id = deviceId,
                            status = "failed",
                            error = ex.Message
                        });
                    }
                }

                var summary = new
                {
                    batch_name = batchName,
                    total_devices = deviceIds.Count,
                    successful = successCount,
                    failed = failCount,
                    success_rate = deviceIds.Count > 0 ? (double)successCount / deviceIds.Count * 100 : 0,
                    results = results
                };

                return new AgentToolResult
                {
                    Success = true,
                    Data = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }),
                    Metadata = new Dictionary<string, object>
                    {
                        ["success_count"] = successCount,
                        ["fail_count"] = failCount,
                        ["success_rate"] = summary.success_rate
                    }
                };
            }
            catch (Exception ex)
            {
                return new AgentToolResult
                {
                    Success = false,
                    Error = $"Failed to enroll devices: {ex.Message}"
                };
            }
        }
    }
}
