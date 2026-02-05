using Winhance.Core.Features.Common.Models;

namespace Winhance.UI.Features.Common.Interfaces;

/// <summary>
/// Service for localizing setting definitions.
/// </summary>
public interface ISettingLocalizationService
{
    /// <summary>
    /// Localizes a setting definition's properties.
    /// </summary>
    SettingDefinition LocalizeSetting(SettingDefinition setting);

    /// <summary>
    /// Localizes a setting group's properties.
    /// </summary>
    SettingGroup LocalizeSettingGroup(SettingGroup group);

    /// <summary>
    /// Builds a localized message showing cross-group child settings grouped by feature and group.
    /// </summary>
    string? BuildCrossGroupInfoMessage(SettingDefinition setting);
}
