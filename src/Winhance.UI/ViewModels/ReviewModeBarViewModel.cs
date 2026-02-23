using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.ViewModels;

/// <summary>
/// Child ViewModel for the review mode bar in the main window.
/// Manages review mode state, status text, and apply/cancel commands.
/// </summary>
public partial class ReviewModeBarViewModel : ObservableObject
{
    private readonly IConfigReviewModeService _configReviewModeService;
    private readonly IConfigReviewDiffService _configReviewDiffService;
    private readonly IConfigReviewBadgeService _configReviewBadgeService;
    private readonly IConfigurationService _configurationService;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILocalizationService _localizationService;
    private readonly IDialogService _dialogService;
    private readonly ILogService _logService;

    [ObservableProperty]
    public partial bool IsInReviewMode { get; set; }

    [ObservableProperty]
    public partial string ReviewModeStatusText { get; set; }

    [ObservableProperty]
    public partial bool CanApplyReviewedConfig { get; set; }

    public string ReviewModeTitleText =>
        _localizationService.GetString("Review_Mode_Title") ?? "Config Review Mode";

    public string ReviewModeApplyButtonText =>
        _localizationService.GetString("Review_Mode_Apply_Button") ?? "Apply Config";

    public string ReviewModeCancelButtonText =>
        _localizationService.GetString("Review_Mode_Cancel_Button") ?? "Cancel";

    public string ReviewModeDescriptionText =>
        _localizationService.GetString("Review_Mode_Description") ?? "Review the changes below across all sections, then click Apply Config when ready.";

    public ReviewModeBarViewModel(
        IConfigReviewModeService configReviewModeService,
        IConfigReviewDiffService configReviewDiffService,
        IConfigReviewBadgeService configReviewBadgeService,
        IConfigurationService configurationService,
        IDispatcherService dispatcherService,
        ILocalizationService localizationService,
        IDialogService dialogService,
        ILogService logService)
    {
        _configReviewModeService = configReviewModeService;
        _configReviewDiffService = configReviewDiffService;
        _configReviewBadgeService = configReviewBadgeService;
        _configurationService = configurationService;
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

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (IsInReviewMode)
        {
            OnPropertyChanged(nameof(ReviewModeTitleText));
            OnPropertyChanged(nameof(ReviewModeDescriptionText));
            OnPropertyChanged(nameof(ReviewModeApplyButtonText));
            OnPropertyChanged(nameof(ReviewModeCancelButtonText));
            UpdateReviewModeStatus();
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
            return;
        }

        // All Optimize/Customize settings must be explicitly reviewed (accept or reject)
        bool allSettingsReviewed = _configReviewDiffService.TotalChanges == 0
            || _configReviewDiffService.ReviewedChanges >= _configReviewDiffService.TotalChanges;

        // SoftwareApps action choices must be made for sections that have items
        bool softwareAppsReviewed = _configReviewBadgeService.IsSoftwareAppsReviewed
            || (!_configReviewBadgeService.IsFeatureInConfig(FeatureIds.WindowsApps)
                && !_configReviewBadgeService.IsFeatureInConfig(FeatureIds.ExternalApps));

        // All Optimize features must be fully reviewed
        bool optimizeReviewed = _configReviewBadgeService.IsSectionFullyReviewed("Optimize")
            || !FeatureDefinitions.OptimizeFeatures.Any(f => _configReviewBadgeService.IsFeatureInConfig(f));

        // All Customize features must be fully reviewed
        bool customizeReviewed = _configReviewBadgeService.IsSectionFullyReviewed("Customize")
            || !FeatureDefinitions.CustomizeFeatures.Any(f => _configReviewBadgeService.IsFeatureInConfig(f));

        CanApplyReviewedConfig = allSettingsReviewed && softwareAppsReviewed && optimizeReviewed && customizeReviewed;
    }

    private void UpdateReviewModeStatus()
    {
        if (!_configReviewModeService.IsInReviewMode)
        {
            ReviewModeStatusText = string.Empty;
            return;
        }

        if (_configReviewDiffService.TotalChanges > 0)
        {
            // Show reviewed/total count and how many will be applied
            var format = _localizationService.GetString("Review_Mode_Status_Format") ?? "{0} of {1} reviewed ({2} will be applied)";
            ReviewModeStatusText = string.Format(format,
                _configReviewDiffService.ReviewedChanges,
                _configReviewDiffService.TotalChanges,
                _configReviewDiffService.ApprovedChanges);
        }
        else if (_configReviewDiffService.TotalConfigItems > 0)
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
}
