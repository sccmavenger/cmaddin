using CloudJourneyAddin.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CloudJourneyAddin.Services
{
    /// <summary>
    /// Provides AI-powered enrollment momentum insights and recommendations.
    /// Uses Azure OpenAI (GPT-4) for personalized velocity analysis and batch sizing.
    /// </summary>
    public class EnrollmentMomentumService
    {
        private readonly AzureOpenAIService? _openAIService;
        private readonly GraphDataService _graphDataService;

        public EnrollmentMomentumService(GraphDataService graphDataService)
        {
            _graphDataService = graphDataService;
            
            try
            {
                _openAIService = new AzureOpenAIService();
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"Failed to initialize Azure OpenAI for enrollment momentum: {ex.Message}");
                _openAIService = null;
            }
        }

        /// <summary>
        /// Generates enrollment momentum insights using GPT-4. Requires Azure OpenAI configuration.
        /// v2.6.0: Enhanced with device readiness data for health-based recommendations.
        /// </summary>
        public async Task<EnrollmentMomentumInsight?> GetEnrollmentMomentumAsync(
            int totalDevices,
            int enrolledDevices,
            int devicesPerWeek,
            bool hasCMG,
            bool hasCoManagement,
            int weeksSinceStart,
            DeviceReadinessBreakdown? deviceReadiness = null,
            EnrollmentBlockerSummary? enrollmentBlockers = null)
        {
            if (_openAIService?.IsConfigured != true)
            {
                FileLogger.Instance.Warning("Azure OpenAI not configured - enrollment momentum analysis unavailable");
                return null;
            }

            try
            {
                var aiInsight = await GetEnrollmentInsightFromGPT4Async(
                    totalDevices, enrolledDevices, devicesPerWeek, 
                    hasCMG, hasCoManagement, weeksSinceStart,
                    deviceReadiness, enrollmentBlockers);
                
                if (aiInsight != null)
                {
                    aiInsight.IsAIPowered = true;
                    return aiInsight;
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"GPT-4 enrollment analysis failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Gets enrollment insights from GPT-4 with velocity analysis.
        /// v2.6.0: Enhanced with device health and blocker data.
        /// </summary>
        private async Task<EnrollmentMomentumInsight?> GetEnrollmentInsightFromGPT4Async(
            int totalDevices, int enrolledDevices, int devicesPerWeek,
            bool hasCMG, bool hasCoManagement, int weeksSinceStart,
            DeviceReadinessBreakdown? deviceReadiness,
            EnrollmentBlockerSummary? enrollmentBlockers)
        {
            double enrollmentPercentage = totalDevices > 0 ? (enrolledDevices * 100.0 / totalDevices) : 0;
            int remainingDevices = totalDevices - enrolledDevices;
            int weeksToComplete = devicesPerWeek > 0 ? (int)Math.Ceiling(remainingDevices / (double)devicesPerWeek) : 0;

            string systemPrompt = @"You are an expert Microsoft Intune enrollment strategist with 15+ years of experience helping organizations migrate from ConfigMgr to Intune. Your role is to analyze enrollment velocity and provide actionable recommendations to accelerate device enrollment while maintaining stability.

Key Expertise:
- Cloud Management Gateway (CMG) capacity planning
- Co-management configuration and workload transitions
- Bulk enrollment strategies and batch sizing
- Infrastructure bottleneck identification
- Enrollment velocity optimization

Provide practical, specific guidance that administrators can act on immediately.";

            string userPrompt = $@"Analyze this organization's Intune enrollment progress and provide acceleration strategies:

CURRENT STATE:
- Total devices: {totalDevices}
- Enrolled devices: {enrolledDevices} ({enrollmentPercentage:F1}%)
- Remaining devices: {remainingDevices}
- Current velocity: {devicesPerWeek} devices/week
- Time in migration: {weeksSinceStart} weeks
- Projected completion: {weeksToComplete} weeks

INFRASTRUCTURE:
- Cloud Management Gateway (CMG): {(hasCMG ? "Deployed" : "Not deployed")}
- Co-management: {(hasCoManagement ? "Enabled" : "Not enabled")}

ANALYSIS REQUIRED:
1. Is the current velocity acceptable? Compare to industry benchmarks for organizations of this size.
2. What is the RECOMMENDED velocity (devices/week) they should target?
   - CRITICAL: Consider device health breakdown. Prioritize High Success devices first for faster, safer enrollment.
   - If High Success devices are available, recommend aggressive velocity (they have 98% success rate).
   - If only Moderate/High Risk remain, recommend conservative velocity to avoid failures.
3. What is the OPTIMAL batch size for the next enrollment wave?
   - Base on device health: High Success = larger batches, High Risk = smaller batches.
4. Identify 2-3 specific INFRASTRUCTURE or HEALTH BLOCKERS that may be limiting velocity.
   - Include ConfigMgr client health issues if High Risk device count is significant.
5. Provide 3-4 actionable ACCELERATION STRATEGIES with expected impact.
   - Strategy 1 MUST address device prioritization based on health scores if data available.
   - Include specific actions to remediate High Risk devices (fix ConfigMgr clients).
6. Create a WEEK-BY-WEEK roadmap for the next 4 weeks (specific device counts per week).
   - Week 1-2: Focus on High Success devices (fast wins).
   - Week 3-4: Moderate Success devices or High Risk after remediation.
7. Calculate REALISTIC completion timeline with the recommended velocity.
   - Factor in enrollment blockers (subtract blocked devices from enrollable total).

Respond in JSON format:
{{
    ""currentVelocityAssessment"": ""string (e.g., 'Below average for organization size', 'On track', 'Ahead of schedule')"",
    ""recommendedVelocity"": integer (devices per week),
    ""optimalBatchSize"": integer (devices for next wave),
    ""infrastructureBlockers"": [
        ""string (specific blocker with root cause)""
    ],
    ""accelerationStrategies"": [
        {{
            ""action"": ""string (specific action to take)"",
            ""impact"": ""string (expected velocity increase)"",
            ""effortLevel"": ""string (Low/Medium/High)""
        }}
    ],
    ""weekByWeekRoadmap"": [
        {{
            ""week"": integer (1-4),
            ""targetDevices"": integer,
            ""focusArea"": ""string (what to focus on this week)""
        }}
    ],
    ""projectedCompletionWeeks"": integer,
    ""rationale"": ""string (2-3 sentences explaining the recommendations)""
}}";

            try
            {
                var response = await _openAIService!.GetStructuredResponseAsync<EnrollmentInsightResponse>(
                    systemPrompt,
                    userPrompt,
                    1000, // max tokens
                    0.7f); // temperature

                if (response != null)
                {
                    FileLogger.Instance.Info($"GPT-4 enrollment analysis: {response.RecommendedVelocity} devices/week recommended");
                    
                    return new EnrollmentMomentumInsight
                    {
                        CurrentVelocity = devicesPerWeek,
                        RecommendedVelocity = response.RecommendedVelocity,
                        OptimalBatchSize = response.OptimalBatchSize,
                        VelocityAssessment = response.CurrentVelocityAssessment,
                        InfrastructureBlockers = response.InfrastructureBlockers,
                        AccelerationStrategies = response.AccelerationStrategies,
                        WeekByWeekRoadmap = response.WeekByWeekRoadmap,
                        ProjectedCompletionWeeks = response.ProjectedCompletionWeeks,
                        Rationale = response.Rationale,
                        IsAIPowered = true
                    };
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"Failed to get GPT-4 enrollment insight: {ex.Message}");
            }

            return null;
        }

    }

    /// <summary>
    /// Response model from GPT-4 enrollment analysis.
    /// </summary>
    public class EnrollmentInsightResponse
    {
        public string CurrentVelocityAssessment { get; set; } = string.Empty;
        public int RecommendedVelocity { get; set; }
        public int OptimalBatchSize { get; set; }
        public List<string> InfrastructureBlockers { get; set; } = new();
        public List<AccelerationStrategy> AccelerationStrategies { get; set; } = new();
        public List<WeeklyTarget> WeekByWeekRoadmap { get; set; } = new();
        public int ProjectedCompletionWeeks { get; set; }
        public string Rationale { get; set; } = string.Empty;
    }
}
