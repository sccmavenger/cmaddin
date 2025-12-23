using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CloudJourneyAddin.Models;

namespace CloudJourneyAddin.Services.AgentTools
{
    /// <summary>
    /// Tool for analyzing device readiness and identifying preparation needs
    /// </summary>
    public class AnalyzeReadinessTool : AgentTool
    {
        private readonly GraphDataService _graphService;

        public AnalyzeReadinessTool(GraphDataService graphService)
        {
            _graphService = graphService;
        }

        public override string Name => "analyze_readiness";

        public override string Description =>
            "Analyze device readiness for enrollment. Returns detailed breakdown of blockers, " +
            "app compatibility issues, policy conflicts, and preparation recommendations.";

        public override Dictionary<string, object> Parameters => new()
        {
            ["device_id"] = new
            {
                type = "string",
                description = "Device ID to analyze (optional - if omitted, analyzes all devices)"
            },
            ["include_recommendations"] = new
            {
                type = "boolean",
                description = "Include preparation recommendations (default: true)"
            }
        };

        protected override string[] GetRequiredParameters() => Array.Empty<string>();

        public override async Task<AgentToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var deviceId = parameters.GetValueOrDefault("device_id")?.ToString();
                var includeRecs = parameters.GetValueOrDefault("include_recommendations") is bool b ? b : true;

                // Check if authenticated
                if (!_graphService.IsAuthenticated)
                {
                    // UNAUTHENTICATED: Return mock analysis for demo
                    var mockSummary = new
                    {
                        total_devices = 1100,
                        ready_for_enrollment = 660,
                        needs_preparation = 440,
                        by_manufacturer = new[]
                        {
                            new { manufacturer = "Dell", count = 495, avg_readiness = 78.5 },
                            new { manufacturer = "HP", count = 330, avg_readiness = 65.2 },
                            new { manufacturer = "Microsoft", count = 275, avg_readiness = 82.1 }
                        },
                        common_blockers = new[]
                        {
                            new { blocker = "Outdated drivers", count = 45 },
                            new { blocker = "Legacy applications", count = 38 },
                            new { blocker = "Policy conflicts", count = 22 }
                        },
                        note = "Mock data - authenticate to see real device analysis"
                    };

                    return new AgentToolResult
                    {
                        Success = true,
                        Data = JsonSerializer.Serialize(mockSummary, new JsonSerializerOptions { WriteIndented = true }),
                        Metadata = new Dictionary<string, object>
                        {
                            ["ready_count"] = 660,
                            ["needs_prep_count"] = 440,
                            ["mock_data"] = true
                        }
                    };
                }

                // PRODUCTION: Get real device readiness data
                var enrollment = await _graphService.GetDeviceEnrollmentAsync();
                var blockers = await _graphService.GetEnrollmentBlockersAsync();

                if (!string.IsNullOrEmpty(deviceId))
                {
                    // PHASE 1: Single device analysis with real readiness calculation
                    var readiness = await _graphService.CalculateDeviceReadinessAsync(deviceId);
                    
                    var analysis = new
                    {
                        device_id = deviceId,
                        device_name = readiness.DeviceName,
                        manufacturer = readiness.Manufacturer,
                        model = readiness.Model,
                        os_version = readiness.OSVersion,
                        readiness_score = readiness.ReadinessScore,
                        readiness_level = readiness.ReadinessLevel,
                        issues = readiness.Issues,
                        last_checked = readiness.LastChecked,
                        recommendations = includeRecs ? GenerateRecommendations(readiness) : null
                    };

                    return new AgentToolResult
                    {
                        Success = true,
                        Data = JsonSerializer.Serialize(analysis, new JsonSerializerOptions { WriteIndented = true }),
                        Metadata = new Dictionary<string, object>
                        {
                            ["readiness_score"] = readiness.ReadinessScore,
                            ["readiness_level"] = readiness.ReadinessLevel,
                            ["issue_count"] = readiness.Issues.Count
                        }
                    };
                }
                else
                {
                    // Fleet-wide analysis with real blocker data
                    int totalDevices = enrollment.ConfigMgrOnlyDevices;
                    int blockedDeviceCount = blockers.Sum(b => b.AffectedDevices);
                    int readyDevices = Math.Max(0, totalDevices - blockedDeviceCount);
                    int needsPrep = totalDevices - readyDevices;

                    var summary = new
                    {
                        total_devices = totalDevices,
                        ready_for_enrollment = readyDevices,
                        needs_preparation = needsPrep,
                        readiness_percentage = totalDevices > 0 ? (double)readyDevices / totalDevices * 100 : 0,
                        common_blockers = blockers.Select(b => new
                        {
                            blocker = b.Title,
                            count = b.AffectedDevices,
                            severity = b.Severity.ToString(),
                            recommendation = b.RemediationUrl
                        }).ToList(),
                        // Note: Manufacturer breakdown requires device-level data from Graph API
                        note = "Manufacturer breakdown requires additional Graph API integration for device details"
                    };

                    return new AgentToolResult
                    {
                        Success = true,
                        Data = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }),
                        Metadata = new Dictionary<string, object>
                        {
                            ["ready_count"] = readyDevices,
                            ["needs_prep_count"] = needsPrep,
                            ["blocker_count"] = blockers.Count
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                return new AgentToolResult
                {
                    Success = false,
                    Error = $"Failed to analyze readiness: {ex.Message}"
                };
            }
        }

        private List<string> GenerateRecommendations(DeviceReadiness readiness)
        {
            var recommendations = new List<string>();

            if (readiness.ReadinessScore < 60)
            {
                recommendations.Add("Device needs preparation before enrollment");
            }

            foreach (var issue in readiness.Issues)
            {
                if (issue.Contains("Non-compliant"))
                    recommendations.Add("Resolve compliance issues before enrolling");
                else if (issue.Contains("not encrypted"))
                    recommendations.Add("Enable BitLocker encryption");
                else if (issue.Contains("Outdated"))
                    recommendations.Add("Update Windows to latest version");
                else if (issue.Contains("not seen"))
                    recommendations.Add("Ensure device is online and connected");
                else
                    recommendations.Add($"Address: {issue}");
            }

            if (readiness.ReadinessScore >= 80)
            {
                recommendations.Add("✅ Device is ready for immediate enrollment");
            }
            else if (readiness.ReadinessScore >= 60)
            {
                recommendations.Add("Device is enrollment-ready but has minor issues to address");
            }

            return recommendations;
        }

        private List<string> GenerateRecommendations(dynamic device)
        {
            var recommendations = new List<string>();

            if (device.ReadinessScore < 60)
            {
                recommendations.Add("Device needs preparation before enrollment");
            }

            if (device.IncompatibleApps > 0)
            {
                recommendations.Add($"Review and remediate {device.IncompatibleApps} incompatible applications");
            }

            if (device.PolicyConflicts > 0)
            {
                recommendations.Add($"Resolve {device.PolicyConflicts} policy conflicts");
            }

            if (device.Blockers?.Count > 0)
            {
                recommendations.Add($"Address critical blockers: {string.Join(", ", device.Blockers.Take(2))}");
            }

            if (recommendations.Count == 0)
            {
                recommendations.Add("Device is ready for immediate enrollment");
            }

            return recommendations;
        }
    }
}
