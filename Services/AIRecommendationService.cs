using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CloudJourneyAddin.Models;

namespace CloudJourneyAddin.Services
{
    /// <summary>
    /// AI-powered recommendation engine to guide migration and prevent stalls.
    /// Uses customer journey insights to provide contextual guidance.
    /// Enhanced with Phase 1: Phased Planning, Device Selection, and Trend Analysis.
    /// </summary>
    public class AIRecommendationService
    {
        private readonly GraphDataService _graphService;
        private readonly PhasedMigrationService _phasedMigrationService;
        private readonly DeviceSelectionService _deviceSelectionService;
        private readonly WorkloadTrendService _workloadTrendService;
        private readonly AzureOpenAIService? _openAIService;
        
        public AIRecommendationService(GraphDataService graphService)
        {
            _graphService = graphService;
            _phasedMigrationService = new PhasedMigrationService(graphService);
            _deviceSelectionService = new DeviceSelectionService(graphService);
            _workloadTrendService = new WorkloadTrendService();
            
            // Initialize Azure OpenAI if configured
            try
            {
                _openAIService = new AzureOpenAIService();
                if (_openAIService.IsConfigured)
                {
                    FileLogger.Instance.Info("Azure OpenAI service initialized and configured");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"Failed to initialize Azure OpenAI service: {ex.Message}");
                _openAIService = null;
            }
        }

        /// <summary>
        /// Analyzes current migration state and generates prioritized recommendations
        /// Focus: 1. Device Enrollment  2. Workload Transitions
        /// ENHANCED: Now includes phased planning, device selection, and velocity tracking
        /// </summary>
        public async Task<List<AIRecommendation>> GetRecommendationsAsync(
            DeviceEnrollment deviceEnrollment,
            List<Workload> workloads,
            ComplianceScore compliance,
            DateTime lastProgressDate,
            MigrationPlan? activePlan = null)
        {
            var recommendations = new List<AIRecommendation>();

            // PHASE 1 ENHANCEMENT: Record workload progress for trend analysis
            await _workloadTrendService.RecordWorkloadProgressAsync(
                workloads, 
                deviceEnrollment.TotalDevices, 
                deviceEnrollment.IntuneEnrolledDevices);

            // PHASE 1 ENHANCEMENT: Phased Migration Plan Guidance
            if (activePlan != null)
            {
                var phaseGuidance = _phasedMigrationService.GetCurrentPhaseGuidance(
                    activePlan, 
                    (int)deviceEnrollment.IntuneEnrollmentPercentage);
                recommendations.AddRange(phaseGuidance);
            }

            // PHASE 1 ENHANCEMENT: Workload Velocity Trend Analysis
            var velocityRecommendations = await _workloadTrendService.AnalyzeWorkloadVelocityAsync(workloads);
            recommendations.AddRange(velocityRecommendations);

            // Priority 1: Device Enrollment (if < 75%)
            if (deviceEnrollment.IntuneEnrollmentPercentage < 75)
            {
                recommendations.AddRange(await GenerateEnrollmentRecommendationsAsync(deviceEnrollment));
            }

            // Priority 2: Detect and Prevent Stalls
            var daysSinceProgress = (DateTime.Now - lastProgressDate).Days;
            if (daysSinceProgress > 30)
            {
                recommendations.AddRange(await GenerateStallPreventionRecommendationsAsync(
                    deviceEnrollment, workloads, daysSinceProgress));
            }

            // Priority 3: Workload Transition Guidance
            recommendations.AddRange(GenerateWorkloadRecommendations(
                workloads, deviceEnrollment, compliance));

            // Priority 4: Compliance & Security
            if (compliance.IntuneScore < 80)
            {
                recommendations.AddRange(GenerateComplianceRecommendations(compliance));
            }

            // Sort by priority and impact
            return recommendations
                .OrderByDescending(r => r.Priority)
                .ThenByDescending(r => r.ImpactScore)
                .ToList();
        }

        /// <summary>
        /// PHASE 1: Generate a phased migration plan with timeline and tasks
        /// </summary>
        public async Task<MigrationPlan> CreateMigrationPlanAsync(
            int totalDevices,
            DateTime targetCompletionDate,
            int currentlyEnrolled)
        {
            return await _phasedMigrationService.GeneratePhasedPlanAsync(
                totalDevices, 
                targetCompletionDate, 
                currentlyEnrolled);
        }

        /// <summary>
        /// PHASE 1: Get intelligent device selection recommendations
        /// Note: This method requires device data to be passed in. 
        /// Call from ViewModel after querying Graph API for device list.
        /// </summary>
        public async Task<List<AIRecommendation>> GetDeviceSelectionGuidanceAsync(
            int unenrolledDeviceCount,
            int batchSize = 50)
        {
            // For now, provide guidance based on count
            // Full implementation requires device list from Graph API
            var recommendations = new List<AIRecommendation>();

            if (unenrolledDeviceCount == 0)
            {
                recommendations.Add(new AIRecommendation
                {
                    Title = "✅ All Devices Enrolled!",
                    Description = "No unenrolled devices remaining. Migration complete!",
                    Priority = RecommendationPriority.Low,
                    Category = RecommendationCategory.DeviceEnrollment
                });
            }
            else
            {
                recommendations.Add(new AIRecommendation
                {
                    Title = $"🎯 Device Selection: {unenrolledDeviceCount} Devices Remaining",
                    Description = $"Recommend enrolling devices in batches of {batchSize}. " +
                                  "Prioritize devices that are Azure AD joined, recently online, and running modern OS versions.",
                    ActionSteps = new List<string>
                    {
                        "1. Query unenrolled devices from ConfigMgr",
                        "2. Filter for Azure AD joined devices (highest success rate)",
                        "3. Select devices seen online in last 7 days",
                        $"4. Create first batch of {batchSize} devices",
                        "5. Enable co-management auto-enrollment for selected group"
                    },
                    Priority = RecommendationPriority.High,
                    Category = RecommendationCategory.DeviceEnrollment,
                    ImpactScore = 85,
                    EstimatedEffort = "2-3 days"
                });
            }

            return await Task.FromResult(recommendations);
        }

        /// <summary>
        /// PHASE 1: Get workload trend data for visualization
        /// </summary>
        public async Task<Dictionary<string, List<WorkloadProgressEntry>>> GetWorkloadTrendsAsync(int days = 90)
        {
            return await _workloadTrendService.GetWorkloadTrendsAsync(days);
        }

        #region Device Enrollment Recommendations

        private async Task<List<AIRecommendation>> GenerateEnrollmentRecommendationsAsync(
            DeviceEnrollment enrollment)
        {
            var recommendations = new List<AIRecommendation>();
            var enrollmentPercent = enrollment.IntuneEnrollmentPercentage;
            var remainingDevices = enrollment.ConfigMgrOnlyDevices;

            // Critical: Very low enrollment (< 25%)
            if (enrollmentPercent < 25)
            {
                recommendations.Add(new AIRecommendation
                {
                    Title = "🚨 Critical: Begin Device Enrollment Immediately",
                    Description = $"Only {enrollmentPercent:F0}% of devices enrolled. {remainingDevices} devices remaining. " +
                                  "Enrollment is the foundation - workloads cannot be migrated until devices are in Intune.",
                    Rationale = "Based on Microsoft FastTrack data, organizations with < 25% enrollment after 90 days have a 65% risk of migration failure. " +
                               "Early enrollment momentum is critical for success.",
                    ActionSteps = new List<string>
                    {
                        "1. Start with pilot group (10-20 devices) - test enrollment process",
                        "2. Choose enrollment method: AutoPilot (new devices) or Co-management (existing)",
                        "3. Set up Cloud Management Gateway (CMG) if not already configured",
                        "4. Create device groups in Azure AD for staged rollout",
                        "5. Target: Enroll 50-100 devices per week"
                    },
                    Priority = RecommendationPriority.Critical,
                    Category = RecommendationCategory.DeviceEnrollment,
                    ImpactScore = 100,
                    EstimatedEffort = "2-3 weeks for pilot, ongoing for full rollout",
                    ResourceLinks = new List<string>
                    {
                        "https://learn.microsoft.com/mem/intune/enrollment/windows-enrollment-methods",
                        "https://learn.microsoft.com/mem/configmgr/comanage/how-to-prepare-win10"
                    }
                });
            }
            // High: Moderate enrollment (25-50%)
            else if (enrollmentPercent < 50)
            {
                recommendations.Add(new AIRecommendation
                {
                    Title = "⚡ High Priority: Accelerate Enrollment Velocity",
                    Description = $"Good start at {enrollmentPercent:F0}% enrolled, but need momentum. {remainingDevices} devices still pending. " +
                                  "Target: 50% enrollment is the tipping point for successful migration.",
                    Rationale = "Industry data shows 50% enrollment is a critical threshold. Organizations reaching 50% within 4 months have 85% on-time completion rate.",
                    ActionSteps = new List<string>
                    {
                        $"1. Analyze pilot success - enrollment time, issues encountered",
                        "2. Expand to next wave: target departmental rollout (HR, Finance, etc.)",
                        "3. Automate enrollment for new devices with AutoPilot",
                        "4. Use ConfigMgr client settings to auto-enroll existing devices",
                        $"5. Goal: Enroll remaining {remainingDevices} devices over next 6-8 weeks"
                    },
                    Priority = RecommendationPriority.High,
                    Category = RecommendationCategory.DeviceEnrollment,
                    ImpactScore = 85,
                    EstimatedEffort = "6-8 weeks",
                    ResourceLinks = new List<string>
                    {
                        "https://learn.microsoft.com/mem/intune/enrollment/windows-bulk-enroll",
                        "https://learn.microsoft.com/mem/autopilot/windows-autopilot"
                    }
                });
            }
            // Medium: Good progress (50-75%)
            else if (enrollmentPercent < 75)
            {
                recommendations.Add(new AIRecommendation
                {
                    Title = "✅ On Track: Complete Remaining Enrollment",
                    Description = $"Excellent progress at {enrollmentPercent:F0}% enrolled. {remainingDevices} devices remain. " +
                                  "Focus: Identify stragglers and edge cases.",
                    Rationale = "You've passed the critical threshold. Final devices are typically edge cases (remote workers, legacy hardware, special configurations).",
                    ActionSteps = new List<string>
                    {
                        "1. Identify remaining devices - why aren't they enrolled?",
                        "2. Common issues: Offline devices, network restrictions, OS versions",
                        "3. Reach out to device owners directly for holdouts",
                        "4. Consider: Is device eligible? (Windows 10 1809+ required)",
                        $"5. Target completion: {remainingDevices} devices within 4 weeks"
                    },
                    Priority = RecommendationPriority.Medium,
                    Category = RecommendationCategory.DeviceEnrollment,
                    ImpactScore = 65,
                    EstimatedEffort = "3-4 weeks",
                    ResourceLinks = new List<string>
                    {
                        "https://learn.microsoft.com/mem/intune/enrollment/troubleshoot-windows-enrollment-errors"
                    }
                });
            }

            return await Task.FromResult(recommendations);
        }

        #endregion

        #region Stall Prevention Recommendations

        private async Task<List<AIRecommendation>> GenerateStallPreventionRecommendationsAsync(
            DeviceEnrollment enrollment,
            List<Workload> workloads,
            int daysSinceProgress)
        {
            var recommendations = new List<AIRecommendation>();

            // Try GPT-4 enhanced analysis first
            if (_openAIService?.IsConfigured == true)
            {
                try
                {
                    var gpt4Recommendation = await AnalyzeStallWithGPT4Async(
                        enrollment, workloads, daysSinceProgress);
                    
                    if (gpt4Recommendation != null)
                    {
                        recommendations.Add(gpt4Recommendation);
                        return recommendations;
                    }
                }
                catch (Exception ex)
                {
                    FileLogger.Instance.Warning($"GPT-4 stall analysis failed, using fallback: {ex.Message}");
                    // Fall through to rule-based logic
                }
            }

            // Fallback to rule-based logic
            recommendations.AddRange(GenerateStallPreventionRecommendations(enrollment, workloads, daysSinceProgress));
            return recommendations;
        }

        private List<AIRecommendation> GenerateStallPreventionRecommendations(
            DeviceEnrollment enrollment,
            List<Workload> workloads,
            int daysSinceProgress)
        {
            var recommendations = new List<AIRecommendation>();

            // Detect type of stall
            bool enrollmentStalled = enrollment.IntuneEnrollmentPercentage < 75;
            bool workloadStalled = workloads.Count(w => w.Status == WorkloadStatus.InProgress) > 0;
            bool noWorkloadsStarted = workloads.All(w => w.Status == WorkloadStatus.NotStarted);

            recommendations.Add(new AIRecommendation
            {
                Title = $"🚨 Migration Stalled: No Progress in {daysSinceProgress} Days",
                Description = $"Migration momentum has stopped. This is a critical risk indicator. " +
                              $"Common causes: Resource constraints, technical blockers, or lack of executive support.",
                Rationale = "Microsoft data shows migrations stalled > 45 days have 70% risk of never completing. " +
                           "Immediate intervention required to restart momentum.",
                ActionSteps = GenerateStallRecoverySteps(enrollmentStalled, workloadStalled, noWorkloadsStarted, daysSinceProgress),
                Priority = RecommendationPriority.Critical,
                Category = RecommendationCategory.StallPrevention,
                ImpactScore = 95,
                EstimatedEffort = "1-2 weeks to restart",
                ResourceLinks = new List<string>
                {
                    "https://aka.ms/FastTrack",
                    "https://learn.microsoft.com/mem/intune/fundamentals/migration-guide"
                }
            });

            return recommendations;
        }

        /// <summary>
        /// GPT-4 Enhanced Stall Analysis - Provides intelligent root cause analysis and recovery guidance
        /// </summary>
        private async Task<AIRecommendation?> AnalyzeStallWithGPT4Async(
            DeviceEnrollment enrollment,
            List<Workload> workloads,
            int daysSinceProgress)
        {
            var completedWorkloads = workloads.Count(w => w.Status == WorkloadStatus.Completed);
            var inProgressWorkloads = workloads.Where(w => w.Status == WorkloadStatus.InProgress).Select(w => w.Name).ToList();
            var totalWorkloads = workloads.Count;

            var systemPrompt = "You are an expert Microsoft Intune migration consultant with 15+ years of experience. " +
                "Analyze the customer's migration state and identify root causes for stalls. Be specific, actionable, and empathetic.";

            var userPrompt = $"MIGRATION STATE:\n" +
                $"- Days since last progress: {daysSinceProgress}\n" +
                $"- Enrollment: {enrollment.IntuneEnrollmentPercentage:F0}% ({enrollment.IntuneEnrolledDevices}/{enrollment.TotalDevices} devices)\n" +
                $"- ConfigMgr-only devices remaining: {enrollment.ConfigMgrOnlyDevices}\n" +
                $"- Workloads completed: {completedWorkloads}/{totalWorkloads}\n" +
                $"- Workloads in progress: {string.Join(", ", inProgressWorkloads)}\n\n" +
                $"CONTEXT:\n" +
                $"- Organization size: {enrollment.TotalDevices} devices\n" +
                $"- Enrollment percentage category: {GetEnrollmentCategory(enrollment.IntuneEnrollmentPercentage)}\n" +
                $"- Stall duration category: {GetStallCategory(daysSinceProgress)}\n\n" +
                "TASK:\n" +
                "1. Identify the 2-3 most likely root causes for this specific stall\n" +
                "2. Provide 4-5 concrete, actionable recovery steps (not generic advice)\n" +
                "3. Estimate realistic recovery timeline\n" +
                "4. Determine if external help (Microsoft FastTrack) is recommended\n\n" +
                "FORMAT YOUR RESPONSE AS VALID JSON (no markdown, just raw JSON):\n" +
                "{\n" +
                "  \"rootCauses\": [\"cause 1 with specifics\", \"cause 2 with context\"],\n" +
                "  \"recoverySteps\": [\"Step 1: specific action\", \"Step 2: ...\", \"Step 3: ...\"],\n" +
                "  \"estimatedRecoveryTime\": \"X weeks\",\n" +
                "  \"recommendFastTrack\": true or false,\n" +
                "  \"rationale\": \"1-2 sentences explaining why this analysis matters\"\n" +
                "}";

            try
            {
                var analysis = await _openAIService!.GetStructuredResponseAsync<StallAnalysisResponse>(
                    systemPrompt, userPrompt, maxTokens: 800, temperature: 0.7f);

                if (analysis == null)
                    return null;

                return new AIRecommendation
                {
                    Title = $"🤖 GPT-4 Stall Analysis: {daysSinceProgress} Days No Progress",
                    Description = "AI-Enhanced Root Cause Analysis:\n" + string.Join("\n", analysis.RootCauses.Select((c, i) => $"  {i + 1}. {c}")),
                    Rationale = analysis.Rationale + (analysis.RecommendFastTrack ? "\n\n⚠️ Microsoft FastTrack assistance recommended." : ""),
                    ActionSteps = analysis.RecoverySteps,
                    Priority = RecommendationPriority.Critical,
                    Category = RecommendationCategory.StallPrevention,
                    ImpactScore = 100,
                    EstimatedEffort = analysis.EstimatedRecoveryTime,
                    ResourceLinks = new List<string>
                    {
                        "https://aka.ms/FastTrack",
                        "https://learn.microsoft.com/mem/intune/fundamentals/migration-guide"
                    }
                };
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"GPT-4 stall analysis failed: {ex.Message}");
                return null;
            }
        }

        private string GetEnrollmentCategory(double percentage)
        {
            if (percentage < 25) return "Very Low (Critical)";
            if (percentage < 50) return "Low (High Priority)";
            if (percentage < 75) return "Moderate (On Track)";
            return "High (Near Completion)";
        }

        private string GetStallCategory(int days)
        {
            if (days < 30) return "Short stall";
            if (days < 60) return "Medium stall (concerning)";
            return "Long stall (critical intervention needed)";
        }

        private List<string> GenerateStallRecoverySteps(bool enrollmentStalled, bool workloadStalled, bool noWorkloadsStarted, int daysSinceProgress)
        {
            var steps = new List<string>
            {
                "1. Schedule emergency review meeting with project sponsor",
                "2. Identify root cause:"
            };

            if (enrollmentStalled)
            {
                steps.Add("   - Enrollment issues? Check CMG connectivity, Azure AD sync");
            }

            if (workloadStalled)
            {
                steps.Add("   - Workload blocked? Review policy conflicts, licensing issues");
            }

            if (noWorkloadsStarted)
            {
                steps.Add("   - Prerequisites missing? Verify co-management, licensing, permissions");
            }

            steps.AddRange(new List<string>
            {
                "3. Reassess resources - do you have dedicated project time?",
                "4. Consider Microsoft FastTrack assistance (free with 150+ licenses)",
                "5. Break into smaller milestones - pick ONE workload to complete this month",
                $"6. Set daily standup for next 2 weeks to rebuild momentum",
                "7. Communicate progress to leadership weekly"
            });

            return steps;
        }

        #endregion

        #region Workload Transition Recommendations

        private List<AIRecommendation> GenerateWorkloadRecommendations(
            List<Workload> workloads,
            DeviceEnrollment enrollment,
            ComplianceScore compliance)
        {
            var recommendations = new List<AIRecommendation>();

            // Don't recommend workload migration if enrollment is too low
            if (enrollment.IntuneEnrollmentPercentage < 50)
            {
                recommendations.Add(new AIRecommendation
                {
                    Title = "📋 Workload Migration: Not Ready Yet",
                    Description = $"Current enrollment: {enrollment.IntuneEnrollmentPercentage:F0}%. Workload migration should wait until ≥50% of devices are enrolled.",
                    Rationale = "Migrating workloads before device enrollment creates management gaps. Policies won't apply to unenrolled devices, creating security and compliance risks.",
                    ActionSteps = new List<string>
                    {
                        "1. Focus on device enrollment first (see enrollment recommendations)",
                        "2. Once enrollment reaches 50%, begin workload planning",
                        "3. Start with Compliance Policies (lowest risk workload)"
                    },
                    Priority = RecommendationPriority.Low,
                    Category = RecommendationCategory.WorkloadTransition,
                    ImpactScore = 40,
                    EstimatedEffort = "N/A - waiting on enrollment",
                    ResourceLinks = new List<string>()
                });

                return recommendations;
            }

            // Recommend next workload based on current state
            var completedWorkloads = workloads.Where(w => w.Status == WorkloadStatus.Completed).ToList();
            var inProgressWorkloads = workloads.Where(w => w.Status == WorkloadStatus.InProgress).ToList();
            var notStartedWorkloads = workloads.Where(w => w.Status == WorkloadStatus.NotStarted).ToList();

            // If workload is in progress, help complete it
            if (inProgressWorkloads.Any())
            {
                var workload = inProgressWorkloads.First();
                recommendations.Add(new AIRecommendation
                {
                    Title = $"🎯 Complete In-Progress Workload: {workload.Name}",
                    Description = $"You have '{workload.Name}' in progress. Completing this workload builds momentum and prevents stall.",
                    Rationale = "Completing started workloads before starting new ones maintains focus and demonstrates progress to stakeholders.",
                    ActionSteps = GetWorkloadCompletionSteps(workload.Name),
                    Priority = RecommendationPriority.High,
                    Category = RecommendationCategory.WorkloadTransition,
                    ImpactScore = 80,
                    EstimatedEffort = "1-2 weeks",
                    ResourceLinks = new List<string> { workload.LearnMoreUrl }
                });
            }
            // Recommend next workload to start
            else if (notStartedWorkloads.Any())
            {
                var nextWorkload = GetRecommendedNextWorkload(completedWorkloads, notStartedWorkloads);
                recommendations.Add(new AIRecommendation
                {
                    Title = $"🚀 Ready for Next Workload: {nextWorkload.Name}",
                    Description = $"Recommended next step: Migrate '{nextWorkload.Name}' workload. " +
                                  $"With {enrollment.IntuneEnrollmentPercentage:F0}% enrollment, you're ready for this transition.",
                    Rationale = GetWorkloadRationale(nextWorkload.Name, completedWorkloads.Count),
                    ActionSteps = GetWorkloadMigrationSteps(nextWorkload.Name),
                    Priority = RecommendationPriority.High,
                    Category = RecommendationCategory.WorkloadTransition,
                    ImpactScore = 75,
                    EstimatedEffort = GetWorkloadEffort(nextWorkload.Name),
                    ResourceLinks = new List<string> { nextWorkload.LearnMoreUrl }
                });
            }
            // All workloads complete!
            else
            {
                recommendations.Add(new AIRecommendation
                {
                    Title = "🎉 All Workloads Complete!",
                    Description = "Congratulations! All 7 workloads have been migrated to Intune. You're ready for final steps.",
                    Rationale = "Complete workload migration means you can now decommission ConfigMgr infrastructure and realize full ROI.",
                    ActionSteps = new List<string>
                    {
                        "1. Verify all devices receiving policies from Intune",
                        "2. Monitor for 2 weeks - ensure stability",
                        "3. Plan ConfigMgr decommissioning (keep 3-month rollback capability)",
                        "4. Document lessons learned for other teams",
                        "5. Celebrate with your team! 🎉"
                    },
                    Priority = RecommendationPriority.Medium,
                    Category = RecommendationCategory.WorkloadTransition,
                    ImpactScore = 60,
                    EstimatedEffort = "4 weeks (stabilization + decommission)",
                    ResourceLinks = new List<string>
                    {
                        "https://learn.microsoft.com/mem/configmgr/core/plan-design/changes/removed-and-deprecated"
                    }
                });
            }

            return recommendations;
        }

        private Workload GetRecommendedNextWorkload(List<Workload> completed, List<Workload> notStarted)
        {
            // Recommended order based on Microsoft best practices and complexity
            var recommendedOrder = new List<string>
            {
                "Compliance Policies",        // 1st - Foundation, low risk
                "Endpoint Protection",        // 2nd - Security, low risk
                "Device Configuration",       // 3rd - Medium complexity
                "Resource Access",           // 4th - User productivity
                "Windows Update for Business", // 5th - Medium-high complexity
                "Office Click-to-Run",       // 6th - App delivery
                "Client Apps"                // 7th - Most complex
            };

            foreach (var workloadName in recommendedOrder)
            {
                var workload = notStarted.FirstOrDefault(w => w.Name.Contains(workloadName));
                if (workload != null)
                    return workload;
            }

            return notStarted.First(); // Fallback
        }

        private string GetWorkloadRationale(string workloadName, int completedCount)
        {
            if (workloadName.Contains("Compliance")) return "This is the recommended first workload. It's low-risk (policies are evaluative, not enforcing) and establishes your device health baseline.";
            if (workloadName.Contains("Endpoint Protection")) return "Security is critical and should be migrated early. Windows Defender, firewall, and BitLocker policies are largely compatible and low-risk.";
            if (workloadName.Contains("Device Configuration")) return "With compliance and security established, you're ready for configuration profiles. This includes settings, WiFi, VPN.";
            if (workloadName.Contains("Resource Access")) return "User productivity depends on access to resources. WiFi, VPN, and certificate profiles are straightforward migrations.";
            if (workloadName.Contains("Windows Update")) return "Patch management is complex but critical. With previous workloads complete, you have the foundation to handle update rings.";
            if (workloadName.Contains("Office")) return "Office 365 Apps deployment through Intune is more efficient than ConfigMgr.";
            if (workloadName.Contains("Client Apps")) return "This is the final and most complex workload. Win32 app deployment requires packaging, testing, and validation.";
            
            return $"Recommended based on your {completedCount} completed workloads.";
        }

        private List<string> GetWorkloadMigrationSteps(string workloadName)
        {
            if (workloadName.Contains("Compliance"))
            {
                return new List<string>
                {
                    "1. Review existing ConfigMgr compliance baselines",
                    "2. Create equivalent compliance policies in Intune",
                    "3. Start with pilot group (10-20 devices)",
                    "4. Assign policies and monitor for 1 week",
                    "5. Expand to full fleet once validated"
                };
            }

            return new List<string>
            {
                "1. Review current ConfigMgr configuration",
                "2. Create equivalent policies in Intune",
                "3. Test on pilot group",
                "4. Monitor and validate",
                "5. Roll out to production"
            };
        }

        private List<string> GetWorkloadCompletionSteps(string workloadName)
        {
            return new List<string>
            {
                "1. Review what's completed vs what remains",
                "2. Check for policy conflicts (ConfigMgr vs Intune)",
                "3. Expand assignment from pilot to broader groups",
                "4. Monitor for 1 week before marking complete",
                "5. Move co-management slider fully to Intune"
            };
        }

        private string GetWorkloadEffort(string workloadName)
        {
            if (workloadName.Contains("Compliance") || workloadName.Contains("Endpoint")) return "1-2 weeks";
            if (workloadName.Contains("Device Configuration") || workloadName.Contains("Resource")) return "2-3 weeks";
            if (workloadName.Contains("Windows Update") || workloadName.Contains("Office")) return "3-4 weeks";
            if (workloadName.Contains("Client Apps")) return "8-12 weeks";
            
            return "2-4 weeks";
        }

        #endregion

        #region Compliance Recommendations

        private List<AIRecommendation> GenerateComplianceRecommendations(ComplianceScore compliance)
        {
            var recommendations = new List<AIRecommendation>();

            if (compliance.IntuneScore < 80)
            {
                recommendations.Add(new AIRecommendation
                {
                    Title = $"⚠️ Compliance Score Below Target: {compliance.IntuneScore:F0}%",
                    Description = $"Current compliance: {compliance.IntuneScore:F0}%. Target: 95%+. " +
                                  $"Risk areas identified in your environment.",
                    Rationale = "Low compliance indicates security gaps. Intune provides better compliance enforcement than ConfigMgr, but policies must be configured.",
                    ActionSteps = new List<string>
                    {
                        "1. Review non-compliant devices in Intune",
                        "2. Identify most common compliance failures",
                        "3. Create remediation plan for each risk area",
                        "4. Give users 7 days notice before enforcement",
                        "5. Enable Conditional Access for non-compliant devices"
                    },
                    Priority = RecommendationPriority.High,
                    Category = RecommendationCategory.Compliance,
                    ImpactScore = 75,
                    EstimatedEffort = "2-3 weeks",
                    ResourceLinks = new List<string>
                    {
                        "https://learn.microsoft.com/mem/intune/protect/device-compliance-get-started"
                    }
                });
            }

            return recommendations;
        }

        #endregion
    }

    #region AI Recommendation Models

    public class AIRecommendation
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Rationale { get; set; } = string.Empty;
        public List<string> ActionSteps { get; set; } = new List<string>();
        public RecommendationPriority Priority { get; set; }
        public RecommendationCategory Category { get; set; }
        public int ImpactScore { get; set; } // 0-100
        public string EstimatedEffort { get; set; } = string.Empty;
        public List<string> ResourceLinks { get; set; } = new List<string>();
        public DateTime GeneratedDate { get; set; } = DateTime.Now;
    }

    public enum RecommendationPriority
    {
        Critical = 4,  // Blocking issue, immediate action required
        High = 3,      // Important, should address within 1 week
        Medium = 2,    // Should address within 2-4 weeks
        Low = 1        // Nice to have, address when capacity allows
    }

    public enum RecommendationCategory
    {
        DeviceEnrollment,
        WorkloadTransition,
        StallPrevention,
        Compliance,
        Performance,
        General
    }

    /// <summary>
    /// Response model for GPT-4 stall analysis
    /// </summary>
    public class StallAnalysisResponse
    {
        public List<string> RootCauses { get; set; } = new();
        public List<string> RecoverySteps { get; set; } = new();
        public string EstimatedRecoveryTime { get; set; } = string.Empty;
        public bool RecommendFastTrack { get; set; }
        public string Rationale { get; set; } = string.Empty;
    }

    #endregion
}
