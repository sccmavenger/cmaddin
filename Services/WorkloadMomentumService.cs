using CloudJourneyAddin.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CloudJourneyAddin.Services
{
    /// <summary>
    /// Provides AI-powered workload momentum insights and transition recommendations.
    /// Uses Azure OpenAI (GPT-4) for intelligent workload ordering based on readiness.
    /// </summary>
    public class WorkloadMomentumService
    {
        private readonly AzureOpenAIService? _openAIService;
        private readonly GraphDataService _graphDataService;

        public WorkloadMomentumService(GraphDataService graphDataService)
        {
            _graphDataService = graphDataService;
            
            try
            {
                _openAIService = new AzureOpenAIService();
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"Failed to initialize Azure OpenAI for workload momentum: {ex.Message}");
                _openAIService = null;
            }
        }

        /// <summary>
        /// Recommends the next best workload to transition using GPT-4. Requires Azure OpenAI configuration.
        /// </summary>
        public async Task<WorkloadMomentumInsight?> GetWorkloadRecommendationAsync(
            List<string> completedWorkloads,
            List<string> inProgressWorkloads,
            double complianceScore,
            int totalDevices,
            int enrolledDevices,
            bool hasSecurityBaseline)
        {
            if (_openAIService?.IsConfigured != true)
            {
                FileLogger.Instance.Warning("Azure OpenAI not configured - workload momentum analysis unavailable");
                return null;
            }

            try
            {
                var aiInsight = await GetWorkloadInsightFromGPT4Async(
                    completedWorkloads, inProgressWorkloads, complianceScore,
                    totalDevices, enrolledDevices, hasSecurityBaseline);
                
                if (aiInsight != null)
                {
                    aiInsight.IsAIPowered = true;
                    return aiInsight;
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"GPT-4 workload analysis failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Gets workload recommendation from GPT-4 with readiness analysis.
        /// </summary>
        private async Task<WorkloadMomentumInsight?> GetWorkloadInsightFromGPT4Async(
            List<string> completedWorkloads,
            List<string> inProgressWorkloads,
            double complianceScore,
            int totalDevices,
            int enrolledDevices,
            bool hasSecurityBaseline)
        {
            string allWorkloads = "Compliance Policies, Device Configuration, Windows Update, Endpoint Protection, Resource Access, Office Click-to-Run, Client Apps";
            double enrollmentPercentage = totalDevices > 0 ? (enrolledDevices * 100.0 / totalDevices) : 0;

            string systemPrompt = @"You are an expert Microsoft Intune co-management strategist with deep expertise in workload transition sequencing. Your role is to analyze an organization's readiness and recommend the optimal NEXT workload to transition from ConfigMgr to Intune.

Key Expertise:
- Co-management workload dependencies and prerequisites
- Risk assessment for each workload transition
- Policy migration strategies
- Rollback planning and validation criteria
- Conservative vs aggressive transition strategies

Provide specific, actionable guidance that minimizes risk while maintaining momentum.";

            string userPrompt = $@"Analyze this organization's co-management state and recommend the NEXT BEST workload to transition:

COMPLETED WORKLOADS:
{(completedWorkloads.Any() ? string.Join(", ", completedWorkloads) : "None")}

IN-PROGRESS WORKLOADS:
{(inProgressWorkloads.Any() ? string.Join(", ", inProgressWorkloads) : "None")}

AVAILABLE WORKLOADS:
{allWorkloads}

ORGANIZATION READINESS:
- Compliance score: {complianceScore:F1}%
- Total devices: {totalDevices}
- Enrolled devices: {enrolledDevices} ({enrollmentPercentage:F1}%)
- Security baseline: {(hasSecurityBaseline ? "Deployed" : "Not deployed")}

ANALYSIS REQUIRED:
1. Which workload should they transition NEXT? Consider dependencies, risk, and readiness.
2. WHY is this workload the best choice right now? (2-3 sentence rationale)
3. What is the READINESS SCORE (0-100) for this workload based on current state?
4. What is the RISK LEVEL (Low/Medium/High/Critical)?
5. List 2-3 PREREQUISITES that must be met before transition.
6. List 2-3 SUCCESS FACTORS that indicate they're ready.
7. Create a week-by-week TRANSITION ROADMAP (4 weeks: Pilot, Deploy, Validate, Optimize).
8. How many weeks will the full transition take?

Respond in JSON format:
{{
    ""recommendedWorkload"": ""string (exact workload name)"",
    ""rationale"": ""string (2-3 sentences why this workload is next)"",
    ""readinessScore"": double (0-100),
    ""riskLevel"": ""string (Low/Medium/High/Critical)"",
    ""prerequisites"": [
        ""string (prerequisite that must be met)""
    ],
    ""successFactors"": [
        ""string (indicator they're ready)""
    ],
    ""transitionRoadmap"": [
        {{
            ""week"": integer (1-4),
            ""phase"": ""string (Pilot/Deploy/Validate/Optimize)"",
            ""action"": ""string (specific action for this week)"",
            ""validationCriteria"": ""string (how to know this week succeeded)""
        }}
    ],
    ""estimatedWeeks"": integer
}}";

            try
            {
                var response = await _openAIService!.GetStructuredResponseAsync<WorkloadInsightResponse>(
                    systemPrompt,
                    userPrompt,
                    1000, // max tokens
                    0.7f); // temperature

                if (response != null)
                {
                    FileLogger.Instance.Info($"GPT-4 workload analysis: Recommend {response.RecommendedWorkload} (Readiness: {response.ReadinessScore:F0})");
                    
                    return new WorkloadMomentumInsight
                    {
                        RecommendedWorkload = response.RecommendedWorkload,
                        Rationale = response.Rationale,
                        ReadinessScore = response.ReadinessScore,
                        RiskLevel = response.RiskLevel,
                        Prerequisites = response.Prerequisites,
                        SuccessFactors = response.SuccessFactors,
                        TransitionRoadmap = response.TransitionRoadmap,
                        EstimatedWeeks = response.EstimatedWeeks,
                        IsAIPowered = true
                    };
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"Failed to get GPT-4 workload insight: {ex.Message}");
            }

            return null;
        }
    }

    /// <summary>
    /// Response model from GPT-4 workload analysis.
    /// </summary>
    public class WorkloadInsightResponse
    {
        public string RecommendedWorkload { get; set; } = string.Empty;
        public string Rationale { get; set; } = string.Empty;
        public double ReadinessScore { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
        public List<string> Prerequisites { get; set; } = new();
        public List<string> SuccessFactors { get; set; } = new();
        public List<WorkloadTransitionStep> TransitionRoadmap { get; set; } = new();
        public int EstimatedWeeks { get; set; }
    }
}
