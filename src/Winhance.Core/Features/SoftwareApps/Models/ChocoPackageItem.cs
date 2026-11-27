namespace Winhance.Core.Features.SoftwareApps.Models;

// An External App found by searching Chocolatey, saved the same way as a winget search pick. Chocolatey's search
// gives ids only, and its ids are readable names (vlc, notepadplusplus), so the id doubles as the name.
public static class ChocoPackageItem
{
    public const string IdPrefix = "choco-";
    public const string GroupName = "Chocolatey";

    public static ItemDefinition Create(string packageId) => new()
    {
        Id = IdPrefix + packageId,
        Name = packageId,
        Description = packageId,
        GroupName = GroupName,
        ChocoPackageId = packageId,
    };

    public static ItemDefinition? FromId(string id) =>
        PackageSearchItem.PackageIdIn(id, IdPrefix) is { } packageId ? Create(packageId) : null;

    public static bool Is(string id) => id.StartsWith(IdPrefix, StringComparison.Ordinal);
}
