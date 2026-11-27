using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;
using Microsoft.UI.Xaml;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Winhance.Core.Features.Common.Extensions;
using Winhance.UI.Helpers;

namespace Winhance.UI.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private static readonly string[] LocalizedProperties =
    [
        nameof(AppTitle), nameof(AppSubtitle), nameof(WinhanceModeLabel), nameof(ModeNormalLabel),
        nameof(ModeBuilderLabel), nameof(ModeConfigReviewLabel), nameof(ModeNormalTooltip), nameof(ModeBuilderTooltip),
        nameof(ModeConfigReviewTooltip), nameof(WindowsFilterTooltip), nameof(ToggleNavigationTooltip),
        nameof(BackTooltip), nameof(DonateTooltip), nameof(SupportButtonText), nameof(BugReportTooltip),
        nameof(DocsTooltip), nameof(NavSoftwareAppsText), nameof(NavOptimizeText), nameof(NavCustomizeText),
        nameof(NavAutounattendText), nameof(NavWimUtilText), nameof(NavSettingsText), nameof(NavMoreText),
    ];

    private static readonly string[] ModeProperties =
    [
        nameof(CurrentMode), nameof(IsNormalMode), nameof(IsBuilderModeActive), nameof(IsConfigReviewModeActive),
        nameof(IsWindowsFilterButtonEnabled),
    ];

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
    private readonly IBuilderModeEntry _builderModeEntry;

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
        IBuilderModeEntry builderModeEntry)
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
        _builderModeEntry = builderModeEntry;

        TaskProgress = taskProgress;
        UpdateCheck = updateCheck;
        ReviewModeBar = reviewModeBar;
        BuilderModeBar = builderModeBar;

        AppIconSource = "ms-appx:///Assets/AppIcons/winhance-rocket-white-transparent-bg.png";
        VersionInfo = "Winhance";
        IsWindowsVersionFilterEnabled = true;
        OtsInfoBarTitle = string.Empty;
        OtsInfoBarMessage = string.Empty;
    }

    // Call after construction, after the caller has subscribed to PropertyChanged, so initial state changes are observed.
    public void Initialize()
    {
        _themeService.ThemeChanged += OnThemeChanged;

        _localizationService.LanguageChanged += OnLanguageChanged;

        ReviewModeBar.PropertyChanged += OnReviewModeBarPropertyChanged;

        _applicationModeService.ModeChanged += OnApplicationModeChanged;

        _windowsVersionFilterService.FilterStateChanged += OnFilterStateChanged;

        UpdateAppIconForTheme();

        InitializeVersionInfo();

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
        foreach (var name in LocalizedProperties)
            OnPropertyChanged(name);

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
            _logService.LogDebug($"Failed to get version info: {ex.Message}");
            VersionInfo = "Winhance";
        }
    }

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

    public string AppTitle =>
        _localizationService.GetStringOrDefault("App_Title", "Winhance");

    public string AppSubtitle =>
        _localizationService.GetStringOrDefault("App_By", "by Memory");

    public string WinhanceModeLabel => _localizationService.GetStringOrDefault("Mode_Switcher_Label", "Winhance Mode");
    public string ModeNormalLabel => _localizationService.GetStringOrDefault("Mode_Normal", "Normal");
    public string ModeBuilderLabel => _localizationService.GetStringOrDefault("Mode_Builder", "Builder");
    public string ModeConfigReviewLabel => _localizationService.GetStringOrDefault("Mode_ConfigReview", "Config Import");
    public string ModeNormalTooltip => _localizationService.GetStringOrDefault("Mode_Normal_Tooltip", "Normal mode — changes apply to this PC immediately.");
    public string ModeBuilderTooltip => _localizationService.GetStringOrDefault("Mode_Builder_Tooltip", "Builder mode — create a config or autounattend file without changing this PC.");
    public string ModeConfigReviewTooltip => _localizationService.GetStringOrDefault("Mode_ConfigReview_Tooltip", "Config Import. Choose a config to review or apply immediately.");

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

    public Icon WindowsFilterIcon => IsWindowsVersionFilterEnabled ? Icon.Filter : Icon.FilterDismiss;

    public string ToggleNavigationTooltip =>
        _localizationService.GetStringOrDefault("Tooltip_ToggleNavigation", "Toggle Navigation");

    public string BackTooltip => _localizationService.GetString("Tooltip_Back");

    public string DonateTooltip =>
        _localizationService.GetStringOrDefault("Menu_SupportWinhance", "Support Winhance");

    public string SupportButtonText =>
        _localizationService.GetStringOrDefault("Support_TitleBarLabel", "Support");

    public string BugReportTooltip =>
        _localizationService.GetStringOrDefault("Tooltip_ReportBug", "Report a Bug");

    public string DocsTooltip =>
        _localizationService.GetStringOrDefault("Tooltip_Documentation", "Documentation");

    public string NavSoftwareAppsText =>
        _localizationService.GetStringOrDefault("Nav_SoftwareAndApps", "Software & Apps");

    public string NavOptimizeText =>
        _localizationService.GetStringOrDefault("Nav_Optimize", "Optimize");

    public string NavCustomizeText =>
        _localizationService.GetStringOrDefault("Nav_Customize", "Customize");

    public string NavAutounattendText =>
        _localizationService.GetStringOrDefault("Nav_Autounattend", "Unattend");

    public string NavWimUtilText =>
        _localizationService.GetStringOrDefault("Nav_WimUtil", "WIMUtil");

    public string NavSettingsText =>
        _localizationService.GetStringOrDefault("Nav_Settings", "Settings");

    public string NavMoreText =>
        _localizationService.GetStringOrDefault("Nav_More", "More");

    // Builder picks the Windows a file is for in its own bar, so this PC's filter has nothing to say there.
    public bool IsWindowsFilterButtonEnabled => !ReviewModeBar.IsInReviewMode && !IsBuilderModeActive;

    // Confirms first if the current mode has unsaved progress (Builder edits, or a pending Config Review).
    public async Task RequestSwitchModeAsync(WinhanceMode target)
    {
        if (target == _applicationModeService.CurrentMode)
        {
            return;
        }

        // The mode buttons have already moved to the target, so they are put back.
        if (_dialogService.IsDialogOpen)
        {
            RaiseModeProperties();
            return;
        }

        // Builder and Config Import warn inside their own entry dialog, so only Normal asks here.
        if (target == WinhanceMode.Normal
            && !await ModeSwitchConfirmation.ConfirmLeavingAsync(_applicationModeService, _dialogService, _localizationService))
        {
            RaiseModeProperties();
            return;
        }

        if (target == WinhanceMode.Builder && !await _builderModeEntry.EnterAsync(BuilderTarget.Config, reason: null))
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

                case WinhanceMode.ConfigReview:
                    // The same Import as Settings: it asks before leaving this mode and offers review or apply.
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

    // True once the mode lets the page open; false when the user stays where they are.
    public async Task<bool> RequestPageAsync(string tag)
    {
        // UI Automation's Invoke (Narrator, Voice Access) reaches a nav button past an open dialog; a second dialog
        // queued behind it would ask about the mode as it was before the first one closed.
        if (_dialogService.IsDialogOpen)
            return false;

        var wimUtil = tag == "WimUtil";
        switch (NavLockPolicy.GateFor(tag, _applicationModeService.CurrentMode, _applicationModeService.CurrentBuilderTarget))
        {
            case PageGate.SwitchToAutounattend:
                if (!await ConfirmAsync(
                        _localizationService.GetString("Nav_Autounattend_SwitchTarget_Title"),
                        _localizationService.GetString(wimUtil ? "Nav_WimUtil_SwitchTarget_Message" : "Nav_Autounattend_SwitchTarget_Message"),
                        _localizationService.GetString("Nav_Autounattend_SwitchTarget_Confirm"),
                        _localizationService.GetString("Button_Cancel")))
                    return false;
                BuilderModeBar.SelectAutounattendTarget();
                return true;

            // Entering Builder ends a review itself, once its entry dialog is accepted, so a cancel there keeps it.
            case PageGate.EnterBuilder:
                return await _builderModeEntry.EnterAsync(
                    BuilderTarget.Autounattend,
                    _localizationService.GetString(wimUtil ? "Dialog_ModeEntry_WimUtilReason" : "Dialog_ModeEntry_UnattendReason"));

            default:
                return true;
        }
    }

    private async Task<bool> ConfirmAsync(string title, string message, string confirm, string cancel) =>
        (await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
        {
            Title = title,
            Message = message,
            ConfirmButtonText = confirm,
            CancelButtonText = cancel,
        })).Confirmed;

    private void OnApplicationModeChanged(object? sender, EventArgs e)
    {
        RaiseModeProperties();
    }

    private void RaiseModeProperties()
    {
        foreach (var property in ModeProperties)
            OnPropertyChanged(property);
    }

    [RelayCommand]
    private async Task ToggleWindowsFilterAsync()
    {
        await _windowsVersionFilterService.ToggleFilterAsync(ReviewModeBar.IsInReviewMode);
    }

    public void LoadFilterPreference() => _windowsVersionFilterService.LoadFilterPreference();

    [RelayCommand]
    private async Task DonateAsync()
    {
        if (!_dialogService.IsDialogOpen)
            await _dialogService.ShowSponsorsDialogAsync(SponsorsDialogMode.Normal);
    }

    [RelayCommand]
    private async Task BugReportAsync()
    {
        await Windows.System.Launcher.LaunchUriAsync(HelpLinks.BugReport);
    }

    [RelayCommand]
    private async Task DocsAsync(string? pageTag)
    {
        await Windows.System.Launcher.LaunchUriAsync(HelpLinks.DocsFor(pageTag));
    }

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
            _windowsVersionFilterService.RestoreFilterPreference();
        }
    }

    private void OnFilterStateChanged(object? sender, bool isEnabled)
    {
        IsWindowsVersionFilterEnabled = isEnabled;
    }

    private void OnThemeChanged(object? sender, WinhanceTheme theme)
    {
        UpdateAppIconForTheme();
    }

    public void UpdateAppIconForTheme()
    {
        var effectiveTheme = _themeService.GetEffectiveTheme();
        AppIconSource = effectiveTheme == ElementTheme.Dark
            ? "ms-appx:///Assets/AppIcons/winhance-rocket-white-transparent-bg.png"
            : "ms-appx:///Assets/AppIcons/winhance-rocket-black-transparent-bg.png";
    }
}
