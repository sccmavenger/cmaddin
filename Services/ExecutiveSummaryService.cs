using ZeroTrustMigrationAddin.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZeroTrustMigrationAddin.Services
{
    /// <summary>
    /// Provides AI-powered executive summary and migration health insights.
    /// Uses Azure OpenAI (GPT-4) for comprehensive migration status analysis.
    /// </summary>
    public class ExecutiveSummaryService
    {
        private readonly AzureOpenAIService? _openAIService;
        private readonly GraphDataService _graphDataService;

        public ExecutiveSummaryService(GraphDataService graphDataService)
        {
            _graphDataService = graphDataService;
            
            try
            {
                _openAIService = new AzureOpenAIService();
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"Failed to initialize Azure OpenAI for executive summary: {ex.Message}");
                _openAIService = null;
            }
        }

        /// <summary>
        /// Generates executive summary using GPT-4. Requires Azure OpenAI configuration.
        /// </summary>
        public async Task<ExecutiveSummary?> GetExecutiveSummaryAsync(
            int totalDevices,
            int enrolledDevices,
            List<string> completedWorkloads,
            List<string> inProgressWorkloads,
            double complianceScore,
            int daysSinceStart,
            int daysSinceLastProgress)
        {
            if (_openAIService?.IsConfigured != true)
            {
                FileLogger.Instance.Warning("Azure OpenAI not configured - executive summary analysis unavailable");
                return null;
            }

            try
            {
                var aiSummary = await GetExecutiveSummaryFromGPT4Async(
                    totalDevices, enrolledDevices, completedWorkloads,
                    inProgressWorkloads, complianceScore, daysSinceStart, daysSinceLastProgress);
                
                if (aiSummary != null)
                {
                    aiSummary.IsAIPowered = true;
                    return aiSummary;
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"GPT-4 executive summary failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Gets executive summary from GPT-4 with comprehensive analysis.
        /// </summary>
        private async Task<ExecutiveSummary?> GetExecutiveSummaryFromGPT4Async(
            int totalDevices,
            int enrolledDevices,
            List<string> completedWorkloads,
            List<string> inProgressWorkloads,
            double complianceScore,
            int daysSinceStart,
            int daysSinceLastProgress)
        {
            // Log input parameters BEFORE making GPT-4 call
            FileLogger.Instance.Info("[EXECUTIVE] Calling GPT-4 for executive summary analysis");
            FileLogger.Instance.Debug($"[EXECUTIVE] Input params: TotalDevices={totalDevices}, Enrolled={enrolledDevices} ({(totalDevices > 0 ? enrolledDevices * 100.0 / totalDevices : 0):F1}%)");
            FileLogger.Instance.Debug($"[EXECUTIVE] Input params: CompletedWorkloads=[{string.Join(", ", completedWorkloads)}], InProgress=[{string.Join(", ", inProgressWorkloads)}]");
            FileLogger.Instance.Debug($"[EXECUTIVE] Input params: ComplianceScore={complianceScore:F1}%, DaysSinceStart={daysSinceStart}, DaysSinceLastProgress={daysSinceLastProgress}");
            
            double enrollmentPercentage = totalDevices > 0 ? (enrolledDevices * 100.0 / totalDevices) : 0;
            int totalWorkloads = 7;

            string systemPrompt = @"You are a C-level technology executive advisor specializing in cloud transformation initiatives. Your role is to provide concise, strategic insights on Microsoft Intune migration health that executives can act upon immediately.

Key Expertise:
- Executive communication (avoid technical jargon)
- Risk assessment and mitigation strategies
- ROI and business value articulation
- Strategic decision-making guidance
- Project health scoring and forecasting

Provide clear, actionable insights that executives can present to leadership.";

            string userPrompt = $@"Provide an executive summary of this Microsoft Intune migration project:

PROJECT METRICS:
- Project duration: {daysSinceStart} days
- Days since last progress: {daysSinceLastProgress}
- Total devices: {totalDevices}
- Enrolled in Intune: {enrolledDevices} ({enrollmentPercentage:F1}%)
- Workloads completed: {completedWorkloads.Count} of {totalWorkloads}
- Workloads in progress: {inProgressWorkloads.Count}
- Compliance score: {complianceScore:F1}%

COMPLETED WORKLOADS:
{(completedWorkloads.Any() ? string.Join(", ", completedWorkloads) : "None")}

IN-PROGRESS WORKLOADS:
{(inProgressWorkloads.Any() ? string.Join(", ", inProgressWorkloads) : "None")}

EXECUTIVE ANALYSIS REQUIRED:
1. Calculate MIGRATION HEALTH SCORE (0-100) based on overall progress and momentum.
2. Determine OVERALL STATUS: ""On Track"" / ""At Risk"" / ""Stalled"" (be honest about problems).
3. List 2-3 KEY ACHIEVEMENTS that demonstrate value delivered.
4. List 1-2 CRITICAL ISSUES that need executive attention (if any).
5. Identify the ONE NEXT CRITICAL ACTION that will have the biggest impact.
6. Estimate days to completion with current velocity.
7. Calculate SUCCESS PROBABILITY (0-100) for on-time completion.
8. Write 3-4 sentence EXECUTIVE SUMMARY for leadership presentation.

Be direct about problems - executives need truth, not optimism.

Respond in JSON format:
{{
    ""migrationHealthScore"": integer (0-100),
    ""overallStatus"": ""string (On Track/At Risk/Stalled)"",
    ""keyAchievements"": [
        ""string (achievement with business impact)""
    ],
    ""criticalIssues"": [
        ""string (issue requiring executive action)""
    ],
    ""nextCriticalAction"": ""string (single most important action)"",
    ""projectedCompletionDays"": integer,
    ""successProbability"": double (0-100),
    ""executiveSummaryText"": ""string (3-4 sentences for leadership)""
}}";

            try
            {
                var response = await _openAIService!.GetStructuredResponseAsync<ExecutiveSummaryResponse>(
                    systemPrompt,
                    userPrompt,
                    1200, // max tokens
                    0.7f); // temperature

                if (response != null)
                {
                    FileLogger.Instance.Info($"GPT-4 executive summary: Health={response.MigrationHealthScore}, Status={response.OverallStatus}");
                    
                    return new ExecutiveSummary
                    {
                        MigrationHealthScore = response.MigrationHealthScore,
                        OverallStatus = response.OverallStatus,
                        KeyAchievements = response.KeyAchievements,
                        CriticalIssues = response.CriticalIssues,
                        NextCriticalAction = response.NextCriticalAction,
                        ProjectedCompletionDays = response.ProjectedCompletionDays,
                        SuccessProbability = response.SuccessProbability,
                        ExecutiveSummaryText = response.ExecutiveSummaryText,
                        IsAIPowered = true
                    };
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"Failed to get GPT-4 executive summary: {ex.Message}");
            }

            return null;
        }
    }

    /// <summary>
    /// Response model from GPT-4 executive summary.
    /// </summary>
    public class ExecutiveSummaryResponse
    {
        public int MigrationHealthScore { get; set; }
        public string OverallStatus { get; set; } = string.Empty;
        public List<string> KeyAchievements { get; set; } = new();
        public List<string> CriticalIssues { get; set; } = new();
        public string NextCriticalAction { get; set; } = string.Empty;
        public int ProjectedCompletionDays { get; set; }
        public double SuccessProbability { get; set; }
        public string ExecutiveSummaryText { get; set; } = string.Empty;
    }
}
