using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IPowerCfgApplier
    {
        Task ApplyPowerCfgSettingsAsync(SettingDefinition setting, bool enable, object? value);
    }
}
