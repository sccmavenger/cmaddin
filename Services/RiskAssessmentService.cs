using System;
using System.Collections.Generic;
using System.Linq;
using ZeroTrustMigrationAddin.Models;

namespace ZeroTrustMigrationAddin.Services
{
    /// <summary>
    /// Phase 2: Risk-based assessment for conditional autonomy
    /// Determines if enrollment should be auto-approved or require human review
    /// </summary>
    public class RiskAssessmentService
    {
        /// <summary>
        /// Assess risk level for a single device enrollment
        /// </summary>
        public RiskAssessment AssessDeviceRisk(DeviceReadiness readiness)
        {
            var assessment = new RiskAssessment
            {
                DeviceId = readiness.DeviceId,
                DeviceName = readiness.DeviceName,
                ReadinessScore = readiness.ReadinessScore
            };

            // Calculate risk score (0-100, lower is better)
            double riskScore = 0;

            // Excellent devices (80+) = Low risk
            if (readiness.ReadinessScore >= 80)
            {
                riskScore = 10;
                assessment.RiskLevel = RiskLevel.Low;
                assessment.RiskFactors.Add("High readiness score");
            }
            // Good devices (60-79) = Medium risk
            else if (readiness.ReadinessScore >= 60)
            {
                riskScore = 30;
                assessment.RiskLevel = RiskLevel.Medium;
                assessment.RiskFactors.Add("Good readiness score with minor issues");
            }
            // Fair devices (40-59) = High risk
            else if (readiness.ReadinessScore >= 40)
            {
                riskScore = 60;
                assessment.RiskLevel = RiskLevel.High;
                assessment.RiskFactors.Add("Fair readiness score - multiple issues");
            }
            // Poor devices (<40) = Critical risk
            else
            {
                riskScore = 90;
                assessment.RiskLevel = RiskLevel.Critical;
                assessment.RiskFactors.Add("Poor readiness score - significant issues");
            }

            // Add issue-specific risks
            foreach (var issue in readiness.Issues)
            {
                if (issue.Contains("Non-compliant"))
                {
                    riskScore += 15;
                    assessment.RiskFactors.Add("Compliance issues present");
                }
                if (issue.Contains("not encrypted"))
                {
                    riskScore += 10;
                    assessment.RiskFactors.Add("Encryption not enabled");
                }
                if (issue.Contains("Outdated"))
                {
                    riskScore += 5;
                    assessment.RiskFactors.Add("Outdated OS version");
                }
            }

            assessment.RiskScore = Math.Min(100, riskScore);

            // Determine if auto-approval is allowed
            assessment.RequiresApproval = DetermineApprovalRequirement(assessment);

            // Generate recommendation
            assessment.Recommendation = GenerateRecommendation(assessment);

            return assessment;
        }

        /// <summary>
        /// Assess risk for a batch of devices
        /// </summary>
        public BatchRiskAssessment AssessBatchRisk(List<DeviceReadiness> devices)
        {
            var batchAssessment = new BatchRiskAssessment
            {
                TotalDevices = devices.Count
            };

            foreach (var device in devices)
            {
                var deviceAssessment = AssessDeviceRisk(device);
                batchAssessment.DeviceAssessments.Add(deviceAssessment);

                // Count by risk level
                switch (deviceAssessment.RiskLevel)
                {
                    case RiskLevel.Low:
                        batchAssessment.LowRiskCount++;
                        break;
                    case RiskLevel.Medium:
                        batchAssessment.MediumRiskCount++;
                        break;
                    case RiskLevel.High:
                        batchAssessment.HighRiskCount++;
                        break;
                    case RiskLevel.Critical:
                        batchAssessment.CriticalRiskCount++;
                        break;
                }
            }

            // Calculate average risk score
            if (batchAssessment.DeviceAssessments.Any())
            {
                batchAssessment.AverageRiskScore = batchAssessment.DeviceAssessments.Average(a => a.RiskScore);
            }

            // Determine overall batch risk level
            batchAssessment.OverallRiskLevel = DetermineBatchRiskLevel(batchAssessment);

            // Determine if batch can be auto-approved
            batchAssessment.RequiresApproval = DetermineBatchApprovalRequirement(batchAssessment);

            // Generate batch recommendation
            batchAssessment.Recommendation = GenerateBatchRecommendation(batchAssessment);

            return batchAssessment;
        }

        /// <summary>
        /// Phase 2: Determine if device requires approval based on risk
        /// </summary>
        private bool DetermineApprovalRequirement(RiskAssessment assessment)
        {
            // Phase 2 Rules:
            // - Low risk (score 80+, batch ≤10): AUTO-APPROVE
            // - Medium risk (score 60-79, batch ≤25): AUTO-APPROVE
            // - High risk (score 40-59): REQUIRE APPROVAL
            // - Critical risk (score <40): BLOCK until issues fixed

            return assessment.RiskLevel switch
            {
                RiskLevel.Low => false,      // Auto-approve excellent devices
                RiskLevel.Medium => false,   // Auto-approve good devices
                RiskLevel.High => true,      // Require approval for fair devices
                RiskLevel.Critical => true,  // Require approval for poor devices
                _ => true
            };
        }

        /// <summary>
        /// Determine if batch requires approval
        /// </summary>
        private bool DetermineBatchApprovalRequirement(BatchRiskAssessment batch)
        {
            // Auto-approve if:
            // 1. All devices are low or medium risk
            // 2. Batch size ≤ 10 devices
            // 3. No critical risk devices

            if (batch.CriticalRiskCount > 0)
                return true;  // Any critical risk = require approval

            if (batch.HighRiskCount > 0 && batch.TotalDevices > 5)
                return true;  // High risk + large batch = require approval

            if (batch.TotalDevices > 10)
                return true;  // Large batches always require approval

            return false;  // Small batches of good devices = auto-approve
        }

        /// <summary>
        /// Determine overall batch risk level
        /// </summary>
        private RiskLevel DetermineBatchRiskLevel(BatchRiskAssessment batch)
        {
            if (batch.CriticalRiskCount > 0)
                return RiskLevel.Critical;

            var highRiskPercentage = (double)batch.HighRiskCount / batch.TotalDevices;
            if (highRiskPercentage > 0.3)  // >30% high risk
                return RiskLevel.High;

            var mediumRiskPercentage = (double)batch.MediumRiskCount / batch.TotalDevices;
            if (mediumRiskPercentage > 0.5)  // >50% medium risk
                return RiskLevel.Medium;

            return RiskLevel.Low;
        }

        private string GenerateRecommendation(RiskAssessment assessment)
        {
            return assessment.RiskLevel switch
            {
                RiskLevel.Low => "✅ Auto-approve: Excellent device ready for immediate enrollment",
                RiskLevel.Medium => "✅ Auto-approve: Good device with minor issues, safe to enroll",
                RiskLevel.High => "⚠️ Review required: Device has issues that should be addressed",
                RiskLevel.Critical => "🛑 Block: Device not ready - resolve critical issues first",
                _ => "Unknown risk level"
            };
        }

        private string GenerateBatchRecommendation(BatchRiskAssessment batch)
        {
            if (!batch.RequiresApproval)
            {
                return $"✅ Auto-approve batch: {batch.TotalDevices} devices, all low/medium risk";
            }

            if (batch.CriticalRiskCount > 0)
            {
                return $"🛑 Approval required: {batch.CriticalRiskCount} critical risk devices need review";
            }

            if (batch.TotalDevices > 10)
            {
                return $"⚠️ Approval required: Large batch ({batch.TotalDevices} devices) needs review";
            }

            return $"⚠️ Approval required: {batch.HighRiskCount} high-risk devices in batch";
        }

        /// <summary>
        /// Calculate recommended batch size based on success rate
        /// Phase 2: Self-adjusting batch sizes
        /// </summary>
        public int CalculateOptimalBatchSize(double recentSuccessRate, int currentBatchSize)
        {
            const int MIN_BATCH_SIZE = 5;
            const int MAX_BATCH_SIZE = 50;

            // Excellent success rate (95%+): Increase by 5
            if (recentSuccessRate >= 0.95)
            {
                return Math.Min(MAX_BATCH_SIZE, currentBatchSize + 5);
            }

            // Good success rate (85-95%): Keep stable
            if (recentSuccessRate >= 0.85)
            {
                return currentBatchSize;
            }

            // Fair success rate (75-85%): Decrease slightly
            if (recentSuccessRate >= 0.75)
            {
                return Math.Max(MIN_BATCH_SIZE, currentBatchSize - 2);
            }

            // Poor success rate (<75%): Decrease significantly
            return Math.Max(MIN_BATCH_SIZE, currentBatchSize - 5);
        }
    }

    // Risk assessment models
    public class RiskAssessment
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public double ReadinessScore { get; set; }
        public double RiskScore { get; set; }
        public RiskLevel RiskLevel { get; set; }
        public List<string> RiskFactors { get; set; } = new();
        public bool RequiresApproval { get; set; }
        public string Recommendation { get; set; } = string.Empty;
    }

    public class BatchRiskAssessment
    {
        public int TotalDevices { get; set; }
        public List<RiskAssessment> DeviceAssessments { get; set; } = new();
        public int LowRiskCount { get; set; }
        public int MediumRiskCount { get; set; }
        public int HighRiskCount { get; set; }
        public int CriticalRiskCount { get; set; }
        public double AverageRiskScore { get; set; }
        public RiskLevel OverallRiskLevel { get; set; }
        public bool RequiresApproval { get; set; }
        public string Recommendation { get; set; } = string.Empty;
    }

    public enum RiskLevel
    {
        Low,       // Score 80+: Auto-approve
        Medium,    // Score 60-79: Auto-approve
        High,      // Score 40-59: Require approval
        Critical   // Score <40: Block until fixed
    }
}
