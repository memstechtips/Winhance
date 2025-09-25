using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;

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
    }
}