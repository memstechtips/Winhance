using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>
/// Service for removing standard applications from the system.
/// </summary>
public class AppRemovalService : IAppRemovalService
{
    private readonly ILogService _logService;
    private readonly ISpecialAppHandlerService _specialAppHandlerService;
    private readonly IAppDiscoveryService _appDiscoveryService;
    private readonly IScriptTemplateProvider _scriptTemplateProvider;
    private readonly ISystemServices _systemServices;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppRemovalService"/> class.
    /// </summary>
    /// <param name="logService">The logging service.</param>
    /// <param name="specialAppHandlerService">The special app handler service.</param>
    /// <param name="appDiscoveryService">The app discovery service.</param>
    /// <param name="scriptTemplateProvider">The script template provider.</param>
    /// <param name="systemServices">The system services.</param>
    public AppRemovalService(
        ILogService logService,
        ISpecialAppHandlerService specialAppHandlerService,
        IAppDiscoveryService appDiscoveryService,
        IScriptTemplateProvider scriptTemplateProvider,
        ISystemServices systemServices)
    {
        _logService = logService;
        _specialAppHandlerService = specialAppHandlerService;
        _appDiscoveryService = appDiscoveryService;
        _scriptTemplateProvider = scriptTemplateProvider;
        _systemServices = systemServices;
    }

    /// <inheritdoc/>
    public async Task<OperationResult<bool>> RemoveAppAsync(
        AppInfo appInfo,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (appInfo == null)
        {
            throw new ArgumentNullException(nameof(appInfo));
        }
        // Call the other overload and return its result
        return await RemoveAppAsync(appInfo.PackageName, progress, cancellationToken);
    }

    /// <summary>
    /// Removes an application by package name.
    /// </summary>
    /// <param name="packageName">The package name of the application to remove.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An operation result indicating success or failure with error details.</returns>
    public async Task<OperationResult<bool>> RemoveAppAsync(
        string packageName,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logService.LogInformation($"Removing app: {packageName}");
            
            // Get all standard apps to find the app definition
            var allRemovableApps = (await _appDiscoveryService.GetStandardAppsAsync()).ToList();
            
            // Find the app definition that matches the current app
            var appDefinition = allRemovableApps.FirstOrDefault(a =>
                a.PackageName.Equals(packageName, StringComparison.OrdinalIgnoreCase));
            
            // Get subpackages if any
            string[]? subPackages = appDefinition?.SubPackages;
            if (subPackages != null && subPackages.Length > 0)
            {
                _logService.LogInformation($"App {packageName} has {subPackages.Length} subpackages that will also be removed");
            }
            
            // Handle standard app removal
            using var powerShell = PowerShellFactory.CreateWindowsPowerShell(_logService, _systemServices);
            // No need to set execution policy as it's already done in the factory
            
            powerShell.AddScript(@"
                param($packageName, $subPackages)
                try {
                    $success = $true
                    
                    # Remove the main app package
                    $packagesToRemove = Get-AppxPackage | Where-Object { $_.Name -eq $packageName }
                    
                    if ($packagesToRemove -ne $null -and $packagesToRemove.Count -gt 0) {
                        $packagesToRemove | ForEach-Object {
                            Write-Output ""Removing package: $($_.PackageFullName)""
                            Remove-AppxPackage -Package $_.PackageFullName -ErrorAction SilentlyContinue
                        }
                    }
                    
                    # Also remove the provisioned package
                    $provPackages = Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -eq $packageName }
                    
                    if ($provPackages -ne $null -and $provPackages.Count -gt 0) {
                        $provPackages | ForEach-Object {
                            Write-Output ""Removing provisioned package: $($_.PackageName)""
                            Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -ErrorAction SilentlyContinue
                        }
                    }
                    
                    # If we have subpackages, remove those too
                    if ($subPackages -ne $null) {
                        foreach ($subPackage in $subPackages) {
                            Write-Output ""Processing subpackage: $subPackage""
                            
                            # Remove the subpackage
                            $subPackagesToRemove = Get-AppxPackage | Where-Object { $_.Name -eq $subPackage }
                            
                            if ($subPackagesToRemove -ne $null -and $subPackagesToRemove.Count -gt 0) {
                                $subPackagesToRemove | ForEach-Object {
                                    Write-Output ""Removing subpackage: $($_.PackageFullName)""
                                    Remove-AppxPackage -Package $_.PackageFullName -ErrorAction SilentlyContinue
                                }
                            }
                            
                            # Also remove the provisioned subpackage
                            $subProvPackages = Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -eq $subPackage }
                            
                            if ($subProvPackages -ne $null -and $subProvPackages.Count -gt 0) {
                                $subProvPackages | ForEach-Object {
                                    Write-Output ""Removing provisioned subpackage: $($_.PackageName)""
                                    Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -ErrorAction SilentlyContinue
                                }
                            }
                        }
                    }
                    
                    # Check if the main package is still installed
                    $stillInstalled = Get-AppxPackage | Where-Object { $_.Name -eq $packageName }
                    $mainPackageRemoved = ($stillInstalled -eq $null -or $stillInstalled.Count -eq 0)
                    
                    # Check if any subpackages are still installed
                    $subPackagesRemoved = $true
                    if ($subPackages -ne $null) {
                        foreach ($subPackage in $subPackages) {
                            $stillInstalledSub = Get-AppxPackage | Where-Object { $_.Name -eq $subPackage }
                            if ($stillInstalledSub -ne $null -and $stillInstalledSub.Count -gt 0) {
                                $subPackagesRemoved = $false
                                Write-Output ""Subpackage $subPackage is still installed""
                                break
                            }
                        }
                    }
                    
                    # Return true only if both main package and all subpackages are removed
                    return $mainPackageRemoved -and $subPackagesRemoved
                }
                catch {
                    Write-Error ""Error removing package: $_""
                    return $false
                }
            ");
            powerShell.AddParameter("packageName", packageName);
            powerShell.AddParameter("subPackages", subPackages);
            
            var result = await Task.Run(() => powerShell.Invoke<bool>());
            var success = result.FirstOrDefault();
            
            if (!success)
            {
                _logService.LogError($"Failed to remove app: {packageName}");
            }
            else
            {
                _logService.LogSuccess($"Successfully removed app: {packageName}");
                
                // Apply registry settings for this app if it has any
                try
                {
                    // If we found the app definition and it has registry settings, apply them
                    if (appDefinition?.RegistrySettings != null && appDefinition.RegistrySettings.Length > 0)
                    {
                        _logService.LogInformation($"Found {appDefinition.RegistrySettings.Length} registry settings for {packageName}");
                        var registrySettings = appDefinition.RegistrySettings.ToList();
                        
                        // Apply the registry settings
                        var settingsApplied = await ApplyRegistrySettingsAsync(registrySettings);
                        if (settingsApplied)
                        {
                            _logService.LogSuccess($"Successfully applied registry settings for {packageName}");
                        }
                        else
                        {
                            _logService.LogWarning($"Some registry settings for {packageName} could not be applied");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error applying registry settings for {packageName}", ex);
                }
            }
            // Return success status after handling registry settings
            return success
                ? OperationResult<bool>.Succeeded(true)
                : OperationResult<bool>.Failed($"Failed to remove app: {packageName}");
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error removing app: {packageName}", ex);
            // Return failure with exception details
            return OperationResult<bool>.Failed($"Error removing app: {packageName}", ex);
        }
    }

    /// <inheritdoc/>
    public Task<OperationResult<string>> GenerateRemovalScriptAsync(AppInfo appInfo)
    {
        if (appInfo == null)
        {
            throw new ArgumentNullException(nameof(appInfo));
        }

        try
        {
            // Generate a script for removing the app using the pipeline approach
            string script = $@"
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
                script += $@"
    
    # Remove subpackages";

                foreach (var subPackage in appInfo.SubPackages)
                {
                    script += $@"
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
            script += $@"
}} catch {{
    # Error handling without output
}}
";
            return Task.FromResult(OperationResult<string>.Succeeded(script));
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error generating removal script for {appInfo.PackageName}", ex);
            return Task.FromResult(OperationResult<string>.Failed($"Error generating removal script: {ex.Message}", ex));
        }
    }


    /// <inheritdoc/>
    public async Task<List<(string Name, bool Success, string? Error)>> RemoveAppsInBatchAsync(
        List<AppInfo> apps)
    {
        if (apps == null)
        {
            throw new ArgumentNullException(nameof(apps));
        }

        return await RemoveAppsInBatchAsync(apps.Select(a => a.PackageName).ToList());
    }

    /// <inheritdoc/>
    public async Task<List<(string Name, bool Success, string? Error)>> RemoveAppsInBatchAsync(
        List<string> packageNames)
    {
        var results = new List<(string Name, bool Success, string? Error)>();
        
        try
        {
            _logService.LogInformation($"Starting batch removal of {packageNames.Count} apps");
            
            using var powerShell = PowerShellFactory.CreateWindowsPowerShell(_logService, _systemServices);
            // No need to set execution policy as it's already done in the factory
            
            // Get all standard apps to find app definitions (do this once for the batch)
            var allApps = (await _appDiscoveryService.GetStandardAppsAsync()).ToList();
            
            foreach (var packageName in packageNames)
            {
                try
                {
                    _logService.LogInformation($"Removing app: {packageName}");
                    
                    // Find the app definition that matches the current app
                    var appDefinition = allApps.FirstOrDefault(a =>
                        a.PackageName.Equals(packageName, StringComparison.OrdinalIgnoreCase));
                    
                    // Get subpackages if any
                    string[]? subPackages = appDefinition?.SubPackages;
                    if (subPackages != null && subPackages.Length > 0)
                    {
                        _logService.LogInformation($"App {packageName} has {subPackages.Length} subpackages that will also be removed");
                    }
                    
                    powerShell.Commands.Clear();
                    
                    powerShell.AddScript(@"
                        param($packageName, $subPackages)
                        try {
                            $success = $true
                            
                            # Remove the main app package
                            $packagesToRemove = Get-AppxPackage | Where-Object { $_.Name -eq $packageName }
                            
                            if ($packagesToRemove -ne $null -and $packagesToRemove.Count -gt 0) {
                                $packagesToRemove | ForEach-Object {
                                    Write-Output ""Removing package: $($_.PackageFullName)""
                                    Remove-AppxPackage -Package $_.PackageFullName -ErrorAction SilentlyContinue
                                }
                            }
                            
                            # Also remove the provisioned package
                            $provPackages = Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -eq $packageName }
                            
                            if ($provPackages -ne $null -and $provPackages.Count -gt 0) {
                                $provPackages | ForEach-Object {
                                    Write-Output ""Removing provisioned package: $($_.PackageName)""
                                    Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -ErrorAction SilentlyContinue
                                }
                            }
                            
                            # If we have subpackages, remove those too
                            if ($subPackages -ne $null) {
                                foreach ($subPackage in $subPackages) {
                                    Write-Output ""Processing subpackage: $subPackage""
                                    
                                    # Remove the subpackage
                                    $subPackagesToRemove = Get-AppxPackage | Where-Object { $_.Name -eq $subPackage }
                                    
                                    if ($subPackagesToRemove -ne $null -and $subPackagesToRemove.Count -gt 0) {
                                        $subPackagesToRemove | ForEach-Object {
                                            Write-Output ""Removing subpackage: $($_.PackageFullName)""
                                            Remove-AppxPackage -Package $_.PackageFullName -ErrorAction SilentlyContinue
                                        }
                                    }
                                    
                                    # Also remove the provisioned subpackage
                                    $subProvPackages = Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -eq $subPackage }
                                    
                                    if ($subProvPackages -ne $null -and $subProvPackages.Count -gt 0) {
                                        $subProvPackages | ForEach-Object {
                                            Write-Output ""Removing provisioned subpackage: $($_.PackageName)""
                                            Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -ErrorAction SilentlyContinue
                                        }
                                    }
                                }
                            }
                            
                            # Check if the main package is still installed
                            $stillInstalled = Get-AppxPackage | Where-Object { $_.Name -eq $packageName }
                            $mainPackageRemoved = ($stillInstalled -eq $null -or $stillInstalled.Count -eq 0)
                            
                            # Check if any subpackages are still installed
                            $subPackagesRemoved = $true
                            if ($subPackages -ne $null) {
                                foreach ($subPackage in $subPackages) {
                                    $stillInstalledSub = Get-AppxPackage | Where-Object { $_.Name -eq $subPackage }
                                    if ($stillInstalledSub -ne $null -and $stillInstalledSub.Count -gt 0) {
                                        $subPackagesRemoved = $false
                                        Write-Output ""Subpackage $subPackage is still installed""
                                        break
                                    }
                                }
                            }
                            
                            # Return true only if both main package and all subpackages are removed
                            return $mainPackageRemoved -and $subPackagesRemoved
                        }
                        catch {
                            Write-Error ""Error removing package: $_""
                            return $false
                        }
                    ");
                    powerShell.AddParameter("packageName", packageName);
                    powerShell.AddParameter("subPackages", subPackages);
                    
                    var result = await Task.Run(() => powerShell.Invoke<bool>());
                    var success = result.FirstOrDefault();
                    results.Add((packageName, success, success ? null : "Failed to remove app"));
                    _logService.LogInformation($"Removal of {packageName} {(success ? "succeeded" : "failed")}");
                    
                    // Apply registry settings for this app if it has any and removal was successful
                    if (success)
                    {
                        try
                        {
                            // If we found the app definition and it has registry settings, apply them
                            if (appDefinition?.RegistrySettings != null && appDefinition.RegistrySettings.Length > 0)
                            {
                                _logService.LogInformation($"Found {appDefinition.RegistrySettings.Length} registry settings for {packageName}");
                                var registrySettings = appDefinition.RegistrySettings.ToList();
                                
                                // Apply the registry settings
                                var settingsApplied = await ApplyRegistrySettingsAsync(registrySettings);
                                if (settingsApplied)
                                {
                                    _logService.LogSuccess($"Successfully applied registry settings for {packageName}");
                                }
                                else
                                {
                                    _logService.LogWarning($"Some registry settings for {packageName} could not be applied");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logService.LogError($"Error applying registry settings for {packageName}", ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error removing app {packageName}", ex);
                    results.Add((packageName, false, $"Error: {ex.Message}"));
                }
            }
            
            return results;
        }
        catch (Exception ex)
        {
            _logService.LogError("Error removing standard apps", ex);
            return packageNames.Select(p => (p, false, $"Error: {ex.Message}")).ToList();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ApplyRegistrySettingsAsync(List<AppRegistrySetting> settings)
    {
        try
        {
            _logService.LogInformation($"Applying {settings.Count} registry settings");
            
            using var powerShell = PowerShellFactory.CreateWindowsPowerShell(_logService, _systemServices);
            // No need to set execution policy as it's already done in the factory
            
            bool allSucceeded = true;
            
            foreach (var setting in settings)
            {
                try
                {
                    _logService.LogInformation($"Applying registry setting: {setting.Path}\\{setting.Name}");
                    
                    powerShell.Commands.Clear();
                    if (setting.Value == null)
                    {
                        // If value is null, use a script to delete the registry value
                        _logService.LogInformation($"Deleting registry value: {setting.Path}\\{setting.Name}");
                        powerShell.AddScript(@"
                            param($path, $name)
                            try {
                                # Check if the registry key exists
                                if (Test-Path $path) {
                                    # Check if the registry value exists
                                    $item = Get-Item -Path $path -ErrorAction SilentlyContinue
                                    if ($item -and ($item.GetValueNames() -contains $name)) {
                                        # Delete the registry value
                                        Remove-ItemProperty -Path $path -Name $name -Force -ErrorAction Stop
                                        Write-Output $true
                                    }
                                    else {
                                        # Value doesn't exist, consider it a success
                                        Write-Output $true
                                    }
                                }
                                else {
                                    # Key doesn't exist, consider it a success
                                    Write-Output $true
                                }
                            }
                            catch {
                                Write-Error ""Failed to delete registry value""
                                Write-Output $false
                            }
                        ");
                        powerShell.AddParameter("path", setting.Path);
                        powerShell.AddParameter("name", setting.Name);
                    }
                    else
                    {
                        // If value is not null, use a script to set the registry value
                        powerShell.AddScript(@"
                            param($path, $name, $value, $valueKind)
                            try {
                                # Create the registry key if it doesn't exist
                                if (-not (Test-Path $path)) {
                                    New-Item -Path $path -Force | Out-Null
                                }
                                
                                # Set the registry value
                                Set-ItemProperty -Path $path -Name $name -Value $value -Type $valueKind -Force
                                return $true
                            }
                            catch {
                                return $false
                            }
                        ");
                        powerShell.AddParameter("path", setting.Path);
                        powerShell.AddParameter("name", setting.Name);
                        powerShell.AddParameter("value", setting.Value);
                        powerShell.AddParameter("valueKind", setting.ValueKind.ToString());
                    }
                    
                    var result = await Task.Run(() => powerShell.Invoke<bool>());
                    var settingSuccess = result.FirstOrDefault();
                    
                    if (!settingSuccess)
                    {
                        _logService.LogError($"Failed to apply registry setting: {setting.Path}\\{setting.Name}");
                        allSucceeded = false;
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error applying registry setting: {setting.Path}\\{setting.Name}", ex);
                    allSucceeded = false;
                }
            }
            
            return allSucceeded;
        }
        catch (Exception ex)
        {
            _logService.LogError("Error applying registry settings", ex);
            return false;
        }
    }

    // SetExecutionPolicy is now handled by PowerShellFactory
}
