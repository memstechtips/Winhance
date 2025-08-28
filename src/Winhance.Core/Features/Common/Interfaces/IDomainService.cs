using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IDomainService : ISharedSettingOperations
    {
        Task<IEnumerable<SettingDefinition>> GetSettingsAsync();
        Task<IEnumerable<SettingDefinition>> GetRawSettingsAsync();
        string DomainName { get; }
    }
}
