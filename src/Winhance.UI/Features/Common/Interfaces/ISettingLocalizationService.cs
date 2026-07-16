using Winhance.Core.Features.Common.Catalog;

namespace Winhance.UI.Features.Common.Interfaces;

/// <summary>
/// Service for building the cross-group info banner for a setting. (Slice B2 retired LocalizeSetting - display
/// localization now happens on the catalog path in SettingViewModelFactory.)
/// </summary>
public interface ISettingLocalizationService
{
    /// <summary>
    /// Builds a localized message showing cross-group child settings grouped by feature and group.
    /// </summary>
    string? BuildCrossGroupInfoMessage(Setting setting);
}
