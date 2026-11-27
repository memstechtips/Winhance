namespace Winhance.Core.Features.SoftwareApps.Models;

// Any app saved by its package id alone: what config files and answer files rebuild without the catalog.
public static class PackageSearchItem
{
    public static bool Is(string id) => WinGetPackageItem.Is(id) || ChocoPackageItem.Is(id) || MsStorePackageItem.Is(id);

    public static ItemDefinition? FromId(string id) =>
        WinGetPackageItem.FromId(id) ?? ChocoPackageItem.FromId(id) ?? MsStorePackageItem.FromId(id);

    // The file may come from anyone, and the package id goes unquoted onto the winget and choco command lines and
    // names a staging folder. So only [A-Za-z0-9][A-Za-z0-9.+_-]* counts as a package id: no space, quote, slash,
    // backslash or leading dash.
    internal static string? PackageIdIn(string id, string prefix)
    {
        if (!id.StartsWith(prefix, StringComparison.Ordinal))
            return null;

        var packageId = id[prefix.Length..];
        return packageId.Length > 0
            && char.IsAsciiLetterOrDigit(packageId[0])
            && packageId.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '+' or '_' or '-')
            ? packageId
            : null;
    }
}

// An installed Store app the catalog does not list, saved by its Store id so another PC installs it from the Store.
// It joins the winget group: winget installs it, from its msstore source.
public static class MsStorePackageItem
{
    public const string IdPrefix = "msstore-";

    public static ItemDefinition Create(string storeId) => new()
    {
        Id = IdPrefix + storeId,
        Name = storeId,
        Description = storeId,
        GroupName = WinGetPackageItem.GroupName,
        MsStoreId = storeId,
    };

    public static ItemDefinition? FromId(string id) =>
        PackageSearchItem.PackageIdIn(id, IdPrefix) is { } storeId ? Create(storeId) : null;

    public static bool Is(string id) => id.StartsWith(IdPrefix, StringComparison.Ordinal);
}

public sealed record PackageSearchResult(IReadOnlyList<ItemDefinition> Items, bool SearchedChocolatey);
