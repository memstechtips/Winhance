using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Interfaces;

public interface ISettingViewModelFactory
{
    Task<SettingItemViewModel> CreateAsync(
        Setting setting,
        SettingStateResult currentState,
        ISettingsFeatureViewModel? parentViewModel,
        string? crossGroupInfoMessage,
        ComboBoxSetupResult? builderComboBoxOptions,
        string? compatibilityMessage,
        WinBuild build = default);
}
