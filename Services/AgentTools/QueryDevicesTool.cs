using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroTrustMigrationAddin.Models;

namespace ZeroTrustMigrationAddin.Services.AgentTools
{
    /// <summary>
    /// Tool for querying device inventory
    /// </summary>
    public class QueryDevicesTool : AgentTool
    {
        private readonly GraphDataService _graphService;

        public QueryDevicesTool(GraphDataService graphService)
        {
            _graphService = graphService;
        }

        public override string Name => "query_devices";

        public override string Description => 
            "Query device inventory to get list of devices available for enrollment. " +
            "Returns devices with their readiness scores, manufacturers, models, and current state.";

        public override Dictionary<string, object> Parameters => new()
        {
            ["filter"] = new
            {
                type = "string",
                description = "Filter criteria: 'all', 'ready' (score >= 60), 'needs_prep' (score < 60), or manufacturer like 'Dell'",
                @enum = new[] { "all", "ready", "needs_prep" }
            },
            ["limit"] = new
            {
                type = "integer",
                description = "Maximum number of devices to return (default: 100)",
                minimum = 1,
                maximum = 1000
            }
        };

        protected override string[] GetRequiredParameters() => new[] { "filter" };

        public override async Task<AgentToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var filter = parameters.GetValueOrDefault("filter")?.ToString() ?? "all";
                var limit = parameters.GetValueOrDefault("limit") is int l ? l : 100;

                // Check if authenticated
                if (!_graphService.IsAuthenticated)
                {
                    // UNAUTHENTICATED: Return mock data for demo
                    var mockSummary = new
                    {
                        total_devices = 1100,
                        intune_enrolled = 0,
                        ready_for_enrollment = 660, // 60% estimate
                        needs_preparation = 440,
                        filter_applied = filter,
                        note = "Mock data - authenticate to see real devices"
                    };

                    return new AgentToolResult
                    {
                        Success = true,
                        Data = JsonSerializer.Serialize(mockSummary, new JsonSerializerOptions { WriteIndented = true }),
                        Metadata = new Dictionary<string, object>
                        {
                            ["device_count"] = 1100,
                            ["filter"] = filter,
                            ["mock_data"] = true
                        }
                    };
                }

                // PRODUCTION: Get real device data
                var enrollment = await _graphService.GetDeviceEnrollmentAsync();
                
                // Calculate readiness based on blockers (devices without blockers are ready)
                var blockers = await _graphService.GetEnrollmentBlockersAsync();
                int blockedDeviceCount = blockers.Sum(b => b.AffectedDevices);
                int readyDevices = Math.Max(0, enrollment.ConfigMgrOnlyDevices - blockedDeviceCount);
                int needsPrep = enrollment.ConfigMgrOnlyDevices - readyDevices;

                // Apply filter
                int filteredCount = filter switch
                {
                    "ready" => readyDevices,
                    "needs_prep" => needsPrep,
                    _ => enrollment.ConfigMgrOnlyDevices
                };

                var summary = new
                {
                    total_devices = enrollment.ConfigMgrOnlyDevices,
                    intune_enrolled = enrollment.IntuneEnrolledDevices,
                    ready_for_enrollment = readyDevices,
                    needs_preparation = needsPrep,
                    filter_applied = filter,
                    filtered_count = Math.Min(filteredCount, limit),
                    blocker_count = blockers.Count
                };

                return new AgentToolResult
                {
                    Success = true,
                    Data = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }),
                    Metadata = new Dictionary<string, object>
                    {
                        ["device_count"] = enrollment.ConfigMgrOnlyDevices,
                        ["filter"] = filter,
                        ["ready_count"] = readyDevices,
                        ["blocked_count"] = blockedDeviceCount
                    }
                };
            }
            catch (Exception ex)
            {
                return new AgentToolResult
                {
                    Success = false,
                    Error = $"Failed to query devices: {ex.Message}"
                };
            }
        }
    }
}
