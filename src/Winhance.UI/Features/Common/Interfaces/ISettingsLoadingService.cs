using System.Collections.ObjectModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Interfaces;

/// <summary>
/// Service for loading and creating setting ViewModels.
/// </summary>
public interface ISettingsLoadingService
{
    /// <summary>
    /// Loads settings for a feature and creates ViewModels for each.
    /// </summary>
    Task<ObservableCollection<object>> LoadConfiguredSettingsAsync<TDomainService>(
        TDomainService domainService,
        string featureModuleId,
        string progressMessage,
        ISettingsFeatureViewModel? parentViewModel = null)
        where TDomainService : class, IDomainService;

    /// <summary>
    /// Creates a SettingItemViewModel for a given setting definition.
    /// </summary>
    Task<SettingItemViewModel> CreateSettingViewModelAsync(
        SettingDefinition setting,
        Dictionary<string, SettingStateResult> batchStates,
        ISettingsFeatureViewModel? parentViewModel);
}
