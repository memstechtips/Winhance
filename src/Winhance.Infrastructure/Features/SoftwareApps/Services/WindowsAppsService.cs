using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Native;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class WindowsAppsService(
    ILogService logService,
    IWinGetService winGetService,
    IAppStatusDiscoveryService appStatusDiscoveryService,
    IStoreDownloadService storeDownloadService = null,
    IDialogService dialogService = null,
    IUserPreferencesService userPreferencesService = null,
    ITaskProgressService taskProgressService = null,
    ILocalizationService localizationService = null,
    ISettingApplicationService settingApplicationService = null,
    ISystemSettingsDiscoveryService systemSettingsDiscoveryService = null) : IWindowsAppsService
{
    public string DomainName => FeatureIds.WindowsApps;
    private const string FallbackConfirmationPreferenceKey = "StoreDownloadFallback_DontShowAgain";

    private CancellationToken GetCurrentCancellationToken()
    {
        return taskProgressService?.CurrentTaskCancellationSource?.Token ?? CancellationToken.None;
    }

    public async Task<IEnumerable<ItemDefinition>> GetAppsAsync()
    {
        var allItems = new List<ItemDefinition>();
        allItems.AddRange(WindowsAppDefinitions.GetWindowsApps().Items);
        allItems.AddRange(CapabilityDefinitions.GetWindowsCapabilities().Items);
        allItems.AddRange(OptionalFeatureDefinitions.GetWindowsOptionalFeatures().Items);
        return allItems;
    }

    public async Task<ItemDefinition?> GetAppByIdAsync(string appId)
    {
        var apps = await GetAppsAsync();
        return apps.FirstOrDefault(app => app.Id == appId);
    }

    public async Task<Dictionary<string, bool>> CheckBatchInstalledAsync(IEnumerable<ItemDefinition> definitions)
    {
        return await appStatusDiscoveryService.GetInstallationStatusBatchAsync(definitions);
    }

    public async Task<OperationResult<bool>> InstallAppAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null)
    {
        try
        {
            if (!string.IsNullOrEmpty(item.MsStoreId) || (item.WinGetPackageId != null && item.WinGetPackageId.Any()) || !string.IsNullOrEmpty(item.AppxPackageName))
            {
                // Determine package ID and source
                string? packageId = null;
                string? source = null;

                if (!string.IsNullOrEmpty(item.MsStoreId))
                {
                    packageId = item.MsStoreId;
                    source = "msstore";
                }
                else if (item.WinGetPackageId != null && item.WinGetPackageId.Any())
                {
                    packageId = item.WinGetPackageId.FirstOrDefault();
                    source = "winget";
                }
                else
                {
                    packageId = item.AppxPackageName;
                }

                // Try WinGet first (official method)
                logService?.LogInformation($"Attempting to install {item.Name} using WinGet...");
                var cancellationToken = GetCurrentCancellationToken();
                var installResult = await winGetService.InstallPackageAsync(packageId, source, item.Name, cancellationToken);

                if (installResult.Success)
                {
                    return OperationResult<bool>.Succeeded(true);
                }

                // If WinGet failed, check if Windows Update policy is blocking installations
                if (await IsUpdatePolicyDisabledAsync() && dialogService != null && settingApplicationService != null)
                {
                    logService?.LogWarning($"Windows Update DLLs appear to be renamed (Disabled mode). Offering to fix for {item.Name}...");

                    var updateTitle = localizationService?.GetString("Dialog_UpdatePolicyBlocking_Title") ?? "Windows Updates Disabled";
                    var updateMessage = localizationService?.GetString("Dialog_UpdatePolicyBlocking_Message", item.Name) ??
                        $"The installation of '{item.Name}' could not complete, likely because Windows Updates are disabled.\n\n" +
                        "Disabling Windows Updates prevents app installations from the Microsoft Store from completing.\n\n" +
                        "Would you like Winhance to change the update policy to 'Paused for a long time' and retry the installation?";
                    var yesButton = localizationService?.GetString("Button_Yes") ?? "Yes";
                    var noButton = localizationService?.GetString("Button_No") ?? "No";

                    var userAccepted = await dialogService.ShowConfirmationAsync(
                        message: updateMessage,
                        title: updateTitle,
                        okButtonText: yesButton,
                        cancelButtonText: noButton
                    );

                    if (userAccepted)
                    {
                        logService?.LogInformation("User accepted update policy change. Switching to 'Paused for a long time'...");
                        try
                        {
                            await settingApplicationService.ApplySettingAsync("updates-policy-mode", true, 2);
                            logService?.LogInformation("Update policy changed to Paused. Retrying WinGet installation...");

                            var cancellationToken2 = GetCurrentCancellationToken();
                            var retryResult = await winGetService.InstallPackageAsync(packageId, source, item.Name, cancellationToken2);
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

                // If WinGet failed and we have a WinGetPackageId, try fallback to direct download
                // This bypasses market restrictions
                if ((!string.IsNullOrEmpty(item.MsStoreId) || (item.WinGetPackageId != null && item.WinGetPackageId.Any())) && storeDownloadService != null)
                {
                    logService?.LogWarning($"WinGet installation failed for {item.Name}. Checking if fallback method should be used...");

                    // Check if user has opted to not show the confirmation dialog
                    bool skipConfirmation = false;
                    if (userPreferencesService != null)
                    {
                        skipConfirmation = await userPreferencesService.GetPreferenceAsync(FallbackConfirmationPreferenceKey, false);
                    }

                    bool userConsent = skipConfirmation;

                    // Show confirmation dialog if needed
                    if (!skipConfirmation && dialogService != null)
                    {
                        var title = localizationService?.GetString("Dialog_FallbackDownload") ?? "Alternative Download Method";
                        var message = localizationService?.GetString("WindowsApps_Msg_FallbackDownload", item.Name) ??
                                     $"The package '{item.Name}' could not be found via WinGet, likely due to geographic market restrictions.\n\n" +
                                     $"Winhance can download this package directly from Microsoft's servers using an alternative method (store.rg-adguard.net).\n\n" +
                                     $"• The package files come directly from Microsoft's official CDN\n" +
                                     $"• This method is completely legal and safe\n" +
                                     $"• It bypasses regional restrictions only\n\n" +
                                     $"Would you like to proceed with the alternative download method?";
                        var checkboxText = localizationService?.GetString("WindowsApps_Checkbox_DontAskAgain") ?? "Don't ask me again for future installations";
                        var downloadButton = localizationService?.GetString("Button_Download") ?? "Download";
                        var cancelButton = localizationService?.GetString("Button_Cancel") ?? "Cancel";

                        var (confirmed, dontShowAgain) = await dialogService.ShowConfirmationWithCheckboxAsync(
                            message: message,
                            checkboxText: checkboxText,
                            title: title,
                            continueButtonText: downloadButton,
                            cancelButtonText: cancelButton,
                            titleBarIcon: "Download"
                        );

                        userConsent = confirmed;

                        // Save preference if user checked "don't show again"
                        if (dontShowAgain && userPreferencesService != null)
                        {
                            await userPreferencesService.SetPreferenceAsync(FallbackConfirmationPreferenceKey, true);
                            logService?.LogInformation("User opted to skip fallback confirmation in future");
                        }
                    }

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
                            cancellationToken);

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

    public async Task<OperationResult<bool>> UninstallAppAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null)
    {
        try
        {
            if (string.IsNullOrEmpty(item.AppxPackageName))
                return OperationResult<bool>.Failed("No package name specified");

            try
            {
                var packageManager = new Windows.Management.Deployment.PackageManager();
                var packages = packageManager.FindPackagesForUser("")
                    .Where(p => p.Id.Name.Contains(item.AppxPackageName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (packages.Count == 0)
                    return OperationResult<bool>.Failed($"Package '{item.AppxPackageName}' not found");

                foreach (var package in packages)
                {
                    await packageManager.RemovePackageAsync(package.Id.FullName);
                }

                return OperationResult<bool>.Succeeded(true);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Failed(ex.Message);
            }
        }
        catch (OperationCanceledException)
        {
            logService?.LogInformation($"Uninstall of {item.Name} was cancelled by user");
            return OperationResult<bool>.Cancelled("Uninstall cancelled by user");
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to uninstall {item.Name}: {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    public async Task<OperationResult<bool>> EnableCapabilityAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null)
    {
        try
        {
            if (string.IsNullOrEmpty(item.CapabilityName))
                return OperationResult<bool>.Failed("No capability name specified");

            var psCommand = $"Add-WindowsCapability -Online -Name '{item.CapabilityName}'";
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -Command \"& {{ {psCommand}; pause }}\"",
                    UseShellExecute = true,
                    CreateNoWindow = false
                }
            };
            process.Start();

            return OperationResult<bool>.Succeeded(true);
        }
        catch (OperationCanceledException)
        {
            logService?.LogInformation($"Enable capability {item.Name} was cancelled by user");
            return OperationResult<bool>.Cancelled("Enable capability cancelled by user");
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to enable capability {item.Name}: {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    public async Task<OperationResult<bool>> DisableCapabilityAsync(ItemDefinition item)
    {
        try
        {
            if (string.IsNullOrEmpty(item.CapabilityName))
                return OperationResult<bool>.Failed("No capability name specified");

            var result = await RemoveCapabilityAsync(item);
            return result;
        }
        catch (OperationCanceledException)
        {
            logService?.LogInformation($"Disable capability {item.Name} was cancelled by user");
            return OperationResult<bool>.Cancelled("Disable capability cancelled by user");
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to disable capability {item.Name}: {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    public async Task<OperationResult<bool>> EnableOptionalFeatureAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null)
    {
        try
        {
            if (string.IsNullOrEmpty(item.OptionalFeatureName))
                return OperationResult<bool>.Failed("No feature name specified");

            var psCommand = $"Enable-WindowsOptionalFeature -Online -FeatureName '{item.OptionalFeatureName}' -All -NoRestart";
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -Command \"& {{ {psCommand}; pause }}\"",
                    UseShellExecute = true,
                    CreateNoWindow = false
                }
            };
            process.Start();

            return OperationResult<bool>.Succeeded(true);
        }
        catch (OperationCanceledException)
        {
            logService?.LogInformation($"Enable feature {item.Name} was cancelled by user");
            return OperationResult<bool>.Cancelled("Enable feature cancelled by user");
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to enable feature {item.Name}: {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    public async Task<OperationResult<bool>> DisableOptionalFeatureAsync(ItemDefinition item)
    {
        try
        {
            if (string.IsNullOrEmpty(item.OptionalFeatureName))
                return OperationResult<bool>.Failed("No feature name specified");

            var result = await DisableOptionalFeatureNativeAsync(item);
            return result;
        }
        catch (OperationCanceledException)
        {
            logService?.LogInformation($"Disable feature {item.Name} was cancelled by user");
            return OperationResult<bool>.Cancelled("Disable feature cancelled by user");
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to disable feature {item.Name}: {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    public async Task<OperationResult<int>> RemoveAppxPackagesAsync(
        List<ItemDefinition> packages,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken ct = default)
    {
        if (packages == null || packages.Count == 0)
            return OperationResult<int>.Succeeded(0);

        int successCount = 0;
        int processedCount = 0;
        int totalCount = packages.Count;
        var errors = new List<string>();
        var throttle = new SemaphoreSlim(3);
        var perPackageTimeout = TimeSpan.FromSeconds(60);

        // Run on thread pool to avoid WPF SynchronizationContext deadlock
        // when multiple WinRT async operations complete in parallel
        await Task.Run(async () =>
        {
            // Single enumeration: build dictionary of all installed packages by name
            var enumerationPm = new Windows.Management.Deployment.PackageManager();
            var allPackages = enumerationPm.FindPackagesForUser("")
                .GroupBy(p => p.Id.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            // Single enumeration: build set of provisioned package family names
            var provisionedFamilyNames = new HashSet<string>(
                enumerationPm.FindProvisionedPackages().Select(p => p.Id.FamilyName),
                StringComparer.OrdinalIgnoreCase);

            logService?.LogInformation($"Enumerated {allPackages.Count} installed package names, {provisionedFamilyNames.Count} provisioned families");

            var tasks = packages.Select(async item =>
            {
                await throttle.WaitAsync(ct);
                try
                {
                    ct.ThrowIfCancellationRequested();

                    if (string.IsNullOrEmpty(item.AppxPackageName))
                        return;

                    // Look up from pre-built dictionary instead of per-package FindPackagesForUser
                    if (!allPackages.TryGetValue(item.AppxPackageName, out var installedPackages) || installedPackages.Count == 0)
                    {
                        logService?.LogInformation($"Package '{item.AppxPackageName}' not found/not installed, skipping.");
                        Interlocked.Increment(ref successCount);
                        return;
                    }

                    // Each parallel task uses its own PackageManager for mutation operations
                    var packageManager = new Windows.Management.Deployment.PackageManager();

                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeoutCts.CancelAfter(perPackageTimeout);

                    foreach (var pkg in installedPackages)
                    {
                        logService?.LogInformation($"Removing package: {pkg.Id.FullName}");
                        var removeTask = packageManager.RemovePackageAsync(
                            pkg.Id.FullName,
                            Windows.Management.Deployment.RemovalOptions.RemoveForAllUsers).AsTask(timeoutCts.Token);
                        await removeTask;
                    }

                    // Only deprovision if actually provisioned
                    var familyName = installedPackages[0].Id.FamilyName;
                    if (provisionedFamilyNames.Contains(familyName))
                    {
                        try
                        {
                            var deprovisionTask = packageManager.DeprovisionPackageForAllUsersAsync(familyName).AsTask(timeoutCts.Token);
                            await deprovisionTask;
                            logService?.LogInformation($"Deprovisioned package family: {familyName}");
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            logService?.LogWarning($"Deprovision warning for {familyName} (non-fatal): {ex.Message}");
                        }
                    }

                    // Also handle SubPackages if any
                    if (item.SubPackages != null)
                    {
                        foreach (var subPkg in item.SubPackages)
                        {
                            // Look up from pre-built dictionary
                            if (!allPackages.TryGetValue(subPkg, out var subInstalled))
                                continue;

                            foreach (var pkg in subInstalled)
                            {
                                logService?.LogInformation($"Removing sub-package: {pkg.Id.FullName}");
                                var subRemoveTask = packageManager.RemovePackageAsync(
                                    pkg.Id.FullName,
                                    Windows.Management.Deployment.RemovalOptions.RemoveForAllUsers).AsTask(timeoutCts.Token);
                                await subRemoveTask;
                            }
                        }
                    }

                    Interlocked.Increment(ref successCount);
                    logService?.LogInformation($"Successfully removed AppX package: {item.AppxPackageName}");
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw; // Propagate user-initiated cancellation
                }
                catch (OperationCanceledException)
                {
                    logService?.LogWarning($"Timed out removing AppX package {item.AppxPackageName} after {perPackageTimeout.TotalSeconds}s");
                    lock (errors) { errors.Add($"{item.Name}: timed out"); }
                }
                catch (Exception ex)
                {
                    logService?.LogError($"Failed to remove AppX package {item.AppxPackageName}: {ex.Message}");
                    lock (errors) { errors.Add($"{item.Name}: {ex.Message}"); }
                }
                finally
                {
                    throttle.Release();
                    var processed = Interlocked.Increment(ref processedCount);
                    progress?.Report(new TaskProgressDetail
                    {
                        StatusText = $"Processed {processed} of {totalCount} packages",
                        Progress = (double)processed / totalCount * 100
                    });
                }
            });

            await Task.WhenAll(tasks);
        }, ct);

        if (successCount == 0 && errors.Count > 0)
            return OperationResult<int>.Failed($"Failed to remove packages: {string.Join("; ", errors)}");

        return OperationResult<int>.Succeeded(successCount);
    }

    public async Task<OperationResult<bool>> RemoveCapabilityAsync(
        ItemDefinition item, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(item.CapabilityName))
                return OperationResult<bool>.Failed("No capability name specified");

            logService?.LogInformation($"[DISM-Remove] RemoveCapabilityAsync START: '{item.CapabilityName}' ({item.Name})");

            await DismSessionManager.ExecuteAsync(session =>
            {
                logService?.LogInformation("[DISM-Remove] Enumerating capabilities...");
                DismApi.ThrowIfFailed(
                    DismApi.DismGetCapabilities(session, out IntPtr capPtr, out uint count),
                    "GetCapabilities");
                try
                {
                    var capabilities = DismApi.MarshalArray<DismApi.DISM_CAPABILITY>(capPtr, count);
                    logService?.LogInformation($"[DISM-Remove] Enumerated {count} capabilities, searching for prefix '{item.CapabilityName}'");

                    var matching = capabilities
                        .Where(c =>
                        {
                            var name = Marshal.PtrToStringUni(c.Name);
                            return name != null
                                && name.StartsWith(item.CapabilityName, StringComparison.OrdinalIgnoreCase)
                                && c.State == DismApi.DismStateInstalled;
                        })
                        .ToList();

                    if (matching.Count == 0)
                    {
                        logService?.LogInformation($"[DISM-Remove] Capability '{item.CapabilityName}' not found or not installed (State != {DismApi.DismStateInstalled})");
                        return;
                    }

                    logService?.LogInformation($"[DISM-Remove] Found {matching.Count} matching installed capabilities");

                    using var cancelEvent = DismSessionManager.CreateCancelEvent(ct);
                    var cancelHandle = cancelEvent.SafeWaitHandle.DangerousGetHandle();
                    logService?.LogInformation($"[DISM-Remove] Cancel event handle: 0x{cancelHandle:X}, CanBeCanceled={ct.CanBeCanceled}");

                    foreach (var cap in matching)
                    {
                        var capName = Marshal.PtrToStringUni(cap.Name)!;
                        logService?.LogInformation($"[DISM-Remove] >>> Calling DismRemoveCapability('{capName}')...");
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        var hr = DismApi.DismRemoveCapability(session, capName, cancelHandle, DismApi.NoOpProgressCallback, IntPtr.Zero);
                        logService?.LogInformation($"[DISM-Remove] <<< DismRemoveCapability returned 0x{hr:X8} ({sw.ElapsedMilliseconds}ms)");
                        DismApi.ThrowIfFailed(hr, "RemoveCapability");
                    }
                }
                finally
                {
                    DismApi.DismDelete(capPtr);
                }
            }, ct, log: msg => logService?.LogInformation(msg));

            logService?.LogInformation($"[DISM-Remove] RemoveCapabilityAsync DONE: '{item.CapabilityName}'");
            return OperationResult<bool>.Succeeded(true);
        }
        catch (Exception ex)
        {
            logService?.LogError($"[DISM-Remove] RemoveCapabilityAsync FAILED for '{item.CapabilityName}': {ex.GetType().Name}: {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    public async Task<OperationResult<bool>> DisableOptionalFeatureNativeAsync(
        ItemDefinition item, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(item.OptionalFeatureName))
                return OperationResult<bool>.Failed("No feature name specified");

            logService?.LogInformation($"[DISM-Disable] DisableOptionalFeatureNativeAsync START: '{item.OptionalFeatureName}' ({item.Name})");

            await DismSessionManager.ExecuteAsync(session =>
            {
                using var cancelEvent = DismSessionManager.CreateCancelEvent(ct);
                var cancelHandle = cancelEvent.SafeWaitHandle.DangerousGetHandle();
                logService?.LogInformation($"[DISM-Disable] Cancel event handle: 0x{cancelHandle:X}, CanBeCanceled={ct.CanBeCanceled}");

                logService?.LogInformation($"[DISM-Disable] >>> Calling DismDisableFeature('{item.OptionalFeatureName}')...");
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var hr = DismApi.DismDisableFeature(session, item.OptionalFeatureName, null, false, cancelHandle, DismApi.NoOpProgressCallback, IntPtr.Zero);
                logService?.LogInformation($"[DISM-Disable] <<< DismDisableFeature returned 0x{hr:X8} ({sw.ElapsedMilliseconds}ms)");
                DismApi.ThrowIfFailed(hr, "DisableFeature");
                logService?.LogInformation($"[DISM-Disable] Feature '{item.OptionalFeatureName}' disabled successfully");
            }, ct, log: msg => logService?.LogInformation(msg));

            logService?.LogInformation($"[DISM-Disable] DisableOptionalFeatureNativeAsync DONE: '{item.OptionalFeatureName}'");
            return OperationResult<bool>.Succeeded(true);
        }
        catch (Exception ex)
        {
            logService?.LogError($"[DISM-Disable] DisableOptionalFeatureNativeAsync FAILED for '{item.OptionalFeatureName}': {ex.GetType().Name}: {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    public async Task<OperationResult<int>> RemoveCapabilitiesBatchAsync(
        List<ItemDefinition> capabilities,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken ct = default)
    {
        if (capabilities == null || capabilities.Count == 0)
            return OperationResult<int>.Succeeded(0);

        try
        {
            logService?.LogInformation($"[DISM-BatchRemove] RemoveCapabilitiesBatchAsync START: {capabilities.Count} capabilities");
            foreach (var c in capabilities)
                logService?.LogInformation($"[DISM-BatchRemove]   - '{c.CapabilityName}' ({c.Name})");

            int successCount = 0;

            await DismSessionManager.ExecuteAsync(session =>
            {
                logService?.LogInformation("[DISM-BatchRemove] Enumerating all capabilities...");
                DismApi.ThrowIfFailed(
                    DismApi.DismGetCapabilities(session, out IntPtr capPtr, out uint count),
                    "GetCapabilities");
                try
                {
                    var allCapabilities = DismApi.MarshalArray<DismApi.DISM_CAPABILITY>(capPtr, count);
                    logService?.LogInformation($"[DISM-BatchRemove] Enumerated {count} capabilities from DISM");

                    using var cancelEvent = DismSessionManager.CreateCancelEvent(ct);
                    var cancelHandle = cancelEvent.SafeWaitHandle.DangerousGetHandle();

                    foreach (var item in capabilities)
                    {
                        ct.ThrowIfCancellationRequested();

                        if (string.IsNullOrEmpty(item.CapabilityName))
                            continue;

                        logService?.LogInformation($"[DISM-BatchRemove] Processing: '{item.CapabilityName}'");

                        var matching = allCapabilities
                            .Where(c =>
                            {
                                var name = Marshal.PtrToStringUni(c.Name);
                                return name != null
                                    && name.StartsWith(item.CapabilityName, StringComparison.OrdinalIgnoreCase)
                                    && c.State == DismApi.DismStateInstalled;
                            })
                            .ToList();

                        if (matching.Count == 0)
                        {
                            logService?.LogInformation($"[DISM-BatchRemove] Capability '{item.CapabilityName}' not found or not installed (State != {DismApi.DismStateInstalled}), skipping.");
                            successCount++;
                            progress?.Report(new TaskProgressDetail
                            {
                                StatusText = $"Skipped {item.Name} (not installed)",
                                Progress = (double)successCount / capabilities.Count * 100
                            });
                            continue;
                        }

                        foreach (var cap in matching)
                        {
                            var capName = Marshal.PtrToStringUni(cap.Name)!;
                            logService?.LogInformation($"[DISM-BatchRemove] >>> Calling DismRemoveCapability('{capName}')...");
                            var sw = System.Diagnostics.Stopwatch.StartNew();
                            try
                            {
                                var hr = DismApi.DismRemoveCapability(session, capName, cancelHandle, DismApi.NoOpProgressCallback, IntPtr.Zero);
                                logService?.LogInformation($"[DISM-BatchRemove] <<< DismRemoveCapability returned 0x{hr:X8} ({sw.ElapsedMilliseconds}ms)");
                                DismApi.ThrowIfFailed(hr, "RemoveCapability");
                                logService?.LogInformation($"[DISM-BatchRemove] Removed capability: {capName}");
                            }
                            catch (Exception ex)
                            {
                                logService?.LogError($"[DISM-BatchRemove] Failed to remove capability {capName}: {ex.GetType().Name}: {ex.Message}");
                            }
                        }

                        successCount++;
                        progress?.Report(new TaskProgressDetail
                        {
                            StatusText = $"Removed {item.Name}",
                            Progress = (double)successCount / capabilities.Count * 100
                        });
                    }
                }
                finally
                {
                    DismApi.DismDelete(capPtr);
                }
            }, ct, log: msg => logService?.LogInformation(msg));

            logService?.LogInformation($"[DISM-BatchRemove] RemoveCapabilitiesBatchAsync DONE: {successCount}/{capabilities.Count}");
            return OperationResult<int>.Succeeded(successCount);
        }
        catch (Exception ex)
        {
            logService?.LogError($"[DISM-BatchRemove] RemoveCapabilitiesBatchAsync FAILED: {ex.GetType().Name}: {ex.Message}");
            return OperationResult<int>.Failed(ex.Message);
        }
    }

    public async Task<OperationResult<int>> DisableOptionalFeaturesBatchAsync(
        List<ItemDefinition> features,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken ct = default)
    {
        if (features == null || features.Count == 0)
            return OperationResult<int>.Succeeded(0);

        try
        {
            logService?.LogInformation($"[DISM-BatchDisable] DisableOptionalFeaturesBatchAsync START: {features.Count} features");
            foreach (var f in features)
                logService?.LogInformation($"[DISM-BatchDisable]   - '{f.OptionalFeatureName}' ({f.Name})");

            int successCount = 0;

            await DismSessionManager.ExecuteAsync(session =>
            {
                using var cancelEvent = DismSessionManager.CreateCancelEvent(ct);
                var cancelHandle = cancelEvent.SafeWaitHandle.DangerousGetHandle();
                logService?.LogInformation($"[DISM-BatchDisable] Cancel event handle: 0x{cancelHandle:X}, CanBeCanceled={ct.CanBeCanceled}");

                foreach (var item in features)
                {
                    ct.ThrowIfCancellationRequested();

                    if (string.IsNullOrEmpty(item.OptionalFeatureName))
                        continue;

                    try
                    {
                        logService?.LogInformation($"[DISM-BatchDisable] >>> Calling DismDisableFeature('{item.OptionalFeatureName}')...");
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        var hr = DismApi.DismDisableFeature(session, item.OptionalFeatureName, null, false, cancelHandle, DismApi.NoOpProgressCallback, IntPtr.Zero);
                        logService?.LogInformation($"[DISM-BatchDisable] <<< DismDisableFeature returned 0x{hr:X8} ({sw.ElapsedMilliseconds}ms)");
                        DismApi.ThrowIfFailed(hr, "DisableFeature");
                        logService?.LogInformation($"[DISM-BatchDisable] Feature '{item.OptionalFeatureName}' disabled successfully");
                        successCount++;
                        progress?.Report(new TaskProgressDetail
                        {
                            StatusText = $"Disabled {item.Name}",
                            Progress = (double)successCount / features.Count * 100
                        });
                    }
                    catch (Exception ex)
                    {
                        logService?.LogError($"[DISM-BatchDisable] Failed to disable feature '{item.OptionalFeatureName}': {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }, ct, log: msg => logService?.LogInformation(msg));

            logService?.LogInformation($"[DISM-BatchDisable] DisableOptionalFeaturesBatchAsync DONE: {successCount}/{features.Count}");
            return OperationResult<int>.Succeeded(successCount);
        }
        catch (Exception ex)
        {
            logService?.LogError($"[DISM-BatchDisable] DisableOptionalFeaturesBatchAsync FAILED: {ex.GetType().Name}: {ex.Message}");
            return OperationResult<int>.Failed(ex.Message);
        }
    }

    public async Task<OperationResult<bool>> UninstallViaRegistryAsync(
        ItemDefinition item, IProgress<TaskProgressDetail>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(item.RegistryUninstallSearchPattern))
                return OperationResult<bool>.Failed("No registry uninstall search pattern specified");

            logService?.LogInformation($"Starting registry-based uninstall for: {item.Name}");

            // Stop processes
            if (item.ProcessesToStop != null)
            {
                foreach (var processName in item.ProcessesToStop)
                {
                    try
                    {
                        var processes = Process.GetProcessesByName(processName);
                        foreach (var proc in processes)
                        {
                            logService?.LogInformation($"Stopping process: {processName} (PID {proc.Id})");
                            proc.Kill();
                            await proc.WaitForExitAsync(ct);
                            proc.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        logService?.LogWarning($"Failed to stop process {processName}: {ex.Message}");
                    }
                }
            }

            progress?.Report(new TaskProgressDetail
            {
                StatusText = $"Searching for {item.Name} uninstall entries...",
                IsIndeterminate = true
            });

            // Search registry for uninstall strings
            var uninstallBasePaths = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            bool uninstallExecuted = false;

            foreach (var basePath in uninstallBasePaths)
            {
                try
                {
                    using var baseKey = Registry.LocalMachine.OpenSubKey(basePath);
                    if (baseKey == null) continue;

                    foreach (var subKeyName in baseKey.GetSubKeyNames())
                    {
                        if (!MatchesWildcard(subKeyName, item.RegistryUninstallSearchPattern))
                            continue;

                        using var subKey = baseKey.OpenSubKey(subKeyName);
                        var uninstallString = subKey?.GetValue("UninstallString") as string;
                        if (string.IsNullOrEmpty(uninstallString)) continue;

                        logService?.LogInformation($"Found uninstall string: {uninstallString}");

                        await ExecuteUninstallStringAsync(uninstallString, ct);
                        uninstallExecuted = true;
                    }
                }
                catch (Exception ex)
                {
                    logService?.LogError($"Error searching registry path {basePath}: {ex.Message}");
                }
            }

            // Also check HKCU
            try
            {
                var hkcuPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
                using var hkcuKey = Registry.CurrentUser.OpenSubKey(hkcuPath);
                if (hkcuKey != null)
                {
                    foreach (var subKeyName in hkcuKey.GetSubKeyNames())
                    {
                        if (!MatchesWildcard(subKeyName, item.RegistryUninstallSearchPattern))
                            continue;

                        using var subKey = hkcuKey.OpenSubKey(subKeyName);
                        var uninstallString = subKey?.GetValue("UninstallString") as string;
                        if (string.IsNullOrEmpty(uninstallString)) continue;

                        logService?.LogInformation($"Found HKCU uninstall string: {uninstallString}");

                        await ExecuteUninstallStringAsync(uninstallString, ct);
                        uninstallExecuted = true;
                    }
                }
            }
            catch (Exception ex)
            {
                logService?.LogError($"Error searching HKCU registry: {ex.Message}");
            }

            if (!uninstallExecuted)
            {
                logService?.LogInformation($"No uninstall strings found for {item.Name}");
            }

            progress?.Report(new TaskProgressDetail
            {
                StatusText = $"Registry-based uninstall completed for {item.Name}",
                IsIndeterminate = false
            });

            return OperationResult<bool>.Succeeded(true);
        }
        catch (Exception ex)
        {
            logService?.LogError($"Failed registry-based uninstall for {item.Name}: {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    private async Task ExecuteUninstallStringAsync(string uninstallString, CancellationToken ct)
    {
        string exePath;
        string args;

        // Parse quoted path: "C:\path\to\exe.exe" args
        if (uninstallString.StartsWith('"'))
        {
            var closingQuote = uninstallString.IndexOf('"', 1);
            if (closingQuote > 0)
            {
                exePath = uninstallString.Substring(1, closingQuote - 1);
                args = uninstallString.Substring(closingQuote + 1).Trim();
            }
            else
            {
                exePath = uninstallString.Trim('"');
                args = string.Empty;
            }
        }
        else
        {
            // No quotes - take everything as the exe path (may include args)
            var spaceIndex = uninstallString.IndexOf(' ');
            if (spaceIndex > 0)
            {
                exePath = uninstallString.Substring(0, spaceIndex);
                args = uninstallString.Substring(spaceIndex + 1).Trim();
            }
            else
            {
                exePath = uninstallString;
                args = string.Empty;
            }
        }

        // Add silent flags based on the uninstaller type
        if (exePath.Contains("OfficeClickToRun", StringComparison.OrdinalIgnoreCase))
        {
            args += " DisplayLevel=False";
        }
        else
        {
            args += " /silent";
        }

        logService?.LogInformation($"Executing uninstall: {exePath} {args}");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync(ct);
        logService?.LogInformation($"Uninstall process exited with code: {process.ExitCode}");
        process.Dispose();
    }

    private async Task<bool> IsUpdatePolicyDisabledAsync()
    {
        if (systemSettingsDiscoveryService == null)
            return false;

        try
        {
            var updateSettings = UpdateOptimizations.GetUpdateOptimizations();
            var policySetting = updateSettings.Settings.FirstOrDefault(s => s.Id == "updates-policy-mode");
            if (policySetting == null)
                return false;

            var states = await systemSettingsDiscoveryService.GetSettingStatesAsync(new[] { policySetting });
            if (states.TryGetValue("updates-policy-mode", out var state) && state.Success)
            {
                return state.CurrentValue is int index && index == 3;
            }
        }
        catch (Exception ex)
        {
            logService?.LogError($"Failed to check update policy state: {ex.Message}");
        }

        return false;
    }

    private static bool MatchesWildcard(string input, string pattern)
    {
        // Simple wildcard matching: only supports trailing '*'
        if (pattern.EndsWith('*'))
        {
            var prefix = pattern.TrimEnd('*');
            return input.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        return input.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}
