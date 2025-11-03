using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface ICommandService
    {
        Task<(bool Success, string Output, string Error)> ExecuteCommandAsync(string command, bool requiresElevation = true);
        Task<(bool Success, string Message)> ApplyCommandSettingsAsync(IEnumerable<CommandSetting> settings, bool isEnabled);
        Task<bool> IsCommandSettingEnabledAsync(CommandSetting setting);
    }
}
