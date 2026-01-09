using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CloudJourneyAddin.Models;

namespace CloudJourneyAddin.Services
{
    /// <summary>
    /// AI-powered recommendation engine using GPT-4 to guide migration.
    /// Focuses on: 1. Device Enrollment  2. Workload Transitions
    /// REQUIRES Azure OpenAI to be configured - no rule-based fallback.
    /// Enhanced with Phase 1: Phased Planning, Device Selection, and Trend Analysis.
    /// </summary>
    public class AIRecommendationService
    {
        private readonly GraphDataService _graphService;
        private readonly PhasedMigrationService _phasedMigrationService;
        private readonly DeviceSelectionService _deviceSelectionService;
        private readonly WorkloadTrendService _workloadTrendService;
        private readonly AzureOpenAIService? _openAIService;
        
        public bool IsConfigured => _openAIService?.IsConfigured ?? false;
        
        public AIRecommendationService(GraphDataService graphService)
        {
            _graphService = graphService;
            _phasedMigrationService = new PhasedMigrationService(graphService);
            _deviceSelectionService = new DeviceSelectionService(graphService);
            _workloadTrendService = new WorkloadTrendService();
            
            // Initialize Azure OpenAI - Optional (gracefully handle if not configured)
            try
            {
                _openAIService = new AzureOpenAIService();
                if (_openAIService.IsConfigured)
                {
                    FileLogger.Instance.Info("Azure OpenAI service initialized and configured successfully");
                }
                else
                {
                    FileLogger.Instance.Warning("Azure OpenAI not configured - recommendations will not be available until configured");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"Azure OpenAI initialization failed: {ex.Message}");
                _openAIService = null;
            }
        }

        /// <summary>
        /// Analyzes current migration state and generates GPT-4 powered recommendations.
        /// Focuses exclusively on: 1. Device Enrollment  2. Workload Transitions
        /// ENHANCED: Incorporates phased planning, device selection, velocity tracking, and stall detection
        /// </summary>
        public async Task<List<AIRecommendation>> GetRecommendationsAsync(
            DeviceEnrollment deviceEnrollment,
            List<Workload> workloads,
            ComplianceScore compliance,
            DateTime lastProgressDate,
            MigrationPlan? activePlan = null)
        {
            // If Azure OpenAI not configured, return empty list (UI will show config message)
            if (!IsConfigured)
            {
                return new List<AIRecommendation>();
            }

            // PHASE 1 ENHANCEMENT: Record workload progress for trend analysis
            await _workloadTrendService.RecordWorkloadProgressAsync(
                workloads, 
                deviceEnrollment.TotalDevices, 
                deviceEnrollment.IntuneEnrolledDevices);

            // Get velocity trends for GPT-4 context
            var velocityData = await _workloadTrendService.GetWorkloadTrendsAsync(90);
            var velocityInsight = await _workloadTrendService.AnalyzeWorkloadVelocityAsync(workloads);

            // Get phase guidance if migration plan exists
            string? phaseContext = null;
            if (activePlan != null)
            {
                var currentPhaseNum = activePlan.Phases
                    .FirstOrDefault(p => p.PhaseNumber > 0)?.PhaseNumber ?? 1;
                phaseContext = $"Active Migration Plan - Phase {currentPhaseNum}: {activePlan.Phases.FirstOrDefault(p => p.PhaseNumber == currentPhaseNum)?.Name ?? "N/A"}";
            }

            // Detect stalls
            var daysSinceProgress = (DateTime.Now - lastProgressDate).Days;
            var isStalled = daysSinceProgress > 30;

            // Single comprehensive GPT-4 call for enrollment and workload recommendations
            var recommendations = await GenerateGPT4RecommendationsAsync(
                deviceEnrollment,
                workloads,
                daysSinceProgress,
                isStalled,
                phaseContext,
                velocityInsight);

            return recommendations;
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

        /// <summary>
        /// Comprehensive GPT-4 analysis focusing on Enrollment and Workload Transitions.
        /// Incorporates velocity trends, phased plan status, and stall detection.
        /// 
        /// DATA PRIVACY: Only sends aggregated metrics to Azure OpenAI:
        /// - Device counts (total, enrolled, percentages)
        /// - Workload status (counts and generic names like "Compliance Policies")
        /// - Progress timing (days since last progress)
        /// - Velocity trends (descriptive summaries)
        /// - Phase status (if migration plan active)
        /// 
        /// DOES NOT SEND:
        /// - Device names, hostnames, or computer names
        /// - User names, emails, or identities
        /// - IP addresses, network info, or hardware IDs
        /// - Organization/tenant names
        /// - Configuration details or policy settings
        /// - Any personally identifiable information (PII)
        /// </summary>
        private async Task<List<AIRecommendation>> GenerateGPT4RecommendationsAsync(
            DeviceEnrollment enrollment,
            List<Workload> workloads,
            int daysSinceProgress,
            bool isStalled,
            string? phaseContext,
            List<AIRecommendation> velocityInsights)
        {
            var completedWorkloads = workloads.Where(w => w.Status == WorkloadStatus.Completed).ToList();
            var inProgressWorkloads = workloads.Where(w => w.Status == WorkloadStatus.InProgress).ToList();
            var notStartedWorkloads = workloads.Where(w => w.Status == WorkloadStatus.NotStarted).ToList();

            // Build velocity context from insights
            var velocityContext = velocityInsights.Any() 
                ? string.Join("\n", velocityInsights.Select(v => $"  - {v.Title}: {v.Description}"))
                : "No significant velocity issues detected";

            var systemPrompt = @"You are an expert Microsoft Intune migration consultant with 15+ years of experience helping organizations migrate from ConfigMgr to Intune.

Your role is to provide specific, actionable recommendations focused EXCLUSIVELY on:
1. Device Enrollment progress and acceleration
2. Workload Transition planning and execution

Be specific, actionable, and empathetic. Use Microsoft FastTrack best practices.";

            var userPrompt = $@"MIGRATION STATE:
- Total Devices: {enrollment.TotalDevices}
- Intune Enrolled: {enrollment.IntuneEnrolledDevices} ({enrollment.IntuneEnrollmentPercentage:F1}%)
- ConfigMgr Only: {enrollment.ConfigMgrOnlyDevices}
- Days Since Last Progress: {daysSinceProgress}
- Stalled: {(isStalled ? "YES - CRITICAL" : "No")}

WORKLOAD STATUS:
- Completed: {completedWorkloads.Count}/{workloads.Count} ({string.Join(", ", completedWorkloads.Select(w => w.Name))})
- In Progress: {inProgressWorkloads.Count} ({string.Join(", ", inProgressWorkloads.Select(w => w.Name))})
- Not Started: {notStartedWorkloads.Count}

VELOCITY & TRENDS:
{velocityContext}

{(phaseContext != null ? $"MIGRATION PLAN:\n{phaseContext}\n" : "")}
CONTEXT & BEST PRACTICES:
- 50% enrollment is the critical tipping point (85% on-time completion rate)
- <25% enrollment after 90 days = 65% failure risk
- Recommended workload order: Compliance→Endpoint Protection→Device Config→Resource Access→Updates→Office→Apps
- Don't start workloads until ≥50% enrollment
- Stalls >45 days rarely recover without intervention

TASK:
Generate 2-4 prioritized recommendations focused on ENROLLMENT and WORKLOAD TRANSITIONS.
{(isStalled ? "\n⚠️ CRITICAL: Address the stall first - identify root causes related to enrollment or workload blockers.\n" : "")}
For each recommendation:
1. Clear title with priority indicator (🚨 Critical, ⚡ High, ✅ Medium)
2. Specific description of current state and why it matters
3. Rationale backed by FastTrack data
4. 4-6 concrete, numbered action steps
5. Realistic effort estimate
6. Impact score (0-100)
7. Priority level (Critical, High, Medium, Low)
8. Category (DeviceEnrollment or WorkloadTransition)

FORMAT AS VALID JSON (no markdown):
{{
  ""recommendations"": [
    {{
      ""title"": ""string"",
      ""description"": ""string"",
      ""rationale"": ""string"",
      ""actionSteps"": [""step 1"", ""step 2"", ...],
      ""estimatedEffort"": ""string"",
      ""impactScore"": number,
      ""priority"": ""Critical|High|Medium|Low"",
      ""category"": ""DeviceEnrollment|WorkloadTransition"",
      ""resourceLinks"": [""url1"", ""url2""]
    }}
  ]
}}";

            try
            {
                var response = await _openAIService!.GetStructuredResponseAsync<GPT4RecommendationResponse>(
                    systemPrompt, userPrompt, maxTokens: 1500, temperature: 0.7f);

                if (response == null || response.Recommendations == null || !response.Recommendations.Any())
                {
                    FileLogger.Instance.Warning("GPT-4 returned no recommendations");
                    return new List<AIRecommendation>();
                }

                // Convert GPT-4 response to AIRecommendation objects
                return response.Recommendations.Select(r => new AIRecommendation
                {
                    Title = r.Title,
                    Description = r.Description,
                    Rationale = r.Rationale,
                    ActionSteps = r.ActionSteps,
                    EstimatedEffort = r.EstimatedEffort,
                    ImpactScore = r.ImpactScore,
                    Priority = ParsePriority(r.Priority),
                    Category = ParseCategory(r.Category),
                    ResourceLinks = r.ResourceLinks ?? new List<string>()
                }).ToList();
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"GPT-4 recommendation generation failed: {ex.Message}");
                return new List<AIRecommendation>();
            }
        }

        private RecommendationPriority ParsePriority(string priority)
        {
            return priority?.ToLower() switch
            {
                "critical" => RecommendationPriority.Critical,
                "high" => RecommendationPriority.High,
                "medium" => RecommendationPriority.Medium,
                "low" => RecommendationPriority.Low,
                _ => RecommendationPriority.Medium
            };
        }

        private RecommendationCategory ParseCategory(string category)
        {
            return category?.ToLower() switch
            {
                "deviceenrollment" => RecommendationCategory.DeviceEnrollment,
                "workloadtransition" => RecommendationCategory.WorkloadTransition,
                "stallprevention" => RecommendationCategory.StallPrevention,
                _ => RecommendationCategory.General
            };
        }
    } // End AIRecommendationService class

    #region AI Recommendation Models

    /// <summary>
    /// Response model for GPT-4 comprehensive recommendations
    /// </summary>
    public class GPT4RecommendationResponse
    {
        public List<GPT4Recommendation> Recommendations { get; set; } = new();
    }

    public class GPT4Recommendation
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Rationale { get; set; } = string.Empty;
        public List<string> ActionSteps { get; set; } = new();
        public string EstimatedEffort { get; set; } = string.Empty;
        public int ImpactScore { get; set; }
        public string Priority { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<string>? ResourceLinks { get; set; }
    }

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

    #endregion
}
