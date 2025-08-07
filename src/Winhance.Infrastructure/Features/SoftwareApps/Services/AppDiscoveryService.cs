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


namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>
/// Service for discovering and querying applications on the system.
/// </summary>
public class AppDiscoveryService
    : Winhance.Core.Features.SoftwareApps.Interfaces.IAppDiscoveryService
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

    private readonly IPowerShellDetectionService _powerShellDetectionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppDiscoveryService"/> class.
    /// </summary>
    /// <param name="logService">The logging service.</param>
    /// <param name="powerShellDetectionService">The PowerShell detection service.</param>
    public AppDiscoveryService(ILogService logService, IPowerShellDetectionService powerShellDetectionService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _powerShellDetectionService = powerShellDetectionService ?? throw new ArgumentNullException(nameof(powerShellDetectionService));
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
    public async Task<bool> IsAppInstalledAsync(
        string packageName,
        CancellationToken cancellationToken = default
    )
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
                    a.PackageName.Equals(packageName, StringComparison.OrdinalIgnoreCase)
                );

                if (appDefinition?.SubPackages != null && appDefinition.SubPackages.Length > 0)
                {
                    subPackages = appDefinition.SubPackages;
                    _logService.LogInformation(
                        $"App {packageName} has {subPackages.Length} subpackages"
                    );
                }
            }

            using var powerShell = CreatePowerShell();
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
                using var powerShellForAppx = CreatePowerShell();
                powerShellForAppx.AddScript(
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
                powerShellForAppx.AddParameter("packageName", packageName);
                powerShellForAppx.AddParameter("subPackages", subPackages);

                // Execute with timeout
                var task = Task.Run(() => powerShellForAppx.Invoke<bool>());
                if (await Task.WhenAny(task, Task.Delay(_powershellTimeout)) == task)
                {
                    var result = await task;
                    isInstalled = result.FirstOrDefault();
                }
                else
                {
                    _logService.LogWarning(
                        $"Timeout checking installation status for {packageName}"
                    );
                    isInstalled = false;
                }
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
            // Check for special apps that need custom detection logic
            var oneNotePackage = "Microsoft.Office.OneNote";
            bool hasOneNote = packageList.Any(p =>
                p.Equals(oneNotePackage, StringComparison.OrdinalIgnoreCase)
            );
            bool oneNoteInstalled = false;

            // If OneNote is in the list, check it separately using our special detection
            if (hasOneNote)
            {
                oneNoteInstalled = await IsOneNoteInstalledAsync();
                _logService.LogInformation(
                    $"OneNote special registry check result: {oneNoteInstalled}"
                );
                // Remove OneNote from the list as we'll handle it separately
                packageList = packageList
                    .Where(p => !p.Equals(oneNotePackage, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

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

            // Add OneNote result if it was in the original list
            if (hasOneNote)
            {
                // Check if OneNote is also detected via AppX package
                bool appxOneNoteInstalled = false;
                if (result.TryGetValue(oneNotePackage, out bool appxInstalled))
                {
                    appxOneNoteInstalled = appxInstalled;
                }

                // Use either the registry check or AppX check - if either is true, OneNote is installed
                result[oneNotePackage] = oneNoteInstalled || appxOneNoteInstalled;
                _logService.LogInformation(
                    $"Final OneNote installation status: {result[oneNotePackage]} (Registry: {oneNoteInstalled}, AppX: {appxOneNoteInstalled})"
                );
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

        using var powerShell = CreatePowerShell();
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

        using var powerShell = CreatePowerShell();
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

        using var powerShell = CreatePowerShell();
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
                        a.PackageName.Equals(app, StringComparison.OrdinalIgnoreCase)
                    );

                    if (appDefinition?.SubPackages != null && appDefinition.SubPackages.Length > 0)
                    {
                        foreach (var subPackage in appDefinition.SubPackages)
                        {
                            if (
                                installedApps.Any(a =>
                                    a.Equals(subPackage, StringComparison.OrdinalIgnoreCase)
                                )
                            )
                            {
                                isInstalled = true;
                                _logService.LogInformation(
                                    $"App {app} is installed via subpackage {subPackage}"
                                );
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

            using var powerShell = CreatePowerShell();
            // No need to set execution policy as it's already done in the factory

            // Only check the specific registry key as requested
            powerShell.AddScript(
                @"
                try {
                    $result = Get-ItemProperty ""HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe"" -ErrorAction SilentlyContinue
                    return $result -ne $null
                }
                catch {
                    Write-Output ""Error checking Edge registry key: $($_.Exception.Message)""
                    return $false
                }
            "
            );

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

            using var powerShell = CreatePowerShell();
            // No need to set execution policy as it's already done in the factory

            // Check the specific registry keys
            powerShell.AddScript(
                @"
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
            "
            );

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

    /// <inheritdoc/>
    public async Task<bool> IsOneNoteInstalledAsync()
    {
        try
        {
            _logService.LogInformation("Checking if OneNote is installed");

            // First, try a simpler, more direct approach without recursive searches
            using var powerShell = CreatePowerShell();

            // Use a more targeted, non-recursive approach for better performance and reliability
            powerShell.AddScript(
                @"
                try {
                # Check for OneNote in common registry locations using direct paths instead of recursive searches
                
                # Check standard uninstall keys
                $installed = $false
                
                # Check for OneNote in standard uninstall locations
                $hklmKeys = Get-ChildItem ""HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"" -ErrorAction SilentlyContinue | 
                    Get-ItemProperty -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -like ""*OneNote*"" }
                if ($hklmKeys) { $installed = $true }
                
                $hkcuKeys = Get-ChildItem ""HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"" -ErrorAction SilentlyContinue | 
                    Get-ItemProperty -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -like ""*OneNote*"" }
                if ($hkcuKeys) { $installed = $true }
                
                # Check for Wow6432Node registry keys (for 32-bit apps on 64-bit Windows)
                $hklmWowKeys = Get-ChildItem ""HKLM:\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall"" -ErrorAction SilentlyContinue | 
                    Get-ItemProperty -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -like ""*OneNote*"" }
                if ($hklmWowKeys) { $installed = $true }
                
                # Check for UWP/Store app version
                try {
                    $appxPackage = Get-AppxPackage -Name Microsoft.Office.OneNote -ErrorAction SilentlyContinue
                    if ($appxPackage) { $installed = $true }
                } catch {
                    # Ignore errors with AppX commands
                }
                
                return $installed
                }
                catch {
                    Write-Output ""Error checking OneNote registry: $($_.Exception.Message)""
                    return $false
                }
            "
            );

            // Use a shorter timeout to prevent hanging
            var result = await ExecuteWithTimeoutAsync<bool>(powerShell, 10);
            bool isInstalled = result.FirstOrDefault();

            _logService.LogInformation($"OneNote registry check result: {isInstalled}");
            return isInstalled;
        }
        catch (Exception ex)
        {
            _logService.LogError("Error checking OneNote installation", ex);
            // Don't crash the application if OneNote check fails
            return false;
        }
    }

    // PowerShell configuration is handled by the detection service

    /// <summary>
    /// Creates a new PowerShell instance based on the detected PowerShell environment.
    /// </summary>
    /// <returns>A new PowerShell instance configured for the detected environment.</returns>
    private PowerShell CreatePowerShell()
    {
        var powerShellInfo = _powerShellDetectionService.GetPowerShellInfo();
        var powerShell = PowerShell.Create();
        
        // The detection service has already handled any necessary configuration
        _logService.LogInformation($"Created PowerShell instance using: {powerShellInfo.PowerShellPath}");
        
        return powerShell;
    }

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

    /// <inheritdoc/>
    public async Task RefreshAppInstallationStatusAsync()
    {
        try
        {
            _logService.LogInformation("Refreshing installation status for all apps, capabilities, and features");
            
            // Clear the existing cache
            ClearInstallationStatusCache();
            
            // Get all apps, capabilities, and features
            var standardApps = (await GetStandardAppsAsync()).ToList();
            var installableApps = (await GetInstallableAppsAsync()).ToList();
            var capabilities = (await GetCapabilitiesAsync()).ToList();
            var features = (await GetOptionalFeaturesAsync()).ToList();
            
            // Collect all package names
            var packageNames = new List<string>();
            packageNames.AddRange(standardApps.Select(a => a.PackageName));
            packageNames.AddRange(installableApps.Select(a => a.PackageName));
            packageNames.AddRange(capabilities.Select(c => c.PackageName));
            packageNames.AddRange(features.Select(f => f.PackageName));
            
            // Check installation status in batch
            await GetInstallationStatusBatchAsync(packageNames.Distinct());
            
            // Also refresh special apps
            await IsEdgeInstalledAsync();
            await IsOneDriveInstalledAsync();
            await IsOneNoteInstalledAsync();
            
            _logService.LogInformation("Successfully refreshed installation status for all items");
        }
        catch (Exception ex)
        {
            _logService.LogError("Error refreshing installation status", ex);
        }
    }

    /// <summary>
    /// Executes a PowerShell command with a timeout.
    /// </summary>
    /// <typeparam name="T">The type of result to return.</typeparam>
    /// <param name="powerShell">The PowerShell instance.</param>
    /// <param name="timeoutSeconds">The timeout in seconds.</param>
    /// <returns>The result of the PowerShell command.</returns>
    private async Task<System.Collections.ObjectModel.Collection<T>> ExecuteWithTimeoutAsync<T>(
        PowerShell powerShell,
        int timeoutSeconds
    )
    {
        // Create a cancellation token source for the timeout
        using var cancellationTokenSource = new CancellationTokenSource();

        try
        {
            // Set up the timeout
            cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            // Run the PowerShell command with cancellation support
            var task = Task.Run(
                () =>
                {
                    try
                    {
                        // Check if cancellation was requested before starting
                        if (cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            _logService.LogWarning(
                                "PowerShell execution cancelled before starting"
                            );
                            return new System.Collections.ObjectModel.Collection<T>();
                        }

                        // Execute the PowerShell command
                        return powerShell.Invoke<T>();
                    }
                    catch (Exception innerEx)
                    {
                        _logService.LogError(
                            $"Error in PowerShell execution thread: {innerEx.Message}",
                            innerEx
                        );
                        return new System.Collections.ObjectModel.Collection<T>();
                    }
                },
                cancellationTokenSource.Token
            );

            // Wait for completion or timeout
            if (
                await Task.WhenAny(
                    task,
                    Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), cancellationTokenSource.Token)
                ) == task
            )
            {
                // Task completed within timeout
                if (task.IsCompletedSuccessfully)
                {
                    return await task;
                }
                else if (task.IsFaulted)
                {
                    _logService.LogError(
                        $"PowerShell task faulted: {task.Exception?.Message}",
                        task.Exception
                    );
                }
                else if (task.IsCanceled)
                {
                    _logService.LogWarning("PowerShell task was cancelled");
                }
            }
            else
            {
                // Task timed out, attempt to cancel it
                _logService.LogWarning(
                    $"PowerShell execution timed out after {timeoutSeconds} seconds"
                );
                cancellationTokenSource.Cancel();

                // Try to stop the PowerShell pipeline if it's still running
                try
                {
                    powerShell.Stop();
                }
                catch (Exception stopEx)
                {
                    _logService.LogWarning($"Error stopping PowerShell pipeline: {stopEx.Message}");
                }
            }

            // Return empty collection if we reached here (timeout or error)
            return new System.Collections.ObjectModel.Collection<T>();
        }
        catch (OperationCanceledException)
        {
            _logService.LogWarning(
                $"PowerShell execution cancelled after {timeoutSeconds} seconds"
            );
            return new System.Collections.ObjectModel.Collection<T>();
        }
        catch (Exception ex)
        {
            _logService.LogError(
                $"Error executing PowerShell command with timeout: {ex.Message}",
                ex
            );
            return new System.Collections.ObjectModel.Collection<T>();
        }
    }
}
