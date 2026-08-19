using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.ViewModels;

namespace Winhance.UI.Features.Common.Services;

public sealed class AppSelectionSource : IAppSelectionSource
{
    private readonly IWindowsAppsItemsProvider _windowsApps;
    private readonly IExternalAppsItemsProvider _externalApps;

    public AppSelectionSource(IWindowsAppsItemsProvider windowsApps, IExternalAppsItemsProvider externalApps)
    {
        _windowsApps = windowsApps;
        _externalApps = externalApps;
    }

    public async Task<IReadOnlyList<AppChoice>> CheckedWindowsAppsAsync()
    {
        if (!_windowsApps.IsInitialized) await _windowsApps.LoadItemsAsync();
        return _windowsApps.Items.Where(i => i.IsSelected).Select(WindowsApp).ToList();
    }

    public async Task<IReadOnlyList<AppChoice>> InstalledWindowsAppsAsync()
    {
        if (!_windowsApps.IsInitialized) await _windowsApps.LoadItemsAsync();
        return _windowsApps.Items.Where(i => i.IsInstalled).Select(WindowsApp).ToList();
    }

    public async Task<IReadOnlyList<AppChoice>> CheckedExternalAppsAsync()
    {
        if (!_externalApps.IsInitialized) await _externalApps.LoadItemsAsync();
        return _externalApps.Items.Where(i => i.IsSelected)
            .Select(i => new AppChoice(i.Id, i.Name, null, null, null, i.Definition.WinGetPackageId is { Length: > 0 } ids ? ids[0] : null))
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
