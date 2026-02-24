using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface ISpecialSettingHandler
    {
        Task<bool> TryApplySpecialSettingAsync(
            SettingDefinition setting,
            object value,
            bool additionalContext = false,
            ISettingApplicationService? settingApplicationService = null);

        Task<Dictionary<string, Dictionary<string, object?>>> DiscoverSpecialSettingsAsync(
            IEnumerable<SettingDefinition> settings)
        {
            return Task.FromResult(new Dictionary<string, Dictionary<string, object?>>());
        }
    }
}
