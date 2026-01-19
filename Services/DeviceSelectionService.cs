using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroTrustMigrationAddin.Models;

namespace ZeroTrustMigrationAddin.Services
{
    /// <summary>
    /// Intelligent device selection for enrollment.
    /// Suggests which specific devices to enroll based on readiness scoring.
    /// </summary>
    public class DeviceSelectionService
    {
        private readonly GraphDataService _graphService;
        private readonly FileLogger _fileLogger;

        public DeviceSelectionService(GraphDataService graphService)
        {
            _graphService = graphService;
            _fileLogger = FileLogger.Instance;
        }

        /// <summary>
        /// Analyzes unenrolled devices and suggests best candidates for next enrollment batch
        /// </summary>
        public async Task<List<AIRecommendation>> SuggestDevicesForEnrollmentAsync(
            List<Device> unenrolledDevices, 
            int batchSize = 50)
        {
            _fileLogger.Log(FileLogger.LogLevel.Info, 
                $"Analyzing {unenrolledDevices.Count} unenrolled devices for readiness scoring");

            var recommendations = new List<AIRecommendation>();

            if (!unenrolledDevices.Any())
            {
                recommendations.Add(new AIRecommendation
                {
                    Title = "✅ All Devices Enrolled!",
                    Description = "No unenrolled devices remaining. Migration complete!",
                    Priority = RecommendationPriority.Low,
                    Category = RecommendationCategory.DeviceEnrollment
                });
                return recommendations;
            }

            // Score all devices
            var scoredDevices = unenrolledDevices
                .Select(d => new
                {
                    Device = d,
                    Score = CalculateEnrollmentReadiness(d),
                    Reasoning = GetReadinessReasoning(d)
                })
                .OrderByDescending(d => d.Score.TotalScore)
                .ToList();

            _fileLogger.Log(FileLogger.LogLevel.Info, 
                $"Device scoring complete. Avg score: {scoredDevices.Average(d => d.Score.TotalScore):F1}");
            
            // Log score distribution for diagnostics
            var excellent = scoredDevices.Count(d => d.Score.TotalScore >= 80);
            var good = scoredDevices.Count(d => d.Score.TotalScore >= 60 && d.Score.TotalScore < 80);
            var fair = scoredDevices.Count(d => d.Score.TotalScore >= 40 && d.Score.TotalScore < 60);
            var poor = scoredDevices.Count(d => d.Score.TotalScore < 40);
            _fileLogger.Log(FileLogger.LogLevel.Debug, 
                $"[SELECTION] Score distribution: Excellent(80+)={excellent}, Good(60-79)={good}, Fair(40-59)={fair}, Poor(<40)={poor}");
            _fileLogger.Log(FileLogger.LogLevel.Debug, 
                $"[SELECTION] Score range: Min={scoredDevices.Min(d => d.Score.TotalScore):F0}, Max={scoredDevices.Max(d => d.Score.TotalScore):F0}");

            // Top candidates for enrollment
            var topCandidates = scoredDevices.Take(batchSize).ToList();
            var mediumCandidates = scoredDevices.Skip(batchSize).Take(batchSize).ToList();
            var lowPriorityCandidates = scoredDevices.Skip(batchSize * 2).ToList();
            
            _fileLogger.Log(FileLogger.LogLevel.Debug, 
                $"[SELECTION] Batch sizes: TopCandidates={topCandidates.Count}, Medium={mediumCandidates.Count}, LowPriority={lowPriorityCandidates.Count}");

            // Recommendation 1: Top batch ready to enroll
            if (topCandidates.Any())
            {
                recommendations.Add(new AIRecommendation
                {
                    Title = $"🎯 Next Batch Ready: {topCandidates.Count} High-Readiness Devices",
                    Description = $"These devices have high readiness scores (avg: {topCandidates.Average(d => d.Score.TotalScore):F0}/100). " +
                                  "Optimal candidates for immediate enrollment.",
                    Rationale = "Prioritizing high-readiness devices increases enrollment success rate and reduces troubleshooting time.",
                    ActionSteps = new List<string>
                    {
                        "1. Create Azure AD group: 'Intune-Enrollment-Batch-Next'",
                        $"2. Add these devices: {string.Join(", ", topCandidates.Take(10).Select(d => d.Device.DeviceName))}",
                        topCandidates.Count > 10 ? $"   ... and {topCandidates.Count - 10} more (see exported list)" : "",
                        "3. Enable auto-enrollment via ConfigMgr client settings or GPO",
                        "4. Monitor enrollment status in Intune portal for 48 hours",
                        "5. Once validated, proceed to next batch"
                    }.Where(s => !string.IsNullOrEmpty(s)).ToList(),
                    Priority = RecommendationPriority.High,
                    Category = RecommendationCategory.DeviceEnrollment,
                    ImpactScore = 90,
                    EstimatedEffort = $"2-3 days to enroll {topCandidates.Count} devices",
                    ResourceLinks = new List<string>
                    {
                        "Export device list: enrollment_batch_high_priority.csv"
                    }
                });
            }

            // Recommendation 2: Address barriers for medium-priority devices
            if (mediumCandidates.Any())
            {
                var commonBarriers = IdentifyCommonBarriers(mediumCandidates.Select(d => d.Device).ToList());
                
                recommendations.Add(new AIRecommendation
                {
                    Title = $"⚠️ {mediumCandidates.Count} Devices Need Preparation",
                    Description = $"Medium readiness scores (avg: {mediumCandidates.Average(d => d.Score.TotalScore):F0}/100). " +
                                  "Address common barriers to improve readiness.",
                    Rationale = "Fixing common issues before enrollment reduces failure rate and support tickets.",
                    ActionSteps = commonBarriers,
                    Priority = RecommendationPriority.Medium,
                    Category = RecommendationCategory.DeviceEnrollment,
                    ImpactScore = 70,
                    EstimatedEffort = "1-2 weeks to prepare devices"
                });
            }

            // Recommendation 3: Postpone low-priority devices
            if (lowPriorityCandidates.Any())
            {
                var riskFactors = IdentifyRiskFactors(lowPriorityCandidates.Select(d => d.Device).ToList());

                recommendations.Add(new AIRecommendation
                {
                    Title = $"📋 {lowPriorityCandidates.Count} Devices: Enroll Last",
                    Description = $"Low readiness scores (avg: {lowPriorityCandidates.Average(d => d.Score.TotalScore):F0}/100). " +
                                  "These are higher-risk and should be enrolled in final waves.",
                    Rationale = "VIP devices, offline devices, and those with special configurations require extra planning.",
                    ActionSteps = riskFactors,
                    Priority = RecommendationPriority.Low,
                    Category = RecommendationCategory.DeviceEnrollment,
                    ImpactScore = 40,
                    EstimatedEffort = "Save for final phase"
                });
            }

            // Recommendation 4: Motivational progress update
            var totalDevices = unenrolledDevices.Count;
            var readyToEnroll = scoredDevices.Count(d => d.Score.TotalScore >= 70);
            
            recommendations.Add(new AIRecommendation
            {
                Title = $"📊 Readiness Summary: {readyToEnroll} of {totalDevices} Ready",
                Description = $"{(readyToEnroll / (double)totalDevices * 100):F0}% of unenrolled devices have good readiness scores. " +
                              "Strong foundation for successful enrollment.",
                ActionSteps = new List<string>
                {
                    $"✅ {scoredDevices.Count(d => d.Score.TotalScore >= 80)} devices: Excellent (80+ score)",
                    $"✅ {scoredDevices.Count(d => d.Score.TotalScore >= 60 && d.Score.TotalScore < 80)} devices: Good (60-79 score)",
                    $"⚠️ {scoredDevices.Count(d => d.Score.TotalScore < 60)} devices: Needs attention (<60 score)",
                    "",
                    "Recommendation: Enroll high-scoring devices first to build momentum"
                },
                Priority = RecommendationPriority.Low,
                Category = RecommendationCategory.General,
                ImpactScore = 50
            });

            return recommendations;
        }

        /// <summary>
        /// Calculates enrollment readiness score for a device (0-100)
        /// </summary>
        private DeviceReadinessScore CalculateEnrollmentReadiness(Device device)
        {
            var score = new DeviceReadinessScore { DeviceName = device.DeviceName };

            // Positive factors
            if (device.OSVersion?.StartsWith("10.0.19041") == true || // Windows 10 2004+
                device.OSVersion?.StartsWith("10.0.22") == true)      // Windows 11
            {
                score.OSVersionScore = 30;
                score.Factors.Add("✅ Modern OS version");
            }
            else if (device.OSVersion?.StartsWith("10.0.17") == true) // Windows 10 1809+
            {
                score.OSVersionScore = 20;
                score.Factors.Add("⚠️ Older but supported OS");
            }

            if (device.IsAzureADJoined)
            {
                score.AADJoinScore = 40;
                score.Factors.Add("✅ Azure AD joined");
            }
            else
            {
                score.Factors.Add("❌ Not Azure AD joined (blocker)");
            }

            if (device.LastSeenDays < 7)
            {
                score.OnlineScore = 20;
                score.Factors.Add("✅ Recently online");
            }
            else if (device.LastSeenDays < 30)
            {
                score.OnlineScore = 10;
                score.Factors.Add("⚠️ Seen within 30 days");
            }
            else
            {
                score.Factors.Add("❌ Offline >30 days");
            }

            if (device.IsCompliant)
            {
                score.ComplianceScore = 10;
                score.Factors.Add("✅ Currently compliant");
            }

            // Negative factors
            if (device.UserPrincipalName?.Contains("admin") == true || 
                device.UserPrincipalName?.Contains("vip") == true ||
                device.DeviceName?.Contains("EXEC") == true)
            {
                score.RiskScore = -30;
                score.Factors.Add("⚠️ VIP device - high risk");
            }

            if (device.LastSeenDays > 30)
            {
                score.RiskScore -= 50;
            }

            if (!device.IsAzureADJoined)
            {
                score.RiskScore -= 40; // Major blocker
            }

            _fileLogger.Log(FileLogger.LogLevel.Debug, 
                $"Device {device.DeviceName}: Score {score.TotalScore} (OS:{score.OSVersionScore}, AAD:{score.AADJoinScore}, Online:{score.OnlineScore}, Risk:{score.RiskScore})");

            return score;
        }

        private string GetReadinessReasoning(Device device)
        {
            var reasons = new List<string>();

            if (device.IsAzureADJoined) reasons.Add("Azure AD joined");
            if (device.LastSeenDays < 7) reasons.Add("Recently online");
            if (device.OSVersion?.StartsWith("10.0.19041") == true) reasons.Add("Modern OS");
            if (!device.IsAzureADJoined) reasons.Add("NOT Azure AD joined");
            if (device.LastSeenDays > 30) reasons.Add("Offline >30 days");

            return string.Join(", ", reasons);
        }

        private List<string> IdentifyCommonBarriers(List<Device> devices)
        {
            var barriers = new List<string>();
            
            var notAADJoined = devices.Count(d => !d.IsAzureADJoined);
            if (notAADJoined > 0)
                barriers.Add($"1. {notAADJoined} devices not Azure AD joined - run hybrid join sync");

            var offline = devices.Count(d => d.LastSeenDays > 14);
            if (offline > 0)
                barriers.Add($"2. {offline} devices offline >14 days - reach out to device owners");

            var oldOS = devices.Count(d => !d.OSVersion?.StartsWith("10.0.19041") == true);
            if (oldOS > 0)
                barriers.Add($"3. {oldOS} devices on older OS - schedule Windows updates");

            if (barriers.Count == 0)
                barriers.Add("No major barriers identified. Proceed with enrollment.");

            return barriers;
        }

        private List<string> IdentifyRiskFactors(List<Device> devices)
        {
            var risks = new List<string>();

            var vipDevices = devices.Count(d => 
                d.UserPrincipalName?.Contains("admin") == true || 
                d.DeviceName?.Contains("EXEC") == true);
            if (vipDevices > 0)
                risks.Add($"1. {vipDevices} VIP/executive devices - schedule dedicated support window");

            var offlineDevices = devices.Count(d => d.LastSeenDays > 30);
            if (offlineDevices > 0)
                risks.Add($"2. {offlineDevices} devices offline >30 days - verify still in use");

            var notAAD = devices.Count(d => !d.IsAzureADJoined);
            if (notAAD > 0)
                risks.Add($"3. {notAAD} devices missing Azure AD join - requires remediation first");

            if (risks.Count == 0)
                risks.Add("Low-risk devices. Can enroll in standard waves.");

            return risks;
        }
    }

    #region Models

    public class DeviceReadinessScore
    {
        public string DeviceName { get; set; } = string.Empty;
        public int OSVersionScore { get; set; }
        public int AADJoinScore { get; set; }
        public int OnlineScore { get; set; }
        public int ComplianceScore { get; set; }
        public int RiskScore { get; set; }
        public List<string> Factors { get; set; } = new List<string>();

        public int TotalScore => Math.Max(0, Math.Min(100, 
            OSVersionScore + AADJoinScore + OnlineScore + ComplianceScore + RiskScore));

        public string ReadinessLevel
        {
            get
            {
                if (TotalScore >= 80) return "Excellent";
                if (TotalScore >= 60) return "Good";
                if (TotalScore >= 40) return "Fair";
                return "Poor";
            }
        }
    }

    // Placeholder Device model (will use existing from Models namespace)
    public class Device
    {
        public string DeviceName { get; set; } = string.Empty;
        public string UserPrincipalName { get; set; } = string.Empty;
        public string OSVersion { get; set; } = string.Empty;
        public bool IsAzureADJoined { get; set; }
        public int LastSeenDays { get; set; }
        public bool IsCompliant { get; set; }
    }

    #endregion
}
