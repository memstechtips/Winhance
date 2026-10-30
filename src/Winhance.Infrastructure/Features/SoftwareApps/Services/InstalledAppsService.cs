using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Catalogs;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

internal sealed class InstalledAppsService(
    IWindowsRegistryService registry,
    IAppxPackageSource appxPackageSource,
    IWinGetDetectionService winGetDetectionService,
    IChocolateyService chocolateyService,
    IWinGetBootstrapper winGetBootstrapper,
    IWinGetPackageInstaller winGetPackageInstaller,
    ILogService logService) : IInstalledAppsService
{
    public async Task<InstalledAppsScan> GetInstalledAppsAsync(CancellationToken cancellationToken = default)
    {
        var programs = Task.Run(() => UninstallRegistryReader.Read(registry), cancellationToken);
        var storePackages = appxPackageSource.GetStorePackagesAsync(cancellationToken);
        var winGetPackages = winGetDetectionService.GetInstalledPackagesAsync(cancellationToken);
        var chocoInstalled = chocolateyService.GetInstalledPackageIdsAsync(cancellationToken);
        var chocoOutdated = chocolateyService.GetOutdatedPackagesAsync(cancellationToken);
        await Task.WhenAll(programs, storePackages, winGetPackages, chocoInstalled, chocoOutdated).ConfigureAwait(false);

        var catalog = ExternalAppDefinitions.GetExternalApps().Items.Concat(WindowsAppDefinitions.GetWindowsApps().Items).ToList();
        var apps = InstalledAppsBuilder.Build(programs.Result, storePackages.Result, winGetPackages.Result ?? [], chocoInstalled.Result,
            chocoOutdated.Result ?? new Dictionary<string, string>(), catalog);
        logService.LogInformation($"Installed apps: {apps.Count} ({programs.Result.Count} programs, {storePackages.Result.Count} Store packages, "
            + $"{winGetPackages.Result?.Count.ToString() ?? "no"} winget matches, {apps.Count(a => a.HasUpdate)} updates)");

        // winget's list comes over COM only. A PC without winget has nothing to ask there, so Chocolatey alone answers.
        var chocolateyAlone = !winGetBootstrapper.IsSystemWinGetAvailable
            && await chocolateyService.IsChocolateyInstalledAsync(cancellationToken).ConfigureAwait(false);
        var updatesKnown = chocoOutdated.Result is not null && (winGetPackages.Result is not null || chocolateyAlone);
        return new InstalledAppsScan(apps, updatesKnown);
    }

    public async Task<OperationResult<bool>> UpdateAsync(InstalledApp app, CancellationToken cancellationToken = default)
    {
        try
        {
            var (success, error) = app.UpdateVia switch
            {
                PackageSources.Chocolatey => (await chocolateyService.UpgradePackageAsync(app.ChocoId!, app.Name, cancellationToken).ConfigureAwait(false), null),
                PackageSources.WinGet => FromInstall(await winGetPackageInstaller.UpgradePackageAsync(app.WinGetId!, WinGetSources.WinGet, app.Name, cancellationToken).ConfigureAwait(false)),
                PackageSources.MsStore => FromInstall(await winGetPackageInstaller.UpgradePackageAsync(app.MsStoreId!, WinGetSources.MsStore, app.Name, cancellationToken).ConfigureAwait(false)),
                _ => (false, $"{app.Name} has no update"),
            };
            return success ? OperationResult<bool>.Succeeded(true) : OperationResult<bool>.Failed(error ?? $"Updating {app.Name} failed");
        }
        finally
        {
            // A cancelled update may have changed the app before it stopped.
            winGetDetectionService.InvalidateInstalledPackages();
        }

        static (bool, string?) FromInstall(PackageInstallResult result) => (result.Success, result.ErrorMessage);
    }
}
