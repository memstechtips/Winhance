using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IConfigurationCoordinatorService
    {
        Task SaveUnifiedConfigAsync();
        Task ImportUnifiedConfigAsync();
        Task<UnifiedConfigurationFile> CreateUnifiedConfigurationAsync();
        Task<bool> ApplyUnifiedConfigurationAsync(UnifiedConfigurationFile config, IEnumerable<string> selectedSections);
    }
}