using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Management.Deployment;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet;

/// <summary>
/// Handles WinGet package detection — installed package enumeration and installer type lookup.
/// Uses COM API with CLI fallback.
/// </summary>
public class WinGetDetectionService : IWinGetDetectionService
{
    private readonly WinGetComSession _comSession;
    private readonly ILogService _logService;
    private readonly IInteractiveUserService _interactiveUserService;
    private readonly IFileSystemService _fileSystemService;

    private const int ComOperationTimeoutSeconds = 15;

    public WinGetDetectionService(
        WinGetComSession comSession,
        ILogService logService,
        IInteractiveUserService interactiveUserService,
        IFileSystemService fileSystemService)
    {
        _comSession = comSession;
        _logService = logService;
        _interactiveUserService = interactiveUserService;
        _fileSystemService = fileSystemService;
    }

    public async Task<HashSet<string>> GetInstalledPackageIdsAsync(CancellationToken cancellationToken = default)
    {
        var installedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // Only try COM if system winget is available (COM requires DesktopAppInstaller MSIX)
            if (_comSession.EnsureComInitialized() && _comSession.PackageManager != null && _comSession.Factory != null)
            {
                var comResult = await GetInstalledPackageIdsViaCom(cancellationToken).ConfigureAwait(false);
                if (comResult != null)
                    return comResult;
                _logService?.LogInformation("COM detection failed/timed out, falling back to CLI");
            }

            // CLI fallback (uses winget export → JSON)
            _logService?.LogInformation("COM not available, falling back to CLI for installed package detection");
            return await GetInstalledPackageIdsViaCli(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logService?.LogError($"Error in GetInstalledPackageIdsAsync: {ex.Message}");
            return installedPackageIds;
        }
    }

    private async Task<HashSet<string>?> GetInstalledPackageIdsViaCom(CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(ComOperationTimeoutSeconds));

            return await Task.Run(() =>
            {
                var installedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var catalogs = _comSession.PackageManager!.GetPackageCatalogs().ToArray();
                var wingetCatalog = catalogs.FirstOrDefault(c =>
                    c.Info.Name.Equals("winget", StringComparison.OrdinalIgnoreCase));

                if (wingetCatalog == null && catalogs.Length > 0)
                {
                    wingetCatalog = catalogs[0];
                    _logService?.LogInformation($"Using catalog: {wingetCatalog.Info.Name}");
                }

                if (wingetCatalog == null)
                {
                    _logService?.LogWarning("No package catalogs available");
                    return installedPackageIds;
                }

                var compositeOptions = _comSession.Factory!.CreateCreateCompositePackageCatalogOptions();
                compositeOptions.Catalogs.Add(wingetCatalog);
                compositeOptions.CompositeSearchBehavior = CompositeSearchBehavior.LocalCatalogs;

                var compositeCatalogRef = _comSession.PackageManager.CreateCompositePackageCatalog(compositeOptions);
                var connectResult = compositeCatalogRef.Connect();

                if (connectResult.Status != ConnectResultStatus.Ok)
                {
                    _logService?.LogError($"Failed to connect to composite catalog: {connectResult.Status}");
                    return installedPackageIds;
                }

                var findOptions = _comSession.Factory.CreateFindPackagesOptions();
                var filter = _comSession.Factory.CreatePackageMatchFilter();
                filter.Field = PackageMatchField.Id;
                filter.Option = PackageFieldMatchOption.ContainsCaseInsensitive;
                filter.Value = "";
                findOptions.Filters.Add(filter);

                var findResult = connectResult.PackageCatalog.FindPackages(findOptions);
                var matches = findResult.Matches.ToArray();

                foreach (var match in matches)
                {
                    var packageId = match.CatalogPackage?.Id;
                    if (!string.IsNullOrEmpty(packageId))
                    {
                        installedPackageIds.Add(packageId);
                    }
                }

                _logService?.LogInformation($"WinGet COM API: Found {installedPackageIds.Count} installed packages");
                return installedPackageIds;
            }, timeoutCts.Token).ConfigureAwait(false);
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

    private async Task<HashSet<string>> GetInstalledPackageIdsViaCli(CancellationToken cancellationToken)
    {
        var installedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var cacheDir = _fileSystemService.CombinePath(
            _interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Winhance", "Cache");
        _fileSystemService.CreateDirectory(cacheDir);
        var exportPath = _fileSystemService.CombinePath(cacheDir, "winget-packages.json");

        const int maxRetries = 3;
        const int retryDelayMs = 2000;
        const int timeoutMs = 10_000;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Clean up any previous export file
                if (_fileSystemService.FileExists(exportPath))
                    _fileSystemService.DeleteFile(exportPath);

                var arguments = $"export -o \"{exportPath}\" --accept-source-agreements --nowarn --disable-interactivity";
                _logService?.LogInformation($"[winget-bundled] Running: winget {arguments} (attempt {attempt}/{maxRetries})");

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

        // Clean up export file
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
            // Try COM first
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

            // CLI fallback: parse "winget show" output for Installer Type
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

    private async Task<CatalogPackage?> FindPackageAsync(string packageId, CancellationToken cancellationToken)
    {
        if (!_comSession.EnsureComInitialized() || _comSession.PackageManager == null || _comSession.Factory == null)
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
