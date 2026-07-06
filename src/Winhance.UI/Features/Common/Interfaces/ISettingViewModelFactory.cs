using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Interfaces;

/// <summary>
/// Creates fully-configured SettingItemViewModel instances from the new Setting model.
/// </summary>
public interface ISettingViewModelFactory
{
    /// <summary>
    /// Creates a fully-configured SettingItemViewModel for the given catalog Setting and current state.
    /// </summary>
    Task<SettingItemViewModel> CreateAsync(
        Setting setting,
        InputType inputType,
        SettingStateResult currentState,
        ISettingsFeatureViewModel? parentViewModel,
        string? crossGroupInfoMessage,
        ComboBoxSetupResult? builderComboBoxOptions,
        string? compatibilityMessage);
}
