using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Common.Interfaces
{
    public interface ISettingsLoadingService
    {
        Task<ObservableCollection<object>> LoadConfiguredSettingsAsync<TDomainService>(
            TDomainService domainService,
            string featureModuleId,
            string progressMessage,
            ISettingsFeatureViewModel? parentViewModel = null)
            where TDomainService : class, IDomainService;

        Task<SettingItemViewModel> CreateSettingViewModelAsync(
            SettingDefinition setting,
            Dictionary<string, SettingStateResult> batchStates,
            ISettingsFeatureViewModel? parentViewModel);
    }
}