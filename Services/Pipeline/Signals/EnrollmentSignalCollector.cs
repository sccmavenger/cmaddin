using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZeroTrustMigrationAddin.Models;

namespace ZeroTrustMigrationAddin.Services.Pipeline.Signals
{
    /// <summary>
    /// Collects enrollment data from Graph API and ConfigMgr, producing a normalized EnrollmentSignal.
    /// Wraps existing GraphDataService and EnrollmentAnalyticsService.
    /// </summary>
    public class EnrollmentSignalCollector : SignalCollectorBase<EnrollmentSignal>
    {
        private readonly GraphDataService _graphDataService;
        private readonly EnrollmentAnalyticsService _analyticsService;

        public override string Name => "EnrollmentSignalCollector";
        protected override TimeSpan CacheTtl => TimeSpan.FromMinutes(5);

        public EnrollmentSignalCollector(GraphDataService graphDataService)
        {
            _graphDataService = graphDataService ?? throw new ArgumentNullException(nameof(graphDataService));
            _analyticsService = new EnrollmentAnalyticsService(graphDataService);
        }

        protected override async Task<EnrollmentSignal?> CollectCoreAsync(CancellationToken ct)
        {
            // Gather enrollment data from existing services
            var deviceEnrollment = await _graphDataService.GetDeviceEnrollmentAsync();
            if (deviceEnrollment == null)
                return null;

            ct.ThrowIfCancellationRequested();

            // Get analytics (velocity, trend, confidence)
            var analytics = await _analyticsService.ComputeAsync(ct);

            var signal = new EnrollmentSignal
            {
                TotalDevices = deviceEnrollment.TotalDevices,
                EnrolledDevices = deviceEnrollment.IntuneEnrolledDevices,
                CoManagedDevices = deviceEnrollment.CoManagedDevices,
                ConfigMgrOnlyDevices = deviceEnrollment.ConfigMgrOnlyDevices,
                CloudNativeDevices = deviceEnrollment.CloudNativeDevices,

                // Velocity from analytics
                Velocity7Day = analytics.Trend.Velocity7Day,
                Velocity30Day = analytics.Trend.Velocity30,
                Velocity60Day = analytics.Trend.Velocity60,
                Velocity90Day = analytics.Trend.Velocity90,
                WeekOverWeekChange = analytics.Trend.WeekOverWeekChange,
                TrendState = analytics.Trend.TrendState,

                // Stall duration
                DaysSinceLastEnrollment = analytics.Snapshots
                    .Where(s => s.NewEnrollmentsCount > 0)
                    .OrderByDescending(s => s.Date)
                    .Select(s => (int)(DateTime.UtcNow - s.Date).TotalDays)
                    .FirstOrDefault(),

                Snapshots = analytics.Snapshots,
                ConfidenceScore = analytics.Confidence.Score,
                IsLiveData = !deviceEnrollment.IsMockData,
                CollectedAt = DateTime.UtcNow
            };

            return signal;
        }
    }
}
