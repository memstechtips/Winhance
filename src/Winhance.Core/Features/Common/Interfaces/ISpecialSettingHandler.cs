namespace Winhance.Core.Features.Common.Interfaces;

public interface ISpecialSettingHandler
{
    Task<bool> TryApplySpecialSettingAsync(
        string settingId,
        object value,
        ISettingApplicationService? settingApplicationService = null);
}
