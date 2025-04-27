using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>
/// Service for discovering and querying applications on the system.
/// </summary>
public class AppDiscoveryService : Winhance.Core.Features.SoftwareApps.Interfaces.IAppDiscoveryService
{
    private readonly ILogService _logService;
    private readonly ExternalAppCatalog _externalAppCatalog;
    private readonly WindowsAppCatalog _windowsAppCatalog;
    private readonly CapabilityCatalog _capabilityCatalog;
    private readonly FeatureCatalog _featureCatalog;
    private readonly TimeSpan _powershellTimeout = TimeSpan.FromSeconds(10); // Add a timeout for PowerShell commands
    private Dictionary<string, bool> _installationStatusCache = new Dictionary<string, bool>(
        StringComparer.OrdinalIgnoreCase
    );
    private DateTime _lastCacheRefresh = DateTime.MinValue;
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(5);
    private readonly object _cacheLock = new object();

    /// <summary>
    /// Initializes a new instance of the <see cref="AppDiscoveryService"/> class.
    /// </summary>
    /// <param name="logService">The logging service.</param>
    public AppDiscoveryService(ILogService logService)
    {
        _logService = logService;
        _externalAppCatalog = ExternalAppCatalog.CreateDefault();
        _windowsAppCatalog = WindowsAppCatalog.CreateDefault();
        _capabilityCatalog = CapabilityCatalog.CreateDefault();
        _featureCatalog = FeatureCatalog.CreateDefault();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<AppInfo>> GetInstallableAppsAsync()
    {
        try
        {
            _logService.LogInformation("Getting installable external apps list");
            var installableApps = _externalAppCatalog.ExternalApps.ToList();

            // Return the apps without checking installation status initially
            // This will allow the UI to load faster
            _logService.LogInformation($"Found {installableApps.Count} installable external apps");
            return installableApps;
        }
        catch (Exception ex)
        {
            _logService.LogError("Error retrieving installable external apps", ex);
            return new List<AppInfo>();
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<AppInfo>> GetStandardAppsAsync()
    {
        try
        {
            _logService.LogInformation("Getting standard Windows apps list");
            var standardApps = _windowsAppCatalog.WindowsApps.ToList();

            // Return the apps without checking installation status initially
            // This will allow the UI to load faster
            _logService.LogInformation($"Found {standardApps.Count} standard Windows apps");
            return standardApps;
        }
        catch (Exception ex)
        {
            _logService.LogError("Error retrieving standard Windows apps", ex);
            return new List<AppInfo>();
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<CapabilityInfo>> GetCapabilitiesAsync()
    {
        try
        {
            _logService.LogInformation("Getting Windows capabilities list");
            var capabilities = _capabilityCatalog.Capabilities.ToList();

            // Return the capabilities without checking installation status initially
            // This will allow the UI to load faster
            _logService.LogInformation($"Found {capabilities.Count} capabilities");
            return capabilities;
        }
        catch (Exception ex)
        {
            _logService.LogError("Error retrieving capabilities", ex);
            return new List<CapabilityInfo>();
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<FeatureInfo>> GetOptionalFeaturesAsync()
    {
        try
        {
            _logService.LogInformation("Getting Windows optional features list");
            var features = _featureCatalog.Features.ToList();

            // Return the features without checking installation status initially
            // This will allow the UI to load faster
            _logService.LogInformation($"Found {features.Count} optional features");
            return features;
        }
        catch (Exception ex)
        {
            _logService.LogError("Error retrieving optional features", ex);
            return new List<FeatureInfo>();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsAppInstalledAsync(string packageName)
    {
        return await IsAppInstalledAsync(packageName, CancellationToken.None);
    }

    /// <inheritdoc/>
    public async Task<bool> IsAppInstalledAsync(string packageName, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check cache first
            lock (_cacheLock)
            {
                if (_installationStatusCache.TryGetValue(packageName, out bool cachedStatus))
                {
                    // Only use cache if it's fresh
                    if (DateTime.Now - _lastCacheRefresh < _cacheLifetime)
                    {
                        return cachedStatus;
                    }
                }
            }

            // Determine item type more efficiently by using a type flag in the item itself
            // This avoids searching through collections each time
            bool isCapability = _capabilityCatalog.Capabilities.Any(c =>
                c.PackageName.Equals(packageName, StringComparison.OrdinalIgnoreCase)
            );
            bool isFeature =
                !isCapability
                && _featureCatalog.Features.Any(f =>
                    f.PackageName.Equals(packageName, StringComparison.OrdinalIgnoreCase)
                );

            bool isInstalled = false;

            // Check if this app has subpackages
            string[]? subPackages = null;
            if (!isCapability && !isFeature)
            {
                // Find the app definition to check for subpackages
                var appDefinition = _windowsAppCatalog.WindowsApps.FirstOrDefault(a =>
                    a.PackageName.Equals(packageName, StringComparison.OrdinalIgnoreCase));
                
                if (appDefinition?.SubPackages != null && appDefinition.SubPackages.Length > 0)
                {
                    subPackages = appDefinition.SubPackages;
                    _logService.LogInformation($"App {packageName} has {subPackages.Length} subpackages");
                }
            }

            using var powerShell = PowerShellFactory.CreateWindowsPowerShell(_logService);
            // No need to set execution policy as it's already done in the factory

            if (isCapability)
            {
                // Check capability status
                powerShell.AddScript(
                    @"
                param($capabilityName)
                try {
                    $capability = Get-WindowsCapability -Online |
                        Where-Object { $_.Name -like ""$capabilityName*"" } |
                        Select-Object -First 1
                    return $capability -ne $null -and $capability.State -eq 'Installed'
                }
                catch {
                    return $false
                }
            "
                );
                powerShell.AddParameter("capabilityName", packageName);
            }
            else if (isFeature)
            {
                // Check feature status
                powerShell.AddScript(
                    @"
                param($featureName)
                try {
                    $feature = Get-WindowsOptionalFeature -Online |
                        Where-Object { $_.FeatureName -eq $featureName }
                    return $feature -ne $null -and $feature.State -eq 'Enabled'
                }
                catch {
                    return $false
                }
            "
                );
                powerShell.AddParameter("featureName", packageName);
            }
            else
            {
                // Check standard app status using the same approach as the original PowerShell script
                // But also check subpackages if the main package is not installed
                powerShell.AddScript(
                    @"
                param($packageName, $subPackages)
                try {
                    # Check main package
                    $appx = Get-AppxPackage -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq $packageName }
                    $isInstalled = ($appx -ne $null)
                    Write-Output ""Checking if $packageName is installed: $isInstalled""
                    
                    # If not installed and we have subpackages, check those too
                    if (-not $isInstalled -and $subPackages -ne $null) {
                        foreach ($subPackage in $subPackages) {
                            Write-Output ""Checking subpackage: $subPackage""
                            $subAppx = Get-AppxPackage -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq $subPackage }
                            if ($subAppx -ne $null) {
                                $isInstalled = $true
                                Write-Output ""Subpackage $subPackage is installed""
                                break
                            }
                        }
                    }
                    
                    return $isInstalled
                }
                catch {
                    Write-Output ""Error checking if $packageName is installed: $_""
                    return $false
                }
            "
                );
                powerShell.AddParameter("packageName", packageName);
                powerShell.AddParameter("subPackages", subPackages);
            }

            // Execute with timeout
            var task = Task.Run(() => powerShell.Invoke<bool>());
            if (await Task.WhenAny(task, Task.Delay(_powershellTimeout)) == task)
            {
                var result = await task;
                isInstalled = result.FirstOrDefault();
            }
            else
            {
                _logService.LogWarning($"Timeout checking installation status for {packageName}");
                isInstalled = false;
            }

            // Cache the result
            lock (_cacheLock)
            {
                _installationStatusCache[packageName] = isInstalled;
                _lastCacheRefresh = DateTime.Now;
            }

            return isInstalled;
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error checking if item is installed: {packageName}", ex);
            return false;
        }
    }

    /// <summary>
    /// Efficiently checks installation status for multiple items at once.
    /// </summary>
    /// <param name="packageNames">The package names to check.</param>
    /// <returns>A dictionary mapping package names to their installation status.</returns>
    public async Task<Dictionary<string, bool>> GetInstallationStatusBatchAsync(
        IEnumerable<string> packageNames
    )
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var packageList = packageNames.ToList();

        if (!packageList.Any())
            return result;

        try
        {
            // Group items by type for batch processing
            var capabilities = packageList
                .Where(p =>
                    _capabilityCatalog.Capabilities.Any(c =>
                        c.PackageName.Equals(p, StringComparison.OrdinalIgnoreCase)
                    )
                )
                .ToList();

            var features = packageList
                .Where(p =>
                    _featureCatalog.Features.Any(f =>
                        f.PackageName.Equals(p, StringComparison.OrdinalIgnoreCase)
                    )
                )
                .ToList();

            var standardApps = packageList.Except(capabilities).Except(features).ToList();

            // Process capabilities in batch
            if (capabilities.Any())
            {
                var capabilityStatuses = await GetCapabilitiesStatusBatchAsync(capabilities);
                foreach (var pair in capabilityStatuses)
                {
                    result[pair.Key] = pair.Value;
                }
            }

            // Process features in batch
            if (features.Any())
            {
                var featureStatuses = await GetFeaturesStatusBatchAsync(features);
                foreach (var pair in featureStatuses)
                {
                    result[pair.Key] = pair.Value;
                }
            }

            // Process standard apps in batch
            if (standardApps.Any())
            {
                var appStatuses = await GetStandardAppsStatusBatchAsync(standardApps);
                foreach (var pair in appStatuses)
                {
                    result[pair.Key] = pair.Value;
                }
            }

            // Cache all results
            lock (_cacheLock)
            {
                foreach (var pair in result)
                {
                    _installationStatusCache[pair.Key] = pair.Value;
                }
                _lastCacheRefresh = DateTime.Now;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logService.LogError("Error checking batch installation status", ex);
            return packageList.ToDictionary(p => p, p => false, StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<Dictionary<string, bool>> GetCapabilitiesStatusBatchAsync(
        List<string> capabilities
    )
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        using var powerShell = PowerShellFactory.CreateWindowsPowerShell(_logService);
        // No need to set execution policy as it's already done in the factory

        // Get all installed capabilities in one query
        powerShell.AddScript(
            @"
        try {
            $installedCapabilities = Get-WindowsCapability -Online | Where-Object { $_.State -eq 'Installed' }
            return $installedCapabilities | Select-Object -ExpandProperty Name
        }
        catch {
            return @()
        }
    "
        );

        var task = Task.Run(() => powerShell.Invoke<string>());
        if (await Task.WhenAny(task, Task.Delay(_powershellTimeout)) == task)
        {
            var installedCapabilities = (await task).ToList();

            // Check each capability against the installed list
            foreach (var capability in capabilities)
            {
                result[capability] = installedCapabilities.Any(c =>
                    c.StartsWith(capability, StringComparison.OrdinalIgnoreCase)
                );
            }
        }
        else
        {
            _logService.LogWarning("Timeout getting installed capabilities");
            foreach (var capability in capabilities)
            {
                result[capability] = false;
            }
        }

        return result;
    }

    private async Task<Dictionary<string, bool>> GetFeaturesStatusBatchAsync(List<string> features)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        using var powerShell = PowerShellFactory.CreateWindowsPowerShell(_logService);
        // No need to set execution policy as it's already done in the factory

        // Get all enabled features in one query
        powerShell.AddScript(
            @"
        try {
            $enabledFeatures = Get-WindowsOptionalFeature -Online | Where-Object { $_.State -eq 'Enabled' }
            return $enabledFeatures | Select-Object -ExpandProperty FeatureName
        }
        catch {
            return @()
        }
    "
        );

        var task = Task.Run(() => powerShell.Invoke<string>());
        if (await Task.WhenAny(task, Task.Delay(_powershellTimeout)) == task)
        {
            var enabledFeatures = (await task).ToList();

            // Check each feature against the enabled list
            foreach (var feature in features)
            {
                result[feature] = enabledFeatures.Any(f =>
                    f.Equals(feature, StringComparison.OrdinalIgnoreCase)
                );
            }
        }
        else
        {
            _logService.LogWarning("Timeout getting enabled features");
            foreach (var feature in features)
            {
                result[feature] = false;
            }
        }

        return result;
    }

    private async Task<Dictionary<string, bool>> GetStandardAppsStatusBatchAsync(List<string> apps)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        using var powerShell = PowerShellFactory.CreateWindowsPowerShell(_logService);
        // No need to set execution policy as it's already done in the factory

        // Get all installed apps in one query using the same approach as the original PowerShell script
        powerShell.AddScript(
            @"
        try {
            $installedApps = @(Get-AppxPackage | Select-Object -ExpandProperty Name)
            
            # Log the count for diagnostic purposes
            Write-Output ""Found $($installedApps.Count) installed apps""
            
            # Return the array of names
            return $installedApps
        }
        catch {
            Write-Output ""Error getting installed apps: $_""
            return @()
        }
    "
        );

        var task = Task.Run(() => powerShell.Invoke<string>());
        if (await Task.WhenAny(task, Task.Delay(_powershellTimeout)) == task)
        {
            var installedApps = (await task).ToList();

            // Check each app against the installed list
            foreach (var app in apps)
            {
                // First check if the main package is installed
                bool isInstalled = installedApps.Any(a =>
                    a.Equals(app, StringComparison.OrdinalIgnoreCase)
                );
                
                // If not installed, check if it has subpackages and if any of them are installed
                if (!isInstalled)
                {
                    var appDefinition = _windowsAppCatalog.WindowsApps.FirstOrDefault(a =>
                        a.PackageName.Equals(app, StringComparison.OrdinalIgnoreCase));
                    
                    if (appDefinition?.SubPackages != null && appDefinition.SubPackages.Length > 0)
                    {
                        foreach (var subPackage in appDefinition.SubPackages)
                        {
                            if (installedApps.Any(a => a.Equals(subPackage, StringComparison.OrdinalIgnoreCase)))
                            {
                                isInstalled = true;
                                _logService.LogInformation($"App {app} is installed via subpackage {subPackage}");
                                break;
                            }
                        }
                    }
                }
                
                result[app] = isInstalled;
            }
        }
        else
        {
            _logService.LogWarning("Timeout getting installed apps");
            foreach (var app in apps)
            {
                result[app] = false;
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<bool> IsEdgeInstalledAsync()
    {
        try
        {
            _logService.LogInformation("Checking if Microsoft Edge is installed");
            
            using var powerShell = PowerShellFactory.CreateWindowsPowerShell(_logService);
            // No need to set execution policy as it's already done in the factory
            
            // Only check the specific registry key as requested
            powerShell.AddScript(@"
                try {
                    $result = Get-ItemProperty ""HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe"" -ErrorAction SilentlyContinue
                    return $result -ne $null
                }
                catch {
                    Write-Output ""Error checking Edge registry key: $($_.Exception.Message)""
                    return $false
                }
            ");
            
            var result = await ExecuteWithTimeoutAsync<bool>(powerShell, 15);
            bool isInstalled = result.FirstOrDefault();
            
            _logService.LogInformation($"Edge registry check result: {isInstalled}");
            return isInstalled;
        }
        catch (Exception ex)
        {
            _logService.LogError("Error checking Edge installation", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsOneDriveInstalledAsync()
    {
        try
        {
            _logService.LogInformation("Checking if OneDrive is installed");
            
            using var powerShell = PowerShellFactory.CreateWindowsPowerShell(_logService);
            // No need to set execution policy as it's already done in the factory
            
            // Check the specific registry keys
            powerShell.AddScript(@"
                try {
                    # Check the two specific registry keys
                    $hklmKey = Get-ItemProperty ""HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe"" -ErrorAction SilentlyContinue
                    $hkcuKey = Get-ItemProperty ""HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe"" -ErrorAction SilentlyContinue
                    
                    # Return true if either key exists
                    return ($hklmKey -ne $null) -or ($hkcuKey -ne $null)
                }
                catch {
                    Write-Output ""Error checking OneDrive registry: $($_.Exception.Message)""
                    return $false
                }
            ");
            
            var result = await ExecuteWithTimeoutAsync<bool>(powerShell, 15);
            bool isInstalled = result.FirstOrDefault();
            
            _logService.LogInformation($"OneDrive registry check result: {isInstalled}");
            return isInstalled;
        }
        catch (Exception ex)
        {
            _logService.LogError("Error checking OneDrive installation", ex);
            return false;
        }
    }

    // SetExecutionPolicy is now handled by PowerShellFactory

    /// <summary>
    /// Clears the installation status cache, forcing fresh checks on next query.
    /// Call this after installing or removing items.
    /// </summary>
    public void ClearInstallationStatusCache()
    {
        lock (_cacheLock)
        {
            _installationStatusCache.Clear();
        }
    }
    
    /// <summary>
    /// Executes a PowerShell command with a timeout.
    /// </summary>
    /// <typeparam name="T">The type of result to return.</typeparam>
    /// <param name="powerShell">The PowerShell instance.</param>
    /// <param name="timeoutSeconds">The timeout in seconds.</param>
    /// <returns>The result of the PowerShell command.</returns>
    private async Task<System.Collections.ObjectModel.Collection<T>> ExecuteWithTimeoutAsync<T>(PowerShell powerShell, int timeoutSeconds)
    {
        try
        {
            var task = Task.Run(() => powerShell.Invoke<T>());
            if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds))) == task)
            {
                return await task;
            }
            
            _logService.LogWarning($"PowerShell execution timed out after {timeoutSeconds} seconds");
            return new System.Collections.ObjectModel.Collection<T>();
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error executing PowerShell command with timeout: {ex.Message}", ex);
            return new System.Collections.ObjectModel.Collection<T>();
        }
    }
}
