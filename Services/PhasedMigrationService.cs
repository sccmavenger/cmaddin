using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroTrustMigrationAddin.Models;

namespace ZeroTrustMigrationAddin.Services
{
    /// <summary>
    /// Generates phased migration plans with timelines and task lists.
    /// Takes the Autopatch approach: enroll devices over X timeframe with specific to-dos.
    /// </summary>
    public class PhasedMigrationService
    {
        private readonly GraphDataService _graphService;
        private readonly FileLogger _fileLogger;

        public PhasedMigrationService(GraphDataService graphService)
        {
            _graphService = graphService;
            _fileLogger = FileLogger.Instance;
        }

        /// <summary>
        /// Generates a complete phased migration plan based on total devices and target date
        /// </summary>
        public async Task<MigrationPlan> GeneratePhasedPlanAsync(
            int totalDevices,
            DateTime targetCompletionDate,
            int currentlyEnrolled = 0)
        {
            _fileLogger.Log(FileLogger.LogLevel.Info, 
                $"Generating phased migration plan: {totalDevices} devices, target: {targetCompletionDate:yyyy-MM-dd}");

            var remainingDevices = totalDevices - currentlyEnrolled;
            var weeksAvailable = Math.Max(1, (targetCompletionDate - DateTime.Now).TotalDays / 7);
            var devicesPerWeek = (int)Math.Ceiling(remainingDevices / weeksAvailable);

            var plan = new MigrationPlan
            {
                TotalDevices = totalDevices,
                CurrentlyEnrolled = currentlyEnrolled,
                TargetDate = targetCompletionDate,
                Phases = new List<MigrationPhase>(),
                GeneratedDate = DateTime.Now
            };

            // Phase 1: Pilot (Week 1-2) - Always start with pilot
            plan.Phases.Add(new MigrationPhase
            {
                PhaseNumber = 1,
                Name = "Phase 1: Pilot",
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddDays(14),
                DeviceCount = Math.Min(20, remainingDevices),
                Tasks = new List<string>
                {
                    "Week 1: Select 10-20 pilot devices (IT team members first)",
                    "Week 1: Enroll pilot devices via ConfigMgr co-management settings",
                    "Week 1: Deploy Compliance Policies workload to pilot group",
                    "Week 2: Monitor for enrollment issues, check Intune portal daily",
                    "Week 2: Collect feedback from pilot users about experience",
                    "Week 2: Document enrollment process and any gotchas discovered",
                    "Week 2: Get stakeholder approval to proceed to Wave 1"
                },
                SuccessCriteria = "95%+ pilot devices enrolled successfully with no critical issues reported",
                IsComplete = false
            });

            var devicesAssigned = 20;

            // Phase 2: Wave 1 - Early Adopters (Week 3-4)
            if (remainingDevices > 20)
            {
                var wave1Count = Math.Min(100, remainingDevices - devicesAssigned);
                plan.Phases.Add(new MigrationPhase
                {
                    PhaseNumber = 2,
                    Name = "Phase 2: Wave 1 - Early Adopters",
                    StartDate = DateTime.Now.AddDays(14),
                    EndDate = DateTime.Now.AddDays(28),
                    DeviceCount = wave1Count,
                    Tasks = new List<string>
                    {
                        $"Week 3: Enroll {wave1Count / 2} devices (target: Finance, HR departments)",
                        "Week 3: Deploy Device Configuration workload to Wave 1",
                        "Week 3: Monitor compliance scores daily, address issues quickly",
                        $"Week 4: Enroll remaining {wave1Count / 2} devices",
                        "Week 4: Verify all policies applying correctly",
                        "Week 4: Address any policy conflicts between ConfigMgr and Intune"
                    },
                    SuccessCriteria = "90%+ devices enrolled, compliance score >85%, no critical blockers",
                    IsComplete = false
                });
                devicesAssigned += wave1Count;
            }

            // Generate remaining waves (2-week cycles)
            var waveNumber = 3;
            var currentStartDay = 28;
            
            while (devicesAssigned < remainingDevices)
            {
                var waveDevices = Math.Min(
                    Math.Max(100, devicesPerWeek * 2), 
                    remainingDevices - devicesAssigned
                );

                plan.Phases.Add(GenerateWavePlan(
                    waveNumber,
                    waveDevices,
                    currentStartDay,
                    devicesAssigned,
                    remainingDevices
                ));

                devicesAssigned += waveDevices;
                currentStartDay += 14;
                waveNumber++;
            }

            _fileLogger.Log(FileLogger.LogLevel.Info, 
                $"Migration plan generated: {plan.Phases.Count} phases over {weeksAvailable:F0} weeks");

            return await Task.FromResult(plan);
        }

        private MigrationPhase GenerateWavePlan(
            int waveNumber, 
            int deviceCount, 
            int startDay,
            int alreadyAssigned,
            int totalRemaining)
        {
            var progressPercentage = (int)((alreadyAssigned / (double)totalRemaining) * 100);

            return new MigrationPhase
            {
                PhaseNumber = waveNumber,
                Name = $"Phase {waveNumber}: Wave {waveNumber - 1} - Production Rollout",
                StartDate = DateTime.Now.AddDays(startDay),
                EndDate = DateTime.Now.AddDays(startDay + 14),
                DeviceCount = deviceCount,
                Tasks = new List<string>
                {
                    $"Week {startDay / 7 + 1}: Enroll {deviceCount / 2} devices from identified groups",
                    $"Week {startDay / 7 + 1}: Continue workload migrations (now at {progressPercentage}% progress)",
                    $"Week {startDay / 7 + 2}: Enroll remaining {deviceCount / 2} devices",
                    $"Week {startDay / 7 + 2}: Monitor overall fleet health and compliance",
                    $"Week {startDay / 7 + 2}: Address any emerging patterns or issues"
                },
                SuccessCriteria = $"85%+ enrollment rate, compliance maintained above 80%",
                IsComplete = false
            };
        }

        /// <summary>
        /// Gets AI recommendations for the currently active phase
        /// </summary>
        public List<AIRecommendation> GetCurrentPhaseGuidance(MigrationPlan plan, int currentEnrollmentPercentage)
        {
            var recommendations = new List<AIRecommendation>();

            if (plan == null || !plan.Phases.Any())
                return recommendations;

            // Find current active phase
            var currentPhase = plan.Phases.FirstOrDefault(p =>
                DateTime.Now >= p.StartDate && DateTime.Now <= p.EndDate && !p.IsComplete);

            // If no active phase, check if we're behind schedule
            if (currentPhase == null)
            {
                var lastPhase = plan.Phases.OrderBy(p => p.EndDate).LastOrDefault();
                if (lastPhase != null && DateTime.Now > lastPhase.EndDate)
                {
                    recommendations.Add(new AIRecommendation
                    {
                        Title = "⏰ Migration Behind Schedule",
                        Description = "Target completion date has passed. Time to reassess the plan.",
                        ActionSteps = new List<string>
                        {
                            "1. Review what's blocking completion",
                            "2. Extend timeline or increase weekly enrollment velocity",
                            "3. Consider Microsoft FastTrack assistance"
                        },
                        Priority = RecommendationPriority.Critical,
                        Category = RecommendationCategory.StallPrevention
                    });
                }
                return recommendations;
            }

            // Guidance for current phase
            var daysRemaining = (currentPhase.EndDate - DateTime.Now).Days;
            var urgency = daysRemaining < 3 ? "⚠️ " : "";

            recommendations.Add(new AIRecommendation
            {
                Title = $"{urgency}📅 {currentPhase.Name} - Active Now",
                Description = $"You're currently in {currentPhase.Name} (ends {currentPhase.EndDate:MMM dd}, {daysRemaining} days remaining). " +
                              $"Target: {currentPhase.DeviceCount} devices this phase.",
                Rationale = currentPhase.SuccessCriteria,
                ActionSteps = currentPhase.Tasks,
                Priority = daysRemaining < 3 ? RecommendationPriority.High : RecommendationPriority.Medium,
                Category = RecommendationCategory.DeviceEnrollment,
                EstimatedEffort = $"{(currentPhase.EndDate - currentPhase.StartDate).Days} days",
                ImpactScore = 85
            });

            // Preview next phase
            var nextPhase = plan.Phases
                .Where(p => p.StartDate > currentPhase.EndDate)
                .OrderBy(p => p.StartDate)
                .FirstOrDefault();

            if (nextPhase != null)
            {
                recommendations.Add(new AIRecommendation
                {
                    Title = $"🔮 Next: {nextPhase.Name}",
                    Description = $"Starting {nextPhase.StartDate:MMM dd}. Begin preparation to maintain momentum.",
                    ActionSteps = new List<string>
                    {
                        $"1. Complete current phase tasks before {currentPhase.EndDate:MMM dd}",
                        "2. Review and document lessons learned from current phase",
                        $"3. Identify {nextPhase.DeviceCount} devices for next enrollment batch",
                        "4. Communicate timeline to next wave of users"
                    },
                    Priority = RecommendationPriority.Low,
                    Category = RecommendationCategory.General,
                    EstimatedEffort = "Pre-planning",
                    ImpactScore = 50
                });
            }

            // Check if on track
            var expectedProgress = CalculateExpectedProgress(plan);
            if (currentEnrollmentPercentage < expectedProgress - 10)
            {
                recommendations.Add(new AIRecommendation
                {
                    Title = "📉 Behind Schedule Alert",
                    Description = $"Current progress: {currentEnrollmentPercentage}%. Expected: {expectedProgress}% by now.",
                    Rationale = "Falling behind the planned schedule may jeopardize target completion date.",
                    ActionSteps = new List<string>
                    {
                        "1. Identify bottlenecks causing delays",
                        "2. Increase enrollment velocity (more devices per day)",
                        "3. Consider extending timeline or requesting additional resources",
                        "4. Schedule emergency review with project sponsor"
                    },
                    Priority = RecommendationPriority.High,
                    Category = RecommendationCategory.StallPrevention,
                    ImpactScore = 90
                });
            }

            return recommendations;
        }

        private int CalculateExpectedProgress(MigrationPlan plan)
        {
            var completedPhases = plan.Phases.Where(p => p.IsComplete || DateTime.Now > p.EndDate);
            var devicesExpected = completedPhases.Sum(p => p.DeviceCount);
            return (int)((devicesExpected / (double)plan.TotalDevices) * 100);
        }

        /// <summary>
        /// Marks a phase as complete and advances to next phase
        /// </summary>
        public void CompletePhase(MigrationPlan plan, int phaseNumber)
        {
            var phase = plan.Phases.FirstOrDefault(p => p.PhaseNumber == phaseNumber);
            if (phase != null)
            {
                phase.IsComplete = true;
                phase.CompletionDate = DateTime.Now;
                
                _fileLogger.Log(FileLogger.LogLevel.Info, 
                    $"Phase {phaseNumber} marked complete: {phase.Name}");
            }
        }
    }

    #region Models

    public class MigrationPlan
    {
        public int TotalDevices { get; set; }
        public int CurrentlyEnrolled { get; set; }
        public DateTime TargetDate { get; set; }
        public List<MigrationPhase> Phases { get; set; } = new List<MigrationPhase>();
        public DateTime GeneratedDate { get; set; }
        
        public int CurrentPhaseIndex
        {
            get
            {
                var currentPhase = Phases.FirstOrDefault(p =>
                    DateTime.Now >= p.StartDate && DateTime.Now <= p.EndDate);
                return currentPhase != null ? Phases.IndexOf(currentPhase) : -1;
            }
        }

        public double OverallProgress
        {
            get
            {
                var completedDevices = Phases.Where(p => p.IsComplete).Sum(p => p.DeviceCount);
                return TotalDevices > 0 ? (completedDevices / (double)TotalDevices) * 100 : 0;
            }
        }

        public string TimelineStatus
        {
            get
            {
                if (DateTime.Now > TargetDate && OverallProgress < 100)
                    return "Behind Schedule";
                if (OverallProgress >= 100)
                    return "Complete";
                return "On Track";
            }
        }
    }

    public class MigrationPhase
    {
        public int PhaseNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int DeviceCount { get; set; }
        public List<string> Tasks { get; set; } = new List<string>();
        public string SuccessCriteria { get; set; } = string.Empty;
        public bool IsComplete { get; set; }
        public DateTime? CompletionDate { get; set; }

        public int DaysRemaining => Math.Max(0, (EndDate - DateTime.Now).Days);
        public bool IsActive => DateTime.Now >= StartDate && DateTime.Now <= EndDate && !IsComplete;
        public bool IsUpcoming => DateTime.Now < StartDate;
        public bool IsPastDue => DateTime.Now > EndDate && !IsComplete;
    }

    #endregion
}
