namespace Winhance.Core.Features.SoftwareApps.Models;

public enum InstalledAppKind
{
    Program,
    StoreApp,
}

public enum PackageSources
{
    None,
    WinGet,
    MsStore,
    Chocolatey,
}

// One app the installed-apps scan found: a program from the Uninstall keys or a Store package. The ids are what the
// package managers call it. UpdateVia names the one package manager that offers AvailableVersion, None when the app is
// up to date. CatalogId names the catalog item it is, null for an app the catalog does not list.
public sealed record InstalledApp(string Key, string Name, InstalledAppKind Kind)
{
    public string? Publisher { get; init; }

    public string? Version { get; init; }

    public DateTime? InstalledOn { get; init; }

    public long? SizeBytes { get; init; }

    public string? WinGetId { get; init; }

    public string? MsStoreId { get; init; }

    public string? ChocoId { get; init; }

    public bool InstalledByChocolatey { get; init; }

    public string? RegistryKeyName { get; init; }

    public string? PackageName { get; init; }

    public string? AvailableVersion { get; init; }

    public PackageSources UpdateVia { get; init; }

    public bool HasUpdate => UpdateVia != PackageSources.None;

    public string? CatalogId { get; init; }
}

// UpdatesKnown is false when a package manager this PC has could not be asked, or it has none: the apps are still
// listed, but an app without an update may only be one nobody was asked about.
public sealed record InstalledAppsScan(IReadOnlyList<InstalledApp> Apps, bool UpdatesKnown);

// What winget's installed-apps catalog says about one package. SourceName is the remote source winget matched it to
// (winget, msstore), null when it only knows the local install (ARP\..., MSIX\... ids).
public sealed record WinGetInstalledPackage(string Id, string Name, string? Version, string? SourceName, string? AvailableVersion)
{
    public IReadOnlyList<string> ProductCodes { get; init; } = [];

    public IReadOnlyList<string> PackageFamilyNames { get; init; } = [];
}

public sealed record StorePackage(string Name, string FamilyName, string DisplayName, string Version)
{
    public string? Publisher { get; init; }

    public DateTime? InstalledOn { get; init; }

    public bool IsFramework { get; init; }

    public bool IsResource { get; init; }

    public bool IsSystem { get; init; }
}
