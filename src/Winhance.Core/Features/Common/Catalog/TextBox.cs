using System.Text.RegularExpressions;
using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Features.Common.Catalog;

public sealed record TextBox(
    TextRule Rule,
    string? Default = null,
    string? SeedKey = null,
    PickerKind Picker = PickerKind.None,
    LocKey? Placeholder = null);

public enum PickerKind
{
    None,
    Folder,
    File,
}

public sealed record TextRule(string Pattern, bool UpperCase, LocKey Message)
{
    public string Normalize(string value) => UpperCase ? value.Trim().ToUpperInvariant() : value.Trim();

    public bool Matches(string value) => Regex.IsMatch(Normalize(value), Pattern);
}
