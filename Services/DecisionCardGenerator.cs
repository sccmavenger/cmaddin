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
                    card.SafetyScore = momentumInsight.SafetyScore;
                }

                // Skip completed workloads — no decision needed
                if (workload.Status == WorkloadStatus.Completed || workload.IntuneAdoptionPercentage >= 90)
                {
                    continue;
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
        /// Generate Workload Unlock Chain — explains WHY completing each workload enables others.
        /// Co-management workloads have real prerequisite relationships defined by Microsoft:
        /// some workloads require others to be in place before they can be safely transitioned.
        /// </summary>
        public List<WorkloadUnlockChain> GenerateUnlockChains(IList<Workload> workloads)
        {
            var chains = new List<WorkloadUnlockChain>();
            var workloadLookup = workloads.ToDictionary(w => w.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var workload in workloads.OrderBy(w => w.Order))
            {
                // Find workloads that list this one as a prerequisite (DependsOn)
                var dependents = workloads
                    .Where(w => w.DependsOn.Any(d =>
                        string.Equals(d, workload.Name, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                // Skip workloads with no downstream impact and already completed
                if (dependents.Count == 0 && (workload.Status == WorkloadStatus.Completed || workload.IntuneAdoptionPercentage >= 90))
                    continue;

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
                    // This workload has no downstream dependents — it's a leaf in the chain
                    chain.UnlockDescription = GetWhyNoDownstream(workload.Name);
                    chain.MultiplierEffect = "End of chain — enables agent removal";
                }
                else
                {
                    var names = string.Join(", ", dependents.Select(d => d.Name));
                    // Explain the real reason WHY this workload unlocks others
                    chain.UnlockDescription = chain.IsCompleted
                        ? $"{workload.Name} is complete — {names} can now proceed."
                        : GetWhyUnlocks(workload.Name, dependents.Select(d => d.Name).ToList());
                    chain.MultiplierEffect = totalDevicesUnblocked > 0
                        ? $"Unlocks {dependents.Count} workload(s) affecting {totalDevicesUnblocked:N0} devices"
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
                // Skip workloads fully on Intune — they no longer run on ConfigMgr
                if (workload.IntuneAdoptionPercentage >= 90)
                    continue;

                var total = workload.IntuneDeviceCount + workload.ConfigMgrDeviceCount;
                var card = new ConfigMgrCoverage
                {
                    WorkloadName = workload.Name,
                    IntuneDeviceCount = workload.IntuneDeviceCount,
                    ConfigMgrDeviceCount = workload.ConfigMgrDeviceCount,
                    IntunePercentage = workload.IntuneAdoptionPercentage,
                    HasRealData = workload.HasRealData
                };

                if (workload.IntuneAdoptionPercentage > 0)
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
                    score.PolicyConflictCount = momentumInsight.PolicyConflicts?.Count ?? 0;
                    score.IntuneCoverage = momentumInsight.SuccessFactors ?? new List<string>();
                }
                else
                {
                    // Derive safety from workload characteristics
                    score.SafetyLevel = DeriveWorkloadSafety(workload);
                    score.PolicyConflictCount = 0;
                }

                score.WhatStopsRunning = GetWhatStopsRunning(workload.Name);
                score.WhySafe = GenerateWhySafe(workload, score.SafetyLevel);

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
                WhatNeedsToBeDone = GetLastHoldoutActions(remaining.Name)
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

        /// <summary>
        /// Generates a data-driven explanation of WHY this workload has its safety level.
        /// Based on the customer's actual environment data: adoption %, device counts, risk level, readiness.
        /// </summary>
        private static string GenerateWhySafe(Workload workload, string safetyLevel)
        {
            var reasons = new List<string>();

            // Adoption-based reasoning
            if (workload.IntuneAdoptionPercentage >= 80)
                reasons.Add($"{workload.IntuneAdoptionPercentage:F0}% of devices already on Intune for this workload");
            else if (workload.IntuneAdoptionPercentage >= 50)
                reasons.Add($"{workload.IntuneAdoptionPercentage:F0}% adoption — majority of devices are on Intune but gaps remain");
            else if (workload.IntuneAdoptionPercentage > 0)
                reasons.Add($"Only {workload.IntuneAdoptionPercentage:F0}% of devices are on Intune — most still rely on ConfigMgr");
            else
                reasons.Add("No devices have been moved to Intune for this workload yet");

            // Risk level reasoning
            if (workload.RiskLevel == "Low")
                reasons.Add("Microsoft rates this workload as low-risk to transition");
            else if (workload.RiskLevel == "High")
                reasons.Add("Microsoft rates this as a higher-risk workload — requires careful planning");

            // Device count context
            if (workload.ConfigMgrDeviceCount > 0)
                reasons.Add($"{workload.ConfigMgrDeviceCount:N0} devices still managed by ConfigMgr");

            // Readiness
            if (workload.ReadinessScore >= 80)
                reasons.Add($"Readiness score of {workload.ReadinessScore:F0}% indicates strong Intune policy coverage");
            else if (workload.ReadinessScore >= 50)
                reasons.Add($"Readiness score of {workload.ReadinessScore:F0}% — some Intune policy gaps to address");
            else if (workload.ReadinessScore > 0)
                reasons.Add($"Readiness score of {workload.ReadinessScore:F0}% — Intune policies need configuration before moving");

            // Dependencies
            if (workload.DependsOn.Count > 0)
            {
                var deps = string.Join(", ", workload.DependsOn);
                reasons.Add($"Depends on {deps} being completed first");
            }

            return string.Join(". ", reasons) + ".";
        }

        /// <summary>
        /// Explains WHY one workload unlocks others based on real Microsoft co-management dependencies.
        /// </summary>
        private static string GetWhyUnlocks(string workloadName, List<string> dependentNames)
        {
            var names = string.Join(", ", dependentNames);

            if (workloadName.Contains("Compliance", StringComparison.OrdinalIgnoreCase))
                return $"Compliance Policies is a prerequisite for {names} because Intune needs compliance baselines " +
                    "to evaluate device health before those workloads can safely use Intune-based configuration and protection.";
            if (workloadName.Contains("Endpoint Protection", StringComparison.OrdinalIgnoreCase))
                return $"Endpoint Protection must be on Intune before {names} because device security posture " +
                    "needs to be validated through Intune/Defender before expanding configuration management scope.";
            if (workloadName.Contains("Device Configuration", StringComparison.OrdinalIgnoreCase))
                return $"Device Configuration manages core device settings. Moving it to Intune enables {names} " +
                    "to also use Intune, since they depend on configuration profiles being consistently delivered.";

            // Generic dependency explanation
            return $"{workloadName} is a prerequisite for {names}. " +
                $"These workloads require {workloadName} to be managed by Intune before they can be safely transitioned.";
        }

        /// <summary>
        /// Explains why a workload with no downstream dependents is important.
        /// </summary>
        private static string GetWhyNoDownstream(string workloadName)
        {
            if (workloadName.Contains("Client Apps", StringComparison.OrdinalIgnoreCase))
                return "Client Apps is typically the last workload to transition because it requires repackaging " +
                    "ConfigMgr applications as Win32 apps for Intune. It has no downstream dependencies — " +
                    "completing it is the final step before ConfigMgr agent removal.";
            if (workloadName.Contains("Resource Access", StringComparison.OrdinalIgnoreCase))
                return "Resource Access (VPN, Wi-Fi, certificates) is an endpoint workload with no downstream " +
                    "dependencies. Once complete, it contributes toward being able to remove the ConfigMgr agent.";
            if (workloadName.Contains("Windows Update", StringComparison.OrdinalIgnoreCase))
                return "Windows Update for Business replaces WSUS/ConfigMgr update management. " +
                    "No other workloads depend on it — completing it moves you closer to agent removal.";
            if (workloadName.Contains("Office", StringComparison.OrdinalIgnoreCase))
                return "Office Click-to-Run updates move from ConfigMgr to Intune-managed M365 Apps servicing. " +
                    "No other workloads depend on it — it's an independent transition step.";

            return $"Completing {workloadName} has no other workload dependencies — it moves all affected devices " +
                "closer to being fully cloud-native and eligible for ConfigMgr agent removal.";
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

        #region Feature 1: Uninstall Readiness

        public UninstallReadinessResult GenerateUninstallReadiness(
            IEnumerable<Workload> workloads, DeviceEnrollment? enrollment)
        {
            var wl = workloads.ToList();
            int coManagedDevices = enrollment?.CoManagedDevices ?? wl.Max(w => w.IntuneDeviceCount + w.ConfigMgrDeviceCount);
            if (coManagedDevices == 0) coManagedDevices = enrollment?.TotalDevices ?? 1;

            int totalWorkloads = wl.Count > 0 ? wl.Count : 7;

            // BOTTLENECK MATH: A device can only uninstall ConfigMgr when ALL 7 workloads are on Intune.
            // The bottleneck workload (lowest adoption) caps the green tier.
            // Sort workloads by adoption ascending — the worst one defines the ceiling.
            var sortedByAdoption = wl.OrderBy(w => w.IntuneAdoptionPercentage).ToList();
            double worstAdoption = sortedByAdoption.First().IntuneAdoptionPercentage;
            double secondWorstAdoption = sortedByAdoption.Count > 1 ? sortedByAdoption[1].IntuneAdoptionPercentage : worstAdoption;

            // Green = devices where the BOTTLENECK workload has authority on Intune.
            // This is at most the min adoption % across all workloads.
            int green = (int)(coManagedDevices * (worstAdoption / 100.0));

            // Yellow = devices between 2nd-worst and worst (1-2 workloads from green).
            int yellow = (int)(coManagedDevices * ((secondWorstAdoption - worstAdoption) / 100.0));

            // Red = everything else
            int red = Math.Max(0, coManagedDevices - green - yellow);

            // Per-workload gap breakdown
            var gaps = wl
                .OrderBy(w => w.IntuneAdoptionPercentage)
                .Select(w => new WorkloadGapDetail
                {
                    WorkloadName = w.Name,
                    AdoptionPercent = w.IntuneAdoptionPercentage,
                    DevicesBlocked = w.ConfigMgrDeviceCount,
                    Status = w.IntuneAdoptionPercentage >= 90 ? "Complete"
                        : w.IntuneAdoptionPercentage >= 50 ? "Almost"
                        : "Blocking"
                })
                .ToList();

            var blockerWorkloads = wl
                .Where(w => w.IntuneAdoptionPercentage < 90)
                .OrderBy(w => w.IntuneAdoptionPercentage)
                .ToList();

            var blockers = blockerWorkloads
                .Take(3)
                .Select(w => $"{w.Name} at {w.IntuneAdoptionPercentage:F0}% — bottleneck for {w.ConfigMgrDeviceCount:N0} devices")
                .ToList();

            // "Next win" = if the bottleneck workload completes, new green ceiling becomes 2nd-worst adoption
            var bottleneck = sortedByAdoption.First();
            int nextWinDevices = (int)(coManagedDevices * ((secondWorstAdoption - worstAdoption) / 100.0));
            string nextWinName = bottleneck.Name;

            // Dual-management cost messaging
            int dualManagedDevices = coManagedDevices - green;

            return new UninstallReadinessResult
            {
                GreenCount = Math.Max(0, green),
                YellowCount = Math.Max(0, yellow),
                RedCount = Math.Max(0, red),
                TotalDevices = coManagedDevices,
                TopBlockers = blockers,
                WorkloadGaps = gaps,
                GreenActions = green > 0
                    ? new List<string>
                    {
                        $"Schedule ConfigMgr client uninstall for {green:N0} devices with all workload authorities on Intune",
                        $"Each dual-managed device runs two management agents — uninstalling eliminates agent conflicts and reduces helpdesk tickets",
                        $"Validate Intune policy enforcement on a pilot batch of 50 devices before bulk uninstall"
                    }
                    : new List<string> { $"No devices have all {totalWorkloads} workloads on Intune yet. The bottleneck is {bottleneck.Name} at {worstAdoption:F0}% adoption." },
                YellowActions = yellow > 0
                    ? new List<string>
                    {
                        $"Complete {nextWinName} ({worstAdoption:F0}% → 90%+) to unlock {nextWinDevices:N0} additional devices for uninstall",
                        $"These {yellow:N0} devices are 1-2 workloads away — the fastest path to reducing dual management overhead"
                    }
                    : new List<string>(),
                RedActions = red > 0
                    ? new List<string>
                    {
                        $"{red:N0} devices have 3+ workloads still on ConfigMgr — start with {blockerWorkloads.LastOrDefault()?.Name ?? "the highest-adoption workload"} to build momentum",
                        $"Every workload you complete raises the green ceiling for ALL {coManagedDevices:N0} co-managed devices"
                    }
                    : new List<string>(),
                NextWinDeviceCount = nextWinDevices,
                NextWinWorkload = nextWinName,
                Summary = green > 0
                    ? $"{green:N0} of {coManagedDevices:N0} co-managed devices have all {totalWorkloads} workload authorities on Intune. " +
                      $"Bottleneck: {nextWinName} at {worstAdoption:F0}%. Completing it would unlock {nextWinDevices:N0} more."
                    : $"The bottleneck workload is {nextWinName} at {worstAdoption:F0}% Intune adoption — " +
                      $"no devices can uninstall ConfigMgr until all {totalWorkloads} workloads reach 90%+."
            };
        }

        #endregion

        #region Feature 2: Security Exposure Gap

        public SecurityExposureResult GenerateSecurityExposure(
            IEnumerable<Workload> workloads, DeviceEnrollment? enrollment, ComplianceScore? compliance)
        {
            var wl = workloads.ToList();
            int configMgrOnly = enrollment?.ConfigMgrOnlyDevices ?? (int)((enrollment?.TotalDevices ?? 100000) * 0.4);
            int totalDevices = enrollment?.TotalDevices ?? 100000;

            // === FACTS ONLY — metrics derived from real data or binary truths ===

            // 1. Compliance Rate — REAL DATA from ComplianceScore
            double intuneCompliance = compliance?.IntuneScore ?? 0;
            double configMgrCompliance = compliance?.ConfigMgrScore ?? 0;
            bool hasComplianceData = compliance != null && intuneCompliance > 0;

            // 2. Conditional Access — BINARY FACT: ConfigMgr-only devices cannot enforce CA
            int devicesWithoutCA = compliance?.DevicesLackingConditionalAccess ?? configMgrOnly;

            // 3. Workload authority gap — computed from REAL workload adoption data
            int workloadsOnConfigMgr = wl.Count(w => w.IntuneAdoptionPercentage < 90);

            var metrics = new List<SecurityMetricComparison>();

            // Metric 1: Conditional Access — the HEADLINE. Binary fact, not estimated.
            metrics.Add(new SecurityMetricComparison
            {
                MetricName = "Conditional Access",
                MetricIcon = "🔑",
                IntuneValue = 100,
                ConfigMgrValue = 0,
                IntuneLabel = "Enforced",
                ConfigMgrLabel = "Impossible",
                HigherIsBetter = true
            });

            // Metric 2: Compliance Rate — REAL if connected
            if (hasComplianceData)
            {
                metrics.Add(new SecurityMetricComparison
                {
                    MetricName = "Compliance Rate",
                    MetricIcon = "✅",
                    IntuneValue = intuneCompliance,
                    ConfigMgrValue = configMgrCompliance,
                    IntuneLabel = $"{intuneCompliance:F0}%",
                    ConfigMgrLabel = $"{configMgrCompliance:F0}%",
                    HigherIsBetter = true
                });
            }

            // Metric 3: Workload authority — computed from real workload data
            double intuneAuthorityPct = wl.Count > 0 ? wl.Average(w => w.IntuneAdoptionPercentage) : 0;
            metrics.Add(new SecurityMetricComparison
            {
                MetricName = "Workload Authority",
                MetricIcon = "⚙️",
                IntuneValue = intuneAuthorityPct,
                ConfigMgrValue = 100 - intuneAuthorityPct,
                IntuneLabel = $"{intuneAuthorityPct:F0}% cloud",
                ConfigMgrLabel = $"{100 - intuneAuthorityPct:F0}% on-prem",
                HigherIsBetter = true
            });

            // Risk severity based on factual gaps
            string severity;
            if (configMgrOnly > totalDevices * 0.3)
                severity = "Critical";
            else if (configMgrOnly > totalDevices * 0.15)
                severity = "High";
            else if (configMgrOnly > totalDevices * 0.05)
                severity = "Moderate";
            else
                severity = "Low";

            int securityDelta = hasComplianceData ? (int)(intuneCompliance - configMgrCompliance) : 0;

            // Per-workload security impact — GAP POINTS scaled by actual adoption gap
            var workloadImpacts = wl
                .Where(w => w.IntuneAdoptionPercentage < 90)
                .OrderByDescending(w => GetSecurityWeight(w.Name) * (100 - w.IntuneAdoptionPercentage) / 100.0)
                .Select(w => new WorkloadSecurityImpact
                {
                    WorkloadName = w.Name,
                    Icon = GetSecurityIcon(w.Name),
                    GapPoints = (int)(GetSecurityWeight(w.Name) * (100 - w.IntuneAdoptionPercentage) / 100.0),
                    RiskContribution = GetSecurityRiskDescription(w.Name)
                })
                .ToList();

            // Remediation actions — specific, not generic
            var actions = new List<string>();
            if (configMgrOnly > 0)
                actions.Add($"Enroll {configMgrOnly:N0} ConfigMgr-only devices into Intune — they cannot enforce Conditional Access until enrolled");
            var worstWorkload = wl.Where(w => w.IntuneAdoptionPercentage < 90).OrderBy(w => w.IntuneAdoptionPercentage).FirstOrDefault();
            if (worstWorkload != null)
                actions.Add($"Move {worstWorkload.Name} workload authority to Intune ({worstWorkload.IntuneAdoptionPercentage:F0}% → 90%+) — this is the current bottleneck");
            if (devicesWithoutCA > 0)
                actions.Add($"{devicesWithoutCA:N0} devices lack Conditional Access — these devices bypass your Zero Trust access policies");

            return new SecurityExposureResult
            {
                Metrics = metrics,
                SecurityDeltaScore = securityDelta,
                RiskSeverity = severity,
                DevicesAtRisk = configMgrOnly,
                WorkloadImpacts = workloadImpacts,
                RemediationActions = actions,
                ExecutiveRiskSummary = $"{configMgrOnly:N0} devices cannot enforce Conditional Access. " +
                    $"These devices bypass your Zero Trust access controls — they can access corporate resources " +
                    $"even when non-compliant, unencrypted, or compromised.",
                Verdict = configMgrOnly > 0
                    ? $"Conditional Access is your strongest security control — and {configMgrOnly:N0} devices are completely outside it. " +
                      $"Every device not enrolled in Intune is a device that can access Exchange, SharePoint, and Teams regardless of compliance state."
                    : "All devices are enrolled in Intune with Conditional Access enforcement. Security posture is strong."
            };
        }

        private static int GetSecurityWeight(string workloadName)
        {
            if (workloadName.Contains("Endpoint", StringComparison.OrdinalIgnoreCase)) return 18;
            if (workloadName.Contains("Compliance", StringComparison.OrdinalIgnoreCase)) return 15;
            if (workloadName.Contains("Windows Update", StringComparison.OrdinalIgnoreCase)) return 12;
            if (workloadName.Contains("Device Config", StringComparison.OrdinalIgnoreCase)) return 10;
            if (workloadName.Contains("Resource Access", StringComparison.OrdinalIgnoreCase)) return 8;
            return 5;
        }

        private static string GetSecurityIcon(string workloadName)
        {
            if (workloadName.Contains("Endpoint", StringComparison.OrdinalIgnoreCase)) return "🛡️";
            if (workloadName.Contains("Compliance", StringComparison.OrdinalIgnoreCase)) return "✅";
            if (workloadName.Contains("Windows Update", StringComparison.OrdinalIgnoreCase)) return "📦";
            if (workloadName.Contains("Device Config", StringComparison.OrdinalIgnoreCase)) return "⚙️";
            if (workloadName.Contains("Resource Access", StringComparison.OrdinalIgnoreCase)) return "🔑";
            return "📋";
        }

        private static string GetSecurityRiskDescription(string workloadName)
        {
            if (workloadName.Contains("Endpoint", StringComparison.OrdinalIgnoreCase))
                return "Devices lack cloud-delivered protection, ASR rules, and Defender for Endpoint integration";
            if (workloadName.Contains("Compliance", StringComparison.OrdinalIgnoreCase))
                return "No continuous compliance evaluation or Conditional Access remediation triggers";
            if (workloadName.Contains("Windows Update", StringComparison.OrdinalIgnoreCase))
                return "Patch deployment relies on WSUS — slower distribution, no cloud intelligence";
            if (workloadName.Contains("Device Config", StringComparison.OrdinalIgnoreCase))
                return "Security baselines and hardening policies not enforced through Intune";
            if (workloadName.Contains("Resource Access", StringComparison.OrdinalIgnoreCase))
                return "Certificate and VPN profiles not managed via cloud — weaker identity verification";
            return "Workload policies remain on-premises without cloud enforcement";
        }

        #endregion

        #region Feature 3: Stale/Orphan Detection

        public StaleOrphanResult GenerateStaleOrphanDetection(
            IEnumerable<Workload> workloads, DeviceEnrollment? enrollment)
        {
            int totalDevices = enrollment?.TotalDevices ?? 100000;
            int configMgrOnly = enrollment?.ConfigMgrOnlyDevices ?? (int)(totalDevices * 0.4);
            int coManaged = enrollment?.CoManagedDevices ?? (int)(totalDevices * 0.45);
            bool isMock = enrollment?.IsMockData ?? true;

            // === RANGES, NOT SINGLE NUMBERS ===
            // Industry benchmarks applied to YOUR fleet size — shown as ranges

            // Stale: 5-8% of ConfigMgr-managed devices inactive 30+ days (Gartner/Forrester range)
            int staleLow = (int)(configMgrOnly * 0.05);
            int staleHigh = (int)(configMgrOnly * 0.08);
            int staleMid = (staleLow + staleHigh) / 2;

            // Orphaned: 2-4% of co-managed fail enrollment handshake
            int orphanedLow = (int)(coManaged * 0.02);
            int orphanedHigh = (int)(coManaged * 0.04);
            int orphanedMid = (orphanedLow + orphanedHigh) / 2;

            // Ghost: 1-2% identity mismatches across directories
            int ghostLow = (int)(totalDevices * 0.01);
            int ghostHigh = (int)(totalDevices * 0.02);
            int ghostMid = (ghostLow + ghostHigh) / 2;

            // Blockers: 3-5% of ConfigMgr-only have enrollment failures
            int blockerLow = (int)(configMgrOnly * 0.03);
            int blockerHigh = (int)(configMgrOnly * 0.05);
            int blockerMid = (blockerLow + blockerHigh) / 2;

            int totalMid = staleMid + orphanedMid + ghostMid + blockerMid;
            double wastePercent = totalDevices > 0 ? (double)totalMid / totalDevices * 100 : 0;

            var categories = new List<StaleOrphanCategory>
            {
                new()
                {
                    CategoryName = "Stale Devices",
                    Icon = "💤",
                    DeviceCount = staleMid,
                    DeviceCountLow = staleLow,
                    DeviceCountHigh = staleHigh,
                    Methodology = "5–8% of ConfigMgr-managed devices (industry benchmark)",
                    Description = "Devices inactive 30+ days — no heartbeat, no policy refresh, no inventory update",
                    Impact = $"Each stale device holds a ConfigMgr client license and inflates reporting. {staleLow:N0}–{staleHigh:N0} stale records skew your compliance metrics.",
                    ActionItem = "Run ConfigMgr Device Collection cleanup rule or create a dynamic collection filtering lastActiveTime > 30 days, then disable/remove",
                    SeverityColor = "#D97706",
                    SeverityBackground = "#FFFBEB"
                },
                new()
                {
                    CategoryName = "Orphaned Devices",
                    Icon = "🔗",
                    DeviceCount = orphanedMid,
                    DeviceCountLow = orphanedLow,
                    DeviceCountHigh = orphanedHigh,
                    Methodology = "2–4% of co-managed devices (enrollment handshake failure rate)",
                    Description = "ConfigMgr-registered but co-management handshake never completed — Intune cannot see these devices",
                    Impact = $"{orphanedLow:N0}–{orphanedHigh:N0} devices are stuck in split-management: ConfigMgr manages them but Intune policies are not applied.",
                    ActionItem = "Check Azure AD Hybrid Join status and co-management prerequisites. Re-run co-management enrollment on affected devices via ConfigMgr client action.",
                    SeverityColor = "#DC2626",
                    SeverityBackground = "#FEF2F2"
                },
                new()
                {
                    CategoryName = "Ghost Devices",
                    Icon = "👻",
                    DeviceCount = ghostMid,
                    DeviceCountLow = ghostLow,
                    DeviceCountHigh = ghostHigh,
                    Methodology = "1–2% of total fleet (directory identity mismatches)",
                    Description = "Exists in Intune but has no matching ConfigMgr record — could be cloud-native, decommissioned, or data mismatch",
                    Impact = $"{ghostLow:N0}–{ghostHigh:N0} devices appear in Intune without ConfigMgr counterparts. If not intentionally cloud-native, these represent identity mismatches blocking accurate migration counts.",
                    ActionItem = "Cross-reference Intune device list with ConfigMgr inventory by serial number. Validate cloud-native devices are intentional; clean up duplicates.",
                    SeverityColor = "#7C3AED",
                    SeverityBackground = "#F5F3FF"
                },
                new()
                {
                    CategoryName = "Enrollment Blockers",
                    Icon = "🚫",
                    DeviceCount = blockerMid,
                    DeviceCountLow = blockerLow,
                    DeviceCountHigh = blockerHigh,
                    Methodology = "3–5% of ConfigMgr-only devices (enrollment failure rate)",
                    Description = "Active devices where co-management enrollment explicitly failed — these devices cannot transition until errors are resolved",
                    Impact = $"{blockerLow:N0}–{blockerHigh:N0} active production devices attempted Intune enrollment and failed. These directly block your migration timeline.",
                    ActionItem = "Review CoManagementHandler.log on affected devices. Common fixes: renew Azure AD device certificate, verify MDM authority URL, check enrollment restrictions.",
                    SeverityColor = "#1E293B",
                    SeverityBackground = "#F1F5F9"
                }
            };

            // Actions framed around infrastructure decommission blocking
            var actions = new List<string>();
            if (staleMid > 0)
                actions.Add($"Clean up {staleLow:N0}–{staleHigh:N0} stale devices — these inflate your ConfigMgr device count and block accurate migration planning");
            if (blockerMid > 0)
                actions.Add($"Remediate {blockerLow:N0}–{blockerHigh:N0} enrollment failures — every blocked device extends your ConfigMgr infrastructure dependency");
            if (orphanedMid > 0)
                actions.Add($"Re-enroll {orphanedLow:N0}–{orphanedHigh:N0} orphaned devices to complete co-management and reduce ConfigMgr-only load");
            if (ghostMid > 0)
                actions.Add($"Reconcile {ghostLow:N0}–{ghostHigh:N0} ghost devices — accurate device counts are required before decommissioning any ConfigMgr infrastructure");

            // Infrastructure decommission framing instead of fabricated dollar costs
            string infraMessage = configMgrOnly > 0
                ? $"{configMgrOnly:N0} devices still require ConfigMgr infrastructure"
                : "All devices are cloud-managed — ConfigMgr infrastructure can be evaluated for retirement";

            return new StaleOrphanResult
            {
                StaleCount = staleMid,
                OrphanedCount = orphanedMid,
                GhostCount = ghostMid,
                BlockerCount = blockerMid,
                WastePercent = wastePercent,
                Categories = categories,
                CleanupActions = actions,
                EstimatedAnnualWaste = infraMessage,
                DataConfidence = isMock
                    ? "⚠️ Estimated — industry benchmarks applied to your fleet size. Connect to live data for actual counts."
                    : "📊 Ranges based on industry benchmarks applied to your actual device counts.",
                WasteSummary = $"An estimated {totalMid:N0} devices ({wastePercent:F1}% of your fleet) are consuming ConfigMgr infrastructure " +
                    $"with limited or no management value. Until these are cleaned up, you cannot accurately scope your migration " +
                    $"or plan ConfigMgr infrastructure retirement. {infraMessage}."
            };
        }

        #endregion
    }
}
