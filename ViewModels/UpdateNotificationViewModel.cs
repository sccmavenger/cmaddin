using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ZeroTrustMigrationAddin.Models;

namespace ZeroTrustMigrationAddin.ViewModels
{
    /// <summary>
    /// ViewModel for the update notification window.
    /// </summary>
    public class UpdateNotificationViewModel : INotifyPropertyChanged
    {
        private readonly UpdateCheckResult _updateInfo;
        private bool _isDownloading;
        private int _downloadProgress;
        private string _statusMessage = string.Empty;
        private string _detailMessage = string.Empty;
        private UpdatePhase _currentPhase = UpdatePhase.Downloading;

        public event PropertyChangedEventHandler? PropertyChanged;

        public UpdateNotificationViewModel(UpdateCheckResult updateInfo)
        {
            _updateInfo = updateInfo;
            
            DownloadCommand = new RelayCommand(async () => await DownloadUpdate(), () => !IsDownloading);
            SkipCommand = new RelayCommand(() => SkipUpdate());
            RemindLaterCommand = new RelayCommand(() => RemindLater());
            
            StatusMessage = $"Version {updateInfo.LatestVersion} is available (Current: {updateInfo.CurrentVersion})";
        }

        public string CurrentVersion => _updateInfo.CurrentVersion;
        public string LatestVersion => _updateInfo.LatestVersion;
        public string ReleaseNotes => _updateInfo.ReleaseNotes ?? "No release notes available.";
        public string DeltaSizeFormatted => FormatBytes(_updateInfo.DeltaSize);
        public int ChangedFileCount => _updateInfo.ChangedFiles.Count;

        public bool IsDownloading
        {
            get => _isDownloading;
            set => SetProperty(ref _isDownloading, value);
        }

        public int DownloadProgress
        {
            get => _downloadProgress;
            set => SetProperty(ref _downloadProgress, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string DetailMessage
        {
            get => _detailMessage;
            set => SetProperty(ref _detailMessage, value);
        }

        public UpdatePhase CurrentPhase
        {
            get => _currentPhase;
            set => SetProperty(ref _currentPhase, value);
        }

        public ICommand DownloadCommand { get; }
        public ICommand SkipCommand { get; }
        public ICommand RemindLaterCommand { get; }

        public bool DialogResult { get; set; }

        private async System.Threading.Tasks.Task DownloadUpdate()
        {
            IsDownloading = true;
            StatusMessage = "🔍 Preparing update...";
            DetailMessage = "Checking installed files...";
            
            try
            {
                var deltaService = new Services.DeltaUpdateService();
                
                // Create detailed progress handler
                var detailedProgress = new Progress<UpdateProgress>(p =>
                {
                    CurrentPhase = p.Phase;
                    DownloadProgress = p.PercentComplete;
                    
                    // Set emoji and message based on phase
                    switch (p.Phase)
                    {
                        case UpdatePhase.Verifying:
                            StatusMessage = $"🔍 Verifying files... ({p.CurrentFileIndex} of {p.TotalFiles})";
                            DetailMessage = p.CurrentFile;
                            break;
                            
                        case UpdatePhase.Downloading:
                            StatusMessage = $"📥 Downloading... {p.PercentComplete}%";
                            DetailMessage = p.BytesProgressFormatted;
                            break;
                            
                        case UpdatePhase.Extracting:
                            StatusMessage = "📦 Extracting files...";
                            DetailMessage = $"{p.TotalFiles} files";
                            break;
                            
                        case UpdatePhase.Validating:
                            StatusMessage = $"✅ Validating... ({p.CurrentFileIndex} of {p.TotalFiles})";
                            DetailMessage = p.CurrentFile;
                            break;
                            
                        case UpdatePhase.BackingUp:
                            StatusMessage = "💾 Creating backup...";
                            DetailMessage = p.CurrentFile;
                            break;
                            
                        case UpdatePhase.Applying:
                            StatusMessage = $"🔄 Applying update... ({p.CurrentFileIndex} of {p.TotalFiles})";
                            DetailMessage = p.CurrentFile;
                            break;
                            
                        case UpdatePhase.Restarting:
                            StatusMessage = $"🚀 Restarting in {p.RestartCountdown}...";
                            DetailMessage = "Update complete!";
                            break;
                            
                        case UpdatePhase.Failed:
                            StatusMessage = $"❌ {p.StatusMessage}";
                            DetailMessage = p.ErrorMessage ?? "Unknown error";
                            break;
                    }
                });

                // Download the files with detailed progress
                var success = await deltaService.DownloadDeltaFilesAsync(
                    _updateInfo.DownloadUrl!,
                    _updateInfo.ChangedFiles,
                    detailedProgress);

                if (!success)
                {
                    StatusMessage = "❌ Download failed";
                    DetailMessage = "Please check your connection and try again.";
                    IsDownloading = false;
                    return;
                }

                StatusMessage = "💾 Creating backup...";
                DetailMessage = "Preparing to apply update...";

                // Apply the update
                var applier = new Services.UpdateApplier();
                var remoteManifest = await deltaService.DownloadRemoteManifestAsync(_updateInfo.ManifestUrl!);
                
                if (remoteManifest != null)
                {
                    StatusMessage = "🔄 Applying update...";
                    DetailMessage = $"Updating {_updateInfo.ChangedFiles.Count} files...";
                    
                    success = await applier.ApplyUpdateAsync(
                        deltaService.GetTempDownloadPath(),
                        _updateInfo.ChangedFiles,
                        remoteManifest);

                    if (success)
                    {
                        // Countdown before restart
                        for (int i = 3; i >= 1; i--)
                        {
                            StatusMessage = $"🚀 Restarting in {i}...";
                            DetailMessage = "Update complete!";
                            DownloadProgress = 100;
                            await System.Threading.Tasks.Task.Delay(1000);
                        }
                        
                        DialogResult = true;
                        // Close will be handled by the window
                    }
                    else
                    {
                        StatusMessage = "❌ Failed to apply update";
                        DetailMessage = "Check Update.log for details.";
                    }
                }
                else
                {
                    StatusMessage = "❌ Failed to download manifest";
                    DetailMessage = "Could not verify update package.";
                }
            }
            catch (System.Exception ex)
            {
                StatusMessage = "❌ Update failed";
                DetailMessage = ex.Message;
                Services.FileLogger.Instance.Error($"Update download failed: {ex.Message}");
            }
            finally
            {
                IsDownloading = false;
            }
        }

        private void SkipUpdate()
        {
            var updateService = new Services.GitHubUpdateService();
            updateService.SkipVersion(_updateInfo.LatestVersion);
            DialogResult = false;
        }

        private void RemindLater()
        {
            DialogResult = false;
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }

        protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (!System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
