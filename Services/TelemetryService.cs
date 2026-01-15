using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroTrustMigrationAddin.Models;

namespace ZeroTrustMigrationAddin.Services
{
    public class TelemetryService
    {
        private readonly Random _random = new Random();

        public async Task<MigrationStatus> GetMigrationStatusAsync()
        {
            // Simulate async data retrieval
            await Task.Delay(100);

            return new MigrationStatus
            {
                WorkloadsTransitioned = 3,
                TotalWorkloads = 7,
                ProjectedFinishDate = DateTime.Now.AddMonths(8),
                LastUpdateDate = DateTime.Now.AddDays(-2)
            };
        }

        public async Task<DeviceEnrollment> GetDeviceEnrollmentAsync()
        {
            await Task.Delay(100);

            var trendData = new List<EnrollmentTrend>();
            var baseDate = DateTime.Now.AddMonths(-6);

            for (int i = 0; i <= 6; i++)
            {
                trendData.Add(new EnrollmentTrend
                {
                    Month = baseDate.AddMonths(i),
                    IntuneDevices = 23000 + (i * 6900) + _random.Next(-2300, 2300),
                    ConfigMgrDevices = 92000 - (i * 5520)
                });
            }

            return new DeviceEnrollment
            {
                TotalDevices = 115000,
                IntuneEnrolledDevices = 64400,
                ConfigMgrOnlyDevices = 50600,
                TrendData = trendData.ToArray(),
                // Mock join type data for visualization
                HybridJoinedDevices = 78000,  // 68% - Most enterprise devices
                AzureADOnlyDevices = 22000,   // 19% - Cloud-native devices
                OnPremDomainOnlyDevices = 12000,  // 10% - Need Hybrid AAD Join
                WorkgroupDevices = 3000,      // 3% - Need domain join
                UnknownJoinTypeDevices = 0
            };
        }

        public async Task<List<Workload>> GetWorkloadsAsync()
        {
            await Task.Delay(100);

            return new List<Workload>
            {
                new Workload { Name = "Compliance Policies", Status = WorkloadStatus.Completed, Description = "Device compliance policies moved to Intune", TransitionDate = DateTime.Now.AddMonths(-3) },
                new Workload { Name = "Device Configuration", Status = WorkloadStatus.Completed, Description = "Configuration profiles migrated", TransitionDate = DateTime.Now.AddMonths(-2) },
                new Workload { Name = "Endpoint Protection", Status = WorkloadStatus.Completed, Description = "Antivirus and security settings", TransitionDate = DateTime.Now.AddMonths(-1) },
                new Workload { Name = "Windows Update for Business", Status = WorkloadStatus.InProgress, Description = "Windows update policies", LearnMoreUrl = "https://docs.microsoft.com/mem/intune/protect/windows-update-for-business-configure" },
                new Workload { Name = "Client Apps", Status = WorkloadStatus.NotStarted, Description = "Application deployment and management", LearnMoreUrl = "https://docs.microsoft.com/mem/intune/apps/apps-add" },
                new Workload { Name = "Office Click-to-Run Apps", Status = WorkloadStatus.NotStarted, Description = "Microsoft 365 Apps management", LearnMoreUrl = "https://docs.microsoft.com/mem/intune/apps/apps-add-office365" },
                new Workload { Name = "Resource Access Policies", Status = WorkloadStatus.NotStarted, Description = "VPN, Wi-Fi, and certificate profiles", LearnMoreUrl = "https://docs.microsoft.com/mem/intune/configuration/device-profiles" }
            };
        }

        public async Task<ComplianceScore> GetComplianceScoreAsync()
        {
            await Task.Delay(100);

            return new ComplianceScore
            {
                IntuneScore = 87.5,
                ConfigMgrScore = 72.3,
                RiskAreas = new[] { "Devices lacking Conditional Access", "Outdated OS versions", "Missing encryption" },
                DevicesLackingConditionalAccess = 450
            };
        }

        public async Task<EnrollmentAccelerationInsight> GetEnrollmentAccelerationInsightAsync()
        {
            await Task.Delay(100);

            return new EnrollmentAccelerationInsight
            {
                YourWeeklyEnrollmentRate = 45,
                PeerAverageRate = 78,
                DevicesNeededToMatchPeers = 132,
                RecommendedAction = "Increase batch size to 100 devices/week to match peer velocity",
                OrganizationCategory = "Enterprise (5000-10000 devices)",
                SpecificTactics = new List<string>
                {
                    "Use Phase 3 Autonomous Agent to auto-enroll ready devices",
                    "Target 'Good' readiness devices (60-79 score) for quick wins",
                    "Schedule CMG capacity expansion for 200+ concurrent enrollments"
                },
                EstimatedWeeksToMatchPeers = 3
            };
        }

        public async Task<SavingsUnlockInsight> GetSavingsUnlockInsightAsync()
        {
            await Task.Delay(100);

            return new SavingsUnlockInsight
            {
                SavingsLockedBehindEnrollment = 180000m,
                DevicesNeededToUnlock = 850,
                NextSavingsMilestone = "$50,000 annual savings unlocked at 75% enrollment",
                SavingsFromWorkloadTransition = 105000m,
                WorkloadToTransition = "Conditional Access",
                RecommendedAction = "Enroll 132 devices this week to unlock $8,500/month savings"
            };
        }

        public async Task<List<Alert>> GetAlertsAsync()
        {
            await Task.Delay(100);

            return new List<Alert>
            {
                new Alert
                {
                    Severity = AlertSeverity.Warning,
                    Title = "Migration Stall Detected",
                    Description = "No workload changes in 45 days. Consider scheduling FastTrack assistance.",
                    ActionText = "Schedule FastTrack",
                    DetectedDate = DateTime.Now.AddDays(-5)
                },
                new Alert
                {
                    Severity = AlertSeverity.Info,
                    Title = "Enrollment Trending Up",
                    Description = "Intune enrollment increased by 15% this month.",
                    ActionText = "View Details",
                    DetectedDate = DateTime.Now.AddDays(-1)
                }
            };
        }

        public async Task<List<ProgressTarget>> GetProgressTargetsAsync()
        {
            await Task.Delay(100);

            return new List<ProgressTarget>
            {
                new ProgressTarget
                {
                    TargetDescription = "Reach 75% device enrollment",
                    TargetDate = DateTime.Now.AddDays(21),
                    DevicesRequired = 425,
                    WorkloadsRequired = 0,
                    ActionToAchieve = "Run Phase 3 agent with 100 device/week batch size",
                    IsEnrollmentTarget = true,
                    IsWorkloadTarget = false,
                    Icon = "🎯"
                },
                new ProgressTarget
                {
                    TargetDescription = "Transition Conditional Access workload",
                    TargetDate = DateTime.Now.AddDays(14),
                    DevicesRequired = 0,
                    WorkloadsRequired = 1,
                    ActionToAchieve = "Validate 95%+ policy compliance, then shift slider to Intune",
                    IsEnrollmentTarget = false,
                    IsWorkloadTarget = true,
                    Icon = "🔐"
                },
                new ProgressTarget
                {
                    TargetDescription = "Complete endpoint security migration",
                    TargetDate = DateTime.Now.AddDays(35),
                    DevicesRequired = 150,
                    WorkloadsRequired = 2,
                    ActionToAchieve = "Enroll remaining high-priority devices, transition Endpoint Protection + Windows Update",
                    IsEnrollmentTarget = true,
                    IsWorkloadTarget = true,
                    Icon = "🛡️"
                }
            };
        }

        public async Task<List<Milestone>> GetMilestonesAsync()
        {
            await Task.Delay(100);

            return new List<Milestone>
            {
                new Milestone
                {
                    Title = "50% Device Enrollment Achieved",
                    AchievedDate = DateTime.Now.AddDays(-15),
                    Description = "Reached 1,250 devices enrolled in Intune",
                    Icon = "🎯"
                },
                new Milestone
                {
                    Title = "Endpoint Protection Completed",
                    AchievedDate = DateTime.Now.AddMonths(-1),
                    Description = "Successfully transitioned all security policies",
                    Icon = "🛡️"
                },
                new Milestone
                {
                    Title = "Tenant Attach Configured",
                    AchievedDate = DateTime.Now.AddMonths(-3),
                    Description = "ConfigMgr connected to Microsoft Endpoint Manager",
                    Icon = "🔗"
                }
            };
        }

        public async Task<List<Blocker>> GetBlockersAsync()
        {
            await Task.Delay(100);

            return new List<Blocker>
            {
                new Blocker
                {
                    Title = "Legacy OS Versions",
                    Description = "320 devices running Windows 7 or older",
                    AffectedDevices = 320,
                    RemediationUrl = "https://docs.microsoft.com/windows/deployment/upgrade/",
                    Severity = BlockerSeverity.High
                },
                new Blocker
                {
                    Title = "Missing Azure AD Join",
                    Description = "185 devices not Azure AD joined or hybrid joined",
                    AffectedDevices = 185,
                    RemediationUrl = "https://docs.microsoft.com/azure/active-directory/devices/",
                    Severity = BlockerSeverity.Medium
                },
                new Blocker
                {
                    Title = "Incompatible Applications",
                    Description = "42 legacy applications need packaging updates",
                    AffectedDevices = 420,
                    RemediationUrl = "https://docs.microsoft.com/mem/intune/apps/apps-win32-app-management",
                    Severity = BlockerSeverity.Low
                }
            };
        }

        public async Task<List<EngagementOption>> GetEngagementOptionsAsync()
        {
            await Task.Delay(100);

            return new List<EngagementOption>
            {
                new EngagementOption
                {
                    Title = "Schedule FastTrack",
                    Description = "Get personalized migration assistance from Microsoft experts",
                    Url = "https://www.microsoft.com/fasttrack",
                    IconPath = "🚀"
                },
                new EngagementOption
                {
                    Title = "Community Resources",
                    Description = "Connect with peers and Microsoft MVPs",
                    Url = "https://techcommunity.microsoft.com/t5/microsoft-endpoint-manager/ct-p/MicrosoftEndpointManager",
                    IconPath = "👥"
                },
                new EngagementOption
                {
                    Title = "Documentation",
                    Description = "Step-by-step migration guides and best practices",
                    Url = "https://docs.microsoft.com/mem/configmgr/comanage/",
                    IconPath = "📚"
                }
            };
        }

        /// <summary>
        /// Gets mock enrollment momentum insights for unauthenticated state.
        /// This is shown when user clicks "Generate Insights" without AI credentials configured.
        /// </summary>
        public async Task<EnrollmentMomentumInsight> GetMockEnrollmentInsightsAsync()
        {
            await Task.Delay(500); // Simulate API call delay

            return new EnrollmentMomentumInsight
            {
                CurrentVelocity = 35,
                RecommendedVelocity = 75,
                OptimalBatchSize = 100,
                VelocityAssessment = "Below recommended pace for organization size",
                InfrastructureBlockers = new List<string>
                {
                    "Cloud Management Gateway not deployed - limits remote device enrollment",
                    "Co-management workload transitions paused - reduces enrollment motivation",
                    "Autopilot profiles not configured - missing streamlined enrollment experience"
                },
                AccelerationStrategies = new List<AccelerationStrategy>
                {
                    new AccelerationStrategy
                    {
                        Action = "Deploy Cloud Management Gateway (CMG)",
                        Impact = "Enables enrollment of 500+ remote devices",
                        EffortLevel = "Medium"
                    },
                    new AccelerationStrategy
                    {
                        Action = "Configure Autopilot bulk enrollment profiles",
                        Impact = "Can increase velocity by 2-3x with zero-touch deployment",
                        EffortLevel = "Low"
                    },
                    new AccelerationStrategy
                    {
                        Action = "Target departments with high compliance scores first",
                        Impact = "Reduces enrollment failures by 40%",
                        EffortLevel = "Low"
                    },
                    new AccelerationStrategy
                    {
                        Action = "Schedule enrollment windows during off-peak hours",
                        Impact = "Minimizes user disruption and support tickets",
                        EffortLevel = "Low"
                    }
                },
                WeekByWeekRoadmap = new List<WeeklyTarget>
                {
                    new WeeklyTarget
                    {
                        Week = 1,
                        TargetDevices = 50,
                        FocusArea = "Pilot group validation - IT department devices"
                    },
                    new WeeklyTarget
                    {
                        Week = 2,
                        TargetDevices = 100,
                        FocusArea = "Primary enrollment wave - Sales and Marketing teams"
                    },
                    new WeeklyTarget
                    {
                        Week = 3,
                        TargetDevices = 100,
                        FocusArea = "Secondary enrollment wave - Finance and HR teams"
                    },
                    new WeeklyTarget
                    {
                        Week = 4,
                        TargetDevices = 75,
                        FocusArea = "Cleanup and stragglers - remaining devices and troubleshooting"
                    }
                },
                ProjectedCompletionWeeks = 14,
                Rationale = "Based on your organization size (2,500 devices) and current velocity (35 devices/week), " +
                           "increasing to 75 devices/week would reduce completion time from 30+ weeks to 14 weeks. " +
                           "This velocity is achievable with CMG deployment and Autopilot configuration.",
                IsAIPowered = false // Mock data, not from GPT-4
            };
        }

        public async Task RefreshDataAsync()
        {
            // Simulate data refresh from APIs
            await Task.Delay(500);
        }
    }
}
