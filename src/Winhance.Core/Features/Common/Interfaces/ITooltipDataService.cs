using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Service for managing tooltip data in a reactive manner.
/// Follows SRP by handling only tooltip data retrieval and refresh operations.
/// </summary>
public interface ITooltipDataService
{
    /// <summary>
    /// Gets tooltip data for the specified settings
    /// </summary>
    /// <param name="settings">The settings to get tooltip data for</param>
    /// <returns>A dictionary mapping setting IDs to tooltip data</returns>
    Task<IReadOnlyDictionary<string, SettingTooltipData>> GetTooltipDataAsync(IEnumerable<SettingDefinition> settings);

    /// <summary>
    /// Refreshes tooltip data for a specific setting by retrieving current registry values
    /// </summary>
    /// <param name="settingId">The ID of the setting to refresh</param>
    /// <param name="setting">The application setting model</param>
    /// <returns>Updated tooltip data for the setting, or null if not found</returns>
    Task<SettingTooltipData?> RefreshTooltipDataAsync(string settingId, SettingDefinition setting);

    /// <summary>
    /// Refreshes tooltip data for multiple settings efficiently
    /// </summary>
    /// <param name="settings">The settings to refresh tooltip data for</param>
    /// <returns>A dictionary mapping setting IDs to updated tooltip data</returns>
    Task<IReadOnlyDictionary<string, SettingTooltipData>> RefreshMultipleTooltipDataAsync(IEnumerable<SettingDefinition> settings);
}
