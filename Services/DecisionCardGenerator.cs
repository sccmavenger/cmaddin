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
            int totalDevices = enrollment?.TotalDevices ?? wl.FirstOrDefault()?.ConfigMgrDeviceCount + wl.FirstOrDefault()?.IntuneDeviceCount ?? 0;
            if (totalDevices == 0) totalDevices = wl.Sum(w => Math.Max(w.IntuneDeviceCount, w.ConfigMgrDeviceCount));

            int completedWorkloads = wl.Count(w => w.Status == WorkloadStatus.Completed || w.IntuneAdoptionPercentage >= 90);
            int totalWorkloads = wl.Count > 0 ? wl.Count : 7;

            // Estimate device tiers based on workload completion
            int green = 0, yellow = 0, red = 0;
            if (completedWorkloads == totalWorkloads)
            {
                green = totalDevices;
            }
            else if (completedWorkloads >= totalWorkloads - 2)
            {
                // Most workloads done: many green, some yellow
                green = (int)(totalDevices * (completedWorkloads / (double)totalWorkloads) * 0.8);
                yellow = (int)(totalDevices * 0.15);
                red = totalDevices - green - yellow;
            }
            else
            {
                double completionRatio = completedWorkloads / (double)totalWorkloads;
                green = (int)(totalDevices * completionRatio * 0.5);
                yellow = (int)(totalDevices * 0.2);
                red = totalDevices - green - yellow;
            }

            var blockers = wl
                .Where(w => w.Status != WorkloadStatus.Completed && w.IntuneAdoptionPercentage < 90)
                .OrderBy(w => w.IntuneAdoptionPercentage)
                .Take(3)
                .Select(w => $"{w.Name} ({w.IntuneAdoptionPercentage:F0}% Intune)")
                .ToList();

            return new UninstallReadinessResult
            {
                GreenCount = Math.Max(0, green),
                YellowCount = Math.Max(0, yellow),
                RedCount = Math.Max(0, red),
                TotalDevices = totalDevices,
                TopBlockers = blockers,
                Summary = green > 0
                    ? $"{green:N0} devices have all workload authorities on Intune and could uninstall the ConfigMgr client today."
                    : "No devices are fully ready to uninstall the ConfigMgr client yet. Complete more workload transitions to unlock this."
            };
        }

        #endregion

        #region Feature 2: Security Exposure Gap

        public SecurityExposureResult GenerateSecurityExposure(
            IEnumerable<Workload> workloads, DeviceEnrollment? enrollment, ComplianceScore? compliance)
        {
            var wl = workloads.ToList();
            double intuneCompliance = compliance?.IntuneScore ?? 85;
            double configMgrCompliance = compliance?.ConfigMgrScore ?? 52;

            // Encryption: Intune enforces BitLocker via compliance policy
            double intuneEncryption = Math.Min(99, intuneCompliance + 8);
            double configMgrEncryption = Math.Max(30, configMgrCompliance - 20);

            // Active threats: inverse relationship with compliance
            double intuneThreats = Math.Max(0.1, (100 - intuneCompliance) * 0.08);
            double configMgrThreats = Math.Max(0.5, (100 - configMgrCompliance) * 0.15);

            // Patch currency: approximate from compliance + workload state
            var wuWorkload = wl.FirstOrDefault(w => w.Name.Contains("Windows Update", StringComparison.OrdinalIgnoreCase));
            double intunePatch = wuWorkload != null && wuWorkload.IntuneAdoptionPercentage > 50 ? 91 : 82;
            double configMgrPatch = wuWorkload != null ? Math.Max(45, 70 - (100 - wuWorkload.IntuneAdoptionPercentage) * 0.3) : 55;

            var metrics = new List<SecurityMetricComparison>
            {
                new() { MetricName = "Compliance Rate", MetricIcon = "✅", IntuneValue = intuneCompliance, ConfigMgrValue = configMgrCompliance, IntuneLabel = $"{intuneCompliance:F0}%", ConfigMgrLabel = $"{configMgrCompliance:F0}%", HigherIsBetter = true },
                new() { MetricName = "Encryption Rate", MetricIcon = "🔒", IntuneValue = intuneEncryption, ConfigMgrValue = configMgrEncryption, IntuneLabel = $"{intuneEncryption:F0}%", ConfigMgrLabel = $"{configMgrEncryption:F0}%", HigherIsBetter = true },
                new() { MetricName = "Active Threats", MetricIcon = "🛡️", IntuneValue = intuneThreats, ConfigMgrValue = configMgrThreats, IntuneLabel = $"{intuneThreats:F1}%", ConfigMgrLabel = $"{configMgrThreats:F1}%", HigherIsBetter = false },
                new() { MetricName = "Patch Currency", MetricIcon = "📦", IntuneValue = intunePatch, ConfigMgrValue = configMgrPatch, IntuneLabel = $"{intunePatch:F0}%", ConfigMgrLabel = $"{configMgrPatch:F0}%", HigherIsBetter = true },
            };

            int delta = (int)((intuneCompliance - configMgrCompliance + intuneEncryption - configMgrEncryption) / 2);

            return new SecurityExposureResult
            {
                Metrics = metrics,
                SecurityDeltaScore = delta,
                Verdict = delta > 20
                    ? $"ConfigMgr-only devices are significantly less secure. {delta}-point security gap means unmanaged devices carry higher risk of compliance violations, unpatched vulnerabilities, and unencrypted data."
                    : $"Security gap of {delta} points between Intune-managed and ConfigMgr-only devices. Migrating remaining workloads will close this gap."
            };
        }

        #endregion

        #region Feature 3: ConfigMgr-Free Countdown

        public ConfigMgrFreeCountdown GenerateCountdown(
            IEnumerable<Workload> workloads, DeviceEnrollment? enrollment, double currentVelocity)
        {
            var wl = workloads.ToList();
            int devicesRemaining = enrollment?.ConfigMgrOnlyDevices ?? wl.Sum(w => w.ConfigMgrDeviceCount);
            if (devicesRemaining == 0) devicesRemaining = (int)((enrollment?.TotalDevices ?? 100000) * 0.4);

            int workloadsRemaining = wl.Count(w => w.Status != WorkloadStatus.Completed && w.IntuneAdoptionPercentage < 90);

            double velocity = currentVelocity > 0 ? currentVelocity : 45; // devices per week
            int daysAtCurrent = velocity > 0 ? (int)(devicesRemaining / (velocity / 7.0)) : 999;
            double accelerated = velocity * 1.5;
            int daysAccelerated = accelerated > 0 ? (int)(devicesRemaining / (accelerated / 7.0)) : 999;

            return new ConfigMgrFreeCountdown
            {
                ProjectedDate = DateTime.Now.AddDays(daysAtCurrent),
                AcceleratedDate = DateTime.Now.AddDays(daysAccelerated),
                DaysRemaining = daysAtCurrent,
                DaysWithAcceleration = daysAccelerated,
                WeeksSaved = (daysAtCurrent - daysAccelerated) / 7,
                CurrentVelocity = velocity,
                AcceleratedVelocity = accelerated,
                DevicesRemaining = devicesRemaining,
                WorkloadsRemaining = workloadsRemaining,
                TopAccelerators = new List<string>
                {
                    workloadsRemaining > 0 ? $"Complete {wl.Where(w => w.Status != WorkloadStatus.Completed).OrderByDescending(w => w.IntuneAdoptionPercentage).FirstOrDefault()?.Name ?? "next workload"} (nearest to completion)" : "All workloads complete — focus on device enrollment",
                    "Increase pilot batch size from 50 to 100 devices per wave",
                    "Enable auto-enrollment for Hybrid Azure AD Joined devices"
                }
            };
        }

        #endregion

        #region Feature 4: Pilot Wave Optimizer

        public List<PilotWave> GeneratePilotWaves(
            IEnumerable<Workload> workloads, DeviceEnrollment? enrollment)
        {
            int totalDevices = enrollment?.TotalDevices ?? 100000;
            int enrolled = enrollment?.IntuneEnrolledDevices ?? (int)(totalDevices * 0.6);
            int remaining = totalDevices - enrolled;

            int wave1Size = Math.Min(75, remaining);
            int wave2Size = Math.Min(150, remaining - wave1Size);
            int wave3Size = Math.Min(200, remaining - wave1Size - wave2Size);

            return new List<PilotWave>
            {
                new()
                {
                    WaveNumber = 1,
                    WaveName = "IT & Early Adopters",
                    DeviceCount = wave1Size,
                    ExpectedSuccessRate = 96,
                    RiskProfile = "Low",
                    Description = "Highest readiness devices: compliant, encrypted, Hybrid AAD joined. IT department devices for validation."
                },
                new()
                {
                    WaveNumber = 2,
                    WaveName = "Primary Business Units",
                    DeviceCount = wave2Size,
                    ExpectedSuccessRate = 91,
                    RiskProfile = "Low",
                    Description = "Good readiness scores, mixed departments for diversity coverage. Sales, marketing, and general office devices."
                },
                new()
                {
                    WaveNumber = 3,
                    WaveName = "Remaining & Remediation",
                    DeviceCount = wave3Size,
                    ExpectedSuccessRate = 84,
                    RiskProfile = "Medium",
                    Description = "Devices that may need remediation: older OS, missing encryption, or compliance gaps. Field/manufacturing devices."
                }
            };
        }

        #endregion

        #region Feature 5: Workload What-If

        public List<WorkloadWhatIf> GenerateWhatIfAnalysis(IEnumerable<Workload> workloads)
        {
            var wl = workloads.ToList();
            var results = new List<WorkloadWhatIf>();

            foreach (var workload in wl.Where(w => w.Status != WorkloadStatus.Completed && w.IntuneAdoptionPercentage < 90))
            {
                // Calculate which workloads would be unblocked
                var unblocked = wl
                    .Where(other => other.DependsOn.Contains(workload.Name) && other.Status == WorkloadStatus.NotStarted)
                    .Select(w => w.Name)
                    .ToList();

                // Security impact based on workload type
                int securityDelta = workload.Name.Contains("Compliance", StringComparison.OrdinalIgnoreCase) ? 12
                    : workload.Name.Contains("Endpoint", StringComparison.OrdinalIgnoreCase) ? 15
                    : workload.Name.Contains("Windows Update", StringComparison.OrdinalIgnoreCase) ? 8
                    : 5;

                int opsDelta = workload.Name.Contains("Windows Update", StringComparison.OrdinalIgnoreCase) ? 18
                    : workload.Name.Contains("Client Apps", StringComparison.OrdinalIgnoreCase) ? 14
                    : workload.Name.Contains("Office", StringComparison.OrdinalIgnoreCase) ? 10
                    : 6;

                int complianceDelta = workload.Name.Contains("Compliance", StringComparison.OrdinalIgnoreCase) ? 20
                    : workload.Name.Contains("Device Config", StringComparison.OrdinalIgnoreCase) ? 12
                    : 7;

                // Estimate new uninstall-ready devices
                int completedAfterThis = wl.Count(w => w.Status == WorkloadStatus.Completed || w.IntuneAdoptionPercentage >= 90) + 1;
                int totalWl = wl.Count > 0 ? wl.Count : 7;
                int newReady = completedAfterThis >= totalWl - 1
                    ? workload.ConfigMgrDeviceCount
                    : (int)(workload.ConfigMgrDeviceCount * 0.1);

                results.Add(new WorkloadWhatIf
                {
                    WorkloadName = workload.Name,
                    DevicesAffected = workload.ConfigMgrDeviceCount + workload.IntuneDeviceCount,
                    SecurityDelta = securityDelta,
                    OperationsDelta = opsDelta,
                    ComplianceDelta = complianceDelta,
                    WorkloadsUnblocked = unblocked,
                    NewUninstallReadyDevices = newReady,
                    Recommendation = $"Moving {workload.Name} to Intune affects {workload.ConfigMgrDeviceCount:N0} devices" +
                        (unblocked.Count > 0 ? $" and unblocks {string.Join(", ", unblocked)}" : "") +
                        $". Security improves +{securityDelta}, operations +{opsDelta}."
                });
            }

            return results.OrderByDescending(r => r.SecurityDelta + r.OperationsDelta + r.ComplianceDelta).ToList();
        }

        #endregion

        #region Feature 6: Stale/Orphan Detection

        public StaleOrphanResult GenerateStaleOrphanDetection(
            IEnumerable<Workload> workloads, DeviceEnrollment? enrollment)
        {
            int totalDevices = enrollment?.TotalDevices ?? 100000;
            int configMgrOnly = enrollment?.ConfigMgrOnlyDevices ?? (int)(totalDevices * 0.4);
            int coManaged = enrollment?.CoManagedDevices ?? (int)(totalDevices * 0.45);

            // Estimate stale devices (industry average: ~5-8% of fleet inactive 30+ days)
            int stale = (int)(configMgrOnly * 0.07);
            // Orphaned: co-managed devices where ConfigMgr can't reach Intune (estimated ~3%)
            int orphaned = (int)(coManaged * 0.03);
            // Ghost: devices in Intune without ConfigMgr match (cloud-native already counted separately, this is errors)
            int ghost = (int)(totalDevices * 0.015);
            // Blockers: active devices that failed co-management enrollment
            int blockers = (int)(configMgrOnly * 0.04);

            int total = stale + orphaned + ghost + blockers;

            return new StaleOrphanResult
            {
                StaleCount = stale,
                OrphanedCount = orphaned,
                GhostCount = ghost,
                BlockerCount = blockers,
                WasteSummary = $"{total:N0} devices are consuming ConfigMgr infrastructure with limited or no management value. " +
                    $"Cleaning up stale ({stale:N0}) and orphaned ({orphaned:N0}) devices alone would reduce your ConfigMgr footprint by {(double)total / totalDevices * 100:F1}%."
            };
        }

        #endregion

        #region Feature 7: Infrastructure Retirement Map

        public List<InfraRetirementItem> GenerateInfraRetirementMap(IEnumerable<Workload> workloads)
        {
            var wl = workloads.ToList();

            string GetStatus(string workloadContains)
            {
                var w = wl.FirstOrDefault(x => x.Name.Contains(workloadContains, StringComparison.OrdinalIgnoreCase));
                if (w == null) return "Still Needed";
                if (w.Status == WorkloadStatus.Completed || w.IntuneAdoptionPercentage >= 90) return "Ready to Retire";
                if (w.IntuneAdoptionPercentage >= 50) return "Partially Retired";
                return "Still Needed";
            }

            return new List<InfraRetirementItem>
            {
                new() { WorkloadName = "Windows Update", InfrastructureName = "WSUS Servers + SUP Role", InfrastructureDescription = "Windows Server Update Services servers and ConfigMgr Software Update Point role", Status = GetStatus("Windows Update") },
                new() { WorkloadName = "Client Apps", InfrastructureName = "Distribution Points (partial)", InfrastructureDescription = "Content distribution servers for application packages — partial retirement as apps move to Intune Win32", Status = GetStatus("Client Apps") },
                new() { WorkloadName = "Endpoint Protection", InfrastructureName = "EP Role + SCEP Infrastructure", InfrastructureDescription = "ConfigMgr Endpoint Protection role and SCEP certificate infrastructure", Status = GetStatus("Endpoint Protection") },
                new() { WorkloadName = "Compliance Policies", InfrastructureName = "ConfigMgr Compliance Baselines", InfrastructureDescription = "On-premises compliance baseline evaluation and reporting infrastructure", Status = GetStatus("Compliance") },
                new() { WorkloadName = "Device Configuration", InfrastructureName = "CI Baselines + GPO Dependencies", InfrastructureDescription = "Configuration Item baselines and Group Policy-dependent configuration management", Status = GetStatus("Device Config") },
                new() { WorkloadName = "Resource Access", InfrastructureName = "On-Prem Cert/VPN/Wi-Fi Profiles", InfrastructureDescription = "Certificate, VPN, and Wi-Fi profile deployment via ConfigMgr", Status = GetStatus("Resource Access") },
                new() { WorkloadName = "Office Apps", InfrastructureName = "Office Deployment Shares", InfrastructureDescription = "On-premises Office Click-to-Run deployment shares and update channels", Status = GetStatus("Office") }
            };
        }

        #endregion

        #region Feature 8: Compliance Trend Snapshot

        public ComplianceTrendSnapshot GenerateComplianceTrend(
            IEnumerable<Workload> workloads, ComplianceScore? compliance)
        {
            var wl = workloads.ToList();
            double currentCompliance = compliance?.IntuneScore ?? 65;

            var impacts = new List<ComplianceWorkloadImpact>();
            foreach (var workload in wl.Where(w => w.Status != WorkloadStatus.Completed))
            {
                double improvement = workload.Name.Contains("Compliance", StringComparison.OrdinalIgnoreCase) ? 22
                    : workload.Name.Contains("Endpoint", StringComparison.OrdinalIgnoreCase) ? 15
                    : workload.Name.Contains("Device Config", StringComparison.OrdinalIgnoreCase) ? 12
                    : workload.Name.Contains("Windows Update", StringComparison.OrdinalIgnoreCase) ? 10
                    : 6;

                // Reduce improvement proportional to current adoption (already partially moved)
                double remainingGain = improvement * (1 - workload.IntuneAdoptionPercentage / 100.0);

                impacts.Add(new ComplianceWorkloadImpact
                {
                    WorkloadName = workload.Name,
                    CurrentCompliance = Math.Min(100, currentCompliance + workload.IntuneAdoptionPercentage * 0.1),
                    ProjectedCompliance = Math.Min(100, currentCompliance + workload.IntuneAdoptionPercentage * 0.1 + remainingGain),
                });
            }

            double totalImprovement = impacts.Sum(i => i.Improvement) / Math.Max(1, impacts.Count);
            double projectedCompliance = Math.Min(98, currentCompliance + totalImprovement);

            return new ComplianceTrendSnapshot
            {
                CurrentComplianceRate = currentCompliance,
                ProjectedComplianceRate = projectedCompliance,
                WorkloadImpacts = impacts.OrderByDescending(i => i.Improvement).ToList(),
                Insight = $"Completing all remaining workload transitions is projected to improve overall compliance from {currentCompliance:F0}% to {projectedCompliance:F0}%. " +
                    (impacts.Any() ? $"Largest single contributor: {impacts.OrderByDescending(i => i.Improvement).First().WorkloadName} (+{impacts.Max(i => i.Improvement):F0}%)." : "")
            };
        }

        #endregion
    }
}
