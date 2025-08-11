using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Status aggregation for registry-backed settings.
    /// </summary>
    public interface IRegistryStatus
    {
        Task<RegistrySettingStatus> GetSettingStatusAsync(RegistrySetting setting);
        Task<RegistrySettingStatus> GetLinkedSettingsStatusAsync(LinkedRegistrySettings linkedSettings);
        Task<RegistrySettingStatus> GetOptimizationSettingStatusAsync(OptimizationSetting setting);
    }
}
