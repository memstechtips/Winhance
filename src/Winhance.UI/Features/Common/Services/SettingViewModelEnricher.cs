using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Services;

public class SettingViewModelEnricher : ISettingViewModelEnricher
{
    private readonly IHardwareDetectionService _hardwareDetectionService;
    private readonly ISettingReviewDiffApplier _reviewDiffApplier;

    public SettingViewModelEnricher(
        IHardwareDetectionService hardwareDetectionService,
        ISettingReviewDiffApplier reviewDiffApplier)
    {
        _hardwareDetectionService = hardwareDetectionService;
        _reviewDiffApplier = reviewDiffApplier;
    }

    public async Task DetectBatteryAsync(SettingItemViewModel viewModel)
    {
        // Task.Run at the call site: the first HasBattery() blocks on WMI and this is the UI thread.
        // Unknown shows both AC and DC.
        viewModel.HasBattery = await Task.Run(() => _hardwareDetectionService.HasBattery()) ?? true;
    }

    public void ApplyReviewDiff(SettingItemViewModel viewModel, SettingStateResult currentState)
    {
        _reviewDiffApplier.ApplyReviewDiffToViewModel(viewModel, currentState);
    }
}
