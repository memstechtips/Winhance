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
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class WindowsAppsService(
    ILogService logService,
    IWinGetPackageInstaller winGetPackageInstaller,
    IWinGetBootstrapper winGetBootstrapper,
    IAppStatusDiscoveryService appStatusDiscoveryService,
    IStoreDownloadService storeDownloadService,
    IDialogService dialogService,
    IUserPreferencesService userPreferencesService,
    ITaskProgressService taskProgressService,
    ILocalizationService localizationService,
    ISettingApplicationService settingApplicationService,
    ISystemSettingsDiscoveryService systemSettingsDiscoveryService) : IWindowsAppsService
{
    public string DomainName => FeatureIds.WindowsApps;
    private const string FallbackConfirmationPreferenceKey = "StoreDownloadFallback_DontShowAgain";

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
            if (!string.IsNullOrEmpty(item.MsStoreId) || (item.WinGetPackageId != null && item.WinGetPackageId.Any()) || item.AppxPackageName?.Length > 0)
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
                    packageId = item.AppxPackageName?.FirstOrDefault();
                }

                // Try WinGet first (official method)
                logService?.LogInformation($"Attempting to install {item.Name} using WinGet...");
                var cancellationToken = taskProgressService.GetCurrentCancellationToken();
                var installResult = await winGetPackageInstaller.InstallPackageAsync(packageId!, source, item.Name, cancellationToken: cancellationToken).ConfigureAwait(false);

                if (installResult.Success)
                {
                    return OperationResult<bool>.Succeeded(true);
                }

                // If WinGet failed, check if Windows Update policy is blocking installations
                if (await IsUpdatePolicyDisabledAsync().ConfigureAwait(false))
                {
                    logService?.LogWarning($"Windows Update DLLs appear to be renamed (Disabled mode). Offering to fix for {item.Name}...");

                    var updateTitle = localizationService.GetString("Dialog_UpdatePolicyBlocking_Title") ?? "Windows Updates Disabled";
                    var updateMessage = localizationService.GetString("Dialog_UpdatePolicyBlocking_Message", item.Name) ??
                        $"The installation of '{item.Name}' could not complete, likely because Windows Updates are disabled.\n\n" +
                        "Disabling Windows Updates prevents app installations from the Microsoft Store from completing.\n\n" +
                        "Would you like Winhance to change the update policy to 'Paused for a long time' and retry the installation?";
                    var yesButton = localizationService.GetString("Button_Yes") ?? "Yes";
                    var noButton = localizationService.GetString("Button_No") ?? "No";

                    var userAccepted = (await dialogService.ShowConfirmationAsync(new ConfirmationRequest
                    {
                        Message = updateMessage,
                        Title = updateTitle,
                        ConfirmButtonText = yesButton,
                        CancelButtonText = noButton,
                    }).ConfigureAwait(false)).Confirmed;

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

                // If WinGet failed and we have a WinGetPackageId, try fallback to direct download
                // This bypasses market restrictions
                if (!string.IsNullOrEmpty(item.MsStoreId) || (item.WinGetPackageId != null && item.WinGetPackageId.Any()))
                {
                    logService?.LogWarning($"WinGet installation failed for {item.Name}. Checking if fallback method should be used...");

                    // Check if user has opted to not show the confirmation dialog
                    bool skipConfirmation = false;
                    skipConfirmation = await userPreferencesService.GetPreferenceAsync(FallbackConfirmationPreferenceKey, false).ConfigureAwait(false);

                    bool userConsent = skipConfirmation;

                    // Show confirmation dialog if needed
                    if (!skipConfirmation)
                    {
                        var title = localizationService.GetString("Dialog_FallbackDownload") ?? "Alternative Download Method";
                        var message = localizationService.GetString("WindowsApps_Msg_FallbackDownload", item.Name) ??
                                     $"The package '{item.Name}' could not be found via WinGet, likely due to geographic market restrictions.\n\n" +
                                     $"Winhance can download this package directly from Microsoft's servers using an alternative method (store.rg-adguard.net).\n\n" +
                                     $"• The package files come directly from Microsoft's official CDN\n" +
                                     $"• This method is completely legal and safe\n" +
                                     $"• It bypasses regional restrictions only\n\n" +
                                     $"Would you like to proceed with the alternative download method?";
                        var checkboxText = localizationService.GetString("WindowsApps_Checkbox_DontAskAgain") ?? "Don't ask me again for future installations";
                        var downloadButton = localizationService.GetString("Button_Download") ?? "Download";
                        var cancelButton = localizationService.GetString("Button_Cancel") ?? "Cancel";

                        var r = await dialogService.ShowConfirmationAsync(new ConfirmationRequest
                        {
                            Message = message,
                            CheckboxText = checkboxText,
                            Title = title,
                            ConfirmButtonText = downloadButton,
                            CancelButtonText = cancelButton,
                        }).ConfigureAwait(false);
                        bool confirmed = r.Confirmed;
                        bool dontShowAgain = r.CheckboxChecked;

                        userConsent = confirmed;

                        // Save preference if user checked "don't show again"
                        if (dontShowAgain)
                        {
                            await userPreferencesService.SetPreferenceAsync(FallbackConfirmationPreferenceKey, true).ConfigureAwait(false);
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
            var updateSettings = UpdateOptimizations.GetUpdateOptimizations();
            var policySetting = updateSettings.Settings.FirstOrDefault(s => s.Id == SettingIds.UpdatesPolicyMode);
            if (policySetting == null)
                return false;

            var states = await systemSettingsDiscoveryService.GetSettingStatesAsync(new[] { policySetting }).ConfigureAwait(false);
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
