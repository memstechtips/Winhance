using System.Collections.Generic;
using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IDomainServiceRouter : ISharedSettingOperations
    {
        Task<Dictionary<string, bool>> GetMultipleSettingsStateAsync(
            IEnumerable<string> settingIds
        );
        Task<Dictionary<string, object?>> GetMultipleSettingsValuesAsync(
            IEnumerable<string> settingIds
        );
        IDomainService GetDomainService(string featureIdOrSettingId);
        void AddSettingMappings(string featureId, IEnumerable<string> settingIds);
    }
}
