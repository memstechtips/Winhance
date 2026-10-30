using System.Text.Json;
using Microsoft.Management.Deployment;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Helpers;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet;

internal class WinGetDetectionService : IWinGetDetectionService
{
    private readonly WinGetComSession _comSession;
    private readonly ILogService _logService;
    private readonly IInteractiveUserService _interactiveUserService;
    private readonly IFileSystemService _fileSystemService;
    private readonly TimeProvider _timeProvider;

    private const int ComOperationTimeoutSeconds = 15;

    // Two composite catalogs connecting at once fail with CatalogError (seen when startup detection and the Installed
    // Apps list ran together), so enumerations take turns. The worker releases it, so an abandoned hang still frees it.
    private readonly SemaphoreSlim _enumerationGate = new(1, 1);
    private const uint SearchResultLimit = 30;

    // A status check and the installed-apps scan behind it ask for the same catalog seconds apart, so the second one
    // reuses the first one's walk. Kept short because winget also sees installs made outside Winhance.
    private static readonly TimeSpan InstalledWalkLifetime = TimeSpan.FromSeconds(60);

    private readonly object _installedWalkLock = new();
    private IReadOnlyList<WinGetInstalledPackage>? _installedWalk;
    private long _installedWalkTimestamp;
    private int _installedWalkGeneration;

    public WinGetDetectionService(
        WinGetComSession comSession,
        ILogService logService,
        IInteractiveUserService interactiveUserService,
        IFileSystemService fileSystemService,
        TimeProvider timeProvider)
    {
        _comSession = comSession;
        _logService = logService;
        _interactiveUserService = interactiveUserService;
        _fileSystemService = fileSystemService;
        _timeProvider = timeProvider;
    }

    public async Task<HashSet<string>> GetInstalledPackageIdsAsync(CancellationToken cancellationToken = default)
    {
        var installedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // Only try COM if system winget is available (COM requires DesktopAppInstaller MSIX)
            if (_comSession.IsReady())
            {
                var packages = await GetOrWalkInstalledAsync(WalkInstalledViaComAsync, cancellationToken).ConfigureAwait(false);
                if (packages != null)
                    return new HashSet<string>(packages.Select(package => package.Id), StringComparer.OrdinalIgnoreCase);
                _logService?.LogInformation("COM detection failed/timed out, falling back to CLI");
            }

            _logService?.LogInformation("COM not available, falling back to CLI for installed package detection");
            return await GetInstalledPackageIdsViaCli(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logService?.LogError($"Error in GetInstalledPackageIdsAsync: {ex.Message}");
            return installedPackageIds;
        }
    }

    // Null when COM is unavailable or broken: the CLI fallback knows ids only, so it cannot fill these records.
    public async Task<IReadOnlyList<WinGetInstalledPackage>?> GetInstalledPackagesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_comSession.IsReady())
                return null;

            return await GetOrWalkInstalledAsync(WalkInstalledViaComAsync, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logService?.LogError($"Error in GetInstalledPackagesAsync: {ex.Message}");
            return null;
        }
    }

    public void InvalidateInstalledPackages()
    {
        lock (_installedWalkLock)
        {
            _installedWalk = null;
            _installedWalkGeneration++;
        }
    }

    // A walk that was already running when the list was invalidated read the PC before the change, so it is not kept.
    internal async Task<IReadOnlyList<WinGetInstalledPackage>?> GetOrWalkInstalledAsync(
        Func<CancellationToken, Task<List<WinGetInstalledPackage>?>> walk, CancellationToken cancellationToken)
    {
        int generation;
        lock (_installedWalkLock)
        {
            if (_installedWalk != null && _timeProvider.GetElapsedTime(_installedWalkTimestamp) < InstalledWalkLifetime)
                return _installedWalk;
            generation = _installedWalkGeneration;
        }

        var packages = await walk(cancellationToken).ConfigureAwait(false);
        if (packages == null)
            return null;

        lock (_installedWalkLock)
        {
            if (generation == _installedWalkGeneration)
            {
                _installedWalk = packages;
                _installedWalkTimestamp = _timeProvider.GetTimestamp();
            }
        }

        return packages;
    }

    // DefaultInstallVersion is the remote source's version, so its catalog names where the app can come from again.
    private static WinGetInstalledPackage Describe(CatalogPackage package)
    {
        var installed = package.InstalledVersion;
        var remote = package.DefaultInstallVersion;
        return new WinGetInstalledPackage(
            package.Id,
            package.Name,
            installed?.Version,
            remote?.PackageCatalog?.Info?.Name,
            remote is not null && package.IsUpdateAvailable ? remote.Version : null)
        {
            ProductCodes = installed?.ProductCodes?.ToList() ?? [],
            PackageFamilyNames = installed?.PackageFamilyNames?.ToList() ?? [],
        };
    }

    private async Task<List<WinGetInstalledPackage>?> WalkInstalledViaComAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await _enumerationGate.WaitAsync(TimeSpan.FromSeconds(ComOperationTimeoutSeconds), cancellationToken).ConfigureAwait(false))
            {
                _logService?.LogWarning("Another WinGet enumeration is still running, falling back");
                return null;
            }

            var workTask = Task.Run(() =>
            {
                try
                {
                    return EnumerateInstalled();
                }
                finally
                {
                    _enumerationGate.Release();
                }
            }, CancellationToken.None);

            return await WaitForComAsync(workTask, null, "WinGet installed package enumeration", cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logService?.LogWarning("COM package enumeration timed out");
            return null;
        }
        catch (Exception ex)
        {
            _logService?.LogError($"Error getting installed packages via COM API: {ex.Message}");
            return null;
        }
    }

    // Native COM calls (Connect, FindPackages) can block forever and cannot be interrupted, so the caller stops
    // waiting after the timeout and leaves the thread behind.
    private async Task<T> WaitForComAsync<T>(Task<T> work, T onTimeout, string operation, CancellationToken cancellationToken)
    {
        var timeout = Task.Delay(TimeSpan.FromSeconds(ComOperationTimeoutSeconds), cancellationToken);
        if (await Task.WhenAny(work, timeout).ConfigureAwait(false) == timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logService?.LogWarning($"{operation} timed out after {ComOperationTimeoutSeconds}s, abandoning thread");
            return onTimeout;
        }

        return await work.ConfigureAwait(false);
    }

    private PackageCatalog? ConnectToWinGetSource(PackageManager packageManager, string operation)
    {
        var connectResult = packageManager
            .GetPredefinedPackageCatalog(PredefinedPackageCatalog.OpenWindowsCatalog)
            .Connect();
        if (connectResult.Status == ConnectResultStatus.Ok)
            return connectResult.PackageCatalog;

        _logService?.LogWarning($"{operation} could not connect to the winget source: {connectResult.Status}");
        return null;
    }

    private List<WinGetInstalledPackage>? EnumerateInstalled()
    {
        var catalogs = _comSession.PackageManager!.GetPackageCatalogs().ToArray();
        var compositeOptions = _comSession.Factory!.CreateCreateCompositePackageCatalogOptions();
        foreach (var catalog in catalogs)
        {
            compositeOptions.Catalogs.Add(catalog);
            _logService?.LogInformation($"Added catalog: {catalog.Info.Name}");
        }

        if (compositeOptions.Catalogs.Count == 0)
        {
            _logService?.LogWarning("No package catalogs available");
            return [];
        }
        compositeOptions.CompositeSearchBehavior = CompositeSearchBehavior.LocalCatalogs;

        var compositeCatalogRef = _comSession.PackageManager.CreateCompositePackageCatalog(compositeOptions);
        var connectResult = compositeCatalogRef.Connect();

        if (connectResult.Status != ConnectResultStatus.Ok)
        {
            // CLI fallback recovers when the COM source store is broken.
            _logService?.LogWarning($"Failed to connect to composite catalog: {connectResult.Status} — falling back to CLI");
            return null;
        }

        var findOptions = _comSession.Factory.CreateFindPackagesOptions();
        var filter = _comSession.Factory.CreatePackageMatchFilter();
        filter.Field = PackageMatchField.Id;
        filter.Option = PackageFieldMatchOption.ContainsCaseInsensitive;
        filter.Value = "";
        findOptions.Filters.Add(filter);

        _logService?.LogInformation("WinGet COM: calling FindPackages...");
        var findResult = connectResult.PackageCatalog.FindPackages(findOptions);
        var packages = findResult.Matches.ToArray()
            .Select(match => match.CatalogPackage)
            .Where(package => package is not null && !string.IsNullOrEmpty(package.Id))
            .Select(Describe)
            .ToList();

        _logService?.LogInformation($"WinGet COM API: Found {packages.Count} installed packages");

        // If COM returns 0 packages despite catalogs being available,
        // the database may be corrupted — signal caller to fall through to CLI
        if (packages.Count == 0)
        {
            _logService?.LogWarning("COM returned 0 installed packages — possible database corruption, falling back to CLI");
            return null;
        }

        return packages;
    }

    private async Task<HashSet<string>> GetInstalledPackageIdsViaCli(CancellationToken cancellationToken)
    {
        var installedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var cacheDir = _fileSystemService.CombinePath(
            AppDataPaths.UserRoot(_interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData)),
            "Cache");
        _fileSystemService.CreateDirectory(cacheDir);
        var exportPath = _fileSystemService.CombinePath(cacheDir, "winget-packages.json");

        const int maxRetries = 3;
        const int retryDelayMs = 2000;
        const int timeoutMs = 10_000;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (_fileSystemService.FileExists(exportPath))
                    _fileSystemService.DeleteFile(exportPath);

                var arguments = $"export -o \"{exportPath}\" --accept-source-agreements --nowarn --disable-interactivity";
                _logService?.LogInformation($"Running: winget {arguments} (attempt {attempt}/{maxRetries})");

                var result = await WinGetCliRunner.RunAsync(
                    arguments,
                    cancellationToken: cancellationToken,
                    timeoutMs: timeoutMs,
                    exePathOverride: WinGetCliRunner.GetBundledWinGetExePath(),
                    interactiveUserService: _interactiveUserService).ConfigureAwait(false);

                if (result.ExitCode != 0)
                {
                    _logService?.LogWarning($"winget export failed with exit code 0x{result.ExitCode:X8} (attempt {attempt}/{maxRetries})");

                    // FailedToOpenAllSources = sources not initialized (no internet / no AppInstaller)
                    // No point retrying — the sources won't appear on their own
                    if (result.ExitCode == WinGetExitCodes.FailedToOpenAllSources)
                    {
                        _logService?.LogWarning("Sources not available — skipping retries");
                        return installedPackageIds;
                    }

                    if (attempt < maxRetries)
                    {
                        await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    return installedPackageIds;
                }

                if (!_fileSystemService.FileExists(exportPath))
                {
                    _logService?.LogWarning($"winget export succeeded but file not found (attempt {attempt}/{maxRetries})");
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    return installedPackageIds;
                }

                // Parse JSON: root.Sources[].Packages[].PackageIdentifier
                var jsonBytes = await _fileSystemService.ReadAllBytesAsync(exportPath, cancellationToken).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(jsonBytes);

                if (doc.RootElement.TryGetProperty("Sources", out var sourcesElement))
                {
                    foreach (var source in sourcesElement.EnumerateArray())
                    {
                        if (source.TryGetProperty("Packages", out var packagesElement))
                        {
                            foreach (var package in packagesElement.EnumerateArray())
                            {
                                if (package.TryGetProperty("PackageIdentifier", out var idElement))
                                {
                                    var id = idElement.GetString();
                                    if (!string.IsNullOrEmpty(id))
                                        installedPackageIds.Add(id);
                                }
                            }
                        }
                    }
                }

                _logService?.LogInformation($"WinGet CLI (export): Found {installedPackageIds.Count} installed packages");
                break;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error getting installed packages via winget export (attempt {attempt}/{maxRetries}): {ex.Message}");
                if (attempt < maxRetries)
                {
                    await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        try
        {
            if (_fileSystemService.FileExists(exportPath))
                _fileSystemService.DeleteFile(exportPath);
        }
        catch (Exception ex) { _logService?.LogDebug($"Best-effort export file cleanup failed: {ex.Message}"); }

        return installedPackageIds;
    }

    public async Task<string?> GetInstallerTypeAsync(string packageId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            return null;

        try
        {
            if (_comSession.EnsureComInitialized())
            {
                var package = await FindPackageAsync(packageId, cancellationToken).ConfigureAwait(false);
                if (package?.DefaultInstallVersion != null)
                {
                    var catalogInfo = package.DefaultInstallVersion.PackageCatalog?.Info;
                    if (catalogInfo != null)
                    {
                        _logService?.LogInformation($"Package {packageId} from catalog: {catalogInfo.Name}");
                    }
                }
            }

            var result = await WinGetCliRunner.RunAsync(
                $"show --id {packageId} --accept-source-agreements --disable-interactivity",
                cancellationToken: cancellationToken,
                timeoutMs: 60_000,
                interactiveUserService: _interactiveUserService).ConfigureAwait(false);

            if (result.ExitCode == 0)
            {
                foreach (var rawLine in result.StandardOutput.Split('\n'))
                {
                    var line = rawLine.Trim();
                    if (line.StartsWith("Installer Type:", StringComparison.OrdinalIgnoreCase))
                    {
                        var installerType = line.Substring("Installer Type:".Length).Trim();
                        if (!string.IsNullOrEmpty(installerType))
                        {
                            _logService?.LogInformation($"Package {packageId} installer type: {installerType}");
                            return installerType;
                        }
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logService?.LogWarning($"Could not determine installer type for {packageId}: {ex.Message}");
            return null;
        }
    }

    public async Task<IReadOnlyList<WinGetPackageMatch>> SearchPackagesAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        try
        {
            var workTask = Task.Run(() => SearchWinGetCatalog(query.Trim()), cancellationToken);
            return await WaitForComAsync(workTask, [], $"WinGet search for '{query}'", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logService?.LogError($"WinGet search for '{query}' failed: {ex.Message}");
            return [];
        }
    }

    public async Task<IReadOnlyCollection<string>> FindMissingPackageIdsAsync(IReadOnlyCollection<string> packageIds, CancellationToken cancellationToken = default)
    {
        if (packageIds.Count == 0)
            return [];

        try
        {
            // The lookups read winget's local copy of the source index, so even a whole catalog fits the same timeout.
            var workTask = Task.Run(() => FindMissingInWinGetCatalog(packageIds), cancellationToken);
            return await WaitForComAsync(workTask, [], "WinGet id check", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logService?.LogError($"WinGet id check failed: {ex.Message}");
            return [];
        }
    }

    private List<string> FindMissingInWinGetCatalog(IReadOnlyCollection<string> packageIds)
    {
        if (!_comSession.IsReady() || ConnectToWinGetSource(_comSession.PackageManager, "WinGet id check") is not { } catalog)
            return [];

        var missing = new List<string>();
        foreach (var packageId in packageIds)
        {
            var findOptions = _comSession.Factory.CreateFindPackagesOptions();
            findOptions.ResultLimit = 1;
            var filter = _comSession.Factory.CreatePackageMatchFilter();
            filter.Field = PackageMatchField.Id;
            filter.Option = PackageFieldMatchOption.EqualsCaseInsensitive;
            filter.Value = packageId;
            findOptions.Filters.Add(filter);

            var findResult = catalog.FindPackages(findOptions);
            if (findResult.Status == FindPackagesResultStatus.Ok && findResult.Matches.Count == 0)
                missing.Add(packageId);
        }

        return missing;
    }

    // Only the winget source: a Microsoft Store match carries a Store id, which the install path takes through a
    // different source than the winget id it gets from a search result.
    private List<WinGetPackageMatch> SearchWinGetCatalog(string query)
    {
        if (!_comSession.IsReady() || ConnectToWinGetSource(_comSession.PackageManager, "WinGet search") is not { } catalog)
            return [];

        var findOptions = _comSession.Factory.CreateFindPackagesOptions();
        findOptions.ResultLimit = SearchResultLimit;
        var selector = _comSession.Factory.CreatePackageMatchFilter();
        selector.Field = PackageMatchField.CatalogDefault;
        selector.Option = PackageFieldMatchOption.ContainsCaseInsensitive;
        selector.Value = query;
        findOptions.Selectors.Add(selector);

        var findResult = catalog.FindPackages(findOptions);
        if (findResult.Status != FindPackagesResultStatus.Ok)
        {
            _logService?.LogWarning($"WinGet search for '{query}' returned {findResult.Status}");
            return [];
        }

        var matches = new List<WinGetPackageMatch>();
        foreach (var match in findResult.Matches.ToArray())
        {
            var package = match.CatalogPackage;
            if (package == null || string.IsNullOrEmpty(package.Id))
                continue;

            matches.Add(new WinGetPackageMatch(package.Id, string.IsNullOrWhiteSpace(package.Name) ? package.Id : package.Name));
        }

        return matches;
    }

    private async Task<CatalogPackage?> FindPackageAsync(string packageId, CancellationToken cancellationToken)
    {
        if (!_comSession.IsReady())
            return null;

        return await Task.Run(() =>
        {
            try
            {
                var catalogs = _comSession.PackageManager.GetPackageCatalogs().ToArray();

                foreach (var catalogRef in catalogs)
                {
                    var connectResult = catalogRef.Connect();
                    if (connectResult.Status != ConnectResultStatus.Ok)
                        continue;

                    var findOptions = _comSession.Factory.CreateFindPackagesOptions();
                    var filter = _comSession.Factory.CreatePackageMatchFilter();
                    filter.Field = PackageMatchField.Id;
                    filter.Option = PackageFieldMatchOption.EqualsCaseInsensitive;
                    filter.Value = packageId;
                    findOptions.Filters.Add(filter);

                    var findResult = connectResult.PackageCatalog.FindPackages(findOptions);

                    var match = findResult.Matches.ToArray().FirstOrDefault();
                    if (match != null)
                    {
                        return match.CatalogPackage;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error finding package {packageId}: {ex.Message}");
                return null;
            }
        }, cancellationToken).ConfigureAwait(false);
    }
}
