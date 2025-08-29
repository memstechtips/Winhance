using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.WPF.Features.Common.ViewModels
{
    public partial class UpdateNotificationViewModel : ObservableObject
    {
        private readonly IVersionService _versionService;
        private readonly ILogService _logService;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private string _currentVersion = string.Empty;

        [ObservableProperty]
        private string _latestVersion = string.Empty;

        [ObservableProperty]
        private bool _isUpdateAvailable;

        [ObservableProperty]
        private bool _isDownloading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public UpdateNotificationViewModel(
            IVersionService versionService,
            ILogService logService,
            IDialogService dialogService)
        {
            _versionService = versionService;
            _logService = logService;
            _dialogService = dialogService;

            VersionInfo currentVersion = _versionService.GetCurrentVersion();
            CurrentVersion = currentVersion.Version;
        }

        [RelayCommand]
        private async Task CheckForUpdateAsync()
        {
            try
            {
                StatusMessage = "Checking for updates...";

                VersionInfo latestVersion = await _versionService.CheckForUpdateAsync();
                LatestVersion = latestVersion.Version;
                IsUpdateAvailable = latestVersion.IsUpdateAvailable;

                StatusMessage = IsUpdateAvailable
                    ? $"Update available: {LatestVersion}"
                    : "You have the latest version.";

                return;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking for updates: {ex.Message}", ex);
                StatusMessage = "Error checking for updates.";
            }
        }

        [RelayCommand]
        private async Task DownloadAndInstallUpdateAsync()
        {
            if (!IsUpdateAvailable)
                return;

            try
            {
                IsDownloading = true;
                StatusMessage = "Downloading update...";

                await _versionService.DownloadAndInstallUpdateAsync();

                StatusMessage = "Update downloaded. Installing...";

                // Notify the user that the application will close
                await _dialogService.ShowInformationAsync(
                    "The installer has been launched. The application will now close.",
                    "Update");

                // Close the application
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error downloading update: {ex.Message}", ex);
                StatusMessage = "Error downloading update.";
                IsDownloading = false;
            }
        }

        [RelayCommand]
        private void RemindLater()
        {
            // Just close the dialog, will check again next time
        }
    }
}
