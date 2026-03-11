using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroTrustMigrationAddin.Models;

namespace ZeroTrustMigrationAddin.Services.Pipeline.Analyzers
{
    /// <summary>
    /// Detects enrollment stalls including Trust Trough zone detection,
    /// velocity decline analysis, and root-cause classification.
    /// Migrates and extends logic from EnrollmentAnalyticsService.AssessStallRisk().
    /// </summary>
    public class EnrollmentStallAnalyzer : AnalyzerBase<EnrollmentSignal, EnrollmentStallAssessment>
    {
        public override string Name => "EnrollmentStallAnalyzer";
        public override int Priority => 10; // Run early — enrollment stalls are highest priority

        protected override Task<EnrollmentStallAssessment> AnalyzeCoreAsync(
            EnrollmentSignal signal, CancellationToken ct)
        {
            var options = EnrollmentScoringOptions.Current;
            var assessment = new EnrollmentStallAssessment
            {
                CurrentEnrollmentPercentage = signal.EnrolledPercentage
            };

            // === Trust Trough Detection (50-60% with declining velocity) ===
            bool inTrustTroughZone = signal.EnrolledPercentage >= options.TrustTroughLowerPct
                                     && signal.EnrolledPercentage <= options.TrustTroughUpperPct;

            bool hasSlowVelocity = signal.TrendState == TrendState.Declining
                                   || signal.TrendState == TrendState.Stalled
                                   || signal.TrendState == TrendState.Steady;

            if (inTrustTroughZone && hasSlowVelocity && signal.DaysSinceLastEnrollment > 30)
            {
                assessment.IsStalled = true;
                assessment.IsTrustTroughRisk = true;
                assessment.Severity = SeverityLevel.High;
                assessment.StallDurationDays = signal.DaysSinceLastEnrollment;
                assessment.EnrollmentPercentageAtStall = signal.EnrolledPercentage;
                assessment.Classification = ClassifyRootCause(signal);

                assessment.ContributingFactors.AddRange(new[]
                {
                    "Migration is in the 'Trust Trough' zone (50-60% enrollment)",
                    $"Enrollment velocity has {signal.TrendState.ToString().ToLower()} (7-day: {signal.Velocity7Day:F1} devices/day)",
                    $"No significant enrollment activity in {signal.DaysSinceLastEnrollment} days",
                    "Remaining devices likely have higher complexity or organizational resistance"
                });
            }
            // === General Stall Detection (near-zero velocity) ===
            else if (signal.TrendState == TrendState.Stalled)
            {
                assessment.IsStalled = true;
                assessment.StallDurationDays = signal.DaysSinceLastEnrollment;
                assessment.EnrollmentPercentageAtStall = signal.EnrolledPercentage;
                assessment.Classification = ClassifyRootCause(signal);

                assessment.Severity = signal.DaysSinceLastEnrollment > options.StallRiskDaysThreshold
                    ? SeverityLevel.Critical
                    : SeverityLevel.Medium;

                assessment.ContributingFactors.Add(
                    $"Near-zero enrollment velocity for {signal.DaysSinceLastEnrollment} days");

                if (signal.EnrolledPercentage < 25)
                    assessment.ContributingFactors.Add("Migration is still in early stages — may indicate launch issues");
                else if (signal.EnrolledPercentage > 80)
                    assessment.ContributingFactors.Add("Final 20% of devices — typically the most complex and resistant");
            }
            // === Declining Velocity Warning ===
            else if (signal.TrendState == TrendState.Declining)
            {
                assessment.IsStalled = false;
                assessment.Severity = SeverityLevel.Low;
                assessment.Classification = StallClassification.Operational;
                assessment.ContributingFactors.Add(
                    $"Week-over-week velocity decreased {signal.WeekOverWeekChange:F0}%");
            }
            // === No Stall ===
            else
            {
                assessment.IsStalled = false;
                assessment.Severity = SeverityLevel.None;
                assessment.Classification = StallClassification.None;
            }

            // === Cost of Inaction Quantification ===
            if (assessment.IsStalled)
            {
                int unenrolledDevices = signal.TotalDevices - signal.EnrolledDevices;
                assessment.DevicesWithExtendedPatchLatency = unenrolledDevices;
                assessment.PatchLatencyImpact =
                    $"{unenrolledDevices} devices have extended patch latency (ConfigMgr WSUS vs Intune WUfB)";
                assessment.ZeroTrustGapDescription =
                    $"{unenrolledDevices} devices are not reporting to Intune compliance — " +
                    "Conditional Access policies cannot enforce Zero Trust posture for these endpoints";
            }

            // === Trust Reset Batch Sizing ===
            if (assessment.IsStalled || assessment.IsTrustTroughRisk)
            {
                // Recommend 20-50 devices from the "Excellent" readiness tier
                int candidatePool = signal.ExcellentDevices > 0
                    ? signal.ExcellentDevices
                    : signal.GoodDevices;
                assessment.TrustResetBatchSize = candidatePool > 50 ? 50
                    : candidatePool > 20 ? candidatePool
                    : 20;
            }

            return Task.FromResult(assessment);
        }

        /// <summary>
        /// Classifies the root cause of a stall: Technical, Operational, Confidence-based, or Resource-constrained.
        /// </summary>
        private static StallClassification ClassifyRootCause(EnrollmentSignal signal)
        {
            // Confidence-based: High enrollment % + stall = fear of finishing / removing agent
            if (signal.EnrolledPercentage > 70 && signal.ConfidenceScore < 50)
                return StallClassification.ConfidenceBased;

            // Technical: Low confidence + many poor-health devices
            if (signal.PoorDevices > signal.ExcellentDevices && signal.ConfidenceScore < 40)
                return StallClassification.Technical;

            // Resource-constrained: Velocity was good but dropped to near-zero suddenly
            if (signal.Velocity90Day > 2.0 && signal.Velocity7Day < 0.5)
                return StallClassification.ResourceConstrained;

            // Default: Operational
            return StallClassification.Operational;
        }
    }
}
