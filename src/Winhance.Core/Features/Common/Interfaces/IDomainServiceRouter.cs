using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IDomainServiceRouter
    {
        IDomainService GetDomainService(string featureIdOrSettingId);
        void AddSettingMappings(string featureId, IEnumerable<string> settingIds);
    }
}
