using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

internal class WindowsAppsService(
    ILogService logService,
    IWinGetPackageInstaller winGetPackageInstaller,
    IWinGetBootstrapper winGetBootstrapper,
    IAppStatusDiscoveryService appStatusDiscoveryService,
    IStoreDownloadService storeDownloadService,
    IInstallConsent installConsent,
    ITaskProgressService taskProgressService,
    ISettingApplicationService settingApplicationService,
    ICatalogSettingStateProvider settingStateProvider) : IWindowsAppsService
{
    public string DomainName => FeatureIds.WindowsApps;

    public event EventHandler? WinGetReady
    {
        add => winGetBootstrapper.WinGetInstalled += value;
        remove => winGetBootstrapper.WinGetInstalled -= value;
    }

    public void InvalidateStatusCache() => appStatusDiscoveryService.InvalidateCache();

    public Task<IEnumerable<ItemDefinition>> GetAppsAsync()
    {
        var allItems = new List<ItemDefinition>();
        allItems.AddRange(WindowsAppDefinitions.GetWindowsApps().Items);
        allItems.AddRange(CapabilityDefinitions.GetWindowsCapabilities().Items);
        allItems.AddRange(OptionalFeatureDefinitions.GetWindowsOptionalFeatures().Items);
        return Task.FromResult<IEnumerable<ItemDefinition>>(allItems);
    }

    public async Task<ItemDefinition?> GetAppByIdAsync(string appId)
    {
        var apps = await GetAppsAsync().ConfigureAwait(false);
        return apps.FirstOrDefault(app => app.Id == appId);
    }

    public async Task<Dictionary<string, bool>> CheckBatchInstalledAsync(IEnumerable<ItemDefinition> definitions)
    {
        return await appStatusDiscoveryService.GetInstallationStatusBatchAsync(definitions).ConfigureAwait(false);
    }

    public async Task<OperationResult<bool>> InstallAppAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null)
    {
        try
        {
            if (!string.IsNullOrEmpty(item.MsStoreId) || (item.WinGetPackageId != null && item.WinGetPackageId.Length > 0) || item.AppxPackageName?.Length > 0)
            {
                string? packageId = null;
                string? source = null;

                if (!string.IsNullOrEmpty(item.MsStoreId))
                {
                    packageId = item.MsStoreId;
                    source = "msstore";
                }
                else if (item.WinGetPackageId != null && item.WinGetPackageId.Length > 0)
                {
                    packageId = item.WinGetPackageId.FirstOrDefault();
                    source = "winget";
                }
                else
                {
                    packageId = item.AppxPackageName?.FirstOrDefault();
                }

                logService?.LogInformation($"Attempting to install {item.Name} using WinGet...");
                var cancellationToken = taskProgressService.GetCurrentCancellationToken();
                var installResult = await winGetPackageInstaller.InstallPackageAsync(packageId!, source, item.Name, cancellationToken: cancellationToken).ConfigureAwait(false);

                if (installResult.Success)
                {
                    return OperationResult<bool>.Succeeded(true);
                }

                if (await IsUpdatePolicyDisabledAsync().ConfigureAwait(false))
                {
                    logService?.LogWarning($"Windows Update DLLs appear to be renamed (Disabled mode). Offering to fix for {item.Name}...");

                    var userAccepted = await installConsent.AllowUpdatePolicyChangeAsync(item.Name).ConfigureAwait(false);

                    if (userAccepted)
                    {
                        logService?.LogInformation("User accepted update policy change. Switching to 'Paused for a long time'...");
                        try
                        {
                            await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                            {
                                SettingId = SettingIds.UpdatesPolicyMode,
                                Enable = true,
                                Value = 2
                            }).ConfigureAwait(false);
                            logService?.LogInformation("Update policy changed to Paused. Retrying WinGet installation...");

                            var cancellationToken2 = taskProgressService.GetCurrentCancellationToken();
                            var retryResult = await winGetPackageInstaller.InstallPackageAsync(packageId!, source, item.Name, cancellationToken: cancellationToken2).ConfigureAwait(false);
                            if (retryResult.Success)
                            {
                                return OperationResult<bool>.Succeeded(true);
                            }
                            logService?.LogWarning($"Retry after policy change also failed for {item.Name}. Continuing to fallback...");
                        }
                        catch (Exception ex)
                        {
                            logService?.LogError($"Failed to change update policy or retry install: {ex.Message}");
                        }
                    }
                    else
                    {
                        logService?.LogInformation($"User declined update policy change for {item.Name}");
                    }
                }

                if (!string.IsNullOrEmpty(item.MsStoreId) || (item.WinGetPackageId != null && item.WinGetPackageId.Length > 0))
                {
                    logService?.LogWarning($"WinGet installation failed for {item.Name}. Checking if fallback method should be used...");

                    var userConsent = await installConsent.AllowFallbackDownloadAsync(item.Name).ConfigureAwait(false);

                    if (!userConsent)
                    {
                        logService?.LogInformation($"User declined fallback installation for {item.Name}");
                        return OperationResult<bool>.Failed("Installation cancelled by user");
                    }

                    logService?.LogInformation($"Attempting fallback installation method for {item.Name}...");

                    try
                    {
                        var fallbackPackageId = item.MsStoreId ?? item.WinGetPackageId![0];
                        var fallbackSuccess = await storeDownloadService.DownloadAndInstallPackageAsync(
                            fallbackPackageId,
                            item.Name,
                            cancellationToken).ConfigureAwait(false);

                        if (fallbackSuccess)
                        {
                            logService?.LogInformation($"Successfully installed {item.Name} using fallback method");
                            return OperationResult<bool>.Succeeded(true);
                        }

                        logService?.LogError($"Fallback installation also failed for {item.Name}");
                    }
                    catch (OperationCanceledException)
                    {
                        logService?.LogInformation($"Installation of {item.Name} was cancelled by user");
                        return OperationResult<bool>.Cancelled("Installation cancelled by user");
                    }
                    catch (Exception fallbackEx)
                    {
                        logService?.LogError($"Fallback installation error for {item.Name}: {fallbackEx.Message}");
                    }
                }

                return OperationResult<bool>.Failed("Installation failed with both WinGet and fallback methods");
            }

            return OperationResult<bool>.Failed($"App type not supported: {item.Name}");
        }
        catch (OperationCanceledException)
        {
            logService?.LogInformation($"Installation of {item.Name} was cancelled by user");
            return OperationResult<bool>.Cancelled("Installation cancelled by user");
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to install {item.Name}: {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    private async Task<bool> IsUpdatePolicyDisabledAsync()
    {
        try
        {
            var policySetting = SettingCatalog.Find(SettingIds.UpdatesPolicyMode);
            if (policySetting == null)
                return false;

            // Read the update-policy state from the full-state provider. UpdatePolicyDetector applies the
            // precedence where renamed DLLs -> Disabled = index 3, so the DLL-rename signal is preserved,
            // unlike a registry-only read.
            var states = await settingStateProvider.GetStatesAsync(new[] { policySetting }).ConfigureAwait(false);
            if (states.TryGetValue(SettingIds.UpdatesPolicyMode, out var state) && state.Success)
            {
                // Always record what was discovered, so support transcripts show why
                // the "updates disabled" dialog did or didn't appear after a failed install.
                logService?.LogInformation(
                    $"Update policy state at install-failure check: index={state.CurrentValue ?? "(null)"} (dialog fires only on index 3 = Disabled)");
                return state.CurrentValue is int index && index == 3;
            }

            logService?.LogWarning("Update policy state could not be determined at install-failure check");
        }
        catch (Exception ex)
        {
            logService?.LogError($"Failed to check update policy state: {ex.Message}");
        }

        return false;
    }

}
