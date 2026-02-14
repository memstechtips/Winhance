namespace Winhance.UI.Features.Customize.Models;

/// <summary>
/// Represents a search suggestion item for cross-section searching.
/// </summary>
public class SearchSuggestionItem
{
    /// <summary>
    /// Gets or sets the setting name that matched the search.
    /// </summary>
    public string SettingName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the section key where this setting is found.
    /// </summary>
    public string SectionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the section.
    /// </summary>
    public string SectionDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the icon glyph for the section.
    /// </summary>
    public string SectionIconGlyph { get; set; } = string.Empty;

    /// <summary>
    /// Gets the display text for the suggestion.
    /// </summary>
    public string DisplayText => $"{SettingName} ({SectionDisplayName})";

    public SearchSuggestionItem()
    {
    }

    public SearchSuggestionItem(string settingName, string sectionKey, string sectionDisplayName, string sectionIconGlyph)
    {
        SettingName = settingName;
        SectionKey = sectionKey;
        SectionDisplayName = sectionDisplayName;
        SectionIconGlyph = sectionIconGlyph;
    }
}
