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

        public ICommand DownloadCommand { get; }
        public ICommand SkipCommand { get; }
        public ICommand RemindLaterCommand { get; }

        public bool DialogResult { get; set; }

        private async System.Threading.Tasks.Task DownloadUpdate()
        {
            IsDownloading = true;
            StatusMessage = "Downloading update...";
            
            try
            {
                var deltaService = new Services.DeltaUpdateService();
                var progress = new Progress<int>(percent =>
                {
                    DownloadProgress = percent;
                    StatusMessage = $"Downloading... {percent}%";
                });

                // Download the files
                var success = await deltaService.DownloadDeltaFilesAsync(
                    _updateInfo.DownloadUrl!,
                    _updateInfo.ChangedFiles,
                    progress);

                if (!success)
                {
                    StatusMessage = "Download failed. Please try again.";
                    IsDownloading = false;
                    return;
                }

                StatusMessage = "Applying update...";

                // Apply the update
                var applier = new Services.UpdateApplier();
                var remoteManifest = await deltaService.DownloadRemoteManifestAsync(_updateInfo.ManifestUrl!);
                
                if (remoteManifest != null)
                {
                    success = await applier.ApplyUpdateAsync(
                        deltaService.GetTempDownloadPath(),
                        _updateInfo.ChangedFiles,
                        remoteManifest);

                    if (success)
                    {
                        StatusMessage = "Update ready! Application will restart...";
                        DialogResult = true;
                        await System.Threading.Tasks.Task.Delay(2000);
                        
                        // Close will be handled by the window
                    }
                    else
                    {
                        StatusMessage = "Failed to apply update.";
                    }
                }
                else
                {
                    StatusMessage = "Failed to download manifest.";
                }
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
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
