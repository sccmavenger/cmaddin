using System;
using System.Collections.Generic;
using System.Linq;
using ZeroTrustMigrationAddin.Models;
using ZeroTrustMigrationAddin.Services.Pipeline;

namespace ZeroTrustMigrationAddin.Services
{
    /// <summary>
    /// Generates Decision Cards and Tier 1 Ideas content by synthesizing existing data.
    /// No new API calls — purely transforms data already collected by the pipeline and ViewModel.
    /// </summary>
    public class DecisionCardGenerator
    {
        /// <summary>
        /// Generate one Decision Card per workload based on current state.
        /// </summary>
        public List<DecisionCard> GenerateDecisionCards(
            IList<Workload> workloads,
            WorkloadStallAssessment? stallAssessment,
            WorkloadMomentumInsight? momentumInsight,
            int nearCloudNativeCount)
        {
            var cards = new List<DecisionCard>();
            var stalledNames = stallAssessment?.StalledWorkloads
                .Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string>();
            var stalledLookup = stallAssessment?.StalledWorkloads
                .ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, StalledWorkload>();

            foreach (var workload in workloads.OrderBy(w => w.Order))
            {
                var card = new DecisionCard
                {
                    WorkloadName = workload.Name,
                    Order = workload.Order,
                    RiskLevel = workload.RiskLevel,
                    DevicesAffected = workload.IntuneDeviceCount + workload.ConfigMgrDeviceCount,
                    ReadinessScore = workload.ReadinessScore
                };

                // Apply momentum insight data if it matches this workload
                if (momentumInsight != null &&
                    string.Equals(momentumInsight.RecommendedWorkload, workload.Name, StringComparison.OrdinalIgnoreCase))
                {
                    card.RollbackTimeMinutes = momentumInsight.RollbackTimeMinutes;
                    card.SafetyScore = momentumInsight.SafetyScore;
                }

                // Determine card type based on workload status + stall state
                if (workload.Status == WorkloadStatus.Completed || workload.IntuneAdoptionPercentage >= 90)
                {
                    card.CardType = DecisionCardType.Complete;
                    card.Decision = $"{workload.Name} is fully transitioned to Intune";
                    card.WhyItMatters = string.Join(" ", workload.Benefits.Take(1));
                    card.CostOfInaction = "None — this workload is complete.";
                    card.LowestRiskNextStep = workload.DependsOn.Count > 0
                        ? "Focus on workloads that depended on this one"
                        : "Move to the next workload in the sequence";
                }
                else if (stalledNames.Contains(workload.Name))
                {
                    var stall = stalledLookup[workload.Name];
                    card.CardType = DecisionCardType.StallRecovery;
                    card.Decision = $"Investigate and resolve {workload.Name} stall ({stall.DaysSinceChange} days)";
                    card.WhyItMatters = $"{stall.DevicesBlocked} devices remain on ConfigMgr for this workload. " +
                        $"Stall classification: {stall.WhyStalled}.";
                    card.CostOfInaction = $"{stall.DevicesBlocked} devices stay dual-managed. " +
                        (!string.IsNullOrEmpty(stall.BlockReason) ? stall.BlockReason : "No progress toward cloud-native.");
                    card.LowestRiskNextStep = GetStallRecoveryStep(stall);
                }
                else if (workload.Status == WorkloadStatus.InProgress && workload.IntuneAdoptionPercentage >= 50)
                {
                    card.CardType = DecisionCardType.NearComplete;
                    card.Decision = $"Push {workload.Name} to completion ({workload.IntuneAdoptionPercentage:F0}% → 90%+)";
                    card.WhyItMatters = $"Only {workload.ConfigMgrDeviceCount} devices remain on ConfigMgr. " +
                        string.Join(" ", workload.Benefits.Take(1));
                    card.CostOfInaction = $"{workload.ConfigMgrDeviceCount} devices stay dual-managed, " +
                        "preventing downstream workloads and delaying agent removal.";
                    card.LowestRiskNextStep = $"Expand Intune assignment to the next batch of devices for {workload.Name}";
                }
                else if (workload.Status == WorkloadStatus.InProgress)
                {
                    card.CardType = DecisionCardType.ExpandScope;
                    card.Decision = $"Expand {workload.Name} scope ({workload.IntuneAdoptionPercentage:F0}% adoption)";
                    card.WhyItMatters = string.Join(" ", workload.Benefits.Take(2));
                    card.CostOfInaction = $"{workload.ConfigMgrDeviceCount} devices lack Intune management for this workload. " +
                        "Dual-management overhead continues.";
                    card.LowestRiskNextStep = $"Assign {workload.Name} Intune policies to the next pilot ring of 50-100 devices";
                }
                else // NotStarted
                {
                    card.CardType = DecisionCardType.ReadyToStart;
                    var prereqs = workload.DependsOn.Count > 0
                        ? $"Prerequisites: {string.Join(", ", workload.DependsOn)}"
                        : "No prerequisites — ready to begin";
                    card.Decision = $"Start {workload.Name} transition ({workload.EstimatedTime})";
                    card.WhyItMatters = string.Join(" ", workload.Benefits.Take(2));
                    card.CostOfInaction = workload.ConfigMgrDeviceCount > 0
                        ? $"{workload.ConfigMgrDeviceCount} devices managed solely by ConfigMgr for this workload."
                        : "This workload remains entirely on ConfigMgr — no cloud benefit realized.";
                    card.LowestRiskNextStep = workload.DependsOn.Count > 0
                        ? $"Complete {workload.DependsOn.First()} first, then configure {workload.Name} in Intune"
                        : $"Configure {workload.Name} policies in Intune, then assign to a pilot group of 20-50 devices";
                }

                cards.Add(card);
            }

            return cards;
        }

        /// <summary>
        /// Generate Workload Unlock Chain — what completing each workload enables downstream.
        /// </summary>
        public List<WorkloadUnlockChain> GenerateUnlockChains(IList<Workload> workloads)
        {
            var chains = new List<WorkloadUnlockChain>();
            var workloadLookup = workloads.ToDictionary(w => w.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var workload in workloads.OrderBy(w => w.Order))
            {
                // Find workloads that depend on this one
                var dependents = workloads
                    .Where(w => w.DependsOn.Any(d =>
                        string.Equals(d, workload.Name, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                var totalDevicesUnblocked = dependents.Sum(d => d.ConfigMgrDeviceCount);

                var chain = new WorkloadUnlockChain
                {
                    WorkloadName = workload.Name,
                    IsCompleted = workload.Status == WorkloadStatus.Completed || workload.IntuneAdoptionPercentage >= 90,
                    EnabledWorkloads = dependents.Select(d => d.Name).ToList(),
                    DevicesUnblocked = totalDevicesUnblocked
                };

                if (dependents.Count == 0)
                {
                    chain.UnlockDescription = workload.Status == WorkloadStatus.Completed
                        ? $"{workload.Name} is complete. No downstream dependencies."
                        : $"Completing {workload.Name} is the final step — enables ConfigMgr agent removal.";
                    chain.MultiplierEffect = "End of chain — enables agent removal";
                }
                else
                {
                    var names = string.Join(", ", dependents.Select(d => d.Name));
                    chain.UnlockDescription = workload.Status == WorkloadStatus.Completed
                        ? $"{workload.Name} is complete — {names} can now proceed."
                        : $"Completing {workload.Name} enables: {names}.";
                    chain.MultiplierEffect = totalDevicesUnblocked > 0
                        ? $"Unlocks {dependents.Count} workload(s) affecting {totalDevicesUnblocked} devices"
                        : $"Unlocks {dependents.Count} downstream workload(s)";
                }

                chains.Add(chain);
            }

            return chains;
        }

        /// <summary>
        /// Generate ConfigMgr vs Intune coverage per workload.
        /// </summary>
        public List<ConfigMgrCoverage> GenerateCoverageCards(IList<Workload> workloads)
        {
            var cards = new List<ConfigMgrCoverage>();

            foreach (var workload in workloads.OrderBy(w => w.Order))
            {
                var total = workload.IntuneDeviceCount + workload.ConfigMgrDeviceCount;
                var card = new ConfigMgrCoverage
                {
                    WorkloadName = workload.Name,
                    IntuneDeviceCount = workload.IntuneDeviceCount,
                    ConfigMgrDeviceCount = workload.ConfigMgrDeviceCount,
                    IntunePercentage = workload.IntuneAdoptionPercentage,
                    HasRealData = workload.HasRealData
                };

                if (workload.IntuneAdoptionPercentage >= 90)
                {
                    card.StatusSummary = $"✅ Fully on Intune — {workload.IntuneDeviceCount} devices";
                    card.MigrationNote = "Consider removing ConfigMgr management for this workload.";
                }
                else if (workload.IntuneAdoptionPercentage > 0)
                {
                    card.StatusSummary = $"🔄 Split — {workload.IntuneDeviceCount} Intune / {workload.ConfigMgrDeviceCount} ConfigMgr";
                    card.MigrationNote = $"{workload.ConfigMgrDeviceCount} devices still managed by ConfigMgr for {workload.Name}.";
                }
                else
                {
                    card.StatusSummary = total > 0
                        ? $"⏸️ All {workload.ConfigMgrDeviceCount} devices on ConfigMgr"
                        : "⏸️ Not started — no device data";
                    card.MigrationNote = $"All {workload.Name} management through ConfigMgr. " +
                        "Configure Intune policies before moving the slider.";
                }

                cards.Add(card);
            }

            return cards;
        }

        /// <summary>
        /// Generate per-workload safety scores.
        /// </summary>
        public List<WorkloadSafetyScore> GenerateSafetyScores(
            IList<Workload> workloads,
            WorkloadMomentumInsight? momentumInsight)
        {
            var scores = new List<WorkloadSafetyScore>();

            foreach (var workload in workloads.OrderBy(w => w.Order))
            {
                var score = new WorkloadSafetyScore
                {
                    WorkloadName = workload.Name
                };

                // Use momentum insight for the recommended workload if available
                if (momentumInsight != null &&
                    string.Equals(momentumInsight.RecommendedWorkload, workload.Name, StringComparison.OrdinalIgnoreCase))
                {
                    score.SafetyLevel = momentumInsight.SafetyScore;
                    score.RollbackTimeMinutes = momentumInsight.RollbackTimeMinutes;
                    score.PolicyConflictCount = momentumInsight.PolicyConflicts?.Count ?? 0;
                    score.IntuneCoverage = momentumInsight.SuccessFactors ?? new List<string>();
                }
                else
                {
                    // Derive safety from workload characteristics
                    score.SafetyLevel = DeriveWorkloadSafety(workload);
                    score.RollbackTimeMinutes = GetEstimatedRollbackTime(workload.Name);
                    score.PolicyConflictCount = 0;
                }

                score.WhatStopsRunning = GetWhatStopsRunning(workload.Name);
                score.RollbackDescription = GetRollbackDescription(workload.Name, score.RollbackTimeMinutes);

                scores.Add(score);
            }

            return scores;
        }

        /// <summary>
        /// Generate Last Holdout Spotlight if conditions are met (6/7 workloads done).
        /// </summary>
        public LastHoldoutSpotlight? GenerateLastHoldoutSpotlight(
            IList<Workload> workloads,
            WorkloadStallAssessment? stallAssessment,
            int nearCloudNativeCount)
        {
            var completed = workloads.Count(w =>
                w.Status == WorkloadStatus.Completed || w.IntuneAdoptionPercentage >= 90);

            if (completed < 5) return null; // Only show when close to done

            var remaining = workloads
                .Where(w => w.Status != WorkloadStatus.Completed && w.IntuneAdoptionPercentage < 90)
                .OrderByDescending(w => w.ConfigMgrDeviceCount)
                .FirstOrDefault();

            if (remaining == null) return null;

            var holdoutData = stallAssessment?.LastHoldouts
                .FirstOrDefault(h => string.Equals(h.WorkloadName, remaining.Name, StringComparison.OrdinalIgnoreCase));

            return new LastHoldoutSpotlight
            {
                WorkloadName = remaining.Name,
                DevicesBlocked = holdoutData?.DevicesBlockedCount ?? remaining.ConfigMgrDeviceCount,
                IsVisible = true,
                CloudNativeDevicesOnCompletion = nearCloudNativeCount,
                WhyItMatters = $"You're {7 - completed} workload(s) from fully cloud-native. " +
                    $"{remaining.Name} is the highest-impact remaining workload — " +
                    $"completing it unblocks ConfigMgr agent removal for {nearCloudNativeCount} devices.",
                WhatNeedsToBeDone = GetLastHoldoutActions(remaining.Name),
                RollbackPlan = $"Flip the {remaining.Name} slider back to ConfigMgr. " +
                    $"Time to revert: ~{GetEstimatedRollbackTime(remaining.Name)} minutes. " +
                    "ConfigMgr policies re-apply on next machine policy cycle. Nothing is lost."
            };
        }

        #region Private Helpers

        private static string GetStallRecoveryStep(StalledWorkload stall)
        {
            return stall.WhyStalled switch
            {
                StallClassification.Technical =>
                    $"Resolve the technical blocker: {stall.BlockReason}. Then re-assign to a pilot batch of 20 devices.",
                StallClassification.ConfidenceBased =>
                    $"Start with a small batch of 20-50 devices. Monitor for 48 hours. " +
                    "Document results to build organizational confidence.",
                StallClassification.Operational =>
                    $"Assign a dedicated owner for {stall.Name}. Set a weekly target of 50 devices. " +
                    "Review progress in weekly standup.",
                StallClassification.ResourceConstrained =>
                    $"Allocate dedicated time for {stall.Name} transition. " +
                    "Consider Microsoft FastTrack engagement for acceleration.",
                _ => $"Review {stall.Name} status and assign next action owner"
            };
        }

        private static string DeriveWorkloadSafety(Workload workload)
        {
            if (workload.RiskLevel == "Low") return "High";
            if (workload.RiskLevel == "Medium") return "Medium";
            if (workload.RiskLevel == "High") return "Low";
            // Derive from readiness score
            if (workload.ReadinessScore >= 80) return "High";
            if (workload.ReadinessScore >= 50) return "Medium";
            return "Low";
        }

        private static int GetEstimatedRollbackTime(string workloadName)
        {
            // Based on Microsoft documentation and real-world experience
            if (workloadName.Contains("Compliance", StringComparison.OrdinalIgnoreCase)) return 15;
            if (workloadName.Contains("Endpoint", StringComparison.OrdinalIgnoreCase)) return 20;
            if (workloadName.Contains("Device Configuration", StringComparison.OrdinalIgnoreCase)) return 30;
            if (workloadName.Contains("Resource Access", StringComparison.OrdinalIgnoreCase)) return 25;
            if (workloadName.Contains("Windows Update", StringComparison.OrdinalIgnoreCase)) return 15;
            if (workloadName.Contains("Office", StringComparison.OrdinalIgnoreCase)) return 20;
            if (workloadName.Contains("Client Apps", StringComparison.OrdinalIgnoreCase)) return 45;
            return 30;
        }

        private static List<string> GetWhatStopsRunning(string workloadName)
        {
            if (workloadName.Contains("Compliance", StringComparison.OrdinalIgnoreCase))
                return new List<string>
                {
                    "ConfigMgr compliance baselines stop evaluating",
                    "ConfigMgr configuration items no longer apply",
                    "Intune compliance policies take full control"
                };
            if (workloadName.Contains("Endpoint", StringComparison.OrdinalIgnoreCase))
                return new List<string>
                {
                    "ConfigMgr Endpoint Protection policies stop applying",
                    "ConfigMgr antivirus definition updates stop",
                    "Intune/Defender for Endpoint takes full control"
                };
            if (workloadName.Contains("Device Configuration", StringComparison.OrdinalIgnoreCase))
                return new List<string>
                {
                    "ConfigMgr configuration profiles stop deploying",
                    "ConfigMgr custom settings no longer apply",
                    "Intune configuration profiles take full control"
                };
            if (workloadName.Contains("Resource Access", StringComparison.OrdinalIgnoreCase))
                return new List<string>
                {
                    "ConfigMgr VPN/Wi-Fi/certificate profiles stop deploying",
                    "ConfigMgr resource access policies no longer apply",
                    "Intune connectivity profiles take full control"
                };
            if (workloadName.Contains("Windows Update", StringComparison.OrdinalIgnoreCase))
                return new List<string>
                {
                    "ConfigMgr/WSUS software update deployments stop",
                    "ConfigMgr update groups no longer apply",
                    "Windows Update for Business (WUfB) via Intune takes full control"
                };
            if (workloadName.Contains("Office", StringComparison.OrdinalIgnoreCase))
                return new List<string>
                {
                    "ConfigMgr Office 365 update deployments stop",
                    "ConfigMgr Office configuration policies no longer apply",
                    "Intune Microsoft 365 Apps management takes full control"
                };
            if (workloadName.Contains("Client Apps", StringComparison.OrdinalIgnoreCase))
                return new List<string>
                {
                    "ConfigMgr application deployments stop for these devices",
                    "Software Center app availability changes",
                    "Intune Company Portal becomes primary app delivery"
                };
            return new List<string> { "ConfigMgr management for this workload stops", "Intune policies take full control" };
        }

        private static string GetRollbackDescription(string workloadName, int minutes)
        {
            return $"Flip the co-management slider back to ConfigMgr. " +
                $"Revert time: ~{minutes} minutes. On next machine policy cycle, " +
                "ConfigMgr policies re-apply automatically. No data loss.";
        }

        private static string GetLastHoldoutActions(string workloadName)
        {
            if (workloadName.Contains("Client Apps", StringComparison.OrdinalIgnoreCase))
                return "1. Inventory remaining ConfigMgr app deployments\n" +
                    "2. Package top apps as Win32 apps in Intune\n" +
                    "3. Test deployment to pilot group\n" +
                    "4. Move the Client Apps slider for pilot devices\n" +
                    "5. Expand to remaining devices in batches";
            if (workloadName.Contains("Office", StringComparison.OrdinalIgnoreCase))
                return "1. Verify M365 Apps configuration in Intune\n" +
                    "2. Configure update channel in Intune\n" +
                    "3. Move the Office slider for pilot batch\n" +
                    "4. Monitor update delivery for 1 week\n" +
                    "5. Expand to all devices";
            if (workloadName.Contains("Windows Update", StringComparison.OrdinalIgnoreCase))
                return "1. Configure WUfB update rings in Intune\n" +
                    "2. Set feature update policies\n" +
                    "3. Move the slider for a test ring of 50 devices\n" +
                    "4. Validate update compliance for 2 weeks\n" +
                    "5. Expand to production rings";
            return $"1. Verify Intune policies for {workloadName} are configured\n" +
                "2. Test on a pilot group of 20-50 devices\n" +
                "3. Monitor for 48-72 hours\n" +
                "4. Move the slider for remaining devices\n" +
                "5. Validate and document results";
        }

        #endregion
    }
}
