using Winhance.Core.Features.SoftwareApps.Enums;

namespace Winhance.Core.Features.SoftwareApps.Models;

// An installed app the catalog does not list, shown in its own External Software group. The exact Uninstall key, or
// the package name, keeps its uninstall from matching a different program by name. A package manager that knows the
// app removes it first: it checks the app is gone afterwards, where an uninstall string is only launched (a winget
// portable's string is itself a winget command, and launching it did nothing on the 26H2 VM). Its failure falls back
// to that exact key.
public static class OtherInstalledItem
{
    public const string GroupName = "Other Installed Apps";

    public static ItemDefinition Create(InstalledApp app) => app.Kind == InstalledAppKind.StoreApp
        ? new ItemDefinition
        {
            Id = app.Key,
            Name = app.Name,
            Description = app.Publisher ?? string.Empty,
            GroupName = GroupName,
            AppxPackageName = [app.PackageName!],
            DetectedVia = DetectionSource.AppX,
            IsInstalled = true,
        }
        : new ItemDefinition
        {
            Id = app.Key,
            Name = app.Name,
            Description = app.Publisher ?? string.Empty,
            GroupName = GroupName,
            RegistrySubKeyName = app.RegistryKeyName,
            WinGetPackageId = app.WinGetId is { } winGetId ? [winGetId] : null,
            MsStoreId = app.MsStoreId,
            ChocoPackageId = app.InstalledByChocolatey ? app.ChocoId : null,
            DetectedVia = app.InstalledByChocolatey ? DetectionSource.Chocolatey
                : app.WinGetId is not null || app.MsStoreId is not null ? DetectionSource.WinGet
                : DetectionSource.Registry,
            IsInstalled = true,
        };

    // The id a config file or answer file saves the app under, null when no package manager offers it. Chocolatey
    // never appears: the scan only knows a Chocolatey id for catalog apps.
    public static string? PackageId(InstalledApp app) =>
        app.WinGetId is { } winGetId ? WinGetPackageItem.IdPrefix + winGetId
        : app.MsStoreId is { } storeId ? MsStorePackageItem.IdPrefix + storeId
        : null;
}
