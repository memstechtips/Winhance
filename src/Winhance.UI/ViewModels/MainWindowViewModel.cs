using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.ViewModels;

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

    [ObservableProperty]
    private InfoBarSeverity _updateInfoBarSeverity;

    [ObservableProperty]
    private bool _isUpdateActionButtonVisible;

    [ObservableProperty]
    private bool _isUpdateCheckInProgress;

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
        IDispatcherService dispatcherService)
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

        // Subscribe to theme changes
        _themeService.ThemeChanged += OnThemeChanged;

        // Subscribe to language changes
        _localizationService.LanguageChanged += OnLanguageChanged;

        // Subscribe to task progress updates
        _taskProgressService.ProgressUpdated += OnProgressUpdated;

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
        OnPropertyChanged(nameof(DonateTooltip));
        OnPropertyChanged(nameof(BugReportTooltip));

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

    public string DonateTooltip =>
        _localizationService.GetString("Tooltip_Donate") ?? "Donate";

    public string BugReportTooltip =>
        _localizationService.GetString("Tooltip_ReportBug") ?? "Report a Bug";

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
            System.Diagnostics.Debug.WriteLine($"Failed to save configuration: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"Failed to import configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// Command to toggle Windows version filter.
    /// </summary>
    [RelayCommand]
    private async Task ToggleWindowsFilterAsync()
    {
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
            System.Diagnostics.Debug.WriteLine($"Failed to open donation page: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"Failed to open bug report page: {ex.Message}");
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

            if (latestVersion != null && latestVersion.Version != currentVersion.Version)
            {
                _logService.Log(Core.Features.Common.Enums.LogLevel.Info, $"Update available: {latestVersion.Version}");

                var message = _localizationService.GetString("Dialog_Update_Message") ?? "Good News! A New Version of Winhance is available.";
                var currentVersionLabel = _localizationService.GetString("Dialog_Update_CurrentVersion") ?? "Current Version:";
                var latestVersionLabel = _localizationService.GetString("Dialog_Update_LatestVersion") ?? "Latest Version:";
                var title = _localizationService.GetString("Dialog_Update_Title") ?? "Update Available";

                UpdateInfoBarTitle = title;
                UpdateInfoBarMessage = $"{message}  {currentVersionLabel} {currentVersion.Version}  →  {latestVersionLabel} {latestVersion.Version}";
                UpdateInfoBarSeverity = InfoBarSeverity.Success;
                IsUpdateActionButtonVisible = true;
                IsUpdateInfoBarOpen = true;
            }
            else
            {
                _logService.Log(Core.Features.Common.Enums.LogLevel.Info, "No updates available");

                var noUpdatesTitle = _localizationService.GetString("Dialog_Update_NoUpdates_Title") ?? "No Updates Available";
                var noUpdatesMessage = _localizationService.GetString("Dialog_Update_NoUpdates_Message") ?? "You have the latest version of Winhance.";

                UpdateInfoBarTitle = noUpdatesTitle;
                UpdateInfoBarMessage = noUpdatesMessage;
                UpdateInfoBarSeverity = InfoBarSeverity.Success;
                IsUpdateActionButtonVisible = false;
                IsUpdateInfoBarOpen = true;
            }
        }
        catch (Exception ex)
        {
            _logService.Log(Core.Features.Common.Enums.LogLevel.Error, $"Error checking for updates: {ex.Message}");

            var errorTitle = _localizationService.GetString("Dialog_Update_CheckError_Title") ?? "Update Check Error";
            var errorMessageTemplate = _localizationService.GetString("Dialog_Update_CheckError_Message") ?? "An error occurred while checking for updates: {0}";
            var errorMessage = string.Format(errorMessageTemplate, ex.Message);

            UpdateInfoBarTitle = errorTitle;
            UpdateInfoBarMessage = errorMessage;
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
            var downloadingMessage = _localizationService.GetString("Dialog_Update_Status_Downloading") ?? "Downloading update...";
            UpdateInfoBarMessage = downloadingMessage;
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

            if (latestVersion != null && latestVersion.Version != currentVersion.Version)
            {
                _logService.Log(Core.Features.Common.Enums.LogLevel.Info, $"Startup: Update available: {latestVersion.Version}");

                var message = _localizationService.GetString("Dialog_Update_Message") ?? "Good News! A New Version of Winhance is available.";
                var currentVersionLabel = _localizationService.GetString("Dialog_Update_CurrentVersion") ?? "Current Version:";
                var latestVersionLabel = _localizationService.GetString("Dialog_Update_LatestVersion") ?? "Latest Version:";
                var title = _localizationService.GetString("Dialog_Update_Title") ?? "Update Available";

                UpdateInfoBarTitle = title;
                UpdateInfoBarMessage = $"{message}  {currentVersionLabel} {currentVersion.Version}  →  {latestVersionLabel} {latestVersion.Version}";
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
