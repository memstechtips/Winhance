using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.ViewModels;

/// <summary>
/// ViewModel for the MainWindow, handling title bar commands and state.
/// Child ViewModels handle task progress, update checking, and review mode.
/// </summary>
public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly IThemeService _themeService;
    private readonly IConfigurationService _configurationService;
    private readonly ILocalizationService _localizationService;
    private readonly IVersionService _versionService;
    private readonly ILogService _logService;
    private readonly IInteractiveUserService _interactiveUserService;
    private readonly IWindowsVersionFilterService _windowsVersionFilterService;
    private readonly IApplicationModeService _applicationModeService;
    private readonly IDialogService _dialogService;
    private readonly IUserPreferencesService _userPreferencesService;

    /// <summary>Child ViewModel for task progress display.</summary>
    public TaskProgressViewModel TaskProgress { get; }

    /// <summary>Child ViewModel for update checking.</summary>
    public UpdateCheckViewModel UpdateCheck { get; }

    /// <summary>Child ViewModel for review mode bar.</summary>
    public ReviewModeBarViewModel ReviewModeBar { get; }

    /// <summary>Child ViewModel for the Builder mode bar.</summary>
    public BuilderModeBarViewModel BuilderModeBar { get; }

    /// <summary>The current app-wide interaction mode (for the mode switcher).</summary>
    public WinhanceMode CurrentMode => _applicationModeService.CurrentMode;
    public bool IsNormalMode => CurrentMode == WinhanceMode.Normal;
    public bool IsBuilderModeActive => CurrentMode == WinhanceMode.Builder;
    public bool IsConfigReviewModeActive => CurrentMode == WinhanceMode.ConfigReview;

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
        IInteractiveUserService interactiveUserService,
        IWindowsVersionFilterService windowsVersionFilterService,
        TaskProgressViewModel taskProgress,
        UpdateCheckViewModel updateCheck,
        ReviewModeBarViewModel reviewModeBar,
        BuilderModeBarViewModel builderModeBar,
        IApplicationModeService applicationModeService,
        IDialogService dialogService,
        IUserPreferencesService userPreferencesService)
    {
        _themeService = themeService;
        _configurationService = configurationService;
        _localizationService = localizationService;
        _versionService = versionService;
        _logService = logService;
        _interactiveUserService = interactiveUserService;
        _windowsVersionFilterService = windowsVersionFilterService;
        _applicationModeService = applicationModeService;
        _dialogService = dialogService;
        _userPreferencesService = userPreferencesService;

        TaskProgress = taskProgress;
        UpdateCheck = updateCheck;
        ReviewModeBar = reviewModeBar;
        BuilderModeBar = builderModeBar;

        // Initialize partial property defaults
        AppIconSource = "ms-appx:///Assets/AppIcons/winhance-rocket-white-transparent-bg.png";
        VersionInfo = "Winhance";
        IsWindowsVersionFilterEnabled = true;
        OtsInfoBarTitle = string.Empty;
        OtsInfoBarMessage = string.Empty;
    }

    /// <summary>
    /// Performs deferred initialization: subscribes to events and sets initial state.
    /// Must be called after construction, typically after the caller has subscribed
    /// to PropertyChanged so that initial state changes are observed.
    /// </summary>
    public void Initialize()
    {
        // Subscribe to theme changes
        _themeService.ThemeChanged += OnThemeChanged;

        // Subscribe to language changes
        _localizationService.LanguageChanged += OnLanguageChanged;

        // Subscribe to review mode filter cross-cutting
        ReviewModeBar.PropertyChanged += OnReviewModeBarPropertyChanged;

        // Keep the mode switcher in sync with the app-wide mode
        _applicationModeService.ModeChanged += OnApplicationModeChanged;

        // Subscribe to filter state changes from the service
        _windowsVersionFilterService.FilterStateChanged += OnFilterStateChanged;

        // Set initial icon based on current theme
        UpdateAppIconForTheme();

        // Initialize version info
        InitializeVersionInfo();

        // Show OTS elevation InfoBar if needed
        InitializeOtsInfoBar();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _themeService.ThemeChanged -= OnThemeChanged;
        _localizationService.LanguageChanged -= OnLanguageChanged;
        ReviewModeBar.PropertyChanged -= OnReviewModeBarPropertyChanged;
        _applicationModeService.ModeChanged -= OnApplicationModeChanged;
        _windowsVersionFilterService.FilterStateChanged -= OnFilterStateChanged;
    }

    /// <summary>
    /// Handles language changes to update localized strings.
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Notify all localized string properties
        OnPropertyChanged(nameof(AppTitle));
        OnPropertyChanged(nameof(AppSubtitle));
        OnPropertyChanged(nameof(WinhanceModeLabel));
        OnPropertyChanged(nameof(ModeNormalLabel));
        OnPropertyChanged(nameof(ModeBuilderLabel));
        OnPropertyChanged(nameof(ModeConfigReviewLabel));
        OnPropertyChanged(nameof(ModeNormalTooltip));
        OnPropertyChanged(nameof(ModeBuilderTooltip));
        OnPropertyChanged(nameof(ModeConfigReviewTooltip));
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
        catch (Exception ex)
        {
            _logService.LogDebug($"[MainWindowViewModel] Failed to get version info: {ex.Message}");
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

    // Mode switcher label + per-mode labels and tooltips
    public string WinhanceModeLabel => _localizationService.GetString("Mode_Switcher_Label") ?? "Winhance Mode";
    public string ModeNormalLabel => _localizationService.GetString("Mode_Normal") ?? "Normal";
    public string ModeBuilderLabel => _localizationService.GetString("Mode_Builder") ?? "Builder";
    public string ModeConfigReviewLabel => _localizationService.GetString("Mode_ConfigReview") ?? "Config Review";
    public string ModeNormalTooltip => _localizationService.GetString("Mode_Normal_Tooltip") ?? "Normal mode";
    public string ModeBuilderTooltip => _localizationService.GetString("Mode_Builder_Tooltip") ?? "Builder mode";
    public string ModeConfigReviewTooltip => _localizationService.GetString("Mode_ConfigReview_Tooltip") ?? "Config Review";

    // Tooltips
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
    /// Switch the app-wide mode from the title-bar mode switcher. Confirms first if the
    /// current mode has unsaved progress (Builder edits, or a pending Config Review).
    /// Normal → live system; Builder → author without applying; Config Review → import dialog.
    /// </summary>
    public async Task RequestSwitchModeAsync(WinhanceMode target)
    {
        if (target == _applicationModeService.CurrentMode)
        {
            return;
        }

        bool leavingBuilderWithEdits = _applicationModeService.CurrentMode == WinhanceMode.Builder
            && _applicationModeService.GetBuilderEdits().Count > 0;
        bool leavingReview = _applicationModeService.CurrentMode == WinhanceMode.ConfigReview;

        if (leavingBuilderWithEdits || leavingReview)
        {
            var message = _localizationService.GetString("Mode_Switch_Confirmation")
                ?? "Switch mode? Your current unsaved progress will be discarded. Nothing was applied to this PC.";
            var title = _localizationService.GetString("Mode_Switch_Confirmation_Title") ?? "Switch Mode";
            var confirmed = (await _dialogService.ShowConfirmationAsync(
                new ConfirmationRequest { Message = message, Title = title })).Confirmed;
            if (!confirmed)
            {
                RaiseModeProperties();
                return;
            }
        }

        // Show the first-run explainer for the mode being entered (unless dismissed).
        if (target == WinhanceMode.Builder && !await ShowBuilderIntroIfNeededAsync())
        {
            RaiseModeProperties();
            return;
        }
        if (target == WinhanceMode.ConfigReview && !await ShowConfigReviewIntroIfNeededAsync())
        {
            RaiseModeProperties();
            return;
        }

        try
        {
            switch (target)
            {
                case WinhanceMode.Normal:
                    if (_applicationModeService.CurrentMode == WinhanceMode.ConfigReview)
                        await _configurationService.CancelReviewModeAsync();
                    else
                        _applicationModeService.EnterNormalMode();
                    break;

                case WinhanceMode.Builder:
                    if (_applicationModeService.CurrentMode == WinhanceMode.ConfigReview)
                        await _configurationService.CancelReviewModeAsync();
                    _applicationModeService.EnterBuilderMode(BuilderTarget.Config);
                    break;

                case WinhanceMode.ConfigReview:
                    // Entering review = the existing import-and-review flow (file picker).
                    await _configurationService.ImportConfigurationAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Failed to switch mode to {target}: {ex.Message}");
        }

        RaiseModeProperties();
    }

    private void OnApplicationModeChanged(object? sender, EventArgs e)
    {
        RaiseModeProperties();
    }

    private const string BuilderIntroDontShowKey = "BuilderModeIntroDontShow";
    private const string ConfigReviewIntroDontShowKey = "ConfigReviewModeIntroDontShow";

    /// <summary>
    /// Shows the Builder Mode explainer (with a "don't show again" option) unless the user
    /// has dismissed it. Returns true to proceed into Builder mode, false if the user cancels.
    /// </summary>
    private Task<bool> ShowBuilderIntroIfNeededAsync()
    {
        return ShowModeIntroIfNeededAsync(
            BuilderIntroDontShowKey,
            "Dialog_BuilderIntro_Title",
            "Dialog_BuilderIntro_Message",
            "Dialog_BuilderIntro_Confirm");
    }

    /// <summary>
    /// Shows the Config Review explainer (with a "don't show again" option) unless the user
    /// has dismissed it. Returns true to proceed to the import window, false if the user cancels.
    /// </summary>
    private Task<bool> ShowConfigReviewIntroIfNeededAsync()
    {
        return ShowModeIntroIfNeededAsync(
            ConfigReviewIntroDontShowKey,
            "Dialog_ConfigReviewIntro_Title",
            "Dialog_ConfigReviewIntro_Message",
            "Dialog_ConfigReviewIntro_Confirm");
    }

    private async Task<bool> ShowModeIntroIfNeededAsync(
        string dontShowKey,
        string titleKey,
        string messageKey,
        string confirmKey)
    {
        if (_userPreferencesService.GetPreference(dontShowKey, false))
        {
            return true;
        }

        var response = await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
        {
            Title = _localizationService.GetString(titleKey),
            Message = _localizationService.GetString(messageKey),
            CheckboxText = _localizationService.GetString("Dialog_Mode_DontShowAgain"),
            CheckboxInitiallyChecked = false,
            ConfirmButtonText = _localizationService.GetString(confirmKey),
            CancelButtonText = _localizationService.GetString("Button_Cancel"),
        });

        if (response.Confirmed && response.CheckboxChecked)
        {
            await _userPreferencesService.SetPreferenceAsync(dontShowKey, true);
        }

        return response.Confirmed;
    }

    private void RaiseModeProperties()
    {
        OnPropertyChanged(nameof(CurrentMode));
        OnPropertyChanged(nameof(IsNormalMode));
        OnPropertyChanged(nameof(IsBuilderModeActive));
        OnPropertyChanged(nameof(IsConfigReviewModeActive));
    }

    /// <summary>
    /// Command to toggle Windows version filter.
    /// </summary>
    [RelayCommand]
    private async Task ToggleWindowsFilterAsync()
    {
        await _windowsVersionFilterService.ToggleFilterAsync(ReviewModeBar.IsInReviewMode);
    }

    /// <summary>
    /// Loads the filter preference from user preferences.
    /// Should be called during initialization.
    /// </summary>
    public async Task LoadFilterPreferenceAsync()
    {
        await _windowsVersionFilterService.LoadFilterPreferenceAsync();
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
            _windowsVersionFilterService.ForceFilterOn();
        }
        else
        {
            _ = _windowsVersionFilterService.RestoreFilterPreferenceAsync();
        }
    }

    /// <summary>
    /// Syncs the ViewModel's IsWindowsVersionFilterEnabled property when the service state changes.
    /// </summary>
    private void OnFilterStateChanged(object? sender, bool isEnabled)
    {
        IsWindowsVersionFilterEnabled = isEnabled;
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
