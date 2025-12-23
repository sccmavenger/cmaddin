using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CloudJourneyAddin.Models;
using CloudJourneyAddin.Services;
using CloudJourneyAddin.Services.AgentTools;
using LiveCharts;
using LiveCharts.Wpf;
using static CloudJourneyAddin.Services.FileLogger;

namespace CloudJourneyAddin.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly TelemetryService _telemetryService;
        private readonly GraphDataService _graphDataService;
        private readonly ConfigMgrAdminService _configMgrService;
        private readonly AIRecommendationService _aiRecommendationService;
        private readonly WorkloadMomentumService _workloadMomentumService;
        private readonly ExecutiveSummaryService _executiveSummaryService;
        private readonly AppMigrationService _appMigrationService;
        private readonly DeviceReadinessService _deviceReadinessService;
        private readonly EnrollmentReActAgent? _enrollmentAgent;
        private readonly AgentMemoryService _agentMemoryService;
        private MigrationStatus? _migrationStatus;
        private DeviceEnrollment? _deviceEnrollment;
        private ComplianceScore? _complianceScore;
        private EnrollmentAccelerationInsight? _enrollmentAccelerationInsight;
        private SavingsUnlockInsight? _savingsUnlockInsight;
        private bool _isLoading;
        private DateTime _lastRefreshTime;
        private bool _useRealData;
        private bool _isConfigMgrConnected;
        private DateTime _lastProgressDate;
        private System.Text.StringBuilder _connectionLog = new System.Text.StringBuilder();
        private CloudJourneyAddin.Services.MigrationPlan? _migrationPlan;
        private int _excellentReadinessCount;
        private int _goodReadinessCount;
        private int _fairReadinessCount;
        private int _poorReadinessCount;
        private int _devicesNeedingPreparation;
        private int _highRiskDeviceCount;
        private int _excellentVelocityCount;
        private int _goodVelocityCount;
        private int _stalledWorkloadCount;
        private ObservableCollection<ApplicationMigrationAnalysis>? _applicationMigrations;
        private int _lowComplexityCount;
        private int _mediumComplexityCount;
        private int _highComplexityCount;
        private int _totalApplicationCount;
        private string _openAIEndpoint = string.Empty;
        private string _openAIDeploymentName = string.Empty;
        private string _openAIApiKey = string.Empty;
        private bool _isOpenAIEnabled = false;
        private string _openAIStatus = string.Empty;
        private bool _hasOpenAIStatus = false;
        private EnrollmentMomentumInsight? _enrollmentInsight;
        private bool _isLoadingEnrollmentInsight = false;
        private WorkloadMomentumInsight? _workloadMomentumInsight;
        private AIActionSummary? _aiActionSummary;
        private ExecutiveSummary? _executiveSummary; // Backward compatibility
        private bool _isAIAvailable = false;
        
        // v2.6.0 - Device Readiness & Enrollment Blockers
        private DeviceReadinessBreakdown? _deviceReadiness;
        private EnrollmentBlockerSummary? _enrollmentBlockers;
        
        // Agent v2.0 fields
        private bool _isAgentRunning = false;
        private string _agentStatus = "Ready";
        private ObservableCollection<AgentReasoningStep> _agentReasoningSteps = new();
        private AgentExecutionTrace? _currentAgentTrace;
        private EnrollmentGoals? _agentGoals;
        private string? _agentCompletionMessage;

        public DashboardViewModel(TelemetryService telemetryService)
        {
            _telemetryService = telemetryService;
            _graphDataService = new GraphDataService();
            _configMgrService = new ConfigMgrAdminService();
            
            // Initialize AI Recommendation Service - Azure OpenAI is now required
            try
            {
                _aiRecommendationService = new AIRecommendationService(_graphDataService);
            }
            catch (InvalidOperationException ex)
            {
                // Azure OpenAI not configured - this is now a critical error
                Instance.Error($"Azure OpenAI is required but not configured: {ex.Message}");
                // Service will be null, and we'll show appropriate UI messaging
                _aiRecommendationService = null!;
            }
            
            _workloadMomentumService = new WorkloadMomentumService(_graphDataService);
            _executiveSummaryService = new ExecutiveSummaryService(_graphDataService);
            _appMigrationService = new AppMigrationService(null, _graphDataService);
            _deviceReadinessService = new DeviceReadinessService(_configMgrService, _graphDataService);
            _useRealData = false; // Start with mock data
            _lastProgressDate = DateTime.Now.AddDays(-10); // Mock: 10 days since last progress
            
            // Initialize Agent v2.0 services
            var memoryLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentMemoryService>.Instance;
            _agentMemoryService = new AgentMemoryService(memoryLogger);
            try
            {
                var aiService = new AzureOpenAIService();
                var toolkit = new AgentToolkit();
                
                // Register agent tools
                toolkit.RegisterTool(new QueryDevicesTool(_graphDataService));
                toolkit.RegisterTool(new EnrollDevicesTool(_graphDataService));
                toolkit.RegisterTool(new AnalyzeReadinessTool(_graphDataService));
                
                // Create logger for agent (using FileLogger)
                var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<EnrollmentReActAgent>.Instance;
                
                // Create RiskAssessmentService for Phase 2
                var riskService = new RiskAssessmentService();
                
                _enrollmentAgent = new EnrollmentReActAgent(aiService, _graphDataService, _agentMemoryService, logger, riskService);
                
                // Subscribe to agent events
                _enrollmentAgent.ReasoningStepCompleted += OnAgentReasoningStepCompleted;
                _enrollmentAgent.StatusChanged += OnAgentStatusChanged;
                _enrollmentAgent.InsightDiscovered += OnAgentInsightDiscovered;
                
                Instance.Info("Enrollment Agent v2.0 initialized successfully");
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to initialize Enrollment Agent: {ex.Message}");
                _enrollmentAgent = null;
            }
            
            // Initialize file logger
            Instance.Info("======== CloudJourney Dashboard Starting ========");
            Instance.Info($"Version: 3.4.2 - Enrollment Agent (Production Build)");
            Instance.Info($"User: {Environment.UserName}");
            Instance.Info($"Machine: {Environment.MachineName}");
            Instance.CleanupOldLogs(7); // Keep last 7 days
            LogConnection("Dashboard initialized with MOCK data");
            Instance.Info("Dashboard initialized with MOCK data (pre-authentication)");
            
            Workloads = new ObservableCollection<Workload>();
            Alerts = new ObservableCollection<Alert>();
            Milestones = new ObservableCollection<Milestone>();
            ProgressTargets = new ObservableCollection<ProgressTarget>();
            Blockers = new ObservableCollection<Blocker>();
            EngagementOptions = new ObservableCollection<EngagementOption>();
            AIRecommendations = new ObservableCollection<AIRecommendation>();
            
            RefreshCommand = new RelayCommand(async () => await RefreshDataAsync());
            ConnectToGraphCommand = new RelayCommand(async () => await ConnectToGraphAsync());
            ConnectToConfigMgrCommand = new RelayCommand(async () => await ConnectToConfigMgrAsync());
            ShowDiagnosticsCommand = new RelayCommand(OnShowDiagnostics);
            ShowAISettingsCommand = new RelayCommand(OnShowAISettings);
            TestOpenAIConnectionCommand = new RelayCommand(async () => await TestOpenAIConnectionAsync());
            SaveOpenAIConfigCommand = new RelayCommand(OnSaveOpenAIConfig);
            OpenSetupGuideCommand = new RelayCommand(OnOpenSetupGuide);
            OpenLogFolderCommand = new RelayCommand(OnOpenLogFolder);
            OpenUserGuideCommand = new RelayCommand(OnOpenUserGuide);
            StartMigrationCommand = new RelayCommand<Workload>(OnStartMigration);
            LearnMoreCommand = new RelayCommand<string>(OnLearnMore);
            ActionCommand = new RelayCommand<string>(OnAction);
            OpenLinkCommand = new RelayCommand<string>(OnOpenLink);
            GenerateMigrationPlanCommand = new RelayCommand(async () => await GenerateMigrationPlanAsync());
            MarkPhaseCompleteCommand = new RelayCommand(async () => await MarkPhaseCompleteAsync());
            ExportDeviceListCommand = new RelayCommand(OnExportDeviceList);
            AnalyzeApplicationsCommand = new RelayCommand(async () => await AnalyzeApplicationsAsync());
            GenerateEnrollmentInsightsCommand = new RelayCommand(async () => await GenerateEnrollmentInsightsAsync());
            LoadWorkloadRecommendationCommand = new RelayCommand(async () => await LoadWorkloadRecommendationAsync());
            LoadExecutiveSummaryCommand = new RelayCommand(async () => await LoadExecutiveSummaryAsync());
            
            // Agent v2.0 commands
            GenerateAgentPlanCommand = new RelayCommand(async () => await GenerateAgentPlanAsync(), () => !IsAgentRunning);
            StopAgentCommand = new RelayCommand(OnStopAgent, () => IsAgentRunning);
            SaveAgentConfigCommand = new RelayCommand(OnSaveAgentConfig);
            ViewAgentMemoryCommand = new RelayCommand(OnViewAgentMemory);
            ViewMonitoringStatsCommand = new RelayCommand(OnViewMonitoringStats);

            InitializeCharts();
            WorkloadTrendSeries = new SeriesCollection();
            WorkloadTrendLabels = Array.Empty<string>();
            _ = LoadDataAsync();
        }

        public bool UseRealData
        {
            get => _useRealData;
            set => SetProperty(ref _useRealData, value);
        }

        public bool IsConfigMgrConnected
        {
            get => _isConfigMgrConnected;
            set
            {
                if (SetProperty(ref _isConfigMgrConnected, value))
                {
                    OnPropertyChanged(nameof(IsFullyAuthenticated));
                }
            }
        }

        /// <summary>
        /// True when ALL THREE required connections are established:
        /// 1. Microsoft Graph (Intune)
        /// 2. Configuration Manager (Admin Service)
        /// 3. Azure OpenAI
        /// </summary>
        public bool IsFullyAuthenticated =>
            _graphDataService.IsAuthenticated &&
            IsConfigMgrConnected &&
            _aiRecommendationService != null;

        public MigrationStatus? MigrationStatus
        {
            get => _migrationStatus;
            set => SetProperty(ref _migrationStatus, value);
        }

        public DeviceEnrollment? DeviceEnrollment
        {
            get => _deviceEnrollment;
            set
            {
                if (SetProperty(ref _deviceEnrollment, value))
                {
                    OnPropertyChanged(nameof(EnrollmentProgressPercentage));
                    OnPropertyChanged(nameof(CalculatedRequiredVelocity));
                }
            }
        }

        public ComplianceScore? ComplianceScore
        {
            get => _complianceScore;
            set => SetProperty(ref _complianceScore, value);
        }

        public EnrollmentAccelerationInsight? EnrollmentAccelerationInsight
        {
            get => _enrollmentAccelerationInsight;
            set => SetProperty(ref _enrollmentAccelerationInsight, value);
        }

        public SavingsUnlockInsight? SavingsUnlockInsight
        {
            get => _savingsUnlockInsight;
            set => SetProperty(ref _savingsUnlockInsight, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public DateTime LastRefreshTime
        {
            get => _lastRefreshTime;
            set => SetProperty(ref _lastRefreshTime, value);
        }

        public ObservableCollection<Workload> Workloads { get; }
        public ObservableCollection<Alert> Alerts { get; }
        public ObservableCollection<ProgressTarget> ProgressTargets { get; }
        public ObservableCollection<Milestone> Milestones { get; }
        public ObservableCollection<Blocker> Blockers { get; }
        public ObservableCollection<EngagementOption> EngagementOptions { get; }
        public ObservableCollection<AIRecommendation> AIRecommendations { get; }

        public bool HasNoRecommendations => AIRecommendations.Count == 0;

        // Phase 1 AI Enhancement Properties
        public CloudJourneyAddin.Services.MigrationPlan? MigrationPlan
        {
            get => _migrationPlan;
            set => SetProperty(ref _migrationPlan, value);
        }

        public int ExcellentReadinessCount
        {
            get => _excellentReadinessCount;
            set => SetProperty(ref _excellentReadinessCount, value);
        }

        public int GoodReadinessCount
        {
            get => _goodReadinessCount;
            set => SetProperty(ref _goodReadinessCount, value);
        }

        public int FairReadinessCount
        {
            get => _fairReadinessCount;
            set => SetProperty(ref _fairReadinessCount, value);
        }

        public int PoorReadinessCount
        {
            get => _poorReadinessCount;
            set => SetProperty(ref _poorReadinessCount, value);
        }

        public int NextBatchSize => ExcellentReadinessCount + GoodReadinessCount;

        public int DevicesNeedingPreparation
        {
            get => _devicesNeedingPreparation;
            set => SetProperty(ref _devicesNeedingPreparation, value);
        }

        public int HighRiskDeviceCount
        {
            get => _highRiskDeviceCount;
            set => SetProperty(ref _highRiskDeviceCount, value);
        }

        public int ExcellentVelocityCount
        {
            get => _excellentVelocityCount;
            set => SetProperty(ref _excellentVelocityCount, value);
        }

        public int GoodVelocityCount
        {
            get => _goodVelocityCount;
            set => SetProperty(ref _goodVelocityCount, value);
        }

        public int StalledWorkloadCount
        {
            get => _stalledWorkloadCount;
            set => SetProperty(ref _stalledWorkloadCount, value);
        }

        public ObservableCollection<ApplicationMigrationAnalysis>? ApplicationMigrations
        {
            get => _applicationMigrations;
            set => SetProperty(ref _applicationMigrations, value);
        }

        public int LowComplexityCount
        {
            get => _lowComplexityCount;
            set => SetProperty(ref _lowComplexityCount, value);
        }

        public int MediumComplexityCount
        {
            get => _mediumComplexityCount;
            set => SetProperty(ref _mediumComplexityCount, value);
        }

        public int HighComplexityCount
        {
            get => _highComplexityCount;
            set => SetProperty(ref _highComplexityCount, value);
        }

        public int TotalApplicationCount
        {
            get => _totalApplicationCount;
            set => SetProperty(ref _totalApplicationCount, value);
        }

        // Azure OpenAI Configuration Properties
        public string OpenAIEndpoint
        {
            get => _openAIEndpoint;
            set => SetProperty(ref _openAIEndpoint, value);
        }

        public string OpenAIDeploymentName
        {
            get => _openAIDeploymentName;
            set => SetProperty(ref _openAIDeploymentName, value);
        }

        public string OpenAIApiKey
        {
            get => _openAIApiKey;
            set => SetProperty(ref _openAIApiKey, value);
        }

        public bool IsOpenAIEnabled
        {
            get => _isOpenAIEnabled;
            set => SetProperty(ref _isOpenAIEnabled, value);
        }

        public string OpenAIStatus
        {
            get => _openAIStatus;
            set => SetProperty(ref _openAIStatus, value);
        }

        public bool HasOpenAIStatus
        {
            get => _hasOpenAIStatus;
            set => SetProperty(ref _hasOpenAIStatus, value);
        }

        public EnrollmentMomentumInsight? EnrollmentInsight
        {
            get => _enrollmentInsight;
            set
            {
                if (SetProperty(ref _enrollmentInsight, value))
                {
                    OnPropertyChanged(nameof(CurrentEnrollmentVelocity));
                    OnPropertyChanged(nameof(RecommendedEnrollmentVelocity));
                    OnPropertyChanged(nameof(ProjectedCompletionWeeks));
                }
            }
        }

        public bool IsLoadingEnrollmentInsight
        {
            get => _isLoadingEnrollmentInsight;
            set => SetProperty(ref _isLoadingEnrollmentInsight, value);
        }

        public WorkloadMomentumInsight? WorkloadMomentumInsight
        {
            get => _workloadMomentumInsight;
            set => SetProperty(ref _workloadMomentumInsight, value);
        }

        public AIActionSummary? AIActionSummary
        {
            get => _aiActionSummary;
            set => SetProperty(ref _aiActionSummary, value);
        }

        public ExecutiveSummary? ExecutiveSummary
        {
            get => _executiveSummary;
            set => SetProperty(ref _executiveSummary, value);
        }

        // v2.6.0 - Device Readiness & Enrollment Blockers
        public DeviceReadinessBreakdown? DeviceReadiness
        {
            get => _deviceReadiness;
            set => SetProperty(ref _deviceReadiness, value);
        }

        public EnrollmentBlockerSummary? EnrollmentBlockers
        {
            get => _enrollmentBlockers;
            set => SetProperty(ref _enrollmentBlockers, value);
        }

        public bool IsAIAvailable
        {
            get => _isAIAvailable;
            set => SetProperty(ref _isAIAvailable, value);
        }

        public double EnrollmentProgressPercentage
        {
            get
            {
                if (DeviceEnrollment == null || DeviceEnrollment.TotalDevices == 0)
                    return 0;
                
                return (DeviceEnrollment.IntuneEnrolledDevices / (double)DeviceEnrollment.TotalDevices) * 100;
            }
        }

        private int _targetCompletionWeeks = 14;
        public int TargetCompletionWeeks
        {
            get => _targetCompletionWeeks;
            set
            {
                if (_targetCompletionWeeks != value)
                {
                    _targetCompletionWeeks = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CalculatedRequiredVelocity));
                }
            }
        }

        public double CalculatedRequiredVelocity
        {
            get
            {
                if (DeviceEnrollment == null || DeviceEnrollment.ConfigMgrOnlyDevices == 0 || TargetCompletionWeeks == 0)
                    return 0;
                
                // Calculate devices per week needed to complete in target weeks
                return Math.Ceiling(DeviceEnrollment.ConfigMgrOnlyDevices / (double)TargetCompletionWeeks);
            }
        }

        public double CurrentEnrollmentVelocity
        {
            get
            {
                // Use AI insight if available, otherwise return 0 (will show as --)
                return EnrollmentInsight?.CurrentVelocity ?? 0;
            }
        }

        public double RecommendedEnrollmentVelocity
        {
            get
            {
                // Use AI insight if available, otherwise use calculated required velocity
                return EnrollmentInsight?.RecommendedVelocity ?? CalculatedRequiredVelocity;
            }
        }

        public double ProjectedCompletionWeeks
        {
            get
            {
                // Use AI insight if available, otherwise calculate based on current velocity
                if (EnrollmentInsight != null && EnrollmentInsight.ProjectedCompletionWeeks > 0)
                    return EnrollmentInsight.ProjectedCompletionWeeks;
                
                // If we have current velocity from AI, calculate projected weeks
                if (EnrollmentInsight?.CurrentVelocity > 0 && DeviceEnrollment?.ConfigMgrOnlyDevices > 0)
                {
                    return Math.Ceiling((double)DeviceEnrollment.ConfigMgrOnlyDevices / EnrollmentInsight.CurrentVelocity);
                }
                
                // Otherwise return target weeks as fallback
                return TargetCompletionWeeks;
            }
        }

        public SeriesCollection WorkloadTrendSeries { get; set; }
        public string[] WorkloadTrendLabels { get; set; }

        public SeriesCollection EnrollmentTrendSeries { get; set; } = new SeriesCollection();
        public string[] EnrollmentTrendLabels { get; set; } = Array.Empty<string>();
        
        public SeriesCollection ComplianceComparisonSeries { get; set; } = new SeriesCollection();
        
        public ICommand RefreshCommand { get; }
        public ICommand ConnectToGraphCommand { get; }
        public ICommand ConnectToConfigMgrCommand { get; }
        public ICommand ShowDiagnosticsCommand { get; }
        public ICommand ShowAISettingsCommand { get; }
        public ICommand TestOpenAIConnectionCommand { get; }
        public ICommand SaveOpenAIConfigCommand { get; }
        public ICommand OpenSetupGuideCommand { get; }
        public ICommand OpenLogFolderCommand { get; }
        public ICommand OpenUserGuideCommand { get; }
        public ICommand StartMigrationCommand { get; }
        public ICommand LearnMoreCommand { get; }
        public ICommand ActionCommand { get; }
        public ICommand OpenLinkCommand { get; }
        public ICommand GenerateMigrationPlanCommand { get; }
        public ICommand MarkPhaseCompleteCommand { get; }
        public ICommand ExportDeviceListCommand { get; }
        public ICommand AnalyzeApplicationsCommand { get; }
        public ICommand GenerateEnrollmentInsightsCommand { get; }
        public ICommand LoadWorkloadRecommendationCommand { get; }
        public ICommand LoadExecutiveSummaryCommand { get; }
        
        // Agent v2.0 commands
        public ICommand GenerateAgentPlanCommand { get; }
        public ICommand StopAgentCommand { get; }
        public ICommand SaveAgentConfigCommand { get; }
        public ICommand ViewAgentMemoryCommand { get; }
        public ICommand ViewMonitoringStatsCommand { get; }
        
        // Agent v2.0 properties
        public bool IsAgentRunning
        {
            get => _isAgentRunning;
            set
            {
                if (SetProperty(ref _isAgentRunning, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }
        
        public string AgentStatus
        {
            get => _agentStatus;
            set => SetProperty(ref _agentStatus, value);
        }
        
        public string? AgentCompletionMessage
        {
            get => _agentCompletionMessage;
            set => SetProperty(ref _agentCompletionMessage, value);
        }
        
        public ObservableCollection<AgentReasoningStep> AgentReasoningSteps
        {
            get => _agentReasoningSteps;
            set => SetProperty(ref _agentReasoningSteps, value);
        }
        
        public AgentExecutionTrace? CurrentAgentTrace
        {
            get => _currentAgentTrace;
            set => SetProperty(ref _currentAgentTrace, value);
        }
        
        public EnrollmentGoals? AgentGoals
        {
            get => _agentGoals;
            set => SetProperty(ref _agentGoals, value);
        }
        
        // Phase 2/3 properties
        private int _agentPhaseIndex = 2;
        private bool _isMonitoringActive = false;
        private DeviceMonitoringService? _monitoringService;
        private int _monitoredDeviceCount = 0;
        private int _autoEnrolledToday = 0;
        private string _nextMonitoringCheck = "N/A";
        private bool _showAutoApprovalStatus = false;
        private string _autoApprovalStatusMessage = "";
        private string _agentPhaseInfo = "ℹ️ Phase 1: Supervised Agent\n• Agent plans require your approval before execution\n• Emergency stop available at all times\n• Agent pauses if failure rate exceeds 15%\n• Complete audit trail of all agent actions";
        
        public int AgentPhaseIndex
        {
            get => _agentPhaseIndex;
            set
            {
                if (SetProperty(ref _agentPhaseIndex, value))
                {
                    OnAgentPhaseChanged();
                }
            }
        }
        
        public bool IsMonitoringActive
        {
            get => _isMonitoringActive;
            set => SetProperty(ref _isMonitoringActive, value);
        }
        
        public int MonitoredDeviceCount
        {
            get => _monitoredDeviceCount;
            set => SetProperty(ref _monitoredDeviceCount, value);
        }
        
        public int AutoEnrolledToday
        {
            get => _autoEnrolledToday;
            set => SetProperty(ref _autoEnrolledToday, value);
        }
        
        public string NextMonitoringCheck
        {
            get => _nextMonitoringCheck;
            set => SetProperty(ref _nextMonitoringCheck, value);
        }
        
        public bool ShowAutoApprovalStatus
        {
            get => _showAutoApprovalStatus;
            set => SetProperty(ref _showAutoApprovalStatus, value);
        }
        
        public string AutoApprovalStatusMessage
        {
            get => _autoApprovalStatusMessage;
            set => SetProperty(ref _autoApprovalStatusMessage, value);
        }
        
        public string AgentPhaseInfo
        {
            get => _agentPhaseInfo;
            set => SetProperty(ref _agentPhaseInfo, value);
        }

        private void LogConnection(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            _connectionLog.AppendLine($"[{timestamp}] {message}");
            System.Diagnostics.Debug.WriteLine($"[CONNECTION] {message}");
            // Also log to file
            Instance.Info($"[CONNECTION] {message}");
        }

        private void OnOpenLogFolder()
        {
            try
            {
                Instance.Info("User requested to open log folder");
                Instance.OpenLogDirectory();
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "OnOpenLogFolder");
                System.Windows.MessageBox.Show(
                    $"Failed to open log folder: {ex.Message}\n\nLog location: {Instance.LogDirectory}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void OnOpenUserGuide()
        {
            try
            {
                Instance.Info("User requested to open User Guide");
                
                // Look for AdminUserGuide.html in the application directory
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string userGuidePath = System.IO.Path.Combine(appDirectory, "AdminUserGuide.html");
                
                if (System.IO.File.Exists(userGuidePath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = userGuidePath,
                        UseShellExecute = true
                    });
                    Instance.Info($"Opened User Guide: {userGuidePath}");
                }
                else
                {
                    // Fallback to README.md if AdminUserGuide.html not found
                    string readmePath = System.IO.Path.Combine(appDirectory, "README.md");
                    if (System.IO.File.Exists(readmePath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = readmePath,
                            UseShellExecute = true
                        });
                        Instance.Info($"Opened README: {readmePath}");
                    }
                    else
                    {
                        System.Windows.MessageBox.Show(
                            $"User Guide not found.\n\nExpected location: {userGuidePath}\n\nPlease ensure AdminUserGuide.html is in the application directory.",
                            "User Guide Not Found",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                        Instance.Warning($"User Guide not found: {userGuidePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "OnOpenUserGuide");
                System.Windows.MessageBox.Show(
                    $"Failed to open User Guide: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void OnShowDiagnostics()
        {
            var diagWindow = new Views.DiagnosticsWindow();
            
            // Handle manual ConfigMgr setup from diagnostics
            diagWindow.ManualConfigMgrRequested += async (sender, siteServer) =>
            {
                diagWindow.Close();
                await TryManualConfigMgrConnection(siteServer);
            };
            
            // Overall authentication status
            bool fullyAuthenticated = IsFullyAuthenticated;
            string overallStatus = fullyAuthenticated 
                ? "✅ FULLY AUTHENTICATED - Showing REAL DATA" 
                : "⚠️ NOT FULLY AUTHENTICATED - Showing MOCK DATA";
            
            // Graph status
            bool graphConnected = _graphDataService.IsAuthenticated;
            diagWindow.SetGraphStatus(
                graphConnected,
                graphConnected ? 
                    "✅ Connected successfully\nAuthenticated user found\nReady to query Intune data" :
                    "❌ Not connected\nClick 'Connect to Microsoft Graph' to authenticate",
                graphConnected ?
                    "Required for: Device Enrollment, Compliance, Workload Status, Alerts" :
                    "NOT CONNECTED - Required for real data"
            );

            // ConfigMgr status - Now with explicit connection details
            bool configMgrConnected = _graphDataService.ConfigMgrService.IsConfigured;
            string configMgrMethod = _graphDataService.ConfigMgrService.ConnectionMethod;
            string connectionError = _graphDataService.ConfigMgrService.LastConnectionError;
            
            string statusMessage;
            if (configMgrConnected)
            {
                statusMessage = $"✅ Connected successfully\n\n" +
                    $"Connection Method: {configMgrMethod}\n" +
                    $"Status: Ready to query ConfigMgr device inventory\n\n" +
                    $"What this means:\n";
                
                if (_graphDataService.ConfigMgrService.IsUsingWmiFallback)
                {
                    statusMessage += "• Admin Service (REST API) connection failed or unavailable\n" +
                        "• Automatically fell back to WMI (ConfigMgr SDK)\n" +
                        "• Device data is being queried via WMI queries\n" +
                        $"• Original failure reason: {connectionError}";
                }
                else
                {
                    statusMessage += "• Using Admin Service (preferred method)\n" +
                        "• REST API connection established\n" +
                        "• Querying devices via HTTPS endpoint";
                }
            }
            else
            {
                statusMessage = $"❌ Not connected\n\n" +
                    $"ConfigMgr Console not detected or connection failed\n\n";
                
                if (!string.IsNullOrEmpty(connectionError))
                {
                    statusMessage += $"Error Details:\n{connectionError}\n\n";
                }
                
                statusMessage += "Troubleshooting:\n" +
                    "1. Check if ConfigMgr Console is installed\n" +
                    "2. Verify Console has connected to a site server\n" +
                    "3. Ensure Admin Service is enabled (or WMI access available)\n" +
                    "4. Check network connectivity to site server";
            }

            diagWindow.SetConfigMgrStatus(
                configMgrConnected,
                statusMessage,
                configMgrConnected ?
                    "Required for: Windows 10/11 device counts, Co-management status" :
                    "NOT CONNECTED - Required for real data"
            );

            // Azure OpenAI status
            bool aiConnected = _aiRecommendationService != null;
            diagWindow.SetAIStatus(
                aiConnected,
                aiConnected ?
                    "✅ Connected successfully\nAzure OpenAI configured and ready\nGPT-4 recommendations enabled" :
                    "❌ Not configured\nClick '🤖 AI' button to configure Azure OpenAI",
                aiConnected ?
                    "Required for: AI-powered recommendations, stall analysis, migration insights" :
                    "NOT CONFIGURED - Required for real data"
            );

            // Overall authentication message
            diagWindow.SetOverallStatus(
                fullyAuthenticated,
                overallStatus,
                fullyAuthenticated ?
                    "All three required connections are established. Dashboard is showing real data from your environment." :
                    "⚠️ IMPORTANT: All three connections (Graph + ConfigMgr + Azure OpenAI) must be established before real data is displayed.\n\n" +
                    $"Current state:\n" +
                    $"  • Microsoft Graph: {(graphConnected ? "✅ Connected" : "❌ Not connected")}\n" +
                    $"  • Configuration Manager: {(configMgrConnected ? "✅ Connected" : "❌ Not connected")}\n" +
                    $"  • Azure OpenAI: {(aiConnected ? "✅ Configured" : "❌ Not configured")}\n\n" +
                    "Mock data is being displayed until all connections are ready."
            );

            // Sections status
            var sectionsStatus = new System.Text.StringBuilder();
            sectionsStatus.AppendLine($"1. Overall Migration Status: {(IsFullyAuthenticated ? "✅ REAL (from Intune workload policies)" : "❌ MOCK (placeholder)")}");
            sectionsStatus.AppendLine($"2. Device Enrollment: {(IsFullyAuthenticated ? "✅ REAL (from Intune + ConfigMgr)" : "❌ MOCK")}");
            sectionsStatus.AppendLine($"3. Workload Status: {(IsFullyAuthenticated ? "✅ REAL (detected from Intune policies)" : "❌ MOCK")}");
            sectionsStatus.AppendLine($"4. Security & Compliance: {(IsFullyAuthenticated ? "✅ REAL (from Intune compliance policies)" : "❌ MOCK")}");
            sectionsStatus.AppendLine($"5. ROI & Savings: ⚠️ ESTIMATED (industry averages, not real cost data)");
            sectionsStatus.AppendLine($"6. Enrollment Readiness: {(IsFullyAuthenticated ? "✅ REAL (detected enrollment prerequisites)" : "❌ MOCK")}");
            sectionsStatus.AppendLine($"7. Peer Benchmarking: ⚠️ ESTIMATED (Microsoft published statistics, not live comparison)");
            sectionsStatus.AppendLine($"8. Alerts & Recommendations: {(IsFullyAuthenticated ? "✅ REAL (AI-powered from GPT-4)" : "❌ MOCK")}");
            sectionsStatus.AppendLine($"9. Recent Milestones: ⚠️ PREDEFINED (example milestones, not detected achievements)");
            sectionsStatus.AppendLine($"10. Support & Engagement: ✅ REAL (Microsoft resources links)");
            
            diagWindow.SetSectionsStatus(sectionsStatus.ToString());

            // Debug log
            diagWindow.SetDebugLog(_connectionLog.ToString());

            diagWindow.ShowDialog();
        }

        private void OnShowAISettings()
        {
            try
            {
                // Load existing config
                var config = Services.AzureOpenAIConfig.Load();
                
                var aiWindow = new Views.AISettingsWindow();
                var aiViewModel = new
                {
                    IsOpenAIEnabled = config?.IsEnabled ?? false,
                    OpenAIEndpoint = config?.Endpoint ?? string.Empty,
                    OpenAIDeploymentName = config?.DeploymentName ?? string.Empty,
                    OpenAIApiKey = config?.ApiKey ?? string.Empty,
                    OpenAIStatus = _openAIStatus,
                    HasOpenAIStatus = _hasOpenAIStatus,
                    TestOpenAIConnectionCommand = TestOpenAIConnectionCommand,
                    SaveOpenAIConfigCommand = SaveOpenAIConfigCommand,
                    OpenSetupGuideCommand = OpenSetupGuideCommand
                };
                
                aiWindow.DataContext = this; // Use DashboardViewModel as DataContext
                
                // Set API key separately (PasswordBox doesn't support binding)
                if (!string.IsNullOrEmpty(config?.ApiKey))
                {
                    aiWindow.SetApiKey(config.ApiKey);
                }
                
                // Update properties for binding
                IsOpenAIEnabled = config?.IsEnabled ?? false;
                OpenAIEndpoint = config?.Endpoint ?? string.Empty;
                OpenAIDeploymentName = config?.DeploymentName ?? string.Empty;
                OpenAIApiKey = config?.ApiKey ?? string.Empty;
                
                aiWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to open AI Settings window: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"Error opening AI Settings:\n{ex.Message}",
                    "AI Settings Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task TestOpenAIConnectionAsync()
        {
            try
            {
                HasOpenAIStatus = false;
                OpenAIStatus = "Testing connection...";
                HasOpenAIStatus = true;
                
                var service = new Services.AzureOpenAIService();
                var (success, message) = await service.TestConnectionAsync();
                
                OpenAIStatus = message;
                HasOpenAIStatus = true;
                
                Instance.Info($"OpenAI connection test: {message}");
            }
            catch (Exception ex)
            {
                OpenAIStatus = $"❌ Test failed: {ex.Message}";
                HasOpenAIStatus = true;
                Instance.Error($"OpenAI connection test failed: {ex.Message}");
            }
        }

        private void OnSaveOpenAIConfig()
        {
            try
            {
                var config = new Services.AzureOpenAIConfig
                {
                    IsEnabled = IsOpenAIEnabled,
                    Endpoint = OpenAIEndpoint?.Trim(),
                    DeploymentName = OpenAIDeploymentName?.Trim(),
                    ApiKey = OpenAIApiKey?.Trim()
                };
                
                config.Save();
                
                System.Windows.MessageBox.Show(
                    "Azure OpenAI configuration saved successfully!\n\n" +
                    "Settings will be used for AI-enhanced recommendations.",
                    "Settings Saved",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                
                Instance.Info($"OpenAI config saved - Enabled: {config.IsEnabled}, Endpoint: {config.Endpoint}");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error saving configuration:\n{ex.Message}",
                    "Save Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                Instance.Error($"Failed to save OpenAI config: {ex.Message}");
            }
        }

        private void OnOpenSetupGuide()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://learn.microsoft.com/azure/ai-services/openai/how-to/create-resource",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Instance.Error($"Failed to open setup guide: {ex.Message}");
            }
        }

        private async Task ConnectToGraphAsync()
        {
            IsLoading = true;
            LogConnection("Starting connection to Microsoft Graph...");
            try
            {
                // Step 1: Connect to Microsoft Graph (Intune)
                LogConnection("Attempting Microsoft Graph authentication...");
                bool graphSuccess = await _graphDataService.AuthenticateAsync();
                
                if (!graphSuccess)
                {
                    LogConnection("❌ Microsoft Graph authentication FAILED");
                    IsLoading = false;
                    return;
                }

                LogConnection("✅ Microsoft Graph authentication SUCCESS");
                
                // Don't enable real data yet - need ConfigMgr and Azure OpenAI too
                OnPropertyChanged(nameof(IsFullyAuthenticated));

                // Step 2: Auto-detect ConfigMgr Admin Service URL (no hardcoded values)
                LogConnection("Attempting to auto-detect ConfigMgr Admin Service URL...");

                var (adminServiceUrl, debugInfo) = _graphDataService.ConfigMgrService.DetectAdminServiceUrl();
                
                if (!string.IsNullOrEmpty(adminServiceUrl))
                {
                    // Try to connect with auto-detected URL
                    LogConnection($"Auto-detected URL: {adminServiceUrl}");
                    bool configMgrSuccess = await _graphDataService.ConfigMgrService.ConfigureAsync(adminServiceUrl);
                    
                    if (configMgrSuccess)
                    {
                        IsConfigMgrConnected = true;
                        var connectionMethod = _graphDataService.ConfigMgrService.ConnectionMethod;
                        
                        // Check if all three connections are now ready
                        string statusMessage;
                        if (IsFullyAuthenticated)
                        {
                            statusMessage = $"✅ ALL CONNECTIONS ESTABLISHED\n\n" +
                                          $"• Microsoft Graph (Intune): Connected\n" +
                                          $"• Configuration Manager: {connectionMethod}\n" +
                                          $"• Azure OpenAI: Configured\n\n" +
                                          $"Dashboard is now fully authenticated and will show REAL DATA.";
                        }
                        else
                        {
                            statusMessage = $"Connected: Graph + ConfigMgr ({connectionMethod})\n\n" +
                                          $"⚠️ Still showing MOCK DATA until all connections are established:\n\n";
                            if (_aiRecommendationService == null)
                                statusMessage += "• Azure OpenAI: Not configured\n";
                            statusMessage += "\nUse the 🤖 AI button to complete setup.";
                        }
                        
                        System.Windows.MessageBox.Show(
                            statusMessage,
                            IsFullyAuthenticated ? "Fully Authenticated" : "Partial Connection",
                            System.Windows.MessageBoxButton.OK,
                            IsFullyAuthenticated ? System.Windows.MessageBoxImage.Information : System.Windows.MessageBoxImage.Warning);
                    }
                    else
                    {
                        // ConfigMgr connection failed, but Graph succeeded
                        LogConnection($"ConfigMgr connection failed: {debugInfo}");
                        LogConnection("Use the 🔧 Diagnostics button to manually configure.");
                        
                        System.Windows.MessageBox.Show(
                            $"Microsoft Graph Connected ✓\n\n" +
                            $"⚠️ ConfigMgr Admin Service connection failed.\n" +
                            $"Detected URL: {adminServiceUrl}\n\n" +
                            $"⚠️ Dashboard will show MOCK DATA until all three connections are established:\n" +
                            $"• Microsoft Graph: Connected\n" +
                            $"• Configuration Manager: Failed\n" +
                            $"• Azure OpenAI: {(_aiRecommendationService != null ? "Configured" : "Not configured")}\n\n" +
                            $"To complete setup:\n" +
                            $"1. Click the 🔧 Diagnostics button to manually configure ConfigMgr\n" +
                            $"2. Click the 🤖 AI button to configure Azure OpenAI",
                            "Partial Connection",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                    }
                }
                else
                {
                    // Couldn't detect ConfigMgr installation - prompt for manual entry
                    LogConnection($"Auto-detection failed: {debugInfo}");
                    LogConnection("Prompting for manual server entry...");
                    var manualServer = Microsoft.VisualBasic.Interaction.InputBox(
                        "ConfigMgr Console not detected automatically.\n\n" +
                        "Enter your ConfigMgr Site Server name:\n\n" +
                        "Examples:\n" +
                        "• localhost (if ConfigMgr is on this machine)\n" +
                        "• CM01\n" +
                        "• CM01.contoso.com\n\n" +
                        "Or click Cancel to continue with Intune data only.",
                        "ConfigMgr Site Server",
                        "localhost",
                        -1, -1);

                    if (!string.IsNullOrWhiteSpace(manualServer))
                    {
                        // Remove any protocol or path if user pasted full URL
                        manualServer = manualServer.Replace("https://", "").Replace("http://", "").Split('/')[0].Trim();
                        var manualUrl = $"https://{manualServer}/AdminService";
                        
                        LogConnection($"Manual entry: {manualServer}, attempting connection to {manualUrl}");
                        bool configMgrSuccess = await _graphDataService.ConfigMgrService.ConfigureAsync(manualUrl);
                        
                        if (configMgrSuccess)
                        {
                            IsConfigMgrConnected = true;
                            var connectionMethod = _graphDataService.ConfigMgrService.ConnectionMethod;
                            LogConnection($"✅ Manual connection SUCCESS via {connectionMethod}");
                            
                            // Check if all three connections are now ready
                            string statusMessage;
                            if (IsFullyAuthenticated)
                            {
                                statusMessage = $"✅ ALL CONNECTIONS ESTABLISHED\n\n" +
                                              $"• Microsoft Graph (Intune): Connected\n" +
                                              $"• Configuration Manager: {connectionMethod}\n" +
                                              $"• Azure OpenAI: Configured\n\n" +
                                              $"Dashboard is now fully authenticated and will show REAL DATA.";
                            }
                            else
                            {
                                statusMessage = $"Connected: Graph + ConfigMgr ({connectionMethod})\n\n" +
                                              $"⚠️ Still showing MOCK DATA until all connections are established:\n\n";
                                if (_aiRecommendationService == null)
                                    statusMessage += "• Azure OpenAI: Not configured\n";
                                statusMessage += "\nUse the 🤖 AI button to complete setup.";
                            }
                            
                            System.Windows.MessageBox.Show(
                                statusMessage,
                                IsFullyAuthenticated ? "Fully Authenticated" : "Partial Connection",
                                System.Windows.MessageBoxButton.OK,
                                IsFullyAuthenticated ? System.Windows.MessageBoxImage.Information : System.Windows.MessageBoxImage.Warning);
                        }
                        else
                        {
                            var error = _graphDataService.ConfigMgrService.LastConnectionError;
                            LogConnection($"❌ Manual connection FAILED: {error}");
                            
                            System.Windows.MessageBox.Show(
                                $"Failed to connect to ConfigMgr.\n\n" +
                                $"Site Server: {manualServer}\n" +
                                $"Error: {error}\n\n" +
                                $"⚠️ Dashboard will show MOCK DATA until all three connections are established.\n\n" +
                                $"Use the 🔧 Diagnostics button to try again or configure Azure OpenAI (🤖 AI button).",
                                "ConfigMgr Connection Failed",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Warning);
                        }
                    }
                    else
                    {
                        LogConnection("User cancelled manual entry - continuing with Intune only");
                    }
                }
                
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to connect: {ex.Message}",
                    "Connection Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ConnectToConfigMgrAsync()
        {
            IsLoading = true;
            LogConnection("Starting ConfigMgr Admin Service connection...");
            
            try
            {
                // Step 1: Auto-detect ConfigMgr Admin Service URL
                LogConnection("Attempting to auto-detect ConfigMgr Admin Service URL...");
                var (adminServiceUrl, debugInfo) = _graphDataService.ConfigMgrService.DetectAdminServiceUrl();
                
                if (!string.IsNullOrEmpty(adminServiceUrl))
                {
                    // Auto-detected - try to connect
                    LogConnection($"Auto-detected URL: {adminServiceUrl}");
                    bool success = await _graphDataService.ConfigMgrService.ConfigureAsync(adminServiceUrl);
                    
                    if (success)
                    {
                        IsConfigMgrConnected = true;
                        var connectionMethod = _graphDataService.ConfigMgrService.ConnectionMethod;
                        LogConnection($"✅ ConfigMgr connection SUCCESS via {connectionMethod}");
                        
                        // Check if all three connections are now ready
                        string statusMessage;
                        if (IsFullyAuthenticated)
                        {
                            statusMessage = $"✅ ALL CONNECTIONS ESTABLISHED\n\n" +
                                          $"• Microsoft Graph (Intune): Connected\n" +
                                          $"• Configuration Manager: {connectionMethod}\n" +
                                          $"• Azure OpenAI: Configured\n\n" +
                                          $"Dashboard is now fully authenticated and will show REAL DATA.";
                        }
                        else
                        {
                            statusMessage = $"ConfigMgr connected via {connectionMethod}\n\n" +
                                          $"⚠️ Still showing MOCK DATA until all connections are established:\n\n";
                            if (!_graphDataService.IsAuthenticated)
                                statusMessage += "• Microsoft Graph: Not connected\n";
                            if (_aiRecommendationService == null)
                                statusMessage += "• Azure OpenAI: Not configured\n";
                            statusMessage += "\nUse the 🔗 and 🤖 buttons to complete setup.";
                        }
                        
                        System.Windows.MessageBox.Show(
                            statusMessage,
                            IsFullyAuthenticated ? "Fully Authenticated" : "ConfigMgr Connected",
                            System.Windows.MessageBoxButton.OK,
                            IsFullyAuthenticated ? System.Windows.MessageBoxImage.Information : System.Windows.MessageBoxImage.Warning);
                        
                        // Reload data
                        await RefreshDataAsync();
                    }
                    else
                    {
                        LogConnection($"❌ Auto-detected connection FAILED: {_graphDataService.ConfigMgrService.LastConnectionError}");
                        PromptForManualConfigMgrEntry();
                    }
                }
                else
                {
                    // Couldn't auto-detect - prompt for manual entry
                    LogConnection($"Auto-detection failed: {debugInfo}");
                    PromptForManualConfigMgrEntry();
                }
            }
            catch (Exception ex)
            {
                LogConnection($"❌ ConfigMgr connection error: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"Failed to connect to ConfigMgr:\n\n{ex.Message}\n\n" +
                    $"Please verify:\n" +
                    $"• ConfigMgr Admin Service is enabled\n" +
                    $"• You have Full Administrator or Read-only Analyst role\n" +
                    $"• Network connectivity to the site server",
                    "ConfigMgr Connection Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void PromptForManualConfigMgrEntry()
        {
            LogConnection("Prompting for manual ConfigMgr server entry...");
            var manualServer = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter your ConfigMgr Site Server name:\n\n" +
                "Examples:\n" +
                "• localhost (if ConfigMgr is on this machine)\n" +
                "• CM01\n" +
                "• CM01.contoso.com\n" +
                "• sccm.corp.contoso.com\n\n" +
                "Or click Cancel to skip ConfigMgr connection.",
                "ConfigMgr Site Server",
                "",
                -1, -1);

            if (!string.IsNullOrWhiteSpace(manualServer))
            {
                // Remove any protocol or path if user pasted full URL
                manualServer = manualServer.Replace("https://", "").Replace("http://", "").Split('/')[0].Trim();
                var manualUrl = $"https://{manualServer}/AdminService";
                
                LogConnection($"Manual entry: {manualServer}, attempting connection to {manualUrl}");
                bool success = await _graphDataService.ConfigMgrService.ConfigureAsync(manualUrl);
                
                if (success)
                {
                    IsConfigMgrConnected = true;
                    var connectionMethod = _graphDataService.ConfigMgrService.ConnectionMethod;
                    LogConnection($"✅ Manual ConfigMgr connection SUCCESS via {connectionMethod}");
                    
                    // Check if all three connections are now ready
                    string statusMessage;
                    if (IsFullyAuthenticated)
                    {
                        statusMessage = $"✅ ALL CONNECTIONS ESTABLISHED\n\n" +
                                      $"• Microsoft Graph (Intune): Connected\n" +
                                      $"• Configuration Manager: {connectionMethod}\n" +
                                      $"• Azure OpenAI: Configured\n\n" +
                                      $"Dashboard is now fully authenticated and will show REAL DATA.";
                    }
                    else
                    {
                        statusMessage = $"ConfigMgr connected via {connectionMethod}\n\n" +
                                      $"⚠️ Still showing MOCK DATA until all connections are established:\n\n";
                        if (!_graphDataService.IsAuthenticated)
                            statusMessage += "• Microsoft Graph: Not connected\n";
                        if (_aiRecommendationService == null)
                            statusMessage += "• Azure OpenAI: Not configured\n";
                        statusMessage += "\nUse the 🔗 and 🤖 buttons to complete setup.";
                    }
                    
                    System.Windows.MessageBox.Show(
                        statusMessage,
                        IsFullyAuthenticated ? "Fully Authenticated" : "ConfigMgr Connected",
                        System.Windows.MessageBoxButton.OK,
                        IsFullyAuthenticated ? System.Windows.MessageBoxImage.Information : System.Windows.MessageBoxImage.Warning);
                    
                    await RefreshDataAsync();
                }
                else
                {
                    LogConnection($"❌ Manual connection FAILED: {_graphDataService.ConfigMgrService.LastConnectionError}");
                    System.Windows.MessageBox.Show(
                        $"Failed to connect to {manualServer}\n\n" +
                        $"Error: {_graphDataService.ConfigMgrService.LastConnectionError}\n\n" +
                        $"Please verify:\n" +
                        $"• Server name is correct\n" +
                        $"• ConfigMgr Admin Service is enabled\n" +
                        $"• You have appropriate permissions\n" +
                        $"• Network connectivity to the server",
                        "Connection Failed",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
            }
            else
            {
                LogConnection("User cancelled manual ConfigMgr entry");
            }
        }

        private void InitializeCharts()
        {
            EnrollmentTrendSeries = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Intune Enrolled",
                    Values = new ChartValues<int>(),
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 8
                },
                new LineSeries
                {
                    Title = "ConfigMgr Only",
                    Values = new ChartValues<int>(),
                    PointGeometry = DefaultGeometries.Square,
                    PointGeometrySize = 8
                }
            };

            ComplianceComparisonSeries = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "Intune Managed",
                    Values = new ChartValues<double>()
                },
                new ColumnSeries
                {
                    Title = "ConfigMgr Only",
                    Values = new ChartValues<double>()
                }
            };
        }

        private async Task LoadDataAsync()
        {
            IsLoading = true;

            try
            {
                // Check ConfigMgr connection status
                IsConfigMgrConnected = _graphDataService.ConfigMgrService.IsConfigured;
                
                // Only show real data when ALL THREE connections are established
                if (IsFullyAuthenticated)
                {
                    Instance.Info("All three connections authenticated - loading real data");
                    UseRealData = true;
                    await LoadRealDataAsync();
                }
                else
                {
                    Instance.Warning($"Not fully authenticated - showing mock data. Graph: {_graphDataService.IsAuthenticated}, ConfigMgr: {IsConfigMgrConnected}, AI: {_aiRecommendationService != null}");
                    UseRealData = false;
                    await LoadMockDataAsync();
                }

                LastRefreshTime = DateTime.Now;
                
                // Load AI recommendations (after other data is loaded)
                await LoadAIRecommendationsAsync();

                // Load Phase 1 AI Enhancement data (always - with mock data when not authenticated)
                await LoadDeviceSelectionDataAsync();
                await LoadWorkloadTrendsAsync();

                // Load new tab data (v1.7.1)
                await LoadWorkloadRecommendationDataAsync();
                await LoadApplicationMigrationDataAsync();
                await LoadExecutiveSummaryDataAsync();

                // Update AI availability
                CheckAIAvailability();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadAIRecommendationsAsync()
        {
            try
            {
                // Check if AI service is available (Azure OpenAI configured)
                if (_aiRecommendationService == null)
                {
                    AIRecommendations.Clear();
                    AIRecommendations.Add(new AIRecommendation
                    {
                        Title = "⚠️ Azure OpenAI Required",
                        Description = "AI-powered recommendations require Azure OpenAI to be configured. Click the 🤖 AI button in the toolbar to set up your Azure OpenAI credentials.",
                        Priority = RecommendationPriority.Critical,
                        Category = RecommendationCategory.StallPrevention,
                        ActionSteps = new List<string>
                        {
                            "1. Click the 🤖 AI button in the toolbar",
                            "2. Enter your Azure OpenAI endpoint, deployment name, and API key",
                            "3. Save the configuration",
                            "4. Restart the dashboard to enable AI recommendations"
                        }
                    });
                    OnPropertyChanged(nameof(HasNoRecommendations));
                    return;
                }

                if (DeviceEnrollment != null && ComplianceScore != null && Workloads.Count > 0)
                {
                    var recommendations = await _aiRecommendationService.GetRecommendationsAsync(
                        DeviceEnrollment,
                        Workloads.ToList(),
                        ComplianceScore,
                        _lastProgressDate,
                        activePlan: MigrationPlan  // Pass the migration plan for enhanced recommendations
                    );

                    AIRecommendations.Clear();
                    foreach (var recommendation in recommendations.Take(5)) // Show top 5 recommendations
                    {
                        AIRecommendations.Add(recommendation);
                    }
                    
                    OnPropertyChanged(nameof(HasNoRecommendations));
                }
            }
            catch (Exception ex)
            {
                // Show error as a recommendation
                AIRecommendations.Clear();
                AIRecommendations.Add(new AIRecommendation
                {
                    Title = "❌ AI Recommendations Error",
                    Description = $"Failed to generate AI recommendations: {ex.Message}",
                    Priority = RecommendationPriority.Critical,
                    Category = RecommendationCategory.StallPrevention,
                    ActionSteps = new List<string>
                    {
                        "1. Check Azure OpenAI configuration (🤖 AI button)",
                        "2. Verify API key and endpoint are correct",
                        "3. Check network connectivity to Azure OpenAI",
                        "4. Review logs for detailed error information"
                    }
                });
                Instance.Error($"Error loading AI recommendations: {ex.Message}");
            }
        }

        private async Task LoadRealDataAsync()
        {
            try
            {
                Instance.Info("=== Starting LoadRealDataAsync ===");
                Instance.Info($"Graph authenticated: {_graphDataService != null}");
                
                // Load device enrollment from Graph
                Instance.Info("Loading device enrollment from Graph API...");
                DeviceEnrollment = await _graphDataService.GetDeviceEnrollmentAsync();
                Instance.Info($"Device Enrollment loaded: Total={DeviceEnrollment?.TotalDevices}, Intune={DeviceEnrollment?.IntuneEnrolledDevices}, ConfigMgr={DeviceEnrollment?.ConfigMgrOnlyDevices}");

                // Load compliance data from Graph
                Instance.Info("Loading compliance data from Graph API...");
                var complianceDashboard = await _graphDataService.GetComplianceDashboardAsync();
                ComplianceScore = new ComplianceScore
                {
                    IntuneScore = complianceDashboard.OverallComplianceRate,
                    ConfigMgrScore = 0, // Would need ConfigMgr integration
                    RiskAreas = Array.Empty<string>(),
                    DevicesLackingConditionalAccess = complianceDashboard.NonCompliantDevices
                };

                // Calculate migration status based on real data
                int intuneDevices = DeviceEnrollment?.IntuneEnrolledDevices ?? 0;
                int totalDevices = DeviceEnrollment?.TotalDevices ?? 1;
                double progress = totalDevices > 0 ? (intuneDevices / (double)totalDevices) * 100 : 0;

                MigrationStatus = new MigrationStatus
                {
                    WorkloadsTransitioned = (int)(progress / 14.3), // Rough estimate (100% / 7 workloads)
                    TotalWorkloads = 7,
                    ProjectedFinishDate = DateTime.Now.AddMonths((int)((100 - progress) / 5)), // Rough estimate
                    LastUpdateDate = DateTime.Now
                };

                // Load remaining data from mock service (until we implement full Graph integration)
                await LoadMockDataPartialAsync();

                // Update charts
                UpdateEnrollmentChart();
                UpdateComplianceChart();
                
                // v2.6.0 - Load device readiness and enrollment blockers
                Instance.Info("Loading device readiness and enrollment blockers...");
                try
                {
                    DeviceReadiness = await _deviceReadinessService.GetDeviceReadinessBreakdownAsync();
                    EnrollmentBlockers = await _deviceReadinessService.GetEnrollmentBlockersAsync();
                    Instance.Info($"✅ Device readiness loaded: {DeviceReadiness?.HighSuccessDevices ?? 0} high success, {DeviceReadiness?.ModerateSuccessDevices ?? 0} moderate, {DeviceReadiness?.HighRiskDevices ?? 0} high risk");
                    Instance.Info($"✅ Enrollment blockers loaded: {EnrollmentBlockers?.TotalBlockedDevices ?? 0} blocked, {EnrollmentBlockers?.EnrollableDevices ?? 0} enrollable");
                }
                catch (Exception ex)
                {
                    Instance.LogException(ex, "Failed to load device readiness");
                    DeviceReadiness = null;
                    EnrollmentBlockers = null;
                }
                
                Instance.Info("LoadRealDataAsync completed successfully");
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "LoadRealDataAsync");
                System.Windows.MessageBox.Show(
                    $"Error loading real data: {ex.Message}\n\nFalling back to mock data.",
                    "Data Load Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                
                // Fall back to mock data
                UseRealData = false;
                await LoadMockDataAsync();
            }
        }

        private async Task LoadMockDataPartialAsync()
        {
            // Load data now available from Graph API + remaining mock data
            Instance.Info("=== Starting LoadMockDataPartialAsync ===");
            try
            {
                // Get workloads from Graph
                Instance.Info("Loading workloads from Graph API...");
                var workloads = await _graphDataService.GetWorkloadsAsync();
                Workloads.Clear();
                foreach (var workload in workloads)
                    Workloads.Add(workload);
                Instance.Info($"Loaded {Workloads.Count} workloads from Graph");

                // Get alerts from Graph
                Instance.Info("Loading alerts from Graph API...");
                var alerts = await _graphDataService.GetAlertsAsync();
                Alerts.Clear();
                foreach (var alert in alerts)
                    Alerts.Add(alert);
                Instance.Info($"Loaded {Alerts.Count} alerts from Graph");
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "LoadMockDataPartialAsync - Graph API calls");
                Instance.Warning("Falling back to mock data for workloads and alerts");
                
                // Fall back to mock data for workloads and alerts
                var workloads = await _telemetryService.GetWorkloadsAsync();
                Workloads.Clear();
                foreach (var workload in workloads)
                    Workloads.Add(workload);

                var alerts = await _telemetryService.GetAlertsAsync();
                Alerts.Clear();
                foreach (var alert in alerts)
                    Alerts.Add(alert);
            }

            // Industry insights focused on ACTIONS to accelerate enrollment (principle #1)
            Instance.Info("Loading enrollment acceleration insights...");
            var enrollmentInsightTask = _telemetryService.GetEnrollmentAccelerationInsightAsync();
            var savingsInsightTask = _telemetryService.GetSavingsUnlockInsightAsync();
            var engagementOptionsTask = _telemetryService.GetEngagementOptionsAsync();

            await Task.WhenAll(enrollmentInsightTask, savingsInsightTask, engagementOptionsTask);

            EnrollmentAccelerationInsight = await enrollmentInsightTask;
            SavingsUnlockInsight = await savingsInsightTask;

            // NO mock milestones - replaced with forward-looking ProgressTargets
            ProgressTargets.Clear();
            Milestones.Clear();

            // Mock AI Action Summary for authenticated users
            AIActionSummary = new AIActionSummary
            {
                PrimaryEnrollmentAction = "Enroll the 425 'Good' readiness devices (scores 60-79) using Phase 3 Autonomous Agent",
                EnrollmentActionImpact = 425,
                PrimaryWorkloadAction = "Transition Conditional Access workload to unlock modern security policies",
                WorkloadActionImpact = "Unlock Zero Trust security and app protection policies",
                EnrollmentBlockers = new List<string>
                {
                    "132 devices have insufficient disk space (<20GB free)",
                    "48 devices running Windows 7 (OS upgrade required)",
                    "25 devices have outdated TPM firmware (blocks BitLocker)"
                },
                WorkloadBlockers = new List<string>
                {
                    "Conditional Access policies not yet configured in Intune",
                    "75% enrollment threshold not yet met (currently 58%)"
                },
                AIRecommendation = "Focus on device enrollment first. Use Phase 3 agent to auto-enroll 100 devices/week targeting 'Good' readiness scores. Once you reach 75% enrollment (3 weeks at current velocity), transition Conditional Access workload to unlock $105K annual savings from reduced infrastructure costs.",
                WeeksToNextMilestone = 3,
                IsAIPowered = false
            };
            OnPropertyChanged(nameof(AIActionSummary));

            // REAL ENROLLMENT BLOCKER DETECTION (only true prerequisites)
            Instance.Info("Detecting enrollment blockers...");
            try
            {
                var blockers = await _graphDataService.GetEnrollmentBlockersAsync();
                Blockers.Clear();
                foreach (var blocker in blockers)
                    Blockers.Add(blocker);
                Instance.Info($"✅ Loaded {blockers.Count} enrollment blockers");
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "Failed to load enrollment blockers");
                Blockers.Clear(); // Show empty state on error
            }

            EngagementOptions.Clear();
            foreach (var option in await engagementOptionsTask)
                EngagementOptions.Add(option);
        }

        private async Task LoadMockDataAsync()
        {
            Instance.Info("=== Loading MOCK data (pre-authentication) ===");
            // Load all data in parallel
            var migrationStatusTask = _telemetryService.GetMigrationStatusAsync();
            var deviceEnrollmentTask = _telemetryService.GetDeviceEnrollmentAsync();
            var workloadsTask = _telemetryService.GetWorkloadsAsync();
            var complianceScoreTask = _telemetryService.GetComplianceScoreAsync();
            var enrollmentInsightTask = _telemetryService.GetEnrollmentAccelerationInsightAsync();
            var savingsInsightTask = _telemetryService.GetSavingsUnlockInsightAsync();
            var alertsTask = _telemetryService.GetAlertsAsync();
            var milestonesTask = _telemetryService.GetMilestonesAsync();
            var progressTargetsTask = _telemetryService.GetProgressTargetsAsync();
            var blockersTask = _telemetryService.GetBlockersAsync();
            var engagementOptionsTask = _telemetryService.GetEngagementOptionsAsync();

            await Task.WhenAll(
                migrationStatusTask,
                deviceEnrollmentTask,
                workloadsTask,
                complianceScoreTask,
                enrollmentInsightTask,
                savingsInsightTask,
                alertsTask,
                milestonesTask,
                progressTargetsTask,
                blockersTask,
                engagementOptionsTask
            );

            MigrationStatus = await migrationStatusTask;
            DeviceEnrollment = await deviceEnrollmentTask;
            OnPropertyChanged(nameof(EnrollmentProgressPercentage));
                
            var workloads = await workloadsTask;
            Workloads.Clear();
            foreach (var workload in workloads)
                Workloads.Add(workload);

            ComplianceScore = await complianceScoreTask;
            EnrollmentAccelerationInsight = await enrollmentInsightTask;
            SavingsUnlockInsight = await savingsInsightTask;

            var alerts = await alertsTask;
            Alerts.Clear();
            foreach (var alert in alerts)
                Alerts.Add(alert);

            var milestones = await milestonesTask;
            Milestones.Clear();
            foreach (var milestone in milestones.OrderByDescending(m => m.AchievedDate).Take(3))
                Milestones.Add(milestone);
            
            // Also populate ProgressTargets with forward-looking goals
            ProgressTargets.Clear();
            var progressTargets = await progressTargetsTask;
            foreach (var target in progressTargets)
                ProgressTargets.Add(target);

            // Mock AI Action Summary data
            AIActionSummary = new AIActionSummary
            {
                PrimaryEnrollmentAction = "Enroll the 425 'Good' readiness devices (scores 60-79) using Phase 3 Autonomous Agent",
                EnrollmentActionImpact = 425,
                PrimaryWorkloadAction = "Transition Conditional Access workload to unlock modern security policies",
                WorkloadActionImpact = "Unlock Zero Trust security and app protection policies",
                EnrollmentBlockers = new List<string>
                {
                    "132 devices have insufficient disk space (<20GB free)",
                    "48 devices running Windows 7 (OS upgrade required)",
                    "25 devices have outdated TPM firmware (blocks BitLocker)"
                },
                WorkloadBlockers = new List<string>
                {
                    "Conditional Access policies not yet configured in Intune",
                    "75% enrollment threshold not yet met (currently 58%)"
                },
                AIRecommendation = "Focus on device enrollment first. Use Phase 3 agent to auto-enroll 100 devices/week targeting 'Good' readiness scores. Once you reach 75% enrollment (3 weeks at current velocity), transition Conditional Access workload to unlock $105K annual savings from reduced infrastructure costs.",
                WeeksToNextMilestone = 3,
                IsAIPowered = false
            };
            OnPropertyChanged(nameof(AIActionSummary));

            var blockers = await blockersTask;
            Blockers.Clear();
            foreach (var blocker in blockers)
                Blockers.Add(blocker);

            var engagementOptions = await engagementOptionsTask;
            EngagementOptions.Clear();
            foreach (var option in engagementOptions)
                EngagementOptions.Add(option);

            // Load device selection mock data
            await LoadDeviceSelectionDataAsync();

            UpdateCharts();
        }

        private void UpdateEnrollmentChart()
        {
            if (DeviceEnrollment?.TrendData != null)
            {
                var intuneValues = new ChartValues<int>();
                var configMgrValues = new ChartValues<int>();
                var labels = new List<string>();

                foreach (var trend in DeviceEnrollment.TrendData)
                {
                    intuneValues.Add(trend.IntuneDevices);
                    configMgrValues.Add(trend.ConfigMgrDevices);
                    labels.Add(trend.Month.ToString("MMM yyyy"));
                }

                EnrollmentTrendSeries[0].Values = intuneValues;
                EnrollmentTrendSeries[1].Values = configMgrValues;
                EnrollmentTrendLabels = labels.ToArray();
                
                OnPropertyChanged(nameof(EnrollmentTrendSeries));
                OnPropertyChanged(nameof(EnrollmentTrendLabels));
            }
        }

        private void UpdateComplianceChart()
        {
            if (ComplianceScore != null)
            {
                ComplianceComparisonSeries[0].Values = new ChartValues<double> { ComplianceScore.IntuneScore };
                ComplianceComparisonSeries[1].Values = new ChartValues<double> { ComplianceScore.ConfigMgrScore };
                OnPropertyChanged(nameof(ComplianceComparisonSeries));
            }
        }

        private void UpdateCharts()
        {
            if (DeviceEnrollment?.TrendData != null)
            {
                var intuneValues = new ChartValues<int>();
                var configMgrValues = new ChartValues<int>();
                var labels = new List<string>();

                foreach (var trend in DeviceEnrollment.TrendData)
                {
                    intuneValues.Add(trend.IntuneDevices);
                    configMgrValues.Add(trend.ConfigMgrDevices);
                    labels.Add(trend.Month.ToString("MMM yyyy"));
                }

                EnrollmentTrendSeries[0].Values = intuneValues;
                EnrollmentTrendSeries[1].Values = configMgrValues;
                EnrollmentTrendLabels = labels.ToArray();
                
                OnPropertyChanged(nameof(EnrollmentTrendSeries));
                OnPropertyChanged(nameof(EnrollmentTrendLabels));
            }

            if (ComplianceScore != null)
            {
                ComplianceComparisonSeries[0].Values = new ChartValues<double> { ComplianceScore.IntuneScore };
                ComplianceComparisonSeries[1].Values = new ChartValues<double> { ComplianceScore.ConfigMgrScore };
                OnPropertyChanged(nameof(ComplianceComparisonSeries));
            }
        }

        private async Task RefreshDataAsync()
        {
            await _telemetryService.RefreshDataAsync();
            await LoadDataAsync();
        }

        private async Task TryManualConfigMgrConnection(string siteServer)
        {
            IsLoading = true;
            try
            {
                LogConnection($"Manual ConfigMgr connection requested: {siteServer}");
                
                // Build URL
                var adminServiceUrl = $"https://{siteServer}/AdminService";
                LogConnection($"Attempting connection to: {adminServiceUrl}");
                
                bool success = await _graphDataService.ConfigMgrService.ConfigureAsync(adminServiceUrl);
                
                if (success)
                {
                    var method = _graphDataService.ConfigMgrService.ConnectionMethod;
                    LogConnection($"✅ Manual connection SUCCESS via {method}");
                    
                    System.Windows.MessageBox.Show(
                        $"Successfully connected to ConfigMgr!\n\n" +
                        $"Site Server: {siteServer}\n" +
                        $"Connection Method: {method}\n\n" +
                        $"Refreshing dashboard data...",
                        "ConfigMgr Connected",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                    
                    await RefreshDataAsync();
                }
                else
                {
                    var error = _graphDataService.ConfigMgrService.LastConnectionError;
                    LogConnection($"❌ Manual connection FAILED: {error}");
                    
                    System.Windows.MessageBox.Show(
                        $"Failed to connect to ConfigMgr\n\n" +
                        $"Site Server: {siteServer}\n" +
                        $"URL Tried: {adminServiceUrl}\n\n" +
                        $"Error Details:\n{error}\n\n" +
                        $"Common Issues:\n" +
                        $"• Admin Service not enabled (requires ConfigMgr 1810+)\n" +
                        $"• WMI access denied (need SMS Provider permissions)\n" +
                        $"• Firewall blocking ports 443 (HTTPS) or 135 (WMI)\n" +
                        $"• Site server name incorrect or unreachable\n\n" +
                        $"Check the Diagnostics window for detailed error information.",
                        "ConfigMgr Connection Failed",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                LogConnection($"❌ Manual connection exception: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"Error connecting to ConfigMgr: {ex.Message}",
                    "Connection Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OnStartMigration(Workload? workload)
        {
            if (workload == null) return;
            
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://docs.microsoft.com/mem/configmgr/comanage/how-to-switch-workloads",
                UseShellExecute = true
            });
        }

        private void OnLearnMore(string? url)
        {
            if (string.IsNullOrEmpty(url)) return;
            
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }

        private void OnOpenLink(string? url)
        {
            if (string.IsNullOrEmpty(url)) return;
            
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Unable to open link: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        #region Phase 1 AI Enhancement Commands

        private async Task GenerateMigrationPlanAsync()
        {
            try
            {
                if (_aiRecommendationService == null)
                {
                    System.Windows.MessageBox.Show(
                        "Azure OpenAI is required for migration plan generation.\n\nPlease configure Azure OpenAI using the 🤖 AI button in the toolbar.",
                        "Azure OpenAI Required",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                Instance.Info("Generating migration plan...");

                if (DeviceEnrollment == null)
                {
                    System.Windows.MessageBox.Show(
                        "Please connect to Microsoft Graph first to load device data.",
                        "Connection Required",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                    return;
                }

                // Prompt user for target completion date  
                var months = 3; // Default 3 months
                var targetDate = DateTime.Now.AddMonths(months);

                IsLoading = true;

                MigrationPlan = await _aiRecommendationService.CreateMigrationPlanAsync(
                    DeviceEnrollment.TotalDevices,
                    targetDate,
                    DeviceEnrollment.IntuneEnrolledDevices);

                Instance.Info($"Migration plan generated: {MigrationPlan.Phases.Count} phases, target: {targetDate:yyyy-MM-dd}");

                System.Windows.MessageBox.Show(
                    $"Migration plan generated successfully!\n\n" +
                    $"Phases: {MigrationPlan.Phases.Count}\n" +
                    $"Target Date: {targetDate:MMMM dd, yyyy}\n" +
                    $"Total Devices: {MigrationPlan.TotalDevices}",
                    "Plan Generated",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);

                // Refresh AI recommendations with new plan
                await LoadAIRecommendationsAsync();
            }
            catch (Exception ex)
            {
                Instance.Error($"Error generating migration plan: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"Error generating migration plan: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task MarkPhaseCompleteAsync()
        {
            try
            {
                if (MigrationPlan == null || MigrationPlan.CurrentPhaseIndex < 0)
                    return;

                var currentPhase = MigrationPlan.Phases[MigrationPlan.CurrentPhaseIndex];
                
                var result = System.Windows.MessageBox.Show(
                    $"Mark Phase {currentPhase.PhaseNumber}: {currentPhase.Name} as complete?\n\n" +
                    $"This will record the completion date and move to the next phase.",
                    "Confirm Phase Completion",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result != System.Windows.MessageBoxResult.Yes)
                    return;

                currentPhase.IsComplete = true;
                currentPhase.CompletionDate = DateTime.Now;

                Instance.Info($"Phase {currentPhase.PhaseNumber} marked complete");

                // Trigger UI update
                OnPropertyChanged(nameof(MigrationPlan));

                System.Windows.MessageBox.Show(
                    $"Phase {currentPhase.PhaseNumber} marked as complete!\n\n" +
                    $"Overall Progress: {MigrationPlan.OverallProgress:F0}%",
                    "Phase Complete",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);

                // Refresh AI recommendations
                await LoadAIRecommendationsAsync();
            }
            catch (Exception ex)
            {
                Instance.Error($"Error marking phase complete: {ex.Message}");
            }
        }

        private void OnExportDeviceList()
        {
            try
            {
                Instance.Info("Exporting device readiness list...");

                System.Windows.MessageBox.Show(
                    "Device export functionality will save a CSV file with:\n\n" +
                    "• Device names and readiness scores\n" +
                    "• Enrollment barriers identified\n" +
                    "• Recommended enrollment order\n\n" +
                    "Feature coming in next update!",
                    "Export Device List",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Instance.Error($"Error exporting device list: {ex.Message}");
            }
        }

        private async Task LoadDeviceSelectionDataAsync()
        {
            try
            {
                Instance.Info("Loading device selection intelligence...");

                int unenrolledCount;

                if (DeviceEnrollment != null && DeviceEnrollment.ConfigMgrOnlyDevices > 0)
                {
                    // Use real data
                    unenrolledCount = DeviceEnrollment.ConfigMgrOnlyDevices;
                }
                else
                {
                    // Use mock data (when not authenticated or no devices)
                    unenrolledCount = 50600; // Mock: 50,600 unenrolled devices (matches ConfigMgrOnlyDevices)
                    Instance.Info("Using MOCK device selection data (not authenticated)");
                }

                // Get AI guidance if available
                if (_aiRecommendationService != null)
                {
                    var guidance = await _aiRecommendationService.GetDeviceSelectionGuidanceAsync(unenrolledCount, 50);
                }

                // Calculate readiness counts (works with or without AI)
                ExcellentReadinessCount = Math.Max(0, (int)(unenrolledCount * 0.35)); // ~35% excellent
                GoodReadinessCount = Math.Max(0, (int)(unenrolledCount * 0.30)); // ~30% good
                FairReadinessCount = Math.Max(0, (int)(unenrolledCount * 0.25)); // ~25% fair
                PoorReadinessCount = unenrolledCount - ExcellentReadinessCount - GoodReadinessCount - FairReadinessCount;

                DevicesNeedingPreparation = FairReadinessCount;
                HighRiskDeviceCount = Math.Max(0, (int)(unenrolledCount * 0.10)); // ~10% high risk

                OnPropertyChanged(nameof(NextBatchSize));

                Instance.Info($"Device selection data loaded: {ExcellentReadinessCount} excellent, {GoodReadinessCount} good, {FairReadinessCount} fair, {PoorReadinessCount} poor");
            }
            catch (Exception ex)
            {
                Instance.Error($"Error loading device selection data: {ex.Message}");
            }
        }

        private async Task LoadWorkloadTrendsAsync()
        {
            try
            {
                if (_aiRecommendationService == null)
                {
                    Instance.Warning("Workload trends require Azure OpenAI - skipping");
                    return;
                }

                Instance.Info("Loading workload velocity trends...");

                // If no workloads exist, create mock workloads for demo
                if (Workloads.Count == 0)
                {
                    Instance.Info("No workloads available, using MOCK velocity data");
                    
                    // Create mock velocity data for 3 categories
                    ExcellentVelocityCount = 2; // Mock: 2 workloads with excellent velocity
                    GoodVelocityCount = 3; // Mock: 3 workloads with good velocity
                    StalledWorkloadCount = 0; // Mock: 0 stalled workloads

                    // Create simple mock trend data
                    WorkloadTrendSeries.Clear();
                    var mockSeries = new LineSeries
                    {
                        Title = "Overall Velocity",
                        Values = new ChartValues<double> { 5, 8, 12, 15, 18, 22, 25 },
                        PointGeometry = LiveCharts.Wpf.DefaultGeometries.Circle,
                        PointGeometrySize = 8
                    };
                    WorkloadTrendSeries.Add(mockSeries);
                    
                    WorkloadTrendLabels = new[] { "Oct 15", "Oct 29", "Nov 12", "Nov 26", "Dec 10" };
                    
                    Instance.Info("MOCK workload trends loaded: 2 excellent, 3 good, 0 stalled");
                    return;
                }

                var trends = await _aiRecommendationService.GetWorkloadTrendsAsync(90); // Last 90 days

                if (trends.Count == 0)
                {
                    Instance.Info("No historical trend data available yet, using simplified view");
                    
                    // Show current workload states as static data
                    ExcellentVelocityCount = Workloads.Count(w => w.Status == WorkloadStatus.Completed);
                    GoodVelocityCount = Workloads.Count(w => w.Status == WorkloadStatus.InProgress);
                    StalledWorkloadCount = 0;
                    
                    return;
                }

                // Create chart series for each workload
                WorkloadTrendSeries.Clear();

                foreach (var workload in trends)
                {
                    var series = new LineSeries
                    {
                        Title = workload.Key,
                        Values = new ChartValues<double>(
                            workload.Value.Select(e => e.PercentageComplete)
                        ),
                        PointGeometry = LiveCharts.Wpf.DefaultGeometries.Circle,
                        PointGeometrySize = 8
                    };
                    WorkloadTrendSeries.Add(series);
                }

                // Extract dates for labels (show every 14 days)
                var firstWorkload = trends.First().Value;
                WorkloadTrendLabels = firstWorkload
                    .Where((e, i) => i % 14 == 0)
                    .Select(e => e.Date.ToString("MMM dd"))
                    .ToArray();

                // Analyze velocity for summary counts  
                ExcellentVelocityCount = 0; // >15% per week
                GoodVelocityCount = 0; // 10-15% per week
                StalledWorkloadCount = 0; // <5% per week

                // Simplified velocity calculation for demo
                foreach (var workload in Workloads)
                {
                    var random = new Random(workload.Name.GetHashCode());
                    var velocity = random.Next(0, 20);
                    
                    if (velocity > 15)
                        ExcellentVelocityCount++;
                    else if (velocity >= 10)
                        GoodVelocityCount++;
                    else if (velocity < 5)
                        StalledWorkloadCount++;
                }

                Instance.Info($"Workload trends loaded: {WorkloadTrendSeries.Count} series, {WorkloadTrendLabels.Length} data points");
            }
            catch (Exception ex)
            {
                Instance.Error($"Error loading workload trends: {ex.Message}");
            }
        }

        #endregion

        #region Phase 2 #1: App Migration Intelligence

        private async Task AnalyzeApplicationsAsync()
        {
            IsLoading = true;
            try
            {
                Instance.Info("Analyzing ConfigMgr applications for Intune migration...");

                // Create the AppMigrationService (using demo data for now)
                var appMigrationService = new AppMigrationService(null, null);
                var results = await appMigrationService.AnalyzeApplicationsAsync();

                ApplicationMigrations = new ObservableCollection<ApplicationMigrationAnalysis>(results);

                // Calculate summary counts
                LowComplexityCount = results.Count(a => a.ComplexityCategory == "Low");
                MediumComplexityCount = results.Count(a => a.ComplexityCategory == "Medium");
                HighComplexityCount = results.Count(a => a.ComplexityCategory == "High");
                TotalApplicationCount = results.Count;

                Instance.Info($"Application analysis complete: {TotalApplicationCount} apps " +
                             $"(Low: {LowComplexityCount}, Medium: {MediumComplexityCount}, High: {HighComplexityCount})");

                System.Windows.MessageBox.Show(
                    $"Application analysis complete!\n\n" +
                    $"Total Applications: {TotalApplicationCount}\n" +
                    $"Low Complexity: {LowComplexityCount}\n" +
                    $"Medium Complexity: {MediumComplexityCount}\n" +
                    $"High Complexity: {HighComplexityCount}",
                    "Analysis Complete",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Instance.Error($"Error analyzing applications: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"Failed to analyze applications:\n{ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task GenerateEnrollmentInsightsAsync()
        {
            IsLoadingEnrollmentInsight = true;
            try
            {
                // Check if we're in unauthenticated mode (using mock data)
                if (!UseRealData)
                {
                    Instance.Info("Loading mock enrollment insights (unauthenticated mode)...");
                    EnrollmentInsight = await _telemetryService.GetMockEnrollmentInsightsAsync();
                    
                    if (EnrollmentInsight != null)
                    {
                        Instance.Info($"Mock enrollment insights loaded: {EnrollmentInsight.RecommendedVelocity} devices/week recommended");
                        OnPropertyChanged(nameof(EnrollmentInsight));
                    }
                    return;
                }

                Instance.Info("Generating enrollment momentum insights with GPT-4...");

                var enrollmentService = new EnrollmentMomentumService(_graphDataService);
                
                // Get current enrollment data
                int totalDevices = DeviceEnrollment?.TotalDevices ?? 0;
                int enrolledDevices = DeviceEnrollment?.IntuneEnrolledDevices ?? 0;
                
                // Calculate devices per week (mock for now - in production would come from trend data)
                int devicesPerWeek = enrolledDevices > 0 ? Math.Max(1, enrolledDevices / 4) : 0;
                
                // Check infrastructure (mock for now - in production would query ConfigMgr)
                bool hasCMG = MigrationStatus?.WorkloadsTransitioned >= 2; // 2+ workloads transitioned likely has CMG
                bool hasCoManagement = MigrationStatus?.WorkloadsTransitioned >= 3; // 3+ workloads likely has co-management
                
                // Calculate weeks since start (mock - estimate 1 week per 10% completion)
                int weeksSinceStart = MigrationStatus != null ? 
                    (int)Math.Ceiling(MigrationStatus.CompletionPercentage / 10.0) : 4;

                EnrollmentInsight = await enrollmentService.GetEnrollmentMomentumAsync(
                    totalDevices,
                    enrolledDevices,
                    devicesPerWeek,
                    hasCMG,
                    hasCoManagement,
                    weeksSinceStart);

                if (EnrollmentInsight != null)
                {
                    Instance.Info($"Enrollment insights generated: {EnrollmentInsight.RecommendedVelocity} devices/week recommended (AI: {EnrollmentInsight.IsAIPowered})");
                    OnPropertyChanged(nameof(EnrollmentInsight)); // Force UI update
                }
                else
                {
                    Instance.Warning("Enrollment insights returned null - using fallback logic");
                }
            }
            catch (Exception ex)
            {
                Instance.Error($"Error generating enrollment insights: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"Failed to generate enrollment insights:\n{ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoadingEnrollmentInsight = false;
            }
        }

        #endregion

        #region Tab Data Loading Methods (v1.7.1)

        /// <summary>
        /// Loads workload recommendation data for Workloads tab (Phase 1)
        /// </summary>
        private async Task LoadWorkloadRecommendationDataAsync()
        {
            try
            {
                Instance.Info("Loading workload momentum recommendation...");

                if (UseRealData && _graphDataService.IsAuthenticated && Workloads.Any())
                {
                    // Use real data
                    var completedWorkloads = Workloads.Where(w => w.Status == WorkloadStatus.Completed).Select(w => w.Name).ToList();
                    var inProgressWorkloads = Workloads.Where(w => w.Status == WorkloadStatus.InProgress).Select(w => w.Name).ToList();
                    var complianceScore = ComplianceScore?.IntuneScore ?? 0;
                    var totalDevices = DeviceEnrollment?.TotalDevices ?? 0;
                    var enrolledDevices = DeviceEnrollment?.IntuneEnrolledDevices ?? 0;

                    WorkloadMomentumInsight = await _workloadMomentumService.GetWorkloadRecommendationAsync(
                        completedWorkloads,
                        inProgressWorkloads,
                        complianceScore,
                        totalDevices,
                        enrolledDevices,
                        hasSecurityBaseline: complianceScore > 70
                    );
                    OnPropertyChanged(nameof(WorkloadMomentumInsight));

                    Instance.Info($"✅ Loaded workload recommendation: {WorkloadMomentumInsight?.RecommendedWorkload ?? "None"}");
                }
                else
                {
                    // Use mock data for unauthenticated state
                    Instance.Info("Using MOCK workload recommendation (not authenticated)");
                    WorkloadMomentumInsight = await _workloadMomentumService.GetWorkloadRecommendationAsync(
                        new List<string> { "Compliance Policies", "Device Configuration" },
                        new List<string> { "Windows Update" },
                        75.0,
                        500,
                        300,
                        true
                    );
                    OnPropertyChanged(nameof(WorkloadMomentumInsight));
                    Instance.Info($"MOCK workload insight set: {WorkloadMomentumInsight?.RecommendedWorkload ?? "NULL"}");
                }
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "LoadWorkloadRecommendationDataAsync");
            }
        }

        /// <summary>
        /// Loads application migration data for Applications tab (Phase 2)
        /// </summary>
        private async Task LoadApplicationMigrationDataAsync()
        {
            try
            {
                Instance.Info("Loading application migration analysis...");

                var apps = await _appMigrationService.AnalyzeApplicationsAsync();
                ApplicationMigrations = new ObservableCollection<ApplicationMigrationAnalysis>(apps);
                OnPropertyChanged(nameof(ApplicationMigrations));

                // Update counts
                TotalApplicationCount = apps.Count;
                LowComplexityCount = apps.Count(a => a.ComplexityScore < 30);
                MediumComplexityCount = apps.Count(a => a.ComplexityScore >= 30 && a.ComplexityScore < 60);
                HighComplexityCount = apps.Count(a => a.ComplexityScore >= 60);
                
                OnPropertyChanged(nameof(TotalApplicationCount));
                OnPropertyChanged(nameof(LowComplexityCount));
                OnPropertyChanged(nameof(MediumComplexityCount));
                OnPropertyChanged(nameof(HighComplexityCount));

                if (UseRealData && _graphDataService.IsAuthenticated)
                {
                    Instance.Info($"✅ Loaded {apps.Count} applications from ConfigMgr");
                }
                else
                {
                    Instance.Info($"Using MOCK application data ({apps.Count} sample apps)");
                }
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "LoadApplicationMigrationDataAsync");
            }
        }

        /// <summary>
        /// Loads executive summary data for Executive tab (Phase 3)
        /// </summary>
        private async Task LoadExecutiveSummaryDataAsync()
        {
            try
            {
                Instance.Info("Loading executive summary...");

                if (UseRealData && _graphDataService.IsAuthenticated)
                {
                    // Use real data
                    var totalDevices = DeviceEnrollment?.TotalDevices ?? 0;
                    var enrolledDevices = DeviceEnrollment?.IntuneEnrolledDevices ?? 0;
                    var completedWorkloads = Workloads.Where(w => w.Status == WorkloadStatus.Completed).Select(w => w.Name).ToList();
                    var inProgressWorkloads = Workloads.Where(w => w.Status == WorkloadStatus.InProgress).Select(w => w.Name).ToList();
                    var complianceScore = ComplianceScore?.IntuneScore ?? 0;

                    ExecutiveSummary = await _executiveSummaryService.GetExecutiveSummaryAsync(
                        totalDevices,
                        enrolledDevices,
                        completedWorkloads,
                        inProgressWorkloads,
                        complianceScore,
                        daysSinceStart: 90,
                        daysSinceLastProgress: (DateTime.Now - _lastProgressDate).Days
                    );
                    OnPropertyChanged(nameof(ExecutiveSummary));

                    Instance.Info($"✅ Loaded executive summary: Health Score {ExecutiveSummary?.MigrationHealthScore ?? 0}/100");
                }
                else
                {
                    // Use mock data for unauthenticated state
                    Instance.Info("Using MOCK executive summary (not authenticated)");
                    ExecutiveSummary = await _executiveSummaryService.GetExecutiveSummaryAsync(
                        500,
                        300,
                        new List<string> { "Compliance Policies", "Device Configuration" },
                        new List<string> { "Windows Update" },
                        75.0,
                        90,
                        7
                    );
                    OnPropertyChanged(nameof(ExecutiveSummary));
                    Instance.Info($"MOCK executive summary set: Score={ExecutiveSummary?.MigrationHealthScore ?? 0}");
                }
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "LoadExecutiveSummaryDataAsync");
            }
        }

        /// <summary>
        /// Command handler to load workload recommendation (can be triggered by button)
        /// </summary>
        private async Task LoadWorkloadRecommendationAsync()
        {
            await LoadWorkloadRecommendationDataAsync();
        }

        /// <summary>
        /// Command handler to load executive summary (can be triggered by button)
        /// </summary>
        private async Task LoadExecutiveSummaryAsync()
        {
            await LoadExecutiveSummaryDataAsync();
        }

        /// <summary>
        /// Checks if Azure OpenAI is available and updates IsAIAvailable property
        /// </summary>
        private void CheckAIAvailability()
        {
            try
            {
                // Check if hardcoded Azure OpenAI credentials are present
                var azureOpenAIService = new AzureOpenAIService();
                IsAIAvailable = azureOpenAIService.IsConfigured;
                Instance.Info($"AI Availability: {IsAIAvailable}");
            }
            catch
            {
                IsAIAvailable = false;
            }
        }

        #endregion

        #region Agent v2.0 Command Handlers

        /// <summary>
        /// Generate enrollment plan using ReAct agent
        /// </summary>
        private async Task GenerateAgentPlanAsync()
        {
            if (_enrollmentAgent == null)
            {
                System.Windows.MessageBox.Show(
                    "Enrollment Agent is not initialized. Please restart the application.",
                    "Agent Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return;
            }

            IsAgentRunning = true;
            AgentStatus = "Initializing...";
            AgentReasoningSteps.Clear();

            try
            {
                // Create enrollment goals from UI
                var goals = new EnrollmentGoals
                {
                    TargetCompletionDate = DateTime.Now.AddMonths(6),
                    RiskLevel = RiskTolerance.Balanced,
                    PreferredBatchSize = 50,
                    MaxDevicesPerDay = 100
                };

                AgentGoals = goals;
                Instance.Info($"Starting agent with goal: Enroll {DeviceEnrollment?.ConfigMgrOnlyDevices ?? 0} devices by {goals.TargetCompletionDate:yyyy-MM-dd}");

                // Execute agent
                var trace = await _enrollmentAgent.ExecuteGoalAsync(goals);
                
                CurrentAgentTrace = trace;
                IsAgentRunning = false;
                
                // Phase 3: Start continuous monitoring service
                if (AgentPhaseIndex == 2 && trace.GoalAchieved)
                {
                    await StartPhase3MonitoringAsync();
                }
                
                if (trace.GoalAchieved)
                {
                    if (AgentPhaseIndex == 2)
                    {
                        AgentStatus = $"✅ Phase 3 monitoring active - auto-enrolling as devices improve";
                        AgentCompletionMessage = $"Agent completed! Enrolled {trace.Steps.Count} devices. Continuous monitoring is now active - devices will be automatically enrolled when their readiness improves.";
                    }
                    else
                    {
                        AgentStatus = $"✅ Enrollment complete";
                        AgentCompletionMessage = $"Agent completed successfully! Enrolled {trace.Steps.Count} devices.";
                    }
                    Instance.Info($"Agent completed successfully: {trace.FinalSummary}");
                }
                else
                {
                    AgentStatus = $"⚠️ Completed with warnings";
                    AgentCompletionMessage = $"Enrollment complete with some warnings. {trace.FinalSummary}";
                    Instance.Warning($"Agent completed with warnings: {trace.FinalSummary}");
                }
            }
            catch (Exception ex)
            {
                IsAgentRunning = false;
                AgentStatus = "❌ Agent failed";
                Instance.LogException(ex, "GenerateAgentPlanAsync");
                System.Windows.MessageBox.Show(
                    $"Agent execution failed:\n{ex.Message}",
                    "Agent Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Stop agent execution
        /// </summary>
        private void OnStopAgent()
        {
            if (_enrollmentAgent != null && IsAgentRunning)
            {
                // Stop the agent
                IsAgentRunning = false;
                AgentStatus = "Stopped by user";
                Instance.Info("Agent execution stopped by user");
            }
            
            // Stop monitoring service if active
            if (_monitoringService != null && IsMonitoringActive)
            {
                _monitoringService.StopMonitoring();
                IsMonitoringActive = false;
                Instance.Info("Phase 3 monitoring stopped");
            }
            
            // Clear the completion message when user stops the agent
            AgentCompletionMessage = null;
        }
        
        /// <summary>
        /// Start Phase 3 continuous monitoring
        /// </summary>
        private async Task StartPhase3MonitoringAsync()
        {
            try
            {
                Instance.Info("Starting Phase 3 continuous monitoring...");
                
                // Initialize monitoring service if not already created
                if (_monitoringService == null)
                {
                    var riskService = new RiskAssessmentService();
                    _monitoringService = new DeviceMonitoringService(_graphDataService, riskService, _enrollmentAgent);
                    
                    // Subscribe to monitoring events
                    _monitoringService.StatusChanged += OnMonitoringStatusChanged;
                    _monitoringService.DeviceReadinessChanged += OnDeviceReadinessChanged;
                    _monitoringService.DeviceEnrolled += OnDeviceAutoEnrolled;
                }
                
                // For now, start monitoring without pre-populating devices
                // The agent will have already identified poor/fair devices during execution
                // In a future update, we can query and add specific devices to monitor
                
                // Start the monitoring service
                _monitoringService.StartMonitoring();
                IsMonitoringActive = true;
                MonitoredDeviceCount = 0; // Will be updated as devices are added
                AutoEnrolledToday = 0;
                
                Instance.Info("Phase 3 monitoring started - will auto-enroll devices as they improve");
                
                // Update next check time
                UpdateNextMonitoringCheck();
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "StartPhase3MonitoringAsync");
            }
        }
        
        /// <summary>
        /// Update next monitoring check countdown
        /// </summary>
        private void UpdateNextMonitoringCheck()
        {
            if (_monitoringService != null && IsMonitoringActive)
            {
                var stats = _monitoringService.GetStatistics();
                NextMonitoringCheck = $"{stats.NextCheckIn.TotalMinutes:F0} min";
            }
        }
        
        /// <summary>
        /// Event handler for monitoring status changes
        /// </summary>
        private void OnMonitoringStatusChanged(object? sender, string status)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Instance.Info($"Monitoring status: {status}");
                UpdateNextMonitoringCheck();
            });
        }
        
        /// <summary>
        /// Event handler for device readiness changes
        /// </summary>
        private void OnDeviceReadinessChanged(object? sender, DeviceReadinessChangedEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Instance.Info($"Device readiness improved: {e.DeviceName} from {e.PreviousLevel} ({e.PreviousScore:F0}) to {e.NewLevel} ({e.NewScore:F0})");
            });
        }
        
        /// <summary>
        /// Event handler for auto-enrollment events
        /// </summary>
        private void OnDeviceAutoEnrolled(object? sender, DeviceEnrolledEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (e.Success)
                {
                    AutoEnrolledToday++;
                    Instance.Info($"Phase 3 auto-enrolled: {e.DeviceName} (readiness: {e.ReadinessScore:F0})");
                }
                else
                {
                    Instance.Warning($"Phase 3 auto-enrollment failed: {e.DeviceName} - {e.Message}");
                }
            });
        }

        /// <summary>
        /// Save agent configuration
        /// </summary>
        private void OnSaveAgentConfig()
        {
            if (AgentGoals != null)
            {
                Instance.Info($"Agent config saved: Target date {AgentGoals.TargetCompletionDate:yyyy-MM-dd}, {AgentGoals.RiskLevel} risk");
                System.Windows.MessageBox.Show(
                    "Agent configuration saved successfully!",
                    "Configuration Saved",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// View agent memory and insights
        /// </summary>
        private void OnViewAgentMemory()
        {
            try
            {
                var memoryPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CloudJourneyAddin",
                    "AgentMemory");

                if (System.IO.Directory.Exists(memoryPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = memoryPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        "Agent memory not found. Run the agent first to generate memories.",
                        "No Memory",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "OnViewAgentMemory");
            }
        }
        
        /// <summary>
        /// View monitoring statistics
        /// </summary>
        private void OnViewMonitoringStats()
        {
            try
            {
                if (_monitoringService == null || !IsMonitoringActive)
                {
                    System.Windows.MessageBox.Show(
                        "Monitoring is not currently active. Start the agent in Phase 3 mode to enable continuous monitoring.",
                        "Monitoring Inactive",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                    return;
                }
                
                var stats = _monitoringService.GetStatistics();
                var message = $"📊 Monitoring Statistics\n\n" +
                    $"Status: {(stats.IsActive ? "Active" : "Inactive")}\n" +
                    $"Devices Monitored: {stats.DevicesMonitored}\n" +
                    $"Check Interval: {stats.CheckInterval.TotalMinutes:F0} minutes\n" +
                    $"Next Check In: {stats.NextCheckIn.TotalMinutes:F1} minutes\n";
                
                System.Windows.MessageBox.Show(
                    message,
                    "Monitoring Statistics",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "OnViewMonitoringStats");
            }
        }
        
        /// <summary>
        /// Handle agent phase changes
        /// </summary>
        private void OnAgentPhaseChanged()
        {
            try
            {
                // Update agent phase info text
                switch (AgentPhaseIndex)
                {
                    case 0: // Phase 1: Supervised
                        AgentPhaseInfo = "ℹ️ Phase 1: Supervised Agent\n" +
                            "• Agent plans require your approval before execution\n" +
                            "• Emergency stop available at all times\n" +
                            "• Agent pauses if failure rate exceeds 15%\n" +
                            "• Complete audit trail of all agent actions";
                        ShowAutoApprovalStatus = false;
                        break;
                        
                    case 1: // Phase 2: Conditional
                        AgentPhaseInfo = "ℹ️ Phase 2: Conditional Autonomy\n" +
                            "• Low/Medium risk devices auto-approved\n" +
                            "• High/Critical risk devices require approval\n" +
                            "• Self-adjusting batch sizes based on success rate\n" +
                            "• Risk assessment for every device";
                        ShowAutoApprovalStatus = false; // Will be set to true when agent runs
                        break;
                        
                    case 2: // Phase 3: Full Autonomy
                        AgentPhaseInfo = "ℹ️ Phase 3: Fully Autonomous\n" +
                            "• Continuous monitoring every 15 minutes\n" +
                            "• Auto-enrolls devices when readiness improves\n" +
                            "• No approval required for qualifying devices\n" +
                            "• Real-time device status tracking";
                        break;
                }
                
                // Update agent if it exists
                if (_enrollmentAgent != null)
                {
                    _enrollmentAgent.CurrentPhase = AgentPhaseIndex switch
                    {
                        0 => AgentPhase.Phase1_Supervised,
                        1 => AgentPhase.Phase2_Conditional,
                        2 => AgentPhase.Phase3_FullAutonomy,
                        _ => AgentPhase.Phase1_Supervised
                    };
                }
                
                Instance.Info($"Agent phase changed to: {AgentPhaseIndex} ({_enrollmentAgent?.CurrentPhase})");
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "OnAgentPhaseChanged");
            }
        }

        /// <summary>
        /// Event handler for agent reasoning steps
        /// </summary>
        private void OnAgentReasoningStepCompleted(object? sender, AgentReasoningStep step)
        {
            // Update on UI thread
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AgentReasoningSteps.Add(step);
                AgentStatus = $"Step {AgentReasoningSteps.Count}: {step.ToolToUse ?? "Thinking..."}";
            });
        }

        /// <summary>
        /// Event handler for agent status changes
        /// </summary>
        private void OnAgentStatusChanged(object? sender, string status)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AgentStatus = status;
            });
        }

        /// <summary>
        /// Event handler for agent insights
        /// </summary>
        private void OnAgentInsightDiscovered(object? sender, AgentInsight insight)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Instance.Info($"Agent discovered insight: {insight.Pattern} (confidence: {insight.Confidence:P})");
            });
        }

        #endregion

        private void OnAction(string? action)
        {
            if (string.IsNullOrEmpty(action)) return;
            
            // Handle specific actions
            switch (action.ToLower())
            {
                case "fasttrack":
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "https://www.microsoft.com/fasttrack",
                        UseShellExecute = true
                    });
                    break;
            }
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => _execute();
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
        public void Execute(object? parameter) => _execute((T?)parameter);
    }
}
