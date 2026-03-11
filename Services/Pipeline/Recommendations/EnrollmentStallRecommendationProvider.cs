using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroTrustMigrationAddin.Services.Pipeline.Recommendations
{
    /// <summary>
    /// Generates scoped, bounded action recommendations for enrollment stalls.
    /// Produces Trust Reset Batch proposals, Cost of Inaction quantification,
    /// and root-cause-specific recovery actions.
    /// </summary>
    public class EnrollmentStallRecommendationProvider : IRecommendationProvider<EnrollmentStallAssessment>
    {
        public string Name => "EnrollmentStallRecommendationProvider";

        public Task<List<PipelineRecommendation>> GetRecommendationsAsync(
            EnrollmentStallAssessment assessment, CancellationToken ct = default)
        {
            var recommendations = new List<PipelineRecommendation>();

            if (!assessment.IsStalled && assessment.Severity == SeverityLevel.None)
                return Task.FromResult(recommendations);

            // === Trust Trough Recovery ===
            if (assessment.IsTrustTroughRisk)
            {
                recommendations.Add(new PipelineRecommendation
                {
                    Title = "Run Trust Reset Batch to Rebuild Momentum",
                    Description =
                        $"Migration has stalled at {assessment.CurrentEnrollmentPercentage:F0}% in the Trust Trough zone. " +
                        $"Select {assessment.TrustResetBatchSize} low-risk devices to enroll and rebuild organizational confidence.",
                    Rationale =
                        "The Trust Trough (50-60% enrollment) is the most common stall point. " +
                        "A small batch of successful enrollments resets organizational momentum. " +
                        "Target devices with excellent readiness scores for near-guaranteed success.",
                    ActionSteps = new List<string>
                    {
                        $"1. Select {assessment.TrustResetBatchSize} devices from the 'Excellent' readiness tier",
                        "2. Verify these devices have active users and are currently online",
                        "3. Create a dedicated Autopilot deployment profile for this batch",
                        "4. Enroll the batch and monitor for 48 hours",
                        "5. Share results with stakeholders to demonstrate successful migration",
                        "6. Use success as evidence to expand to next batch of 50-100 devices"
                    },
                    Priority = RecommendationPriority.Critical,
                    Category = RecommendationCategory.StallPrevention,
                    TargetDeviceCount = assessment.TrustResetBatchSize,
                    TargetDeviceNames = assessment.TrustResetCandidateDevices,
                    BlastRadiusDevices = assessment.TrustResetBatchSize,
                    RiskLevel = "Low",
                    ImpactScore = 95,
                    EstimatedEffort = "2-3 days",
                    CostOfInaction =
                        $"Every week at {assessment.CurrentEnrollmentPercentage:F0}% enrollment, " +
                        $"{assessment.DevicesWithExtendedPatchLatency} devices remain without cloud-native patch management."
                });
            }

            // === General Stall Recovery ===
            if (assessment.IsStalled && !assessment.IsTrustTroughRisk)
            {
                recommendations.Add(BuildStallRecoveryRecommendation(assessment));
            }

            // === Root-Cause Specific Recommendations ===
            switch (assessment.Classification)
            {
                case StallClassification.Technical:
                    recommendations.Add(new PipelineRecommendation
                    {
                        Title = "Resolve Technical Enrollment Blockers",
                        Description =
                            "Stall is driven by technical issues. Many remaining devices have poor health scores " +
                            "indicating hardware, software, or configuration issues blocking successful enrollment.",
                        Rationale =
                            "Technical blockers compound over time. Devices with failed enrollment attempts " +
                            "create duplicate objects and erode admin confidence.",
                        ActionSteps = new List<string>
                        {
                            "1. Review devices in 'Poor' and 'Fair' readiness tiers for common blockers",
                            "2. Check for TPM issues, outdated OS versions, or missing Azure AD hybrid join",
                            "3. Reduce ESP blocking applications to ≤3",
                            "4. Review and fix Conditional Access policies that may block enrollment",
                            "5. Clean up duplicate device objects in Intune"
                        },
                        Priority = RecommendationPriority.High,
                        Category = RecommendationCategory.DeviceEnrollment,
                        RiskLevel = "Low",
                        ImpactScore = 85,
                        EstimatedEffort = "1-2 weeks"
                    });
                    break;

                case StallClassification.ConfidenceBased:
                    recommendations.Add(new PipelineRecommendation
                    {
                        Title = "Address Organizational Confidence Gap",
                        Description =
                            $"At {assessment.CurrentEnrollmentPercentage:F0}% enrollment, the stall appears driven by " +
                            "organizational hesitation rather than technical issues. Admins may fear " +
                            "disrupting the remaining complex devices.",
                        Rationale =
                            "Confidence-based stalls are resolved through evidence, not technology. " +
                            "Demonstrating successful migrations of similar device types reduces perceived risk.",
                        ActionSteps = new List<string>
                        {
                            "1. Generate a side-by-side compliance comparison (Intune vs ConfigMgr enrolled devices)",
                            "2. Identify and communicate quick-win devices among the remaining unenrolled",
                            "3. Present enrollment success rate data to stakeholders (overall and by device type)",
                            "4. Document rollback procedures to reduce perceived risk",
                            "5. Schedule a stakeholder review meeting with success metrics"
                        },
                        Priority = RecommendationPriority.High,
                        Category = RecommendationCategory.StallPrevention,
                        RiskLevel = "Low",
                        ImpactScore = 80,
                        EstimatedEffort = "3-5 days"
                    });
                    break;

                case StallClassification.ResourceConstrained:
                    recommendations.Add(new PipelineRecommendation
                    {
                        Title = "Re-Allocate Migration Resources",
                        Description =
                            "Enrollment velocity dropped sharply from historical averages, suggesting the migration " +
                            "team has been redirected to other priorities.",
                        Rationale =
                            "Migrations that lose dedicated resources rarely self-recover. " +
                            "Re-establishing even part-time focus is usually sufficient to resume progress.",
                        ActionSteps = new List<string>
                        {
                            "1. Review team allocation — has the migration lost dedicated resources?",
                            "2. Establish a minimum weekly enrollment target (even 10-20 devices/week maintains momentum)",
                            "3. Automate what can be automated (Autopilot profiles, assignment groups)",
                            "4. Consider Microsoft FastTrack engagement for additional support",
                            "5. Schedule weekly 15-minute standup to maintain visibility"
                        },
                        Priority = RecommendationPriority.High,
                        Category = RecommendationCategory.StallPrevention,
                        RiskLevel = "Low",
                        ImpactScore = 75,
                        EstimatedEffort = "Ongoing (weekly commitment)"
                    });
                    break;
            }

            // === Cost of Inaction (always include when stalled) ===
            if (assessment.IsStalled && assessment.DevicesWithExtendedPatchLatency > 0)
            {
                recommendations.Add(new PipelineRecommendation
                {
                    Title = "Cost of Inaction: Security & Compliance Gap",
                    Description =
                        $"{assessment.DevicesWithExtendedPatchLatency} devices are not yet cloud-managed. " +
                        assessment.PatchLatencyImpact,
                    Rationale = assessment.ZeroTrustGapDescription,
                    ActionSteps = new List<string>
                    {
                        "This is not a task — it quantifies the ongoing cost of the current stall:",
                        $"• {assessment.DevicesWithExtendedPatchLatency} devices with longer patch latency",
                        "• Conditional Access cannot enforce compliance for unenrolled endpoints",
                        "• Each week of delay extends the Zero Trust gap",
                        "• Infrastructure maintenance cost continues for ConfigMgr-only devices"
                    },
                    Priority = RecommendationPriority.Medium,
                    Category = RecommendationCategory.StallPrevention,
                    BlastRadiusDevices = assessment.DevicesWithExtendedPatchLatency,
                    RiskLevel = "None",
                    ImpactScore = 70,
                    CostOfInaction =
                        $"{assessment.DevicesWithExtendedPatchLatency} devices × {assessment.StallDurationDays} days = " +
                        "cumulative Zero Trust exposure"
                });
            }

            // === Declining Velocity Warning ===
            if (!assessment.IsStalled && assessment.Severity == SeverityLevel.Low)
            {
                recommendations.Add(new PipelineRecommendation
                {
                    Title = "Enrollment Velocity Declining — Monitor Closely",
                    Description =
                        "Enrollment velocity is declining week-over-week. If this trend continues, " +
                        "a full stall is likely within 2-4 weeks.",
                    Rationale =
                        "Velocity decline is the earliest stall predictor. Addressing it now " +
                        "prevents the harder problem of restarting from zero momentum.",
                    ActionSteps = new List<string>
                    {
                        "1. Check if the enrollment team has competing priorities this sprint",
                        "2. Review if remaining devices are more complex than previously enrolled ones",
                        "3. Verify infrastructure capacity (CMG, Autopilot, ESP) for current load",
                        "4. Re-run pipeline analysis in 7 days to confirm trend direction"
                    },
                    Priority = RecommendationPriority.Medium,
                    Category = RecommendationCategory.StallPrevention,
                    RiskLevel = "Low",
                    ImpactScore = 60,
                    EstimatedEffort = "1-2 hours review"
                });
            }

            return Task.FromResult(recommendations);
        }

        private static PipelineRecommendation BuildStallRecoveryRecommendation(EnrollmentStallAssessment assessment)
        {
            return new PipelineRecommendation
            {
                Title = $"Enrollment Stalled for {assessment.StallDurationDays} Days — Restart Momentum",
                Description =
                    $"No significant enrollment activity for {assessment.StallDurationDays} days " +
                    $"at {assessment.CurrentEnrollmentPercentage:F0}% completion. " +
                    "Root cause: " + assessment.Classification.ToString(),
                Rationale =
                    "Stalls lasting more than 30 days rarely self-recover without intervention. " +
                    "The recommended approach is a small, focused batch enrollment to restart the pipeline.",
                ActionSteps = new List<string>
                {
                    $"1. Select {assessment.TrustResetBatchSize} devices with highest readiness scores",
                    "2. Validate these devices are active and have recent policy check-ins",
                    "3. Enroll the batch within the next 5 business days",
                    "4. Monitor success rate for 48 hours post-enrollment",
                    "5. If success rate >90%, expand to next batch within 1 week"
                },
                Priority = assessment.Severity >= SeverityLevel.High
                    ? RecommendationPriority.Critical
                    : RecommendationPriority.High,
                Category = RecommendationCategory.StallPrevention,
                TargetDeviceCount = assessment.TrustResetBatchSize,
                BlastRadiusDevices = assessment.TrustResetBatchSize,
                RiskLevel = "Low",
                ImpactScore = 90,
                EstimatedEffort = "3-5 days",
                CostOfInaction =
                    $"Currently {assessment.DevicesWithExtendedPatchLatency} devices remain without cloud management."
            };
        }
    }
}
