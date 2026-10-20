using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Features.Common.Catalog;

public sealed record List(
    IReadOnlyList<Field> Fields,
    AutounattendFirstLogonCommand? PasswordCleanup = null,
    IReadOnlyDictionary<string, string>? Seed = null);

public sealed record Field(
    string Key,
    FieldKind Kind,
    LocKey Label,
    TextRule? Rule = null,
    IReadOnlyList<LocKey>? Options = null,
    LocKey? Tooltip = null,
    string? Default = null,
    bool OneRowOnly = false,
    FieldCondition? OnlyWhen = null);

public sealed record FieldCondition(string FieldKey, int OptionIndex);

public enum FieldKind
{
    Text,
    Password,
    Selection,
    CheckBox,
}
