namespace Winhance.UI.Features.Common.Models;

// The suggestion list's automation name and the text a chosen suggestion writes into the box both come from
// ToString, which a record would otherwise fill with every member.
public sealed record SearchSuggestionItem(string SettingName, string SectionKey, string SectionDisplayName)
{
    public override string ToString() => SettingName;
}
