using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Interfaces;

public interface ISettingReviewDiffApplier
{
    void ApplyReviewDiffToViewModel(SettingItemViewModel viewModel, SettingStateResult currentState);
}
