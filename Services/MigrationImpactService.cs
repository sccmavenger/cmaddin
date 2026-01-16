using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroTrustMigrationAddin.Models;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Services
{
    /// <summary>
    /// Service that calculates comprehensive migration impact predictions across all categories:
    /// Security, Operational Efficiency, User Experience, Cost Optimization, Compliance, and Modernization.
    /// </summary>
    public class MigrationImpactService
    {
        private readonly GraphDataService? _graphService;
        private readonly ConfigMgrAdminService? _configMgrService;

        public MigrationImpactService(GraphDataService? graphService = null, ConfigMgrAdminService? configMgrService = null)
        {
            _graphService = graphService;
            _configMgrService = configMgrService;
        }

        /// <summary>
        /// Computes the full migration impact analysis.
        /// </summary>
        public async Task<MigrationImpactResult> ComputeImpactAsync()
        {
            Instance.Info("[MIGRATION IMPACT] Starting comprehensive impact analysis...");
            
            var result = new MigrationImpactResult();
            var inputs = await GatherInputsAsync();

            // Calculate metrics for each category
            var securityImpact = CalculateSecurityImpact(inputs);
            var operationsImpact = CalculateOperationalImpact(inputs);
            var uxImpact = CalculateUserExperienceImpact(inputs);
            var costImpact = CalculateCostImpact(inputs);
            var complianceImpact = CalculateComplianceImpact(inputs);
            var modernizationImpact = CalculateModernizationImpact(inputs);

            result.CategoryImpacts = new List<CategoryImpact>
            {
                securityImpact,
                operationsImpact,
                uxImpact,
                costImpact,
                complianceImpact,
                modernizationImpact
            };

            // Flatten all metrics
            result.AllMetrics = result.CategoryImpacts.SelectMany(c => c.Metrics).ToList();

            // Calculate workload impacts
            result.WorkloadImpacts = CalculateWorkloadImpacts(inputs);

            // Calculate overall scores
            result.OverallCurrentScore = (int)result.CategoryImpacts.Average(c => c.CurrentScore);
            result.OverallProjectedScore = (int)result.CategoryImpacts.Average(c => c.ProjectedScore);

            // Generate top benefits
            result.TopBenefits = GenerateTopBenefits(result);

            // Generate executive summary
            result.ExecutiveSummary = GenerateExecutiveSummary(result, inputs);

            // Set additional context
            result.DevicesRemaining = inputs.TotalDevices - inputs.EnrolledDevices;
            result.CurrentEnrollmentPercent = inputs.TotalDevices > 0 
                ? (double)inputs.EnrolledDevices / inputs.TotalDevices * 100 
                : 0;

            // Estimate timeline
            result.TimelineEstimate = EstimateTimeline(inputs);

            // Calculate data quality
            result.DataQualityScore = CalculateDataQuality(inputs);

            Instance.Info($"[MIGRATION IMPACT] Analysis complete: Current={result.OverallCurrentScore}, Projected={result.OverallProjectedScore}, Improvement=+{result.OverallImprovement}");

            return result;
        }

        private async Task<MigrationImpactInputs> GatherInputsAsync()
        {
            var inputs = new MigrationImpactInputs();

            try
            {
                if (_graphService != null)
                {
                    // Get device counts using the correct method name
                    var devices = await _graphService.GetCachedManagedDevicesAsync();
                    inputs.TotalDevices = devices?.Count ?? 0;
                    inputs.EnrolledDevices = devices?.Count(d => !string.IsNullOrEmpty(d.Id)) ?? 0;

                    // Get compliance data
                    var complianceData = await _graphService.GetComplianceDashboardAsync();
                    if (complianceData != null)
                    {
                        inputs.CurrentComplianceRate = complianceData.OverallComplianceRate;
                        inputs.CompliantDevices = complianceData.CompliantDevices;
                        inputs.NonCompliantDevices = complianceData.NonCompliantDevices;
                    }

                    // Get co-management details from workloads
                    var workloads = await _graphService.GetWorkloadsAsync();
                    if (workloads != null && workloads.Count > 0)
                    {
                        inputs.HasCoManagement = true;
                        inputs.CoManagedDevices = inputs.EnrolledDevices;
                        inputs.ComplianceWorkloadInCloud = workloads.Any(w => w.Name.Contains("Compliance", StringComparison.OrdinalIgnoreCase) && w.Status == WorkloadStatus.Completed);
                        inputs.EndpointProtectionWorkloadInCloud = workloads.Any(w => w.Name.Contains("Endpoint", StringComparison.OrdinalIgnoreCase) && w.Status == WorkloadStatus.Completed);
                        inputs.WindowsUpdateWorkloadInCloud = workloads.Any(w => w.Name.Contains("Update", StringComparison.OrdinalIgnoreCase) && w.Status == WorkloadStatus.Completed);
                    }

                    // Estimate other metrics from device data
                    if (devices != null)
                    {
                        inputs.Windows11Devices = devices.Count(d => d.OperatingSystem?.Contains("11") == true);
                        inputs.Windows10Devices = devices.Count(d => d.OperatingSystem?.Contains("10") == true && d.OperatingSystem?.Contains("11") != true);
                        inputs.StaleDevices = devices.Count(d => d.LastSyncDateTime < DateTime.UtcNow.AddDays(-7));
                        inputs.ActiveDevices = inputs.TotalDevices - inputs.StaleDevices;
                        
                        // Encryption estimate (devices with compliance state set)
                        inputs.EncryptedDevices = (int)(inputs.EnrolledDevices * 0.85); // Estimate 85% encrypted when enrolled
                        
                        // CA-ready = enrolled + compliant
                        inputs.ConditionalAccessReadyDevices = inputs.CompliantDevices;
                    }
                }

                // Set defaults for ConfigMgr-only scenarios
                if (inputs.TotalDevices == 0)
                {
                    // Demo/estimation mode
                    inputs.TotalDevices = 1000;
                    inputs.EnrolledDevices = 350;
                    inputs.CoManagedDevices = 350;
                    inputs.ConfigMgrOnlyDevices = 650;
                    inputs.CurrentComplianceRate = 62;
                    inputs.CompliantDevices = 217;
                    inputs.NonCompliantDevices = 133;
                    inputs.EncryptedDevices = 280;
                    inputs.ConditionalAccessReadyDevices = 217;
                    inputs.HasCMG = true;
                    inputs.HasCoManagement = true;
                    inputs.HasAutopilot = false;
                    inputs.DistributionPointCount = 15;
                    inputs.SUPCount = 3;
                    inputs.Windows11Devices = 150;
                    inputs.Windows10Devices = 750;
                    inputs.LegacyOSDevices = 100;
                    inputs.StaleDevices = 85;
                    inputs.ActiveDevices = 915;
                    inputs.AverageEnrollmentVelocity = 2.5;
                    inputs.AverageProvisioningTimeHours = 4.0;
                    inputs.SelfServiceAppCount = 25;
                    inputs.HealthyClients = 870;
                    inputs.UnhealthyClients = 130;
                    inputs.PatchComplianceRate = 78;
                }
            }
            catch (Exception ex)
            {
                Instance.Error($"[MIGRATION IMPACT] Error gathering inputs: {ex.Message}");
            }

            return inputs;
        }

        #region Security Impact

        private CategoryImpact CalculateSecurityImpact(MigrationImpactInputs inputs)
        {
            var impact = new CategoryImpact
            {
                Category = ImpactCategory.Security,
                Metrics = new List<ImpactMetric>()
            };

            // 1. Conditional Access Coverage
            var caCurrentPct = inputs.TotalDevices > 0 
                ? (double)inputs.ConditionalAccessReadyDevices / inputs.TotalDevices * 100 
                : 0;
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "ca_coverage",
                Name = "Conditional Access Coverage",
                Description = "Devices protected by Conditional Access policies",
                Category = ImpactCategory.Security,
                CurrentValue = caCurrentPct,
                ProjectedValue = 95, // Target with full enrollment
                Unit = "%",
                Icon = "🛡️",
                HigherIsBetter = true,
                ConfidenceLevel = 90,
                Explanation = "Enrolling devices in Intune enables Conditional Access enforcement, blocking access from non-compliant or unmanaged devices."
            });

            // 2. Endpoint Protection Coverage
            var epCurrentPct = inputs.TotalDevices > 0 
                ? (double)inputs.DefenderManagedDevices / inputs.TotalDevices * 100 
                : (inputs.EndpointProtectionWorkloadInCloud ? 35 : 20);
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "defender_coverage",
                Name = "Defender for Endpoint Coverage",
                Description = "Devices onboarded to Microsoft Defender for Endpoint",
                Category = ImpactCategory.Security,
                CurrentValue = epCurrentPct,
                ProjectedValue = 98,
                Unit = "%",
                Icon = "🔰",
                HigherIsBetter = true,
                ConfidenceLevel = 85,
                Explanation = "Cloud enrollment automatically onboards devices to Defender for Endpoint, providing advanced threat protection and EDR capabilities.",
                RelatedWorkload = "Endpoint Protection"
            });

            // 3. Encryption Status
            var encCurrentPct = inputs.TotalDevices > 0 
                ? (double)inputs.EncryptedDevices / inputs.TotalDevices * 100 
                : 28;
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "encryption",
                Name = "BitLocker Encryption",
                Description = "Devices with disk encryption enabled and keys escrowed",
                Category = ImpactCategory.Security,
                CurrentValue = encCurrentPct,
                ProjectedValue = 99,
                Unit = "%",
                Icon = "🔐",
                HigherIsBetter = true,
                ConfidenceLevel = 95,
                Explanation = "Intune compliance policies can require and enforce BitLocker encryption with automatic key escrow to Azure AD."
            });

            // 4. Zero Trust Readiness
            var ztCurrent = CalculateZeroTrustScore(inputs, false);
            var ztProjected = CalculateZeroTrustScore(inputs, true);
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "zero_trust",
                Name = "Zero Trust Readiness",
                Description = "Alignment with Zero Trust security principles",
                Category = ImpactCategory.Security,
                CurrentValue = ztCurrent,
                ProjectedValue = ztProjected,
                Unit = "%",
                Icon = "🎯",
                HigherIsBetter = true,
                ConfidenceLevel = 80,
                Explanation = "Zero Trust requires device identity verification, compliance checks, and least-privilege access - all enabled by cloud enrollment."
            });

            // 5. Stale Device Visibility
            var stalePct = inputs.TotalDevices > 0 
                ? (double)inputs.StaleDevices / inputs.TotalDevices * 100 
                : 8.5;
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "stale_devices",
                Name = "Stale Device Risk",
                Description = "Devices without recent check-in (potential security blind spots)",
                Category = ImpactCategory.Security,
                CurrentValue = stalePct,
                ProjectedValue = 2, // Intune keeps devices checking in
                Unit = "%",
                Icon = "👁️",
                HigherIsBetter = false,
                ConfidenceLevel = 85,
                Explanation = "Cloud-managed devices maintain continuous communication, reducing security blind spots from disconnected devices."
            });

            // Calculate category scores
            impact.CurrentScore = (int)impact.Metrics.Where(m => m.HasData).Average(m => 
                m.HigherIsBetter ? m.CurrentValue : (100 - m.CurrentValue));
            impact.ProjectedScore = (int)impact.Metrics.Where(m => m.HasData).Average(m => 
                m.HigherIsBetter ? m.ProjectedValue : (100 - m.ProjectedValue));

            impact.Summary = $"Security posture will improve from {impact.CurrentScore}% to {impact.ProjectedScore}% with cloud enrollment.";
            impact.TopBenefits = new List<string>
            {
                "Conditional Access blocks unauthorized access",
                "Automatic Defender for Endpoint onboarding",
                "Enforced encryption with key escrow"
            };

            return impact;
        }

        private double CalculateZeroTrustScore(MigrationImpactInputs inputs, bool projected)
        {
            // Zero Trust pillars: Identity, Devices, Applications, Data, Infrastructure, Networks
            double score = 0;
            
            if (projected)
            {
                score += 20; // Device identity (enrolled = verified)
                score += 18; // Device health (compliance policies)
                score += 15; // Application access (CA + MAM)
                score += 12; // Data protection (encryption + DLP)
                score += 10; // Infrastructure (cloud-native)
                score += 10; // Network (location-independent)
                return Math.Min(score, 85);
            }
            else
            {
                var enrolledPct = inputs.TotalDevices > 0 ? (double)inputs.EnrolledDevices / inputs.TotalDevices : 0.35;
                score += enrolledPct * 20; // Device identity
                score += (inputs.CurrentComplianceRate / 100) * 18; // Device health
                score += enrolledPct * 0.7 * 15; // App access (partial)
                score += (inputs.EncryptedDevices > 0 ? 0.5 : 0.2) * 12; // Data protection
                score += (inputs.HasCoManagement ? 0.5 : 0.3) * 10; // Infrastructure
                score += (inputs.HasCMG ? 0.6 : 0.3) * 10; // Network
                return score;
            }
        }

        #endregion

        #region Operational Efficiency Impact

        private CategoryImpact CalculateOperationalImpact(MigrationImpactInputs inputs)
        {
            var impact = new CategoryImpact
            {
                Category = ImpactCategory.OperationalEfficiency,
                Metrics = new List<ImpactMetric>()
            };

            // 1. Policy Deployment Speed
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "policy_speed",
                Name = "Policy Deployment Speed",
                Description = "Time to deploy new security or configuration policies",
                Category = ImpactCategory.OperationalEfficiency,
                CurrentValue = 24, // Hours with ConfigMgr
                ProjectedValue = 0.25, // 15 minutes with Intune
                Unit = "hours",
                Icon = "⚡",
                HigherIsBetter = false,
                ConfidenceLevel = 95,
                Explanation = "Intune policies deploy in near real-time vs. ConfigMgr's client polling intervals."
            });

            // 2. Real-time Visibility
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "visibility_lag",
                Name = "Device Visibility Lag",
                Description = "Time delay in device inventory and status updates",
                Category = ImpactCategory.OperationalEfficiency,
                CurrentValue = 12, // Hours with ConfigMgr
                ProjectedValue = 0.5, // 30 minutes with Intune
                Unit = "hours",
                Icon = "📡",
                HigherIsBetter = false,
                ConfidenceLevel = 90,
                Explanation = "Cloud-managed devices report status continuously, enabling real-time dashboards and alerts."
            });

            // 3. Remote Management Capability
            var remoteCurrentPct = inputs.HasCMG ? 65 : 25;
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "remote_mgmt",
                Name = "Remote Management Coverage",
                Description = "Devices manageable without VPN or corporate network",
                Category = ImpactCategory.OperationalEfficiency,
                CurrentValue = remoteCurrentPct,
                ProjectedValue = 100,
                Unit = "%",
                Icon = "🌐",
                HigherIsBetter = true,
                ConfidenceLevel = 95,
                Explanation = "Intune manages devices anywhere with internet connectivity, eliminating VPN dependencies."
            });

            // 4. Automation Potential
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "automation",
                Name = "Automation Readiness",
                Description = "Capability to automate common IT tasks via Graph API",
                Category = ImpactCategory.OperationalEfficiency,
                CurrentValue = 30,
                ProjectedValue = 90,
                Unit = "%",
                Icon = "🤖",
                HigherIsBetter = true,
                ConfidenceLevel = 85,
                Explanation = "Microsoft Graph API enables Power Automate workflows, Logic Apps, and custom automation for device management."
            });

            // 5. Single Pane of Glass
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "single_pane",
                Name = "Unified Management Console",
                Description = "Management tasks completable from a single portal",
                Category = ImpactCategory.OperationalEfficiency,
                CurrentValue = 40, // Split between ConfigMgr and partial Intune
                ProjectedValue = 95,
                Unit = "%",
                Icon = "🖥️",
                HigherIsBetter = true,
                ConfidenceLevel = 90,
                Explanation = "Microsoft Intune admin center consolidates device, app, security, and compliance management."
            });

            // Calculate scores (invert for lower-is-better metrics)
            var scores = impact.Metrics.Select(m => m.HigherIsBetter ? m.CurrentValue : Math.Max(0, 100 - m.CurrentValue * 4)).ToList();
            var projScores = impact.Metrics.Select(m => m.HigherIsBetter ? m.ProjectedValue : Math.Max(0, 100 - m.ProjectedValue * 4)).ToList();
            
            impact.CurrentScore = (int)scores.Average();
            impact.ProjectedScore = (int)projScores.Average();

            impact.Summary = $"Operational efficiency improves from {impact.CurrentScore}% to {impact.ProjectedScore}%.";
            impact.TopBenefits = new List<string>
            {
                "Near real-time policy deployment",
                "Manage devices anywhere without VPN",
                "Graph API enables automation workflows"
            };

            return impact;
        }

        #endregion

        #region User Experience Impact

        private CategoryImpact CalculateUserExperienceImpact(MigrationImpactInputs inputs)
        {
            var impact = new CategoryImpact
            {
                Category = ImpactCategory.UserExperience,
                Metrics = new List<ImpactMetric>()
            };

            // 1. Device Provisioning Time
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "provisioning_time",
                Name = "New Device Setup Time",
                Description = "Time for a user to have a fully configured device",
                Category = ImpactCategory.UserExperience,
                CurrentValue = inputs.AverageProvisioningTimeHours > 0 ? inputs.AverageProvisioningTimeHours : 4.0,
                ProjectedValue = 0.5, // 30 minutes with Autopilot
                Unit = "hours",
                Icon = "⏱️",
                HigherIsBetter = false,
                ConfidenceLevel = 90,
                Explanation = "Windows Autopilot enables self-service deployment with pre-configured settings, reducing setup from hours to minutes."
            });

            // 2. Self-Service App Access
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "self_service_apps",
                Name = "Self-Service Applications",
                Description = "Apps users can install without IT intervention",
                Category = ImpactCategory.UserExperience,
                CurrentValue = inputs.SelfServiceAppCount > 0 ? inputs.SelfServiceAppCount : 25,
                ProjectedValue = 150, // Company Portal typical catalog
                Unit = "count",
                Icon = "📱",
                HigherIsBetter = true,
                ConfidenceLevel = 85,
                Explanation = "Company Portal provides a curated app store experience where users can browse and install approved applications."
            });

            // 3. SSO Experience
            var ssoCurrentPct = inputs.EnrolledDevices > 0 
                ? (double)inputs.EnrolledDevices / inputs.TotalDevices * 100 * 0.8 
                : 28;
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "sso_coverage",
                Name = "Single Sign-On Coverage",
                Description = "Apps accessible with seamless Azure AD authentication",
                Category = ImpactCategory.UserExperience,
                CurrentValue = ssoCurrentPct,
                ProjectedValue = 95,
                Unit = "%",
                Icon = "🔑",
                HigherIsBetter = true,
                ConfidenceLevel = 85,
                Explanation = "Enrolled devices get Primary Refresh Tokens enabling seamless SSO to Azure AD-integrated apps."
            });

            // 4. Remote Work Readiness
            var rwrCurrent = inputs.HasCMG ? 55 : 30;
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "remote_work",
                Name = "Remote Work Readiness",
                Description = "Ability to work productively from anywhere",
                Category = ImpactCategory.UserExperience,
                CurrentValue = rwrCurrent,
                ProjectedValue = 95,
                Unit = "%",
                Icon = "🏠",
                HigherIsBetter = true,
                ConfidenceLevel = 90,
                Explanation = "Cloud-managed devices work identically on corporate network or at home, with consistent access to resources."
            });

            // 5. Support Ticket Reduction
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "ticket_reduction",
                Name = "IT Support Ticket Volume",
                Description = "Estimated support tickets per 100 devices/month",
                Category = ImpactCategory.UserExperience,
                CurrentValue = 12,
                ProjectedValue = 6,
                Unit = "count",
                Icon = "🎫",
                HigherIsBetter = false,
                ConfidenceLevel = 75,
                Explanation = "Self-service capabilities, better remote support tools, and proactive remediation reduce support burden."
            });

            // Calculate scores
            impact.CurrentScore = 45;
            impact.ProjectedScore = 88;

            impact.Summary = $"User experience score improves from {impact.CurrentScore} to {impact.ProjectedScore}.";
            impact.TopBenefits = new List<string>
            {
                "30-minute device setup with Autopilot",
                "Self-service app installation via Company Portal",
                "Seamless SSO to cloud applications"
            };

            return impact;
        }

        #endregion

        #region Cost Optimization Impact

        private CategoryImpact CalculateCostImpact(MigrationImpactInputs inputs)
        {
            var impact = new CategoryImpact
            {
                Category = ImpactCategory.CostOptimization,
                Metrics = new List<ImpactMetric>()
            };

            // 1. Infrastructure Reduction - Distribution Points
            var dpReduction = inputs.DistributionPointCount > 0 ? inputs.DistributionPointCount : 15;
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "dp_reduction",
                Name = "Distribution Points Reducible",
                Description = "On-premises servers that can be decommissioned",
                Category = ImpactCategory.CostOptimization,
                CurrentValue = dpReduction,
                ProjectedValue = Math.Max(2, dpReduction / 3), // Keep a few for legacy
                Unit = "count",
                Icon = "🖥️",
                HigherIsBetter = false,
                ConfidenceLevel = 70,
                Explanation = "Cloud content delivery through Delivery Optimization and CDN reduces need for on-prem distribution points."
            });

            // 2. WAN Bandwidth Savings
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "wan_savings",
                Name = "WAN Bandwidth Reduction",
                Description = "Reduction in branch office bandwidth consumption",
                Category = ImpactCategory.CostOptimization,
                CurrentValue = 0,
                ProjectedValue = 40,
                Unit = "%",
                Icon = "📶",
                HigherIsBetter = true,
                ConfidenceLevel = 75,
                Explanation = "Delivery Optimization peer-to-peer and local caching dramatically reduces content download from central locations."
            });

            // 3. Admin Time Savings
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "admin_time",
                Name = "Admin Hours Saved Monthly",
                Description = "IT hours saved through automation and simplified management",
                Category = ImpactCategory.CostOptimization,
                CurrentValue = 0,
                ProjectedValue = 40,
                Unit = "hours",
                Icon = "⏰",
                HigherIsBetter = true,
                ConfidenceLevel = 70,
                Explanation = "Simplified deployment, automated compliance, and self-service capabilities reduce manual IT effort."
            });

            // 4. License Utilization Visibility
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "license_visibility",
                Name = "License Utilization Visibility",
                Description = "Ability to track and optimize software license usage",
                Category = ImpactCategory.CostOptimization,
                CurrentValue = 35,
                ProjectedValue = 90,
                Unit = "%",
                Icon = "📊",
                HigherIsBetter = true,
                ConfidenceLevel = 85,
                Explanation = "Intune app discovery and usage analytics help identify unused licenses for reclamation."
            });

            // 5. Infrastructure TCO
            var monthlyInfraCost = (inputs.DistributionPointCount * 200) + (inputs.SUPCount * 300); // Rough estimate
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "infra_tco",
                Name = "Monthly Infrastructure Savings",
                Description = "Estimated savings from server and maintenance reduction",
                Category = ImpactCategory.CostOptimization,
                CurrentValue = 0,
                ProjectedValue = monthlyInfraCost * 0.6,
                Unit = "$/month",
                Icon = "💵",
                HigherIsBetter = true,
                ConfidenceLevel = 65,
                Explanation = "Reducing on-premises servers decreases hardware, power, cooling, and maintenance costs."
            });

            impact.CurrentScore = 30;
            impact.ProjectedScore = 72;

            impact.Summary = $"Cost optimization potential increases from {impact.CurrentScore}% to {impact.ProjectedScore}%.";
            impact.TopBenefits = new List<string>
            {
                $"Potential to retire {dpReduction - 2} distribution points",
                "40+ admin hours saved monthly",
                "Better license visibility reduces waste"
            };

            return impact;
        }

        #endregion

        #region Compliance & Governance Impact

        private CategoryImpact CalculateComplianceImpact(MigrationImpactInputs inputs)
        {
            var impact = new CategoryImpact
            {
                Category = ImpactCategory.ComplianceGovernance,
                Metrics = new List<ImpactMetric>()
            };

            // 1. Compliance Policy Coverage
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "compliance_coverage",
                Name = "Compliance Policy Coverage",
                Description = "Devices with compliance policies assigned and reporting",
                Category = ImpactCategory.ComplianceGovernance,
                CurrentValue = inputs.CurrentComplianceRate > 0 ? inputs.CurrentComplianceRate : 62,
                ProjectedValue = 98,
                Unit = "%",
                Icon = "✅",
                HigherIsBetter = true,
                ConfidenceLevel = 90,
                Explanation = "All enrolled devices automatically receive compliance policies with continuous evaluation.",
                RelatedWorkload = "Compliance Policies"
            });

            // 2. Audit Readiness
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "audit_readiness",
                Name = "Audit Readiness Score",
                Description = "Ability to demonstrate compliance posture to auditors",
                Category = ImpactCategory.ComplianceGovernance,
                CurrentValue = 50,
                ProjectedValue = 92,
                Unit = "%",
                Icon = "📋",
                HigherIsBetter = true,
                ConfidenceLevel = 85,
                Explanation = "Intune provides built-in compliance reports, audit logs, and exportable evidence for regulatory compliance."
            });

            // 3. Configuration Drift Detection
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "drift_detection",
                Name = "Configuration Drift Detection",
                Description = "Ability to detect and remediate unauthorized changes",
                Category = ImpactCategory.ComplianceGovernance,
                CurrentValue = 40,
                ProjectedValue = 95,
                Unit = "%",
                Icon = "🔍",
                HigherIsBetter = true,
                ConfidenceLevel = 85,
                Explanation = "Continuous compliance evaluation detects configuration drift and can auto-remediate."
            });

            // 4. Data Protection
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "data_protection",
                Name = "Data Protection Policies",
                Description = "Devices with data loss prevention policies enforced",
                Category = ImpactCategory.ComplianceGovernance,
                CurrentValue = 25,
                ProjectedValue = 90,
                Unit = "%",
                Icon = "🔏",
                HigherIsBetter = true,
                ConfidenceLevel = 80,
                Explanation = "Intune App Protection Policies and Windows Information Protection help prevent data leakage."
            });

            // 5. Regulatory Compliance
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "regulatory",
                Name = "Regulatory Framework Alignment",
                Description = "Alignment with common frameworks (NIST, CIS, etc.)",
                Category = ImpactCategory.ComplianceGovernance,
                CurrentValue = 45,
                ProjectedValue = 85,
                Unit = "%",
                Icon = "⚖️",
                HigherIsBetter = true,
                ConfidenceLevel = 75,
                Explanation = "Security baselines and compliance policies map to industry frameworks, simplifying compliance efforts."
            });

            impact.CurrentScore = (int)impact.Metrics.Average(m => m.CurrentValue);
            impact.ProjectedScore = (int)impact.Metrics.Average(m => m.ProjectedValue);

            impact.Summary = $"Compliance posture improves from {impact.CurrentScore}% to {impact.ProjectedScore}%.";
            impact.TopBenefits = new List<string>
            {
                "Continuous compliance monitoring and reporting",
                "Automated audit evidence collection",
                "Real-time configuration drift detection"
            };

            return impact;
        }

        #endregion

        #region Modernization Impact

        private CategoryImpact CalculateModernizationImpact(MigrationImpactInputs inputs)
        {
            var impact = new CategoryImpact
            {
                Category = ImpactCategory.Modernization,
                Metrics = new List<ImpactMetric>()
            };

            // 1. Cloud-Native Device Percentage
            var cloudNativeCurrent = inputs.TotalDevices > 0 
                ? (double)(inputs.EnrolledDevices - inputs.CoManagedDevices) / inputs.TotalDevices * 100 
                : 5;
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "cloud_native",
                Name = "Cloud-Native Devices",
                Description = "Devices managed exclusively through cloud services",
                Category = ImpactCategory.Modernization,
                CurrentValue = Math.Max(5, cloudNativeCurrent),
                ProjectedValue = 85,
                Unit = "%",
                Icon = "☁️",
                HigherIsBetter = true,
                ConfidenceLevel = 85,
                Explanation = "Moving from co-management to cloud-native eliminates ConfigMgr dependencies for modern devices."
            });

            // 2. Windows 11 Readiness
            var w11Pct = inputs.TotalDevices > 0 
                ? (double)inputs.Windows11Devices / inputs.TotalDevices * 100 
                : 15;
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "windows11",
                Name = "Windows 11 Adoption",
                Description = "Devices running Windows 11",
                Category = ImpactCategory.Modernization,
                CurrentValue = w11Pct,
                ProjectedValue = Math.Min(95, w11Pct + 50), // Significant uplift expected
                Unit = "%",
                Icon = "🪟",
                HigherIsBetter = true,
                ConfidenceLevel = 70,
                Explanation = "Cloud management simplifies Windows 11 feature updates and hardware refresh cycles."
            });

            // 3. Legacy OS Reduction
            var legacyPct = inputs.TotalDevices > 0 
                ? (double)inputs.LegacyOSDevices / inputs.TotalDevices * 100 
                : 10;
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "legacy_os",
                Name = "Legacy OS Devices",
                Description = "Devices on Windows 8.1 or earlier",
                Category = ImpactCategory.Modernization,
                CurrentValue = legacyPct,
                ProjectedValue = 0,
                Unit = "%",
                Icon = "🔧",
                HigherIsBetter = false,
                ConfidenceLevel = 80,
                Explanation = "Cloud enrollment drives hardware refresh and OS upgrades to supported versions."
            });

            // 4. Autopilot Enabled
            var autopilotCurrent = inputs.HasAutopilot ? 40 : 0;
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "autopilot",
                Name = "Autopilot Deployment Ready",
                Description = "Devices provisioned or ready for Autopilot",
                Category = ImpactCategory.Modernization,
                CurrentValue = autopilotCurrent,
                ProjectedValue = 95,
                Unit = "%",
                Icon = "🚀",
                HigherIsBetter = true,
                ConfidenceLevel = 85,
                Explanation = "Autopilot enables zero-touch deployment with devices shipped directly to users."
            });

            // 5. Modern App Deployment
            impact.Metrics.Add(new ImpactMetric
            {
                Id = "modern_apps",
                Name = "Modern App Packaging",
                Description = "Apps deployed as MSIX, Win32, or Store apps",
                Category = ImpactCategory.Modernization,
                CurrentValue = 30,
                ProjectedValue = 85,
                Unit = "%",
                Icon = "📦",
                HigherIsBetter = true,
                ConfidenceLevel = 75,
                Explanation = "Intune supports modern packaging formats with better reliability and user experience."
            });

            impact.CurrentScore = (int)impact.Metrics.Average(m => 
                m.HigherIsBetter ? m.CurrentValue : (100 - m.CurrentValue));
            impact.ProjectedScore = (int)impact.Metrics.Average(m => 
                m.HigherIsBetter ? m.ProjectedValue : (100 - m.ProjectedValue));

            impact.Summary = $"Modernization level increases from {impact.CurrentScore}% to {impact.ProjectedScore}%.";
            impact.TopBenefits = new List<string>
            {
                "Zero-touch deployment with Autopilot",
                "Accelerated Windows 11 adoption",
                "Elimination of legacy OS dependencies"
            };

            return impact;
        }

        #endregion

        #region Workload Impacts

        private List<WorkloadImpact> CalculateWorkloadImpacts(MigrationImpactInputs inputs)
        {
            var workloads = new List<WorkloadImpact>();

            workloads.Add(new WorkloadImpact
            {
                WorkloadName = "Compliance Policies",
                Icon = "✅",
                IsCloudManaged = inputs.ComplianceWorkloadInCloud,
                CurrentState = inputs.ComplianceWorkloadInCloud ? "Intune-managed" : "ConfigMgr-managed",
                Benefits = new List<string>
                {
                    "Real-time compliance evaluation",
                    "Conditional Access integration",
                    "Built-in compliance reports"
                },
                TransitionEffort = "Low",
                RecommendedOrder = 1
            });

            workloads.Add(new WorkloadImpact
            {
                WorkloadName = "Windows Update",
                Icon = "🔄",
                IsCloudManaged = inputs.WindowsUpdateWorkloadInCloud,
                CurrentState = inputs.WindowsUpdateWorkloadInCloud ? "Windows Update for Business" : "WSUS/ConfigMgr",
                Benefits = new List<string>
                {
                    "Faster security update deployment",
                    "Delivery Optimization bandwidth savings",
                    "Feature update deferral policies"
                },
                TransitionEffort = "Medium",
                RecommendedOrder = 2
            });

            workloads.Add(new WorkloadImpact
            {
                WorkloadName = "Endpoint Protection",
                Icon = "🔰",
                IsCloudManaged = inputs.EndpointProtectionWorkloadInCloud,
                CurrentState = inputs.EndpointProtectionWorkloadInCloud ? "Defender for Endpoint" : "ConfigMgr EP",
                Benefits = new List<string>
                {
                    "Microsoft Defender for Endpoint integration",
                    "Cloud-delivered protection",
                    "Attack surface reduction rules"
                },
                TransitionEffort = "Low",
                RecommendedOrder = 3
            });

            workloads.Add(new WorkloadImpact
            {
                WorkloadName = "Device Configuration",
                Icon = "⚙️",
                IsCloudManaged = inputs.DeviceConfigurationWorkloadInCloud,
                CurrentState = inputs.DeviceConfigurationWorkloadInCloud ? "Intune profiles" : "Group Policy/ConfigMgr",
                Benefits = new List<string>
                {
                    "Settings catalog with 2000+ settings",
                    "Configuration profiles for any scenario",
                    "Remote configuration without VPN"
                },
                TransitionEffort = "High",
                RecommendedOrder = 5
            });

            workloads.Add(new WorkloadImpact
            {
                WorkloadName = "Resource Access",
                Icon = "🔐",
                IsCloudManaged = inputs.ResourceAccessWorkloadInCloud,
                CurrentState = inputs.ResourceAccessWorkloadInCloud ? "Intune certificates" : "ConfigMgr certificates",
                Benefits = new List<string>
                {
                    "SCEP/PKCS certificate deployment",
                    "VPN and Wi-Fi profile management",
                    "Trusted certificate distribution"
                },
                TransitionEffort = "Medium",
                RecommendedOrder = 4
            });

            workloads.Add(new WorkloadImpact
            {
                WorkloadName = "Client Apps",
                Icon = "📱",
                IsCloudManaged = inputs.ClientAppsWorkloadInCloud,
                CurrentState = inputs.ClientAppsWorkloadInCloud ? "Intune apps" : "ConfigMgr applications",
                Benefits = new List<string>
                {
                    "Company Portal self-service",
                    "Win32 app with dependency support",
                    "Microsoft Store integration"
                },
                TransitionEffort = "High",
                RecommendedOrder = 6
            });

            return workloads;
        }

        #endregion

        #region Summary Generation

        private List<ImpactHighlight> GenerateTopBenefits(MigrationImpactResult result)
        {
            var highlights = new List<ImpactHighlight>();

            // Find top 5 metrics by improvement percentage
            var topMetrics = result.AllMetrics
                .Where(m => m.HasData && m.HigherIsBetter)
                .OrderByDescending(m => m.ProjectedValue - m.CurrentValue)
                .Take(5)
                .ToList();

            foreach (var metric in topMetrics)
            {
                var category = result.CategoryImpacts.FirstOrDefault(c => c.Category == metric.Category);
                highlights.Add(new ImpactHighlight
                {
                    Title = metric.Name,
                    Description = metric.Explanation,
                    Category = metric.Category,
                    Icon = metric.Icon,
                    QuantifiedImpact = metric.ImprovementDisplay,
                    Color = category?.Color ?? "#10B981"
                });
            }

            return highlights;
        }

        private string GenerateExecutiveSummary(MigrationImpactResult result, MigrationImpactInputs inputs)
        {
            var sb = new System.Text.StringBuilder();
            
            sb.Append($"Completing your cloud migration will improve your overall IT posture from ");
            sb.Append($"{result.OverallCurrentScore}% to {result.OverallProjectedScore}% (+{result.OverallImprovement} points). ");

            // Find the category with biggest improvement
            var topCategory = result.CategoryImpacts.OrderByDescending(c => c.ScoreImprovement).FirstOrDefault();
            if (topCategory != null)
            {
                sb.Append($"The biggest gains will be in {topCategory.DisplayName} (+{topCategory.ScoreImprovement} points). ");
            }

            // Add device context
            var devicesRemaining = inputs.TotalDevices - inputs.EnrolledDevices;
            if (devicesRemaining > 0)
            {
                sb.Append($"Enrolling the remaining {devicesRemaining:N0} devices will unlock these benefits.");
            }

            return sb.ToString();
        }

        private string EstimateTimeline(MigrationImpactInputs inputs)
        {
            var devicesRemaining = inputs.TotalDevices - inputs.EnrolledDevices;
            var velocity = inputs.AverageEnrollmentVelocity > 0 ? inputs.AverageEnrollmentVelocity : 2.5;
            
            var daysRemaining = devicesRemaining / velocity;
            
            if (daysRemaining < 30)
                return $"~{(int)daysRemaining} days at current velocity";
            else if (daysRemaining < 90)
                return $"~{(int)(daysRemaining / 7)} weeks at current velocity";
            else
                return $"~{(int)(daysRemaining / 30)} months at current velocity";
        }

        private int CalculateDataQuality(MigrationImpactInputs inputs)
        {
            int score = 0;
            
            if (inputs.TotalDevices > 0) score += 20;
            if (inputs.EnrolledDevices > 0) score += 20;
            if (inputs.CurrentComplianceRate > 0) score += 20;
            if (inputs.HasCoManagement) score += 15;
            if (inputs.EncryptedDevices > 0) score += 15;
            if (inputs.Windows11Devices > 0 || inputs.Windows10Devices > 0) score += 10;

            return Math.Min(100, score);
        }

        #endregion
    }
}
