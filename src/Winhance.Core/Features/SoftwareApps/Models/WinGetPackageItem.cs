namespace Winhance.Core.Features.SoftwareApps.Models;

public sealed record WinGetPackageMatch(string Id, string Name);

// An External App found by searching winget instead of picked from the catalog. The package id lives in the item
// id, so a config file or answer file that saved the pick can rebuild it without searching again. The prefix uses
// a hyphen because staging names a folder after the id.
public static class WinGetPackageItem
{
    public const string IdPrefix = "winget-";
    public const string GroupName = "WinGet";

    public static ItemDefinition Create(string packageId, string name) => new()
    {
        Id = IdPrefix + packageId,
        Name = name,
        Description = packageId,
        GroupName = GroupName,
        WinGetPackageId = [packageId],
    };

    public static ItemDefinition? FromId(string id) =>
        PackageSearchItem.PackageIdIn(id, IdPrefix) is { } packageId ? Create(packageId, packageId) : null;

    public static bool Is(string id) => id.StartsWith(IdPrefix, StringComparison.Ordinal);
}
