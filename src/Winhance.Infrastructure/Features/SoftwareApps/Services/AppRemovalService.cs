using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Interfaces.WindowsRegistry;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services
{
    /// <summary>
    /// Service for removing standard applications from the system.
    /// </summary>
    public class AppRemovalService : IAppRemovalService
    {
        private readonly ILogService _logService;
        private readonly ISpecialAppHandlerService _specialAppHandlerService;
        private readonly IAppDiscoveryService _appDiscoveryService;
        private readonly IBloatRemovalScriptService _bloatRemovalScriptService;
        private readonly ISystemServices _systemServices;
        private readonly IWindowsRegistryService _registryService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppRemovalService"/> class.
        /// </summary>
        /// <param name="logService">The logging service.</param>
        /// <param name="specialAppHandlerService">The special app handler service.</param>
        /// <param name="appDiscoveryService">The app discovery service.</param>
        /// <param name="bloatRemovalScriptService">The bloat removal script service.</param>
        /// <param name="systemServices">The system services.</param>
        /// <param name="registryService">The registry service.</param>
        public AppRemovalService(
            ILogService logService,
            ISpecialAppHandlerService specialAppHandlerService,
            IAppDiscoveryService appDiscoveryService,
            IBloatRemovalScriptService bloatRemovalScriptService,
            ISystemServices systemServices,
            IWindowsRegistryService windowsRegistryService
        )
        {
            _logService = logService;
            _specialAppHandlerService = specialAppHandlerService;
            _appDiscoveryService = appDiscoveryService;
            _bloatRemovalScriptService = bloatRemovalScriptService;
            _systemServices = systemServices;
            _registryService = windowsRegistryService;
        }

        /// <inheritdoc/>
        public async Task<OperationResult<bool>> RemoveAppAsync(
            AppInfo appInfo,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (appInfo == null)
            {
                throw new ArgumentNullException(nameof(appInfo));
            }

            try
            {
                _logService.LogInformation($"Adding app {appInfo.Name} to BloatRemoval script");
                progress?.Report(
                    new TaskProgressDetail
                    {
                        Progress = 0,
                        StatusText = $"Adding app {appInfo.Name} to BloatRemoval script...",
                    }
                );

                // Add the app to the BloatRemoval script
                await _bloatRemovalScriptService.AddAppsToScriptAsync(
                    new List<AppInfo> { appInfo },
                    progress,
                    cancellationToken
                );

                _logService.LogSuccess(
                    $"Successfully added app {appInfo.Name} to BloatRemoval script"
                );
                progress?.Report(
                    new TaskProgressDetail
                    {
                        Progress = 100,
                        StatusText =
                            $"Successfully added app {appInfo.Name} to BloatRemoval script",
                        LogLevel = LogLevel.Success,
                    }
                );

                // Apply registry settings for this app if it has any
                try
                {
                    if (appInfo.RegistrySettings != null && appInfo.RegistrySettings.Length > 0)
                    {
                        _logService.LogInformation(
                            $"Found {appInfo.RegistrySettings.Length} registry settings for {appInfo.PackageName}"
                        );
                        
                        // Apply the registry settings
                        var settingsApplied = ApplyRegistrySettings(appInfo.RegistrySettings);
                        if (settingsApplied)
                        {
                            _logService.LogSuccess(
                                $"Successfully applied registry settings for {appInfo.PackageName}"
                            );
                        }
                        else
                        {
                            _logService.LogWarning(
                                $"Some registry settings for {appInfo.PackageName} could not be applied"
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogError(
                        $"Error applying registry settings for {appInfo.PackageName}",
                        ex
                    );
                }

                return OperationResult<bool>.Succeeded(true);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error adding app {appInfo.Name} to BloatRemoval script", ex);
                progress?.Report(
                    new TaskProgressDetail
                    {
                        Progress = 0,
                        StatusText =
                            $"Error adding app {appInfo.Name} to BloatRemoval script: {ex.Message}",
                        LogLevel = LogLevel.Error,
                    }
                );
                return OperationResult<bool>.Failed(
                    $"Error adding app {appInfo.Name} to BloatRemoval script: {ex.Message}",
                    ex
                );
            }
        }

        /// <inheritdoc/>
        public async Task<OperationResult<string>> GenerateRemovalScriptAsync(AppInfo appInfo)
        {
            if (appInfo == null)
            {
                throw new ArgumentNullException(nameof(appInfo));
            }

            try
            {
                // Generate a script for removing the app using the pipeline approach
                string script =
                    $@"
# Script to remove {appInfo.Name} ({appInfo.PackageName})
# Generated by Winhance on {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}

try {{
    # Remove the main app package
    $packagesToRemove = Get-AppxPackage | Where-Object {{ $_.Name -eq '{appInfo.PackageName}' }}
    
    if ($packagesToRemove -ne $null -and $packagesToRemove.Count -gt 0) {{
        $packagesToRemove | ForEach-Object {{
            Write-Output ""Removing package: $($_.PackageFullName)""
            Remove-AppxPackage -Package $_.PackageFullName -ErrorAction SilentlyContinue
        }}
    }}
    
    # Also remove the provisioned package
    $provPackages = Get-AppxProvisionedPackage -Online | Where-Object {{ $_.DisplayName -eq '{appInfo.PackageName}' }}
    
    if ($provPackages -ne $null -and $provPackages.Count -gt 0) {{
        $provPackages | ForEach-Object {{
            Write-Output ""Removing provisioned package: $($_.PackageName)""
            Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -ErrorAction SilentlyContinue
        }}
    }}";

                // Add subpackage removal if the app has subpackages
                if (appInfo.SubPackages != null && appInfo.SubPackages.Length > 0)
                {
                    script +=
                        $@"
    
    # Remove subpackages";

                    foreach (var subPackage in appInfo.SubPackages)
                    {
                        script +=
                            $@"
    Write-Output ""Processing subpackage: {subPackage}""
    
    # Remove the subpackage
    $subPackagesToRemove = Get-AppxPackage | Where-Object {{ $_.Name -eq '{subPackage}' }}
    
    if ($subPackagesToRemove -ne $null -and $subPackagesToRemove.Count -gt 0) {{
        $subPackagesToRemove | ForEach-Object {{
            Write-Output ""Removing subpackage: $($_.PackageFullName)""
            Remove-AppxPackage -Package $_.PackageFullName -ErrorAction SilentlyContinue
        }}
    }}
    
    # Also remove the provisioned subpackage
    $subProvPackages = Get-AppxProvisionedPackage -Online | Where-Object {{ $_.DisplayName -eq '{subPackage}' }}
    
    if ($subProvPackages -ne $null -and $subProvPackages.Count -gt 0) {{
        $subProvPackages | ForEach-Object {{
            Write-Output ""Removing provisioned subpackage: $($_.PackageName)""
            Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -ErrorAction SilentlyContinue
        }}
    }}";
                    }
                }

                // Close the try-catch block
                script +=
                    $@"
}} catch {{
    # Error handling without output
}}
";
                return OperationResult<string>.Succeeded(script);
            }
            catch (Exception ex)
            {
                _logService.LogError(
                    $"Error generating removal script for {appInfo.PackageName}",
                    ex
                );
                return OperationResult<string>.Failed(
                    $"Error generating removal script: {ex.Message}",
                    ex
                );
            }
        }

        /// <summary>
        /// Applies registry settings for an app.
        /// </summary>
        /// <param name="registrySettings">The registry settings to apply.</param>
        /// <returns>True if all settings were applied successfully, false otherwise.</returns>
        private bool ApplyRegistrySettings(AppRegistrySetting[] registrySettings)
        {
            if (registrySettings == null || !registrySettings.Any())
            {
                return true;
            }

            bool allSuccessful = true;

            foreach (var setting in registrySettings)
            {
                try
                {
                    // No need to check for metadata as it's not used in this version

                    // Apply the registry setting
                    bool success = _registryService.SetValue(
                        setting.Path,
                        setting.Name,
                        setting.Value,
                        setting.ValueKind
                    );

                    if (!success)
                    {
                        _logService.LogWarning(
                            $"Failed to apply registry setting: {setting.Path}\\{setting.Name}"
                        );
                        allSuccessful = false;
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogError(
                        $"Error applying registry setting: {setting.Path}\\{setting.Name}",
                        ex
                    );
                    allSuccessful = false;
                }
            }

            return allSuccessful;
        }
    }
}
