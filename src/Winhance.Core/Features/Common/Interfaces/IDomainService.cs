using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IDomainService
{
    Task<IEnumerable<SettingDefinition>> GetSettingsAsync();
    string DomainName { get; }

    void InvalidateCache()
    {
    }
}
