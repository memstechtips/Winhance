using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.ViewModels;

public partial class ReviewModeBarViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly IConfigReviewModeService _configReviewModeService;
    private readonly IConfigReviewDiffService _configReviewDiffService;
    private readonly IConfigReviewBadgeService _configReviewBadgeService;
    private readonly IConfigurationService _configurationService;
    private readonly IReviewModeViewModelCoordinator _vmCoordinator;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILocalizationService _localizationService;
    private readonly IDialogService _dialogService;
    private readonly ILogService _logService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAcceptAllRemaining))]
    public partial bool IsInReviewMode { get; set; }

    [ObservableProperty]
    public partial string ReviewModeStatusText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAcceptAllRemaining))]
    public partial bool CanApplyReviewedConfig { get; set; }

    // Names what still blocks Apply; null once nothing does.
    [ObservableProperty]
    public partial string? ReviewModeApplyBlockedTooltip { get; set; }

    public bool CanAcceptAllRemaining => IsInReviewMode && !CanApplyReviewedConfig;

    [ObservableProperty]
    public partial string? ReviewModeSetAsideDetail { get; set; }

    public string ReviewModeTitleText =>
        _localizationService.GetStringOrDefault("Review_Mode_Title", "Config Review Mode");

    public string ReviewModeApplyButtonText =>
        _localizationService.GetStringOrDefault("Review_Mode_Apply_Button", "Apply Config");

    public string ReviewModeAcceptAllRemainingText =>
        _localizationService.GetStringOrDefault("Review_Mode_AcceptAllRemaining", "Accept all remaining");

    public string ReviewModeCancelButtonText =>
        _localizationService.GetStringOrDefault("Button_Cancel", "Cancel");

    public string ReviewModeDescriptionText =>
        _localizationService.GetStringOrDefault("Review_Mode_Description", "Check what this config will change before you apply it.");

    public ReviewModeBarViewModel(
        IConfigReviewModeService configReviewModeService,
        IConfigReviewDiffService configReviewDiffService,
        IConfigReviewBadgeService configReviewBadgeService,
        IConfigurationService configurationService,
        IReviewModeViewModelCoordinator vmCoordinator,
        IDispatcherService dispatcherService,
        ILocalizationService localizationService,
        IDialogService dialogService,
        ILogService logService)
    {
        _configReviewModeService = configReviewModeService;
        _configReviewDiffService = configReviewDiffService;
        _configReviewBadgeService = configReviewBadgeService;
        _configurationService = configurationService;
        _vmCoordinator = vmCoordinator;
        _dispatcherService = dispatcherService;
        _localizationService = localizationService;
        _dialogService = dialogService;
        _logService = logService;

        ReviewModeStatusText = string.Empty;

        _configReviewModeService.ReviewModeChanged += OnReviewModeChanged;
        _configReviewDiffService.ApprovalCountChanged += OnApprovalCountChanged;
        _configReviewBadgeService.BadgeStateChanged += OnBadgeStateChangedForApplyButton;
        _localizationService.LanguageChanged += OnLanguageChanged;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _configReviewModeService.ReviewModeChanged -= OnReviewModeChanged;
        _configReviewDiffService.ApprovalCountChanged -= OnApprovalCountChanged;
        _configReviewBadgeService.BadgeStateChanged -= OnBadgeStateChangedForApplyButton;
        _localizationService.LanguageChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (IsInReviewMode)
        {
            OnPropertyChanged(nameof(ReviewModeTitleText));
            OnPropertyChanged(nameof(ReviewModeDescriptionText));
            OnPropertyChanged(nameof(ReviewModeApplyButtonText));
            OnPropertyChanged(nameof(ReviewModeAcceptAllRemainingText));
            OnPropertyChanged(nameof(ReviewModeCancelButtonText));
            UpdateReviewModeStatus();
            UpdateCanApplyReviewedConfig();
        }
    }

    private void OnReviewModeChanged(object? sender, EventArgs e)
    {
        _dispatcherService.RunOnUIThreadAsync(() =>
        {
            IsInReviewMode = _configReviewModeService.IsInReviewMode;
            UpdateReviewModeStatus();
            UpdateCanApplyReviewedConfig();
            return Task.CompletedTask;
        }).FireAndForget(_logService);
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
            ReviewModeApplyBlockedTooltip = null;
            return;
        }

        bool allSettingsReviewed = _configReviewDiffService.TotalChanges == 0
            || _configReviewDiffService.ReviewedChanges >= _configReviewDiffService.TotalChanges;

        bool softwareAppsReviewed = _configReviewBadgeService.IsSoftwareAppsReviewed
            || (!_configReviewBadgeService.IsFeatureInConfig(FeatureIds.WindowsApps)
                && !_configReviewBadgeService.IsFeatureInConfig(FeatureIds.ExternalApps));

        bool optimizeReviewed = _configReviewBadgeService.IsSectionFullyReviewed("Optimize")
            || !FeatureDefinitions.OptimizeFeatures.Any(f => _configReviewBadgeService.IsFeatureInConfig(f));

        bool customizeReviewed = _configReviewBadgeService.IsSectionFullyReviewed("Customize")
            || !FeatureDefinitions.CustomizeFeatures.Any(f => _configReviewBadgeService.IsFeatureInConfig(f));

        CanApplyReviewedConfig = allSettingsReviewed && softwareAppsReviewed && optimizeReviewed && customizeReviewed;

        var open = new List<string>();
        if (!softwareAppsReviewed)
        {
            if (AppListOpen(FeatureIds.WindowsApps, _vmCoordinator.HasSelectedWindowsApps,
                    _vmCoordinator.IsWindowsAppsInstallAction || _vmCoordinator.IsWindowsAppsRemoveAction))
                open.Add(Localized("SoftwareApps_Tab_WindowsApps", "Windows Apps & Features"));
            if (AppListOpen(FeatureIds.ExternalApps, _vmCoordinator.HasSelectedExternalApps,
                    _vmCoordinator.IsExternalAppsInstallAction || _vmCoordinator.IsExternalAppsRemoveAction))
                open.Add(Localized("SoftwareApps_Tab_ExternalApps", "External Software"));
            if (open.Count == 0)
                open.Add(Localized("Category_SoftwareApps_Title", "Software & Apps"));
        }
        if (!optimizeReviewed)
            open.Add(Localized("Nav_Optimize", "Optimize"));
        if (!customizeReviewed)
            open.Add(Localized("Nav_Customize", "Customize"));

        ReviewModeApplyBlockedTooltip = CanApplyReviewedConfig || open.Count == 0
            ? null
            : string.Format(Localized("Review_Mode_Apply_Blocked_Tooltip", "Still to review: {0}"), string.Join(", ", open));
    }

    private bool AppListOpen(string featureId, bool anySelected, bool actionChosen) =>
        _configReviewBadgeService.IsFeatureInConfig(featureId) && anySelected && !actionChosen;

    private string Localized(string key, string fallback) => _localizationService.GetStringOrDefault(key, fallback);

    private void UpdateReviewModeStatus()
    {
        if (!_configReviewModeService.IsInReviewMode)
        {
            ReviewModeStatusText = string.Empty;
            ReviewModeSetAsideDetail = null;
            return;
        }

        string status;
        if (_configReviewDiffService.TotalChanges > 0)
        {
            var format = _localizationService.GetStringOrDefault("Review_Mode_Status_Format", "{0} of {1} reviewed ({2} will be applied)");
            status = string.Format(format,
                _configReviewDiffService.ReviewedChanges,
                _configReviewDiffService.TotalChanges,
                _configReviewDiffService.ApprovedChanges);
        }
        else if (_configReviewDiffService.TotalConfigItems > 0)
        {
            status = _localizationService.GetStringOrDefault("Review_Mode_Status_AllMatch", "All settings already match config");
        }
        else
        {
            status = _localizationService.GetStringOrDefault("Review_Mode_Status_NoItems", "No configuration items to apply");
        }

        if (_configReviewModeService.SetAside is { Count: > 0 } setAside)
        {
            var format = _localizationService.GetStringOrDefault("Config_SetAside_Format", "{0} settings in this file are not on this PC and were set aside.");
            status += " - " + string.Format(format, setAside.Count);
            ReviewModeSetAsideDetail = string.Join("\n", setAside);
        }
        else
        {
            ReviewModeSetAsideDetail = null;
        }

        ReviewModeStatusText = status;
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
    private Task AcceptAllRemainingAsync()
    {
        _vmCoordinator.AcceptAllRemaining();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task CancelReviewModeAsync()
    {
        var title = _localizationService.GetStringOrDefault("Review_Mode_Cancel_Confirmation_Title", "Leave Config Review");
        var message = _localizationService.GetStringOrDefault("Review_Mode_Cancel_Confirmation", "Leave Config Review? Nothing from the imported config will be applied.");

        var confirmed = (await _dialogService.ShowConfirmationAsync(
            new ConfirmationRequest
            {
                Message = message,
                Title = title,
                ConfirmButtonText = _localizationService.GetString("Button_Leave"),
                CancelButtonText = _localizationService.GetString("Button_Stay"),
            })).Confirmed;
        if (confirmed)
        {
            await _configurationService.CancelReviewModeAsync();
        }
    }
}
