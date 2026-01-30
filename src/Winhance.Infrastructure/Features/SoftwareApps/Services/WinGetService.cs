using System;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Management.Deployment;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;
using WindowsPackageManager.Interop;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services
{
    public class WinGetService : IWinGetService
    {
        private readonly ITaskProgressService _taskProgressService;
        private readonly ILogService _logService;
        private readonly ILocalizationService _localization;

        private WindowsPackageManagerFactory? _winGetFactory;
        private PackageManager? _packageManager;
        private readonly object _factoryLock = new();
        private bool _isInitialized;

        public WinGetService(
            ITaskProgressService taskProgressService,
            ILogService logService,
            ILocalizationService localization)
        {
            _taskProgressService = taskProgressService;
            _logService = logService;
            _localization = localization;
        }

        private bool IsRunningAsAdministrator()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private bool EnsureInitialized()
        {
            if (_isInitialized && _packageManager != null)
                return true;

            lock (_factoryLock)
            {
                if (_isInitialized && _packageManager != null)
                    return true;

                try
                {
                    bool isAdmin = IsRunningAsAdministrator();
                    _logService?.LogInformation($"Initializing WinGet COM API (Admin: {isAdmin})");

                    if (isAdmin)
                    {
                        // For admin scenarios, try ElevatedFactory (uses winrtact.dll)
                        // This is the recommended approach for elevated processes
                        // The WinGet COM server may need a moment to initialize, so we retry
                        const int maxRetries = 3;
                        Exception? lastException = null;

                        for (int attempt = 1; attempt <= maxRetries; attempt++)
                        {
                            try
                            {
                                _logService?.LogInformation($"Trying ElevatedFactory for admin context (attempt {attempt}/{maxRetries})...");
                                _winGetFactory = new WindowsPackageManagerElevatedFactory();
                                _packageManager = _winGetFactory.CreatePackageManager();
                                _isInitialized = true;
                                _logService?.LogInformation("WinGet COM API initialized successfully with ElevatedFactory");
                                return true;
                            }
                            catch (Exception ex)
                            {
                                lastException = ex;
                                _logService?.LogWarning($"ElevatedFactory attempt {attempt} failed: {ex.Message}");

                                if (attempt < maxRetries)
                                {
                                    // Wait before retrying - WinGet COM server may need time to start
                                    Thread.Sleep(1000);
                                }
                            }
                        }

                        // All ElevatedFactory attempts failed, try StandardFactory as last resort
                        _logService?.LogWarning($"All ElevatedFactory attempts failed, trying StandardFactory with lower trust registration...");
                        try
                        {
                            _winGetFactory = new WindowsPackageManagerStandardFactory(
                                ClsidContext.Prod,
                                allowLowerTrustRegistration: true);
                            _packageManager = _winGetFactory.CreatePackageManager();
                            _isInitialized = true;
                            _logService?.LogInformation("WinGet COM API initialized successfully with StandardFactory (lower trust)");
                            return true;
                        }
                        catch (Exception standardEx)
                        {
                            _logService?.LogError($"StandardFactory also failed: {standardEx.Message}");
                            throw new AggregateException("Both ElevatedFactory and StandardFactory failed",
                                lastException ?? new Exception("Unknown error"), standardEx);
                        }
                    }
                    else
                    {
                        // For non-admin, use StandardFactory without lower trust registration
                        _winGetFactory = new WindowsPackageManagerStandardFactory();
                        _packageManager = _winGetFactory.CreatePackageManager();
                        _isInitialized = true;
                        _logService?.LogInformation("WinGet COM API initialized successfully with StandardFactory");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logService?.LogError($"Failed to initialize WinGet COM API: {ex.Message}");
                    _isInitialized = false;
                    _packageManager = null;
                    _winGetFactory = null;
                    return false;
                }
            }
        }

        private void ResetFactory()
        {
            lock (_factoryLock)
            {
                _isInitialized = false;
                _packageManager = null;
                _winGetFactory = null;
            }
        }

        public async Task<bool> IsWinGetInstalledAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await Task.Run(() => EnsureInitialized(), cancellationToken);
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error checking if WinGet is installed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> InstallPackageAsync(string packageId, string? displayName = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                throw new ArgumentException("Package ID cannot be null or empty", nameof(packageId));

            displayName ??= packageId;

            _taskProgressService?.UpdateProgress(10, _localization.GetString("Progress_WinGet_CheckingPrerequisites", displayName));

            if (!await IsWinGetInstalledAsync(cancellationToken))
            {
                _taskProgressService?.UpdateProgress(20, _localization.GetString("Progress_WinGet_InstallingManager"));
                if (!await InstallWinGetAsync(cancellationToken))
                {
                    _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_FailedInstallManager", displayName));
                    return false;
                }

                // Re-initialize after installing WinGet
                ResetFactory();
                if (!EnsureInitialized())
                {
                    _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_FailedInstallManager", displayName));
                    return false;
                }
            }

            try
            {
                _taskProgressService?.UpdateProgress(30, _localization.GetString("Progress_WinGet_StartingInstallation", displayName));

                // Find the package first
                var package = await FindPackageAsync(packageId, cancellationToken);
                if (package == null)
                {
                    _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_PackageNotFound", packageId));
                    return false;
                }

                _taskProgressService?.UpdateProgress(40, _localization.GetString("Progress_WinGet_FoundPackage", package.Name));

                // Create install options
                var installOptions = _winGetFactory!.CreateInstallOptions();
                installOptions.PackageInstallMode = PackageInstallMode.Silent;
                installOptions.PackageInstallScope = PackageInstallScope.Any;

                // Install the package
                _taskProgressService?.UpdateProgress(50, _localization.GetString("Progress_Installing", displayName));

                var installResult = await Task.Run(async () =>
                {
                    var operation = _packageManager!.InstallPackageAsync(package, installOptions);

                    // Set up progress reporting
                    operation.Progress = (asyncInfo, progressInfo) =>
                    {
                        var progressPercent = 50 + (int)(progressInfo.DownloadProgress * 25) + (int)(progressInfo.InstallationProgress * 25);
                        var (status, progressDisplay) = progressInfo.State switch
                        {
                            PackageInstallProgressState.Queued => (_localization.GetString("Progress_Processing", displayName), ""),
                            PackageInstallProgressState.Downloading => (_localization.GetString("Progress_Downloading", displayName), $"{progressInfo.DownloadProgress:P0}"),
                            PackageInstallProgressState.Installing => (_localization.GetString("Progress_Installing", displayName), $"{progressInfo.InstallationProgress:P0}"),
                            PackageInstallProgressState.PostInstall => (_localization.GetString("Progress_Finalizing"), ""),
                            PackageInstallProgressState.Finished => (_localization.GetString("Progress_InstalledSuccess", displayName), "100%"),
                            _ => (_localization.GetString("Progress_Processing", displayName), "")
                        };

                        _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
                        {
                            Progress = progressPercent,
                            StatusText = status,
                            TerminalOutput = progressDisplay
                        });
                    };

                    return await operation.AsTask(cancellationToken);
                }, cancellationToken);

                if (installResult.Status == InstallResultStatus.Ok)
                {
                    _taskProgressService?.UpdateProgress(100, _localization.GetString("Progress_WinGet_InstalledSuccess", displayName));
                    return true;
                }

                var errorMessage = GetInstallErrorMessage(packageId, installResult);
                _logService?.LogError($"Installation failed for {packageId}: {errorMessage}");
                _taskProgressService?.UpdateProgress(0, errorMessage);
                return false;
            }
            catch (OperationCanceledException)
            {
                _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_InstallationCancelled", displayName));
                throw;
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error installing {packageId}: {ex.Message}");

                var errorMessage = IsNetworkRelatedError(ex.Message)
                    ? _localization.GetString("Progress_WinGet_NetworkError", displayName)
                    : _localization.GetString("Progress_WinGet_InstallationError", displayName, ex.Message);

                _taskProgressService?.UpdateProgress(0, errorMessage);
                return false;
            }
        }

        public async Task<bool> UninstallPackageAsync(string packageId, string? displayName = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                throw new ArgumentException("Package ID cannot be null or empty", nameof(packageId));

            displayName ??= packageId;

            _taskProgressService?.UpdateProgress(10, _localization.GetString("Progress_WinGet_CheckingPrerequisitesUninstall", displayName));

            if (!await IsWinGetInstalledAsync(cancellationToken))
            {
                _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_NotInstalled"));
                return false;
            }

            try
            {
                _taskProgressService?.UpdateProgress(30, _localization.GetString("Progress_WinGet_StartingUninstallation", displayName));

                // Find the installed package
                var package = await FindInstalledPackageAsync(packageId, cancellationToken);
                if (package == null)
                {
                    _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_PackageNotInstalled", packageId));
                    return false;
                }

                // Create uninstall options
                var uninstallOptions = _winGetFactory!.CreateUninstallOptions();
                uninstallOptions.PackageUninstallMode = PackageUninstallMode.Silent;
                uninstallOptions.Force = true;

                // Uninstall the package
                _taskProgressService?.UpdateProgress(50, _localization.GetString("Progress_Uninstalling", displayName));

                var uninstallResult = await Task.Run(async () =>
                {
                    var operation = _packageManager!.UninstallPackageAsync(package, uninstallOptions);

                    operation.Progress = (asyncInfo, progressInfo) =>
                    {
                        var progressPercent = 50 + (int)(progressInfo.UninstallationProgress * 50);
                        var (status, progressDisplay) = progressInfo.State switch
                        {
                            PackageUninstallProgressState.Queued => (_localization.GetString("Progress_Processing", displayName), ""),
                            PackageUninstallProgressState.Uninstalling => (_localization.GetString("Progress_Uninstalling", displayName), $"{progressInfo.UninstallationProgress:P0}"),
                            PackageUninstallProgressState.PostUninstall => (_localization.GetString("Progress_Finalizing"), ""),
                            PackageUninstallProgressState.Finished => (_localization.GetString("Progress_WinGet_UninstalledSuccess", displayName), "100%"),
                            _ => (_localization.GetString("Progress_Processing", displayName), "")
                        };

                        _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
                        {
                            Progress = progressPercent,
                            StatusText = status,
                            TerminalOutput = progressDisplay
                        });
                    };

                    return await operation.AsTask(cancellationToken);
                }, cancellationToken);

                if (uninstallResult.Status == UninstallResultStatus.Ok)
                {
                    _taskProgressService?.UpdateProgress(100, _localization.GetString("Progress_WinGet_UninstalledSuccess", displayName));
                    return true;
                }

                var errorMessage = GetUninstallErrorMessage(packageId, uninstallResult);
                _logService?.LogError($"Uninstallation failed for {packageId}: {errorMessage}");
                _taskProgressService?.UpdateProgress(0, errorMessage);
                return false;
            }
            catch (OperationCanceledException)
            {
                _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_UninstallationCancelled", displayName));
                throw;
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error uninstalling {packageId}: {ex.Message}");
                _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_UninstallationError", displayName, ex.Message));
                return false;
            }
        }

        public async Task<bool> InstallWinGetAsync(CancellationToken cancellationToken = default)
        {
            if (await IsWinGetInstalledAsync(cancellationToken))
                return true;

            var progress = new Progress<TaskProgressDetail>(p => _taskProgressService?.UpdateDetailedProgress(p));

            try
            {
                _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_Installing"));

                var installer = new WinGetInstaller(_logService, _localization);
                var result = await installer.InstallAsync(progress, cancellationToken);

                if (!result.Success)
                {
                    _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_InstallFailed"));
                    return false;
                }

                // MSIX/AppX COM registration happens asynchronously after installation
                // We need to retry multiple times with delays to wait for registration to complete
                _taskProgressService?.UpdateProgress(50, _localization.GetString("Progress_WinGet_VerifyingInstallation"));

                const int maxRetries = 10;
                const int retryDelayMs = 3000; // 3 seconds between retries

                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    _logService?.LogInformation($"Verifying WinGet COM API (attempt {attempt}/{maxRetries})...");

                    await Task.Delay(retryDelayMs, cancellationToken);
                    ResetFactory();

                    if (EnsureInitialized())
                    {
                        _logService?.LogInformation($"WinGet COM API ready after {attempt} attempt(s)");
                        _taskProgressService?.UpdateProgress(100, _localization.GetString("Progress_WinGet_InstalledSuccessfully"));
                        return true;
                    }

                    var progressPercent = 50 + (attempt * 5); // Progress from 50% to 100%
                    _taskProgressService?.UpdateProgress(Math.Min(progressPercent, 95),
                        _localization.GetString("Progress_WinGet_WaitingForRegistration"));
                }

                _logService?.LogWarning("WinGet COM API not ready after maximum retries - may need app restart");
                _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_VerificationFailed"));
                return false;
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Failed to install WinGet: {ex.Message}");
                _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_InstallError", ex.Message));
                return false;
            }
        }

        public async Task<bool> IsPackageInstalledAsync(string packageId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packageId) || !await IsWinGetInstalledAsync(cancellationToken))
                return false;

            try
            {
                var package = await FindInstalledPackageAsync(packageId, cancellationToken);
                return package != null;
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error checking if package {packageId} is installed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> EnsureWinGetReadyAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logService?.LogInformation("Checking WinGet availability via COM API...");
                bool isInstalled = await IsWinGetInstalledAsync(cancellationToken);

                if (!isInstalled)
                {
                    _logService?.LogInformation("WinGet COM API is not available - will use WMI/Registry for app detection");
                }

                return isInstalled;
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error checking WinGet availability: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> EnsureWinGetUpToDateAsync(IProgress<TaskProgressDetail>? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                _logService?.LogInformation("Ensuring WinGet is up to date via COM API...");
                progress?.Report(new TaskProgressDetail
                {
                    Progress = 10,
                    StatusText = _localization.GetString("Progress_WinGet_CheckingUpdates")
                });

                if (!EnsureInitialized())
                {
                    _logService?.LogWarning("WinGet COM API not available, attempting installation...");
                    return await InstallWinGetAsync(cancellationToken);
                }

                // Try to update WinGet via the COM API by installing Microsoft.AppInstaller
                progress?.Report(new TaskProgressDetail
                {
                    Progress = 30,
                    StatusText = _localization.GetString("Progress_WinGet_Updating")
                });

                var package = await FindPackageAsync("Microsoft.AppInstaller", cancellationToken);
                if (package == null)
                {
                    _logService?.LogInformation("Microsoft.AppInstaller package not found, WinGet is likely up to date");
                    progress?.Report(new TaskProgressDetail
                    {
                        Progress = 100,
                        StatusText = _localization.GetString("Progress_WinGet_AlreadyUpToDate")
                    });
                    return true;
                }

                // Check if an update is available
                if (package.IsUpdateAvailable)
                {
                    _logService?.LogInformation("WinGet update available, installing...");
                    progress?.Report(new TaskProgressDetail
                    {
                        Progress = 50,
                        StatusText = _localization.GetString("Progress_WinGet_Updating")
                    });

                    var installOptions = _winGetFactory!.CreateInstallOptions();
                    installOptions.PackageInstallMode = PackageInstallMode.Silent;

                    var installResult = await Task.Run(async () =>
                    {
                        var operation = _packageManager!.InstallPackageAsync(package, installOptions);
                        return await operation.AsTask(cancellationToken);
                    }, cancellationToken);

                    if (installResult.Status == InstallResultStatus.Ok)
                    {
                        _logService?.LogInformation("WinGet updated successfully");

                        // Reset factory to use new version
                        ResetFactory();

                        progress?.Report(new TaskProgressDetail
                        {
                            Progress = 100,
                            StatusText = _localization.GetString("Progress_WinGet_UpdateSuccess")
                        });
                        return true;
                    }
                    else
                    {
                        _logService?.LogWarning($"WinGet update failed: {installResult.Status}");
                    }
                }
                else
                {
                    _logService?.LogInformation("WinGet is already up to date");
                }

                progress?.Report(new TaskProgressDetail
                {
                    Progress = 100,
                    StatusText = _localization.GetString("Progress_WinGet_Ready")
                });
                return true;
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error updating WinGet: {ex.Message}");

                try
                {
                    return await InstallWinGetAsync(cancellationToken);
                }
                catch
                {
                    return false;
                }
            }
        }

        public async Task<string?> GetInstallerTypeAsync(string packageId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                return null;

            try
            {
                if (!EnsureInitialized())
                    return null;

                var package = await FindPackageAsync(packageId, cancellationToken);
                if (package?.DefaultInstallVersion == null)
                    return null;

                // Get the installer metadata
                var packageVersionInfo = package.DefaultInstallVersion;

                // Try to get installer type from package metadata
                // The COM API doesn't directly expose installer type, but we can infer from package info
                var catalogInfo = packageVersionInfo.PackageCatalog?.Info;
                if (catalogInfo != null)
                {
                    _logService?.LogInformation($"Package {packageId} from catalog: {catalogInfo.Name}");
                }

                // For now, return null as the COM API doesn't easily expose installer type
                // The caller should handle this by checking file extension or other heuristics
                return null;
            }
            catch (Exception ex)
            {
                _logService?.LogWarning($"Could not determine installer type for {packageId}: {ex.Message}");
                return null;
            }
        }

        public async Task<HashSet<string>> GetInstalledPackageIdsAsync(CancellationToken cancellationToken = default)
        {
            var installedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (!EnsureInitialized() || _packageManager == null || _winGetFactory == null)
                {
                    _logService?.LogWarning("WinGet COM API not available for getting installed packages");
                    return installedPackageIds;
                }

                return await Task.Run(() =>
                {
                    try
                    {
                        // Get the winget catalog (primary community repository)
                        var catalogs = _packageManager.GetPackageCatalogs().ToArray();
                        var wingetCatalog = catalogs.FirstOrDefault(c =>
                            c.Info.Name.Equals("winget", StringComparison.OrdinalIgnoreCase));

                        if (wingetCatalog == null && catalogs.Length > 0)
                        {
                            // Fall back to first available catalog
                            wingetCatalog = catalogs[0];
                            _logService?.LogInformation($"Using catalog: {wingetCatalog.Info.Name}");
                        }

                        if (wingetCatalog == null)
                        {
                            _logService?.LogWarning("No package catalogs available");
                            return installedPackageIds;
                        }

                        // Create composite catalog to search for locally installed packages
                        // that correlate with the remote catalog
                        var compositeOptions = _winGetFactory.CreateCreateCompositePackageCatalogOptions();
                        compositeOptions.Catalogs.Add(wingetCatalog);
                        compositeOptions.CompositeSearchBehavior = CompositeSearchBehavior.LocalCatalogs;

                        var compositeCatalogRef = _packageManager.CreateCompositePackageCatalog(compositeOptions);
                        var connectResult = compositeCatalogRef.Connect();

                        if (connectResult.Status != ConnectResultStatus.Ok)
                        {
                            _logService?.LogError($"Failed to connect to composite catalog: {connectResult.Status}");
                            return installedPackageIds;
                        }

                        var findOptions = _winGetFactory.CreateFindPackagesOptions();
                        var filter = _winGetFactory.CreatePackageMatchFilter();
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
                    }
                    catch (Exception ex)
                    {
                        _logService?.LogError($"Error getting installed packages via COM API: {ex.Message}");
                        return installedPackageIds;
                    }
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error in GetInstalledPackageIdsAsync: {ex.Message}");
                return installedPackageIds;
            }
        }

        private async Task<CatalogPackage?> FindPackageAsync(string packageId, CancellationToken cancellationToken)
        {
            if (!EnsureInitialized() || _packageManager == null || _winGetFactory == null)
                return null;

            return await Task.Run(() =>
            {
                try
                {
                    var catalogs = _packageManager.GetPackageCatalogs().ToArray();

                    foreach (var catalogRef in catalogs)
                    {
                        var connectResult = catalogRef.Connect();
                        if (connectResult.Status != ConnectResultStatus.Ok)
                            continue;

                        var findOptions = _winGetFactory.CreateFindPackagesOptions();
                        var filter = _winGetFactory.CreatePackageMatchFilter();
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
            }, cancellationToken);
        }

        private async Task<CatalogPackage?> FindInstalledPackageAsync(string packageId, CancellationToken cancellationToken)
        {
            if (!EnsureInitialized() || _packageManager == null || _winGetFactory == null)
                return null;

            return await Task.Run(() =>
            {
                try
                {
                    // Get the winget catalog
                    var catalogs = _packageManager.GetPackageCatalogs().ToArray();
                    var wingetCatalog = catalogs.FirstOrDefault(c =>
                        c.Info.Name.Equals("winget", StringComparison.OrdinalIgnoreCase));

                    if (wingetCatalog == null && catalogs.Length > 0)
                        wingetCatalog = catalogs[0];

                    if (wingetCatalog == null)
                    {
                        _logService?.LogWarning("No package catalogs available");
                        return null;
                    }

                    // Create composite catalog to search for locally installed packages
                    var compositeOptions = _winGetFactory.CreateCreateCompositePackageCatalogOptions();
                    compositeOptions.Catalogs.Add(wingetCatalog);
                    compositeOptions.CompositeSearchBehavior = CompositeSearchBehavior.LocalCatalogs;

                    var compositeCatalogRef = _packageManager.CreateCompositePackageCatalog(compositeOptions);
                    var connectResult = compositeCatalogRef.Connect();

                    if (connectResult.Status != ConnectResultStatus.Ok)
                    {
                        _logService?.LogError($"Failed to connect to composite catalog: {connectResult.Status}");
                        return null;
                    }

                    var findOptions = _winGetFactory.CreateFindPackagesOptions();
                    var filter = _winGetFactory.CreatePackageMatchFilter();
                    filter.Field = PackageMatchField.Id;
                    filter.Option = PackageFieldMatchOption.EqualsCaseInsensitive;
                    filter.Value = packageId;
                    findOptions.Filters.Add(filter);

                    var findResult = connectResult.PackageCatalog.FindPackages(findOptions);

                    var match = findResult.Matches.ToArray().FirstOrDefault();
                    return match?.CatalogPackage;
                }
                catch (Exception ex)
                {
                    _logService?.LogError($"Error finding installed package {packageId}: {ex.Message}");
                    return null;
                }
            }, cancellationToken);
        }

        private string GetInstallErrorMessage(string packageId, InstallResult result)
        {
            return result.Status switch
            {
                InstallResultStatus.BlockedByPolicy => _localization.GetString("Progress_WinGet_Error_BlockedByPolicy", packageId),
                InstallResultStatus.CatalogError => _localization.GetString("Progress_WinGet_Error_CatalogError", packageId),
                InstallResultStatus.InternalError => _localization.GetString("Progress_WinGet_Error_InternalError", packageId),
                InstallResultStatus.InvalidOptions => _localization.GetString("Progress_WinGet_Error_InvalidOptions", packageId),
                InstallResultStatus.DownloadError => _localization.GetString("Progress_WinGet_Error_DownloadError", packageId),
                InstallResultStatus.InstallError => _localization.GetString("Progress_WinGet_Error_InstallError", packageId, result.InstallerErrorCode),
                InstallResultStatus.ManifestError => _localization.GetString("Progress_WinGet_Error_ManifestError", packageId),
                InstallResultStatus.NoApplicableInstallers => _localization.GetString("Progress_WinGet_Error_NoApplicableInstallers", packageId),
                InstallResultStatus.NoApplicableUpgrade => _localization.GetString("Progress_WinGet_Error_NoApplicableUpgrade", packageId),
                InstallResultStatus.PackageAgreementsNotAccepted => _localization.GetString("Progress_WinGet_Error_AgreementsNotAccepted", packageId),
                _ => _localization.GetString("Progress_WinGet_Error_InstallFailed", packageId, result.Status)
            };
        }

        private string GetUninstallErrorMessage(string packageId, UninstallResult result)
        {
            return result.Status switch
            {
                UninstallResultStatus.BlockedByPolicy => _localization.GetString("Progress_WinGet_Error_UninstallBlockedByPolicy", packageId),
                UninstallResultStatus.CatalogError => _localization.GetString("Progress_WinGet_Error_UninstallCatalogError", packageId),
                UninstallResultStatus.InternalError => _localization.GetString("Progress_WinGet_Error_UninstallInternalError", packageId),
                UninstallResultStatus.InvalidOptions => _localization.GetString("Progress_WinGet_Error_UninstallInvalidOptions", packageId),
                UninstallResultStatus.UninstallError => _localization.GetString("Progress_WinGet_Error_UninstallError", packageId, result.UninstallerErrorCode),
                _ => _localization.GetString("Progress_WinGet_Error_UninstallFailed", packageId, result.Status)
            };
        }

        private bool IsNetworkRelatedError(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            var lowerMessage = message.ToLowerInvariant();
            return lowerMessage.Contains("network") ||
                   lowerMessage.Contains("timeout") ||
                   lowerMessage.Contains("connection") ||
                   lowerMessage.Contains("dns") ||
                   lowerMessage.Contains("resolve") ||
                   lowerMessage.Contains("unreachable") ||
                   lowerMessage.Contains("offline") ||
                   lowerMessage.Contains("proxy") ||
                   lowerMessage.Contains("certificate") ||
                   lowerMessage.Contains("ssl") ||
                   lowerMessage.Contains("tls") ||
                   lowerMessage.Contains("download failed") ||
                   lowerMessage.Contains("no internet") ||
                   lowerMessage.Contains("connectivity");
        }
    }
}
