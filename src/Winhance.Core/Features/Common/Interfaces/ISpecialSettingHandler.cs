using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces;

public interface ISpecialSettingHandler
{
    Task<bool> TryApplySpecialSettingAsync(
        string settingId,
        object value,
        bool additionalContext = false,
        ISettingApplicationService? settingApplicationService = null);
}
