using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces;

public interface ISharedSettingOperations
{
    Task ApplySettingAsync(string settingId, bool enable, object? value = null);
    Task<bool> IsSettingEnabledAsync(string settingId);
    Task<object?> GetSettingValueAsync(string settingId);
}
