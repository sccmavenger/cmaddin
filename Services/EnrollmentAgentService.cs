using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudJourneyAddin.Models;
using Microsoft.Extensions.Logging;

namespace CloudJourneyAddin.Services
{
    /// <summary>
    /// AI agent that orchestrates enrollment operations with intelligent planning and execution
    /// Phase 1: Supervised Agent - All agent plans require human approval
    /// </summary>
    public class EnrollmentAgentService
    {
        private readonly AzureOpenAIService _aiService;
        private readonly GraphDataService _graphService;
        private readonly ILogger<EnrollmentAgentService> _logger;

        private EnrollmentPlan? _currentPlan;
        private EnrollmentProgress? _currentProgress;
        private CancellationTokenSource? _executionCancellation;
        private readonly SemaphoreSlim _executionLock = new SemaphoreSlim(1, 1);

        // Configuration paths
        private readonly string _configPath;
        private readonly string _plansPath;
        private readonly string _auditLogPath;

        // Events for UI updates
        public event EventHandler<EnrollmentProgress>? ProgressUpdated;
        public event EventHandler<string>? StatusChanged;
        public event EventHandler<EnrollmentResult>? DeviceEnrolled;

        public EnrollmentAgentService(
            AzureOpenAIService aiService,
            GraphDataService graphService,
            ILogger<EnrollmentAgentService> logger)
        {
            _aiService = aiService;
            _graphService = graphService;
            _logger = logger;

            // Setup storage paths
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CloudJourneyAddin");

            _configPath = Path.Combine(appDataPath, "autonomous-config.json");
            _plansPath = Path.Combine(appDataPath, "plans");
            _auditLogPath = Path.Combine(appDataPath, "audit-log.jsonl");

            Directory.CreateDirectory(appDataPath);
            Directory.CreateDirectory(_plansPath);

            _logger.LogInformation("AutonomousEnrollmentService initialized");
        }

        #region Configuration Management

        /// <summary>
        /// Save enrollment goals configuration
        /// </summary>
        public async Task SaveGoalsAsync(EnrollmentGoals goals)
        {
            try
            {
                var json = JsonSerializer.Serialize(goals, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_configPath, json);

                await LogAuditEventAsync("GoalsSaved", new { goals });
                _logger.LogInformation("Enrollment goals saved");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save enrollment goals");
                throw;
            }
        }

        /// <summary>
        /// Load saved enrollment goals
        /// </summary>
        public async Task<EnrollmentGoals?> LoadGoalsAsync()
        {
            try
            {
                if (!File.Exists(_configPath))
                    return null;

                var json = await File.ReadAllTextAsync(_configPath);
                return JsonSerializer.Deserialize<EnrollmentGoals>(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load enrollment goals");
                return null;
            }
        }

        #endregion

        #region Plan Generation

        /// <summary>
        /// Generate an AI-powered enrollment plan based on goals and current device data
        /// </summary>
        public async Task<EnrollmentPlan> GeneratePlanAsync(EnrollmentGoals goals, DeviceEnrollment deviceData)
        {
            _logger.LogInformation("Generating enrollment plan with AI...");

            try
            {
                // Get device readiness data
                var readinessData = await GetDeviceReadinessDataAsync(deviceData, goals);

                // Build AI prompt
                var prompt = BuildPlanGenerationPrompt(goals, deviceData, readinessData);

                // Call AI to generate plan
                var aiResponse = await _aiService.GenerateEnrollmentPlanAsync(prompt);

                // Parse AI response into structured plan
                var plan = ParseAIPlan(aiResponse, goals);

                // Save plan
                await SavePlanAsync(plan);

                // Log audit event
                await LogAuditEventAsync("PlanGenerated", new
                {
                    planId = plan.PlanId,
                    totalDevices = plan.TotalDevices,
                    batches = plan.Batches.Count,
                    estimatedDuration = plan.EstimatedDuration
                });

                _currentPlan = plan;
                StatusChanged?.Invoke(this, "Plan generated - awaiting approval");

                return plan;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate enrollment plan");
                throw;
            }
        }

        private async Task<List<DeviceReadinessInfo>> GetDeviceReadinessDataAsync(
            DeviceEnrollment deviceData,
            EnrollmentGoals goals)
        {
            // TODO: Integrate with existing device readiness logic
            // For now, return mock data structure
            var readinessData = new List<DeviceReadinessInfo>();

            // In real implementation, this would:
            // 1. Get all ConfigMgr-only devices
            // 2. Calculate readiness score for each
            // 3. Filter by minimum score threshold
            // 4. Exclude devices in goals.ExcludedDeviceIds
            // 5. Prioritize devices in goals.PriorityDeviceIds

            _logger.LogInformation($"Retrieved {readinessData.Count} eligible devices for enrollment");
            return readinessData;
        }

        private string BuildPlanGenerationPrompt(
            EnrollmentGoals goals,
            DeviceEnrollment deviceData,
            List<DeviceReadinessInfo> readinessData)
        {
            var prompt = $@"You are an expert autonomous enrollment orchestrator for cloud device management.

GOALS:
- Target completion: {goals.TargetCompletionDate:yyyy-MM-dd}
- Days remaining: {(goals.TargetCompletionDate - DateTime.Now).Days}
- Risk tolerance: {goals.RiskLevel}
- Max devices/day: {goals.MaxDevicesPerDay?.ToString() ?? "No limit (you decide)"}
- Preferred batch size: {goals.PreferredBatchSize?.ToString() ?? "Optimize for success"}
- Operating hours: {goals.Schedule}
- Failure threshold: {goals.FailureThresholdPercent}%

CURRENT STATE:
- Total devices needing enrollment: {deviceData.ConfigMgrOnlyDevices}
- Intune-enrolled devices: {deviceData.IntuneEnrolledDevices}
- Total devices: {deviceData.TotalDevices}
- Eligible devices (score >= {goals.MinimumReadinessScore}): {readinessData.Count}

CONSTRAINTS:
- Must respect operating hours: {goals.Schedule}
- Must stay within failure threshold: {goals.FailureThresholdPercent}%
- Maximum batch size: {goals.MaxBatchSize}
- Start with smaller batches to establish baseline success rate

YOUR TASK:
Generate an optimal enrollment plan that:
1. Batches devices intelligently (start small ~10-15, increase if success rate high)
2. Prioritizes highest-readiness devices first (build confidence)
3. Spaces batches appropriately (allow monitoring between batches)
4. Respects operating hours and rate limits
5. Achieves target date while minimizing risk

OUTPUT FORMAT (JSON):
{{
  ""batches"": [
    {{
      ""batchNumber"": 1,
      ""deviceCount"": 10,
      ""scheduledTime"": ""2025-12-20T09:00:00"",
      ""justification"": ""Start small with highest-readiness devices to establish baseline"",
      ""averageRiskScore"": 85
    }},
    ...
  ],
  ""reasoning"": ""Detailed explanation of your strategy..."",
  ""estimatedDuration"": ""P14D"" // ISO 8601 duration
}}

Generate the plan now:";

            return prompt;
        }

        private EnrollmentPlan ParseAIPlan(string aiResponse, EnrollmentGoals goals)
        {
            // TODO: Parse actual AI JSON response
            // For now, create a simple plan structure

            var plan = new EnrollmentPlan
            {
                Goals = goals,
                AIReasoning = "AI-generated plan optimized for conservative approach with supervised execution",
                EstimatedDuration = TimeSpan.FromDays(14),
                TotalDevices = 0, // Will be calculated from batches
                Status = PlanStatus.Generated
            };

            // In real implementation, parse AI response and populate batches
            // For now, create a simple 3-batch plan as example
            plan.Batches = new List<EnrollmentBatch>
            {
                new EnrollmentBatch
                {
                    BatchNumber = 1,
                    ScheduledTime = DateTime.Now.AddHours(2),
                    Justification = "Start with small batch of highest-readiness devices",
                    AverageRiskScore = 85.0
                }
            };

            plan.TotalDevices = plan.Batches.Sum(b => b.DeviceIds.Count);

            return plan;
        }

        #endregion

        #region Plan Approval

        /// <summary>
        /// Submit a plan for approval (Phase 1: always requires approval)
        /// </summary>
        public async Task<bool> ApprovePlanAsync(string planId, string approvedBy)
        {
            try
            {
                if (_currentPlan == null || _currentPlan.PlanId != planId)
                {
                    _logger.LogWarning($"Attempted to approve unknown plan: {planId}");
                    return false;
                }

                _currentPlan.Status = PlanStatus.Approved;
                _currentPlan.ApprovedDate = DateTime.Now;
                _currentPlan.ApprovedBy = approvedBy;

                await SavePlanAsync(_currentPlan);

                await LogAuditEventAsync("PlanApproved", new
                {
                    planId,
                    approvedBy,
                    totalDevices = _currentPlan.TotalDevices,
                    batches = _currentPlan.Batches.Count
                });

                StatusChanged?.Invoke(this, "Plan approved - ready to execute");
                _logger.LogInformation($"Plan {planId} approved by {approvedBy}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to approve plan {planId}");
                return false;
            }
        }

        /// <summary>
        /// Reject a plan
        /// </summary>
        public async Task<bool> RejectPlanAsync(string planId, string rejectedBy, string reason)
        {
            try
            {
                if (_currentPlan == null || _currentPlan.PlanId != planId)
                    return false;

                _currentPlan.Status = PlanStatus.Cancelled;

                await SavePlanAsync(_currentPlan);

                await LogAuditEventAsync("PlanRejected", new
                {
                    planId,
                    rejectedBy,
                    reason
                });

                StatusChanged?.Invoke(this, "Plan rejected");
                _logger.LogInformation($"Plan {planId} rejected by {rejectedBy}: {reason}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to reject plan {planId}");
                return false;
            }
        }

        #endregion

        #region Plan Execution

        /// <summary>
        /// Execute an approved enrollment plan
        /// </summary>
        public async Task ExecutePlanAsync(string planId)
        {
            await _executionLock.WaitAsync();

            try
            {
                if (_currentPlan == null || _currentPlan.PlanId != planId)
                    throw new InvalidOperationException($"Plan {planId} not found or not current");

                if (_currentPlan.Status != PlanStatus.Approved)
                    throw new InvalidOperationException($"Plan must be approved before execution");

                _logger.LogInformation($"Starting execution of plan {planId}");

                _currentPlan.Status = PlanStatus.Executing;
                _currentPlan.ExecutionStartDate = DateTime.Now;
                await SavePlanAsync(_currentPlan);

                // Initialize progress tracking
                _currentProgress = new EnrollmentProgress
                {
                    PlanId = planId,
                    TotalDevices = _currentPlan.TotalDevices,
                    TotalBatches = _currentPlan.Batches.Count,
                    StartTime = DateTime.Now,
                    StatusMessage = "Execution started"
                };

                // Create cancellation token for emergency stop
                _executionCancellation = new CancellationTokenSource();

                await LogAuditEventAsync("ExecutionStarted", new { planId });
                StatusChanged?.Invoke(this, "Execution started");

                // Execute batches sequentially
                foreach (var batch in _currentPlan.Batches.OrderBy(b => b.BatchNumber))
                {
                    if (_executionCancellation.Token.IsCancellationRequested)
                    {
                        _logger.LogWarning("Execution cancelled by emergency stop");
                        break;
                    }

                    await ExecuteBatchAsync(batch, _executionCancellation.Token);

                    // Check failure threshold
                    if (_currentProgress.SuccessRate < (100 - _currentPlan.Goals.FailureThresholdPercent))
                    {
                        _logger.LogWarning($"Failure rate {100 - _currentProgress.SuccessRate:F1}% exceeds threshold {_currentPlan.Goals.FailureThresholdPercent}%");
                        await AutoPauseAsync("Failure threshold exceeded");
                        break;
                    }
                }

                // Mark plan as completed
                _currentPlan.Status = PlanStatus.Completed;
                _currentPlan.ExecutionEndDate = DateTime.Now;
                await SavePlanAsync(_currentPlan);

                await LogAuditEventAsync("ExecutionCompleted", new
                {
                    planId,
                    devicesEnrolled = _currentProgress.DevicesEnrolled,
                    devicesFailed = _currentProgress.DevicesFailed,
                    successRate = _currentProgress.SuccessRate
                });

                StatusChanged?.Invoke(this, "Execution completed");
                _logger.LogInformation($"Plan {planId} execution completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to execute plan {planId}");
                
                if (_currentPlan != null)
                {
                    _currentPlan.Status = PlanStatus.Failed;
                    await SavePlanAsync(_currentPlan);
                }

                await LogAuditEventAsync("ExecutionFailed", new { planId, error = ex.Message });
                throw;
            }
            finally
            {
                _executionLock.Release();
            }
        }

        private async Task ExecuteBatchAsync(EnrollmentBatch batch, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Executing batch {batch.BatchNumber} with {batch.DeviceIds.Count} devices");

            batch.Status = BatchStatus.InProgress;
            batch.ActualStartTime = DateTime.Now;

            if (_currentProgress != null)
            {
                _currentProgress.CurrentBatch = batch.BatchNumber;
                _currentProgress.StatusMessage = $"Enrolling batch {batch.BatchNumber} of {_currentProgress.TotalBatches}";
                ProgressUpdated?.Invoke(this, _currentProgress);
            }

            foreach (var deviceId in batch.DeviceIds)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var result = await EnrollDeviceAsync(deviceId, batch.BatchNumber);

                if (result.Success)
                {
                    batch.SuccessCount++;
                    if (_currentProgress != null)
                    {
                        _currentProgress.DevicesEnrolled++;
                        _currentProgress.DevicesPending--;
                    }
                }
                else
                {
                    batch.FailureCount++;
                    if (_currentProgress != null)
                    {
                        _currentProgress.DevicesFailed++;
                        _currentProgress.DevicesPending--;
                    }
                }

                // Update progress
                if (_currentProgress != null)
                {
                    _currentProgress.RecentResults.Insert(0, result);
                    if (_currentProgress.RecentResults.Count > 10)
                        _currentProgress.RecentResults.RemoveAt(_currentProgress.RecentResults.Count - 1);

                    _currentProgress.LastUpdated = DateTime.Now;
                    ProgressUpdated?.Invoke(this, _currentProgress);
                }

                // Notify UI
                DeviceEnrolled?.Invoke(this, result);

                // Rate limiting: 30 seconds between enrollments
                await Task.Delay(30000, cancellationToken);
            }

            batch.ActualEndTime = DateTime.Now;
            batch.Status = batch.FailureCount == 0 ? BatchStatus.Completed : BatchStatus.CompletedWithErrors;

            await LogAuditEventAsync("BatchCompleted", new
            {
                batchNumber = batch.BatchNumber,
                successCount = batch.SuccessCount,
                failureCount = batch.FailureCount,
                duration = (batch.ActualEndTime - batch.ActualStartTime)?.TotalMinutes
            });
        }

        private async Task<EnrollmentResult> EnrollDeviceAsync(string deviceId, int batchNumber)
        {
            var startTime = DateTime.Now;
            var result = new EnrollmentResult
            {
                DeviceId = deviceId,
                DeviceName = $"Device-{deviceId}", // TODO: Get actual device name
                AttemptTime = startTime,
                BatchNumber = batchNumber
            };

            try
            {
                _logger.LogInformation($"Enrolling device {deviceId}...");

                // TODO: Implement actual enrollment via Graph API
                // For now, simulate enrollment
                await Task.Delay(5000); // Simulate enrollment time

                // Simulate 90% success rate
                result.Success = new Random().NextDouble() > 0.10;

                if (!result.Success)
                {
                    result.ErrorMessage = "Simulated enrollment failure";
                }

                result.Duration = DateTime.Now - startTime;

                await LogAuditEventAsync("DeviceEnrolled", new
                {
                    deviceId,
                    success = result.Success,
                    duration = result.Duration.TotalSeconds,
                    errorMessage = result.ErrorMessage
                });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to enroll device {deviceId}");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Duration = DateTime.Now - startTime;
                return result;
            }
        }

        #endregion

        #region Emergency Controls

        /// <summary>
        /// Emergency stop - immediately halt all enrollment operations
        /// </summary>
        public async Task EmergencyStopAsync(string reason = "User-initiated emergency stop")
        {
            _logger.LogWarning($"EMERGENCY STOP: {reason}");

            _executionCancellation?.Cancel();

            if (_currentPlan != null)
            {
                _currentPlan.Status = PlanStatus.Paused;
                await SavePlanAsync(_currentPlan);
            }

            if (_currentProgress != null)
            {
                _currentProgress.IsPaused = true;
                _currentProgress.PauseReason = reason;
                ProgressUpdated?.Invoke(this, _currentProgress);
            }

            await LogAuditEventAsync("EmergencyStop", new { reason });
            StatusChanged?.Invoke(this, $"PAUSED: {reason}");
        }

        /// <summary>
        /// Auto-pause when failure threshold exceeded
        /// </summary>
        private async Task AutoPauseAsync(string reason)
        {
            _logger.LogWarning($"AUTO-PAUSE: {reason}");

            _executionCancellation?.Cancel();

            if (_currentPlan != null)
            {
                _currentPlan.Status = PlanStatus.Paused;
                await SavePlanAsync(_currentPlan);
            }

            if (_currentProgress != null)
            {
                _currentProgress.IsPaused = true;
                _currentProgress.PauseReason = reason;
                ProgressUpdated?.Invoke(this, _currentProgress);
            }

            await LogAuditEventAsync("AutoPause", new { reason });
            StatusChanged?.Invoke(this, $"AUTO-PAUSED: {reason}");
        }

        /// <summary>
        /// Resume paused execution
        /// </summary>
        public async Task ResumeAsync(string planId, string resumedBy)
        {
            _logger.LogInformation($"Resuming plan {planId}");

            if (_currentPlan == null || _currentPlan.PlanId != planId)
                throw new InvalidOperationException("Cannot resume - plan not found");

            if (_currentPlan.Status != PlanStatus.Paused)
                throw new InvalidOperationException("Cannot resume - plan not paused");

            await LogAuditEventAsync("ExecutionResumed", new { planId, resumedBy });

            // Restart execution
            await ExecutePlanAsync(planId);
        }

        #endregion

        #region Progress & Status

        /// <summary>
        /// Get current execution progress
        /// </summary>
        public EnrollmentProgress? GetCurrentProgress()
        {
            return _currentProgress;
        }

        /// <summary>
        /// Get current plan
        /// </summary>
        public EnrollmentPlan? GetCurrentPlan()
        {
            return _currentPlan;
        }

        #endregion

        #region Persistence

        private async Task SavePlanAsync(EnrollmentPlan plan)
        {
            var planPath = Path.Combine(_plansPath, $"{plan.PlanId}.json");
            var json = JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(planPath, json);
        }

        private async Task LogAuditEventAsync(string eventType, object details)
        {
            var auditEntry = new
            {
                timestamp = DateTime.UtcNow,
                eventType,
                details
            };

            var json = JsonSerializer.Serialize(auditEntry);
            await File.AppendAllTextAsync(_auditLogPath, json + Environment.NewLine);
        }

        #endregion
    }

    /// <summary>
    /// Device readiness information for plan generation
    /// </summary>
    public class DeviceReadinessInfo
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public double ReadinessScore { get; set; }
        public string[] Barriers { get; set; } = Array.Empty<string>();
    }
}
