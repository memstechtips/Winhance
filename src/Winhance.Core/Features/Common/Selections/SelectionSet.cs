namespace Winhance.Core.Features.Common.Selections;

public sealed record CarriedFile(string SettingId, string Destination, string Base64);

public sealed record SelectionSet(
    IReadOnlyList<SettingChoice> Settings,
    IReadOnlyList<AppChoice> WindowsApps,
    IReadOnlyList<AppChoice> ExternalApps)
{
    public IReadOnlyList<CarriedFile> Files { get; init; } = Array.Empty<CarriedFile>();

    public static readonly SelectionSet Empty =
        new(Array.Empty<SettingChoice>(), Array.Empty<AppChoice>(), Array.Empty<AppChoice>());
}
