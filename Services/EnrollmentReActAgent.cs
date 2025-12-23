using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using CloudJourneyAddin.Models;
using CloudJourneyAddin.Services.AgentTools;
using Microsoft.Extensions.Logging;

namespace CloudJourneyAddin.Services
{
    /// <summary>
    /// ReAct Agent with function calling and local learning (v3.0)
    /// Implements: Reason → Act → Observe → Reflect loop
    /// Phase 1: Supervised (requires approval)
    /// Phase 2: Conditional autonomy (auto-approve low-risk)
    /// Phase 3: Full autonomy (continuous monitoring)
    /// </summary>
    public class EnrollmentReActAgent
    {
        private readonly AzureOpenAIService _aiService;
        private readonly GraphDataService _graphService;
        private readonly ILogger<EnrollmentReActAgent> _logger;
        private readonly AgentToolkit _toolkit;
        private readonly AgentMemoryService _memory;
        private readonly RiskAssessmentService _riskService;

        // Agent state
        private AgentExecutionTrace? _currentTrace;
        private AgentStrategy? _currentStrategy;

        // Phase configuration
        public AgentPhase CurrentPhase { get; set; } = AgentPhase.Phase1_Supervised;

        // Events for UI updates
        public event EventHandler<AgentReasoningStep>? ReasoningStepCompleted;
        public event EventHandler<AgentInsight>? InsightDiscovered;
        public event EventHandler<string>? StatusChanged;
        public event EventHandler<AutoApprovalEventArgs>? DeviceAutoApproved;  // Phase 2

        public EnrollmentReActAgent(
            AzureOpenAIService aiService,
            GraphDataService graphService,
            AgentMemoryService memory,
            ILogger<EnrollmentReActAgent> logger,
            RiskAssessmentService? riskService = null)
        {
            _aiService = aiService;
            _graphService = graphService;
            _memory = memory;
            _logger = logger;
            _riskService = riskService ?? new RiskAssessmentService();

            // Initialize toolkit
            _toolkit = new AgentToolkit();
            RegisterTools();
        }

        private void RegisterTools()
        {
            _toolkit.RegisterTool(new QueryDevicesTool(_graphService));
            _toolkit.RegisterTool(new EnrollDevicesTool(_graphService));
            _toolkit.RegisterTool(new AnalyzeReadinessTool(_graphService));
        }

        /// <summary>
        /// Execute goal using ReAct loop
        /// </summary>
        public async Task<AgentExecutionTrace> ExecuteGoalAsync(EnrollmentGoals goal)
        {
            _currentTrace = new AgentExecutionTrace
            {
                Goal = $"Enroll {goal.MaxDevicesPerDay} devices per day until {goal.TargetCompletionDate:yyyy-MM-dd}",
                StartTime = DateTime.UtcNow
            };

            StatusChanged?.Invoke(this, "Agent started: Analyzing goal and environment");

            try
            {
                // Initialize strategy
                _currentStrategy = await InitializeStrategyAsync(goal);

                var maxIterations = 20; // Prevent infinite loops
                var iteration = 0;

                while (!await IsGoalAchievedAsync(goal) && iteration < maxIterations)
                {
                    iteration++;

                    // REASON: What should I do next?
                    var reasoningStep = await ReasonAsync(goal, iteration);
                    reasoningStep.StepNumber = iteration; // Set step number for UI display
                    _currentTrace.Steps.Add(reasoningStep);

                    if (string.IsNullOrEmpty(reasoningStep.ToolToUse))
                    {
                        // Agent decided goal is complete or needs human intervention
                        break;
                    }

                    // ACT: Execute the chosen tool
                    var observation = await ActAsync(reasoningStep.ToolToUse, reasoningStep.ToolParameters ?? new());
                    reasoningStep.Observation = observation;

                    // OBSERVE & REFLECT: Learn from the result
                    var reflection = await ReflectAsync(reasoningStep, observation);
                    reasoningStep.Reflection = reflection;

                    ReasoningStepCompleted?.Invoke(this, reasoningStep);

                    // UPDATE STRATEGY: Adapt based on learning
                    await UpdateStrategyAsync(reasoningStep);

                    // Store memory for learning
                    await _memory.StoreMemoryAsync(new AgentMemory
                    {
                        Context = reasoningStep.Thought,
                        Action = reasoningStep.ToolToUse,
                        Outcome = observation.Success ? "success" : "failure",
                        Successful = observation.Success,
                        Pattern = ExtractPattern(reasoningStep)
                    });

                    // Small delay for demo purposes
                    await Task.Delay(500);
                }

                _currentTrace.EndTime = DateTime.UtcNow;
                _currentTrace.GoalAchieved = await IsGoalAchievedAsync(goal);
                _currentTrace.FinalSummary = await GenerateFinalSummaryAsync();

                StatusChanged?.Invoke(this, _currentTrace.GoalAchieved 
                    ? "Goal achieved! Agent execution complete." 
                    : "Agent execution paused - human intervention needed.");

                return _currentTrace;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agent execution failed");
                StatusChanged?.Invoke(this, $"Agent error: {ex.Message}");
                
                _currentTrace.EndTime = DateTime.UtcNow;
                _currentTrace.FinalSummary = $"Execution failed: {ex.Message}";
                return _currentTrace;
            }
        }

        /// <summary>
        /// REASON: Agent decides next action using GPT-4 function calling
        /// </summary>
        private async Task<AgentReasoningStep> ReasonAsync(EnrollmentGoals goal, int iteration)
        {
            StatusChanged?.Invoke(this, $"Thinking... (step {iteration})");

            // Build context for reasoning
            var context = BuildReasoningContext(goal, iteration);

            // Get previous learnings
            var insights = await _memory.GetRelevantInsightsAsync(goal?.ToString() ?? "");

            var systemPrompt = $@"You are an intelligent enrollment agent. Your goal is: {_currentTrace?.Goal}

Your available tools:
{JsonSerializer.Serialize(_toolkit.GetFunctionDefinitions(), new JsonSerializerOptions { WriteIndented = true })}

Previous learnings from this environment:
{string.Join("\n", insights.Select(i => $"- {i.Pattern} (confidence: {i.Confidence:P0}, success rate: {i.SuccessRate:P0})"))}

Current strategy:
{JsonSerializer.Serialize(_currentStrategy, new JsonSerializerOptions { WriteIndented = true })}

Think step-by-step:
1. What information do I need? (use query_devices or analyze_readiness)
2. What action should I take? (use enroll_devices when ready)
3. Am I making progress toward the goal?

Be strategic: Query devices first, analyze readiness, then enroll in batches.
Respond with your reasoning and which tool to call next.";

            var userPrompt = $"Step {iteration}:\n{context}\n\nWhat should I do next to achieve the goal?";

            try
            {
                // Check if AI is available (authenticated)
                if (_aiService.IsConfigured)
                {
                    // PRODUCTION: Use real GPT-4 function calling
                    var step = await ReasonWithGPT4Async(systemPrompt, userPrompt, iteration);
                    return step;
                }
                else
                {
                    // UNAUTHENTICATED: Use rule-based reasoning (no AI calls)
                    var step = await SimulateReasoningAsync(goal!, iteration, context);
                    return step;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reasoning failed");
                return new AgentReasoningStep
                {
                    Thought = $"Error during reasoning: {ex.Message}",
                    ToolToUse = null
                };
            }
        }

        /// <summary>
        /// PRODUCTION: Real GPT-4 function calling (authenticated only)
        /// </summary>
        private async Task<AgentReasoningStep> ReasonWithGPT4Async(string systemPrompt, string userPrompt, int iteration)
        {
            // Call Azure OpenAI with function calling
            var response = await _aiService.GetChatCompletionWithFunctionsAsync(
                systemPrompt,
                userPrompt,
                _toolkit.GetFunctionDefinitions());

            // Parse response
            if (response.Contains("function_call"))
            {
                var functionCall = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(response);
                var toolName = functionCall?["function_call"].GetProperty("name").GetString();
                var argumentsJson = functionCall?["function_call"].GetProperty("arguments").GetString();
                var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(argumentsJson ?? "{}");

                return new AgentReasoningStep
                {
                    Thought = functionCall?["content"].GetString() ?? "Analyzing next action...",
                    ToolToUse = toolName,
                    ToolParameters = parameters
                };
            }
            else
            {
                // No function call - agent finished
                return new AgentReasoningStep
                {
                    Thought = response,
                    ToolToUse = null
                };
            }
        }

        /// <summary>
        /// Simulate intelligent reasoning for demo (v2.0 prototype)
        /// </summary>
        private async Task<AgentReasoningStep> SimulateReasoningAsync(EnrollmentGoals goal, int iteration, string context)
        {
            await Task.Delay(100); // Simulate thinking

            if (iteration == 1)
            {
                return new AgentReasoningStep
                {
                    Thought = "First, I need to understand the device inventory. Let me query all available devices to see what I'm working with.",
                    ToolToUse = "query_devices",
                    ToolParameters = new Dictionary<string, object>
                    {
                        ["filter"] = "all",
                        ["limit"] = 100
                    }
                };
            }
            else if (iteration == 2)
            {
                return new AgentReasoningStep
                {
                    Thought = "Now I have the device list. Let me analyze their readiness to determine which devices are ready for immediate enrollment and which need preparation.",
                    ToolToUse = "analyze_readiness",
                    ToolParameters = new Dictionary<string, object>
                    {
                        ["include_recommendations"] = true
                    }
                };
            }
            else if (iteration == 3)
            {
                return new AgentReasoningStep
                {
                    Thought = $"I see devices ready for enrollment. Based on the goal of {goal.MaxDevicesPerDay} devices per day, I'll start with a batch of ready devices. Let me enroll the first batch.",
                    ToolToUse = "enroll_devices",
                    ToolParameters = new Dictionary<string, object>
                    {
                        ["device_ids"] = JsonDocument.Parse("[\"device-001\", \"device-002\", \"device-003\"]").RootElement,
                        ["batch_name"] = "Batch-1-HighReadiness",
                        ["priority"] = "high"
                    }
                };
            }
            else
            {
                // Agent decides to pause and report progress
                return new AgentReasoningStep
                {
                    Thought = "I've completed the initial enrollment phase. The system should now monitor progress and I'll continue with next batches based on the schedule.",
                    ToolToUse = null // Signal completion
                };
            }
        }

        /// <summary>
        /// ACT: Execute the chosen tool
        /// </summary>
        private async Task<AgentToolResult> ActAsync(string toolName, Dictionary<string, object> parameters)
        {
            StatusChanged?.Invoke(this, $"Executing: {toolName}");

            var tool = _toolkit.GetTool(toolName);
            if (tool == null)
            {
                return new AgentToolResult
                {
                    Success = false,
                    Error = $"Tool not found: {toolName}"
                };
            }

            return await tool.ExecuteAsync(parameters);
        }

        /// <summary>
        /// REFLECT: Learn from the observation
        /// </summary>
        private async Task<string> ReflectAsync(AgentReasoningStep step, AgentToolResult observation)
        {
            await Task.Delay(50); // Simulate reflection time

            if (observation.Success)
            {
                var metadata = observation.Metadata;
                if (metadata?.ContainsKey("success_rate") == true)
                {
                    var successRate = (double)metadata["success_rate"];
                    if (successRate >= 90)
                    {
                        return $"Excellent outcome! {successRate:F1}% success rate. This strategy is working well. " +
                               "I should continue with similar batch sizes and device selection criteria.";
                    }
                    else if (successRate >= 70)
                    {
                        return $"Good outcome with {successRate:F1}% success rate. " +
                               "I might need to be more selective with device readiness in future batches.";
                    }
                    else
                    {
                        return $"Lower success rate ({successRate:F1}%). " +
                               "I should analyze failures and adjust strategy - possibly reduce batch size or improve device preparation.";
                    }
                }

                return "Action completed successfully. Continuing with next step.";
            }
            else
            {
                return $"Action failed: {observation.Error}. I need to adjust my approach or seek human guidance.";
            }
        }

        private async Task<AgentStrategy> InitializeStrategyAsync(EnrollmentGoals goal)
        {
            var insights = await _memory.GetRelevantInsightsAsync(goal?.ToString() ?? "");

            return new AgentStrategy
            {
                Goal = $"Enroll devices by {goal?.TargetCompletionDate:yyyy-MM-dd}",
                PlannedActions = new List<string>
                {
                    "Query device inventory",
                    "Analyze readiness scores",
                    "Enroll ready devices in batches",
                    "Monitor and adjust"
                },
                ActionPriorities = new Dictionary<string, double>
                {
                    ["query_devices"] = 1.0,
                    ["analyze_readiness"] = 0.9,
                    ["enroll_devices"] = 0.8
                },
                LearnedPatterns = insights.Select(i => i.Pattern).ToList(),
                ConfidenceLevel = insights.Any() ? insights.Average(i => i.Confidence) : 0.5
            };
        }

        private async Task UpdateStrategyAsync(AgentReasoningStep step)
        {
            if (_currentStrategy == null || step.Observation == null) return;

            _currentStrategy.LastUpdated = DateTime.UtcNow;

            // Adjust confidence based on outcomes
            if (step.Observation.Success)
            {
                _currentStrategy.ConfidenceLevel = Math.Min(1.0, _currentStrategy.ConfidenceLevel + 0.05);
            }
            else
            {
                _currentStrategy.ConfidenceLevel = Math.Max(0.1, _currentStrategy.ConfidenceLevel - 0.1);
            }

            await Task.CompletedTask;
        }

        private string BuildReasoningContext(EnrollmentGoals goal, int iteration)
        {
            var previousSteps = _currentTrace?.Steps.TakeLast(3).ToList() ?? new();
            
            return $@"Iteration: {iteration}
Previous steps: {string.Join("; ", previousSteps.Select(s => s.Thought))}
Strategy confidence: {_currentStrategy?.ConfidenceLevel:P0}";
        }

        private async Task<bool> IsGoalAchievedAsync(EnrollmentGoals goal)
        {
            // For demo: goal is "achieved" after 3 reasoning steps
            return (_currentTrace?.Steps.Count ?? 0) >= 3;
        }

        private async Task<string> GenerateFinalSummaryAsync()
        {
            var steps = _currentTrace?.Steps ?? new();
            var toolCalls = steps.Count(s => s.ToolToUse != null);
            var successfulActions = steps.Count(s => s.Observation?.Success == true);

            return $"Agent execution summary:\n" +
                   $"- Total reasoning steps: {steps.Count}\n" +
                   $"- Tools invoked: {toolCalls}\n" +
                   $"- Successful actions: {successfulActions}\n" +
                   $"- Strategy confidence: {_currentStrategy?.ConfidenceLevel:P0}";
        }

        private string ExtractPattern(AgentReasoningStep step)
        {
            if (step.Observation?.Metadata == null) return string.Empty;

            var metadata = step.Observation.Metadata;
            if (metadata.ContainsKey("success_rate"))
            {
                var rate = (double)metadata["success_rate"];
                return $"{step.ToolToUse} achieved {rate:F0}% success rate";
            }

            return $"{step.ToolToUse} completed";
        }
    }

    // Phase 2/3 enhancements
    public enum AgentPhase
    {
        Phase1_Supervised,      // All enrollments require approval
        Phase2_Conditional,     // Auto-approve low-risk, require approval for high-risk
        Phase3_FullAutonomy     // Fully autonomous with continuous monitoring
    }

    public class AutoApprovalEventArgs : EventArgs
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public double ReadinessScore { get; set; }
        public double RiskScore { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}

