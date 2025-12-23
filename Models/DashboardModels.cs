using System;
using System.Collections.Generic;

namespace CloudJourneyAddin.Models
{
    public class MigrationStatus
    {
        public int WorkloadsTransitioned { get; set; }
        public int TotalWorkloads { get; set; }
        public double CompletionPercentage => TotalWorkloads > 0 ? (double)WorkloadsTransitioned / TotalWorkloads * 100 : 0;
        public DateTime? ProjectedFinishDate { get; set; }
        public DateTime LastUpdateDate { get; set; }
    }

    public class DeviceEnrollment
    {
        public int TotalDevices { get; set; }
        public int IntuneEnrolledDevices { get; set; }
        public int ConfigMgrOnlyDevices { get; set; }
        public double IntuneEnrollmentPercentage => TotalDevices > 0 ? (double)IntuneEnrolledDevices / TotalDevices * 100 : 0;
        public EnrollmentTrend[] TrendData { get; set; } = Array.Empty<EnrollmentTrend>();
    }

    public class EnrollmentTrend
    {
        public DateTime Month { get; set; }
        public int IntuneDevices { get; set; }
        public int ConfigMgrDevices { get; set; }
    }

    public class Workload
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public WorkloadStatus Status { get; set; }
        public string LearnMoreUrl { get; set; } = string.Empty;
        public DateTime? TransitionDate { get; set; }
    }

    public enum WorkloadStatus
    {
        NotStarted,
        InProgress,
        Completed
    }

    public class ComplianceScore
    {
        public double IntuneScore { get; set; }
        public double ConfigMgrScore { get; set; }
        public string[] RiskAreas { get; set; } = Array.Empty<string>();
        public int DevicesLackingConditionalAccess { get; set; }
    }

    public class ComplianceDashboard
    {
        public double OverallComplianceRate { get; set; }
        public int TotalDevices { get; set; }
        public int CompliantDevices { get; set; }
        public int NonCompliantDevices { get; set; }
        public int PolicyViolations { get; set; }
    }

    // Renamed from PeerBenchmark to EnrollmentAccelerationInsight
    // Focus: AI insights to help customers match or exceed peer enrollment velocity
    public class EnrollmentAccelerationInsight
    {
        public double YourWeeklyEnrollmentRate { get; set; }
        public double PeerAverageRate { get; set; }
        public int DevicesNeededToMatchPeers { get; set; }
        public string RecommendedAction { get; set; } = string.Empty;
        public string OrganizationCategory { get; set; } = string.Empty;
        public List<string> SpecificTactics { get; set; } = new();
        public int EstimatedWeeksToMatchPeers { get; set; }
    }

    // Renamed from ROIData to SavingsUnlockInsight
    // Focus: Show what actions will unlock savings (entice enrollment/workload transition)
    public class SavingsUnlockInsight
    {
        public decimal SavingsLockedBehindEnrollment { get; set; }
        public int DevicesNeededToUnlock { get; set; }
        public string NextSavingsMilestone { get; set; } = string.Empty;
        public decimal SavingsFromWorkloadTransition { get; set; }
        public string WorkloadToTransition { get; set; } = string.Empty;
        public string RecommendedAction { get; set; } = string.Empty;
    }

    public class Alert
    {
        public AlertSeverity Severity { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ActionText { get; set; } = string.Empty;
        public DateTime DetectedDate { get; set; }
    }

    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical
    }

    // Renamed from Milestone to ProgressTarget
    // Focus: Forward-looking targets with specific actions to achieve them
    public class ProgressTarget
    {
        public string TargetDescription { get; set; } = string.Empty;
        public DateTime TargetDate { get; set; }
        public int DevicesRequired { get; set; }
        public int WorkloadsRequired { get; set; }
        public string ActionToAchieve { get; set; } = string.Empty;
        public bool IsEnrollmentTarget { get; set; }
        public bool IsWorkloadTarget { get; set; }
        public string Icon { get; set; } = string.Empty;
    }

    public class Blocker
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int AffectedDevices { get; set; }
        public string RemediationUrl { get; set; } = string.Empty;
        public BlockerSeverity Severity { get; set; }
    }

    public enum BlockerSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class EngagementOption
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
    }

    // ===== ENROLLMENT MOMENTUM MODELS =====
    public class EnrollmentMomentumInsight
    {
        public int CurrentVelocity { get; set; }
        public int RecommendedVelocity { get; set; }
        public int OptimalBatchSize { get; set; }
        public string VelocityAssessment { get; set; } = string.Empty;
        public List<string> InfrastructureBlockers { get; set; } = new();
        public List<AccelerationStrategy> AccelerationStrategies { get; set; } = new();
        public List<WeeklyTarget> WeekByWeekRoadmap { get; set; } = new();
        public int ProjectedCompletionWeeks { get; set; }
        public string Rationale { get; set; } = string.Empty;
        public bool IsAIPowered { get; set; }
    }

    public class AccelerationStrategy
    {
        public string Action { get; set; } = string.Empty;
        public string Impact { get; set; } = string.Empty;
        public string EffortLevel { get; set; } = string.Empty;
    }

    public class WeeklyTarget
    {
        public int Week { get; set; }
        public int TargetDevices { get; set; }
        public string FocusArea { get; set; } = string.Empty;
    }

    // ===== WORKLOAD MOMENTUM MODELS =====
    public class WorkloadMomentumInsight
    {
        public string RecommendedWorkload { get; set; } = string.Empty;
        public string Rationale { get; set; } = string.Empty;
        public double ReadinessScore { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
        public List<string> Prerequisites { get; set; } = new();
        public List<string> SuccessFactors { get; set; } = new();
        public List<WorkloadTransitionStep> TransitionRoadmap { get; set; } = new();
        public int EstimatedWeeks { get; set; }
        public bool IsAIPowered { get; set; }
    }

    public class WorkloadTransitionStep
    {
        public int Week { get; set; }
        public string Phase { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string ValidationCriteria { get; set; } = string.Empty;
    }

    // ===== AI ACTION SUMMARY MODELS =====
    // Renamed from ExecutiveSummary to AIActionSummary
    // Focus: AI-powered actionable recommendations for enrollment and workload transition
    public class AIActionSummary
    {
        public string PrimaryEnrollmentAction { get; set; } = string.Empty;
        public int EnrollmentActionImpact { get; set; } // Devices affected
        public string PrimaryWorkloadAction { get; set; } = string.Empty;
        public string WorkloadActionImpact { get; set; } = string.Empty; // e.g., "Unlock Conditional Access"
        public List<string> EnrollmentBlockers { get; set; } = new();
        public List<string> WorkloadBlockers { get; set; } = new();
        public string AIRecommendation { get; set; } = string.Empty;
        public int WeeksToNextMilestone { get; set; }
        public bool IsAIPowered { get; set; }
    }

    // BACKWARD COMPATIBILITY - Keep old ExecutiveSummary model for existing services
    public class ExecutiveSummary
    {
        public int MigrationHealthScore { get; set; } // 0-100
        public string OverallStatus { get; set; } = string.Empty; // "On Track" / "At Risk" / "Stalled"
        public List<string> KeyAchievements { get; set; } = new();
        public List<string> CriticalIssues { get; set; } = new();
        public string NextCriticalAction { get; set; } = string.Empty;
        public int ProjectedCompletionDays { get; set; }
        public double SuccessProbability { get; set; } // 0-100
        public string ExecutiveSummaryText { get; set; } = string.Empty;
        public bool IsAIPowered { get; set; }
    }

    // BACKWARD COMPATIBILITY - Keep old Milestone model for existing services
    public class Milestone
    {
        public string Title { get; set; } = string.Empty;
        public DateTime AchievedDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }

    // ===== CONFIGMGR DATA MODELS =====
    public class ConfigMgrApplicationData
    {
        public List<ConfigMgrApplicationInfo> Applications { get; set; } = new();
        public int TotalApplications { get; set; }
        public int DeployedApplications { get; set; }
        public int SupersededApplications { get; set; }
    }

    public class ConfigMgrApplicationInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public int DeploymentTypeCount { get; set; }
        public bool IsDeployed { get; set; }
        public bool IsSuperseded { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateLastModified { get; set; }
        public string MigrationComplexity { get; set; } = "Unknown"; // Low, Medium, High
    }

    public class ConfigMgrHardwareInventory
    {
        public List<DeviceHardwareInfo> Devices { get; set; } = new();
        public Dictionary<string, int> ManufacturerCounts { get; set; } = new();
        public Dictionary<string, int> ModelCounts { get; set; } = new();
    }

    public class DeviceHardwareInfo
    {
        public int ResourceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string SystemType { get; set; } = string.Empty;
        public int EstimatedAgeYears { get; set; }
    }

    public class ConfigMgrClientHealthSummary
    {
        public int TotalClients { get; set; }
        public int HealthyClients { get; set; }
        public int UnhealthyClients { get; set; }
        public int InactiveClients { get; set; }
        public double HealthPercentage => TotalClients > 0 ? (double)HealthyClients / TotalClients * 100 : 0;
        public List<ClientHealthDetail> Details { get; set; } = new();
    }

    public class ClientHealthDetail
    {
        public int ResourceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public bool IsHealthy { get; set; }
        public DateTime? LastActiveTime { get; set; }
        public DateTime? LastPolicyRequest { get; set; }
        public DateTime? LastHardwareScan { get; set; }
        public List<string> HealthIssues { get; set; } = new();
    }

    public class ConfigMgrComplianceSummary
    {
        public int TotalDevices { get; set; }
        public int CompliantDevices { get; set; }
        public int NonCompliantDevices { get; set; }
        public int UnknownDevices { get; set; }
        public double CompliancePercentage => TotalDevices > 0 ? (double)CompliantDevices / TotalDevices * 100 : 0;
    }

    // ===== INTUNE/GRAPH DATA MODELS =====
    public class IntuneAppDeploymentSummary
    {
        public List<AppDeploymentDetail> Deployments { get; set; } = new();
        public int TotalApps { get; set; }
        public int SuccessfulDeployments { get; set; }
        public int FailedDeployments { get; set; }
        public int PendingDeployments { get; set; }
    }

    public class AppDeploymentDetail
    {
        public string AppName { get; set; } = string.Empty;
        public string AppId { get; set; } = string.Empty;
        public int TargetDeviceCount { get; set; }
        public int InstalledCount { get; set; }
        public int FailedCount { get; set; }
        public int PendingCount { get; set; }
        public double SuccessRate => TargetDeviceCount > 0 ? (double)InstalledCount / TargetDeviceCount * 100 : 0;
    }

    public class IntuneUpdateRingSummary
    {
        public List<UpdateRingDetail> UpdateRings { get; set; } = new();
        public int TotalPolicies { get; set; }
        public int DevicesCovered { get; set; }
        public int DevicesCompliant { get; set; }
    }

    public class UpdateRingDetail
    {
        public string PolicyName { get; set; } = string.Empty;
        public string PolicyId { get; set; } = string.Empty;
        public int AssignedDeviceCount { get; set; }
        public int CompliantDeviceCount { get; set; }
        public int NonCompliantDeviceCount { get; set; }
        public double ComplianceRate => AssignedDeviceCount > 0 ? (double)CompliantDeviceCount / AssignedDeviceCount * 100 : 0;
    }

    public class IntuneConfigProfileSummary
    {
        public List<ConfigProfileDetail> Profiles { get; set; } = new();
        public int TotalProfiles { get; set; }
        public int DevicesCovered { get; set; }
        public int SuccessfulApplications { get; set; }
    }

    public class ConfigProfileDetail
    {
        public string ProfileName { get; set; } = string.Empty;
        public string ProfileId { get; set; } = string.Empty;
        public string ProfileType { get; set; } = string.Empty;
        public int AssignedDeviceCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public int PendingCount { get; set; }
    }

    public class AutopilotSummary
    {
        public int TotalDevices { get; set; }
        public int EnrolledDevices { get; set; }
        public int PendingDevices { get; set; }
        public int FailedDevices { get; set; }
        public List<AutopilotDeviceInfo> Devices { get; set; } = new();
    }

    public class AutopilotDeviceInfo
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string EnrollmentState { get; set; } = string.Empty;
        public string GroupTag { get; set; } = string.Empty;
        public DateTime? LastContactedDateTime { get; set; }
        public bool HasDeploymentProfile { get; set; }
    }

    public class DeviceCertificateSummary
    {
        public int TotalDevices { get; set; }
        public int DevicesWithCertificates { get; set; }
        public int CertificateProfiles { get; set; }
        public int SuccessfulDeployments { get; set; }
        public int FailedDeployments { get; set; }
        public List<CertificateProfileDetail> Profiles { get; set; } = new();
    }

    public class CertificateProfileDetail
    {
        public string ProfileName { get; set; } = string.Empty;
        public string CertificateType { get; set; } = string.Empty; // SCEP, PKCS, Trusted
        public int DeviceCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
    }

    public class DeviceNetworkSummary
    {
        public int TotalDevices { get; set; }
        public int WiFiDevices { get; set; }
        public int EthernetDevices { get; set; }
        public int EncryptedDevices { get; set; }
        public List<NetworkDeviceInfo> Devices { get; set; } = new();
    }

    public class NetworkDeviceInfo
    {
        public string DeviceName { get; set; } = string.Empty;
        public string ConnectionType { get; set; } = string.Empty; // WiFi, Ethernet, Both
        public bool IsEncrypted { get; set; }
        public string WiFiMacAddress { get; set; } = string.Empty;
        public string EthernetMacAddress { get; set; } = string.Empty;
    }

    // Device Readiness Models (v2.6.0 - Enrollment Tab Enhancements)
    public class DeviceReadinessBreakdown
    {
        public int HighSuccessDevices { get; set; }
        public double HighSuccessHealthAvg { get; set; }
        public double HighSuccessPredictedRate { get; set; }
        public int HighSuccessRecommendedVelocity { get; set; }

        public int ModerateSuccessDevices { get; set; }
        public double ModerateSuccessHealthAvg { get; set; }
        public double ModerateSuccessPredictedRate { get; set; }
        public int ModerateSuccessRecommendedVelocity { get; set; }

        public int HighRiskDevices { get; set; }
        public double HighRiskHealthAvg { get; set; }
        public double HighRiskPredictedRate { get; set; }
        public string HighRiskRecommendation { get; set; } = string.Empty;

        public List<DeviceReadinessDetail> HighSuccessDeviceList { get; set; } = new();
        public List<DeviceReadinessDetail> ModerateSuccessDeviceList { get; set; } = new();
        public List<DeviceReadinessDetail> HighRiskDeviceList { get; set; } = new();
    }

    public class DeviceReadinessDetail
    {
        public string DeviceName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public double HealthScore { get; set; }
        public DateTime? LastActiveTime { get; set; }
        public string OperatingSystem { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public List<string> Issues { get; set; } = new();
    }

    public class EnrollmentBlockerSummary
    {
        public int TotalBlockedDevices { get; set; }
        public int EnrollableDevices { get; set; }
        public List<EnrollmentBlockerCategory> BlockerCategories { get; set; } = new();
    }

    public class EnrollmentBlockerCategory
    {
        public string BlockerType { get; set; } = string.Empty; // "Unsupported OS", "No TPM", "Client Not Responding", "No Connectivity"
        public int DeviceCount { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<string> AffectedDevices { get; set; } = new();
    }
}
