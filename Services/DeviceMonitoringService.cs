using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CloudJourneyAddin.Models;
using CloudJourneyAddin.Services.AgentTools;

namespace CloudJourneyAddin.Services
{
    /// <summary>
    /// Phase 3: Autonomous device monitoring service
    /// Continuously monitors devices and auto-enrolls when they become ready
    /// </summary>
    public class DeviceMonitoringService
    {
        private readonly GraphDataService _graphService;
        private readonly RiskAssessmentService _riskService;
        private readonly EnrollmentReActAgent _agent;
        private Timer? _monitoringTimer;
        private readonly Dictionary<string, MonitoredDevice> _deviceStates;
        private bool _isMonitoring;
        private readonly object _lock = new object();

        // Configuration
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15);
        private const double MIN_SCORE_FOR_AUTO_ENROLL = 60.0;  // Good or Excellent

        // Events for UI updates
        public event EventHandler<string>? StatusChanged;
        public event EventHandler<DeviceReadinessChangedEventArgs>? DeviceReadinessChanged;
        public event EventHandler<DeviceEnrolledEventArgs>? DeviceEnrolled;

        public bool IsMonitoring => _isMonitoring;

        public DeviceMonitoringService(
            GraphDataService graphService,
            RiskAssessmentService riskService,
            EnrollmentReActAgent agent)
        {
            _graphService = graphService;
            _riskService = riskService;
            _agent = agent;
            _deviceStates = new Dictionary<string, MonitoredDevice>();

            Logger.Instance.Info("DeviceMonitoringService initialized");
        }

        /// <summary>
        /// Start continuous monitoring
        /// </summary>
        public void StartMonitoring()
        {
            lock (_lock)
            {
                if (_isMonitoring)
                {
                    Logger.Instance.Warning("Monitoring already running");
                    return;
                }

                _isMonitoring = true;
                _monitoringTimer = new Timer(CheckDevicesCallback, null, TimeSpan.Zero, _checkInterval);

                Logger.Instance.Info($"🚗 Autonomous monitoring started - checking every {_checkInterval.TotalMinutes} minutes");
                StatusChanged?.Invoke(this, $"Monitoring active - next check in {_checkInterval.TotalMinutes} min");
            }
        }

        /// <summary>
        /// Stop monitoring
        /// </summary>
        public void StopMonitoring()
        {
            lock (_lock)
            {
                if (!_isMonitoring)
                    return;

                _isMonitoring = false;
                _monitoringTimer?.Dispose();
                _monitoringTimer = null;

                Logger.Instance.Info("Autonomous monitoring stopped");
                StatusChanged?.Invoke(this, "Monitoring stopped");
            }
        }

        /// <summary>
        /// Timer callback - checks all devices
        /// </summary>
        private async void CheckDevicesCallback(object? state)
        {
            if (!_isMonitoring)
                return;

            try
            {
                await CheckAllDevicesAsync();
            }
            catch (Exception ex)
            {
                Logger.Instance.LogException(ex, "CheckDevicesCallback");
            }
        }

        /// <summary>
        /// Check all devices for readiness changes
        /// </summary>
        private async Task CheckAllDevicesAsync()
        {
            try
            {
                Logger.Instance.Info("=== Starting device monitoring check ===");
                StatusChanged?.Invoke(this, "Checking device readiness...");

                // Get all unenrolled devices
                var enrollment = await _graphService.GetDeviceEnrollmentAsync();
                var unenrolledDeviceCount = enrollment.ConfigMgrOnlyDevices;

                Logger.Instance.Info($"Found {unenrolledDeviceCount} unenrolled devices to monitor");

                // In production, we'd enumerate actual device IDs from ConfigMgr
                // For now, we'll check devices we're already tracking
                var devicesToCheck = _deviceStates.Keys.ToList();

                if (devicesToCheck.Count == 0)
                {
                    Logger.Instance.Info("No devices currently tracked - add devices to monitoring via agent enrollment attempts");
                    StatusChanged?.Invoke(this, $"Monitoring {unenrolledDeviceCount} devices - waiting for status changes");
                    return;
                }

                int improvedCount = 0;
                int enrolledCount = 0;

                foreach (var deviceId in devicesToCheck)
                {
                    try
                    {
                        // Calculate current readiness
                        var currentReadiness = await _graphService.CalculateDeviceReadinessAsync(deviceId);
                        var previousState = _deviceStates[deviceId];

                        // Check if device improved
                        if (DeviceImprovedSignificantly(previousState.LastReadiness, currentReadiness))
                        {
                            improvedCount++;
                            Logger.Instance.Info($"📈 Device improved: {currentReadiness.DeviceName} ({previousState.LastReadiness.ReadinessScore:F0} → {currentReadiness.ReadinessScore:F0})");

                            // Raise event
                            DeviceReadinessChanged?.Invoke(this, new DeviceReadinessChangedEventArgs
                            {
                                DeviceId = deviceId,
                                DeviceName = currentReadiness.DeviceName,
                                PreviousScore = previousState.LastReadiness.ReadinessScore,
                                NewScore = currentReadiness.ReadinessScore,
                                PreviousLevel = previousState.LastReadiness.ReadinessLevel,
                                NewLevel = currentReadiness.ReadinessLevel
                            });

                            // Auto-enroll if now ready
                            if (ShouldAutoEnroll(currentReadiness))
                            {
                                var enrollResult = await AutoEnrollDeviceAsync(currentReadiness);
                                if (enrollResult.Success)
                                {
                                    enrolledCount++;
                                    // Remove from monitoring (now enrolled)
                                    _deviceStates.Remove(deviceId);
                                }
                            }
                        }

                        // Update tracked state
                        _deviceStates[deviceId].LastReadiness = currentReadiness;
                        _deviceStates[deviceId].LastChecked = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.LogException(ex, $"Check device {deviceId}");
                    }

                    // Small delay between device checks
                    await Task.Delay(100);
                }

                var summary = $"Check complete: {improvedCount} improved, {enrolledCount} auto-enrolled";
                Logger.Instance.Info(summary);
                StatusChanged?.Invoke(this, summary + $" - next check in {_checkInterval.TotalMinutes} min");
            }
            catch (Exception ex)
            {
                Logger.Instance.LogException(ex, "CheckAllDevicesAsync");
                StatusChanged?.Invoke(this, $"Error during check: {ex.Message}");
            }
        }

        /// <summary>
        /// Add device to monitoring
        /// </summary>
        public void AddDeviceToMonitoring(string deviceId, DeviceReadiness initialReadiness)
        {
            lock (_lock)
            {
                if (!_deviceStates.ContainsKey(deviceId))
                {
                    _deviceStates[deviceId] = new MonitoredDevice
                    {
                        DeviceId = deviceId,
                        DeviceName = initialReadiness.DeviceName,
                        LastReadiness = initialReadiness,
                        FirstSeen = DateTime.UtcNow,
                        LastChecked = DateTime.UtcNow
                    };

                    Logger.Instance.Info($"Added device to monitoring: {initialReadiness.DeviceName} (score: {initialReadiness.ReadinessScore:F0})");
                }
            }
        }

        /// <summary>
        /// Check if device improved significantly
        /// </summary>
        private bool DeviceImprovedSignificantly(DeviceReadiness previous, DeviceReadiness current)
        {
            // Improved if:
            // 1. Score increased by 10+ points
            // 2. Crossed into better readiness level (Fair→Good, Good→Excellent)

            var scoreImprovement = current.ReadinessScore - previous.ReadinessScore;
            if (scoreImprovement >= 10)
                return true;

            // Check level improvement
            var levels = new Dictionary<string, int>
            {
                ["Poor"] = 0,
                ["Fair"] = 1,
                ["Good"] = 2,
                ["Excellent"] = 3
            };

            if (levels.TryGetValue(previous.ReadinessLevel, out var prevLevel) &&
                levels.TryGetValue(current.ReadinessLevel, out var currLevel))
            {
                return currLevel > prevLevel;
            }

            return false;
        }

        /// <summary>
        /// Determine if device should be auto-enrolled
        /// </summary>
        private bool ShouldAutoEnroll(DeviceReadiness readiness)
        {
            // Phase 3: Auto-enroll if:
            // 1. Readiness score >= 60 (Good or Excellent)
            // 2. Risk assessment allows auto-approval

            if (readiness.ReadinessScore < MIN_SCORE_FOR_AUTO_ENROLL)
                return false;

            var riskAssessment = _riskService.AssessDeviceRisk(readiness);
            return !riskAssessment.RequiresApproval;
        }

        /// <summary>
        /// Auto-enroll a device that became ready
        /// </summary>
        private async Task<EnrollmentResult> AutoEnrollDeviceAsync(DeviceReadiness readiness)
        {
            try
            {
                Logger.Instance.Info($"🎯 Auto-enrolling device: {readiness.DeviceName} (score: {readiness.ReadinessScore:F0})");
                StatusChanged?.Invoke(this, $"Auto-enrolling {readiness.DeviceName}...");

                // Perform enrollment
                var result = await _graphService.EnrollDeviceAsync(readiness.DeviceId, readiness.DeviceName);

                if (result.Success)
                {
                    Logger.Instance.Info($"✅ Auto-enrollment succeeded: {readiness.DeviceName}");

                    // Raise event
                    DeviceEnrolled?.Invoke(this, new DeviceEnrolledEventArgs
                    {
                        Success = true,
                        DeviceId = readiness.DeviceId,
                        DeviceName = readiness.DeviceName,
                        ReadinessScore = readiness.ReadinessScore,
                        EnrolledAt = result.EnrolledAt ?? DateTime.UtcNow,
                        Message = "Automatically enrolled after readiness improvement"
                    });
                }
                else
                {
                    Logger.Instance.Warning($"❌ Auto-enrollment failed: {readiness.DeviceName} - {result.ErrorMessage}");

                    DeviceEnrolled?.Invoke(this, new DeviceEnrolledEventArgs
                    {
                        Success = false,
                        DeviceId = readiness.DeviceId,
                        DeviceName = readiness.DeviceName,
                        ReadinessScore = readiness.ReadinessScore,
                        Message = $"Auto-enrollment failed: {result.ErrorMessage}"
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogException(ex, $"AutoEnrollDeviceAsync: {readiness.DeviceName}");
                return new EnrollmentResult
                {
                    Success = false,
                    DeviceId = readiness.DeviceId,
                    DeviceName = readiness.DeviceName,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Get monitoring statistics
        /// </summary>
        public MonitoringStatistics GetStatistics()
        {
            lock (_lock)
            {
                return new MonitoringStatistics
                {
                    IsActive = _isMonitoring,
                    DevicesMonitored = _deviceStates.Count,
                    CheckInterval = _checkInterval,
                    NextCheckIn = _isMonitoring ? _checkInterval : TimeSpan.Zero,
                    MonitoredDevices = _deviceStates.Values.Select(d => new
                    {
                        d.DeviceName,
                        ReadinessScore = d.LastReadiness.ReadinessScore,
                        ReadinessLevel = d.LastReadiness.ReadinessLevel,
                        d.LastChecked
                    }).ToList()
                };
            }
        }
    }

    // Supporting classes
    internal class MonitoredDevice
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public DeviceReadiness LastReadiness { get; set; } = null!;
        public DateTime FirstSeen { get; set; }
        public DateTime LastChecked { get; set; }
    }

    public class MonitoringStatistics
    {
        public bool IsActive { get; set; }
        public int DevicesMonitored { get; set; }
        public TimeSpan CheckInterval { get; set; }
        public TimeSpan NextCheckIn { get; set; }
        public object MonitoredDevices { get; set; } = null!;
    }

    public class DeviceReadinessChangedEventArgs : EventArgs
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public double PreviousScore { get; set; }
        public double NewScore { get; set; }
        public string PreviousLevel { get; set; } = string.Empty;
        public string NewLevel { get; set; } = string.Empty;
    }

    public class DeviceEnrolledEventArgs : EventArgs
    {
        public bool Success { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public double ReadinessScore { get; set; }
        public DateTime EnrolledAt { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
