using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.UI.Features.Common.Interfaces;

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
/// Event arguments for filter state changes.
/// </summary>
public class FilterStateChangedEventArgs : EventArgs
{
    public bool IsFilterEnabled { get; }
    public FilterStateChangedEventArgs(bool isFilterEnabled) => IsFilterEnabled = isFilterEnabled;
}

/// <summary>
/// ViewModel for the MainWindow, handling title bar commands and state.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IThemeService _themeService;
    private readonly IConfigurationService _configurationService;
    private readonly ILocalizationService _localizationService;
    private readonly IVersionService _versionService;
    private readonly ILogService _logService;
    private readonly IDialogService _dialogService;
    private readonly IUserPreferencesService _preferencesService;
    private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;
    private readonly ITaskProgressService _taskProgressService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IConfigReviewService _configReviewService;
    private readonly IWinGetService _winGetService;

    [ObservableProperty]
    private string _appIconSource = "ms-appx:///Assets/AppIcons/winhance-rocket-white-transparent-bg.png";

    [ObservableProperty]
    private string _versionInfo = "Winhance";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowsFilterTooltip))]
    [NotifyPropertyChangedFor(nameof(WindowsFilterIcon))]
    private bool _isWindowsVersionFilterEnabled = true;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _appName = string.Empty;

    [ObservableProperty]
    private string _lastTerminalLine = string.Empty;

    [ObservableProperty]
    private bool _isUpdateInfoBarOpen;

    [ObservableProperty]
    private string _updateInfoBarTitle = string.Empty;

    [ObservableProperty]
    private string _updateInfoBarMessage = string.Empty;

    // Tracks InfoBar state for re-rendering on language change
    private UpdateInfoBarState _updateInfoBarState = UpdateInfoBarState.None;
    private string _cachedCurrentVersion = string.Empty;
    private string _cachedLatestVersion = string.Empty;
    private string _cachedErrorMessage = string.Empty;

    [ObservableProperty]
    private InfoBarSeverity _updateInfoBarSeverity;

    [ObservableProperty]
    private bool _isUpdateActionButtonVisible;

    [ObservableProperty]
    private bool _isUpdateCheckInProgress;

    // Review mode properties
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWindowsFilterButtonEnabled))]
    private bool _isInReviewMode;

    [ObservableProperty]
    private string _reviewModeStatusText = string.Empty;

    [ObservableProperty]
    private bool _canApplyReviewedConfig;



    /// <summary>
    /// Event raised when the Windows version filter state changes.
    /// </summary>
    public event EventHandler<FilterStateChangedEventArgs>? FilterStateChanged;

    public MainWindowViewModel(
        IThemeService themeService,
        IConfigurationService configurationService,
        ILocalizationService localizationService,
        IVersionService versionService,
        ILogService logService,
        IDialogService dialogService,
        IUserPreferencesService preferencesService,
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        ITaskProgressService taskProgressService,
        IDispatcherService dispatcherService,
        IConfigReviewService configReviewService,
        IWinGetService winGetService)
    {
        _themeService = themeService;
        _configurationService = configurationService;
        _localizationService = localizationService;
        _versionService = versionService;
        _logService = logService;
        _dialogService = dialogService;
        _preferencesService = preferencesService;
        _compatibleSettingsRegistry = compatibleSettingsRegistry;
        _taskProgressService = taskProgressService;
        _dispatcherService = dispatcherService;
        _configReviewService = configReviewService;
        _winGetService = winGetService;

        // Subscribe to theme changes
        _themeService.ThemeChanged += OnThemeChanged;

        // Subscribe to language changes
        _localizationService.LanguageChanged += OnLanguageChanged;

        // Subscribe to task progress updates
        _taskProgressService.ProgressUpdated += OnProgressUpdated;

        // Subscribe to review mode changes
        _configReviewService.ReviewModeChanged += OnReviewModeChanged;
        _configReviewService.ApprovalCountChanged += OnApprovalCountChanged;
        _configReviewService.BadgeStateChanged += OnBadgeStateChangedForApplyButton;

        // Set initial icon based on current theme
        UpdateAppIconForTheme();

        // Initialize version info
        InitializeVersionInfo();
    }

    /// <summary>
    /// Handles language changes to update localized strings.
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Notify all localized string properties
        OnPropertyChanged(nameof(AppTitle));
        OnPropertyChanged(nameof(AppSubtitle));
        OnPropertyChanged(nameof(SaveConfigTooltip));
        OnPropertyChanged(nameof(ImportConfigTooltip));
        OnPropertyChanged(nameof(WindowsFilterTooltip));
        OnPropertyChanged(nameof(ToggleNavigationTooltip));
        OnPropertyChanged(nameof(DonateTooltip));
        OnPropertyChanged(nameof(BugReportTooltip));
        OnPropertyChanged(nameof(DocsTooltip));

        // Nav bar text
        OnPropertyChanged(nameof(NavSoftwareAppsText));
        OnPropertyChanged(nameof(NavOptimizeText));
        OnPropertyChanged(nameof(NavCustomizeText));
        OnPropertyChanged(nameof(NavAdvancedToolsText));
        OnPropertyChanged(nameof(NavSettingsText));
        OnPropertyChanged(nameof(NavMoreText));

        // Task progress
        OnPropertyChanged(nameof(CancelButtonLabel));

        // Update InfoBar
        OnPropertyChanged(nameof(InstallNowButtonText));
        if (IsUpdateInfoBarOpen)
        {
            RefreshUpdateInfoBarText();
        }
    }

    /// <summary>
    /// Initializes the version info string from the version service.
    /// </summary>
    private void InitializeVersionInfo()
    {
        try
        {
            var versionInfo = _versionService.GetCurrentVersion();
            VersionInfo = $"Winhance {versionInfo.Version}";
        }
        catch
        {
            VersionInfo = "Winhance";
        }
    }

    #region Localized Strings

    // App title bar
    public string AppTitle =>
        _localizationService.GetString("App_Title") ?? "Winhance";

    public string AppSubtitle =>
        _localizationService.GetString("App_By") ?? "by Memory";

    // Tooltips
    public string SaveConfigTooltip =>
        _localizationService.GetString("Tooltip_SaveConfiguration") ?? "Save Configuration";

    public string ImportConfigTooltip =>
        _localizationService.GetString("Tooltip_ImportConfig") ?? "Import Configuration";

    public string WindowsFilterTooltip
    {
        get
        {
            if (IsWindowsVersionFilterEnabled)
            {
                var title = _localizationService.GetString("Tooltip_FilterEnabled") ?? "Windows Version Filter: ON";
                var description = _localizationService.GetString("Tooltip_FilterEnabled_Description") ?? "Click to show settings for all Windows versions";
                return $"{title}\n{description}";
            }
            else
            {
                var title = _localizationService.GetString("Tooltip_FilterDisabled") ?? "Windows Version Filter: OFF";
                var description = _localizationService.GetString("Tooltip_FilterDisabled_Description") ?? "Showing all settings (incompatible settings marked)";
                return $"{title}\n{description}";
            }
        }
    }

    /// <summary>
    /// Gets the icon path data for the Windows filter button based on filter state.
    /// Filter ON = filter-check icon (showing filtered/compatible only)
    /// Filter OFF = filter-off icon (showing all settings)
    /// Path data is retrieved from Application resources (FeatureIcons.xaml).
    /// </summary>
    public string WindowsFilterIcon
    {
        get
        {
            var resourceKey = IsWindowsVersionFilterEnabled ? "FilterCheckIconPath" : "FilterOffIconPath";
            return Application.Current.Resources[resourceKey] as string ?? string.Empty;
        }
    }

    public string ToggleNavigationTooltip =>
        _localizationService.GetString("Tooltip_ToggleNavigation") ?? "Toggle Navigation";

    public string DonateTooltip =>
        _localizationService.GetString("Tooltip_Donate") ?? "Donate";

    public string BugReportTooltip =>
        _localizationService.GetString("Tooltip_ReportBug") ?? "Report a Bug";

    public string DocsTooltip =>
        _localizationService.GetString("Tooltip_Documentation") ?? "Documentation";

    // Nav bar text
    public string NavSoftwareAppsText =>
        _localizationService.GetString("Nav_SoftwareAndApps") ?? "Software & Apps";

    public string NavOptimizeText =>
        _localizationService.GetString("Nav_Optimize") ?? "Optimize";

    public string NavCustomizeText =>
        _localizationService.GetString("Nav_Customize") ?? "Customize";

    public string NavAdvancedToolsText =>
        _localizationService.GetString("Nav_AdvancedTools") ?? "Advanced Tools";

    public string NavSettingsText =>
        _localizationService.GetString("Nav_Settings") ?? "Settings";

    public string NavMoreText =>
        _localizationService.GetString("Nav_More") ?? "More";

    // Task progress
    public string CancelButtonLabel =>
        _localizationService.GetString("Button_Cancel") ?? "Cancel";

    // Update InfoBar
    public string InstallNowButtonText =>
        _localizationService.GetString("Dialog_Update_Button_InstallNow") ?? "Install Now";

    // Filter button enabled state
    public bool IsWindowsFilterButtonEnabled => !IsInReviewMode;

    // Review Mode
    public string ReviewModeTitleText =>
        _localizationService.GetString("Review_Mode_Title") ?? "Config Review Mode";
    public string ReviewModeApplyButtonText =>
        _localizationService.GetString("Review_Mode_Apply_Button") ?? "Apply Config";
    public string ReviewModeCancelButtonText =>
        _localizationService.GetString("Review_Mode_Cancel_Button") ?? "Cancel";
    public string ReviewModeDescriptionText =>
        _localizationService.GetString("Review_Mode_Description") ?? "Review the changes below across all sections, then click Apply Config when ready.";

    #endregion

    #region Commands

    /// <summary>
    /// Command to export/save configuration.
    /// </summary>
    [RelayCommand]
    private async Task SaveConfigAsync()
    {
        try
        {
            await _configurationService.ExportConfigurationAsync();
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Failed to save configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// Command to import configuration.
    /// </summary>
    [RelayCommand]
    private async Task ImportConfigAsync()
    {
        try
        {
            await _configurationService.ImportConfigurationAsync();
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Failed to import configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// Command to toggle Windows version filter.
    /// </summary>
    [RelayCommand]
    private async Task ToggleWindowsFilterAsync()
    {
        // Don't allow toggling during review mode
        if (IsInReviewMode) return;

        try
        {
            // Check if we should show explanation dialog
            var dontShowAgain = await _preferencesService.GetPreferenceAsync(
                UserPreferenceKeys.DontShowFilterExplanation, defaultValue: false);

            if (!dontShowAgain)
            {
                var message = _localizationService.GetString("Filter_Dialog_Message") ??
                    "The Windows Version Filter controls which settings are shown based on your Windows version.\n\nWhen ON: Only settings compatible with your Windows version are shown.\nWhen OFF: All settings are shown, with incompatible ones marked.";
                var checkboxText = _localizationService.GetString("Filter_Dialog_Checkbox") ?? "Don't show this message again";
                var title = _localizationService.GetString("Filter_Dialog_Title") ?? "Windows Version Filter";
                var continueText = _localizationService.GetString("Filter_Dialog_Button_Toggle") ?? "Toggle Filter";
                var cancelText = _localizationService.GetString("Button_Cancel") ?? "Cancel";

                var result = await _dialogService.ShowConfirmationWithCheckboxAsync(
                    message,
                    checkboxText: checkboxText,
                    title: title,
                    continueButtonText: continueText,
                    cancelButtonText: cancelText);

                if (result.CheckboxChecked)
                {
                    await _preferencesService.SetPreferenceAsync(
                        UserPreferenceKeys.DontShowFilterExplanation, true);
                }

                if (!result.Confirmed) return;
            }

            // Toggle state
            IsWindowsVersionFilterEnabled = !IsWindowsVersionFilterEnabled;

            // Persist preference
            await _preferencesService.SetPreferenceAsync(
                UserPreferenceKeys.EnableWindowsVersionFilter,
                IsWindowsVersionFilterEnabled);

            // Update registry filter state
            _compatibleSettingsRegistry.SetFilterEnabled(IsWindowsVersionFilterEnabled);

            // Publish event for all subscribers (pages/viewmodels) to refresh
            FilterStateChanged?.Invoke(this, new FilterStateChangedEventArgs(IsWindowsVersionFilterEnabled));

            _logService.Log(Core.Features.Common.Enums.LogLevel.Info,
                $"Windows version filter toggled to: {(IsWindowsVersionFilterEnabled ? "ON" : "OFF")}");
        }
        catch (Exception ex)
        {
            _logService.Log(Core.Features.Common.Enums.LogLevel.Error,
                $"Failed to toggle Windows version filter: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the filter preference from user preferences.
    /// Should be called during initialization.
    /// </summary>
    public async Task LoadFilterPreferenceAsync()
    {
        try
        {
            IsWindowsVersionFilterEnabled = await _preferencesService.GetPreferenceAsync(
                UserPreferenceKeys.EnableWindowsVersionFilter, defaultValue: true);

            _compatibleSettingsRegistry.SetFilterEnabled(IsWindowsVersionFilterEnabled);

            _logService.Log(Core.Features.Common.Enums.LogLevel.Info,
                $"Loaded Windows version filter preference: {(IsWindowsVersionFilterEnabled ? "ON" : "OFF")}");
        }
        catch (Exception ex)
        {
            _logService.Log(Core.Features.Common.Enums.LogLevel.Error,
                $"Failed to load filter preference: {ex.Message}");
        }
    }

    /// <summary>
    /// Command to open the donation page.
    /// </summary>
    [RelayCommand]
    private async Task DonateAsync()
    {
        try
        {
            await Windows.System.Launcher.LaunchUriAsync(
                new Uri("https://ko-fi.com/memstechtips"));
        }
        catch (Exception ex)
        {
            _logService.LogDebug($"Failed to open donation page: {ex.Message}");
        }
    }

    /// <summary>
    /// Command to open the bug report page.
    /// </summary>
    [RelayCommand]
    private async Task BugReportAsync()
    {
        try
        {
            await Windows.System.Launcher.LaunchUriAsync(
                new Uri("https://github.com/memstechtips/Winhance/issues"));
        }
        catch (Exception ex)
        {
            _logService.LogDebug($"Failed to open bug report page: {ex.Message}");
        }
    }

    /// <summary>
    /// Command to open the documentation page.
    /// </summary>
    [RelayCommand]
    private async Task DocsAsync()
    {
        try
        {
            await Windows.System.Launcher.LaunchUriAsync(
                new Uri("https://winhance.net/docs/index.html"));
        }
        catch (Exception ex)
        {
            _logService.LogDebug($"Failed to open documentation page: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Cancel() => _taskProgressService.CancelCurrentTask();

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
    /// Ensures WinGet is installed and up to date on startup.
    /// Runs silently if WinGet is ready; shows task progress if installation/update is needed.
    /// </summary>
    public async Task EnsureWinGetReadyOnStartupAsync()
    {
        try
        {
            _logService.Log(Core.Features.Common.Enums.LogLevel.Info, "Startup: Checking WinGet readiness...");

            bool isReady = await _winGetService.EnsureWinGetReadyAsync();

            if (isReady)
            {
                _logService.Log(Core.Features.Common.Enums.LogLevel.Info, "Startup: WinGet already ready");
                return;
            }

            _logService.Log(Core.Features.Common.Enums.LogLevel.Info, "Startup: WinGet not ready, attempting to install/update...");

            _taskProgressService.StartTask(
                _localizationService.GetString("Progress_CheckingWinget") ?? "Checking for winget...",
                isIndeterminate: true);

            var progress = _taskProgressService.CreateDetailedProgress();
            bool updated = await _winGetService.EnsureWinGetUpToDateAsync(progress);

            if (updated)
            {
                _logService.Log(Core.Features.Common.Enums.LogLevel.Info, "Startup: WinGet successfully prepared");
                _taskProgressService.CompleteTask();
            }
            else
            {
                _logService.Log(Core.Features.Common.Enums.LogLevel.Warning, "Startup: Could not prepare WinGet - likely no internet connection");
                _taskProgressService.UpdateProgress(0,
                    _localizationService.GetString("Error_WinGetInstallFailed") ?? "Failed to install WinGet. Please check your internet connection.");
                await Task.Delay(7000);
                _taskProgressService.CompleteTask();
            }
        }
        catch (Exception ex)
        {
            _logService.Log(Core.Features.Common.Enums.LogLevel.Error, $"Startup: Error preparing WinGet: {ex.Message}");
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

    #endregion

    #region Review Mode

    private void OnReviewModeChanged(object? sender, EventArgs e)
    {
        _ = _dispatcherService.RunOnUIThreadAsync(async () =>
        {
            var entering = _configReviewService.IsInReviewMode;
            IsInReviewMode = entering;

            if (entering)
            {
                // Force filter ON during review mode
                if (!IsWindowsVersionFilterEnabled)
                {
                    IsWindowsVersionFilterEnabled = true;
                    _compatibleSettingsRegistry.SetFilterEnabled(true);
                    FilterStateChanged?.Invoke(this, new FilterStateChangedEventArgs(true));
                }
            }
            else
            {
                // Restore filter state from persisted user preference (default ON)
                var savedPreference = await _preferencesService.GetPreferenceAsync(
                    UserPreferenceKeys.EnableWindowsVersionFilter, defaultValue: true);

                if (IsWindowsVersionFilterEnabled != savedPreference)
                {
                    IsWindowsVersionFilterEnabled = savedPreference;
                    _compatibleSettingsRegistry.SetFilterEnabled(savedPreference);
                    FilterStateChanged?.Invoke(this, new FilterStateChangedEventArgs(savedPreference));
                }
            }

            UpdateReviewModeStatus();
            UpdateCanApplyReviewedConfig();
        });
    }

    private void OnApprovalCountChanged(object? sender, EventArgs e)
    {
        _dispatcherService.RunOnUIThread(() =>
        {
            UpdateReviewModeStatus();
            UpdateCanApplyReviewedConfig();
        });
    }

    private void OnBadgeStateChangedForApplyButton(object? sender, EventArgs e)
    {
        _dispatcherService.RunOnUIThread(UpdateCanApplyReviewedConfig);
    }

    private void UpdateCanApplyReviewedConfig()
    {
        if (!IsInReviewMode)
        {
            CanApplyReviewedConfig = false;
            return;
        }

        // All Optimize/Customize settings must be explicitly reviewed (accept or reject)
        bool allSettingsReviewed = _configReviewService.TotalChanges == 0
            || _configReviewService.ReviewedChanges >= _configReviewService.TotalChanges;

        // SoftwareApps action choices must be made for sections that have items
        bool softwareAppsReviewed = _configReviewService.IsSoftwareAppsReviewed
            || (!_configReviewService.IsFeatureInConfig(FeatureIds.WindowsApps)
                && !_configReviewService.IsFeatureInConfig(FeatureIds.ExternalApps));

        // All Optimize features must be fully reviewed
        bool optimizeReviewed = _configReviewService.IsSectionFullyReviewed("Optimize")
            || !FeatureDefinitions.OptimizeFeatures.Any(f => _configReviewService.IsFeatureInConfig(f));

        // All Customize features must be fully reviewed
        bool customizeReviewed = _configReviewService.IsSectionFullyReviewed("Customize")
            || !FeatureDefinitions.CustomizeFeatures.Any(f => _configReviewService.IsFeatureInConfig(f));

        CanApplyReviewedConfig = allSettingsReviewed && softwareAppsReviewed && optimizeReviewed && customizeReviewed;
    }

    private void UpdateReviewModeStatus()
    {
        if (!_configReviewService.IsInReviewMode)
        {
            ReviewModeStatusText = string.Empty;
            return;
        }

        if (_configReviewService.TotalChanges > 0)
        {
            // Show reviewed/total count and how many will be applied
            var format = _localizationService.GetString("Review_Mode_Status_Format") ?? "{0} of {1} reviewed ({2} will be applied)";
            ReviewModeStatusText = string.Format(format,
                _configReviewService.ReviewedChanges,
                _configReviewService.TotalChanges,
                _configReviewService.ApprovedChanges);
        }
        else if (_configReviewService.TotalConfigItems > 0)
        {
            // Config has items but all match current state
            ReviewModeStatusText = _localizationService.GetString("Review_Mode_Status_AllMatch")
                ?? "All settings already match config";
        }
        else
        {
            ReviewModeStatusText = _localizationService.GetString("Review_Mode_Status_NoItems")
                ?? "No configuration items to apply";
        }
    }

    [RelayCommand]
    private async Task ApplyReviewedConfigAsync()
    {
        try
        {
            await _configurationService.ApplyReviewedConfigAsync();
        }
        catch (Exception ex)
        {
            _logService.Log(Core.Features.Common.Enums.LogLevel.Error, $"Failed to apply reviewed config: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CancelReviewModeAsync()
    {
        var title = _localizationService.GetString("Review_Mode_Cancel_Confirmation_Title") ?? "Cancel Config Review";
        var message = _localizationService.GetString("Review_Mode_Cancel_Confirmation") ?? "Are you sure you want to cancel? No changes will be applied.";

        var confirmed = await _dialogService.ShowConfirmationAsync(message, title);
        if (confirmed)
        {
            await _configurationService.CancelReviewModeAsync();
        }
    }

    #endregion

    #region Task Progress

    private void OnProgressUpdated(object? sender, TaskProgressDetail detail)
    {
        _dispatcherService.RunOnUIThread(() =>
        {
            IsLoading = _taskProgressService.IsTaskRunning;
            if (!string.IsNullOrEmpty(detail.StatusText))
                AppName = detail.StatusText;
            LastTerminalLine = detail.TerminalOutput ?? string.Empty;
        });
    }

    #endregion

    #region Theme Handling

    /// <summary>
    /// Handles theme changes to update the app icon.
    /// </summary>
    private void OnThemeChanged(object? sender, WinhanceTheme theme)
    {
        UpdateAppIconForTheme();
    }

    /// <summary>
    /// Updates the app icon based on the current effective theme.
    /// </summary>
    public void UpdateAppIconForTheme()
    {
        var effectiveTheme = _themeService.GetEffectiveTheme();
        // Use white icon on dark background, black icon on light background
        AppIconSource = effectiveTheme == ElementTheme.Dark
            ? "ms-appx:///Assets/AppIcons/winhance-rocket-white-transparent-bg.png"
            : "ms-appx:///Assets/AppIcons/winhance-rocket-black-transparent-bg.png";
    }

    #endregion
}
