using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CloudJourneyAddin.Models;
using Microsoft.Extensions.Logging;

namespace CloudJourneyAddin.Services
{
    /// <summary>
    /// Agent memory service for local learning (v2.5)
    /// Stores experiences and derives insights for strategy optimization
    /// </summary>
    public class AgentMemoryService
    {
        private readonly string _memoryPath;
        private readonly string _insightsPath;
        private readonly ILogger<AgentMemoryService> _logger;

        private List<AgentMemory> _memories = new();
        private List<AgentInsight> _insights = new();

        public AgentMemoryService(ILogger<AgentMemoryService> logger)
        {
            _logger = logger;

            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CloudJourneyAddin",
                "AgentMemory");

            Directory.CreateDirectory(appDataPath);

            _memoryPath = Path.Combine(appDataPath, "memories.jsonl");
            _insightsPath = Path.Combine(appDataPath, "insights.json");

            _ = LoadMemoriesAsync();
        }

        /// <summary>
        /// Store a new experience in memory
        /// </summary>
        public async Task StoreMemoryAsync(AgentMemory memory)
        {
            _memories.Add(memory);

            // Append to JSONL file
            try
            {
                var json = JsonSerializer.Serialize(memory);
                await File.AppendAllLinesAsync(_memoryPath, new[] { json });

                // Periodically analyze and derive insights
                if (_memories.Count % 10 == 0)
                {
                    await DeriveInsightsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store memory");
            }
        }

        /// <summary>
        /// Get memories relevant to current context
        /// </summary>
        public async Task<List<AgentMemory>> GetRelevantMemoriesAsync(string context, int limit = 10)
        {
            await Task.CompletedTask;

            // Simple relevance: recent memories + successful ones
            return _memories
                .Where(m => m.Successful || m.Context.Contains(context, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.Timestamp)
                .Take(limit)
                .ToList();
        }

        /// <summary>
        /// Get insights derived from experiences
        /// </summary>
        public async Task<List<AgentInsight>> GetRelevantInsightsAsync(string context)
        {
            await Task.CompletedTask;

            return _insights
                .Where(i => i.Confidence >= 0.7) // High confidence insights only
                .OrderByDescending(i => i.Confidence)
                .ToList();
        }

        /// <summary>
        /// Get all insights for display
        /// </summary>
        public async Task<List<AgentInsight>> GetAllInsightsAsync()
        {
            await Task.CompletedTask;
            return _insights.OrderByDescending(i => i.Confidence).ToList();
        }

        /// <summary>
        /// Analyze memories and derive insights (learning phase)
        /// </summary>
        private async Task DeriveInsightsAsync()
        {
            try
            {
                // Group memories by pattern
                var patternGroups = _memories
                    .Where(m => !string.IsNullOrEmpty(m.Pattern))
                    .GroupBy(m => m.Pattern)
                    .ToList();

                var newInsights = new List<AgentInsight>();

                foreach (var group in patternGroups)
                {
                    var totalCount = group.Count();
                    var successCount = group.Count(m => m.Successful);
                    var successRate = (double)successCount / totalCount;

                    // Only create insights for patterns with enough data
                    if (totalCount >= 3)
                    {
                        var existingInsight = _insights.FirstOrDefault(i => i.Pattern == group.Key);

                        if (existingInsight != null)
                        {
                            // Update existing insight
                            existingInsight.Confidence = CalculateConfidence(totalCount, successRate);
                            existingInsight.BasedOnMemories = totalCount;
                            existingInsight.SuccessRate = successRate;
                        }
                        else
                        {
                            // Create new insight
                            var insight = new AgentInsight
                            {
                                Pattern = group.Key ?? string.Empty,
                                Recommendation = GenerateRecommendation(group.Key ?? string.Empty, successRate),
                                Confidence = CalculateConfidence(totalCount, successRate),
                                BasedOnMemories = totalCount,
                                SuccessRate = successRate
                            };

                            newInsights.Add(insight);
                        }
                    }
                }

                _insights.AddRange(newInsights);

                // Save insights to disk
                await SaveInsightsAsync();

                _logger.LogInformation($"Derived {newInsights.Count} new insights from {_memories.Count} memories");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to derive insights");
            }
        }

        private double CalculateConfidence(int observationCount, double successRate)
        {
            // Confidence increases with more observations and higher success rate
            var observationFactor = Math.Min(1.0, observationCount / 20.0); // Max at 20 observations
            var successFactor = successRate;

            return (observationFactor + successFactor) / 2.0;
        }

        private string GenerateRecommendation(string pattern, double successRate)
        {
            if (successRate >= 0.9)
            {
                return $"Highly recommended: {pattern} has proven very effective";
            }
            else if (successRate >= 0.7)
            {
                return $"Recommended: {pattern} shows good results";
            }
            else if (successRate >= 0.5)
            {
                return $"Use with caution: {pattern} has mixed results";
            }
            else
            {
                return $"Not recommended: {pattern} shows poor outcomes";
            }
        }

        private async Task LoadMemoriesAsync()
        {
            try
            {
                if (File.Exists(_memoryPath))
                {
                    var lines = await File.ReadAllLinesAsync(_memoryPath);
                    _memories = lines
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .Select(l => JsonSerializer.Deserialize<AgentMemory>(l))
                        .Where(m => m != null)
                        .Cast<AgentMemory>()
                        .ToList();

                    _logger.LogInformation($"Loaded {_memories.Count} memories");
                }

                if (File.Exists(_insightsPath))
                {
                    var json = await File.ReadAllTextAsync(_insightsPath);
                    _insights = JsonSerializer.Deserialize<List<AgentInsight>>(json) ?? new();

                    _logger.LogInformation($"Loaded {_insights.Count} insights");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load memories");
            }
        }

        private async Task SaveInsightsAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_insights, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_insightsPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save insights");
            }
        }

        /// <summary>
        /// Get statistics for demo/dashboard
        /// </summary>
        public async Task<object> GetMemoryStatsAsync()
        {
            await Task.CompletedTask;

            return new
            {
                total_memories = _memories.Count,
                successful_actions = _memories.Count(m => m.Successful),
                success_rate = _memories.Count > 0 
                    ? (double)_memories.Count(m => m.Successful) / _memories.Count * 100 
                    : 0,
                insights_discovered = _insights.Count,
                high_confidence_insights = _insights.Count(i => i.Confidence >= 0.8),
                patterns_identified = _memories.Select(m => m.Pattern).Distinct().Count()
            };
        }
    }
}
