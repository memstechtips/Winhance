namespace Winhance.UI.Features.AdvancedTools.Models;

/// <summary>
/// Contains metadata for an advanced tools section.
/// </summary>
public class AdvancedToolsSectionInfo
{
    /// <summary>
    /// Gets the unique key for the section (e.g., "WimUtil", "AutounattendXml").
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the icon resource key for the section.
    /// </summary>
    public string IconResourceKey { get; }

    /// <summary>
    /// Gets the display name of the section.
    /// </summary>
    public string DisplayName { get; }

    public AdvancedToolsSectionInfo(string key, string iconResourceKey, string displayName)
    {
        Key = key;
        IconResourceKey = iconResourceKey;
        DisplayName = displayName;
    }
}
