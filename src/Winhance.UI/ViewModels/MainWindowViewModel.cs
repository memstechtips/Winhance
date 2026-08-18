using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Extensions;

namespace Winhance.UI.ViewModels;

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

    public TaskProgressViewModel TaskProgress { get; }

    public UpdateCheckViewModel UpdateCheck { get; }

    public ReviewModeBarViewModel ReviewModeBar { get; }

    public BuilderModeBarViewModel BuilderModeBar { get; }

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

    // Call after construction, after the caller has subscribed to PropertyChanged, so initial state changes are observed.
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
        GC.SuppressFinalize(this);
    }

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

    private void InitializeOtsInfoBar()
    {
        if (_interactiveUserService.IsOtsElevation)
        {
            RefreshOtsInfoBarText();
            IsOtsInfoBarOpen = true;
        }
    }

    private void RefreshOtsInfoBarText()
    {
        OtsInfoBarTitle = _localizationService.GetStringOrDefault("InfoBar_OtsElevation_Title", "Running as a different user");
        var messageTemplate = _localizationService.GetStringOrDefault("InfoBar_OtsElevation_Message", "This app was elevated with a different account's credentials. Settings will still be applied to the logged-in user ({0}). This message is informational only.");
        OtsInfoBarMessage = string.Format(messageTemplate, _interactiveUserService.InteractiveUserName);
    }

    public void DismissOtsInfoBar()
    {
        IsOtsInfoBarOpen = false;
    }

    #endregion

    #region Localized Strings

    // App title bar
    public string AppTitle =>
        _localizationService.GetStringOrDefault("App_Title", "Winhance");

    public string AppSubtitle =>
        _localizationService.GetStringOrDefault("App_By", "by Memory");

    // Mode switcher label + per-mode labels and tooltips
    public string WinhanceModeLabel => _localizationService.GetStringOrDefault("Mode_Switcher_Label", "Winhance Mode");
    public string ModeNormalLabel => _localizationService.GetStringOrDefault("Mode_Normal", "Normal");
    public string ModeBuilderLabel => _localizationService.GetStringOrDefault("Mode_Builder", "Builder");
    public string ModeConfigReviewLabel => _localizationService.GetStringOrDefault("Mode_ConfigReview", "Config Review");
    public string ModeNormalTooltip => _localizationService.GetStringOrDefault("Mode_Normal_Tooltip", "Normal mode");
    public string ModeBuilderTooltip => _localizationService.GetStringOrDefault("Mode_Builder_Tooltip", "Builder mode");
    public string ModeConfigReviewTooltip => _localizationService.GetStringOrDefault("Mode_ConfigReview_Tooltip", "Config Review");

    // Tooltips
    public string WindowsFilterTooltip
    {
        get
        {
            if (IsWindowsVersionFilterEnabled)
            {
                var title = _localizationService.GetStringOrDefault("Tooltip_FilterEnabled", "Windows Version Filter: ON");
                var description = _localizationService.GetStringOrDefault("Tooltip_FilterEnabled_Description", "Click to show settings for all Windows versions");
                return $"{title}\n{description}";
            }
            else
            {
                var title = _localizationService.GetStringOrDefault("Tooltip_FilterDisabled", "Windows Version Filter: OFF");
                var description = _localizationService.GetStringOrDefault("Tooltip_FilterDisabled_Description", "Showing all settings (incompatible settings marked)");
                return $"{title}\n{description}";
            }
        }
    }

    public string WindowsFilterIcon
    {
        get
        {
            var resourceKey = IsWindowsVersionFilterEnabled ? "FilterCheckIconPath" : "FilterOffIconPath";
            return Application.Current.Resources[resourceKey] as string ?? string.Empty;
        }
    }

    public string ToggleNavigationTooltip =>
        _localizationService.GetStringOrDefault("Tooltip_ToggleNavigation", "Toggle Navigation");

    public string DonateTooltip =>
        _localizationService.GetStringOrDefault("Menu_SupportWinhance", "Support Winhance");

    public string BugReportTooltip =>
        _localizationService.GetStringOrDefault("Tooltip_ReportBug", "Report a Bug");

    public string DocsTooltip =>
        _localizationService.GetStringOrDefault("Tooltip_Documentation", "Documentation");

    // Nav bar text
    public string NavSoftwareAppsText =>
        _localizationService.GetStringOrDefault("Nav_SoftwareAndApps", "Software & Apps");

    public string NavOptimizeText =>
        _localizationService.GetStringOrDefault("Nav_Optimize", "Optimize");

    public string NavCustomizeText =>
        _localizationService.GetStringOrDefault("Nav_Customize", "Customize");

    public string NavAdvancedToolsText =>
        _localizationService.GetStringOrDefault("Nav_AdvancedTools", "Advanced Tools");

    public string NavSettingsText =>
        _localizationService.GetStringOrDefault("Nav_Settings", "Settings");

    public string NavMoreText =>
        _localizationService.GetStringOrDefault("Nav_More", "More");

    // Filter button enabled state
    public bool IsWindowsFilterButtonEnabled => !ReviewModeBar.IsInReviewMode;

    #endregion

    #region Commands

    // Confirms first if the current mode has unsaved progress (Builder edits, or a pending Config Review).
    public async Task RequestSwitchModeAsync(WinhanceMode target)
    {
        if (target == _applicationModeService.CurrentMode)
        {
            return;
        }

        // HasBuilderChanges, not GetBuilderEdits().Count: NumericRange and AC/DC power settings are
        // authored into the UI but do not yet produce a serializable BuilderEdit, so counting edits
        // silently skipped the prompt for anyone who had only moved sliders.
        bool leavingBuilderWithEdits = _applicationModeService.CurrentMode == WinhanceMode.Builder
            && _applicationModeService.HasBuilderChanges;
        bool leavingReview = _applicationModeService.CurrentMode == WinhanceMode.ConfigReview;

        if (leavingBuilderWithEdits || leavingReview)
        {
            var message = _localizationService.GetStringOrDefault("Mode_Switch_Confirmation", "Switch mode? Your current unsaved progress will be discarded. Nothing was applied to this PC.");
            var title = _localizationService.GetStringOrDefault("Mode_Switch_Confirmation_Title", "Switch Mode");
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

    private Task<bool> ShowBuilderIntroIfNeededAsync()
    {
        return ShowModeIntroIfNeededAsync(
            BuilderIntroDontShowKey,
            "Dialog_BuilderIntro_Title",
            "Dialog_BuilderIntro_Message",
            "Dialog_BuilderIntro_Confirm");
    }

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

    [RelayCommand]
    private async Task ToggleWindowsFilterAsync()
    {
        await _windowsVersionFilterService.ToggleFilterAsync(ReviewModeBar.IsInReviewMode);
    }

    public async Task LoadFilterPreferenceAsync()
    {
        await _windowsVersionFilterService.LoadFilterPreferenceAsync();
    }

    [RelayCommand]
    private async Task DonateAsync()
    {
        try
        {
            await _dialogService.ShowSponsorsDialogAsync(SponsorsDialogMode.Normal);
        }
        catch (Exception ex)
        {
            _logService.LogDebug($"Failed to open sponsors dialog: {ex.Message}");
        }
    }

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

    private void OnFilterStateChanged(object? sender, bool isEnabled)
    {
        IsWindowsVersionFilterEnabled = isEnabled;
    }

    #endregion

    #region Theme Handling

    private void OnThemeChanged(object? sender, WinhanceTheme theme)
    {
        UpdateAppIconForTheme();
    }

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
