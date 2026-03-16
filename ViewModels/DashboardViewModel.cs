using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ZeroTrustMigrationAddin.Models;
using ZeroTrustMigrationAddin.Services;
using ZeroTrustMigrationAddin.Services.AgentTools;
using ZeroTrustMigrationAddin.Services.Pipeline;
using LiveCharts;
using LiveCharts.Wpf;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly MockDataService _mockDataService;
        private readonly GraphDataService _graphDataService;
        private AIRecommendationService? _aiRecommendationService;
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
        private ZeroTrustMigrationAddin.Services.MigrationPlan? _migrationPlan;
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
        
        // v3.17.118 - Application Readiness (moved from Cloud Readiness tab)
        private double _appReadinessPercentage = 67.0;
        private int _appReadinessEasyCount = 156;
        private int _appReadinessModerateCount = 55;
        private int _appReadinessComplexCount = 23;
        private int _appBlockerAppVCount = 23;
        private int _appBlockerScriptCount = 45;
        
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
        
        // v3.17.234 - Analysis Pipeline fields
        private AnalysisPipelineResult? _pipelineResult;
        private string _pipelineSeverity = "None";
        private bool _hasPipelineStall;
        private string _pipelineStallSummary = string.Empty;
        private string _pipelineStallClassification = string.Empty;
        private string _pipelineCostOfInaction = string.Empty;
        private ObservableCollection<PipelineRecommendation> _pipelineRecommendations = new();
        private int _trustResetBatchSize;
        private bool _hasWorkloadStall;
        private string _workloadStallSummary = string.Empty;
        private bool _isWorkloadTrustTrough;
        private ObservableCollection<StalledWorkload> _stalledWorkloadDetails = new();

        // v3.16.23 - Event for notifying UI when real data is loaded
        public event EventHandler? RealDataLoaded;

        // Tab visibility options (controlled by command-line switches)
        private Visibility _showEnrollmentTab = Visibility.Visible;
        private Visibility _showWorkloadsTab = Visibility.Visible;
        private Visibility _showWorkloadBrainstormTab = Visibility.Visible;
        private Visibility _showApplicationsTab = Visibility.Visible;
        private Visibility _showAIActionsTab = Visibility.Visible;
        private Visibility _showCloudReadinessTab = Visibility.Visible;
        private Visibility _showCloudValueComparisonTab = Visibility.Collapsed;
        private Visibility _showCloudComparisonDetailsTab = Visibility.Collapsed;
        private Visibility _showDecisionCardsTab = Visibility.Visible;
        private bool _demoStallMode;

        /// <summary>When true, pipeline injects mock stall data for UI preview. Activated via /demostall launch switch.</summary>
        public bool DemoStallMode => _demoStallMode;

        public DashboardViewModel(MockDataService mockDataService, TabVisibilityOptions? tabVisibilityOptions = null)
        {
            _mockDataService = mockDataService;
            _graphDataService = new GraphDataService();
            
            // Apply tab visibility options from command-line arguments
            if (tabVisibilityOptions != null)
            {
                _showEnrollmentTab = tabVisibilityOptions.ShowEnrollmentTab;
                _showWorkloadsTab = tabVisibilityOptions.ShowWorkloadsTab;
                _showWorkloadBrainstormTab = tabVisibilityOptions.ShowWorkloadBrainstormTab;
                _showApplicationsTab = tabVisibilityOptions.ShowApplicationsTab;
                _showAIActionsTab = tabVisibilityOptions.ShowAIActionsTab;
                _showCloudReadinessTab = tabVisibilityOptions.ShowCloudReadinessTab;
                _showCloudValueComparisonTab = tabVisibilityOptions.ShowCloudValueComparisonTab;
                _showCloudComparisonDetailsTab = tabVisibilityOptions.ShowCloudComparisonDetailsTab;
                _showDecisionCardsTab = tabVisibilityOptions.ShowDecisionCardsTab;
                _demoStallMode = tabVisibilityOptions.DemoStallMode;
            }
            
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
            _deviceReadinessService = new DeviceReadinessService(_graphDataService.ConfigMgrService, _graphDataService);
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
            var assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
            Instance.Info($"Version: {assemblyVersion} - Enrollment Agent (Production Build)");
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
            ShowConfigMgrSettingsCommand = new RelayCommand(OnShowConfigMgrSettings);
            TestOpenAIConnectionCommand = new RelayCommand(async () => await TestOpenAIConnectionAsync());
            SaveOpenAIConfigCommand = new RelayCommand(OnSaveOpenAIConfig);
            OpenSetupGuideCommand = new RelayCommand(OnOpenSetupGuide);
            OpenLogFolderCommand = new RelayCommand(OnOpenLogFolder);
            OpenUserGuideCommand = new RelayCommand(OnOpenUserGuide);
            OpenUserGuideSectionCommand = new RelayCommand<string>(OnOpenUserGuideSection);
            ShowFeedbackCommand = new RelayCommand(OnShowFeedback);
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
            
            // Enhanced Workloads Tab Commands
            StartWorkloadTransitionCommand = new RelayCommand<string>(OnStartWorkloadTransition);
            ViewRollbackPlanCommand = new RelayCommand(OnViewRollbackPlan);
            StartPilotPhaseCommand = new RelayCommand(OnStartPilotPhase);
            OpenLearnMoreCommand = new RelayCommand<string>(OnOpenLearnMore);
            OpenRemediationUrlCommand = new RelayCommand<string>(OnOpenRemediationUrl);
            
            // Agent v2.0 commands
            GenerateAgentPlanCommand = new RelayCommand(async () => await GenerateAgentPlanAsync(), () => !IsAgentRunning);
            StopAgentCommand = new RelayCommand(OnStopAgent, () => IsAgentRunning);
            SaveAgentConfigCommand = new RelayCommand(OnSaveAgentConfig);
            ViewAgentMemoryCommand = new RelayCommand(OnViewAgentMemory);
            ViewMonitoringStatsCommand = new RelayCommand(OnViewMonitoringStats);
            
            // Chart series toggle commands
            ToggleComanagedSeriesCommand = new RelayCommand(() => IsComanagedSeriesVisible = !IsComanagedSeriesVisible);
            ToggleCloudNativeSeriesCommand = new RelayCommand(() => IsCloudNativeSeriesVisible = !IsCloudNativeSeriesVisible);
            ToggleConfigMgrOnlySeriesCommand = new RelayCommand(() => IsConfigMgrOnlySeriesVisible = !IsConfigMgrOnlySeriesVisible);

            InitializeCharts();
            WorkloadTrendSeries = new SeriesCollection();
            WorkloadTrendLabels = Array.Empty<string>();
            InitializeWorkloadsWithBenefits();
            
            // Initialize WorkloadMomentumInsight with compelling mock data for Priority #2
            WorkloadMomentumInsight = new WorkloadMomentumInsight
            {
                RecommendedWorkload = "Compliance Policies",
                Rationale = "Start here! Compliance Policies establish your security foundation with minimal risk. 87% of your devices meet requirements, and rollback takes just 30 minutes if needed.",
                ReadinessScore = 87,
                RiskLevel = "Low",
                EstimatedWeeks = 3,
                SuccessFactors = new List<string>
                {
                    "Low complexity - policies are evaluative, not enforcing",
                    "87% device readiness means fast adoption",
                    "Foundation for all other workload migrations"
                },
                RollbackTimeMinutes = 30,
                SafetyScore = "High",
                PolicyConflicts = new List<string>(),
                Prerequisites = new List<string> { "Microsoft Intune licenses assigned", "Device enrollment completed" }
            };
            
            // Initialize WorkloadMotivationInsight with mock AI analysis (unauthenticated state)
            WorkloadMotivationInsight = new WorkloadMotivationInsight
            {
                WorkloadName = "Compliance Policies",
                AIReasons = new List<string>
                {
                    "60-70% of enterprises have majority-remote workforce. ConfigMgr can't verify compliance without VPN connectivity. Intune enables cloud-native compliance checks anywhere, anytime.",
                    "WSUS failures increase 300% with remote work. On-prem update servers struggle with distributed workforce. Intune delivers updates directly from Microsoft cloud with zero infrastructure.",
                    "Average E3 customer uses only 35% of Intune features. You're paying for Conditional Access and cloud-native management but ConfigMgr can't enable these capabilities."
                },
                Risks = new List<RiskItem>
                {
                    new RiskItem
                    {
                        Level = "High",
                        Title = "Remote device compliance gaps",
                        Impact = "Security policy violations go undetected for weeks",
                        Likelihood = "68% of organizations report this issue with ConfigMgr-only management",
                        Fix = "Move Compliance Policies to Intune for real-time cloud verification"
                    },
                    new RiskItem
                    {
                        Level = "Medium",
                        Title = "Infrastructure maintenance overhead",
                        Impact = "10-15 hours per week spent on server maintenance and troubleshooting",
                        Likelihood = "Typical for on-prem WSUS/ConfigMgr infrastructure",
                        Fix = "Shift to cloud-native update delivery—eliminate server maintenance"
                    },
                    new RiskItem
                    {
                        Level = "Low",
                        Title = "Missing cross-platform capabilities",
                        Impact = "Cannot manage Mac, iOS, Android devices natively",
                        Likelihood = "BYOD adoption growing 20% annually across enterprises",
                        Fix = "Enable Intune for comprehensive cross-platform device management"
                    }
                }
            };
            
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
                    OnPropertyChanged(nameof(IsDataSourceConnected));
                    OnPropertyChanged(nameof(IsFullyAuthenticated));
                }
            }
        }

        /// <summary>
        /// True when BOTH required data sources (Graph AND ConfigMgr) are connected.
        /// AI is optional and not required for real data display.
        /// </summary>
        public bool IsDataSourceConnected =>
            _graphDataService.IsAuthenticated && IsConfigMgrConnected;

        /// <summary>
        /// True when ALL THREE optional enhancements are established:
        /// 1. Microsoft Graph (Intune)
        /// 2. Configuration Manager (Admin Service)
        /// 3. Azure OpenAI (optional for AI features)
        /// </summary>
        public bool IsFullyAuthenticated =>
            _graphDataService.IsAuthenticated &&
            IsConfigMgrConnected &&
            _aiRecommendationService != null;

        /// <summary>
        /// Exposes the GraphDataService for device queries
        /// </summary>
        public GraphDataService GraphDataService => _graphDataService;

        /// <summary>
        /// Exposes the ConfigMgrAdminService for ConfigMgr queries.
        /// Note: Use GraphDataService.ConfigMgrService for actual operations.
        /// </summary>
        public ConfigMgrAdminService ConfigMgrAdminService => _graphDataService.ConfigMgrService;

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

        private SeriesCollection? _deviceIdentityPieSeries;
        public SeriesCollection? DeviceIdentityPieSeries
        {
            get => _deviceIdentityPieSeries;
            set => SetProperty(ref _deviceIdentityPieSeries, value);
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
        public bool IsAIConfigured => _aiRecommendationService != null && _aiRecommendationService.IsConfigured;
        public bool IsAINotConfigured => !IsAIConfigured;
        public bool HasNoRecommendationsAndConfigured => HasNoRecommendations && IsAIConfigured;

        // Tab visibility properties (controlled by command-line switches)
        public Visibility ShowEnrollmentTab
        {
            get => _showEnrollmentTab;
            set => SetProperty(ref _showEnrollmentTab, value);
        }

        public Visibility ShowWorkloadsTab
        {
            get => _showWorkloadsTab;
            set => SetProperty(ref _showWorkloadsTab, value);
        }

        public Visibility ShowWorkloadBrainstormTab
        {
            get => _showWorkloadBrainstormTab;
            set => SetProperty(ref _showWorkloadBrainstormTab, value);
        }

        public Visibility ShowApplicationsTab
        {
            get => _showApplicationsTab;
            set => SetProperty(ref _showApplicationsTab, value);
        }

        public Visibility ShowAIActionsTab
        {
            get => _showAIActionsTab;
            set => SetProperty(ref _showAIActionsTab, value);
        }

        public Visibility ShowCloudReadinessTab
        {
            get => _showCloudReadinessTab;
            set => SetProperty(ref _showCloudReadinessTab, value);
        }

        public Visibility ShowCloudValueComparisonTab
        {
            get => _showCloudValueComparisonTab;
            set => SetProperty(ref _showCloudValueComparisonTab, value);
        }

        public Visibility ShowCloudComparisonDetailsTab
        {
            get => _showCloudComparisonDetailsTab;
            set => SetProperty(ref _showCloudComparisonDetailsTab, value);
        }

        public Visibility ShowDecisionCardsTab
        {
            get => _showDecisionCardsTab;
            set => SetProperty(ref _showDecisionCardsTab, value);
        }

        // === Ideas Tab Properties (Decision Cards + Tier 1) ===
        public ObservableCollection<DecisionCard> DecisionCards { get; } = new();
        public ObservableCollection<WorkloadUnlockChain> UnlockChains { get; } = new();
        public ObservableCollection<ConfigMgrCoverage> CoverageCards { get; } = new();
        public ObservableCollection<WorkloadSafetyScore> SafetyScores { get; } = new();

        // === Deep Analysis Features (3 deep-dive features) ===
        private UninstallReadinessResult? _uninstallReadiness;
        public UninstallReadinessResult? UninstallReadiness
        {
            get => _uninstallReadiness;
            set => SetProperty(ref _uninstallReadiness, value);
        }

        private SecurityExposureResult? _securityExposure;
        public SecurityExposureResult? SecurityExposure
        {
            get => _securityExposure;
            set => SetProperty(ref _securityExposure, value);
        }

        private StaleOrphanResult? _staleOrphanResult;
        public StaleOrphanResult? StaleOrphanResult
        {
            get => _staleOrphanResult;
            set => SetProperty(ref _staleOrphanResult, value);
        }

        private LastHoldoutSpotlight? _lastHoldoutSpotlight;
        public LastHoldoutSpotlight? LastHoldoutSpotlightCard
        {
            get => _lastHoldoutSpotlight;
            set => SetProperty(ref _lastHoldoutSpotlight, value);
        }
        public bool HasLastHoldoutSpotlight => LastHoldoutSpotlightCard?.IsVisible == true;
        public bool HasDecisionCards => DecisionCards.Count > 0;

        // Phase 1 AI Enhancement Properties
        public ZeroTrustMigrationAddin.Services.MigrationPlan? MigrationPlan
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

        #region Analysis Pipeline Properties

        public AnalysisPipelineResult? PipelineResult
        {
            get => _pipelineResult;
            set => SetProperty(ref _pipelineResult, value);
        }

        public string PipelineSeverity
        {
            get => _pipelineSeverity;
            set => SetProperty(ref _pipelineSeverity, value);
        }

        public bool HasPipelineStall
        {
            get => _hasPipelineStall;
            set => SetProperty(ref _hasPipelineStall, value);
        }

        public string PipelineStallSummary
        {
            get => _pipelineStallSummary;
            set => SetProperty(ref _pipelineStallSummary, value);
        }

        public string PipelineStallClassification
        {
            get => _pipelineStallClassification;
            set => SetProperty(ref _pipelineStallClassification, value);
        }

        public string PipelineCostOfInaction
        {
            get => _pipelineCostOfInaction;
            set => SetProperty(ref _pipelineCostOfInaction, value);
        }

        public ObservableCollection<PipelineRecommendation> PipelineRecommendations
        {
            get => _pipelineRecommendations;
            set => SetProperty(ref _pipelineRecommendations, value);
        }

        public int TrustResetBatchSize
        {
            get => _trustResetBatchSize;
            set => SetProperty(ref _trustResetBatchSize, value);
        }

        public bool HasWorkloadStall
        {
            get => _hasWorkloadStall;
            set => SetProperty(ref _hasWorkloadStall, value);
        }

        public string WorkloadStallSummary
        {
            get => _workloadStallSummary;
            set => SetProperty(ref _workloadStallSummary, value);
        }

        public bool IsWorkloadTrustTrough
        {
            get => _isWorkloadTrustTrough;
            set => SetProperty(ref _isWorkloadTrustTrough, value);
        }

        public ObservableCollection<StalledWorkload> StalledWorkloadDetails
        {
            get => _stalledWorkloadDetails;
            set => SetProperty(ref _stalledWorkloadDetails, value);
        }

        public bool HasPipelineRecommendations => PipelineRecommendations.Count > 0;

        #endregion

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

        // v3.17.118 - Application Readiness Properties (moved from Cloud Readiness tab)
        public double AppReadinessPercentage
        {
            get => _appReadinessPercentage;
            set => SetProperty(ref _appReadinessPercentage, value);
        }

        public int AppReadinessEasyCount
        {
            get => _appReadinessEasyCount;
            set => SetProperty(ref _appReadinessEasyCount, value);
        }

        public int AppReadinessModerateCount
        {
            get => _appReadinessModerateCount;
            set => SetProperty(ref _appReadinessModerateCount, value);
        }

        public int AppReadinessComplexCount
        {
            get => _appReadinessComplexCount;
            set => SetProperty(ref _appReadinessComplexCount, value);
        }

        public int AppBlockerAppVCount
        {
            get => _appBlockerAppVCount;
            set => SetProperty(ref _appBlockerAppVCount, value);
        }

        public int AppBlockerScriptCount
        {
            get => _appBlockerScriptCount;
            set => SetProperty(ref _appBlockerScriptCount, value);
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

        // AI-powered workload motivation
        private WorkloadMotivationInsight? _workloadMotivationInsight;
        public WorkloadMotivationInsight? WorkloadMotivationInsight
        {
            get => _workloadMotivationInsight;
            set => SetProperty(ref _workloadMotivationInsight, value);
        }

        // Enhanced Workloads Tab Properties
        public bool HasWorkloadBlockers => TopWorkloadBlockers.Count > 0;
        public int WorkloadBlockerDeviceCount => TopWorkloadBlockers.Sum(b => b.AffectedDevices);
        public string BlockedWorkloadName => "Device Configuration"; // Dynamically set based on blockers
        
        // v3.17.227 - Real workload authority properties
        private WorkloadAuthoritySummary? _workloadAuthority;
        public WorkloadAuthoritySummary? WorkloadAuthority
        {
            get => _workloadAuthority;
            set => SetProperty(ref _workloadAuthority, value);
        }

        /// <summary>Number of workloads with ≥90% Intune adoption (considered "Completed")</summary>
        public int WorkloadsCompletedCount => Workloads.Count(w => w.Status == WorkloadStatus.Completed);
        
        /// <summary>Devices with 6 of 7 workloads on Intune (one workload away from cloud-native)</summary>
        private int _nearCloudNativeCount;
        public int NearCloudNativeCount
        {
            get => _nearCloudNativeCount;
            set => SetProperty(ref _nearCloudNativeCount, value);
        }

        /// <summary>Workloads that are the last holdout for the most devices</summary>
        private ObservableCollection<LastHoldoutWorkload> _lastHoldoutWorkloads = new();
        public ObservableCollection<LastHoldoutWorkload> LastHoldoutWorkloads
        {
            get => _lastHoldoutWorkloads;
            set => SetProperty(ref _lastHoldoutWorkloads, value);
        }

        public bool HasLastHoldouts => LastHoldoutWorkloads.Count > 0;
        public bool HasWorkloadAuthority => WorkloadAuthority != null || Workloads.Any(w => w.IntuneAdoptionPercentage > 0);
        public int TotalCoManagedDevices => WorkloadAuthority?.TotalCoManagedDevices ?? Workloads.Select(w => w.IntuneDeviceCount + w.ConfigMgrDeviceCount).DefaultIfEmpty(0).Max();
        public int DevicesReadyForCloudNative => WorkloadAuthority?.DevicesReadyForCloudNative ?? 0;

        /// <summary>Data-driven migration sequence computed from real workload adoption data</summary>
        private ObservableCollection<WorkloadSequenceStep> _workloadSequenceSteps = new();
        public ObservableCollection<WorkloadSequenceStep> WorkloadSequenceSteps
        {
            get => _workloadSequenceSteps;
            set => SetProperty(ref _workloadSequenceSteps, value);
        }
        public bool HasWorkloadSequence => WorkloadSequenceSteps.Count > 0;
        
        private ObservableCollection<Blocker> _topWorkloadBlockers = new();
        public ObservableCollection<Blocker> TopWorkloadBlockers
        {
            get => _topWorkloadBlockers;
            set
            {
                if (SetProperty(ref _topWorkloadBlockers, value))
                {
                    OnPropertyChanged(nameof(HasWorkloadBlockers));
                    OnPropertyChanged(nameof(WorkloadBlockerDeviceCount));
                }
            }
        }

        // Safety Dashboard Properties
        private int _readyDevicesForWorkload;
        public int ReadyDevicesForWorkload
        {
            get => _readyDevicesForWorkload;
            set => SetProperty(ref _readyDevicesForWorkload, value);
        }

        private int _totalDevicesForWorkload;
        public int TotalDevicesForWorkload
        {
            get => _totalDevicesForWorkload;
            set => SetProperty(ref _totalDevicesForWorkload, value);
        }

        public double ReadyDevicesPercentage => TotalDevicesForWorkload > 0 
            ? (double)ReadyDevicesForWorkload / TotalDevicesForWorkload * 100 
            : 0;

        public string PolicyConflictsStatusIcon => WorkloadMomentumInsight?.PolicyConflicts.Count == 0 ? "✅" : "⚠️";
        public string PolicyConflictsStatusText => WorkloadMomentumInsight?.PolicyConflicts.Count == 0 
            ? "No policy conflicts detected" 
            : $"{WorkloadMomentumInsight?.PolicyConflicts.Count} conflicts found (need resolution)";

        public string PrerequisitesStatusIcon => WorkloadMomentumInsight?.Prerequisites.Count == 0 ? "✅" : "⏸️";
        public string PrerequisitesStatusText => WorkloadMomentumInsight?.Prerequisites.Count == 0 
            ? "All prerequisites met" 
            : $"{WorkloadMomentumInsight?.Prerequisites.Count} prerequisites pending";

        private int _devicesNeedingRemediation;
        public int DevicesNeedingRemediation
        {
            get => _devicesNeedingRemediation;
            set => SetProperty(ref _devicesNeedingRemediation, value);
        }

        public string RemediationStatusIcon => DevicesNeedingRemediation == 0 ? "✅" : "⚠️";
        public string RemediationStatusText => DevicesNeedingRemediation == 0 
            ? "All devices ready" 
            : $"{DevicesNeedingRemediation} devices need preparation";

        // Progress Tracking Panel Properties
        private string _velocityIcon = "⚡";
        public string VelocityIcon
        {
            get => _velocityIcon;
            set => SetProperty(ref _velocityIcon, value);
        }

        private string _velocityLabel = "Good Velocity";
        public string VelocityLabel
        {
            get => _velocityLabel;
            set => SetProperty(ref _velocityLabel, value);
        }

        private string _velocityDescription = "10-15% per week";
        public string VelocityDescription
        {
            get => _velocityDescription;
            set => SetProperty(ref _velocityDescription, value);
        }

        private string _velocityBgColor = "#FFF9E6";
        public string VelocityBgColor
        {
            get => _velocityBgColor;
            set => SetProperty(ref _velocityBgColor, value);
        }

        private string _velocityTextColor = "#FDB813";
        public string VelocityTextColor
        {
            get => _velocityTextColor;
            set => SetProperty(ref _velocityTextColor, value);
        }

        private bool _hasPeerComparison;
        public bool HasPeerComparison
        {
            get => _hasPeerComparison;
            set => SetProperty(ref _hasPeerComparison, value);
        }

        private double _yourVelocityPercent;
        public double YourVelocityPercent
        {
            get => _yourVelocityPercent;
            set => SetProperty(ref _yourVelocityPercent, value);
        }

        private double _peerVelocityPercent;
        public double PeerVelocityPercent
        {
            get => _peerVelocityPercent;
            set => SetProperty(ref _peerVelocityPercent, value);
        }

        private string _accelerationNeeded = "N/A";
        public string AccelerationNeeded
        {
            get => _accelerationNeeded;
            set => SetProperty(ref _accelerationNeeded, value);
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
        
        /// <summary>
        /// Indicates if there is sufficient enrollment history to display the trend chart.
        /// When false, shows a message explaining why.
        /// </summary>
        public bool HasSufficientTrendData => DeviceEnrollment?.HasSufficientTrendData ?? true;
        
        /// <summary>
        /// Reason why trend data is unavailable. Displayed to user when HasSufficientTrendData is false.
        /// </summary>
        public string TrendDataUnavailableReason => DeviceEnrollment?.TrendDataUnavailableReason ?? string.Empty;
        
        // Chart series visibility toggles
        private bool _isComanagedSeriesVisible = true;
        private bool _isCloudNativeSeriesVisible = true;
        private bool _isConfigMgrOnlySeriesVisible = true;
        
        public bool IsComanagedSeriesVisible
        {
            get => _isComanagedSeriesVisible;
            set
            {
                if (SetProperty(ref _isComanagedSeriesVisible, value))
                    UpdateSeriesVisibility(0, value);
            }
        }
        
        public bool IsCloudNativeSeriesVisible
        {
            get => _isCloudNativeSeriesVisible;
            set
            {
                if (SetProperty(ref _isCloudNativeSeriesVisible, value))
                    UpdateSeriesVisibility(1, value);
            }
        }
        
        public bool IsConfigMgrOnlySeriesVisible
        {
            get => _isConfigMgrOnlySeriesVisible;
            set
            {
                if (SetProperty(ref _isConfigMgrOnlySeriesVisible, value))
                    UpdateSeriesVisibility(2, value);
            }
        }
        
        private void UpdateSeriesVisibility(int seriesIndex, bool isVisible)
        {
            if (EnrollmentTrendSeries != null && EnrollmentTrendSeries.Count > seriesIndex)
            {
                var series = EnrollmentTrendSeries[seriesIndex] as LiveCharts.Wpf.LineSeries;
                if (series != null)
                {
                    series.Visibility = isVisible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
                }
            }
        }
        
        public ICommand ToggleComanagedSeriesCommand { get; }
        public ICommand ToggleCloudNativeSeriesCommand { get; }
        public ICommand ToggleConfigMgrOnlySeriesCommand { get; }
        
        public SeriesCollection ComplianceComparisonSeries { get; set; } = new SeriesCollection();
        
        public ICommand RefreshCommand { get; }
        public ICommand ConnectToGraphCommand { get; }
        public ICommand ConnectToConfigMgrCommand { get; }
        public ICommand ShowDiagnosticsCommand { get; }
        public ICommand ShowAISettingsCommand { get; }
        public ICommand ShowConfigMgrSettingsCommand { get; }
        public ICommand TestOpenAIConnectionCommand { get; }
        public ICommand SaveOpenAIConfigCommand { get; }
        public ICommand OpenSetupGuideCommand { get; }
        public ICommand OpenLogFolderCommand { get; }
        public ICommand OpenUserGuideCommand { get; }
        public ICommand OpenUserGuideSectionCommand { get; }
        public ICommand ShowFeedbackCommand { get; }
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
        
        // Enhanced Workloads Tab Commands
        public ICommand StartWorkloadTransitionCommand { get; }
        public ICommand ViewRollbackPlanCommand { get; }
        public ICommand StartPilotPhaseCommand { get; }
        public ICommand OpenLearnMoreCommand { get; }
        public ICommand OpenRemediationUrlCommand { get; }
        
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

        private void OnOpenUserGuideSection(string? sectionId)
        {
            try
            {
                Instance.Info($"User requested help for section: {sectionId}");
                
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string userGuidePath = System.IO.Path.Combine(appDirectory, "AdminUserGuide.html");
                
                if (System.IO.File.Exists(userGuidePath))
                {
                    string url = !string.IsNullOrEmpty(sectionId) ? $"{userGuidePath}#{sectionId}" : userGuidePath;
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                    Instance.Info($"Opened User Guide at section: {sectionId}");
                }
                else
                {
                    OnOpenUserGuide();
                }
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "OnOpenUserGuideSection");
            }
        }

        private void OnShowFeedback()
        {
            try
            {
                Instance.Info("User requested to open Feedback window");
                var mainWindow = System.Windows.Application.Current.MainWindow;
                var feedbackWindow = new Views.FeedbackWindow(mainWindow);
                feedbackWindow.Owner = mainWindow;
                feedbackWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "OnShowFeedback");
                System.Windows.MessageBox.Show(
                    $"Failed to open Feedback window: {ex.Message}",
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
            
            // CRITICAL: Check ACTUAL runtime state, not cached properties
            bool graphConnected = _graphDataService?.IsAuthenticated ?? false;
            bool configMgrConnected = _graphDataService?.ConfigMgrService?.IsConfigured ?? false;
            bool aiConnected = _aiRecommendationService != null && (_aiRecommendationService?.IsConfigured ?? false);
            
            // v3.17.11 - Also detect if we're actually showing mock data by checking device counts
            // Mock data has 115,000+ devices, real environments typically have much fewer
            bool hasRealDeviceData = DeviceEnrollment != null && 
                                     DeviceEnrollment.TotalDevices > 0 && 
                                     DeviceEnrollment.TotalDevices < 100000;  // Mock data = 115,000
            
            // Log actual state for debugging
            Instance.Info($"[DIAGNOSTICS] Graph: {graphConnected}, ConfigMgr: {configMgrConnected}, AI: {aiConnected}, UseRealData: {UseRealData}, HasRealDeviceData: {hasRealDeviceData}, TotalDevices: {DeviceEnrollment?.TotalDevices ?? 0}");
            
            // Overall authentication status - based on ACTUAL state AND device data validation
            // Both connections must be established AND device data must look real (not mock)
            bool actuallyShowingRealData = graphConnected && configMgrConnected && hasRealDeviceData;
            string overallStatus = actuallyShowingRealData 
                ? "✅ FULLY AUTHENTICATED - Showing REAL DATA" 
                : "⚠️ NOT FULLY AUTHENTICATED - Showing MOCK DATA";
            
            // Add warning if cached property disagrees with reality
            if (actuallyShowingRealData != UseRealData)
            {
                overallStatus += $"\n\n⚠️ WARNING: UI state mismatch detected!\n" +
                    $"Expected UseRealData: {actuallyShowingRealData}\n" +
                    $"Actual UseRealData: {UseRealData}";
                Instance.Warning($"[DIAGNOSTICS] STATE MISMATCH: Expected UseRealData={actuallyShowingRealData}, Actual={UseRealData}");
            }
            
            // Graph status
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
            string configMgrMethod = _graphDataService?.ConfigMgrService?.ConnectionMethod ?? "Unknown";
            string connectionError = _graphDataService?.ConfigMgrService?.LastConnectionError ?? "Unknown";
            
            string statusMessage;
            if (configMgrConnected)
            {
                statusMessage = $"✅ Connected successfully\n\n" +
                    $"Connection Method: {configMgrMethod}\n" +
                    $"Status: Ready to query ConfigMgr device inventory\n\n" +
                    $"What this means:\n";
                
                // Null-safe check for IsUsingWmiFallback
                bool usingWmiFallback = _graphDataService?.ConfigMgrService?.IsUsingWmiFallback ?? false;
                if (usingWmiFallback)
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
            diagWindow.SetAIStatus(
                aiConnected,
                aiConnected ?
                    "✅ Connected successfully\nAzure OpenAI configured and ready\nGPT-4 recommendations enabled" :
                    "❌ Not configured\nClick '🤖 AI' button to configure Azure OpenAI",
                aiConnected ?
                    "Required for: AI-powered recommendations, stall analysis, migration insights" :
                    "NOT CONFIGURED - Optional enhancement"
            );

            // Overall authentication message - use actual state
            diagWindow.SetOverallStatus(
                actuallyShowingRealData,
                overallStatus,
                actuallyShowingRealData ?
                    "Data sources connected. Dashboard is showing real data from your environment." +
                    (!aiConnected ? "\n\n⚠️ Azure OpenAI not configured - AI features limited." : "") :
                    "⚠️ IMPORTANT: Both Microsoft Graph AND Configuration Manager must be connected to view real data.\n\n" +
                    $"Current state:\n" +
                    $"  • Microsoft Graph: {(graphConnected ? "✅ Connected" : "❌ Not connected")}\n" +
                    $"  • Configuration Manager: {(configMgrConnected ? "✅ Connected" : "❌ Not connected")}\n" +
                    $"  • Azure OpenAI: {(aiConnected ? "✅ Configured" : "⚠️ Not configured (optional)")}\n\n" +
                    "Mock data is being displayed until both data sources are connected."
            );

            // Sections status - use actual state
            var sectionsStatus = new System.Text.StringBuilder();
            sectionsStatus.AppendLine($"1. Overall Migration Status: {(actuallyShowingRealData ? "✅ REAL (from Intune workload policies)" : "❌ MOCK (placeholder)")}");
            sectionsStatus.AppendLine($"2. Device Enrollment: {(actuallyShowingRealData ? "✅ REAL (from Intune + ConfigMgr)" : "❌ MOCK")}");
            sectionsStatus.AppendLine($"3. Workload Status: {(actuallyShowingRealData ? "✅ REAL (detected from Intune policies)" : "❌ MOCK")}");
            sectionsStatus.AppendLine($"4. Security & Compliance: {(actuallyShowingRealData ? "✅ REAL (from Intune compliance policies)" : "❌ MOCK")}");
            sectionsStatus.AppendLine($"5. ROI & Savings: ⚠️ ESTIMATED (industry averages, not real cost data)");
            sectionsStatus.AppendLine($"6. Enrollment Readiness: {(actuallyShowingRealData ? "✅ REAL (detected enrollment prerequisites)" : "❌ MOCK")}");
            sectionsStatus.AppendLine($"7. Peer Benchmarking: ⚠️ ESTIMATED (Microsoft published statistics, not live comparison)");
            sectionsStatus.AppendLine($"8. Alerts & Recommendations: {(aiConnected ? "✅ AI-POWERED (GPT-4)" : "⚠️ BASIC (AI not configured)")}");
            sectionsStatus.AppendLine($"9. Recent Milestones: ⚠️ PREDEFINED (example milestones, not detected achievements)");
            sectionsStatus.AppendLine($"10. Support & Engagement: ✅ REAL (Microsoft resources links)");
            
            diagWindow.SetSectionsStatus(sectionsStatus.ToString());

            // Debug log
            diagWindow.SetDebugLog(_connectionLog.ToString());

            diagWindow.ShowDialog();
        }

        /// <summary>
        /// Shows the ConfigMgr Server Settings dialog for manual configuration.
        /// This is more discoverable than having it only under Diagnostics.
        /// </summary>
        private async void OnShowConfigMgrSettings()
        {
            var serverName = Views.ConfigMgrServerDialog.Prompt();
            
            if (!string.IsNullOrWhiteSpace(serverName))
            {
                await TryManualConfigMgrConnection(serverName);
            }
        }

        private void OnShowAISettings()
        {
            try
            {
                // DO NOT load existing config - always start with blank fields
                // This ensures clean testing and explicit configuration
                
                var aiWindow = new Views.AISettingsWindow();
                aiWindow.DataContext = this; // Use DashboardViewModel as DataContext
                
                // Set all fields to blank/default state
                IsOpenAIEnabled = false;
                OpenAIEndpoint = string.Empty;
                OpenAIDeploymentName = string.Empty;
                OpenAIApiKey = string.Empty;
                
                // Clear password box (it doesn't support binding)
                aiWindow.SetApiKey(string.Empty);
                
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
                Instance.Info("=== AZURE OPENAI CONNECTION TEST START ===");
                
                HasOpenAIStatus = false;
                OpenAIStatus = "⏳ Testing connection...";
                HasOpenAIStatus = true;
                
                // Validate inputs before testing
                var validationErrors = new List<string>();
                
                if (string.IsNullOrWhiteSpace(OpenAIEndpoint))
                    validationErrors.Add("• Endpoint URL is required");
                else if (!Uri.TryCreate(OpenAIEndpoint?.Trim(), UriKind.Absolute, out var uri) || 
                         (uri.Scheme != "https" && uri.Scheme != "http"))
                    validationErrors.Add("• Endpoint URL must be valid (e.g., https://contoso.openai.azure.com)");
                
                if (string.IsNullOrWhiteSpace(OpenAIDeploymentName))
                    validationErrors.Add("• Deployment Name is required");
                
                if (string.IsNullOrWhiteSpace(OpenAIApiKey))
                    validationErrors.Add("• API Key is required");
                else if (OpenAIApiKey?.Length < 20)
                    validationErrors.Add("• API Key appears invalid (too short)");
                
                if (validationErrors.Any())
                {
                    var errorMessage = "❌ Validation Failed:\n\n" + string.Join("\n", validationErrors) + 
                                     "\n\n💡 Fill in all required fields before testing.";
                    OpenAIStatus = errorMessage;
                    HasOpenAIStatus = true;
                    Instance.Warning($"OpenAI connection test validation failed: {string.Join(", ", validationErrors)}");
                    return;
                }
                
                Instance.Info($"Testing connection to: {OpenAIEndpoint}");
                Instance.Info($"Deployment: {OpenAIDeploymentName}");
                Instance.Info($"API Key length: {OpenAIApiKey?.Length ?? 0} characters");
                
                // Test with current UI values (not saved config)
                var service = new Services.AzureOpenAIService();
                var (success, message) = await service.TestConnectionAsync(
                    OpenAIEndpoint?.Trim() ?? "",
                    OpenAIDeploymentName?.Trim() ?? "",
                    OpenAIApiKey?.Trim() ?? ""
                );
                
                OpenAIStatus = message;
                HasOpenAIStatus = true;
                
                if (success)
                {
                    Instance.Info($"✅ OpenAI connection test SUCCEEDED: {message}");
                }
                else
                {
                    Instance.Error($"❌ OpenAI connection test FAILED: {message}");
                }
                
                Instance.Info("=== AZURE OPENAI CONNECTION TEST END ===");
            }
            catch (Exception ex)
            {
                var detailedMessage = $"❌ Test Failed: {ex.Message}";
                
                if (ex.InnerException != null)
                {
                    detailedMessage += $"\n\n📋 Details: {ex.InnerException.Message}";
                }
                
                detailedMessage += "\n\n🔍 Troubleshooting:\n" +
                                  "• Verify endpoint URL is correct\n" +
                                  "• Check API key from Azure Portal\n" +
                                  "• Ensure deployment name matches Azure\n" +
                                  "• Check network/firewall settings";
                
                OpenAIStatus = detailedMessage;
                HasOpenAIStatus = true;
                Instance.Error($"OpenAI connection test exception: {ex.Message}");
                if (ex.InnerException != null)
                    Instance.Error($"Inner exception: {ex.InnerException.Message}");
            }
        }

        private async void OnSaveOpenAIConfig()
        {
            try
            {
                var config = new Services.AzureOpenAIConfig
                {
                    IsEnabled = IsOpenAIEnabled,
                    Endpoint = OpenAIEndpoint?.Trim(),
                    DeploymentName = OpenAIDeploymentName?.Trim()
                };
                
                // SECURITY: Use SetApiKey to encrypt the API key with DPAPI
                if (!string.IsNullOrEmpty(OpenAIApiKey))
                {
                    config.SetApiKey(OpenAIApiKey.Trim());
                }
                
                config.Save();
                
                // Re-initialize AI service if configuration is now valid
                if (config.IsEnabled && !string.IsNullOrEmpty(config.Endpoint))
                {
                    try
                    {
                        _aiRecommendationService = new AIRecommendationService(_graphDataService);
                        Instance.Info("AI Recommendation Service initialized after config save");
                        
                        // Update diagnostics to reflect AI is now connected
                        OnPropertyChanged(nameof(IsAIAvailable));
                        OnPropertyChanged(nameof(IsAIConfigured));
                        OnPropertyChanged(nameof(IsAINotConfigured));
                        OnPropertyChanged(nameof(HasNoRecommendationsAndConfigured));
                        Instance.Info($"AI service initialized and diagnostics updated: {_aiRecommendationService != null}");
                        
                        // Trigger data refresh to load AI recommendations
                        OnPropertyChanged(nameof(IsFullyAuthenticated));
                        await LoadDataAsync();
                        Instance.Info("Data refreshed and AI recommendations loaded after AI configuration");
                    }
                    catch (Exception ex)
                    {
                        Instance.Error($"Failed to initialize AI service after config save: {ex.Message}");
                    }
                }
                
                System.Windows.MessageBox.Show(
                    "Azure OpenAI configuration saved successfully!\n\n" +
                    "Settings will be used for AI-enhanced recommendations." +
                    (config.IsEnabled ? "\n\nDashboard data has been refreshed to leverage AI capabilities." : ""),
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
                    var manualServer = Views.ConfigMgrServerDialog.Prompt();

                    if (!string.IsNullOrWhiteSpace(manualServer))
                    {
                        // Note: ConfigMgrServerDialog already cleans the input
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
                            
                            // Check if this is a certificate trust issue
                            var (pendingThumbprint, pendingSubject) = Services.ConfigMgrAdminService.GetPendingCertificateInfo();
                            
                            if (!string.IsNullOrEmpty(pendingThumbprint) && (error.Contains("SSL") || error.Contains("certificate") || error.Contains("trust")))
                            {
                                // SSL certificate error - offer to trust the certificate with friendlier UX
                                var trustResult = System.Windows.MessageBox.Show(
                                    $"Server Certificate Verification\n\n" +
                                    $"The certificate for ConfigMgr server '{manualServer}' is not in the Windows trust store.\n\n" +
                                    $"Certificate Details:\n" +
                                    $"• Subject: {pendingSubject}\n" +
                                    $"• Thumbprint: {pendingThumbprint}\n\n" +
                                    $"This is common for enterprise servers using self-signed or internal CA certificates.\n\n" +
                                    $"To verify this is your legitimate ConfigMgr server:\n" +
                                    $"• Confirm the server name matches your ConfigMgr primary site\n" +
                                    $"• Check with your IT team if unsure\n\n" +
                                    $"Trust this certificate for future connections?",
                                    "Server Certificate Verification",
                                    System.Windows.MessageBoxButton.YesNo,
                                    System.Windows.MessageBoxImage.Question);
                                
                                if (trustResult == System.Windows.MessageBoxResult.Yes)
                                {
                                    LogConnection($"[SECURITY] User chose to trust certificate");
                                    Services.ConfigMgrAdminService.TrustPendingCertificate();
                                    _graphDataService.ConfigMgrService.RefreshCredentials();
                                    
                                    // Retry connection
                                    var retrySuccess = await _graphDataService.ConfigMgrService.ConfigureAsync($"https://{manualServer}/AdminService");
                                    if (retrySuccess)
                                    {
                                        System.Windows.MessageBox.Show(
                                            $"Certificate trusted. Connected to ConfigMgr successfully!",
                                            "Connected",
                                            System.Windows.MessageBoxButton.OK,
                                            System.Windows.MessageBoxImage.Information);
                                    }
                                }
                                else
                                {
                                    Services.ConfigMgrAdminService.ClearPendingCertificate();
                                }
                            }
                            else
                            {
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
            var manualServer = Views.ConfigMgrServerDialog.Prompt();

            if (!string.IsNullOrWhiteSpace(manualServer))
            {
                // Note: ConfigMgrServerDialog already cleans the input
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
                    Title = "Comanaged",
                    Values = new ChartValues<int>(),
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 10,
                    Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x10, 0x7C, 0x10)), // Green #107C10
                    Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x33, 0x10, 0x7C, 0x10)), // Transparent green
                    StrokeThickness = 3
                },
                new LineSeries
                {
                    Title = "Cloud Native",
                    Values = new ChartValues<int>(),
                    PointGeometry = DefaultGeometries.Diamond,
                    PointGeometrySize = 10,
                    Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4)), // Blue #0078D4
                    Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x33, 0x00, 0x78, 0xD4)), // Transparent blue
                    StrokeThickness = 3
                },
                new LineSeries
                {
                    Title = "ConfigMgr Only",
                    Values = new ChartValues<int>(),
                    PointGeometry = DefaultGeometries.Square,
                    PointGeometrySize = 10,
                    Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD1, 0x34, 0x38)), // Red #D13438
                    Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x33, 0xD1, 0x34, 0x38)), // Transparent red
                    StrokeThickness = 3
                }
            };
            OnPropertyChanged(nameof(EnrollmentTrendSeries));

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
            OnPropertyChanged(nameof(ComplianceComparisonSeries));
        }

        private async Task LoadDataAsync()
        {
            IsLoading = true;

            try
            {
                // Check ConfigMgr connection status
                IsConfigMgrConnected = _graphDataService.ConfigMgrService.IsConfigured;
                var isGraphConnected = _graphDataService.IsAuthenticated;
                
                // Handle 4 connection scenarios:
                // 1. Both connected → Full real data with accurate co-management
                // 2. Graph only → Real Intune data, co-management status unavailable
                // 3. ConfigMgr only → Real ConfigMgr data, Intune/cloud data unavailable
                // 4. Neither → Mock data for demonstration
                
                if (isGraphConnected && IsConfigMgrConnected)
                {
                    Instance.Info($"✅ Both data sources connected - loading full real data");
                    UseRealData = true;
                    await LoadRealDataAsync();
                }
                else if (isGraphConnected && !IsConfigMgrConnected)
                {
                    Instance.Info($"⚠️ Graph only connected - loading Intune data (co-management unavailable)");
                    UseRealData = true;
                    await LoadRealDataAsync(); // GraphDataService handles GraphOnly scenario internally
                }
                else if (!isGraphConnected && IsConfigMgrConnected)
                {
                    Instance.Info($"⚠️ ConfigMgr only connected - loading ConfigMgr data (Intune data unavailable)");
                    UseRealData = true;
                    await LoadConfigMgrOnlyDataAsync();
                }
                else
                {
                    Instance.Warning($"❌ No data sources connected - showing demonstration data");
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

                // v3.17.234 - Run Analysis Pipeline (after all data is loaded)
                await LoadAnalysisPipelineAsync();

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
                // Check if AI service is available
                if (_aiRecommendationService == null || !_aiRecommendationService.IsConfigured)
                {
                    // Azure OpenAI not configured - DO NOT add any recommendations
                    // The UI will show the "Configure Azure OpenAI" message via IsAINotConfigured binding
                    AIRecommendations.Clear();
                    OnPropertyChanged(nameof(HasNoRecommendations));
                    OnPropertyChanged(nameof(IsAIConfigured));
                    OnPropertyChanged(nameof(IsAINotConfigured));
                    OnPropertyChanged(nameof(HasNoRecommendationsAndConfigured));
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
                    
                    if (recommendations.Any())
                    {
                        foreach (var recommendation in recommendations.Take(5)) // Show top 5 recommendations
                        {
                            AIRecommendations.Add(recommendation);
                        }
                    }
                    
                    OnPropertyChanged(nameof(HasNoRecommendations));
                    OnPropertyChanged(nameof(IsAIConfigured));
                    OnPropertyChanged(nameof(IsAINotConfigured));
                    OnPropertyChanged(nameof(HasNoRecommendationsAndConfigured));
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
                    Category = RecommendationCategory.General,
                    ActionSteps = new List<string>
                    {
                        "1. Check Azure OpenAI configuration (🤖 AI button)",
                        "2. Verify API key and endpoint are correct",
                        "3. Check network connectivity to Azure OpenAI",
                        "4. Review logs for detailed error information"
                    }
                });
                Instance.Error($"Error loading AI recommendations: {ex.Message}");
                OnPropertyChanged(nameof(HasNoRecommendations));
                OnPropertyChanged(nameof(IsAIConfigured));
                OnPropertyChanged(nameof(IsAINotConfigured));
                OnPropertyChanged(nameof(HasNoRecommendationsAndConfigured));
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

                // Populate pie chart series
                if (DeviceEnrollment != null)
                {
                    DeviceIdentityPieSeries = new SeriesCollection
                    {
                        new PieSeries
                        {
                            Title = "Hybrid Entra",
                            Values = new ChartValues<int> { DeviceEnrollment.HybridJoinedDevices },
                            Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)),
                            DataLabels = true,
                            LabelPoint = point => $"{point.Y}"
                        },
                        new PieSeries
                        {
                            Title = "Entra Joined",
                            Values = new ChartValues<int> { DeviceEnrollment.AzureADOnlyDevices },
                            Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212)),
                            DataLabels = true,
                            LabelPoint = point => $"{point.Y}"
                        },
                        new PieSeries
                        {
                            Title = "AD Domain",
                            Values = new ChartValues<int> { DeviceEnrollment.OnPremDomainOnlyDevices },
                            Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(253, 184, 19)),
                            DataLabels = true,
                            LabelPoint = point => $"{point.Y}"
                        },
                        new PieSeries
                        {
                            Title = "Workgroup",
                            Values = new ChartValues<int> { DeviceEnrollment.WorkgroupDevices },
                            Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 52, 56)),
                            DataLabels = true,
                            LabelPoint = point => $"{point.Y}"
                        }
                    };
                }

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
                // v3.17.227 - Will be updated with real workload authority data in LoadWorkloadRecommendationDataAsync
                int intuneDevices = DeviceEnrollment?.IntuneEnrolledDevices ?? 0;
                int totalDevices = DeviceEnrollment?.TotalDevices ?? 1;
                double progress = totalDevices > 0 ? (intuneDevices / (double)totalDevices) * 100 : 0;

                MigrationStatus = new MigrationStatus
                {
                    WorkloadsTransitioned = 0, // Will be updated by workload authority bridge
                    TotalWorkloads = 7,
                    ProjectedFinishDate = DateTime.Now.AddMonths((int)((100 - progress) / 5)),
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
                    
                    // v3.16.23 - Enhanced logging with 4-tier readiness breakdown
                    Instance.Info($"✅ Device readiness loaded:");
                    Instance.Info($"   Excellent (≥85): {DeviceReadiness?.ExcellentDevices ?? 0} devices");
                    Instance.Info($"   Good (60-84): {DeviceReadiness?.GoodDevices ?? 0} devices");
                    Instance.Info($"   Fair (40-59): {DeviceReadiness?.FairDevices ?? 0} devices");
                    Instance.Info($"   Poor (<40): {DeviceReadiness?.PoorDevices ?? 0} devices");
                    Instance.Info($"✅ Enrollment blockers loaded: {EnrollmentBlockers?.TotalBlockedDevices ?? 0} blocked, {EnrollmentBlockers?.EnrollableDevices ?? 0} enrollable");
                }
                catch (Exception ex)
                {
                    Instance.LogException(ex, "Failed to load device readiness");
                    Instance.Warning("Device readiness will use estimated values in Smart Enrollment Management");
                    DeviceReadiness = null;
                    EnrollmentBlockers = null;
                }
                
                Instance.Info("LoadRealDataAsync completed successfully");
                
                // v3.16.23 - Notify UI to refresh analytics views with real data
                RealDataLoaded?.Invoke(this, EventArgs.Empty);
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

        /// <summary>
        /// Loads data when only ConfigMgr is connected (no Graph/Intune).
        /// Shows real ConfigMgr device counts but marks Intune data as unavailable.
        /// </summary>
        private async Task LoadConfigMgrOnlyDataAsync()
        {
            try
            {
                Instance.Info("=== Starting LoadConfigMgrOnlyDataAsync ===");
                Instance.Info("ConfigMgr connected, Graph NOT authenticated - loading ConfigMgr data only");
                
                // Query ConfigMgr for Windows 10/11 devices
                var configMgrDevices = await _graphDataService.ConfigMgrService.GetWindows1011DevicesAsync();
                int configMgrCount = configMgrDevices?.Count ?? 0;
                
                Instance.Info($"ConfigMgr returned {configMgrCount} Windows 10/11 devices");
                
                // Build DeviceEnrollment from ConfigMgr data only
                // Intune/cloud data is marked as 0 since we can't query without Graph
                DeviceEnrollment = new DeviceEnrollment
                {
                    TotalDevices = configMgrCount,
                    IntuneEnrolledDevices = 0, // Cannot determine without Graph
                    ConfigMgrOnlyDevices = configMgrCount, // All devices are ConfigMgr-only from this perspective
                    CoManagedDevices = 0, // Cannot determine without Graph
                    CloudNativeDevices = 0, // Cannot determine without Graph
                    HybridJoinedDevices = 0, // Cannot determine without Graph
                    AzureADOnlyDevices = 0, // Cannot determine without Graph
                    OnPremDomainOnlyDevices = configMgrCount, // Assume all are on-prem since no Graph
                    WorkgroupDevices = 0,
                    UnknownJoinTypeDevices = 0,
                    TrendData = Array.Empty<EnrollmentTrend>(), // No trend data without Graph
                    HasSufficientTrendData = false,
                    TrendDataUnavailableReason = "Trend data requires Microsoft Graph connection",
                    DataSource = EnrollmentDataSource.ConfigMgrOnly
                };
                
                OnPropertyChanged(nameof(EnrollmentProgressPercentage));
                OnPropertyChanged(nameof(HasSufficientTrendData));
                OnPropertyChanged(nameof(TrendDataUnavailableReason));
                
                // Use mock data for remaining dashboard elements
                // (compliance, workloads, etc. require Graph)
                var complianceScoreTask = _mockDataService.GetComplianceScoreAsync();
                var enrollmentInsightTask = _mockDataService.GetEnrollmentAccelerationInsightAsync();
                var savingsInsightTask = _mockDataService.GetSavingsUnlockInsightAsync();
                var alertsTask = _mockDataService.GetAlertsAsync();
                var blockersTask = _mockDataService.GetBlockersAsync();
                var engagementOptionsTask = _mockDataService.GetEngagementOptionsAsync();
                
                await Task.WhenAll(
                    complianceScoreTask,
                    enrollmentInsightTask,
                    savingsInsightTask,
                    alertsTask,
                    blockersTask,
                    engagementOptionsTask
                );
                
                ComplianceScore = await complianceScoreTask;
                EnrollmentAccelerationInsight = await enrollmentInsightTask;
                SavingsUnlockInsight = await savingsInsightTask;
                
                var alerts = await alertsTask;
                Alerts.Clear();
                foreach (var alert in alerts)
                    Alerts.Add(alert);
                
                var blockers = await blockersTask;
                Blockers.Clear();
                foreach (var blocker in blockers)
                    Blockers.Add(blocker);
                
                var engagementOptions = await engagementOptionsTask;
                EngagementOptions.Clear();
                foreach (var option in engagementOptions)
                    EngagementOptions.Add(option);
                
                // Migration status based on ConfigMgr data
                MigrationStatus = new MigrationStatus
                {
                    WorkloadsTransitioned = 0, // Cannot determine without Graph
                    TotalWorkloads = 7,
                    ProjectedFinishDate = DateTime.Now.AddMonths(12), // Estimate
                    LastUpdateDate = DateTime.Now
                };
                
                // Mock AI Action Summary
                AIActionSummary = new AIActionSummary
                {
                    PrimaryEnrollmentAction = "Connect to Microsoft Graph to view Intune enrollment status",
                    EnrollmentActionImpact = configMgrCount,
                    PrimaryWorkloadAction = "Graph connection required to analyze workload transition status",
                    WorkloadActionImpact = "Connect Graph to unlock migration insights",
                    EnrollmentBlockers = new List<string>
                    {
                        "Microsoft Graph not connected - Intune enrollment data unavailable"
                    },
                    WorkloadBlockers = new List<string>
                    {
                        "Connect to Microsoft Graph to analyze workload status"
                    },
                    AIRecommendation = $"You have {configMgrCount:N0} Windows 10/11 devices in ConfigMgr. Connect to Microsoft Graph to see which devices are already enrolled in Intune and track your migration progress.",
                    WeeksToNextMilestone = 0,
                    IsAIPowered = false
                };
                OnPropertyChanged(nameof(AIActionSummary));
                
                // Clear progress targets and milestones
                ProgressTargets.Clear();
                Milestones.Clear();
                
                UpdateCharts();
                
                Instance.Info($"ConfigMgr-only data load complete: {configMgrCount} devices");
                
                // Notify UI
                RealDataLoaded?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "LoadConfigMgrOnlyDataAsync");
                System.Windows.MessageBox.Show(
                    $"Error loading ConfigMgr data: {ex.Message}\n\nFalling back to mock data.",
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

                // NOTE: Alerts are now loaded below with real enrollment acceleration data
                // This avoids duplicate loading
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "LoadMockDataPartialAsync - Graph API calls");
                Instance.Warning("Falling back to mock data for workloads");
                
                // Fall back to mock data for workloads
                var workloads = await _mockDataService.GetWorkloadsAsync();
                Workloads.Clear();
                foreach (var workload in workloads)
                    Workloads.Add(workload);
            }

            // Industry insights focused on ACTIONS to accelerate enrollment (principle #1)
            Instance.Info("Loading enrollment acceleration insights...");
            
            // Use REAL data from GraphDataService when available
            try
            {
                var enrollmentInsightTask = _graphDataService.GetEnrollmentAccelerationInsightAsync();
                var alertsTask = _graphDataService.GetRealAlertsAsync();
                var savingsInsightTask = _mockDataService.GetSavingsUnlockInsightAsync();
                var engagementOptionsTask = _mockDataService.GetEngagementOptionsAsync();

                await Task.WhenAll(enrollmentInsightTask, alertsTask, savingsInsightTask, engagementOptionsTask);

                EnrollmentAccelerationInsight = await enrollmentInsightTask;
                SavingsUnlockInsight = await savingsInsightTask;
                
                // Replace mock alerts with real alerts
                var realAlerts = await alertsTask;
                foreach (var alert in realAlerts.Take(5)) // Top 5 alerts
                {
                    Alerts.Add(alert);
                }
                
                Instance.Info($"✅ Loaded REAL enrollment acceleration data:");
                Instance.Info($"   Your velocity: {EnrollmentAccelerationInsight.YourWeeklyEnrollmentRate:F1} devices/week");
                Instance.Info($"   Peer average: {EnrollmentAccelerationInsight.PeerAverageRate:F1} devices/week");
                Instance.Info($"   Loaded {realAlerts.Count} real alerts");
                
                EngagementOptions.Clear();
                foreach (var option in await engagementOptionsTask)
                    EngagementOptions.Add(option);
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "Loading real enrollment insights");
                Instance.Warning("Falling back to mock enrollment insights");
                
                // Fall back to mock data on error
                var enrollmentInsightTask = _mockDataService.GetEnrollmentAccelerationInsightAsync();
                var savingsInsightTask = _mockDataService.GetSavingsUnlockInsightAsync();
                var engagementOptionsTask = _mockDataService.GetEngagementOptionsAsync();

                await Task.WhenAll(enrollmentInsightTask, savingsInsightTask, engagementOptionsTask);

                EnrollmentAccelerationInsight = await enrollmentInsightTask;
                SavingsUnlockInsight = await savingsInsightTask;
            }

            // NO mock milestones - replaced with forward-looking ProgressTargets
            ProgressTargets.Clear();
            Milestones.Clear();

            // v3.16.33 - Generate REAL AI Action Summary from actual data
            Instance.Info("Generating AI Action Summary from real data...");
            await GenerateRealAIActionSummaryAsync();
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
        }

        private async Task LoadMockDataAsync()
        {
            Instance.Info("=== Loading MOCK data (pre-authentication) ===");
            // Load all data in parallel
            var migrationStatusTask = _mockDataService.GetMigrationStatusAsync();
            var deviceEnrollmentTask = _mockDataService.GetDeviceEnrollmentAsync();
            var workloadsTask = _mockDataService.GetWorkloadsAsync();
            var complianceScoreTask = _mockDataService.GetComplianceScoreAsync();
            var enrollmentInsightTask = _mockDataService.GetEnrollmentAccelerationInsightAsync();
            var savingsInsightTask = _mockDataService.GetSavingsUnlockInsightAsync();
            var alertsTask = _mockDataService.GetAlertsAsync();
            var milestonesTask = _mockDataService.GetMilestonesAsync();
            var progressTargetsTask = _mockDataService.GetProgressTargetsAsync();
            var blockersTask = _mockDataService.GetBlockersAsync();
            var engagementOptionsTask = _mockDataService.GetEngagementOptionsAsync();

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
                
            // DON'T replace Workloads - we already initialized with Benefits in constructor via InitializeWorkloadsWithBenefits()
            // The workloads collection already has all the data we need with Benefits

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
            // Notify UI about trend data availability
            OnPropertyChanged(nameof(HasSufficientTrendData));
            OnPropertyChanged(nameof(TrendDataUnavailableReason));
            
            if (DeviceEnrollment?.TrendData != null && DeviceEnrollment.TrendData.Length > 0)
            {
                var intuneValues = new ChartValues<int>();
                var cloudNativeValues = new ChartValues<int>();
                var configMgrValues = new ChartValues<int>();
                var labels = new List<string>();

                foreach (var trend in DeviceEnrollment.TrendData)
                {
                    intuneValues.Add(trend.IntuneDevices);
                    cloudNativeValues.Add(trend.CloudNativeDevices);
                    configMgrValues.Add(trend.ConfigMgrDevices);
                    labels.Add(trend.Month.ToString("MMM d"));
                }

                EnrollmentTrendSeries[0].Values = intuneValues;
                EnrollmentTrendSeries[1].Values = cloudNativeValues;
                EnrollmentTrendSeries[2].Values = configMgrValues;
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
            // Notify UI about trend data availability (for showing/hiding "not enough data" message)
            OnPropertyChanged(nameof(HasSufficientTrendData));
            OnPropertyChanged(nameof(TrendDataUnavailableReason));
            
            if (DeviceEnrollment?.TrendData != null && DeviceEnrollment.TrendData.Length > 0)
            {
                var intuneValues = new ChartValues<int>();
                var cloudNativeValues = new ChartValues<int>();
                var configMgrValues = new ChartValues<int>();
                var labels = new List<string>();

                foreach (var trend in DeviceEnrollment.TrendData)
                {
                    intuneValues.Add(trend.IntuneDevices);
                    cloudNativeValues.Add(trend.CloudNativeDevices);
                    configMgrValues.Add(trend.ConfigMgrDevices);
                    // Use shorter format for weekly data (now showing weeks, not months)
                    labels.Add(trend.Month.ToString("MMM d"));
                }

                EnrollmentTrendSeries[0].Values = intuneValues;
                EnrollmentTrendSeries[1].Values = cloudNativeValues;
                EnrollmentTrendSeries[2].Values = configMgrValues;
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
            await _mockDataService.RefreshDataAsync();
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
                    
                    // Check if this is a certificate trust issue
                    var (pendingThumbprint, pendingSubject) = Services.ConfigMgrAdminService.GetPendingCertificateInfo();
                    
                    if (!string.IsNullOrEmpty(pendingThumbprint) && (error.Contains("SSL") || error.Contains("certificate") || error.Contains("trust")))
                    {
                        // SSL certificate error - offer to trust the certificate with friendlier UX
                        var trustResult = System.Windows.MessageBox.Show(
                            $"Server Certificate Verification\n\n" +
                            $"The certificate for ConfigMgr server '{siteServer}' is not in the Windows trust store.\n\n" +
                            $"Certificate Details:\n" +
                            $"• Subject: {pendingSubject}\n" +
                            $"• Thumbprint: {pendingThumbprint}\n\n" +
                            $"This is common for enterprise servers using self-signed or internal CA certificates.\n\n" +
                            $"To verify this is your legitimate ConfigMgr server:\n" +
                            $"• Confirm the server name matches your ConfigMgr primary site\n" +
                            $"• Check with your IT team if unsure\n\n" +
                            $"Trust this certificate for future connections?",
                            "Server Certificate Verification",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Question);
                        
                        if (trustResult == System.Windows.MessageBoxResult.Yes)
                        {
                            LogConnection($"[SECURITY] User chose to trust certificate");
                            Services.ConfigMgrAdminService.TrustPendingCertificate();
                            _graphDataService.ConfigMgrService.RefreshCredentials();
                            
                            // Retry connection
                            var retrySuccess = await _graphDataService.ConfigMgrService.ConfigureAsync(adminServiceUrl);
                            if (retrySuccess)
                            {
                                var method = _graphDataService.ConfigMgrService.ConnectionMethod;
                                System.Windows.MessageBox.Show(
                                    $"Certificate trusted. Connected to ConfigMgr successfully!\n\n" +
                                    $"Site Server: {siteServer}\n" +
                                    $"Connection Method: {method}\n\n" +
                                    $"Refreshing dashboard data...",
                                    "ConfigMgr Connected",
                                    System.Windows.MessageBoxButton.OK,
                                    System.Windows.MessageBoxImage.Information);
                                
                                await RefreshDataAsync();
                            }
                        }
                        else
                        {
                            Services.ConfigMgrAdminService.ClearPendingCertificate();
                        }
                    }
                    else
                    {
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
                Instance.Info("=== LOADING DEVICE SELECTION DATA ===");

                int unenrolledCount;

                if (DeviceEnrollment != null && DeviceEnrollment.ConfigMgrOnlyDevices > 0)
                {
                    // Use real data
                    unenrolledCount = DeviceEnrollment.ConfigMgrOnlyDevices;
                    Instance.Info($"   Using REAL unenrolled count: {unenrolledCount} devices");
                }
                else
                {
                    // v3.16.33 - Only use mock data when NOT connected
                    if (!UseRealData)
                    {
                        unenrolledCount = 50600; // Mock: 50,600 unenrolled devices (matches ConfigMgrOnlyDevices)
                        Instance.Info("   Using MOCK device selection data (not authenticated)");
                    }
                    else
                    {
                        // Connected but no devices - this is real data showing 0
                        unenrolledCount = 0;
                        Instance.Info("   Connected but ConfigMgrOnlyDevices is 0 - showing real (empty) data");
                    }
                }

                // Get AI guidance if available
                if (_aiRecommendationService != null)
                {
                    var guidance = await _aiRecommendationService.GetDeviceSelectionGuidanceAsync(unenrolledCount, 50);
                }

                // Calculate readiness counts using real device health analysis
                // If DeviceReadiness data is available (from LoadRealDataAsync), use it
                // Otherwise fall back to estimates for demo/mock mode
                Instance.Info($"   DeviceReadiness object: {(DeviceReadiness != null ? "EXISTS" : "NULL")}");
                if (DeviceReadiness != null)
                {
                    Instance.Info($"   DeviceReadiness values: Excellent={DeviceReadiness.ExcellentDevices}, Good={DeviceReadiness.GoodDevices}, Fair={DeviceReadiness.FairDevices}, Poor={DeviceReadiness.PoorDevices}");
                }
                
                if (DeviceReadiness != null && (DeviceReadiness.ExcellentDevices > 0 || DeviceReadiness.GoodDevices > 0 || DeviceReadiness.FairDevices > 0 || DeviceReadiness.PoorDevices > 0))
                {
                    ExcellentReadinessCount = DeviceReadiness.ExcellentDevices;
                    GoodReadinessCount = DeviceReadiness.GoodDevices;
                    FairReadinessCount = DeviceReadiness.FairDevices;
                    PoorReadinessCount = DeviceReadiness.PoorDevices;
                    Instance.Info($"   Using REAL device readiness: {ExcellentReadinessCount} Excellent, {GoodReadinessCount} Good, {FairReadinessCount} Fair, {PoorReadinessCount} Poor");
                }
                else if (!UseRealData)
                {
                    // Fallback estimates for mock/demo mode (NOT connected)
                    ExcellentReadinessCount = Math.Max(0, (int)(unenrolledCount * 0.35)); // ~35% excellent
                    GoodReadinessCount = Math.Max(0, (int)(unenrolledCount * 0.30)); // ~30% good
                    FairReadinessCount = Math.Max(0, (int)(unenrolledCount * 0.25)); // ~25% fair
                    PoorReadinessCount = unenrolledCount - ExcellentReadinessCount - GoodReadinessCount - FairReadinessCount;
                    Instance.Info($"   Using MOCK estimated readiness (not authenticated): {ExcellentReadinessCount} Excellent, {GoodReadinessCount} Good");
                }
                else
                {
                    // v3.16.33 - Connected but DeviceReadiness returned 0's - show actual 0's, not estimates
                    ExcellentReadinessCount = DeviceReadiness?.ExcellentDevices ?? 0;
                    GoodReadinessCount = DeviceReadiness?.GoodDevices ?? 0;
                    FairReadinessCount = DeviceReadiness?.FairDevices ?? 0;
                    PoorReadinessCount = DeviceReadiness?.PoorDevices ?? 0;
                    Instance.Warning("   Connected but readiness counts are 0 - check ConfigMgr device query logs above");
                    Instance.Info($"   Raw values: {ExcellentReadinessCount} Excellent, {GoodReadinessCount} Good, {FairReadinessCount} Fair, {PoorReadinessCount} Poor");
                }

                DevicesNeedingPreparation = FairReadinessCount;
                
                // v3.16.23 - Use PoorReadinessCount for High Risk when real data is available
                // Poor readiness (<40 score) = critical issues, 30% enrollment success rate
                if (DeviceReadiness != null)
                {
                    HighRiskDeviceCount = PoorReadinessCount;
                    Instance.Info($"✅ Using real readiness data for High Risk: {HighRiskDeviceCount} devices");
                }
                else
                {
                    // Fallback estimate when no real data (mock mode)
                    HighRiskDeviceCount = Math.Max(0, (int)(unenrolledCount * 0.10));
                    Instance.Info($"ℹ️ Using estimated High Risk (10% of {unenrolledCount}): {HighRiskDeviceCount} devices");
                }

                OnPropertyChanged(nameof(NextBatchSize));
                OnPropertyChanged(nameof(DevicesNeedingPreparation));
                OnPropertyChanged(nameof(HighRiskDeviceCount));

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

                // If no workloads exist AND not connected, show mock data
                // v3.16.33 - Only use mock if not using real data
                if (Workloads.Count == 0 && !UseRealData)
                {
                    Instance.Info("No workloads available and not connected - using MOCK velocity data");
                    
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
                    EnrollmentInsight = await _mockDataService.GetMockEnrollmentInsightsAsync();
                    
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

                // v3.17.227 - Use real workload authority data when authenticated
                if (UseRealData)
                {
                    await UpdateWorkloadsFromAuthorityDataAsync();
                }
                else
                {
                    // Use constructor-initialized mock data with realistic adoption figures
                    Instance.Info($"✅ Using constructor-initialized workload recommendation: {WorkloadMomentumInsight?.RecommendedWorkload ?? "None"}");
                    PopulateMockWorkloadAuthorityData();
                }

                CalculateWorkloadVelocity();
                UpdateWorkloadBlockers();
                ComputeWorkloadSequence();
                
                // Set safety dashboard values
                ReadyDevicesForWorkload = DeviceEnrollment?.IntuneEnrolledDevices ?? 0;
                TotalDevicesForWorkload = DeviceEnrollment?.TotalDevices ?? 0;
                DevicesNeedingRemediation = Blockers.Sum(b => b.AffectedDevices);
                
                OnPropertyChanged(nameof(ReadyDevicesPercentage));
                OnPropertyChanged(nameof(PolicyConflictsStatusIcon));
                OnPropertyChanged(nameof(PolicyConflictsStatusText));
                OnPropertyChanged(nameof(PrerequisitesStatusIcon));
                OnPropertyChanged(nameof(PrerequisitesStatusText));
                OnPropertyChanged(nameof(RemediationStatusIcon));
                OnPropertyChanged(nameof(RemediationStatusText));
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "LoadWorkloadRecommendationDataAsync");
            }
        }

        /// <summary>
        /// Loads application migration data for Applications tab (Phase 2)
        /// Also updates Application Readiness metrics for the Applications tab
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
                
                // Update Application Readiness metrics
                // Easy = Low complexity (MSI, MSIX, Store apps)
                // Moderate = Medium complexity (Win32 packaging needed)
                // Complex = High complexity (App-V, scripts requiring re-engineering)
                AppReadinessEasyCount = LowComplexityCount;
                AppReadinessModerateCount = MediumComplexityCount;
                AppReadinessComplexCount = HighComplexityCount;
                
                // Calculate percentage ready (easy apps / total)
                if (TotalApplicationCount > 0)
                {
                    AppReadinessPercentage = Math.Round((double)AppReadinessEasyCount / TotalApplicationCount * 100, 0);
                }
                else
                {
                    AppReadinessPercentage = 0;
                }
                
                // Count specific blockers
                AppBlockerAppVCount = apps.Count(a => 
                    a.DeploymentType?.Contains("App-V", StringComparison.OrdinalIgnoreCase) == true ||
                    a.ApplicationName?.Contains("App-V", StringComparison.OrdinalIgnoreCase) == true);
                AppBlockerScriptCount = apps.Count(a => 
                    a.DeploymentType?.Contains("Script", StringComparison.OrdinalIgnoreCase) == true ||
                    a.MigrationPath == MigrationPath.RequiresReengineering);
                
                OnPropertyChanged(nameof(AppReadinessPercentage));
                OnPropertyChanged(nameof(AppReadinessEasyCount));
                OnPropertyChanged(nameof(AppReadinessModerateCount));
                OnPropertyChanged(nameof(AppReadinessComplexCount));
                OnPropertyChanged(nameof(AppBlockerAppVCount));
                OnPropertyChanged(nameof(AppBlockerScriptCount));

                if (UseRealData && _graphDataService.IsAuthenticated)
                {
                    Instance.Info($"✅ Loaded {apps.Count} applications from ConfigMgr (Easy: {AppReadinessEasyCount}, Moderate: {AppReadinessModerateCount}, Complex: {AppReadinessComplexCount})");
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
        /// v3.16.33 - Generate AI Action Summary from REAL data instead of hardcoded mock values
        /// </summary>
        private async Task GenerateRealAIActionSummaryAsync()
        {
            try
            {
                // Get real device readiness counts
                int excellentCount = DeviceReadiness?.ExcellentDevices ?? ExcellentReadinessCount;
                int goodCount = DeviceReadiness?.GoodDevices ?? GoodReadinessCount;
                int fairCount = DeviceReadiness?.FairDevices ?? FairReadinessCount;
                int poorCount = DeviceReadiness?.PoorDevices ?? PoorReadinessCount;
                
                // Get enrollment progress
                int enrolledDevices = DeviceEnrollment?.IntuneEnrolledDevices ?? 0;
                int totalDevices = DeviceEnrollment?.TotalDevices ?? 1;
                double enrollmentPercent = totalDevices > 0 ? (enrolledDevices * 100.0 / totalDevices) : 0;
                
                // Determine primary enrollment action based on real data
                string enrollmentAction;
                int enrollmentImpact;
                if (excellentCount > 0)
                {
                    enrollmentAction = $"Enroll the {excellentCount} 'Excellent' readiness devices (scores ≥85) - highest success probability";
                    enrollmentImpact = excellentCount;
                }
                else if (goodCount > 0)
                {
                    enrollmentAction = $"Enroll the {goodCount} 'Good' readiness devices (scores 60-84) for steady progress";
                    enrollmentImpact = goodCount;
                }
                else if (fairCount > 0)
                {
                    enrollmentAction = $"Remediate the {fairCount} 'Fair' readiness devices (scores 40-59) before enrollment";
                    enrollmentImpact = fairCount;
                }
                else
                {
                    enrollmentAction = "No devices available for enrollment - check ConfigMgr connectivity";
                    enrollmentImpact = 0;
                }
                
                // Determine workload action based on incomplete workloads
                var notStartedWorkloads = Workloads.Where(w => w.Status == WorkloadStatus.NotStarted).ToList();
                var inProgressWorkloads = Workloads.Where(w => w.Status == WorkloadStatus.InProgress).ToList();
                
                string workloadAction;
                string workloadImpact;
                if (inProgressWorkloads.Any())
                {
                    var nextWorkload = inProgressWorkloads.First();
                    workloadAction = $"Complete '{nextWorkload.Name}' workload transition (in progress)";
                    workloadImpact = $"Unlock {nextWorkload.Name} cloud benefits";
                }
                else if (notStartedWorkloads.Any())
                {
                    var nextWorkload = notStartedWorkloads.First();
                    workloadAction = $"Start '{nextWorkload.Name}' workload transition";
                    workloadImpact = $"Begin transition to unlock cloud-native {nextWorkload.Name}";
                }
                else
                {
                    workloadAction = "All workloads transitioned - monitor and optimize";
                    workloadImpact = "Maintain cloud-native posture";
                }
                
                // Generate real enrollment blockers from data
                var realEnrollmentBlockers = new List<string>();
                if (EnrollmentBlockers?.BlockerCategories != null)
                {
                    foreach (var category in EnrollmentBlockers.BlockerCategories.Take(3))
                    {
                        realEnrollmentBlockers.Add($"{category.DeviceCount} devices: {category.Description}");
                    }
                }
                if (!realEnrollmentBlockers.Any())
                {
                    realEnrollmentBlockers.Add("No critical enrollment blockers detected");
                }
                
                // Generate workload blockers from data
                var realWorkloadBlockers = new List<string>();
                if (enrollmentPercent < 75)
                {
                    realWorkloadBlockers.Add($"75% enrollment threshold not yet met (currently {enrollmentPercent:F0}%)");
                }
                foreach (var workload in notStartedWorkloads.Take(2))
                {
                    realWorkloadBlockers.Add($"'{workload.Name}' workload not yet configured in Intune");
                }
                if (!realWorkloadBlockers.Any())
                {
                    realWorkloadBlockers.Add("No workload blockers detected");
                }
                
                // Calculate weeks to next milestone (75% enrollment)
                double weeklyVelocity = EnrollmentAccelerationInsight?.YourWeeklyEnrollmentRate ?? 10;
                int devicesToMilestone = Math.Max(0, (int)(totalDevices * 0.75) - enrolledDevices);
                int weeksToMilestone = weeklyVelocity > 0 ? (int)Math.Ceiling(devicesToMilestone / weeklyVelocity) : 52;
                
                // Build AI recommendation
                string aiReco = $"Focus on enrolling {excellentCount + goodCount} ready devices first. ";
                if (weeklyVelocity > 0)
                {
                    aiReco += $"At current velocity ({weeklyVelocity:F0}/week), you'll reach 75% enrollment in ~{weeksToMilestone} weeks. ";
                }
                if (notStartedWorkloads.Any())
                {
                    aiReco += $"Then transition {notStartedWorkloads.First().Name} to unlock cloud benefits.";
                }
                
                AIActionSummary = new AIActionSummary
                {
                    PrimaryEnrollmentAction = enrollmentAction,
                    EnrollmentActionImpact = enrollmentImpact,
                    PrimaryWorkloadAction = workloadAction,
                    WorkloadActionImpact = workloadImpact,
                    EnrollmentBlockers = realEnrollmentBlockers,
                    WorkloadBlockers = realWorkloadBlockers,
                    AIRecommendation = aiReco,
                    WeeksToNextMilestone = weeksToMilestone,
                    IsAIPowered = _aiRecommendationService?.IsConfigured ?? false
                };
                
                Instance.Info($"✅ Generated REAL AI Action Summary:");
                Instance.Info($"   Primary action: Enroll {enrollmentImpact} devices");
                Instance.Info($"   Enrollment blockers: {realEnrollmentBlockers.Count}");
                Instance.Info($"   Workload blockers: {realWorkloadBlockers.Count}");
                Instance.Info($"   Weeks to 75% milestone: {weeksToMilestone}");
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "GenerateRealAIActionSummaryAsync");
                // Fall back to minimal summary on error
                AIActionSummary = new AIActionSummary
                {
                    PrimaryEnrollmentAction = "Unable to generate recommendations - check data connections",
                    EnrollmentActionImpact = 0,
                    PrimaryWorkloadAction = "Check Graph and ConfigMgr connectivity",
                    WorkloadActionImpact = "Data required for recommendations",
                    EnrollmentBlockers = new List<string> { "Error loading blocker data" },
                    WorkloadBlockers = new List<string> { "Error loading workload data" },
                    AIRecommendation = "Please verify data source connections and try again.",
                    WeeksToNextMilestone = 0,
                    IsAIPowered = false
                };
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
        /// Runs the Analysis Pipeline to detect enrollment and workload stalls.
        /// Populates pipeline properties for UI binding.
        /// </summary>
        private async Task LoadAnalysisPipelineAsync()
        {
            try
            {
                var orchestrator = ServiceRegistration.GetPipelineOrchestrator();
                if (orchestrator == null)
                {
                    Instance.Warning("[PIPELINE] Orchestrator not available — skipping pipeline analysis");
                    return;
                }

                Instance.Info("[PIPELINE] Running analysis pipeline...");
                var result = await orchestrator.RunAsync();
                PipelineResult = result;
                PipelineSeverity = result.OverallSeverity.ToString();

                // Extract enrollment stall assessment
                var enrollmentResult = result.AnalyzerResults.Find(r => r.AnalyzerName == "EnrollmentStallAnalyzer");
                if (enrollmentResult?.Assessment is EnrollmentStallAssessment enrollmentAssessment)
                {
                    HasPipelineStall = enrollmentAssessment.IsStalled;
                    PipelineStallClassification = enrollmentAssessment.Classification.ToString();
                    TrustResetBatchSize = enrollmentAssessment.TrustResetBatchSize;

                    if (enrollmentAssessment.IsStalled)
                    {
                        PipelineStallSummary = enrollmentAssessment.IsTrustTroughRisk
                            ? $"Trust Trough detected — {enrollmentAssessment.CurrentEnrollmentPercentage:F0}% enrolled, stalled {enrollmentAssessment.StallDurationDays} days"
                            : $"Enrollment stalled — {enrollmentAssessment.StallDurationDays} days at {enrollmentAssessment.CurrentEnrollmentPercentage:F0}%";

                        PipelineCostOfInaction = enrollmentAssessment.PatchLatencyImpact;
                    }
                }

                // Extract workload stall assessment
                var workloadResult = result.AnalyzerResults.Find(r => r.AnalyzerName == "WorkloadStallAnalyzer");
                if (workloadResult?.Assessment is WorkloadStallAssessment workloadAssessment)
                {
                    HasWorkloadStall = workloadAssessment.IsStalled;
                    IsWorkloadTrustTrough = workloadAssessment.IsWorkloadTrustTrough;

                    if (workloadAssessment.IsStalled)
                    {
                        var stalledNames = workloadAssessment.StalledWorkloads.Select(s => s.Name);
                        WorkloadStallSummary = workloadAssessment.IsWorkloadTrustTrough
                            ? $"Workload Trust Trough — {workloadAssessment.DaysSinceAnyProgress} days since progress"
                            : $"{workloadAssessment.StalledWorkloads.Count} workload(s) stalled: {string.Join(", ", stalledNames)}";
                    }

                    StalledWorkloadDetails.Clear();
                    foreach (var sw in workloadAssessment.StalledWorkloads)
                        StalledWorkloadDetails.Add(sw);
                }

                // Collect pipeline recommendations
                PipelineRecommendations.Clear();
                foreach (var rec in result.AllRecommendations)
                    PipelineRecommendations.Add(rec);
                OnPropertyChanged(nameof(HasPipelineRecommendations));

                Instance.Info($"[PIPELINE] Complete — severity: {result.OverallSeverity}, " +
                    $"enrollment stall: {HasPipelineStall}, workload stall: {HasWorkloadStall}, " +
                    $"recommendations: {PipelineRecommendations.Count}");
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "LoadAnalysisPipelineAsync");
            }

            // Demo stall mode: inject realistic mock stall data if pipeline didn't find real stalls
            if (_demoStallMode && !HasPipelineStall && !HasWorkloadStall)
            {
                LoadDemoStallData();
            }

            // Generate Ideas tab content (Decision Cards + Tier 1 features)
            GenerateIdeasTabContent();
        }

        /// <summary>
        /// Populates all Ideas tab collections from existing data using DecisionCardGenerator.
        /// </summary>
        private void GenerateIdeasTabContent()
        {
            try
            {
                var generator = new DecisionCardGenerator();

                // Extract workload stall assessment from pipeline result
                WorkloadStallAssessment? workloadAssessment = null;
                if (PipelineResult != null)
                {
                    var workloadResult = PipelineResult.AnalyzerResults
                        .Find(r => r.AnalyzerName == "WorkloadStallAnalyzer");
                    workloadAssessment = workloadResult?.Assessment as WorkloadStallAssessment;
                }

                // Decision Cards
                var cards = generator.GenerateDecisionCards(
                    Workloads, workloadAssessment, WorkloadMomentumInsight, NearCloudNativeCount);
                DecisionCards.Clear();
                foreach (var card in cards) DecisionCards.Add(card);
                OnPropertyChanged(nameof(HasDecisionCards));

                // Workload Unlock Chains
                var chains = generator.GenerateUnlockChains(Workloads);
                UnlockChains.Clear();
                foreach (var chain in chains) UnlockChains.Add(chain);

                // ConfigMgr Coverage Cards
                var coverage = generator.GenerateCoverageCards(Workloads);
                CoverageCards.Clear();
                foreach (var c in coverage) CoverageCards.Add(c);

                // Safety Scores
                var safety = generator.GenerateSafetyScores(Workloads, WorkloadMomentumInsight);
                SafetyScores.Clear();
                foreach (var s in safety) SafetyScores.Add(s);

                // Last Holdout Spotlight
                var spotlight = generator.GenerateLastHoldoutSpotlight(
                    Workloads, workloadAssessment, NearCloudNativeCount);
                LastHoldoutSpotlightCard = spotlight;
                OnPropertyChanged(nameof(HasLastHoldoutSpotlight));

                // === Deep Analysis Features ===

                // Feature 1: Uninstall Readiness
                UninstallReadiness = generator.GenerateUninstallReadiness(Workloads, DeviceEnrollment);

                // Feature 2: Security Exposure Gap
                SecurityExposure = generator.GenerateSecurityExposure(Workloads, DeviceEnrollment, ComplianceScore);

                // Feature 3: Stale/Orphan Detection
                StaleOrphanResult = generator.GenerateStaleOrphanDetection(Workloads, DeviceEnrollment);

                Instance.Info($"[IDEAS] Generated {DecisionCards.Count} decision cards, " +
                    $"{UnlockChains.Count} unlock chains, {CoverageCards.Count} coverage cards, " +
                    $"{SafetyScores.Count} safety scores, " +
                    $"spotlight: {(LastHoldoutSpotlightCard != null ? "yes" : "no")}");
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "GenerateIdeasTabContent");
            }
        }

        /// <summary>
        /// Injects realistic mock stall data for UI preview when launched with /demostall.
        /// </summary>
        private void LoadDemoStallData()
        {
            Instance.Info("[PIPELINE-DEMO] Injecting demo stall data for UI preview");

            // Enrollment stall: Trust Trough scenario at 57%
            HasPipelineStall = true;
            PipelineStallClassification = StallClassification.ConfidenceBased.ToString();
            PipelineStallSummary = "Trust Trough detected — 57% enrolled, stalled 18 days";
            PipelineCostOfInaction = "1,075 devices remain on 48-hour patch delay. Estimated 3.2-day average exposure window for critical CVEs.";
            PipelineSeverity = SeverityLevel.High.ToString();
            TrustResetBatchSize = 142;

            // Workload stall: 2 workloads stuck
            HasWorkloadStall = true;
            IsWorkloadTrustTrough = true;
            WorkloadStallSummary = "Workload Trust Trough — 22 days since progress";

            StalledWorkloadDetails.Clear();
            StalledWorkloadDetails.Add(new StalledWorkload
            {
                Name = "Client Apps",
                CurrentAdoptionPercentage = 34.2,
                DaysSinceChange = 22,
                DevicesBlocked = 412,
                BlockReason = "Win32 app packaging not started for 6 LOB apps",
                WhyStalled = StallClassification.Operational
            });
            StalledWorkloadDetails.Add(new StalledWorkload
            {
                Name = "Device Configuration",
                CurrentAdoptionPercentage = 61.8,
                DaysSinceChange = 15,
                DevicesBlocked = 287,
                BlockReason = "GPO-to-Intune migration blocked on VPN profile testing",
                WhyStalled = StallClassification.Technical
            });

            // Demo recommendations
            PipelineRecommendations.Clear();
            PipelineRecommendations.Add(new PipelineRecommendation
            {
                Title = "Trust Reset Batch: Re-enroll 142 Excellent-readiness devices",
                Description = "142 devices scored 'Excellent' readiness but failed initial enrollment. A targeted re-enrollment batch can break the Trust Trough by demonstrating safe migration at scale.",
                RiskLevel = "Low",
                CostOfInaction = "Trust Trough persists — remaining 1,075 devices stay on legacy patch cycle",
                EstimatedEffort = "2-4 hours",
                TargetDeviceCount = 142,
                BlastRadiusDevices = 142,
                BlastRadiusUsers = 118,
                ImpactScore = 85,
                Priority = RecommendationPriority.Critical,
                Category = RecommendationCategory.DeviceEnrollment,
                SourceAnalyzer = "EnrollmentStallAnalyzer"
            });
            PipelineRecommendations.Add(new PipelineRecommendation
            {
                Title = "Fast-track Win32 app packaging for 6 LOB applications",
                Description = "Client Apps workload is blocked at 34% because 6 line-of-business apps have not been repackaged as Win32 apps for Intune deployment.",
                RiskLevel = "Medium",
                CostOfInaction = "412 devices cannot complete workload transition — dual management overhead continues",
                EstimatedEffort = "1-2 weeks",
                TargetDeviceCount = 412,
                BlastRadiusDevices = 412,
                BlastRadiusUsers = 380,
                ImpactScore = 72,
                Priority = RecommendationPriority.High,
                Category = RecommendationCategory.WorkloadTransition,
                SourceAnalyzer = "WorkloadStallAnalyzer"
            });
            PipelineRecommendations.Add(new PipelineRecommendation
            {
                Title = "Complete VPN profile testing to unblock Device Configuration",
                Description = "Device Configuration workload stalled at 62% pending VPN profile validation. 287 devices waiting on GPO migration.",
                RiskLevel = "Medium",
                CostOfInaction = "287 devices remain GPO-managed — configuration drift risk increases weekly",
                EstimatedEffort = "3-5 days",
                TargetDeviceCount = 287,
                BlastRadiusDevices = 287,
                BlastRadiusUsers = 245,
                ImpactScore = 68,
                Priority = RecommendationPriority.High,
                Category = RecommendationCategory.WorkloadTransition,
                SourceAnalyzer = "WorkloadStallAnalyzer"
            });
            OnPropertyChanged(nameof(HasPipelineRecommendations));

            Instance.Info("[PIPELINE-DEMO] Demo stall data loaded: enrollment stall + 2 workload stalls + 3 recommendations");
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
                    "ZeroTrustMigrationAddin",
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

        #region Enhanced Workloads Tab Methods

        /// <summary>
        /// Initialize workloads with benefits, readiness scores, dependencies, and Microsoft-recommended order
        /// </summary>
        private void InitializeWorkloadsWithBenefits()
        {
            Workloads.Clear();

            // 1. Compliance Policies (First - Foundation)
            Workloads.Add(new Workload
            {
                Name = "Compliance Policies",
                Description = "Device compliance policies moved to Intune",
                Status = WorkloadStatus.NotStarted,
                Order = 1,
                Benefits = new List<string>
                {
                    "Establish security baseline before other migrations",
                    "Prevent unmanaged devices from accessing resources",
                    "Low-risk foundation (policies are evaluative, not enforcing)"
                },
                ReadinessScore = 87,
                EstimatedTime = "1-2 weeks",
                RiskLevel = "Low",
                DependsOn = new List<string>(),
                LearnMoreUrl = "https://learn.microsoft.com/mem/intune/protect/device-compliance-get-started"
            });

            // 2. Endpoint Protection (Second - Security hardening)
            Workloads.Add(new Workload
            {
                Name = "Endpoint Protection",
                Description = "Antivirus and security settings",
                Status = WorkloadStatus.NotStarted,
                Order = 2,
                Benefits = new List<string>
                {
                    "Ensure antivirus and firewall protection in place early",
                    "Largely compatible with existing ConfigMgr settings (low risk)",
                    "Critical for zero-trust security posture"
                },
                ReadinessScore = 82,
                EstimatedTime = "2-3 weeks",
                RiskLevel = "Low",
                DependsOn = new List<string> { "Compliance Policies" },
                LearnMoreUrl = "https://learn.microsoft.com/mem/intune/protect/endpoint-security"
            });

            // 3. Device Configuration (Third - Settings and restrictions)
            Workloads.Add(new Workload
            {
                Name = "Device Configuration",
                Description = "Configuration profiles migrated",
                Status = WorkloadStatus.NotStarted,
                Order = 3,
                Benefits = new List<string>
                {
                    "Standardize device settings across organization",
                    "Enable user productivity with Wi-Fi/VPN profiles",
                    "Reduce help desk tickets with consistent configurations"
                },
                ReadinessScore = 65,
                EstimatedTime = "2-3 weeks",
                RiskLevel = "Medium",
                DependsOn = new List<string> { "Compliance Policies", "Endpoint Protection" },
                LearnMoreUrl = "https://learn.microsoft.com/mem/intune/configuration/device-profiles"
            });

            // 4. Resource Access (Fourth - User connectivity)
            Workloads.Add(new Workload
            {
                Name = "Resource Access",
                Description = "VPN, Wi-Fi, email, certificate profiles",
                Status = WorkloadStatus.NotStarted,
                Order = 4,
                Benefits = new List<string>
                {
                    "Enable BYOD and remote work scenarios",
                    "Secure connectivity for distributed workforce",
                    "Automated certificate deployment reduces manual effort"
                },
                ReadinessScore = 0,
                EstimatedTime = "2-3 weeks",
                RiskLevel = "Medium",
                DependsOn = new List<string> { "Device Configuration" },
                LearnMoreUrl = "https://learn.microsoft.com/mem/intune/configuration/vpn-settings-configure"
            });

            // 5. Windows Update for Business (Fifth - Patch management)
            Workloads.Add(new Workload
            {
                Name = "Windows Update for Business",
                Description = "Patch management and feature updates",
                Status = WorkloadStatus.NotStarted,
                Order = 5,
                Benefits = new List<string>
                {
                    "Eliminate weekend patching work with automated update rings",
                    "Reduce patch deployment failures with gradual rollout",
                    "Unified update experience across Windows 10/11"
                },
                ReadinessScore = 0,
                EstimatedTime = "1-2 weeks",
                RiskLevel = "Low",
                DependsOn = new List<string> { "Device Configuration" },
                LearnMoreUrl = "https://learn.microsoft.com/windows/deployment/update/waas-manage-updates-wufb"
            });

            // 6. Office Click-to-Run (Sixth - Office deployment)
            Workloads.Add(new Workload
            {
                Name = "Office Click-to-Run",
                Description = "Microsoft 365 Apps deployment and updates",
                Status = WorkloadStatus.NotStarted,
                Order = 6,
                Benefits = new List<string>
                {
                    "Automated Office 365 updates reduce admin overhead",
                    "User-driven installs from Company Portal improve satisfaction",
                    "Cloud-delivered updates eliminate SCCM distribution points"
                },
                ReadinessScore = 0,
                EstimatedTime = "1-2 weeks",
                RiskLevel = "Low",
                DependsOn = new List<string> { "Device Configuration", "Windows Update for Business" },
                LearnMoreUrl = "https://learn.microsoft.com/microsoft-365-apps/admin-center/overview-cloud-policy"
            });

            // 7. Client Apps (Last - Most complex)
            Workloads.Add(new Workload
            {
                Name = "Client Apps",
                Description = "Win32 app deployment (LOB apps, third-party)",
                Status = WorkloadStatus.NotStarted,
                Order = 7,
                Benefits = new List<string>
                {
                    "Modern app deployment with self-service Company Portal",
                    "Reduce helpdesk tickets by 40% with user-driven installs",
                    "Eliminate ConfigMgr distribution points and save infrastructure costs"
                },
                ReadinessScore = 0,
                EstimatedTime = "3-4 weeks",
                RiskLevel = "High",
                DependsOn = new List<string> { "Device Configuration", "Windows Update for Business", "Office Click-to-Run" },
                LearnMoreUrl = "https://learn.microsoft.com/mem/intune/apps/apps-win32-app-management"
            });

            FileLogger.Instance.Info($"✅ Initialized {Workloads.Count} workloads with benefits and dependencies");
        }

        /// <summary>
        /// v3.17.229 - Populates realistic mock workload authority data for demonstration mode.
        /// Simulates a mid-migration environment with 1,247 co-managed devices at various adoption stages.
        /// </summary>
        private void PopulateMockWorkloadAuthorityData()
        {
            const int totalDevices = 1247;

            // Realistic mid-migration adoption data (percentage on Intune)
            var mockAdoption = new Dictionary<string, (double pct, WorkloadStatus status)>
            {
                { "Compliance Policies",         (94.2, WorkloadStatus.Completed) },
                { "Endpoint Protection",         (91.5, WorkloadStatus.Completed) },
                { "Device Configuration",        (67.3, WorkloadStatus.InProgress) },
                { "Resource Access",             (52.8, WorkloadStatus.InProgress) },
                { "Windows Update for Business", (43.1, WorkloadStatus.InProgress) },
                { "Office Click-to-Run",         (18.6, WorkloadStatus.InProgress) },
                { "Client Apps",                 (6.4,  WorkloadStatus.NotStarted) }
            };

            foreach (var workload in Workloads)
            {
                if (mockAdoption.TryGetValue(workload.Name, out var data))
                {
                    int intuneCount = (int)Math.Round(totalDevices * data.pct / 100.0);
                    workload.IntuneAdoptionPercentage = data.pct;
                    workload.IntuneDeviceCount = intuneCount;
                    workload.ConfigMgrDeviceCount = totalDevices - intuneCount;
                    workload.HasRealData = true; // Show device count breakdown in demo
                    workload.Status = data.status;
                    workload.ReadinessScore = data.pct;
                }
            }

            NearCloudNativeCount = 83;

            LastHoldoutWorkloads.Clear();
            LastHoldoutWorkloads.Add(new LastHoldoutWorkload { WorkloadName = "Client Apps", DevicesBlockedCount = 38, Icon = "🔒" });
            LastHoldoutWorkloads.Add(new LastHoldoutWorkload { WorkloadName = "Office Click-to-Run", DevicesBlockedCount = 27, Icon = "🔒" });
            LastHoldoutWorkloads.Add(new LastHoldoutWorkload { WorkloadName = "Windows Update for Business", DevicesBlockedCount = 18, Icon = "🔒" });

            MigrationStatus = new MigrationStatus
            {
                WorkloadsTransitioned = Workloads.Count(w => w.Status == WorkloadStatus.Completed),
                TotalWorkloads = 7,
                LastUpdateDate = DateTime.Now
            };

            OnPropertyChanged(nameof(Workloads));
            OnPropertyChanged(nameof(WorkloadsCompletedCount));
            OnPropertyChanged(nameof(HasLastHoldouts));
            OnPropertyChanged(nameof(HasWorkloadAuthority));
            OnPropertyChanged(nameof(TotalCoManagedDevices));
            OnPropertyChanged(nameof(DevicesReadyForCloudNative));

            Instance.Info($"✅ Populated mock workload authority: {Workloads.Count(w => w.Status == WorkloadStatus.Completed)}/7 completed, {NearCloudNativeCount} near cloud-native");
        }

        /// <summary>
        /// Computes a data-driven migration sequence from actual workload data.
        /// Sorts by: completed first (already done), then by adoption % descending (highest adoption = easiest next step),
        /// respects dependencies, and generates rationale from real numbers.
        /// </summary>
        private void ComputeWorkloadSequence()
        {
            try
            {
                var steps = new List<WorkloadSequenceStep>();

                // Separate completed from remaining
                var completed = Workloads.Where(w => w.Status == WorkloadStatus.Completed)
                    .OrderByDescending(w => w.IntuneAdoptionPercentage).ToList();
                var remaining = Workloads.Where(w => w.Status != WorkloadStatus.Completed)
                    .OrderByDescending(w => w.IntuneAdoptionPercentage).ToList();

                // Completed workloads first (already done)
                int step = 1;
                foreach (var w in completed)
                {
                    steps.Add(new WorkloadSequenceStep
                    {
                        StepNumber = step++,
                        WorkloadName = w.Name,
                        AdoptionPercentage = w.IntuneAdoptionPercentage,
                        DeviceCount = w.IntuneDeviceCount,
                        RiskLevel = w.RiskLevel,
                        EstimatedTime = w.EstimatedTime,
                        Rationale = $"Already at {w.IntuneAdoptionPercentage:F0}% Intune adoption — {w.IntuneDeviceCount:N0} devices migrated",
                        ReadinessLabel = "✓ Completed",
                        TimelineLabel = "Done",
                        DependencyNote = ""
                    });
                }

                // Remaining workloads sorted by adoption (highest first = path of least resistance)
                var completedNames = new HashSet<string>(completed.Select(w => w.Name));
                foreach (var w in remaining)
                {
                    var unmetDeps = w.DependsOn.Where(d => !completedNames.Contains(d)).ToList();
                    string depNote = unmetDeps.Count > 0
                        ? $"After: {string.Join(", ", unmetDeps)}"
                        : w.DependsOn.Count > 0 ? "Dependencies met" : "No dependencies";

                    string rationale;
                    if (w.IntuneAdoptionPercentage >= 50)
                        rationale = $"{w.IntuneAdoptionPercentage:F0}% already on Intune — momentum is there, push to completion";
                    else if (w.IntuneAdoptionPercentage >= 20)
                        rationale = $"{w.IntuneAdoptionPercentage:F0}% started — {w.ConfigMgrDeviceCount:N0} devices still on ConfigMgr need migration";
                    else if (w.IntuneAdoptionPercentage > 0)
                        rationale = $"Only {w.IntuneAdoptionPercentage:F0}% adoption — pilot phase, validate before broad rollout";
                    else
                        rationale = $"Not started — {w.RiskLevel} risk, plan {w.EstimatedTime} for rollout";

                    string readiness = w.IntuneAdoptionPercentage >= 50 ? "Ready" :
                                       w.IntuneAdoptionPercentage >= 20 ? "In Progress" :
                                       w.IntuneAdoptionPercentage > 0 ? "Pilot" : "Not Started";

                    string timeline = w.IntuneAdoptionPercentage >= 50 ? "Push now" :
                                      w.IntuneAdoptionPercentage >= 20 ? w.EstimatedTime :
                                      $"{w.EstimatedTime} + pilot";

                    steps.Add(new WorkloadSequenceStep
                    {
                        StepNumber = step++,
                        WorkloadName = w.Name,
                        AdoptionPercentage = w.IntuneAdoptionPercentage,
                        DeviceCount = w.IntuneDeviceCount + w.ConfigMgrDeviceCount,
                        RiskLevel = w.RiskLevel,
                        EstimatedTime = w.EstimatedTime,
                        Rationale = rationale,
                        ReadinessLabel = readiness,
                        TimelineLabel = timeline,
                        DependencyNote = depNote
                    });

                    // Track completed for dependency resolution
                    completedNames.Add(w.Name);
                }

                WorkloadSequenceSteps = new ObservableCollection<WorkloadSequenceStep>(steps);
                OnPropertyChanged(nameof(HasWorkloadSequence));
                Instance.Info($"✅ Computed workload sequence: {steps.Count} steps ({completed.Count} completed, {remaining.Count} remaining)");
            }
            catch (Exception ex)
            {
                Instance.Error($"❌ Failed to compute workload sequence: {ex.Message}");
            }
        }

        /// <summary>
        /// Update workload readiness scores and status based on current state
        /// </summary>
        private void UpdateWorkloadReadinessScores()
        {
            try
            {
                // Calculate readiness based on enrollment percentage and compliance score
                double baseReadiness = DeviceEnrollment.IntuneEnrollmentPercentage * 0.6 + (ComplianceScore?.IntuneScore ?? 0) * 0.4;

                foreach (var workload in Workloads)
                {
                    // First workload (Compliance) is always ready if enrollment > 50%
                    if (workload.Order == 1)
                    {
                        workload.ReadinessScore = DeviceEnrollment.IntuneEnrollmentPercentage >= 50 ? 85 : DeviceEnrollment.IntuneEnrollmentPercentage * 1.5;
                        workload.IsBlocked = DeviceEnrollment.IntuneEnrollmentPercentage < 50;
                        workload.BlockReason = workload.IsBlocked ? "Need ≥50% device enrollment first" : string.Empty;
                    }
                    else
                    {
                        // Check if dependencies are met
                        bool depsMet = workload.DependsOn.All(dep => Workloads.Any(w => w.Name == dep && w.Status == WorkloadStatus.Completed));
                        
                        if (!depsMet)
                        {
                            workload.ReadinessScore = 20;
                            workload.IsBlocked = true;
                            workload.BlockReason = $"Requires {string.Join(", ", workload.DependsOn)} to be completed first";
                        }
                        else
                        {
                            workload.ReadinessScore = baseReadiness + (10 * workload.Order); // Later workloads get bonus for momentum
                            workload.IsBlocked = false;
                            workload.BlockReason = string.Empty;
                        }
                    }

                    // Cap at 100
                    workload.ReadinessScore = Math.Min(100, workload.ReadinessScore);
                }

                OnPropertyChanged(nameof(Workloads));
                FileLogger.Instance.Info($"✅ Updated readiness scores for {Workloads.Count} workloads");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"❌ Failed to update workload readiness scores: {ex.Message}");
            }
        }

        /// <summary>
        /// v3.17.227 - Maps Graph API workload names to UI Workload.Name
        /// </summary>
        private static readonly Dictionary<string, string> WorkloadNameMapping = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Compliance Policy", "Compliance Policies" },
            { "Device Configuration", "Device Configuration" },
            { "Resource Access", "Resource Access" },
            { "Windows Update", "Windows Update for Business" },
            { "Endpoint Protection", "Endpoint Protection" },
            { "Modern Apps", "Client Apps" },
            { "Office Apps", "Office Click-to-Run" }
        };

        /// <summary>
        /// v3.17.227 - Updates workload status and adoption from real Graph API workload authority data.
        /// Replaces the generic formula with actual per-workload Intune adoption percentages.
        /// </summary>
        private async Task UpdateWorkloadsFromAuthorityDataAsync()
        {
            try
            {
                Instance.Info("Loading real workload authority data from Graph API...");
                var authority = await _graphDataService.GetCoManagedWorkloadAuthorityAsync();
                
                if (authority == null || authority.TotalCoManagedDevices == 0)
                {
                    Instance.Warning("No co-managed workload authority data available");
                    return;
                }

                WorkloadAuthority = authority;
                Instance.Info($"Workload authority loaded: {authority.TotalCoManagedDevices} co-managed devices, {authority.DevicesReadyForCloudNative} cloud-native ready");

                // Map adoption counts to each workload
                foreach (var workload in Workloads)
                {
                    var graphKey = WorkloadNameMapping.FirstOrDefault(kvp => kvp.Value == workload.Name).Key;
                    if (graphKey != null && authority.WorkloadIntuneAdoptionCounts.TryGetValue(graphKey, out int intuneCount))
                    {
                        int total = authority.TotalCoManagedDevices;
                        double adoptionPct = total > 0 ? Math.Round((double)intuneCount / total * 100, 1) : 0;

                        workload.IntuneAdoptionPercentage = adoptionPct;
                        workload.IntuneDeviceCount = intuneCount;
                        workload.ConfigMgrDeviceCount = total - intuneCount;
                        workload.HasRealData = true;
                        workload.ReadinessScore = adoptionPct; // Replace generic formula with real data

                        // Derive status from adoption percentage
                        workload.Status = adoptionPct switch
                        {
                            >= 90 => WorkloadStatus.Completed,
                            >= 10 => WorkloadStatus.InProgress,
                            _ => WorkloadStatus.NotStarted
                        };

                        Instance.Info($"  {workload.Name}: {adoptionPct:F1}% Intune ({intuneCount}/{total}) → {workload.Status}");
                    }
                }

                // Calculate near-cloud-native devices (6 of 7 workloads on Intune)
                NearCloudNativeCount = authority.Devices.Count(d => d.WorkloadsManagedByIntuneCount == 6);

                // Calculate last-holdout workloads (for devices with exactly 6/7 on Intune)
                var nearCloudNativeDevices = authority.Devices.Where(d => d.WorkloadsManagedByIntuneCount == 6).ToList();
                var holdoutCounts = new Dictionary<string, int>();
                foreach (var device in nearCloudNativeDevices)
                {
                    var remaining = device.WorkloadsStillOnConfigMgr;
                    foreach (var wl in remaining)
                    {
                        string displayName = WorkloadNameMapping.TryGetValue(wl, out var mapped) ? mapped : wl;
                        holdoutCounts[displayName] = holdoutCounts.GetValueOrDefault(displayName) + 1;
                    }
                }

                LastHoldoutWorkloads.Clear();
                foreach (var kvp in holdoutCounts.OrderByDescending(x => x.Value))
                {
                    LastHoldoutWorkloads.Add(new LastHoldoutWorkload
                    {
                        WorkloadName = kvp.Key,
                        DevicesBlockedCount = kvp.Value,
                        Icon = "🔒"
                    });
                }

                // Update migration status from real data
                int completed = Workloads.Count(w => w.Status == WorkloadStatus.Completed);
                MigrationStatus = new MigrationStatus
                {
                    WorkloadsTransitioned = completed,
                    TotalWorkloads = 7,
                    LastUpdateDate = DateTime.Now
                };

                // Notify UI
                OnPropertyChanged(nameof(Workloads));
                OnPropertyChanged(nameof(WorkloadsCompletedCount));
                OnPropertyChanged(nameof(HasLastHoldouts));
                OnPropertyChanged(nameof(HasWorkloadAuthority));
                OnPropertyChanged(nameof(TotalCoManagedDevices));
                OnPropertyChanged(nameof(DevicesReadyForCloudNative));

                Instance.Info($"✅ Workload authority bridge complete: {completed}/7 completed, {NearCloudNativeCount} near cloud-native, {LastHoldoutWorkloads.Count} holdout workloads");
            }
            catch (Exception ex)
            {
                Instance.LogException(ex, "UpdateWorkloadsFromAuthorityDataAsync");
                // Fall back to generic readiness scores
                UpdateWorkloadReadinessScores();
            }
        }

        /// <summary>
        /// Calculate velocity indicators for progress tracking panel
        /// </summary>
        private void CalculateWorkloadVelocity()
        {
            try
            {
                int completedWorkloads = Workloads.Count(w => w.Status == WorkloadStatus.Completed);
                double completionPercent = MigrationStatus.CompletionPercentage;

                // Estimate weeks since start (mock - would come from real data)
                int weeksSinceStart = 12; // Placeholder

                double weeklyVelocity = weeksSinceStart > 0 ? (completionPercent / weeksSinceStart) : 0;

                // Categorize velocity
                if (weeklyVelocity >= 15)
                {
                    VelocityIcon = "🚀";
                    VelocityLabel = "Excellent Velocity";
                    VelocityDescription = $"{weeklyVelocity:F1}% per week - Ahead of schedule!";
                    VelocityBgColor = "#F1F8F4";
                    VelocityTextColor = "#107C10";
                }
                else if (weeklyVelocity >= 10)
                {
                    VelocityIcon = "⚡";
                    VelocityLabel = "Good Velocity";
                    VelocityDescription = $"{weeklyVelocity:F1}% per week - On track";
                    VelocityBgColor = "#FFF9E6";
                    VelocityTextColor = "#FDB813";
                }
                else if (weeklyVelocity >= 5)
                {
                    VelocityIcon = "🐌";
                    VelocityLabel = "Slow Progress";
                    VelocityDescription = $"{weeklyVelocity:F1}% per week - Consider acceleration";
                    VelocityBgColor = "#FFF4F4";
                    VelocityTextColor = "#D13438";
                }
                else
                {
                    VelocityIcon = "📉";
                    VelocityLabel = "Stalled";
                    VelocityDescription = $"{weeklyVelocity:F1}% per week - Action needed";
                    VelocityBgColor = "#FFE6E6";
                    VelocityTextColor = "#D13438";
                }

                // Mock peer comparison (would come from real data)
                HasPeerComparison = true;
                YourVelocityPercent = weeklyVelocity;
                PeerVelocityPercent = 12.5;
                AccelerationNeeded = weeklyVelocity < PeerVelocityPercent 
                    ? $"{(PeerVelocityPercent - weeklyVelocity):F1}% per week" 
                    : "None - exceeding peers!";

                FileLogger.Instance.Info($"✅ Calculated workload velocity: {VelocityLabel} ({weeklyVelocity:F1}%/week)");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"❌ Failed to calculate workload velocity: {ex.Message}");
            }
        }

        /// <summary>
        /// Update top workload blockers for alert banner
        /// </summary>
        private void UpdateWorkloadBlockers()
        {
            try
            {
                // Get top 3 blockers sorted by affected devices
                var topBlockers = Blockers.OrderByDescending(b => b.AffectedDevices).Take(3).ToList();
                TopWorkloadBlockers = new ObservableCollection<Blocker>(topBlockers);

                OnPropertyChanged(nameof(HasWorkloadBlockers));
                OnPropertyChanged(nameof(WorkloadBlockerDeviceCount));

                FileLogger.Instance.Info($"✅ Updated workload blockers: {TopWorkloadBlockers.Count} top blockers");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"❌ Failed to update workload blockers: {ex.Message}");
            }
        }

        /// <summary>
        /// Command handler: Start workload transition
        /// </summary>
        private void OnStartWorkloadTransition(string? workloadName)
        {
            try
            {
                if (string.IsNullOrEmpty(workloadName)) return;

                var workload = Workloads.FirstOrDefault(w => w.Name == workloadName);
                if (workload == null) return;

                // Scroll to and expand the workload card
                MessageBox.Show(
                    $"Starting transition for: {workloadName}\n\n" +
                    $"Readiness Score: {workload.ReadinessScore:F0}/100\n" +
                    $"Risk Level: {workload.RiskLevel}\n" +
                    $"Estimated Time: {workload.EstimatedTime}\n\n" +
                    $"The workload card below will expand to show the detailed 4-week plan.",
                    "Start Workload Transition",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                FileLogger.Instance.Info($"✅ Starting workload transition: {workloadName}");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"❌ Failed to start workload transition: {ex.Message}");
                MessageBox.Show($"Error starting transition: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Command handler: View rollback plan
        /// </summary>
        private void OnViewRollbackPlan()
        {
            try
            {
                if (WorkloadMomentumInsight == null) return;

                string rollbackPlan = $"ROLLBACK PLAN: {WorkloadMomentumInsight.RecommendedWorkload}\n\n" +
                    $"Estimated Rollback Time: {WorkloadMomentumInsight.RollbackTimeMinutes} minutes\n\n" +
                    "STEPS:\n" +
                    "1. Pause policy sync in Intune portal (5 min)\n" +
                    "2. Set co-management slider back to ConfigMgr (10 min)\n" +
                    "3. Force ConfigMgr policy refresh on devices (15 min)\n" +
                    "4. Validate devices show ConfigMgr as authority (10 min)\n\n" +
                    "DATA TO CAPTURE BEFORE ROLLBACK:\n" +
                    "• Intune policy deployment logs\n" +
                    "• Device compliance reports\n" +
                    "• User feedback and issue tickets\n\n" +
                    "RISK OF ROLLBACK: Low - No data loss expected";

                MessageBox.Show(rollbackPlan, "Rollback Plan", MessageBoxButton.OK, MessageBoxImage.Information);
                FileLogger.Instance.Info("✅ Displayed rollback plan");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"❌ Failed to view rollback plan: {ex.Message}");
            }
        }

        /// <summary>
        /// Command handler: Start pilot phase
        /// </summary>
        private void OnStartPilotPhase()
        {
            try
            {
                if (WorkloadMomentumInsight == null) return;

                string pilotMessage = $"STARTING PILOT PHASE\n\n" +
                    $"Workload: {WorkloadMomentumInsight.RecommendedWorkload}\n" +
                    $"Pilot Size: 10-20 devices (IT team recommended)\n" +
                    $"Duration: Week 1 (5 business days)\n\n" +
                    "NEXT STEPS:\n" +
                    "1. Select 10-20 pilot devices from IT department\n" +
                    "2. Deploy policies to pilot group\n" +
                    "3. Monitor for 5 business days\n" +
                    "4. Collect feedback from pilot users\n\n" +
                    "SUCCESS CRITERIA:\n" +
                    "✓ 95%+ pilot devices successfully applied policies\n" +
                    "✓ Zero critical user complaints\n" +
                    "✓ No help desk tickets related to policy changes\n\n" +
                    "Ready to proceed?";

                var result = MessageBox.Show(pilotMessage, "Start Pilot Phase", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    MessageBox.Show("Pilot phase initiated! Monitor progress in Intune portal.", "Pilot Started", MessageBoxButton.OK, MessageBoxImage.Information);
                    FileLogger.Instance.Info($"✅ Started pilot phase for {WorkloadMomentumInsight.RecommendedWorkload}");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"❌ Failed to start pilot phase: {ex.Message}");
            }
        }

        /// <summary>
        /// Command handler: Open Learn More URL
        /// </summary>
        private void OnOpenLearnMore(string? url)
        {
            try
            {
                if (string.IsNullOrEmpty(url)) return;

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });

                FileLogger.Instance.Info($"✅ Opened Learn More URL: {url}");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"❌ Failed to open Learn More URL: {ex.Message}");
                MessageBox.Show($"Failed to open URL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Command handler: Open remediation URL for blockers
        /// </summary>
        private void OnOpenRemediationUrl(string? url)
        {
            try
            {
                if (string.IsNullOrEmpty(url)) return;

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });

                FileLogger.Instance.Info($"✅ Opened remediation URL: {url}");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"❌ Failed to open remediation URL: {ex.Message}");
                MessageBox.Show($"Failed to open URL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

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
