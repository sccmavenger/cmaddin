using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ZeroTrustMigrationAddin.Models
{
    /// <summary>
    /// Base class for all agent tools that can be invoked during ReAct loop
    /// </summary>
    public abstract class AgentTool
    {
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract Dictionary<string, object> Parameters { get; }

        public abstract Task<AgentToolResult> ExecuteAsync(Dictionary<string, object> parameters);

        /// <summary>
        /// Convert tool to OpenAI function definition format
        /// </summary>
        public object ToFunctionDefinition()
        {
            return new
            {
                name = Name,
                description = Description,
                parameters = new
                {
                    type = "object",
                    properties = Parameters,
                    required = GetRequiredParameters()
                }
            };
        }

        protected virtual string[] GetRequiredParameters() => Array.Empty<string>();
    }

    /// <summary>
    /// Result from executing an agent tool
    /// </summary>
    public class AgentToolResult
    {
        public bool Success { get; set; }
        public string? Data { get; set; }
        public string? Error { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Agent reasoning step in ReAct loop
    /// </summary>
    public class AgentReasoningStep
    {
        public int StepNumber { get; set; }
        public string StepId { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Thought { get; set; } = string.Empty;
        public string? ToolToUse { get; set; }
        public Dictionary<string, object>? ToolParameters { get; set; }
        public AgentToolResult? Observation { get; set; }
        public string? Reflection { get; set; }
    }

    /// <summary>
    /// Complete ReAct execution trace
    /// </summary>
    public class AgentExecutionTrace
    {
        public string TraceId { get; set; } = Guid.NewGuid().ToString();
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }
        public string Goal { get; set; } = string.Empty;
        public List<AgentReasoningStep> Steps { get; set; } = new();
        public bool GoalAchieved { get; set; }
        public string? FinalSummary { get; set; }
    }

    /// <summary>
    /// Agent memory for learning from experiences
    /// </summary>
    public class AgentMemory
    {
        public string MemoryId { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Context { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Outcome { get; set; } = string.Empty;
        public bool Successful { get; set; }
        public string? Pattern { get; set; }
        public double ConfidenceScore { get; set; }
        public int ObservedCount { get; set; } = 1;
    }

    /// <summary>
    /// Agent learning insight derived from memories
    /// </summary>
    public class AgentInsight
    {
        public string InsightId { get; set; } = Guid.NewGuid().ToString();
        public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
        public string Pattern { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public int BasedOnMemories { get; set; }
        public double SuccessRate { get; set; }
    }

    /// <summary>
    /// Agent strategy that adapts based on learning
    /// </summary>
    public class AgentStrategy
    {
        public string StrategyId { get; set; } = Guid.NewGuid().ToString();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUpdated { get; set; }
        public string Goal { get; set; } = string.Empty;
        public List<string> PlannedActions { get; set; } = new();
        public Dictionary<string, double> ActionPriorities { get; set; } = new();
        public List<string> LearnedPatterns { get; set; } = new();
        public double ConfidenceLevel { get; set; } = 0.5;
    }
}
