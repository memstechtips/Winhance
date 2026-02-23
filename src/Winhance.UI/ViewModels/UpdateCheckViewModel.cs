using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.ViewModels;

/// <summary>
/// Tracks the current state of the update InfoBar for language-change re-rendering.
/// </summary>
internal enum UpdateInfoBarState
{
    None,
    UpdateAvailable,
    NoUpdates,
    CheckError,
    Downloading
}

/// <summary>
/// Child ViewModel for update checking in the main window.
/// Manages update InfoBar state, check/install commands.
/// </summary>
public partial class UpdateCheckViewModel : ObservableObject
{
    private readonly IVersionService _versionService;
    private readonly IInternetConnectivityService _internetConnectivityService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogService _logService;

    [ObservableProperty]
    public partial bool IsUpdateInfoBarOpen { get; set; }

    [ObservableProperty]
    public partial string UpdateInfoBarTitle { get; set; }

    [ObservableProperty]
    public partial string UpdateInfoBarMessage { get; set; }

    // Tracks InfoBar state for re-rendering on language change
    private UpdateInfoBarState _updateInfoBarState = UpdateInfoBarState.None;
    private string _cachedCurrentVersion = string.Empty;
    private string _cachedLatestVersion = string.Empty;
    private string _cachedErrorMessage = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity UpdateInfoBarSeverity { get; set; }

    [ObservableProperty]
    public partial bool IsUpdateActionButtonVisible { get; set; }

    [ObservableProperty]
    public partial bool IsUpdateCheckInProgress { get; set; }

    public string InstallNowButtonText =>
        _localizationService.GetString("Dialog_Update_Button_InstallNow") ?? "Install Now";

    public UpdateCheckViewModel(
        IVersionService versionService,
        IInternetConnectivityService internetConnectivityService,
        ILocalizationService localizationService,
        ILogService logService)
    {
        _versionService = versionService;
        _internetConnectivityService = internetConnectivityService;
        _localizationService = localizationService;
        _logService = logService;

        UpdateInfoBarTitle = string.Empty;
        UpdateInfoBarMessage = string.Empty;

        _localizationService.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(InstallNowButtonText));
        if (IsUpdateInfoBarOpen)
        {
            RefreshUpdateInfoBarText();
        }
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (IsUpdateCheckInProgress) return;

        try
        {
            IsUpdateCheckInProgress = true;
            _logService.Log(Core.Features.Common.Enums.LogLevel.Info, "Checking for updates...");

            var latestVersion = await _versionService.CheckForUpdateAsync();
            var currentVersion = _versionService.GetCurrentVersion();

            if (latestVersion != null && latestVersion.IsUpdateAvailable)
            {
                _logService.Log(Core.Features.Common.Enums.LogLevel.Info, $"Update available: {latestVersion.Version}");

                _cachedCurrentVersion = currentVersion.Version;
                _cachedLatestVersion = latestVersion.Version;
                _updateInfoBarState = UpdateInfoBarState.UpdateAvailable;
                RefreshUpdateInfoBarText();
                UpdateInfoBarSeverity = InfoBarSeverity.Success;
                IsUpdateActionButtonVisible = true;
                IsUpdateInfoBarOpen = true;
            }
            else
            {
                _logService.Log(Core.Features.Common.Enums.LogLevel.Info, "No updates available");

                _updateInfoBarState = UpdateInfoBarState.NoUpdates;
                RefreshUpdateInfoBarText();
                UpdateInfoBarSeverity = InfoBarSeverity.Success;
                IsUpdateActionButtonVisible = false;
                IsUpdateInfoBarOpen = true;
            }
        }
        catch (Exception ex)
        {
            _logService.Log(Core.Features.Common.Enums.LogLevel.Error, $"Error checking for updates: {ex.Message}");

            _cachedErrorMessage = ex.Message;
            _updateInfoBarState = UpdateInfoBarState.CheckError;
            RefreshUpdateInfoBarText();
            UpdateInfoBarSeverity = InfoBarSeverity.Error;
            IsUpdateActionButtonVisible = false;
            IsUpdateInfoBarOpen = true;
        }
        finally
        {
            IsUpdateCheckInProgress = false;
        }
    }

    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        try
        {
            _updateInfoBarState = UpdateInfoBarState.Downloading;
            RefreshUpdateInfoBarText();
            IsUpdateActionButtonVisible = false;

            await _versionService.DownloadAndInstallUpdateAsync();
        }
        catch (Exception ex)
        {
            _logService.Log(Core.Features.Common.Enums.LogLevel.Error, $"Error installing update: {ex.Message}");

            var errorMessageTemplate = _localizationService.GetString("Dialog_Update_Status_Error") ?? "Error downloading update: {0}";
            var errorMessage = string.Format(errorMessageTemplate, ex.Message);

            UpdateInfoBarMessage = errorMessage;
            UpdateInfoBarSeverity = InfoBarSeverity.Error;
            IsUpdateActionButtonVisible = false;
        }
    }

    /// <summary>
    /// Silently checks for updates on startup. Only shows the InfoBar if an update is available.
    /// No-update and error scenarios are logged but not shown to the user.
    /// </summary>
    public async Task CheckForUpdatesOnStartupAsync()
    {
        try
        {
            var hasInternet = await _internetConnectivityService.IsInternetConnectedAsync(forceCheck: true);
            if (!hasInternet)
            {
                _logService.Log(Core.Features.Common.Enums.LogLevel.Info,
                    "Startup: No internet connection -- skipping update check");
                return;
            }

            _logService.Log(Core.Features.Common.Enums.LogLevel.Info, "Startup: Checking for updates...");

            var latestVersion = await _versionService.CheckForUpdateAsync();
            var currentVersion = _versionService.GetCurrentVersion();

            if (latestVersion != null && latestVersion.IsUpdateAvailable)
            {
                _logService.Log(Core.Features.Common.Enums.LogLevel.Info, $"Startup: Update available: {latestVersion.Version}");

                _cachedCurrentVersion = currentVersion.Version;
                _cachedLatestVersion = latestVersion.Version;
                _updateInfoBarState = UpdateInfoBarState.UpdateAvailable;
                RefreshUpdateInfoBarText();
                UpdateInfoBarSeverity = InfoBarSeverity.Success;
                IsUpdateActionButtonVisible = true;
                IsUpdateInfoBarOpen = true;
            }
            else
            {
                _logService.Log(Core.Features.Common.Enums.LogLevel.Info, "Startup: No updates available");
            }
        }
        catch (Exception ex)
        {
            _logService.Log(Core.Features.Common.Enums.LogLevel.Error, $"Startup: Error checking for updates: {ex.Message}");
        }
    }

    /// <summary>
    /// Dismisses the update InfoBar (called from code-behind on InfoBar.Closed).
    /// </summary>
    public void DismissUpdateInfoBar()
    {
        IsUpdateInfoBarOpen = false;
        _updateInfoBarState = UpdateInfoBarState.None;
    }

    /// <summary>
    /// Re-resolves InfoBar title/message from localization based on the current state.
    /// Called when the InfoBar state is first set and on language change.
    /// </summary>
    private void RefreshUpdateInfoBarText()
    {
        switch (_updateInfoBarState)
        {
            case UpdateInfoBarState.UpdateAvailable:
                var message = _localizationService.GetString("Dialog_Update_Message") ?? "Good News! A New Version of Winhance is available.";
                var currentVersionLabel = _localizationService.GetString("Dialog_Update_CurrentVersion") ?? "Current Version:";
                var latestVersionLabel = _localizationService.GetString("Dialog_Update_LatestVersion") ?? "Latest Version:";
                UpdateInfoBarTitle = _localizationService.GetString("Dialog_Update_Title") ?? "Update Available";
                UpdateInfoBarMessage = $"{message}  {currentVersionLabel} {_cachedCurrentVersion}  →  {latestVersionLabel} {_cachedLatestVersion}";
                break;

            case UpdateInfoBarState.NoUpdates:
                UpdateInfoBarTitle = _localizationService.GetString("Dialog_Update_NoUpdates_Title") ?? "No Updates Available";
                UpdateInfoBarMessage = _localizationService.GetString("Dialog_Update_NoUpdates_Message") ?? "You have the latest version of Winhance.";
                break;

            case UpdateInfoBarState.CheckError:
                UpdateInfoBarTitle = _localizationService.GetString("Dialog_Update_CheckError_Title") ?? "Update Check Error";
                var errorTemplate = _localizationService.GetString("Dialog_Update_CheckError_Message") ?? "An error occurred while checking for updates: {0}";
                UpdateInfoBarMessage = string.Format(errorTemplate, _cachedErrorMessage);
                break;

            case UpdateInfoBarState.Downloading:
                UpdateInfoBarMessage = _localizationService.GetString("Dialog_Update_Status_Downloading") ?? "Downloading update...";
                break;
        }
    }
}
