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

        #region Feature Roadmap

        /// <summary>
        /// Generates the 8 proposed features for the roadmap brainstorm section.
        /// Each feature is grounded in real Microsoft API data sources.
        /// </summary>
        public List<FeatureRoadmapItem> GenerateFeatureRoadmap()
        {
            return new List<FeatureRoadmapItem>
            {
                new FeatureRoadmapItem
                {
                    Number = 1,
                    Title = "ConfigMgr Client Uninstall Readiness",
                    Goal = "Which devices can uninstall the ConfigMgr client TODAY?",
                    Description = "Traffic-light dashboard showing devices by readiness tier. Green = all 7 workload authorities on Intune + compliant + synced + Entra Joined. Yellow = 1-2 workloads away. Red = 3+ gaps. Per-device gap list shows exactly what's blocking.",
                    Phase = "Phase 1",
                    Effort = "Low",
                    Impact = "Very High",
                    RequiresNewApiCalls = false,
                    DataSources = new List<string>
                    {
                        "Graph: configurationManagerClientEnabledFeatures (7 workload authority flags per device)",
                        "Graph: complianceState, isEncrypted, lastSyncDateTime",
                        "ConfigMgr: SMS_Client_ComanagementState → CoManagementFlags bitmask",
                        "ConfigMgr: SMS_R_System → AADDeviceID (cross-matching)"
                    },
                    WhyItDrivesAction = "Customers see a growing number of devices that COULD uninstall ConfigMgr but haven't — creates urgency from the bottom up. When 2,000 devices are green and waiting, leadership asks 'why haven't we pulled the trigger?'",
                    MicrosoftDocsUrl = "https://learn.microsoft.com/en-us/graph/api/resources/intune-devices-configurationmanagerclientenabledfeatures"
                },
                new FeatureRoadmapItem
                {
                    Number = 2,
                    Title = "Security Exposure Gap",
                    Goal = "Are ConfigMgr-only devices LESS secure than Intune-managed ones?",
                    Description = "Side-by-side cohort comparison: Intune-managed vs ConfigMgr-only devices. Compares compliance rate, encryption rate, active malware count, and threat state distribution. Calculates a 'Security Delta' score.",
                    Phase = "Phase 1",
                    Effort = "Low",
                    Impact = "Very High",
                    RequiresNewApiCalls = false,
                    DataSources = new List<string>
                    {
                        "Graph: complianceState (compliant/noncompliant per device)",
                        "Graph: isEncrypted (BitLocker status per device)",
                        "Graph: windowsActiveMalwareCount, windowsRemediatedMalwareCount",
                        "Graph: partnerReportedThreatState (clean/activated/compromised)"
                    },
                    WhyItDrivesAction = "When leadership sees ConfigMgr-managed devices are 3x more likely to have active malware or 40% less likely to be encrypted, the migration becomes a security mandate — not just an IT project.",
                    MicrosoftDocsUrl = "https://learn.microsoft.com/en-us/security/zero-trust/"
                },
                new FeatureRoadmapItem
                {
                    Number = 3,
                    Title = "Days to ConfigMgr-Free Countdown",
                    Goal = "When will we be done? Give leadership a concrete date.",
                    Description = "Countdown-style display showing projected completion date based on current enrollment velocity. Weekly milestone markers. Acceleration modeling: 'If you increase velocity by 50%, you save X weeks.' Top 3 actions that would accelerate the timeline most.",
                    Phase = "Phase 1",
                    Effort = "Low",
                    Impact = "High",
                    RequiresNewApiCalls = false,
                    DataSources = new List<string>
                    {
                        "Existing: EnrollmentTrendAnalysis → Velocity7Day, Velocity30Day",
                        "Existing: ConfigMgrOnlyDevices count",
                        "Existing: Per-workload adoption velocity",
                        "Existing: EnrollmentSimulator projection formulas"
                    },
                    WhyItDrivesAction = "Turns an abstract 'migration project' into a concrete date. When the projection shows 18+ months at current pace but 8 months with acceleration, urgency is immediate and actionable.",
                    MicrosoftDocsUrl = "https://learn.microsoft.com/en-us/mem/configmgr/comanage/how-to-monitor"
                },
                new FeatureRoadmapItem
                {
                    Number = 4,
                    Title = "Pilot Wave Optimizer",
                    Goal = "Which devices should we migrate NEXT? Remove the paralysis.",
                    Description = "Automatically builds optimal next batch of devices ranked by lowest risk + highest impact. PilotScore = (Readiness × 0.4) + (ComplianceLikelihood × 0.3) + (LowRiskBonus × 0.2) + (DepartmentDiversity × 0.1). Groups into waves of 50-100 with diversity per wave.",
                    Phase = "Phase 2",
                    Effort = "Medium",
                    Impact = "High",
                    RequiresNewApiCalls = false,
                    DataSources = new List<string>
                    {
                        "Existing: DeviceReadinessService → ReadinessScore per device",
                        "Existing: RiskAssessmentService → RiskLevel per device",
                        "Graph: complianceState + isEncrypted per device",
                        "Graph: device join type (Hybrid AAD vs AAD-only)",
                        "ConfigMgr: SMS_CollectionMembership → department segmentation"
                    },
                    WhyItDrivesAction = "Eliminates the 'who do we migrate next?' paralysis that causes stalls. Instead of manually curating pilot groups, the tool hands admins a ready-to-go list with expected success rates.",
                    MicrosoftDocsUrl = "https://learn.microsoft.com/en-us/mem/intune/fundamentals/migration-guide"
                },
                new FeatureRoadmapItem
                {
                    Number = 5,
                    Title = "Workload What-If Simulator",
                    Goal = "Preview the impact of moving the NEXT workload before committing.",
                    Description = "Select any unmoved workload and see projected impact: affected device count, score changes across Security/Operations/UX/Cost/Compliance/Modernization, newly unblocked workloads, and new ConfigMgr uninstall-ready count.",
                    Phase = "Phase 2",
                    Effort = "Medium",
                    Impact = "High",
                    RequiresNewApiCalls = false,
                    DataSources = new List<string>
                    {
                        "Existing: Workload model → IntuneAdoptionPercentage, ConfigMgrDeviceCount, RiskLevel, ReadinessScore",
                        "Existing: MigrationImpactResult → 6-category impact scores",
                        "Existing: Workload dependency graph (DependsOn relationships)",
                        "Graph: deviceCompliancePolicies → per-workload policy coverage"
                    },
                    WhyItDrivesAction = "Removes fear of the unknown — the #1 cause of workload stalls. Admins can see concrete outcomes before making changes. 'Moving Windows Update affects 12,400 devices, improves Security by +8, unblocks Office Apps.'",
                    MicrosoftDocsUrl = "https://learn.microsoft.com/en-us/mem/configmgr/comanage/workloads"
                },
                new FeatureRoadmapItem
                {
                    Number = 6,
                    Title = "Stale Device & Orphan Detection",
                    Goal = "How many devices are wasting ConfigMgr infrastructure?",
                    Description = "4 categories: Stale (no heartbeat 30+ days), Orphaned (in ConfigMgr but not Intune), Ghost (in Intune but not ConfigMgr), Blocker (active but failed co-management). Shows concrete waste count.",
                    Phase = "Phase 3",
                    Effort = "Medium",
                    Impact = "Medium-High",
                    RequiresNewApiCalls = false,
                    DataSources = new List<string>
                    {
                        "ConfigMgr: SMS_R_System → LastActiveTime",
                        "ConfigMgr: SMS_CombinedDeviceResources → LastPolicyRequest, LastHWDDRDate",
                        "Graph: lastSyncDateTime per Intune device",
                        "Cross-match: AADDeviceID present in both systems"
                    },
                    WhyItDrivesAction = "'You have 2,400 stale devices still running the ConfigMgr client that haven't checked in for 30+ days. These can be cleaned up immediately.' Shows tangible waste leadership can act on without any workload decisions.",
                    MicrosoftDocsUrl = "https://learn.microsoft.com/en-us/mem/configmgr/develop/reference/core/clients/manage/sms_combineddeviceresources"
                },
                new FeatureRoadmapItem
                {
                    Number = 7,
                    Title = "ConfigMgr Infrastructure Retirement Map",
                    Goal = "What servers can I turn off as workloads move to Intune?",
                    Description = "Visual map of on-prem infrastructure tied to each workload: WSUS → Windows Update, DPs → Client Apps, EP role → Endpoint Protection. Shows what can be decommissioned now vs what's still needed.",
                    Phase = "Phase 3",
                    Effort = "Medium",
                    Impact = "Medium-High",
                    RequiresNewApiCalls = true,
                    DataSources = new List<string>
                    {
                        "ConfigMgr: SMS_Site → site system roles (DP, SUP, MP, EP)",
                        "ConfigMgr: SMS_SystemResourceList → Distribution Points, SUPs",
                        "Existing: Workload authority state (which workloads are on Intune)",
                        "Microsoft's published workload-to-infrastructure mapping"
                    },
                    WhyItDrivesAction = "IT leadership sees tangible infrastructure they can retire — WSUS servers, distribution points, EP roles — which translates directly to cost savings and operational simplification they can put in a business case.",
                    MicrosoftDocsUrl = "https://learn.microsoft.com/en-us/mem/configmgr/core/servers/deploy/configure/site-components"
                },
                new FeatureRoadmapItem
                {
                    Number = 8,
                    Title = "Compliance Drift Tracker",
                    Goal = "Prove that each workload move measurably improves device health.",
                    Description = "Time-series line chart showing compliance rate over time with vertical markers for workload transitions. Correlation callouts: 'After moving Compliance Policies to Intune, compliance improved from 62% → 89% in 3 weeks.'",
                    Phase = "Phase 3",
                    Effort = "High",
                    Impact = "Medium",
                    RequiresNewApiCalls = false,
                    DataSources = new List<string>
                    {
                        "Graph: complianceState snapshots over time",
                        "Existing: EnrollmentTrendAnalysis 7/30/60/90-day velocity",
                        "Existing: Per-workload adoption percentage tracked over time",
                        "Requires: Local time-series persistence (SQLite or JSON snapshots)"
                    },
                    WhyItDrivesAction = "Proves ROI of each workload move with real trend data. Shows that stalling = compliance regression and that each move measurably improves security posture. The before/after proof is what leadership needs to approve the next phase.",
                    MicrosoftDocsUrl = "https://learn.microsoft.com/en-us/mem/intune/protect/compliance-policy-monitor"
                }
            };
        }

        #endregion
    }
}
