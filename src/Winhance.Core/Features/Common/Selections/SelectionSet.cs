namespace Winhance.Core.Features.Common.Selections;

public sealed record SelectionSet(
    IReadOnlyList<SettingChoice> Settings,
    IReadOnlyList<AppChoice> WindowsApps,
    IReadOnlyList<AppChoice> ExternalApps,
    AutounattendChoices Autounattend)
{
    public static readonly SelectionSet Empty =
        new(Array.Empty<SettingChoice>(), Array.Empty<AppChoice>(), Array.Empty<AppChoice>(), AutounattendChoices.None);
}
