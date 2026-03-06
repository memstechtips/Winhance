using Winhance.UI.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.UI.Features.Optimize.Models;

/// <summary>
/// Contains metadata for an optimization section.
/// </summary>
public class OptimizeSectionInfo : ISectionInfo
{
    /// <summary>
    /// Gets the unique key for the section (e.g., "Sound", "Privacy").
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

    public OptimizeSectionInfo(string key, string iconGlyphKey, string displayName, string moduleId)
    {
        Key = key;
        IconGlyphKey = iconGlyphKey;
        DisplayName = displayName;
        ModuleId = moduleId;
    }
}
