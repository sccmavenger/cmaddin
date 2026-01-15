using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ZeroTrustMigrationAddin.Models;
using ZeroTrustMigrationAddin.Services;
using LiveCharts;
using LiveCharts.Wpf;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.ViewModels
{
    /// <summary>
    /// ViewModel for the Enrollment Momentum panel.
    /// Displays velocity metrics, trend analysis, and stall risk.
    /// </summary>
    public class EnrollmentMomentumViewModel : ViewModelBase
    {
        private readonly EnrollmentAnalyticsService _analyticsService;
        private CancellationTokenSource? _cts;

        #region Observable Properties

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _loadingMessage = "Loading enrollment analytics...";
        public string LoadingMessage
        {
            get => _loadingMessage;
            set => SetProperty(ref _loadingMessage, value);
        }

        private int _totalConfigMgrDevices;
        public int TotalConfigMgrDevices
        {
            get => _totalConfigMgrDevices;
            set => SetProperty(ref _totalConfigMgrDevices, value);
        }

        private int _totalIntuneDevices;
        public int TotalIntuneDevices
        {
            get => _totalIntuneDevices;
            set => SetProperty(ref _totalIntuneDevices, value);
        }

        private int _gap;
        public int Gap
        {
            get => _gap;
            set => SetProperty(ref _gap, value);
        }

        private double _enrolledPct;
        public double EnrolledPct
        {
            get => _enrolledPct;
            set => SetProperty(ref _enrolledPct, value);
        }

        private string _enrolledPctDisplay = "0%";
        public string EnrolledPctDisplay
        {
            get => _enrolledPctDisplay;
            set => SetProperty(ref _enrolledPctDisplay, value);
        }

        // Velocity Metrics
        private double _velocity7Day;
        public double Velocity7Day
        {
            get => _velocity7Day;
            set => SetProperty(ref _velocity7Day, value);
        }

        private double _velocity30;
        public double Velocity30
        {
            get => _velocity30;
            set => SetProperty(ref _velocity30, value);
        }

        private double _devicesPerWeek;
        public double DevicesPerWeek
        {
            get => _devicesPerWeek;
            set => SetProperty(ref _devicesPerWeek, value);
        }

        private string _trendDescription = "Loading...";
        public string TrendDescription
        {
            get => _trendDescription;
            set => SetProperty(ref _trendDescription, value);
        }

        private string _trendState = "Unknown";
        public string TrendState
        {
            get => _trendState;
            set => SetProperty(ref _trendState, value);
        }

        private double _weekOverWeekChange;
        public double WeekOverWeekChange
        {
            get => _weekOverWeekChange;
            set => SetProperty(ref _weekOverWeekChange, value);
        }

        private string _weekOverWeekDisplay = "0%";
        public string WeekOverWeekDisplay
        {
            get => _weekOverWeekDisplay;
            set => SetProperty(ref _weekOverWeekDisplay, value);
        }

        // Stall Risk
        private bool _isAtRisk;
        public bool IsAtRisk
        {
            get => _isAtRisk;
            set => SetProperty(ref _isAtRisk, value);
        }

        private string _stallRiskLevel = "None";
        public string StallRiskLevel
        {
            get => _stallRiskLevel;
            set => SetProperty(ref _stallRiskLevel, value);
        }

        private string _stallRiskDescription = "";
        public string StallRiskDescription
        {
            get => _stallRiskDescription;
            set => SetProperty(ref _stallRiskDescription, value);
        }

        private bool _isTrustTroughRisk;
        public bool IsTrustTroughRisk
        {
            get => _isTrustTroughRisk;
            set => SetProperty(ref _isTrustTroughRisk, value);
        }

        // Chart Data
        private SeriesCollection _velocitySeries = new();
        public SeriesCollection VelocitySeries
        {
            get => _velocitySeries;
            set => SetProperty(ref _velocitySeries, value);
        }

        private string[] _chartLabels = Array.Empty<string>();
        public string[] ChartLabels
        {
            get => _chartLabels;
            set => SetProperty(ref _chartLabels, value);
        }

        private Func<double, string> _chartYFormatter = value => value.ToString("F0");
        public Func<double, string> ChartYFormatter
        {
            get => _chartYFormatter;
            set => SetProperty(ref _chartYFormatter, value);
        }

        // Full analytics result
        private EnrollmentAnalyticsResult? _analyticsResult;
        public EnrollmentAnalyticsResult? AnalyticsResult
        {
            get => _analyticsResult;
            set => SetProperty(ref _analyticsResult, value);
        }

        #endregion

        #region Commands

        public ICommand RefreshCommand { get; }
        public ICommand ViewDetailsCommand { get; }

        #endregion

        public EnrollmentMomentumViewModel(GraphDataService graphDataService)
        {
            _analyticsService = new EnrollmentAnalyticsService(graphDataService);
            
            RefreshCommand = new RelayCommand(async () => await RefreshAsync());
            ViewDetailsCommand = new RelayCommand(() => ViewDetails());
        }

        /// <summary>
        /// Refreshes the enrollment analytics data.
        /// </summary>
        public async Task RefreshAsync()
        {
            // Cancel any existing operation
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            IsLoading = true;
            LoadingMessage = "Computing enrollment analytics...";

            try
            {
                var result = await _analyticsService.ComputeAsync(_cts.Token);
                AnalyticsResult = result;

                // Update core metrics
                TotalConfigMgrDevices = result.TotalConfigMgrDevices;
                TotalIntuneDevices = result.TotalIntuneDevices;
                Gap = result.Gap;
                EnrolledPct = result.EnrolledPct;
                EnrolledPctDisplay = $"{result.EnrolledPct:F1}%";

                // Update velocity metrics
                Velocity7Day = result.Trend.Velocity7Day;
                Velocity30 = result.Trend.Velocity30;
                DevicesPerWeek = result.Trend.DevicesPerWeek;
                TrendDescription = result.Trend.TrendDescription;
                TrendState = result.Trend.TrendState.ToString();
                WeekOverWeekChange = result.Trend.WeekOverWeekChange;
                WeekOverWeekDisplay = result.Trend.WeekOverWeekChange >= 0 
                    ? $"+{result.Trend.WeekOverWeekChange:F1}%" 
                    : $"{result.Trend.WeekOverWeekChange:F1}%";

                // Update stall risk
                IsAtRisk = result.StallRisk.IsAtRisk;
                StallRiskLevel = result.StallRisk.RiskLevelDisplay;
                StallRiskDescription = result.StallRisk.RiskDescription;
                IsTrustTroughRisk = result.StallRisk.IsTrustTroughRisk;

                // Update chart
                UpdateChart(result.Snapshots);

                Instance.Info("[MOMENTUM VM] Analytics refreshed successfully");
            }
            catch (OperationCanceledException)
            {
                Instance.Info("[MOMENTUM VM] Refresh cancelled");
            }
            catch (Exception ex)
            {
                Instance.Error($"[MOMENTUM VM] Refresh failed: {ex.Message}");
                TrendDescription = "❌ Error loading data";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateChart(List<EnrollmentSnapshot> snapshots)
        {
            if (snapshots == null || !snapshots.Any()) return;

            // Take last 30 days for the chart
            var chartData = snapshots.TakeLast(30).ToList();
            
            var enrolledValues = new ChartValues<double>(
                chartData.Select(s => (double)s.TotalIntuneDevices));
            
            var velocityValues = new ChartValues<double>(
                chartData.Select(s => (double)s.NewEnrollmentsCount));

            VelocitySeries = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Enrolled Devices",
                    Values = enrolledValues,
                    PointGeometry = null,
                    Fill = System.Windows.Media.Brushes.Transparent,
                    Stroke = System.Windows.Media.Brushes.DodgerBlue,
                    StrokeThickness = 2
                },
                new ColumnSeries
                {
                    Title = "New Enrollments/Day",
                    Values = velocityValues,
                    Fill = System.Windows.Media.Brushes.Green,
                    MaxColumnWidth = 10
                }
            };

            ChartLabels = chartData.Select(s => s.Date.ToString("MM/dd")).ToArray();
        }

        private void ViewDetails()
        {
            // Open detailed analytics view
            Instance.Info("[MOMENTUM VM] View details clicked");
        }
    }
}
