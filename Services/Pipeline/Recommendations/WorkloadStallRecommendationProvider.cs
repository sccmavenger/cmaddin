using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroTrustMigrationAddin.Services.Pipeline.Recommendations
{
    /// <summary>
    /// Generates scoped, workload-specific action recommendations for workload stalls.
    /// Produces per-workload action cards, blast radius quantification,
    /// and dependency-aware sequencing recommendations.
    /// </summary>
    public class WorkloadStallRecommendationProvider : IRecommendationProvider<WorkloadStallAssessment>
    {
        public string Name => "WorkloadStallRecommendationProvider";

        public Task<List<PipelineRecommendation>> GetRecommendationsAsync(
            WorkloadStallAssessment assessment, CancellationToken ct = default)
        {
            var recommendations = new List<PipelineRecommendation>();

            if (!assessment.IsStalled && assessment.Severity == SeverityLevel.None)
                return Task.FromResult(recommendations);

            // === Workload Trust Trough Recovery ===
            if (assessment.IsWorkloadTrustTrough)
            {
                recommendations.Add(new PipelineRecommendation
                {
                    Title = "Workload Trust Trough: Break Through the Last 2-3 Workloads",
                    Description =
                        $"You've transitioned {7 - assessment.StalledWorkloads.Count} of 7 workloads but progress " +
                        $"has stalled for {assessment.DaysSinceAnyProgress} days. " +
                        "This is the 'Workload Trust Trough' — the final workloads feel riskiest because " +
                        "they carry the most organizational blame if something breaks.",
                    Rationale =
                        "The last 2-3 workloads (typically Client Apps, Office Apps, Windows Update) " +
                        "stall because admins use ConfigMgr as a safety net. The key is decomposing " +
                        "each remaining workload into specific, bounded transitions rather than " +
                        "treating them as monolithic moves.",
                    ActionSteps = new List<string>
                    {
                        "1. Identify which remaining workloads block the most devices (see below)",
                        "2. Start with the workload that has the HIGHEST current Intune adoption %",
                        "3. Move remaining devices for that workload in batches of 50-100",
                        "4. Allow 1 week per batch for monitoring before expanding",
                        "5. Document rollback procedures for each workload transition"
                    },
                    Priority = RecommendationPriority.Critical,
                    Category = RecommendationCategory.WorkloadTransition,
                    RiskLevel = "Medium",
                    ImpactScore = 95,
                    EstimatedEffort = "2-4 weeks per remaining workload",
                    CostOfInaction =
                        $"{assessment.NearCompleteDeviceCount} devices are 1-2 workloads away from cloud-native " +
                        "but cannot remove the ConfigMgr agent until all workloads transition."
                });
            }

            // === Per-Stalled-Workload Recommendations ===
            foreach (var stalled in assessment.StalledWorkloads.OrderByDescending(s => s.DevicesBlocked))
            {
                recommendations.Add(BuildWorkloadSpecificRecommendation(stalled, assessment));
            }

            // === Last Holdout Analysis ===
            if (assessment.LastHoldouts.Any())
            {
                var topHoldout = assessment.LastHoldouts.First();
                if (topHoldout.DevicesBlockedCount > 0)
                {
                    recommendations.Add(new PipelineRecommendation
                    {
                        Title = $"Last Holdout: {topHoldout.WorkloadName} Blocks {topHoldout.DevicesBlockedCount} Devices",
                        Description =
                            $"{topHoldout.WorkloadName} is the workload preventing the most devices " +
                            $"({topHoldout.DevicesBlockedCount}) from becoming fully cloud-native. " +
                            "Transitioning this single workload would unblock agent removal.",
                        Rationale =
                            "When a single workload is the last holdout on many devices, " +
                            "it represents the highest-leverage transition opportunity. " +
                            "Moving it unblocks ConfigMgr agent removal for those devices.",
                        ActionSteps = new List<string>
                        {
                            $"1. Audit what {topHoldout.WorkloadName} manages on these {topHoldout.DevicesBlockedCount} devices",
                            "2. Verify equivalent Intune policies/profiles are configured and tested",
                            "3. Move the workload slider for a pilot batch of 20-50 devices",
                            "4. Monitor for 48-72 hours for any policy gaps",
                            "5. Expand to remaining devices in batches"
                        },
                        Priority = RecommendationPriority.High,
                        Category = RecommendationCategory.WorkloadTransition,
                        BlastRadiusDevices = topHoldout.DevicesBlockedCount,
                        RiskLevel = "Medium",
                        ImpactScore = 90,
                        EstimatedEffort = "1-2 weeks"
                    });
                }
            }

            // === Near-Complete Devices Opportunity ===
            if (assessment.NearCompleteDeviceCount > 0)
            {
                recommendations.Add(new PipelineRecommendation
                {
                    Title = $"{assessment.NearCompleteDeviceCount} Devices Are 1-2 Workloads From Cloud-Native",
                    Description =
                        $"{assessment.NearCompleteDeviceCount} devices have 5-6 of 7 workloads on Intune. " +
                        "These are the highest-leverage targets — completing the last workload(s) " +
                        "enables ConfigMgr agent removal.",
                    Rationale =
                        "Near-complete devices represent the fastest path to measurable cloud-native progress. " +
                        "Each device that completes all 7 workloads is one step closer to " +
                        "decommissioning ConfigMgr infrastructure.",
                    ActionSteps = new List<string>
                    {
                        "1. Export the list of near-complete devices (5-6 of 7 workloads on Intune)",
                        "2. Identify which 1-2 workloads remain for each device",
                        "3. Group devices by remaining workload for batch transitions",
                        "4. Transition the most common remaining workload first",
                        "5. After all 7 workloads are on Intune, validate and remove ConfigMgr agent"
                    },
                    Priority = RecommendationPriority.High,
                    Category = RecommendationCategory.WorkloadTransition,
                    TargetDeviceCount = assessment.NearCompleteDeviceCount,
                    BlastRadiusDevices = assessment.NearCompleteDeviceCount,
                    RiskLevel = "Low",
                    ImpactScore = 88,
                    EstimatedEffort = "1-2 weeks per remaining workload"
                });
            }

            // === Overall Velocity Warning ===
            if (assessment.OverallVelocity < 5 && assessment.OverallVelocity > 0)
            {
                recommendations.Add(new PipelineRecommendation
                {
                    Title = "Overall Workload Velocity: Below Target",
                    Description =
                        $"Workload transition velocity is {assessment.OverallVelocity:F1}% per week " +
                        "(target: 10-15% per week for on-time completion).",
                    Rationale =
                        "Low workload velocity indicates either resource constraints or " +
                        "hesitation about transitioning remaining workloads.",
                    ActionSteps = new List<string>
                    {
                        "1. Review if workload transitions have dedicated owners",
                        "2. Check for policy conflicts between ConfigMgr and Intune on in-progress workloads",
                        "3. Verify Intune policies are fully configured for remaining workloads",
                        "4. Set weekly targets for workload slider movements"
                    },
                    Priority = RecommendationPriority.Medium,
                    Category = RecommendationCategory.WorkloadTransition,
                    RiskLevel = "Low",
                    ImpactScore = 65,
                    EstimatedEffort = "Ongoing weekly focus"
                });
            }

            return Task.FromResult(recommendations);
        }

        private static PipelineRecommendation BuildWorkloadSpecificRecommendation(
            StalledWorkload stalled, WorkloadStallAssessment assessment)
        {
            var actionSteps = GetWorkloadSpecificActions(stalled);

            return new PipelineRecommendation
            {
                Title = $"Stalled: {stalled.Name} — {stalled.DevicesBlocked} Devices Blocked",
                Description =
                    $"{stalled.Name} has been at {stalled.CurrentAdoptionPercentage:F0}% Intune adoption " +
                    $"for {stalled.DaysSinceChange} days. {stalled.DevicesBlocked} devices still have this " +
                    $"workload on ConfigMgr. Stall type: {stalled.WhyStalled}.",
                Rationale = GetWorkloadSpecificRationale(stalled),
                ActionSteps = actionSteps,
                Priority = stalled.DaysSinceChange >= 30
                    ? RecommendationPriority.High
                    : RecommendationPriority.Medium,
                Category = RecommendationCategory.WorkloadTransition,
                BlastRadiusDevices = stalled.DevicesBlocked,
                RiskLevel = stalled.WhyStalled == StallClassification.Technical ? "Medium" : "Low",
                ImpactScore = stalled.DevicesBlocked > 50 ? 85 : 70,
                EstimatedEffort = "1-3 weeks"
            };
        }

        private static List<string> GetWorkloadSpecificActions(StalledWorkload stalled)
        {
            // Provide workload-specific guidance based on the workload name
            if (stalled.Name.Contains("Client Apps", System.StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>
                {
                    "1. Inventory all ConfigMgr application deployments targeting these devices",
                    "2. Categorize apps: already in Intune, needs packaging, needs replacement",
                    "3. Package the top 10 most-deployed apps as Win32 apps in Intune",
                    "4. Test app deployment on a pilot batch before moving the workload slider",
                    "5. Move the Client Apps workload slider for devices where all apps are available in Intune"
                };
            }

            if (stalled.Name.Contains("Windows Update", System.StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>
                {
                    "1. Verify Windows Update for Business (WUfB) policies are configured in Intune",
                    "2. Create update rings matching your current WSUS approval cadence",
                    "3. Configure feature update policies for target OS version",
                    "4. Move the Windows Update workload slider for a pilot ring of 50 devices",
                    "5. Monitor patch compliance for 2 weeks before expanding"
                };
            }

            if (stalled.Name.Contains("Office", System.StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>
                {
                    "1. Verify Microsoft 365 Apps update channel is configured in Intune",
                    "2. Confirm Office deployment configuration XML matches current settings",
                    "3. Test Office update delivery on pilot devices from CDN",
                    "4. Move the Office Click-to-Run workload slider for pilot batch",
                    "5. Verify update ring compliance after 1 update cycle"
                };
            }

            if (stalled.Name.Contains("Compliance", System.StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>
                {
                    "1. Review Intune compliance policies to ensure parity with ConfigMgr baselines",
                    "2. Test compliance evaluation on a small device group",
                    "3. Move the Compliance workload slider (lowest risk workload)",
                    "4. Verify compliance state reporting in Intune within 24 hours",
                    "5. Address any devices that fall out of compliance after transition"
                };
            }

            // Generic fallback
            return new List<string>
            {
                $"1. Verify Intune has equivalent policies configured for {stalled.Name}",
                "2. Test policies on a pilot group of 20-50 devices",
                "3. Move the workload slider for the pilot group",
                "4. Monitor for 48-72 hours for any gaps in policy application",
                "5. Expand to remaining devices in batches of 50-100"
            };
        }

        private static string GetWorkloadSpecificRationale(StalledWorkload stalled)
        {
            return stalled.WhyStalled switch
            {
                StallClassification.ConfidenceBased =>
                    $"{stalled.Name} is often the last workload to transition because admins perceive it as the " +
                    "safety net. The risk is usually lower than perceived — Intune has equivalent management capabilities.",

                StallClassification.Technical =>
                    $"{stalled.Name} has a technical blocker preventing transition: {stalled.BlockReason}. " +
                    "Resolving the technical issue should unblock progress.",

                StallClassification.ResourceConstrained =>
                    $"{stalled.Name} hasn't started transitioning, likely due to resource constraints or prioritization. " +
                    "This workload needs a dedicated owner to begin the transition.",

                _ =>
                    $"{stalled.Name} has stalled at {stalled.CurrentAdoptionPercentage:F0}% adoption. " +
                    "Resuming progress requires identifying and addressing the specific blocker."
            };
        }
    }
}
