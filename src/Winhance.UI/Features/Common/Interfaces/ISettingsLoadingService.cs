using System.Collections.ObjectModel;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Interfaces;

public interface ISettingsLoadingService
{
    Task<ObservableCollection<SettingItemViewModel>> LoadConfiguredSettingsAsync(
        string featureModuleId,
        string progressMessage,
        ISettingsFeatureViewModel? parentViewModel = null);

    // Catalog order, under the scope the loads run against, so a caller can diff it against what it already
    // holds and insert an arrival where a full load would have put it.
    IReadOnlyList<string> GetFeatureSettingIds(string featureModuleId);

    // Cards for a named subset of a feature that is ALREADY initialized - the incremental scope refresh.
    Task<IReadOnlyList<SettingItemViewModel>> LoadSettingsSubsetAsync(
        string featureModuleId,
        IReadOnlyCollection<string> settingIds,
        ISettingsFeatureViewModel? parentViewModel = null);

    // Re-derives the card fields that come from the catalog SCOPE rather than from the setting, for cards
    // that outlived a scope change.
    Task RefreshScopeDerivedStateAsync(IEnumerable<SettingItemViewModel> settings);

    Task<Dictionary<string, SettingStateResult>> RefreshSettingStatesAsync(
        IEnumerable<SettingItemViewModel> settings);
}
