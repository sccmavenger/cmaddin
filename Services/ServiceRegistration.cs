using System;
using Microsoft.Extensions.DependencyInjection;
using ZeroTrustMigrationAddin.Services.Pipeline;
using ZeroTrustMigrationAddin.Services.Pipeline.Analyzers;
using ZeroTrustMigrationAddin.Services.Pipeline.Recommendations;
using ZeroTrustMigrationAddin.Services.Pipeline.Signals;

namespace ZeroTrustMigrationAddin.Services
{
    /// <summary>
    /// Configures the DI container and registers all pipeline services.
    /// Called during app startup. Existing services remain accessible via their
    /// current patterns (singletons, direct construction) for backward compatibility.
    /// </summary>
    public static class ServiceRegistration
    {
        private static IServiceProvider? _serviceProvider;

        /// <summary>The application's DI service provider. Null until ConfigureServices is called.</summary>
        public static IServiceProvider? ServiceProvider => _serviceProvider;

        /// <summary>
        /// Configures the DI container with all registered services.
        /// Call once during app startup.
        /// </summary>
        public static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // === Existing Services (singleton or transient, backward compatible) ===
            services.AddSingleton(FileLogger.Instance);
            services.AddSingleton(AzureTelemetryService.Instance);
            services.AddTransient<GraphDataService>();
            services.AddTransient<ConfigMgrAdminService>();
            services.AddTransient<WorkloadTrendService>();
            services.AddTransient<DeviceReadinessService>();
            services.AddTransient<MockDataService>();

            // === Pipeline Framework ===
            // Signal Collectors
            services.AddSingleton<ISignalCollector<EnrollmentSignal>, EnrollmentSignalCollector>();
            services.AddSingleton<ISignalCollector<WorkloadSignal>, WorkloadSignalCollector>();

            // Analyzers
            services.AddSingleton<IAnalyzer<EnrollmentSignal, EnrollmentStallAssessment>, EnrollmentStallAnalyzer>();
            services.AddSingleton<IAnalyzer<WorkloadSignal, WorkloadStallAssessment>, WorkloadStallAnalyzer>();

            // Recommendation Providers
            services.AddSingleton<IRecommendationProvider<EnrollmentStallAssessment>, EnrollmentStallRecommendationProvider>();
            services.AddSingleton<IRecommendationProvider<WorkloadStallAssessment>, WorkloadStallRecommendationProvider>();

            // Pipeline Orchestrator
            services.AddSingleton<AnalysisPipelineOrchestrator>();

            _serviceProvider = services.BuildServiceProvider();

            // Wire up analyzer registrations in the orchestrator
            ConfigurePipeline(_serviceProvider);

            FileLogger.Instance.Log(FileLogger.LogLevel.Info,
                "[DI] Service container configured with pipeline framework");

            return _serviceProvider;
        }

        /// <summary>
        /// Registers all analyzer chains (signal → analyzer → recommendation) in the orchestrator.
        /// </summary>
        private static void ConfigurePipeline(IServiceProvider provider)
        {
            var orchestrator = provider.GetRequiredService<AnalysisPipelineOrchestrator>();

            // Enrollment Stall Analyzer chain
            orchestrator.RegisterAnalyzer(
                provider.GetRequiredService<ISignalCollector<EnrollmentSignal>>(),
                provider.GetRequiredService<IAnalyzer<EnrollmentSignal, EnrollmentStallAssessment>>(),
                provider.GetRequiredService<IRecommendationProvider<EnrollmentStallAssessment>>());

            // Workload Stall Analyzer chain
            orchestrator.RegisterAnalyzer(
                provider.GetRequiredService<ISignalCollector<WorkloadSignal>>(),
                provider.GetRequiredService<IAnalyzer<WorkloadSignal, WorkloadStallAssessment>>(),
                provider.GetRequiredService<IRecommendationProvider<WorkloadStallAssessment>>());

            FileLogger.Instance.Log(FileLogger.LogLevel.Info,
                "[PIPELINE] Registered 2 analyzer chains: EnrollmentStall, WorkloadStall");
        }

        /// <summary>
        /// Gets the pipeline orchestrator from the DI container.
        /// Returns null if services haven't been configured yet.
        /// </summary>
        public static AnalysisPipelineOrchestrator? GetPipelineOrchestrator()
        {
            return _serviceProvider?.GetService<AnalysisPipelineOrchestrator>();
        }
    }
}
