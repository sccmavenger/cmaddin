using System;
using System.Collections.Generic;

namespace ZeroTrustMigrationAddin.Models
{
    /// <summary>
    /// Categories of migration impact for comprehensive analysis.
    /// </summary>
    public enum ImpactCategory
    {
        Security,
        OperationalEfficiency,
        UserExperience,
        CostOptimization,
        ComplianceGovernance,
        Modernization
    }

    /// <summary>
    /// Represents a single impact metric with before/after projection.
    /// </summary>
    public class ImpactMetric
    {
        /// <summary>Unique identifier for the metric.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Display name (e.g., "Conditional Access Coverage").</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Brief description of what this metric measures.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Category this metric belongs to.</summary>
        public ImpactCategory Category { get; set; }

        /// <summary>Current state value (before full enrollment).</summary>
        public double CurrentValue { get; set; }

        /// <summary>Projected value after full enrollment.</summary>
        public double ProjectedValue { get; set; }

        /// <summary>Unit of measurement (%, count, days, $, etc.).</summary>
        public string Unit { get; set; } = "%";

        /// <summary>Formatted current value for display.</summary>
        public string CurrentDisplay => FormatValue(CurrentValue);

        /// <summary>Formatted projected value for display.</summary>
        public string ProjectedDisplay => FormatValue(ProjectedValue);

        /// <summary>The improvement amount (projected - current).</summary>
        public double Improvement => ProjectedValue - CurrentValue;

        /// <summary>Formatted improvement for display.</summary>
        public string ImprovementDisplay => FormatImprovement();

        /// <summary>Color for the improvement indicator (green for positive, red for negative where lower is worse).</summary>
        public string ImprovementColor => GetImprovementColor();

        /// <summary>Whether higher values are better (true) or lower values are better (false).</summary>
        public bool HigherIsBetter { get; set; } = true;

        /// <summary>Confidence level of the prediction (0-100).</summary>
        public int ConfidenceLevel { get; set; } = 80;

        /// <summary>Explanation of why this improvement will occur.</summary>
        public string Explanation { get; set; } = string.Empty;

        /// <summary>Icon for the metric (emoji).</summary>
        public string Icon { get; set; } = "📊";

        /// <summary>Whether this metric has sufficient data to calculate.</summary>
        public bool HasData { get; set; } = true;

        /// <summary>Co-management workload associated with this metric (if applicable).</summary>
        public string? RelatedWorkload { get; set; }

        private string FormatValue(double value)
        {
            return Unit switch
            {
                "%" => $"{value:F1}%",
                "count" => $"{value:N0}",
                "devices" => $"{value:N0} devices",
                "days" => $"{value:F1} days",
                "hours" => $"{value:F1} hours",
                "minutes" => $"{value:F0} min",
                "$" => $"${value:N0}",
                "$/month" => $"${value:N0}/mo",
                _ => $"{value:F1} {Unit}"
            };
        }

        private string FormatImprovement()
        {
            var delta = Improvement;
            var prefix = delta >= 0 ? "+" : "";
            
            return Unit switch
            {
                "%" => $"{prefix}{delta:F1}%",
                "count" or "devices" => $"{prefix}{delta:N0}",
                "days" => $"{prefix}{delta:F1} days",
                "hours" => $"{prefix}{delta:F1} hours",
                "minutes" => $"{prefix}{delta:F0} min",
                "$" or "$/month" => $"{prefix}${Math.Abs(delta):N0}",
                _ => $"{prefix}{delta:F1}"
            };
        }

        private string GetImprovementColor()
        {
            var isPositive = HigherIsBetter ? Improvement > 0 : Improvement < 0;
            return isPositive ? "#10B981" : (Improvement == 0 ? "#6B7280" : "#EF4444");
        }
    }

    /// <summary>
    /// Aggregated impact for a single category.
    /// </summary>
    public class CategoryImpact
    {
        /// <summary>The category.</summary>
        public ImpactCategory Category { get; set; }

        /// <summary>Display name for the category.</summary>
        public string DisplayName => GetDisplayName();

        /// <summary>Icon for the category.</summary>
        public string Icon => GetIcon();

        /// <summary>Color theme for the category.</summary>
        public string Color => GetColor();

        /// <summary>Individual metrics within this category.</summary>
        public List<ImpactMetric> Metrics { get; set; } = new();

        /// <summary>Overall score for this category (0-100).</summary>
        public int CurrentScore { get; set; }

        /// <summary>Projected score after full enrollment.</summary>
        public int ProjectedScore { get; set; }

        /// <summary>Score improvement.</summary>
        public int ScoreImprovement => ProjectedScore - CurrentScore;

        /// <summary>Brief summary of the category impact.</summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>Top 3 benefits in this category.</summary>
        public List<string> TopBenefits { get; set; } = new();

        private string GetDisplayName() => Category switch
        {
            ImpactCategory.Security => "Security & Protection",
            ImpactCategory.OperationalEfficiency => "Operational Efficiency",
            ImpactCategory.UserExperience => "User Experience",
            ImpactCategory.CostOptimization => "Cost Optimization",
            ImpactCategory.ComplianceGovernance => "Compliance & Governance",
            ImpactCategory.Modernization => "Modernization",
            _ => Category.ToString()
        };

        private string GetIcon() => Category switch
        {
            ImpactCategory.Security => "🔒",
            ImpactCategory.OperationalEfficiency => "⚡",
            ImpactCategory.UserExperience => "👤",
            ImpactCategory.CostOptimization => "💰",
            ImpactCategory.ComplianceGovernance => "📋",
            ImpactCategory.Modernization => "🔄",
            _ => "📊"
        };

        private string GetColor() => Category switch
        {
            ImpactCategory.Security => "#DC2626",
            ImpactCategory.OperationalEfficiency => "#F59E0B",
            ImpactCategory.UserExperience => "#3B82F6",
            ImpactCategory.CostOptimization => "#10B981",
            ImpactCategory.ComplianceGovernance => "#8B5CF6",
            ImpactCategory.Modernization => "#06B6D4",
            _ => "#6B7280"
        };
    }

    /// <summary>
    /// Workload-specific impact analysis for co-management transition.
    /// </summary>
    public class WorkloadImpact
    {
        /// <summary>Workload name (e.g., "Compliance Policies", "Windows Update").</summary>
        public string WorkloadName { get; set; } = string.Empty;

        /// <summary>Icon for the workload.</summary>
        public string Icon { get; set; } = "⚙️";

        /// <summary>Whether this workload is currently managed by Intune.</summary>
        public bool IsCloudManaged { get; set; }

        /// <summary>Current state description.</summary>
        public string CurrentState { get; set; } = string.Empty;

        /// <summary>Benefits of transitioning this workload.</summary>
        public List<string> Benefits { get; set; } = new();

        /// <summary>Metrics that improve when this workload transitions.</summary>
        public List<ImpactMetric> ImpactMetrics { get; set; } = new();

        /// <summary>Estimated effort to transition (Low/Medium/High).</summary>
        public string TransitionEffort { get; set; } = "Medium";

        /// <summary>Recommended transition order (1 = first).</summary>
        public int RecommendedOrder { get; set; }
    }

    /// <summary>
    /// Complete migration impact analysis result.
    /// </summary>
    public class MigrationImpactResult
    {
        /// <summary>When the analysis was computed.</summary>
        public DateTime ComputedAt { get; set; } = DateTime.Now;

        /// <summary>Overall migration impact score (0-100).</summary>
        public int OverallCurrentScore { get; set; }

        /// <summary>Projected overall score after full migration.</summary>
        public int OverallProjectedScore { get; set; }

        /// <summary>Overall improvement.</summary>
        public int OverallImprovement => OverallProjectedScore - OverallCurrentScore;

        /// <summary>Impact broken down by category.</summary>
        public List<CategoryImpact> CategoryImpacts { get; set; } = new();

        /// <summary>Workload-specific impact analysis.</summary>
        public List<WorkloadImpact> WorkloadImpacts { get; set; } = new();

        /// <summary>All individual metrics across categories.</summary>
        public List<ImpactMetric> AllMetrics { get; set; } = new();

        /// <summary>Executive summary of the migration impact.</summary>
        public string ExecutiveSummary { get; set; } = string.Empty;

        /// <summary>Top 5 benefits across all categories.</summary>
        public List<ImpactHighlight> TopBenefits { get; set; } = new();

        /// <summary>Estimated timeline to realize benefits (based on current velocity).</summary>
        public string TimelineEstimate { get; set; } = string.Empty;

        /// <summary>Devices remaining to enroll.</summary>
        public int DevicesRemaining { get; set; }

        /// <summary>Current enrollment percentage.</summary>
        public double CurrentEnrollmentPercent { get; set; }

        /// <summary>Data quality score (0-100) - how much data was available for analysis.</summary>
        public int DataQualityScore { get; set; } = 100;

        /// <summary>Warnings or limitations in the analysis.</summary>
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// A highlighted benefit for display.
    /// </summary>
    public class ImpactHighlight
    {
        /// <summary>Highlight title.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Description of the benefit.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Category of the benefit.</summary>
        public ImpactCategory Category { get; set; }

        /// <summary>Icon for display.</summary>
        public string Icon { get; set; } = "✨";

        /// <summary>Quantified impact (e.g., "+35%", "247 devices").</summary>
        public string QuantifiedImpact { get; set; } = string.Empty;

        /// <summary>Color for the highlight.</summary>
        public string Color { get; set; } = "#10B981";
    }

    /// <summary>
    /// Input data for migration impact calculation.
    /// </summary>
    public class MigrationImpactInputs
    {
        // Device counts
        public int TotalDevices { get; set; }
        public int EnrolledDevices { get; set; }
        public int CoManagedDevices { get; set; }
        public int ConfigMgrOnlyDevices { get; set; }

        // Compliance data
        public double CurrentComplianceRate { get; set; }
        public int CompliantDevices { get; set; }
        public int NonCompliantDevices { get; set; }
        public int CompliancePoliciesDeployed { get; set; }

        // Security data
        public int EncryptedDevices { get; set; }
        public int DefenderManagedDevices { get; set; }
        public int ConditionalAccessReadyDevices { get; set; }
        public bool HasConditionalAccessPolicies { get; set; }

        // Infrastructure data
        public bool HasCMG { get; set; }
        public bool HasCoManagement { get; set; }
        public bool HasAutopilot { get; set; }
        public int DistributionPointCount { get; set; }
        public int SUPCount { get; set; }

        // Co-management workload status
        public bool ComplianceWorkloadInCloud { get; set; }
        public bool ResourceAccessWorkloadInCloud { get; set; }
        public bool WindowsUpdateWorkloadInCloud { get; set; }
        public bool EndpointProtectionWorkloadInCloud { get; set; }
        public bool DeviceConfigurationWorkloadInCloud { get; set; }
        public bool OfficeAppsWorkloadInCloud { get; set; }
        public bool ClientAppsWorkloadInCloud { get; set; }

        // OS data
        public int Windows11Devices { get; set; }
        public int Windows10Devices { get; set; }
        public int LegacyOSDevices { get; set; }

        // Activity data
        public int StaleDevices { get; set; }  // >7 days since check-in
        public int ActiveDevices { get; set; }
        public double AverageEnrollmentVelocity { get; set; }

        // User experience data
        public double AverageProvisioningTimeHours { get; set; }
        public int SelfServiceAppCount { get; set; }

        // Health data from ConfigMgr
        public int HealthyClients { get; set; }
        public int UnhealthyClients { get; set; }
        public double PatchComplianceRate { get; set; }
        
        // Data source indicator
        /// <summary>
        /// True if this data is demo/mock data (not connected), false if real data from Graph/ConfigMgr.
        /// </summary>
        public bool IsDemo { get; set; }
    }
}
