using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroTrustMigrationAddin.Models;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Services
{
    /// <summary>
    /// Service interface for enrollment analytics computation.
    /// </summary>
    public interface IEnrollmentAnalyticsService
    {
        Task<EnrollmentAnalyticsResult> ComputeAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Core service for computing enrollment analytics including momentum, confidence, 
    /// stall risk, and playbook recommendations.
    /// </summary>
    public class EnrollmentAnalyticsService : IEnrollmentAnalyticsService
    {
        private readonly GraphDataService _graphDataService;
        private readonly ConfigMgrAdminService _configMgrService;
        private readonly EnrollmentScoringOptions _options;

        public EnrollmentAnalyticsService(GraphDataService graphDataService, ConfigMgrAdminService? configMgrService = null)
        {
            _graphDataService = graphDataService ?? throw new ArgumentNullException(nameof(graphDataService));
            _configMgrService = configMgrService ?? new ConfigMgrAdminService();
            _options = EnrollmentScoringOptions.Current;
        }

        /// <summary>
        /// Computes comprehensive enrollment analytics including momentum, confidence, and playbooks.
        /// </summary>
        public async Task<EnrollmentAnalyticsResult> ComputeAsync(CancellationToken ct = default)
        {
            var stopwatch = Stopwatch.StartNew();
            Instance.Info("[ANALYTICS] Starting enrollment analytics computation");

            try
            {
                // Gather base data
                var deviceEnrollment = await _graphDataService.GetDeviceEnrollmentAsync();
                ct.ThrowIfCancellationRequested();

                var result = new EnrollmentAnalyticsResult
                {
                    TotalConfigMgrDevices = deviceEnrollment.TotalDevices,
                    TotalIntuneDevices = deviceEnrollment.IntuneEnrolledDevices,
                    DataSource = "Graph + ConfigMgr"
                };

                // Generate synthetic historical snapshots (in production, this would come from stored data)
                result.Snapshots = GenerateHistoricalSnapshots(result.TotalConfigMgrDevices, result.TotalIntuneDevices);

                // Compute trend analysis
                result.Trend = ComputeTrendAnalysis(result.Snapshots);
                ct.ThrowIfCancellationRequested();

                // Build confidence inputs
                var confidenceInputs = await BuildConfidenceInputsAsync(result, ct);

                // Compute confidence score
                result.Confidence = ComputeConfidenceScore(confidenceInputs);
                ct.ThrowIfCancellationRequested();

                // Assess stall risk
                result.StallRisk = AssessStallRisk(result.EnrolledPct, result.Trend, confidenceInputs.DaysSinceLastEnrollment);

                // Build milestones
                result.Milestones = BuildMilestones(result.EnrolledPct);
                result.NextMilestone = result.Milestones.FirstOrDefault(m => m.IsNext);

                // Generate playbook recommendations
                result.RecommendedPlaybooks = await GeneratePlaybooksAsync(result, ct);
                ct.ThrowIfCancellationRequested();

                // Generate low-risk batch
                result.LowRiskBatch = await GenerateLowRiskBatchAsync(ct);

                stopwatch.Stop();
                result.ComputationTime = stopwatch.Elapsed;
                result.GeneratedAt = DateTime.UtcNow;

                // Log results
                LogAnalyticsResult(result);

                // Track telemetry
                TrackAnalyticsTelemetry(result);

                return result;
            }
            catch (OperationCanceledException)
            {
                Instance.Warning("[ANALYTICS] Computation cancelled");
                throw;
            }
            catch (Exception ex)
            {
                Instance.Error($"[ANALYTICS] Computation failed: {ex.Message}");
                throw;
            }
        }

        #region Trend Analysis

        /// <summary>
        /// Generates historical enrollment snapshots for trend analysis.
        /// In production, this would retrieve stored historical data.
        /// </summary>
        private List<EnrollmentSnapshot> GenerateHistoricalSnapshots(int currentTotal, int currentEnrolled)
        {
            Instance.Info("[ANALYTICS] Generating SYNTHETIC historical snapshots (no stored history available)");
            Instance.Debug($"[ANALYTICS] Synthetic baseline: Total={currentTotal}, Enrolled={currentEnrolled}");
            
            var snapshots = new List<EnrollmentSnapshot>();
            var random = new Random(42); // Deterministic for consistency

            // Generate 90 days of historical data
            for (int daysAgo = 90; daysAgo >= 0; daysAgo--)
            {
                var date = DateTime.UtcNow.Date.AddDays(-daysAgo);
                
                // Simulate growth pattern with some randomness
                double progressFactor = 1.0 - (daysAgo / 100.0);
                int enrolledAtDate = (int)(currentEnrolled * progressFactor * (0.95 + random.NextDouble() * 0.1));
                enrolledAtDate = Math.Max(0, Math.Min(enrolledAtDate, currentEnrolled));

                int newEnrollments = daysAgo < 90 && snapshots.Any() 
                    ? Math.Max(0, enrolledAtDate - snapshots.Last().TotalIntuneDevices)
                    : (int)(random.NextDouble() * 10);

                snapshots.Add(new EnrollmentSnapshot
                {
                    Date = date,
                    TotalConfigMgrDevices = currentTotal,
                    TotalIntuneDevices = enrolledAtDate,
                    NewEnrollmentsCount = newEnrollments
                });
            }

            Instance.Info($"[ANALYTICS] Generated {snapshots.Count} synthetic snapshots spanning 90 days");
            return snapshots;
        }

        /// <summary>
        /// Computes velocity and trend metrics from historical snapshots.
        /// </summary>
        private EnrollmentTrendAnalysis ComputeTrendAnalysis(List<EnrollmentSnapshot> snapshots)
        {
            var analysis = new EnrollmentTrendAnalysis();

            if (snapshots.Count < 7)
            {
                Instance.Warning("[ANALYTICS] Insufficient data for trend analysis");
                analysis.TrendState = TrendState.Unknown;
                return analysis;
            }

            // Calculate rolling velocities
            analysis.Velocity7Day = CalculateRollingVelocity(snapshots, 7);
            analysis.Velocity30 = CalculateRollingVelocity(snapshots, 30);
            analysis.Velocity60 = CalculateRollingVelocity(snapshots, 60);
            analysis.Velocity90 = CalculateRollingVelocity(snapshots, 90);

            // Calculate week-over-week change
            var lastWeekVelocity = CalculateRollingVelocity(snapshots.Take(snapshots.Count - 7).ToList(), 7);
            if (lastWeekVelocity > 0)
            {
                analysis.WeekOverWeekChange = ((analysis.Velocity7Day - lastWeekVelocity) / lastWeekVelocity) * 100;
            }

            // Classify trend state
            analysis.TrendState = ClassifyTrendState(analysis);

            Instance.Debug($"[ANALYTICS] Trend: V7={analysis.Velocity7Day:F2}/day, V30={analysis.Velocity30:F2}/day, " +
                          $"V60={analysis.Velocity60:F2}/day, V90={analysis.Velocity90:F2}/day, State={analysis.TrendState}");

            return analysis;
        }

        private double CalculateRollingVelocity(List<EnrollmentSnapshot> snapshots, int days)
        {
            if (snapshots.Count < days) days = snapshots.Count;
            if (days < 2) return 0;

            var recentSnapshots = snapshots.TakeLast(days).ToList();
            var totalNewEnrollments = recentSnapshots.Sum(s => s.NewEnrollmentsCount);
            
            return (double)totalNewEnrollments / days;
        }

        private TrendState ClassifyTrendState(EnrollmentTrendAnalysis analysis)
        {
            var options = EnrollmentScoringOptions.Current;

            // Check for stall (near-zero velocity)
            if (analysis.Velocity7Day < options.FlatVelocityDeltaThreshold)
            {
                return TrendState.Stalled;
            }

            // Compare short-term to long-term velocity
            var velocityRatio = analysis.Velocity30 > 0 
                ? analysis.Velocity7Day / analysis.Velocity30 
                : 1.0;

            if (velocityRatio > 1.15)
            {
                return TrendState.Accelerating;
            }
            else if (velocityRatio < 0.85)
            {
                return TrendState.Declining;
            }
            else
            {
                return TrendState.Steady;
            }
        }

        #endregion

        #region Confidence Score

        /// <summary>
        /// Builds confidence inputs from available data sources.
        /// </summary>
        private async Task<ConfidenceInputs> BuildConfidenceInputsAsync(EnrollmentAnalyticsResult result, CancellationToken ct)
        {
            Instance.Debug("[ANALYTICS] Building confidence inputs from available data sources");
            
            var inputs = new ConfidenceInputs
            {
                Velocity30 = result.Trend.Velocity30,
                Velocity60 = result.Trend.Velocity60,
                Velocity90 = result.Trend.Velocity90,
                CurrentEnrollmentPct = result.EnrolledPct
            };

            try
            {
                // TODO: Add GetDeviceReadinessAsync to GraphDataService when available
                // For now, assume infrastructure is available if we got enrollment data
                inputs.HasCMG = true;
                inputs.HasCoManagement = true;

                // Estimate days since last enrollment from trend data
                var lastEnrollmentDay = result.Snapshots
                    .Where(s => s.NewEnrollmentsCount > 0)
                    .OrderByDescending(s => s.Date)
                    .FirstOrDefault();
                
                inputs.DaysSinceLastEnrollment = lastEnrollmentDay != null 
                    ? (int)(DateTime.UtcNow - lastEnrollmentDay.Date).TotalDays 
                    : 30;

                // Default values for fields we can't easily query
                inputs.FirstAttemptSuccessRate = 0.85;
                inputs.RequiredAppCount = 8;
                inputs.BlockingESPAppCount = 2;
                inputs.HasAutopilot = true;
                
                Instance.Debug($"[ANALYTICS] Confidence inputs: V30={inputs.Velocity30:F2}, V60={inputs.Velocity60:F2}, V90={inputs.Velocity90:F2}, EnrolledPct={inputs.CurrentEnrollmentPct:F1}%, DaysSinceEnroll={inputs.DaysSinceLastEnrollment}, HasCMG={inputs.HasCMG}, HasCoMgmt={inputs.HasCoManagement}");
            }
            catch (Exception ex)
            {
                Instance.Warning($"[ANALYTICS] Failed to gather extended confidence inputs: {ex.Message}");
                Instance.Debug($"[ANALYTICS] Partial confidence inputs gathered: V30={inputs.Velocity30:F2}, V60={inputs.Velocity60:F2}, EnrolledPct={inputs.CurrentEnrollmentPct:F1}%");
            }

            return inputs;
        }

        /// <summary>
        /// Computes the enrollment confidence score (0-100) based on weighted inputs.
        /// </summary>
        public EnrollmentConfidenceResult ComputeConfidenceScore(ConfidenceInputs inputs)
        {
            var options = EnrollmentScoringOptions.Current;
            var result = new EnrollmentConfidenceResult();
            var drivers = new List<ScoreDriver>();
            var detractors = new List<ScoreDriver>();

            // 1. Velocity Score (0-100, weighted)
            var velocityScore = ComputeVelocityScore(inputs, drivers, detractors);
            result.Breakdown.VelocityScore = velocityScore;

            // 2. Success Rate Score (0-100, weighted)
            var successScore = ComputeSuccessRateScore(inputs, drivers, detractors);
            result.Breakdown.SuccessRateScore = successScore;

            // 3. Complexity Score (0-100, weighted) - Lower complexity = higher score
            var complexityScore = ComputeComplexityScore(inputs, drivers, detractors);
            result.Breakdown.ComplexityScore = complexityScore;

            // 4. Infrastructure Score (0-100, weighted)
            var infraScore = ComputeInfrastructureScore(inputs, drivers, detractors);
            result.Breakdown.InfrastructureScore = infraScore;

            // 5. Conditional Access Score (0-100, weighted)
            var caScore = ComputeConditionalAccessScore(inputs, drivers, detractors);
            result.Breakdown.ConditionalAccessScore = caScore;

            // Calculate weighted total
            var totalScore = (
                (velocityScore * options.VelocityWeight / 100.0) +
                (successScore * options.SuccessRateWeight / 100.0) +
                (complexityScore * options.ComplexityWeight / 100.0) +
                (infraScore * options.InfrastructureWeight / 100.0) +
                (caScore * options.ConditionalAccessWeight / 100.0)
            );

            // Clamp to 0-100
            result.Score = Math.Clamp((int)Math.Round(totalScore), 0, 100);

            // Classify band
            result.Band = result.Score switch
            {
                >= 75 => ConfidenceBand.High,
                >= 50 => ConfidenceBand.Medium,
                _ => ConfidenceBand.Low
            };

            // Select top 3 drivers and top 2 detractors
            result.TopDrivers = drivers.OrderByDescending(d => d.Impact).Take(3).ToList();
            result.TopDetractors = detractors.OrderBy(d => d.Impact).Take(2).ToList();

            // Generate explanation
            result.Explanation = GenerateConfidenceExplanation(result);

            // Store weights in breakdown
            result.Breakdown.VelocityWeight = options.VelocityWeight;
            result.Breakdown.SuccessRateWeight = options.SuccessRateWeight;
            result.Breakdown.ComplexityWeight = options.ComplexityWeight;
            result.Breakdown.InfrastructureWeight = options.InfrastructureWeight;
            result.Breakdown.ConditionalAccessWeight = options.ConditionalAccessWeight;

            // Log breakdown
            Instance.Info($"[SCORING] Confidence Score: {result.Score}/100 ({result.Band})");
            Instance.Debug($"[SCORING] Breakdown: Velocity={velocityScore}, Success={successScore}, " +
                          $"Complexity={complexityScore}, Infra={infraScore}, CA={caScore}");

            return result;
        }

        private int ComputeVelocityScore(ConfidenceInputs inputs, List<ScoreDriver> drivers, List<ScoreDriver> detractors)
        {
            var options = EnrollmentScoringOptions.Current;
            int score = 50; // Baseline

            // Score based on 7-day velocity
            var avgVelocity = (inputs.Velocity30 + inputs.Velocity60 + inputs.Velocity90) / 3;
            
            if (avgVelocity >= options.ExcellentVelocityThreshold)
            {
                score = 100;
                drivers.Add(new ScoreDriver
                {
                    Name = "Excellent Velocity",
                    Description = $"Averaging {avgVelocity:F1} devices/day",
                    Impact = 25,
                    Category = "Velocity"
                });
            }
            else if (avgVelocity >= options.GoodVelocityThreshold)
            {
                score = 75;
                drivers.Add(new ScoreDriver
                {
                    Name = "Good Velocity",
                    Description = $"Averaging {avgVelocity:F1} devices/day",
                    Impact = 15,
                    Category = "Velocity"
                });
            }
            else if (avgVelocity >= 1.0)
            {
                score = 50;
            }
            else
            {
                score = 25;
                detractors.Add(new ScoreDriver
                {
                    Name = "Low Velocity",
                    Description = $"Only {avgVelocity:F1} devices/day",
                    Impact = -20,
                    Category = "Velocity"
                });
            }

            // Penalty for stall
            if (inputs.DaysSinceLastEnrollment > 7)
            {
                var penalty = Math.Min(30, (int)(inputs.DaysSinceLastEnrollment * options.StallDayPenalty));
                score -= penalty;
                detractors.Add(new ScoreDriver
                {
                    Name = "Recent Stall",
                    Description = $"No enrollments in {inputs.DaysSinceLastEnrollment} days",
                    Impact = -penalty,
                    Category = "Velocity"
                });
            }

            return Math.Clamp(score, 0, 100);
        }

        private int ComputeSuccessRateScore(ConfidenceInputs inputs, List<ScoreDriver> drivers, List<ScoreDriver> detractors)
        {
            int score = (int)(inputs.FirstAttemptSuccessRate * 100);

            if (inputs.FirstAttemptSuccessRate >= 0.95)
            {
                drivers.Add(new ScoreDriver
                {
                    Name = "Excellent Success Rate",
                    Description = $"{inputs.FirstAttemptSuccessRate:P0} first-attempt success",
                    Impact = 20,
                    Category = "Success Rate"
                });
            }
            else if (inputs.FirstAttemptSuccessRate < 0.70)
            {
                detractors.Add(new ScoreDriver
                {
                    Name = "Low Success Rate",
                    Description = $"Only {inputs.FirstAttemptSuccessRate:P0} success rate",
                    Impact = -15,
                    Category = "Success Rate"
                });
            }

            // Penalty for retries/duplicates
            if (inputs.EnrollmentRetryCount > 10 || inputs.DuplicateDeviceObjectCount > 5)
            {
                score -= 15;
                detractors.Add(new ScoreDriver
                {
                    Name = "Enrollment Issues",
                    Description = $"{inputs.EnrollmentRetryCount} retries, {inputs.DuplicateDeviceObjectCount} duplicates",
                    Impact = -15,
                    Category = "Success Rate"
                });
            }

            return Math.Clamp(score, 0, 100);
        }

        private int ComputeComplexityScore(ConfidenceInputs inputs, List<ScoreDriver> drivers, List<ScoreDriver> detractors)
        {
            var options = EnrollmentScoringOptions.Current;
            int score = 100; // Start high (low complexity is good)

            // Deduct for required apps
            if (inputs.RequiredAppCount > options.HighComplexityAppCount)
            {
                score -= 30;
                detractors.Add(new ScoreDriver
                {
                    Name = "High App Count",
                    Description = $"{inputs.RequiredAppCount} required apps during enrollment",
                    Impact = -15,
                    Category = "Complexity"
                });
            }
            else if (inputs.RequiredAppCount <= options.LowComplexityAppCount)
            {
                drivers.Add(new ScoreDriver
                {
                    Name = "Low Complexity",
                    Description = $"Only {inputs.RequiredAppCount} required apps",
                    Impact = 10,
                    Category = "Complexity"
                });
            }

            // Deduct for ESP-blocking apps
            if (inputs.BlockingESPAppCount > options.ESPBlockingAppWarningThreshold)
            {
                var penalty = inputs.BlockingESPAppCount * options.ESPBlockingAppPenalty;
                score -= penalty;
                detractors.Add(new ScoreDriver
                {
                    Name = "ESP Blockers",
                    Description = $"{inputs.BlockingESPAppCount} apps blocking ESP",
                    Impact = -penalty,
                    Category = "Complexity"
                });
            }

            return Math.Clamp(score, 0, 100);
        }

        private int ComputeInfrastructureScore(ConfidenceInputs inputs, List<ScoreDriver> drivers, List<ScoreDriver> detractors)
        {
            var options = EnrollmentScoringOptions.Current;
            int score = 50; // Baseline

            if (inputs.HasCMG)
            {
                score += options.CMGBonus;
                drivers.Add(new ScoreDriver
                {
                    Name = "CMG Deployed",
                    Description = "Cloud Management Gateway enables internet enrollment",
                    Impact = options.CMGBonus,
                    Category = "Infrastructure"
                });
            }
            else
            {
                detractors.Add(new ScoreDriver
                {
                    Name = "No CMG",
                    Description = "Missing Cloud Management Gateway",
                    Impact = -10,
                    Category = "Infrastructure"
                });
            }

            if (inputs.HasCoManagement)
            {
                score += options.CoManagementBonus;
                drivers.Add(new ScoreDriver
                {
                    Name = "Co-Management",
                    Description = "Co-management enabled for gradual transition",
                    Impact = options.CoManagementBonus,
                    Category = "Infrastructure"
                });
            }

            if (inputs.HasAutopilot)
            {
                score += options.AutopilotBonus;
                drivers.Add(new ScoreDriver
                {
                    Name = "Autopilot Ready",
                    Description = "Windows Autopilot configured",
                    Impact = options.AutopilotBonus,
                    Category = "Infrastructure"
                });
            }

            return Math.Clamp(score, 0, 100);
        }

        private int ComputeConditionalAccessScore(ConfidenceInputs inputs, List<ScoreDriver> drivers, List<ScoreDriver> detractors)
        {
            var options = EnrollmentScoringOptions.Current;
            int score = 80; // Baseline (some CA is expected)

            if (inputs.HasBlockingCAPolicy)
            {
                score -= options.BlockingCAPenalty;
                detractors.Add(new ScoreDriver
                {
                    Name = "Blocking CA Policy",
                    Description = "Conditional Access may block enrollment",
                    Impact = -options.BlockingCAPenalty,
                    Category = "Conditional Access"
                });
            }

            if (inputs.RequiresMFA && !inputs.HasAutopilot)
            {
                score -= 10;
                detractors.Add(new ScoreDriver
                {
                    Name = "MFA Without Autopilot",
                    Description = "MFA required but Autopilot not configured",
                    Impact = -10,
                    Category = "Conditional Access"
                });
            }

            if (inputs.RiskySignInBlockCount > 0)
            {
                score -= Math.Min(20, inputs.RiskySignInBlockCount * 2);
            }

            return Math.Clamp(score, 0, 100);
        }

        private string GenerateConfidenceExplanation(EnrollmentConfidenceResult result)
        {
            var sb = new StringBuilder();
            
            sb.Append($"Enrollment confidence is {result.Band.ToString().ToLower()}. ");
            
            if (result.TopDrivers.Any())
            {
                sb.Append($"Top factors: {string.Join(", ", result.TopDrivers.Take(2).Select(d => d.Name))}. ");
            }
            
            if (result.TopDetractors.Any())
            {
                sb.Append($"Areas for improvement: {string.Join(", ", result.TopDetractors.Select(d => d.Name))}.");
            }

            return sb.ToString();
        }

        #endregion

        #region Stall Risk Assessment

        /// <summary>
        /// Assesses stall risk including "Trust Trough" detection.
        /// </summary>
        public StallRiskAssessment AssessStallRisk(double enrolledPct, EnrollmentTrendAnalysis trend, int daysSinceLastEnrollment)
        {
            var options = EnrollmentScoringOptions.Current;
            var assessment = new StallRiskAssessment();

            // Check for Trust Trough (50-60% with flat/declining velocity)
            bool inTrustTroughZone = enrolledPct >= options.TrustTroughLowerPct && 
                                     enrolledPct <= options.TrustTroughUpperPct;
            
            bool hasSlowVelocity = trend.TrendState == TrendState.Declining || 
                                   trend.TrendState == TrendState.Stalled ||
                                   trend.TrendState == TrendState.Steady;

            if (inTrustTroughZone && hasSlowVelocity && daysSinceLastEnrollment > 30)
            {
                assessment.IsTrustTroughRisk = true;
                assessment.IsAtRisk = true;
                assessment.RiskLevel = StallRiskLevel.High;
                assessment.RiskDescription = "Trust Trough Risk: Migration has stalled in the critical 50-60% zone where organizational resistance is highest.";
                assessment.DaysAtRisk = daysSinceLastEnrollment;
                assessment.EnrollmentPctAtRiskStart = enrolledPct;
                
                assessment.ContributingFactors.AddRange(new[]
                {
                    "Enrollment velocity has declined or stalled",
                    "Migration is in the 'Trust Trough' zone (50-60%)",
                    $"No significant progress in {daysSinceLastEnrollment} days",
                    "Remaining devices may have higher complexity"
                });

                assessment.RecommendedActions.AddRange(new[]
                {
                    "Run 'Rebuild Momentum' playbook with 20-50 low-risk devices",
                    "Review and reduce ESP blocking applications",
                    "Communicate progress to stakeholders",
                    "Consider hybrid join devices for quick wins"
                });
            }
            else if (trend.TrendState == TrendState.Stalled)
            {
                assessment.IsAtRisk = true;
                assessment.RiskLevel = daysSinceLastEnrollment > options.StallRiskDaysThreshold 
                    ? StallRiskLevel.Critical 
                    : StallRiskLevel.Medium;
                assessment.RiskDescription = $"Enrollment has stalled for {daysSinceLastEnrollment} days.";
                assessment.DaysAtRisk = daysSinceLastEnrollment;
                
                assessment.ContributingFactors.Add("Near-zero enrollment velocity");
                assessment.RecommendedActions.Add("Investigate enrollment blockers");
                assessment.RecommendedActions.Add("Run 'Rebuild Momentum' playbook");
            }
            else if (trend.TrendState == TrendState.Declining)
            {
                assessment.IsAtRisk = true;
                assessment.RiskLevel = StallRiskLevel.Low;
                assessment.RiskDescription = "Enrollment velocity is declining.";
                
                assessment.ContributingFactors.Add("Week-over-week velocity decrease");
                assessment.RecommendedActions.Add("Monitor closely for potential stall");
            }

            Instance.Info($"[ANALYTICS] Stall Risk: {assessment.RiskLevel}, TrustTrough={assessment.IsTrustTroughRisk}");

            return assessment;
        }

        #endregion

        #region Milestones

        /// <summary>
        /// Builds the milestone strip with progress indicators.
        /// </summary>
        private List<EnrollmentMilestone> BuildMilestones(double currentPct)
        {
            var milestones = new List<EnrollmentMilestone>
            {
                new EnrollmentMilestone
                {
                    Percentage = 25,
                    Name = "Early Adoption",
                    Description = "Initial rollout complete",
                    WhatChanges = "Pilot group feedback available, ready to expand scope",
                    RecommendedPlaybook = "ScaleUp",
                    IsAchieved = currentPct >= 25,
                    IsCurrent = currentPct >= 25 && currentPct < 50
                },
                new EnrollmentMilestone
                {
                    Percentage = 50,
                    Name = "Trust Trough Entry",
                    Description = "Entering the challenging middle phase",
                    WhatChanges = "Expect increased resistance, focus on communication",
                    RecommendedPlaybook = "RebuildMomentum",
                    IsTrustTrough = true,
                    IsAchieved = currentPct >= 50,
                    IsCurrent = currentPct >= 50 && currentPct < 60
                },
                new EnrollmentMilestone
                {
                    Percentage = 60,
                    Name = "Trust Trough Exit",
                    Description = "Exiting the challenging phase",
                    WhatChanges = "Momentum typically accelerates, remaining devices easier",
                    RecommendedPlaybook = "ScaleUp",
                    IsTrustTrough = true,
                    IsAchieved = currentPct >= 60,
                    IsCurrent = currentPct >= 60 && currentPct < 70
                },
                new EnrollmentMilestone
                {
                    Percentage = 70,
                    Name = "Majority Enrolled",
                    Description = "Over two-thirds complete",
                    WhatChanges = "Can begin decommissioning legacy infrastructure",
                    RecommendedPlaybook = "ReduceDependencies",
                    IsAchieved = currentPct >= 70,
                    IsCurrent = currentPct >= 70 && currentPct < 85
                },
                new EnrollmentMilestone
                {
                    Percentage = 85,
                    Name = "Final Push",
                    Description = "Nearing completion",
                    WhatChanges = "Focus on remaining complex devices, plan cleanup",
                    RecommendedPlaybook = "AutopilotHygiene",
                    IsAchieved = currentPct >= 85,
                    IsCurrent = currentPct >= 85 && currentPct < 100
                },
                new EnrollmentMilestone
                {
                    Percentage = 100,
                    Name = "Migration Complete",
                    Description = "All devices enrolled",
                    WhatChanges = "Ready for ConfigMgr decommissioning",
                    RecommendedPlaybook = "",
                    IsAchieved = currentPct >= 100,
                    IsCurrent = currentPct >= 100
                }
            };

            // Mark the next milestone
            var nextMilestone = milestones.FirstOrDefault(m => !m.IsAchieved);
            if (nextMilestone != null)
            {
                nextMilestone.IsNext = true;
            }

            return milestones;
        }

        #endregion

        #region Playbooks

        /// <summary>
        /// Generates recommended playbooks based on current state.
        /// </summary>
        private async Task<List<EnrollmentPlaybook>> GeneratePlaybooksAsync(EnrollmentAnalyticsResult result, CancellationToken ct)
        {
            var playbooks = new List<EnrollmentPlaybook>();

            // Always include "Rebuild Momentum" playbook
            playbooks.Add(CreateRebuildMomentumPlaybook(result));

            // Add "Reduce Dependencies" if complexity is high
            if (result.Confidence.Breakdown.ComplexityScore < 70)
            {
                playbooks.Add(CreateReduceDependenciesPlaybook());
            }

            // Add "Autopilot Hygiene" playbook
            playbooks.Add(CreateAutopilotHygienePlaybook());

            // Mark recommended playbook based on state
            if (result.StallRisk.IsTrustTroughRisk || result.StallRisk.RiskLevel >= StallRiskLevel.Medium)
            {
                var rebuildPlaybook = playbooks.FirstOrDefault(p => p.Type == PlaybookType.RebuildMomentum);
                if (rebuildPlaybook != null)
                {
                    rebuildPlaybook.IsRecommended = true;
                    rebuildPlaybook.RecommendationReason = "Recommended to rebuild enrollment momentum and exit stall";
                }
            }
            else if (result.Confidence.Breakdown.ComplexityScore < 60)
            {
                var reducePlaybook = playbooks.FirstOrDefault(p => p.Type == PlaybookType.ReduceDependencies);
                if (reducePlaybook != null)
                {
                    reducePlaybook.IsRecommended = true;
                    reducePlaybook.RecommendationReason = "Recommended to reduce enrollment complexity";
                }
            }

            return playbooks;
        }

        private EnrollmentPlaybook CreateRebuildMomentumPlaybook(EnrollmentAnalyticsResult result)
        {
            var options = EnrollmentScoringOptions.Current;
            var batchSize = Math.Min(options.MaxLowRiskBatchSize, Math.Max(options.MinLowRiskBatchSize, result.Gap / 10));

            return new EnrollmentPlaybook
            {
                Name = "Rebuild Momentum (Low-Risk Batch)",
                Description = $"Enroll {batchSize} low-risk devices to restore enrollment velocity safely.",
                Type = PlaybookType.RebuildMomentum,
                RiskLevel = PlaybookRiskLevel.Low,
                EstimatedTime = "1-2 hours",
                ExpectedImpactDevices = batchSize,
                Prerequisites = new List<string>
                {
                    "Co-management enabled",
                    "CMG deployed (for remote devices)",
                    "Intune enrollment configured"
                },
                Steps = new List<PlaybookStep>
                {
                    new PlaybookStep
                    {
                        Order = 1,
                        Title = "Review Device Selection",
                        Description = "Review the auto-selected low-risk devices. Criteria: healthy, compliant, recent check-in, no BitLocker issues.",
                        ActionType = "Review",
                        Checklist = new List<string>
                        {
                            "Verify device count matches expectations",
                            "Check for any VIP/executive devices",
                            "Confirm no production-critical servers included"
                        },
                        ExpectedOutcome = $"Validated list of {batchSize} devices ready for enrollment",
                        RequiresConfirmation = false
                    },
                    new PlaybookStep
                    {
                        Order = 2,
                        Title = "Verify Enrollment Prerequisites",
                        Description = "Confirm Azure AD Hybrid Join status and co-management settings.",
                        ActionType = "Verify",
                        PortalLink = "https://intune.microsoft.com/#view/Microsoft_Intune_DeviceSettings/DevicesMenu/~/enrollmentPolicies",
                        Checklist = new List<string>
                        {
                            "Automatic MDM enrollment enabled",
                            "Co-management authority set correctly",
                            "No blocking Conditional Access policies"
                        },
                        ExpectedOutcome = "All prerequisites verified"
                    },
                    new PlaybookStep
                    {
                        Order = 3,
                        Title = "Initiate Enrollment (Dry Run)",
                        Description = "Review enrollment plan without executing. Creates collection but does not deploy.",
                        ActionType = "Execute",
                        RequiresConfirmation = true,
                        Checklist = new List<string>
                        {
                            "Export device list to CSV for records",
                            "Create device collection in ConfigMgr",
                            "Verify collection membership is correct"
                        },
                        ExpectedOutcome = "Device collection created, ready for deployment",
                        RollbackInstructions = "Delete the device collection from ConfigMgr"
                    },
                    new PlaybookStep
                    {
                        Order = 4,
                        Title = "Execute Enrollment",
                        Description = "Deploy co-management policy to trigger Intune enrollment.",
                        ActionType = "Execute",
                        RequiresConfirmation = true,
                        PortalLink = "https://intune.microsoft.com/#view/Microsoft_Intune_DeviceSettings/DevicesMenu/~/overview",
                        Checklist = new List<string>
                        {
                            "Deploy co-management settings to collection",
                            "Monitor enrollment status in Intune portal",
                            "Check for enrollment errors in Event Viewer"
                        },
                        ExpectedOutcome = $"~{batchSize * 0.95:F0} devices enrolled successfully (95%+ success rate)",
                        RollbackInstructions = "Remove devices from collection to stop enrollment"
                    },
                    new PlaybookStep
                    {
                        Order = 5,
                        Title = "Verify & Document",
                        Description = "Confirm enrollment success and document results.",
                        ActionType = "Verify",
                        Checklist = new List<string>
                        {
                            "Verify device count in Intune matches expectations",
                            "Check compliance status of enrolled devices",
                            "Document any failures for investigation",
                            "Update enrollment tracker/dashboard"
                        },
                        ExpectedOutcome = "Enrollment verified, velocity restored"
                    }
                }
            };
        }

        private EnrollmentPlaybook CreateReduceDependenciesPlaybook()
        {
            return new EnrollmentPlaybook
            {
                Name = "Reduce Enrollment-Time Dependencies",
                Description = "Reduce ESP blocking apps and streamline the enrollment experience.",
                Type = PlaybookType.ReduceDependencies,
                RiskLevel = PlaybookRiskLevel.Medium,
                EstimatedTime = "2-4 hours",
                Prerequisites = new List<string>
                {
                    "Access to Intune admin portal",
                    "List of required apps during enrollment",
                    "Stakeholder approval for changes"
                },
                Steps = new List<PlaybookStep>
                {
                    new PlaybookStep
                    {
                        Order = 1,
                        Title = "Audit ESP Configuration",
                        Description = "Review Enrollment Status Page settings and blocking apps.",
                        ActionType = "Review",
                        PortalLink = "https://intune.microsoft.com/#view/Microsoft_Intune_DeviceSettings/DevicesEnrollmentMenu/~/windowsEnrollment",
                        Checklist = new List<string>
                        {
                            "List all apps tracked by ESP",
                            "Identify apps blocking device use",
                            "Note current ESP timeout settings"
                        },
                        ExpectedOutcome = "Complete list of ESP dependencies"
                    },
                    new PlaybookStep
                    {
                        Order = 2,
                        Title = "Classify Apps by Criticality",
                        Description = "Determine which apps are truly required during enrollment vs can be installed later.",
                        ActionType = "Review",
                        Checklist = new List<string>
                        {
                            "Security/AV apps - Keep required",
                            "VPN apps - Keep required if needed for connectivity",
                            "LOB apps - Consider making available, not required",
                            "Microsoft 365 Apps - Consider deploying post-enrollment"
                        },
                        ExpectedOutcome = "Prioritized app list with enrollment vs post-enrollment classification"
                    },
                    new PlaybookStep
                    {
                        Order = 3,
                        Title = "Update App Assignments",
                        Description = "Change non-critical apps from Required to Available during ESP.",
                        ActionType = "Configure",
                        RequiresConfirmation = true,
                        PortalLink = "https://intune.microsoft.com/#view/Microsoft_Intune_Apps/MainMenu/~/windowsApps",
                        Checklist = new List<string>
                        {
                            "For each non-critical app:",
                            "  - Remove from ESP tracking",
                            "  - Or change assignment to Available",
                            "Document changes made"
                        },
                        ExpectedOutcome = "Reduced ESP app count",
                        RollbackInstructions = "Re-add apps to Required assignment"
                    },
                    new PlaybookStep
                    {
                        Order = 4,
                        Title = "Test Enrollment Experience",
                        Description = "Enroll a test device to verify improved experience.",
                        ActionType = "Execute",
                        Checklist = new List<string>
                        {
                            "Wipe or reset a test device",
                            "Complete Autopilot/enrollment flow",
                            "Measure time to desktop",
                            "Verify critical apps installed"
                        },
                        ExpectedOutcome = "Faster enrollment, same security baseline"
                    }
                }
            };
        }

        private EnrollmentPlaybook CreateAutopilotHygienePlaybook()
        {
            return new EnrollmentPlaybook
            {
                Name = "Autopilot Hygiene",
                Description = "Optimize Autopilot configuration and consider Entra Join for new devices.",
                Type = PlaybookType.AutopilotHygiene,
                RiskLevel = PlaybookRiskLevel.Low,
                EstimatedTime = "1-2 hours",
                Prerequisites = new List<string>
                {
                    "Windows Autopilot configured",
                    "Access to Intune admin portal"
                },
                Steps = new List<PlaybookStep>
                {
                    new PlaybookStep
                    {
                        Order = 1,
                        Title = "Review Current Profiles",
                        Description = "Audit existing Autopilot deployment profiles.",
                        ActionType = "Review",
                        PortalLink = "https://intune.microsoft.com/#view/Microsoft_Intune_DeviceSettings/DevicesEnrollmentMenu/~/windowsDeploymentProfiles",
                        Checklist = new List<string>
                        {
                            "List all Autopilot profiles",
                            "Note join type for each (Hybrid vs Azure AD)",
                            "Check ESP configuration per profile",
                            "Review assignment groups"
                        },
                        ExpectedOutcome = "Complete Autopilot profile inventory"
                    },
                    new PlaybookStep
                    {
                        Order = 2,
                        Title = "Assess Hybrid Join Complexity",
                        Description = "Evaluate if Hybrid Join is adding unnecessary complexity.",
                        ActionType = "Review",
                        Checklist = new List<string>
                        {
                            "Does organization require on-prem AD access?",
                            "Are there on-prem resources needing Kerberos?",
                            "Is VPN required during OOBE?",
                            "⚠️ WARNING: Hybrid Join + ESP = complex setup"
                        },
                        ExpectedOutcome = "Decision on Hybrid vs Azure AD join strategy"
                    },
                    new PlaybookStep
                    {
                        Order = 3,
                        Title = "Create Entra Join Test Profile",
                        Description = "Create a pure Azure AD join profile for testing.",
                        ActionType = "Configure",
                        RequiresConfirmation = true,
                        IsOptional = true,
                        PortalLink = "https://intune.microsoft.com/#view/Microsoft_Intune_DeviceSettings/DevicesEnrollmentMenu/~/windowsDeploymentProfiles",
                        Checklist = new List<string>
                        {
                            "Create new Autopilot profile",
                            "Select 'Azure AD joined' (not Hybrid)",
                            "Configure user-driven or self-deploying",
                            "Assign to test group only"
                        },
                        ExpectedOutcome = "Test profile ready for pilot",
                        RollbackInstructions = "Delete the test profile"
                    },
                    new PlaybookStep
                    {
                        Order = 4,
                        Title = "Test with Pilot Batch",
                        Description = "Enroll 5-10 test devices with new profile.",
                        ActionType = "Execute",
                        RequiresConfirmation = true,
                        Checklist = new List<string>
                        {
                            "Select pilot devices (non-production)",
                            "Register devices with new profile",
                            "Complete Autopilot enrollment",
                            "Verify all apps and policies apply",
                            "Test access to required resources"
                        },
                        ExpectedOutcome = "Validated simpler enrollment path"
                    }
                }
            };
        }

        #endregion

        #region Low-Risk Batch Generation

        /// <summary>
        /// Generates a batch of low-risk devices for the "Rebuild Momentum" playbook.
        /// </summary>
        private async Task<PlaybookEnrollmentBatch?> GenerateLowRiskBatchAsync(CancellationToken ct)
        {
            try
            {
                // TODO: When GetDeviceReadinessAsync is available in GraphDataService, use real data
                // For now, return mock batch to demonstrate the feature
                Instance.Info("[ANALYTICS] Generating mock low-risk batch (real device readiness API not yet available)");
                await Task.Delay(100, ct); // Simulate async work
                return CreateMockLowRiskBatch();
            }
            catch (Exception ex)
            {
                Instance.Error($"[ANALYTICS] Failed to generate low-risk batch: {ex.Message}");
                return CreateMockLowRiskBatch();
            }
        }

        private PlaybookEnrollmentBatch CreateMockLowRiskBatch()
        {
            var options = EnrollmentScoringOptions.Current;
            var random = new Random(42);
            var candidates = new List<DeviceCandidate>();

            for (int i = 0; i < options.MinLowRiskBatchSize; i++)
            {
                var deviceName = $"DESKTOP-{random.Next(1000, 9999)}";
                candidates.Add(new DeviceCandidate
                {
                    DeviceId = Guid.NewGuid().ToString(),
                    DeviceName = deviceName,
                    DeviceNameHashed = HashDeviceName(deviceName),
                    ReadinessScore = 80 + random.NextDouble() * 20,
                    IsCompliant = true,
                    HasRecentCheckIn = true,
                    LastCheckIn = DateTime.UtcNow.AddDays(-random.Next(1, 5)),
                    HasBitLockerRecovery = true,
                    OperatingSystem = "Windows 11 23H2"
                });
            }

            return new PlaybookEnrollmentBatch
            {
                Name = "Low-Risk Enrollment Batch (Sample)",
                Devices = candidates,
                AverageReadinessScore = candidates.Average(c => c.ReadinessScore),
                RiskLevel = PlaybookRiskLevel.Low,
                SelectionCriteria = "Sample data - connect to Graph API for real devices"
            };
        }

        private string HashDeviceName(string deviceName)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(deviceName));
            return Convert.ToHexString(bytes)[..12];
        }

        #endregion

        #region Logging & Telemetry

        private void LogAnalyticsResult(EnrollmentAnalyticsResult result)
        {
            Instance.Info($"[ANALYTICS] ===== ENROLLMENT ANALYTICS RESULT =====");
            Instance.Info($"[ANALYTICS] Devices: {result.TotalIntuneDevices}/{result.TotalConfigMgrDevices} " +
                         $"({result.EnrolledPct:F1}%), Gap: {result.Gap}");
            Instance.Info($"[ANALYTICS] Trend: {result.Trend.TrendState}, Velocity: {result.Trend.DevicesPerWeek:F1}/week");
            Instance.Info($"[ANALYTICS] Confidence: {result.Confidence.Score}/100 ({result.Confidence.Band})");
            Instance.Info($"[ANALYTICS] Stall Risk: {result.StallRisk.RiskLevel}, TrustTrough: {result.StallRisk.IsTrustTroughRisk}");
            Instance.Info($"[ANALYTICS] Next Milestone: {result.NextMilestone?.Name ?? "None"} ({result.NextMilestone?.Percentage}%)");
            Instance.Info($"[ANALYTICS] Playbooks: {result.RecommendedPlaybooks.Count} recommended");
            Instance.Info($"[ANALYTICS] Computation time: {result.ComputationTime.TotalMilliseconds:F0}ms");
            Instance.Info($"[ANALYTICS] ==========================================");
        }

        private void TrackAnalyticsTelemetry(EnrollmentAnalyticsResult result)
        {
            try
            {
                AzureTelemetryService.Instance.TrackEvent("EnrollmentAnalyticsComputed", 
                    new Dictionary<string, string>
                    {
                        { "TrendState", result.Trend.TrendState.ToString() },
                        { "ConfidenceBand", result.Confidence.Band.ToString() },
                        { "StallRiskLevel", result.StallRisk.RiskLevel.ToString() },
                        { "IsTrustTroughRisk", result.StallRisk.IsTrustTroughRisk.ToString() },
                        { "NextMilestone", result.NextMilestone?.Name ?? "Complete" },
                        { "PlaybookCount", result.RecommendedPlaybooks.Count.ToString() }
                    },
                    new Dictionary<string, double>
                    {
                        { "EnrolledPct", result.EnrolledPct },
                        { "ConfidenceScore", result.Confidence.Score },
                        { "Velocity7Day", result.Trend.Velocity7Day },
                        { "ComputationTimeMs", result.ComputationTime.TotalMilliseconds }
                    });
            }
            catch (Exception ex)
            {
                Instance.Warning($"[ANALYTICS] Failed to track telemetry: {ex.Message}");
            }
        }

        #endregion
    }
}
