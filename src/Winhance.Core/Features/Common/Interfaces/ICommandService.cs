using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Service for executing system commands related to optimizations.
    /// </summary>
    public interface ICommandService
    {
        /// <summary>
        /// Executes the specified command with elevated privileges if required.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <param name="requiresElevation">Whether the command requires elevation.</param>
        /// <returns>The result of the command execution.</returns>
        Task<(bool Success, string Output, string Error)> ExecuteCommandAsync(string command, bool requiresElevation = true);
        
        /// <summary>
        /// Applies the command settings based on the enabled state.
        /// </summary>
        /// <param name="settings">The command settings to apply.</param>
        /// <param name="isEnabled">Whether the optimization is enabled.</param>
        /// <returns>A result indicating success or failure with details.</returns>
        Task<(bool Success, string Message)> ApplyCommandSettingsAsync(IEnumerable<CommandSetting> settings, bool isEnabled);
        
        /// <summary>
        /// Gets the current state of a command setting.
        /// </summary>
        /// <param name="setting">The command setting to check.</param>
        /// <returns>True if the setting is in its enabled state, false otherwise.</returns>
        Task<bool> IsCommandSettingEnabledAsync(CommandSetting setting);
    }
}
