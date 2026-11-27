using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.ViewModels;

namespace Winhance.UI.Features.Common.Services;

public sealed class AppSelectionSource(
    IWindowsAppsItemsProvider windowsApps,
    IExternalAppsItemsProvider externalApps) : IAppSelectionSource
{
    public async Task<IReadOnlyList<AppChoice>> CheckedWindowsAppsAsync()
    {
        if (!windowsApps.IsInitialized) await windowsApps.LoadItemsAsync();
        return windowsApps.Items.Where(i => i.IsSelected).Select(WindowsApp).ToList();
    }

    public async Task<IReadOnlyList<AppChoice>> InstalledWindowsAppsAsync()
    {
        if (!windowsApps.IsInitialized) await windowsApps.LoadItemsAsync();
        return windowsApps.Items.Where(i => i.IsInstalled).Select(WindowsApp).ToList();
    }

    public async Task<IReadOnlyList<AppChoice>> CheckedExternalAppsAsync()
    {
        if (!externalApps.IsInitialized) await externalApps.LoadItemsAsync();
        // An installed app the catalog does not list travels by its package id; with none, another PC cannot install it.
        return externalApps.Items.Where(i => i.IsSelected && !(i.IsOtherInstalled && i.PackageId is null))
            .Select(i => new AppChoice(i.PackageId ?? i.Id, i.Name, null, null, null, i.Definition.WinGetPackageId is { Length: > 0 } ids ? ids[0] : null))
            .ToList();
    }

    // Appx wins over capability over optional feature - the same precedence the export has always written.
    private static AppChoice WindowsApp(AppItemViewModel item)
    {
        var d = item.Definition;
        if (d.AppxPackageName is { Length: > 0 } appx) return new AppChoice(item.Id, item.Name, appx, null, null, null);
        if (!string.IsNullOrEmpty(d.CapabilityName)) return new AppChoice(item.Id, item.Name, null, d.CapabilityName, null, null);
        if (!string.IsNullOrEmpty(d.OptionalFeatureName)) return new AppChoice(item.Id, item.Name, null, null, d.OptionalFeatureName, null);
        return new AppChoice(item.Id, item.Name, null, null, null, null);
    }
}
