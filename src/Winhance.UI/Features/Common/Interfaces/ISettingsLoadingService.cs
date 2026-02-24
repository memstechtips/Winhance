using System.Collections.ObjectModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Interfaces;

/// <summary>
/// Service for loading setting ViewModels and refreshing their states.
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
    /// Performs a lightweight refresh of setting states by re-reading from the system.
    /// Returns a dictionary of setting ID to current state.
    /// </summary>
    Task<Dictionary<string, SettingStateResult>> RefreshSettingStatesAsync(
        IEnumerable<SettingItemViewModel> settings);
}
