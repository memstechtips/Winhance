using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Features.Common.Catalog;

public sealed record List(
    IReadOnlyList<Field> Fields,
    AutounattendFirstLogonCommand? PasswordCleanup = null,
    IReadOnlyDictionary<string, string>? Seed = null,
    LocKey? Add = null,
    LocKey? Remove = null,
    bool CanBeEmpty = false);

public sealed record Field(
    string Key,
    FieldKind Kind,
    LocKey Label,
    TextRule? Rule = null,
    IReadOnlyList<LocKey>? Options = null,
    LocKey? Tooltip = null,
    string? Default = null,
    bool OneRowOnly = false,
    FieldCondition? OnlyWhen = null,
    bool MultiLine = false,
    IReadOnlyList<BuildRange>? Builds = null,
    FileFilter? Filter = null);

// The picker a File field opens with: the filter's caption and its pattern, such as "*.exe;*.msi".
public sealed record FileFilter(LocKey Name, string Pattern);

public sealed record FieldCondition(string FieldKey, params int[] OptionIndexes)
{
    public bool Equals(FieldCondition? other) =>
        other is not null && FieldKey == other.FieldKey && OptionIndexes.SequenceEqual(other.OptionIndexes);

    public override int GetHashCode() => HashCode.Combine(FieldKey, OptionIndexes.Length);
}

public enum FieldKind
{
    Text,
    Password,
    Selection,
    CheckBox,
    File,
}
