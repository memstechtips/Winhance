namespace Winhance.UI.Features.Common.Models;

/// <summary>
/// Represents a search suggestion item for cross-section searching.
/// </summary>
public class SearchSuggestionItem
{
    public string SettingName { get; set; } = string.Empty;
    public string SectionKey { get; set; } = string.Empty;
    public string SectionDisplayName { get; set; } = string.Empty;
    public string SectionIconGlyph { get; set; } = string.Empty;
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
