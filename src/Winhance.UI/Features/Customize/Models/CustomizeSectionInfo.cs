namespace Winhance.UI.Features.Customize.Models;

/// <summary>
/// Contains metadata for a customization section.
/// </summary>
public class CustomizeSectionInfo
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
    /// Gets the display name of the section.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the page type for navigation.
    /// </summary>
    public Type PageType { get; }

    /// <summary>
    /// Gets the description of the section.
    /// </summary>
    public string Description { get; }

    public CustomizeSectionInfo(string key, string iconGlyphKey, string displayName, string description, Type pageType)
    {
        Key = key;
        IconGlyphKey = iconGlyphKey;
        DisplayName = displayName;
        Description = description;
        PageType = pageType;
    }
}
