using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class WindowsAppsService(
    ILogService logService,
    IPowerShellExecutionService powerShellService,
    IWinGetService winGetService,
    IStoreDownloadService storeDownloadService = null,
    IDialogService dialogService = null,
    IUserPreferencesService userPreferencesService = null,
    ITaskProgressService taskProgressService = null,
    ILocalizationService localizationService = null) : IWindowsAppsService
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

    public async Task<OperationResult<bool>> InstallAppAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null)
    {
        try
        {
            if (!string.IsNullOrEmpty(item.WinGetPackageId) || !string.IsNullOrEmpty(item.AppxPackageName))
            {
                var packageId = item.WinGetPackageId ?? item.AppxPackageName;

                // Try WinGet first (official method)
                logService?.LogInformation($"Attempting to install {item.Name} using WinGet...");
                var cancellationToken = GetCurrentCancellationToken();
                var success = await winGetService.InstallPackageAsync(packageId, item.Name, cancellationToken);

                if (success)
                {
                    return OperationResult<bool>.Succeeded(true);
                }

                // If WinGet failed and we have a WinGetPackageId, try fallback to direct download
                // This bypasses market restrictions
                if (!string.IsNullOrEmpty(item.WinGetPackageId) && storeDownloadService != null)
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
                        var downloadButton = localizationService?.GetString("Dialog_Button_Download") ?? "Download";
                        var cancelButton = localizationService?.GetString("Dialog_Button_Cancel") ?? "Cancel";

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
                        var fallbackSuccess = await storeDownloadService.DownloadAndInstallPackageAsync(
                            item.WinGetPackageId,
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

            var script = $"Get-AppxPackage '*{item.AppxPackageName}*' | Remove-AppxPackage";
            try
            {
                var output = await powerShellService.ExecuteScriptAsync(script);
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

            var script = $"Add-WindowsCapability -Online -Name '{item.CapabilityName}'";
            try
            {
                var output = await powerShellService.ExecuteScriptAsync(script);
                return OperationResult<bool>.Succeeded(true);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Failed(ex.Message);
            }
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

            var script = $"Remove-WindowsCapability -Online -Name '{item.CapabilityName}'";
            try
            {
                var output = await powerShellService.ExecuteScriptAsync(script);
                return OperationResult<bool>.Succeeded(true);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Failed(ex.Message);
            }
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

            var script = $"Enable-WindowsOptionalFeature -Online -FeatureName '{item.OptionalFeatureName}' -All";
            try
            {
                var output = await powerShellService.ExecuteScriptAsync(script);
                return OperationResult<bool>.Succeeded(true);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Failed(ex.Message);
            }
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

            var script = $"Disable-WindowsOptionalFeature -Online -FeatureName '{item.OptionalFeatureName}'";
            try
            {
                var output = await powerShellService.ExecuteScriptAsync(script);
                return OperationResult<bool>.Succeeded(true);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Failed(ex.Message);
            }
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
}