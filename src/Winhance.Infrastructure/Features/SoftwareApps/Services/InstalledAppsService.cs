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
    IStoreCatalogService storeCatalogService,
    IDismComponentReader dismComponentReader,
    ILogService logService) : IInstalledAppsService
{
    private readonly object _scanGate = new();
    private Task<ScanResult>? _appsScan;
    private Task<ScanResult>? _componentsScan;
    private InstalledApp? _lastUpdated;

    // What one read of the PC found, shared by every caller of that read: the apps are the same for everyone, the status
    // is worked out per caller from the sources.
    private sealed record ScanResult(ScanSources Sources, IReadOnlyList<InstalledApp> Apps, bool UpdatesKnown);

    // Callers that arrive while a scan runs share it; one that arrives after it finished, or after Invalidate, starts a
    // fresh read. A scan that also read DISM serves a caller that did not need it, never the other way round. The shared
    // scan ignores any one caller's token, each caller only stops waiting.
    public async Task<InstalledAppsScan> GetInstalledAppsAsync(IReadOnlyList<ItemDefinition> definitions, StatusRules rules, CancellationToken cancellationToken = default)
    {
        var needsComponents = rules == StatusRules.Windows
            && definitions.Any(d => !string.IsNullOrEmpty(d.CapabilityName) || !string.IsNullOrEmpty(d.OptionalFeatureName));
        var scan = await Share(needsComponents).WaitAsync(cancellationToken).ConfigureAwait(false);
        var status = rules == StatusRules.Windows
            ? InstalledStatus.Windows(definitions, scan.Sources, logService)
            : InstalledStatus.External(definitions, scan.Sources, logService);
        return new InstalledAppsScan(scan.Apps, scan.UpdatesKnown, status);
    }

    private Task<ScanResult> Share(bool needsComponents)
    {
        lock (_scanGate)
        {
            if (needsComponents)
                return _componentsScan ??= Start(true, scan => _componentsScan = scan);
            return _componentsScan ?? (_appsScan ??= Start(false, scan => _appsScan = scan));
        }
    }

    private Task<ScanResult> Start(bool withComponents, Action<Task<ScanResult>?> clear)
    {
        var started = Task.Run(() => ScanAsync(withComponents, CancellationToken.None));
        _ = started.ContinueWith(finished =>
        {
            lock (_scanGate)
            {
                if (ReferenceEquals(withComponents ? _componentsScan : _appsScan, finished))
                    clear(null);
            }
        }, TaskScheduler.Default);
        return started;
    }

    public void Invalidate()
    {
        // A scan already running may have read the old state, so the next caller must not share it.
        lock (_scanGate)
        {
            _appsScan = null;
            _componentsScan = null;
        }
        winGetDetectionService.InvalidateInstalledPackages();
    }

    private async Task<ScanResult> ScanAsync(bool withComponents, CancellationToken cancellationToken)
    {
        // Taken before reading, so a scan already running when the update finished never judges it.
        var updated = Interlocked.Exchange(ref _lastUpdated, null);
        var registryRead = Task.Run(() => UninstallRegistryReader.ReadBoth(registry), cancellationToken);
        var storePackages = appxPackageSource.GetStorePackagesAsync(cancellationToken);
        var winGet = ReadWinGetAsync(cancellationToken);
        var chocoInstalled = chocolateyService.GetInstalledPackageIdsAsync(cancellationToken);
        var chocoOutdated = chocolateyService.GetOutdatedPackagesAsync(cancellationToken);
        var windowsCatalog = WindowsAppDefinitions.GetWindowsApps().Items;
        var components = withComponents ? ReadComponentsAsync() : Task.FromResult<InstalledWindowsComponents?>(null);
        var catalog = ExternalAppDefinitions.GetExternalApps().Items.Concat(windowsCatalog).ToList();
        var storeUpdates = CheckStoreCatalogAsync(storePackages, catalog, cancellationToken);
        await Task.WhenAll(registryRead, storePackages, winGet, chocoInstalled, chocoOutdated, components, storeUpdates).ConfigureAwait(false);

        var (allEntries, programs) = registryRead.Result;
        var winGetPackages = winGet.Result?.Packages;
        var apps = InstalledAppsBuilder.Build(programs, storePackages.Result, winGetPackages ?? [], chocoInstalled.Result,
            chocoOutdated.Result ?? new Dictionary<string, string>(), catalog, storeUpdates.Result, line => logService.LogDebug($"Update sources: {line}"));
        logService.LogInformation($"Installed apps: {apps.Count} ({programs.Count} programs, {storePackages.Result.Count} Store packages, "
            + $"{winGetPackages?.Count.ToString() ?? "no"} winget matches, {apps.Count(a => a.HasUpdate)} updates)");
        WarnIfStillOffered(updated, apps);

        // The PackageManager list is empty when it failed; WMI and Get-AppxPackage can still name the packages.
        var appxNames = storePackages.Result.Count > 0
            ? storePackages.Result.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : await appxPackageSource.GetInstalledPackageNamesAsync(cancellationToken).ConfigureAwait(false);
        var sources = new ScanSources(allEntries, appxNames, winGet.Result?.Ids, chocoInstalled.Result, components.Result);

        // winget's list comes over COM only. A PC without winget has nothing to ask there, so Chocolatey alone answers.
        var chocolateyAlone = !winGetBootstrapper.IsSystemWinGetAvailable
            && await chocolateyService.IsChocolateyInstalledAsync(cancellationToken).ConfigureAwait(false);
        var updatesKnown = chocoOutdated.Result is not null && (winGetPackages is not null || chocolateyAlone);
        return new ScanResult(sources, apps, updatesKnown);
    }

    // The ids come from the same walk as the packages; only a PC where COM cannot be used falls back to the CLI's ids.
    private async Task<(IReadOnlyList<WinGetInstalledPackage>? Packages, HashSet<string> Ids)?> ReadWinGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await winGetBootstrapper.EnsureWinGetReadyAsync(cancellationToken).ConfigureAwait(false))
            {
                logService.LogWarning("WinGet unavailable - skipping WinGet detection");
                return null;
            }

            var packages = await winGetDetectionService.GetInstalledPackagesAsync(cancellationToken).ConfigureAwait(false);
            var ids = packages is null
                ? await winGetDetectionService.GetInstalledPackageIdsAsync(cancellationToken).ConfigureAwait(false)
                : packages.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return (packages, ids);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logService.LogWarning($"WinGet detection failed: {ex.Message}");
            return null;
        }
    }

    private static string[] FeatureNames() =>
        OptionalFeatureDefinitions.GetWindowsOptionalFeatures().Items.Select(d => d.OptionalFeatureName).OfType<string>().Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    // A DISM that hangs or fails leaves every capability and feature reading as not installed.
    private async Task<InstalledWindowsComponents?> ReadComponentsAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            return await dismComponentReader.ReadInstalledAsync(true, FeatureNames(), cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            logService.LogWarning("DISM did not answer within 30s, marking all capabilities and features as not installed.");
        }
        catch (Exception ex)
        {
            logService.LogError($"Error checking capabilities and features: {ex.GetType().Name}: {ex.Message}", ex);
        }

        return new InstalledWindowsComponents(null, null);
    }

    // Starts once the AppX list is in, beside winget's slower walk.
    private async Task<IReadOnlyDictionary<string, StoreCatalogUpdate>> CheckStoreCatalogAsync(
        Task<IReadOnlyList<StorePackage>> storePackages, IReadOnlyList<ItemDefinition> catalog, CancellationToken cancellationToken)
    {
        var packages = await storePackages.ConfigureAwait(false);
        return await storeCatalogService.GetUpdatesAsync(InstalledAppsBuilder.StoreCatalogApps(packages, catalog), cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult<bool>> UpdateAsync(InstalledApp app, CancellationToken cancellationToken = default)
    {
        try
        {
            var (success, error, deferred) = app.UpdateVia switch
            {
                PackageSources.Chocolatey => (await chocolateyService.UpgradePackageAsync(app.ChocoId!, app.Name, cancellationToken).ConfigureAwait(false), null, false),
                PackageSources.WinGet => FromInstall(await winGetPackageInstaller.UpgradePackageAsync(app.WinGetId!, WinGetSources.WinGet, app.Name, cancellationToken).ConfigureAwait(false)),
                PackageSources.MsStore => FromInstall(await winGetPackageInstaller.UpdateFromStoreAsync(app.MsStoreId!, app.Name, cancellationToken).ConfigureAwait(false)),
                _ => (false, $"{app.Name} has no update", false),
            };
            if (!success)
                return OperationResult<bool>.Failed(error ?? $"Updating {app.Name} failed");
            if (!deferred)
                _lastUpdated = app;
            return OperationResult<bool>.Succeeded(true);
        }
        finally
        {
            // A cancelled update may have changed the app before it stopped.
            Invalidate();
        }
    }

    // A package manager can report success while the installed version stays where it was, so the scan after an update
    // says so instead of leaving only a badge that never clears.
    private void WarnIfStillOffered(InstalledApp? updated, IReadOnlyList<InstalledApp> apps)
    {
        if (updated is null
            || apps.FirstOrDefault(a => a.Key == updated.Key) is not { HasUpdate: true } still)
            return;

        logService.LogWarning($"{still.Name} still has an update after {updated.UpdateVia} reported updating it to {updated.AvailableVersion}: "
            + $"installed {still.Version}, offered {still.AvailableVersion} via {still.UpdateVia}");
    }

    private static (bool, string?, bool) FromInstall(PackageInstallResult result) => (result.Success, result.ErrorMessage, result.RegistrationDeferred);

    public Task<InstalledApp[]> FindOpenAppsAsync(IReadOnlyList<InstalledApp> apps, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            if (!apps.Any(IsClosable))
                return [];

            var running = PackageProcesses();
            try
            {
                return apps.Where(app => IsClosable(app) && running.Any(p => p.Package.Equals(app.PackageName, StringComparison.OrdinalIgnoreCase))).ToArray();
            }
            finally
            {
                foreach (var (_, process) in running)
                    process.Dispose();
            }
        }, cancellationToken);

    public async Task CloseAsync(IReadOnlyList<InstalledApp> apps, CancellationToken cancellationToken = default)
    {
        var running = PackageProcesses();
        try
        {
            foreach (var app in apps.Where(IsClosable))
            {
                foreach (var (_, process) in running.Where(p => p.Package.Equals(app.PackageName, StringComparison.OrdinalIgnoreCase)))
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(10));
                    try
                    {
                        process.Kill();
                        await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception
                        || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
                    {
                        logService.LogWarning($"Could not close {app.Name} (process {process.Id}): {ex.Message}");
                    }
                }
                logService.LogInformation($"Closed {app.Name} so its update can install");
            }
        }
        finally
        {
            foreach (var (_, process) in running)
                process.Dispose();
        }
    }

    // Every process started from a package's install folder, C:\Program Files\WindowsApps\<Name>_<Version>_..., with the
    // package name. Processes Winhance may not open (system, protected) throw on MainModule and are skipped.
    private static List<(string Package, System.Diagnostics.Process Process)> PackageProcesses()
    {
        const string Root = @"\WindowsApps\";
        var found = new List<(string, System.Diagnostics.Process)>();
        foreach (var process in System.Diagnostics.Process.GetProcesses())
        {
            string? path = null;
            try
            {
                path = process.MainModule?.FileName;
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {
            }

            var at = path?.IndexOf(Root, StringComparison.OrdinalIgnoreCase) ?? -1;
            var underscore = at < 0 ? -1 : path!.IndexOf('_', at + Root.Length);
            if (underscore < 0)
            {
                process.Dispose();
                continue;
            }
            found.Add((path![(at + Root.Length)..underscore], process));
        }
        return found;
    }

    // App Installer's open process is winget's own COM server, which the update releases itself.
    private static bool IsClosable(InstalledApp app) =>
        app.Kind == InstalledAppKind.StoreApp
        && !string.IsNullOrEmpty(app.PackageName)
        && !app.PackageName.Equals("Microsoft.DesktopAppInstaller", StringComparison.OrdinalIgnoreCase);
}
