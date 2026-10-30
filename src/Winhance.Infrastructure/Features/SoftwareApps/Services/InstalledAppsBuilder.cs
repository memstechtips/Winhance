using Winhance.Core.Features.SoftwareApps.Models;

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
        IReadOnlyList<ItemDefinition> catalog)
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
            }, winGet, definition, chocoInstalled, chocoOutdated));
        }

        var programNames = new HashSet<string>(programs.Select(p => p.DisplayName), StringComparer.OrdinalIgnoreCase);
        var storeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in storePackages)
        {
            if (package.IsFramework || package.IsResource || package.IsSystem
                || programNames.Contains(package.DisplayName) || !storeNames.Add(package.Name))
                continue;

            var winGet = byFamilyName.GetValueOrDefault(package.FamilyName);
            var definition = catalog.FirstOrDefault(d => d.AppxPackageName?.Contains(package.Name, StringComparer.OrdinalIgnoreCase) == true);
            apps.Add(WithSources(new InstalledApp($"{StoreKeyPrefix}{package.FamilyName}", package.DisplayName, InstalledAppKind.StoreApp)
            {
                Publisher = package.Publisher,
                Version = package.Version,
                InstalledOn = package.InstalledOn,
                PackageName = package.Name,
            }, winGet, definition, chocoInstalled, chocoOutdated));
        }

        return apps.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static bool MatchesProgram(ItemDefinition definition, UninstallEntry entry, WinGetInstalledPackage? winGet)
    {
        if (winGet?.SourceName is not null && definition.WinGetPackageId?.Contains(winGet.Id, StringComparer.OrdinalIgnoreCase) == true)
            return true;
        if (!string.IsNullOrEmpty(definition.RegistrySubKeyName) && AppStatusDiscoveryService.MatchesPattern(entry.KeyName, definition.RegistrySubKeyName))
            return true;
        if (!string.IsNullOrEmpty(definition.RegistryDisplayName))
            return AppStatusDiscoveryService.MatchesPattern(entry.DisplayName, definition.RegistryDisplayName);
        return string.IsNullOrEmpty(definition.RegistrySubKeyName)
            && string.Equals(definition.Name, entry.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    // An app Chocolatey installed takes its update from Chocolatey: winget updating it behind Chocolatey's back leaves
    // choco's own record on the old version.
    private static InstalledApp WithSources(
        InstalledApp app, WinGetInstalledPackage? winGet, ItemDefinition? definition,
        IReadOnlySet<string> chocoInstalled, IReadOnlyDictionary<string, string> chocoOutdated)
    {
        var winGetId = winGet?.SourceName == WinGetSources.WinGet ? winGet.Id : definition?.WinGetPackageId?.FirstOrDefault();
        var msStoreId = winGet?.SourceName == WinGetSources.MsStore ? winGet.Id : definition?.MsStoreId;
        var chocoId = definition?.ChocoPackageId;
        var installedByChocolatey = chocoId is not null && chocoInstalled.Contains(chocoId);
        var (updateVia, availableVersion) =
            installedByChocolatey && chocoOutdated.TryGetValue(chocoId!, out var chocoVersion) ? (PackageSources.Chocolatey, chocoVersion)
            : !installedByChocolatey && winGet?.AvailableVersion is { } winGetVersion && winGet.SourceName == WinGetSources.WinGet ? (PackageSources.WinGet, winGetVersion)
            : !installedByChocolatey && winGet?.AvailableVersion is { } storeVersion && winGet.SourceName == WinGetSources.MsStore ? (PackageSources.MsStore, storeVersion)
            : (PackageSources.None, (string?)null);
        return app with
        {
            WinGetId = winGetId,
            MsStoreId = msStoreId,
            ChocoId = chocoId,
            InstalledByChocolatey = installedByChocolatey,
            UpdateVia = updateVia,
            AvailableVersion = availableVersion,
            CatalogId = definition?.Id,
        };
    }
}
