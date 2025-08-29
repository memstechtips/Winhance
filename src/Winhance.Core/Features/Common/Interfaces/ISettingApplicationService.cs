using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Application service interface for coordinating setting operations across domains.
    /// Follows Clean Architecture by handling use cases and orchestrating domain services.
    /// </summary>
    public interface ISettingApplicationService
    {
        Task ApplySettingAsync(string settingId, bool enable, object? value = null);

        Task ApplySettingAsync(string settingId, bool enable, object? value, bool applyWallpaper);


        /// <summary>
        /// Gets the current state and value of a setting by finding the appropriate domain service.
        /// </summary>
        /// <param name="settingId">The ID of the setting to check.</param>
        /// <returns>A result containing the setting's current state and value.</returns>
        Task<SettingApplicationResult> GetSettingStateAsync(string settingId);

        /// <summary>
        /// Gets all settings from all domain services.
        /// </summary>
        /// <returns>A collection of all application settings across all domains.</returns>
        Task<IEnumerable<SettingDefinition>> GetAllSettingsAsync();

        /// <summary>
        /// Gets settings from a specific domain service.
        /// </summary>
        /// <param name="domainName">The name of the domain to get settings from.</param>
        /// <returns>A collection of settings from the specified domain.</returns>
        Task<IEnumerable<SettingDefinition>> GetSettingsByDomainAsync(string domainName);

        /// <summary>
        /// Executes an ActionCommand by finding the appropriate domain service and method.
        /// </summary>
        /// <param name="settingId">The ID of the setting with the ActionCommand.</param>
        /// <param name="commandString">The ActionCommand string to execute (e.g., "CleanWindows11StartMenu").</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ExecuteActionCommandAsync(string settingId, string commandString);

        /// <summary>
        /// Executes an ActionCommand with additional context parameters.
        /// </summary>
        /// <param name="context">The execution context containing all parameters for the action.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ExecuteActionCommandAsync(ActionExecutionContext context);
    }

    /// <summary>
    /// Result model for setting application operations.
    /// </summary>
    public class SettingApplicationResult
    {
        public bool IsEnabled { get; set; }
        public object? CurrentValue { get; set; }
        public bool Status { get; set; }
        public string? ErrorMessage { get; set; }
        public bool Success { get; set; }
    }
}