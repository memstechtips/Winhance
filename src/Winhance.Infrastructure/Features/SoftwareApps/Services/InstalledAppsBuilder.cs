using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.SoftwareApps.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

// Joins what each source knows about one app. winget matches a program by its Uninstall key (ProductCodes) and a Store
// app by its family name; the catalog adds the ids Winhance already curates for apps winget did not match.
internal static class InstalledAppsBuilder
{
    internal const string ProgramKeyPrefix = "reg:";
    internal const string StoreKeyPrefix = "appx:";

    internal static List<InstalledApp> Build(
        IReadOnlyList<UninstallEntry> programs,
        IReadOnlyList<StorePackage> storePackages,
        IReadOnlyList<WinGetInstalledPackage> winGetPackages,
        IReadOnlySet<string> chocoInstalled,
        IReadOnlyDictionary<string, string> chocoOutdated,
        IReadOnlyList<ItemDefinition> catalog,
        IReadOnlyDictionary<string, StoreCatalogUpdate> storeUpdates,
        Action<string>? traceStoreApp = null)
    {
        var byProductCode = new Dictionary<string, WinGetInstalledPackage>(StringComparer.OrdinalIgnoreCase);
        var byFamilyName = new Dictionary<string, WinGetInstalledPackage>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in winGetPackages)
        {
            foreach (var code in package.ProductCodes)
                byProductCode.TryAdd(code, package);
            foreach (var family in package.PackageFamilyNames)
                byFamilyName.TryAdd(family, package);
        }

        var apps = new List<InstalledApp>();
        foreach (var entry in programs)
        {
            var winGet = byProductCode.GetValueOrDefault(entry.KeyName);
            var definition = catalog.FirstOrDefault(d => MatchesProgram(d, entry, winGet));
            apps.Add(WithSources(new InstalledApp($"{ProgramKeyPrefix}{(entry.PerUser ? "user" : "machine")}:{entry.KeyName}", entry.DisplayName, InstalledAppKind.Program)
            {
                Publisher = entry.Publisher,
                Version = entry.Version,
                InstalledOn = entry.InstalledOn,
                SizeBytes = entry.SizeBytes,
                RegistryKeyName = entry.KeyName,
            }, winGet, definition, chocoInstalled, chocoOutdated, null, null));
        }

        var programNames = new HashSet<string>(programs.Select(p => p.DisplayName), StringComparer.OrdinalIgnoreCase);
        var storeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in storePackages)
        {
            if (package.IsFramework || package.IsResource || package.IsSystem
                || programNames.Contains(package.DisplayName) || !storeNames.Add(package.Name))
                continue;

            var winGet = byFamilyName.GetValueOrDefault(package.FamilyName);
            var definition = FindDefinition(package, catalog);
            apps.Add(WithSources(new InstalledApp($"{StoreKeyPrefix}{package.FamilyName}", package.DisplayName, InstalledAppKind.StoreApp)
            {
                Publisher = package.Publisher,
                Version = package.Version,
                InstalledOn = package.InstalledOn,
                PackageName = package.Name,
            }, winGet, definition, chocoInstalled, chocoOutdated, storeUpdates.GetValueOrDefault(package.FamilyName), traceStoreApp));
        }

        return apps.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    // The Store apps the catalog curates a Store id for, which is what the Store catalog can be asked about.
    internal static List<StoreCatalogApp> StoreCatalogApps(IReadOnlyList<StorePackage> storePackages, IReadOnlyList<ItemDefinition> catalog) =>
        storePackages
            .Where(p => !p.IsFramework && !p.IsResource && !p.IsSystem)
            .Select(p => (p.FamilyName, StoreId: FindDefinition(p, catalog)?.MsStoreId))
            .Where(x => x.StoreId is not null)
            .DistinctBy(x => x.FamilyName, StringComparer.OrdinalIgnoreCase)
            .Select(x => new StoreCatalogApp(x.StoreId!, x.FamilyName))
            .ToList();

    private static ItemDefinition? FindDefinition(StorePackage package, IReadOnlyList<ItemDefinition> catalog) =>
        catalog.FirstOrDefault(d => d.AppxPackageName?.Contains(package.Name, StringComparer.OrdinalIgnoreCase) == true);

    private static bool MatchesProgram(ItemDefinition definition, UninstallEntry entry, WinGetInstalledPackage? winGet) =>
        (winGet?.SourceName is not null && definition.WinGetPackageId?.Contains(winGet.Id, StringComparer.OrdinalIgnoreCase) == true)
        || InstalledStatus.MatchesUninstallEntry(definition, entry);

    // An app Chocolatey installed takes its update from Chocolatey: winget updating it behind Chocolatey's back leaves
    // choco's own record on the old version. A winget update is checked against winget's own installed version and
    // shown beside it: an Uninstall key can number the same install differently (a .NET SDK's key carries a bundle
    // version like 10.4.226.50611, not the SDK version winget names). A Store app's AppX version is what Windows has
    // installed, so the offer is checked against the higher of the two and the higher one is shown: winget's installed
    // version lags a fresh update while its COM server is still running. A Store
    // app winget has no update for takes the Store catalog's, shown against the bundle version that catalog numbers by.
    // An app also published outside the Store (Terminal on GitHub) can carry a bundle from there, numbered on its own
    // scheme (1.24 against the Store's 3001.24), so for an app winget maps to its own source a Store offer counts only
    // when both bundles share the leading number.
    private static InstalledApp WithSources(
        InstalledApp app, WinGetInstalledPackage? winGet, ItemDefinition? definition,
        IReadOnlySet<string> chocoInstalled, IReadOnlyDictionary<string, string> chocoOutdated, StoreCatalogUpdate? storeUpdate,
        Action<string>? trace)
    {
        var winGetId = winGet?.SourceName == WinGetSources.WinGet ? winGet.Id : definition?.WinGetPackageId?.FirstOrDefault();
        var msStoreId = winGet?.SourceName == WinGetSources.MsStore ? winGet.Id : definition?.MsStoreId;
        var chocoId = definition?.ChocoPackageId;
        var installedByChocolatey = chocoId is not null && chocoInstalled.Contains(chocoId);
        var installedVersion = app.Kind == InstalledAppKind.StoreApp && (winGet?.Version is null || PackageVersion.Compare(app.Version, winGet.Version) > 0)
            ? app.Version
            : winGet?.Version;
        var winGetUpdate = !installedByChocolatey && winGet?.AvailableVersion is { } offered
            && PackageVersion.IsUpdate(offered, installedVersion);
        var storeOffer = storeUpdate is not null
            && (winGet?.SourceName != WinGetSources.WinGet || LeadingNumber(storeUpdate.InstalledVersion) == LeadingNumber(storeUpdate.AvailableVersion));
        var (updateVia, availableVersion, version) =
            installedByChocolatey && chocoOutdated.TryGetValue(chocoId!, out var chocoVersion) ? (PackageSources.Chocolatey, chocoVersion, app.Version)
            : winGetUpdate && winGet!.SourceName == WinGetSources.WinGet ? (PackageSources.WinGet, winGet.AvailableVersion, installedVersion ?? app.Version)
            : winGetUpdate && winGet!.SourceName == WinGetSources.MsStore ? (PackageSources.MsStore, winGet.AvailableVersion, installedVersion ?? app.Version)
            : !installedByChocolatey && storeOffer && msStoreId is not null ? (PackageSources.MsStore, storeUpdate!.AvailableVersion, storeUpdate.InstalledVersion)
            : (PackageSources.None, (string?)null, app.Version);
        if (winGet?.AvailableVersion is not null || storeUpdate is not null)
            trace?.Invoke($"{app.Name}: AppX {app.Version}, winget {winGet?.Id ?? "none"} ({winGet?.SourceName ?? "no source"}) "
                + $"{winGet?.Version ?? "?"} -> {winGet?.AvailableVersion ?? "none"}, Store bundle {storeUpdate?.InstalledVersion ?? "?"} -> "
                + $"{storeUpdate?.AvailableVersion ?? "none"}, update via {updateVia} {availableVersion ?? ""}".TrimEnd());
        return app with
        {
            Version = version,
            WinGetId = winGetId,
            MsStoreId = msStoreId,
            ChocoId = chocoId,
            InstalledByChocolatey = installedByChocolatey,
            UpdateVia = updateVia,
            AvailableVersion = availableVersion,
            CatalogId = definition?.Id,
        };
    }

    private static string LeadingNumber(string version) => version.Split('.')[0];
}
