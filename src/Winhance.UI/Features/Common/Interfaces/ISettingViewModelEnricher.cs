using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Interfaces;

/// <summary>
/// Enriches a SettingItemViewModel with hardware detection, cross-group info, and review diff state.
/// Consolidates the three post-construction enrichment services (IHardwareDetectionService,
/// ISettingLocalizationService, ISettingReviewDiffApplier) behind a single interface.
/// </summary>
public interface ISettingViewModelEnricher
{
    /// <summary>
    /// Detects whether the device has a battery and sets HasBattery on the ViewModel.
    /// </summary>
    Task DetectBatteryAsync(SettingItemViewModel viewModel);

    /// <summary>
    /// Builds and sets the cross-group info message for selection settings.
    /// </summary>
    void SetCrossGroupInfoMessage(SettingItemViewModel viewModel, SettingDefinition setting);

    /// <summary>
    /// Applies review mode diff state to the ViewModel.
    /// </summary>
    void ApplyReviewDiff(SettingItemViewModel viewModel, SettingStateResult currentState);
}
