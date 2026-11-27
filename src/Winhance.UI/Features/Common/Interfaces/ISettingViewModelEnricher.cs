using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Interfaces;

public interface ISettingViewModelEnricher
{
    Task DetectBatteryAsync(SettingItemViewModel viewModel);

    void ApplyReviewDiff(SettingItemViewModel viewModel, SettingStateResult currentState);
}
