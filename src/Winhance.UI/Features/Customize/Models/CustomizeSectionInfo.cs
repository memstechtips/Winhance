using Winhance.UI.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.UI.Features.Customize.Models;

/// <summary>
/// Contains metadata for a customization section.
/// </summary>
public class CustomizeSectionInfo : ISectionInfo
{
    /// <summary>
    /// Gets the unique key for the section (e.g., "Explorer", "StartMenu").
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the icon glyph resource key for the section.
    /// </summary>
    public string IconGlyphKey { get; }

    /// <summary>
    /// Gets the display name of the section (fallback if ViewModel unavailable).
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the FeatureIds constant that identifies the ViewModel for this section.
    /// </summary>
    public string ModuleId { get; }

    public CustomizeSectionInfo(string key, string iconGlyphKey, string displayName, string moduleId)
    {
        Key = key;
        IconGlyphKey = iconGlyphKey;
        DisplayName = displayName;
        ModuleId = moduleId;
    }
}
