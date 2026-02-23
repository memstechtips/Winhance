using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.ViewModels;

/// <summary>
/// ViewModel for the MainWindow, handling title bar commands and state.
/// Child ViewModels handle task progress, update checking, and review mode.
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
    private readonly IWinGetService _winGetService;
    private readonly IInternetConnectivityService _internetConnectivityService;
    private readonly IInteractiveUserService _interactiveUserService;
    private readonly IEventBus _eventBus;

    /// <summary>Child ViewModel for task progress display.</summary>
    public TaskProgressViewModel TaskProgress { get; }

    /// <summary>Child ViewModel for update checking.</summary>
    public UpdateCheckViewModel UpdateCheck { get; }

    /// <summary>Child ViewModel for review mode bar.</summary>
    public ReviewModeBarViewModel ReviewModeBar { get; }

    [ObservableProperty]
    public partial string AppIconSource { get; set; }

    [ObservableProperty]
    public partial string VersionInfo { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowsFilterTooltip))]
    [NotifyPropertyChangedFor(nameof(WindowsFilterIcon))]
    public partial bool IsWindowsVersionFilterEnabled { get; set; }

    // OTS Elevation InfoBar properties
    [ObservableProperty]
    public partial bool IsOtsInfoBarOpen { get; set; }

    [ObservableProperty]
    public partial string OtsInfoBarTitle { get; set; }

    [ObservableProperty]
    public partial string OtsInfoBarMessage { get; set; }

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
        IWinGetService winGetService,
        IInternetConnectivityService internetConnectivityService,
        IInteractiveUserService interactiveUserService,
        IEventBus eventBus,
        TaskProgressViewModel taskProgress,
        UpdateCheckViewModel updateCheck,
        ReviewModeBarViewModel reviewModeBar)
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
        _winGetService = winGetService;
        _internetConnectivityService = internetConnectivityService;
        _interactiveUserService = interactiveUserService;
        _eventBus = eventBus;

        TaskProgress = taskProgress;
        UpdateCheck = updateCheck;
        ReviewModeBar = reviewModeBar;

        // Initialize partial property defaults
        AppIconSource = "ms-appx:///Assets/AppIcons/winhance-rocket-white-transparent-bg.png";
        VersionInfo = "Winhance";
        IsWindowsVersionFilterEnabled = true;
        OtsInfoBarTitle = string.Empty;
        OtsInfoBarMessage = string.Empty;

        // Subscribe to theme changes
        _themeService.ThemeChanged += OnThemeChanged;

        // Subscribe to language changes
        _localizationService.LanguageChanged += OnLanguageChanged;

        // Subscribe to review mode filter cross-cutting
        ReviewModeBar.PropertyChanged += OnReviewModeBarPropertyChanged;

        // Set initial icon based on current theme
        UpdateAppIconForTheme();

        // Initialize version info
        InitializeVersionInfo();

        // Show OTS elevation InfoBar if needed
        InitializeOtsInfoBar();
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

        // OTS InfoBar
        if (IsOtsInfoBarOpen)
        {
            RefreshOtsInfoBarText();
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

    #region OTS Elevation InfoBar

    /// <summary>
    /// Initializes the OTS InfoBar if the app is running under OTS elevation.
    /// </summary>
    private void InitializeOtsInfoBar()
    {
        if (_interactiveUserService.IsOtsElevation)
        {
            RefreshOtsInfoBarText();
            IsOtsInfoBarOpen = true;
        }
    }

    /// <summary>
    /// Refreshes the OTS InfoBar text from localization.
    /// </summary>
    private void RefreshOtsInfoBarText()
    {
        OtsInfoBarTitle = _localizationService.GetString("InfoBar_OtsElevation_Title")
            ?? "Running as a different user";
        var messageTemplate = _localizationService.GetString("InfoBar_OtsElevation_Message")
            ?? "This app was elevated with a different account's credentials. Settings will still be applied to the logged-in user ({0}). This message is informational only.";
        OtsInfoBarMessage = string.Format(messageTemplate, _interactiveUserService.InteractiveUserName);
    }

    /// <summary>
    /// Dismisses the OTS InfoBar.
    /// </summary>
    public void DismissOtsInfoBar()
    {
        IsOtsInfoBarOpen = false;
    }

    #endregion

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

    // Filter button enabled state
    public bool IsWindowsFilterButtonEnabled => !ReviewModeBar.IsInReviewMode;

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
        if (ReviewModeBar.IsInReviewMode) return;

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
            _eventBus.Publish(new FilterStateChangedEvent(IsWindowsVersionFilterEnabled));

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

    /// <summary>
    /// Post-UI startup flow for WinGet / AppInstaller.
    /// If system winget was already found (EnsureWinGetReadyAsync set it), silently attempts an upgrade.
    /// If only the bundled winget is available, shows progress and installs AppInstaller.
    /// </summary>
    public async Task EnsureWinGetReadyOnStartupAsync()
    {
        try
        {
            if (_winGetService.IsSystemWinGetAvailable)
            {
                // System winget already present -- silently attempt upgrade
                _logService.Log(Core.Features.Common.Enums.LogLevel.Info,
                    "Startup: System winget available, attempting silent AppInstaller upgrade...");

                bool upgraded = await _winGetService.UpgradeAppInstallerAsync();
                if (upgraded)
                {
                    _logService.Log(Core.Features.Common.Enums.LogLevel.Info,
                        "Startup: AppInstaller upgraded successfully");

                    // Re-init COM in case the upgrade changed the COM server
                    await _winGetService.EnsureWinGetReadyAsync();
                }
                else
                {
                    _logService.Log(Core.Features.Common.Enums.LogLevel.Info,
                        "Startup: AppInstaller upgrade not needed or not applicable");
                }
            }
            else
            {
                // Only bundled winget -- need to install AppInstaller
                // Check internet FIRST -- all install paths require connectivity
                var hasInternet = await _internetConnectivityService.IsInternetConnectedAsync(forceCheck: true);
                if (!hasInternet)
                {
                    _logService.Log(Core.Features.Common.Enums.LogLevel.Warning,
                        "Startup: No internet connection -- skipping AppInstaller installation");
                    return;
                }

                _logService.Log(Core.Features.Common.Enums.LogLevel.Info,
                    "Startup: No system winget, attempting to install AppInstaller...");

                _taskProgressService.StartTask(
                    _localizationService.GetString("Progress_WinGet_Installing") ?? "Installing WinGet...",
                    isIndeterminate: false);

                try
                {
                    bool installed = await _winGetService.InstallWinGetAsync();

                    if (installed)
                    {
                        _logService.Log(Core.Features.Common.Enums.LogLevel.Info,
                            "Startup: AppInstaller installed successfully");
                        _taskProgressService.CompleteTask();
                    }
                    else
                    {
                        _logService.Log(Core.Features.Common.Enums.LogLevel.Warning,
                            "Startup: AppInstaller installation failed -- continuing with bundled CLI");
                        _taskProgressService.UpdateProgress(0,
                            _localizationService.GetString("Error_WinGetInstallFailed")
                            ?? "Failed to install WinGet. Please check your internet connection.");
                        await Task.Delay(5000);
                        _taskProgressService.CompleteTask();
                    }
                }
                catch
                {
                    _taskProgressService.CompleteTask();
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Log(Core.Features.Common.Enums.LogLevel.Error,
                $"Startup: Error in WinGet readiness flow: {ex.Message}");
        }
    }

    #endregion

    #region Review Mode / Filter Cross-Cutting

    private void OnReviewModeBarPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReviewModeBarViewModel.IsInReviewMode))
        {
            OnPropertyChanged(nameof(IsWindowsFilterButtonEnabled));
            HandleReviewModeFilterChange(ReviewModeBar.IsInReviewMode);
        }
    }

    private void HandleReviewModeFilterChange(bool entering)
    {
        if (entering)
        {
            if (!IsWindowsVersionFilterEnabled)
            {
                IsWindowsVersionFilterEnabled = true;
                _compatibleSettingsRegistry.SetFilterEnabled(true);
                _eventBus.Publish(new FilterStateChangedEvent(true));
            }
        }
        else
        {
            _ = RestoreFilterPreferenceAsync();
        }
    }

    private async Task RestoreFilterPreferenceAsync()
    {
        var savedPreference = await _preferencesService.GetPreferenceAsync(
            UserPreferenceKeys.EnableWindowsVersionFilter, defaultValue: true);
        if (IsWindowsVersionFilterEnabled != savedPreference)
        {
            IsWindowsVersionFilterEnabled = savedPreference;
            _compatibleSettingsRegistry.SetFilterEnabled(savedPreference);
            _eventBus.Publish(new FilterStateChangedEvent(savedPreference));
        }
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
