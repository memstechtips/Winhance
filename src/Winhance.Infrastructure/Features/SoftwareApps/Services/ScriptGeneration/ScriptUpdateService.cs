using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.ScriptGeneration;

/// <summary>
/// Implementation of IScriptUpdateService that provides methods for updating script content.
/// </summary>
public class ScriptUpdateService : IScriptUpdateService
{
    private readonly ILogService _logService;
    private readonly IAppDiscoveryService _appDiscoveryService;
    private readonly IBloatRemovalScriptContentModifier _bloatRemovalScriptContentModifier;
    private readonly IBloatRemovalScriptTemplateProvider _bloatRemovalScriptTemplateProvider;
    private readonly IScriptPathDetectionService _scriptPathDetectionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptUpdateService"/> class.
    /// </summary>
    /// <param name="logService">The logging service.</param>
    /// <param name="appDiscoveryService">The app discovery service.</param>
    /// <param name="bloatRemovalScriptContentModifier">The script content modifier.</param>
    /// <param name="bloatRemovalScriptTemplateProvider">The script template provider.</param>
    /// <param name="scriptPathDetectionService">The script path detection service.</param>
    public ScriptUpdateService(
        ILogService logService,
        IAppDiscoveryService appDiscoveryService,
        IBloatRemovalScriptContentModifier bloatRemovalScriptContentModifier,
        IBloatRemovalScriptTemplateProvider bloatRemovalScriptTemplateProvider,
        IScriptPathDetectionService scriptPathDetectionService
    )
    {
        _logService = logService;
        _appDiscoveryService = appDiscoveryService;
        _bloatRemovalScriptContentModifier = bloatRemovalScriptContentModifier;
        _bloatRemovalScriptTemplateProvider =
            bloatRemovalScriptTemplateProvider
            ?? throw new ArgumentNullException(nameof(bloatRemovalScriptTemplateProvider));
        _scriptPathDetectionService = scriptPathDetectionService ?? throw new ArgumentNullException(nameof(scriptPathDetectionService));
    }

    /// <inheritdoc/>
    public async Task<RemovalScript> UpdateExistingBloatRemovalScriptAsync(
        List<string> appNames,
        Dictionary<string, List<AppRegistrySetting>> appsWithRegistry,
        Dictionary<string, string[]> appSubPackages,
        bool isInstallOperation = false
    )
    {
        try
        {
            string bloatRemovalScriptPath = Path.Combine(_scriptPathDetectionService.GetScriptsDirectory(), "BloatRemoval.ps1");

            _logService.LogInformation(
                $"Checking for BloatRemoval.ps1 at path: {bloatRemovalScriptPath}"
            );

            string scriptContent;

            if (!File.Exists(bloatRemovalScriptPath))
            {
                _logService.LogWarning(
                    $"BloatRemoval.ps1 file not found at: {bloatRemovalScriptPath}"
                );

                // Create the directory if it doesn't exist
                string scriptDirectory = Path.GetDirectoryName(bloatRemovalScriptPath);
                if (!Directory.Exists(scriptDirectory))
                {
                    _logService.LogInformation($"Creating directory: {scriptDirectory}");
                    Directory.CreateDirectory(scriptDirectory);
                }

                // Create a basic BloatRemoval.ps1 file if it doesn't exist
                _logService.LogInformation("Creating a new BloatRemoval.ps1 file");

                // Get the full script template from the template provider
                string basicScriptContent =
                    _bloatRemovalScriptTemplateProvider.GetFullScriptTemplate();

                await File.WriteAllTextAsync(bloatRemovalScriptPath, basicScriptContent);
                scriptContent = basicScriptContent;
                _logService.LogInformation("Created new BloatRemoval.ps1 file");
            }
            else
            {
                _logService.LogInformation("BloatRemoval.ps1 file found, reading content");
                scriptContent = await File.ReadAllTextAsync(bloatRemovalScriptPath);
            }

            // Separate capabilities, packages, and optional features
            var capabilities = new List<string>();
            var packages = new List<string>();
            var optionalFeatures = new List<string>();

            foreach (var appName in appNames)
            {
                if (
                    !appName.Equals("Edge", StringComparison.OrdinalIgnoreCase)
                    && !appName.Equals("OneDrive", StringComparison.OrdinalIgnoreCase)
                )
                {
                    // Check if this is an OptionalFeature
                    bool isOptionalFeature = false;
                    bool isCapability = false;

                    // Get app info from the catalog
                    var allRemovableApps = (
                        await _appDiscoveryService.GetStandardAppsAsync()
                    ).ToList();
                    var appInfo = allRemovableApps.FirstOrDefault(a =>
                        a.PackageName.Equals(appName, StringComparison.OrdinalIgnoreCase)
                    );

                    if (appInfo != null)
                    {
                        isOptionalFeature = appInfo.Type == AppType.OptionalFeature;
                        isCapability = appInfo.Type == AppType.Capability;
                    }

                    // Check if this app has registry settings
                    if (appsWithRegistry.TryGetValue(appName, out var registrySettings))
                    {
                        // Look for a special metadata registry setting that indicates if this is a capability
                        var capabilitySetting = registrySettings.FirstOrDefault(s =>
                            s.Name.Equals("IsCapability", StringComparison.OrdinalIgnoreCase)
                        );

                        if (
                            capabilitySetting != null
                            && capabilitySetting.Value is bool capabilityValue
                        )
                        {
                            isCapability = capabilityValue;
                        }
                    }

                    // Extract the base name without version for capability detection
                    string baseAppName = appName;
                    if (appName.Contains("~~~~"))
                    {
                        // Extract the base name before the version (~~~~)
                        baseAppName = appName.Split('~')[0];
                        _logService.LogInformation(
                            $"Extracted base capability name '{baseAppName}' from '{appName}'"
                        );
                    }

                    // Check if this is a known Windows optional feature
                    if (
                        appName.Equals("Recall", StringComparison.OrdinalIgnoreCase)
                        || appName.Equals("NetFx3", StringComparison.OrdinalIgnoreCase)
                        || appName.Equals(
                            "Microsoft-Hyper-V-All",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || appName.Equals(
                            "Microsoft-Hyper-V-Tools-All",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || appName.Equals(
                            "Microsoft-Hyper-V-Hypervisor",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || appName.Equals(
                            "Microsoft-Windows-Subsystem-Linux",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || appName.Equals(
                            "MicrosoftCorporationII.WindowsSubsystemForAndroid",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || appName.Equals(
                            "Containers-DisposableClientVM",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || isOptionalFeature
                    )
                    {
                        _logService.LogInformation($"Adding {appName} to optional features array");
                        optionalFeatures.Add(appName);
                        continue;
                    }

                    // Known capabilities have specific formats
                    if (
                        isCapability
                        || baseAppName.Equals(
                            "Browser.InternetExplorer",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || baseAppName.Equals("MathRecognizer", StringComparison.OrdinalIgnoreCase)
                        || baseAppName.Equals("OpenSSH.Client", StringComparison.OrdinalIgnoreCase)
                        || baseAppName.Equals("OpenSSH.Server", StringComparison.OrdinalIgnoreCase)
                        || baseAppName.Equals(
                            "Microsoft.Windows.PowerShell.ISE",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || baseAppName.Equals(
                            "App.StepsRecorder",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || baseAppName.Equals(
                            "Media.WindowsMediaPlayer",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || baseAppName.Equals(
                            "App.Support.QuickAssist",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || baseAppName.Equals(
                            "Microsoft.Windows.WordPad",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || baseAppName.Equals(
                            "Microsoft.Windows.MSPaint",
                            StringComparison.OrdinalIgnoreCase
                        )
                        // Check if the app name contains version numbers in the format ~~~~0.0.1.0
                        || appName.Contains("~~~~")
                    )
                    {
                        _logService.LogInformation($"Adding {appName} to capabilities array");
                        capabilities.Add(appName);
                    }
                    else
                    {
                        // Explicitly handle Copilot and Xbox packages
                        bool isCopilotOrXbox =
                            appName.Contains("Copilot", StringComparison.OrdinalIgnoreCase)
                            || appName.Contains("Xbox", StringComparison.OrdinalIgnoreCase);

                        _logService.LogInformation($"Adding {appName} to packages array");
                        packages.Add(appName);
                    }
                }
            }

            // Process subpackages and add them to the packages list
            if (appSubPackages != null && appSubPackages.Count > 0)
            {
                _logService.LogInformation(
                    $"Processing {appSubPackages.Count} app entries with subpackages"
                );

                foreach (var packageEntry in appSubPackages)
                {
                    string parentPackage = packageEntry.Key;
                    string[] subPackages = packageEntry.Value;

                    // Only process subpackages if the parent package is in the packages list
                    // or if it's a special case like Copilot or Xbox
                    bool isSpecialApp =
                        parentPackage.Contains("Copilot", StringComparison.OrdinalIgnoreCase)
                        || parentPackage.Contains("Xbox", StringComparison.OrdinalIgnoreCase)
                        || parentPackage.Equals(
                            "Microsoft.GamingApp",
                            StringComparison.OrdinalIgnoreCase
                        );

                    if (packages.Contains(parentPackage) || isSpecialApp)
                    {
                        if (subPackages != null && subPackages.Length > 0)
                        {
                            _logService.LogInformation(
                                $"Adding {subPackages.Length} subpackages for {parentPackage}"
                            );

                            foreach (var subPackage in subPackages)
                            {
                                if (
                                    !packages.Contains(subPackage, StringComparer.OrdinalIgnoreCase)
                                )
                                {
                                    _logService.LogInformation(
                                        $"Adding subpackage: {subPackage} for {parentPackage}"
                                    );
                                    packages.Add(subPackage);
                                }
                            }
                        }
                    }
                }
            }

            // Update the script content with new entries
            if (capabilities.Count > 0)
            {
                scriptContent = UpdateCapabilitiesArrayInScript(
                    scriptContent,
                    capabilities,
                    isInstallOperation
                );
            }

            if (optionalFeatures.Count > 0)
            {
                scriptContent = UpdateOptionalFeaturesInScript(
                    scriptContent,
                    optionalFeatures,
                    isInstallOperation
                );
            }

            if (packages.Count > 0)
            {
                scriptContent = UpdatePackagesArrayInScript(
                    scriptContent,
                    packages,
                    isInstallOperation
                );
            }

            if (appsWithRegistry.Count > 0)
            {
                scriptContent = UpdateRegistrySettingsInScript(
                    scriptContent,
                    appsWithRegistry,
                    isInstallOperation
                );
            }

            // Save the updated script
            await File.WriteAllTextAsync(bloatRemovalScriptPath, scriptContent);

            // Return the updated script
            return new RemovalScript
            {
                Name = "BloatRemoval",
                Content = scriptContent,
                TargetScheduledTaskName = "Winhance\\BloatRemoval",
                RunOnStartup = true,
            };
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error updating existing BloatRemoval script: {ex.Message}", ex);
            _logService.LogError($"Stack trace: {ex.StackTrace}");

            // Create a basic error report
            try
            {
                string errorReportPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "WinhanceScriptUpdateError.txt"
                );

                string errorReport =
                    $@"
Script Update Error Report
Time: {DateTime.Now}
Error: {ex.Message}
Stack Trace: {ex.StackTrace}
Inner Exception: {ex.InnerException?.Message}

App Names: {string.Join(", ", appNames)}
Apps with Registry: {appsWithRegistry.Count}
App SubPackages: {appSubPackages.Count}
";

                await File.WriteAllTextAsync(errorReportPath, errorReport);
                _logService.LogInformation($"Error report written to: {errorReportPath}");
            }
            catch (Exception reportEx)
            {
                _logService.LogError($"Failed to write error report: {reportEx.Message}");
            }

            throw;
        }
    }

    /// <inheritdoc/>
    public string UpdateCapabilitiesArrayInScript(
        string scriptContent,
        List<string> capabilities,
        bool isInstallOperation = false
    )
    {
        try
        {
            _logService.LogInformation(
                $"Updating capabilities array with {capabilities.Count} capabilities"
            );

            // Find the capabilities array section in the script
            int arrayStartIndex = scriptContent.IndexOf("$capabilities = @(");
            if (arrayStartIndex == -1)
            {
                _logService.LogWarning("Could not find $capabilities array in BloatRemoval.ps1");

                // If this is an install operation, we don't need to add anything
                if (isInstallOperation)
                {
                    _logService.LogInformation(
                        "Install operation with no capabilities array found - nothing to remove"
                    );
                    return scriptContent;
                }

                // For removal operations, we need to add the capabilities array and processing code
                _logService.LogInformation("Creating capabilities array in BloatRemoval.ps1");

                // Find a good place to insert the section (at the beginning of the script or before packages)
                int insertIndex = scriptContent.IndexOf("$packages = @(");
                if (insertIndex == -1)
                {
                    // If no packages array, find the first non-comment line
                    var scriptLines = scriptContent.Split('\n');
                    for (int i = 0; i < scriptLines.Length; i++)
                    {
                        if (
                            !scriptLines[i].TrimStart().StartsWith("#")
                            && !string.IsNullOrWhiteSpace(scriptLines[i])
                        )
                        {
                            insertIndex = scriptContent.IndexOf(scriptLines[i]);
                            break;
                        }
                    }

                    // If still not found, insert at the beginning
                    if (insertIndex == -1)
                    {
                        insertIndex = 0;
                    }
                }

                // Create the capabilities section
                var capabilitiesSection = new StringBuilder();
                capabilitiesSection.AppendLine("# Capabilities to remove");
                capabilitiesSection.AppendLine("$capabilities = @(");

                // Add capabilities without trailing comma on the last item
                for (int i = 0; i < capabilities.Count; i++)
                {
                    string capability = capabilities[i];
                    if (i < capabilities.Count - 1)
                    {
                        capabilitiesSection.AppendLine($"    '{capability}',");
                    }
                    else
                    {
                        capabilitiesSection.AppendLine($"    '{capability}'");
                    }
                }

                capabilitiesSection.AppendLine(")");
                capabilitiesSection.AppendLine();
                capabilitiesSection.AppendLine("# Process capabilities");
                capabilitiesSection.AppendLine("foreach ($capability in $capabilities) {");
                capabilitiesSection.AppendLine(
                    "    Write-Host \"Removing capability: $capability\" -ForegroundColor Yellow"
                );
                capabilitiesSection.AppendLine(
                    "    Remove-WindowsCapability -Online -Name $capability | Out-Null"
                );
                capabilitiesSection.AppendLine("}");
                capabilitiesSection.AppendLine();

                // Insert the section
                return scriptContent.Substring(0, insertIndex)
                    + capabilitiesSection.ToString()
                    + scriptContent.Substring(insertIndex);
            }

            // Find the end of the capabilities array
            int arrayEndIndex = scriptContent.IndexOf(")", arrayStartIndex);
            if (arrayEndIndex == -1)
            {
                _logService.LogWarning(
                    "Could not find end of $capabilities array in BloatRemoval.ps1"
                );
                return scriptContent;
            }

            // Extract the array content
            string arrayContent = scriptContent.Substring(
                arrayStartIndex,
                arrayEndIndex - arrayStartIndex + 1
            );

            // Parse the capabilities in the array
            var existingCapabilities = new List<string>();
            var lines = arrayContent.Split('\n');
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("'") || trimmedLine.StartsWith("\""))
                {
                    var capability = trimmedLine.Trim('\'', '"', ' ', ',');
                    existingCapabilities.Add(capability);
                }
            }

            bool modified = false;

            if (isInstallOperation)
            {
                // For install operations, REMOVE the capability from the list
                foreach (var capability in capabilities)
                {
                    // Extract the base name without version for capability matching
                    string baseCapabilityName = capability;
                    if (capability.Contains("~~~~"))
                    {
                        // Extract the base name before the version (~~~~)
                        baseCapabilityName = capability.Split('~')[0];
                        _logService.LogInformation(
                            $"Extracted base capability name '{baseCapabilityName}' from '{capability}' for removal"
                        );
                    }

                    // Find any capability in the list that matches the base name (regardless of version)
                    var matchingCapabilities = existingCapabilities
                        .Where(c =>
                            c.StartsWith(baseCapabilityName, StringComparison.OrdinalIgnoreCase)
                            || (
                                c.Contains("~~~~")
                                && c.Split('~')[0]
                                    .Equals(baseCapabilityName, StringComparison.OrdinalIgnoreCase)
                            )
                        )
                        .ToList();

                    foreach (var matchingCapability in matchingCapabilities)
                    {
                        existingCapabilities.Remove(matchingCapability);
                        _logService.LogInformation(
                            $"Removed capability: {matchingCapability} from BloatRemoval.ps1"
                        );
                        modified = true;
                    }
                }

                // Even if no capabilities were found to remove, we should still return the updated script
                // This ensures the method doesn't exit early when no matches are found
                if (!modified)
                {
                    _logService.LogInformation("No capabilities to remove from BloatRemoval.ps1");
                }
            }
            else
            {
                // For removal operations, ADD the capability to the list
                foreach (var capability in capabilities)
                {
                    if (
                        !existingCapabilities.Contains(capability, StringComparer.OrdinalIgnoreCase)
                    )
                    {
                        existingCapabilities.Add(capability);
                        _logService.LogInformation(
                            $"Added capability: {capability} to BloatRemoval.ps1"
                        );
                        modified = true;
                    }
                }

                if (!modified)
                {
                    _logService.LogInformation("No new capabilities to add to BloatRemoval.ps1");
                    return scriptContent;
                }
            }

            // Rebuild the array content
            var newArrayContent = new StringBuilder();
            newArrayContent.AppendLine("$capabilities = @(");

            // Add capabilities without trailing comma on the last item
            for (int i = 0; i < existingCapabilities.Count; i++)
            {
                string capability = existingCapabilities[i];
                if (i < existingCapabilities.Count - 1)
                {
                    newArrayContent.AppendLine($"    '{capability}',");
                }
                else
                {
                    newArrayContent.AppendLine($"    '{capability}'");
                }
            }

            newArrayContent.Append(")");

            // Replace the old array content with the new one
            return scriptContent.Substring(0, arrayStartIndex)
                + newArrayContent.ToString()
                + scriptContent.Substring(arrayEndIndex + 1);
        }
        catch (Exception ex)
        {
            _logService.LogError("Error updating capabilities array in script", ex);
            return scriptContent;
        }
    }

    /// <inheritdoc/>
    public string UpdatePackagesArrayInScript(
        string scriptContent,
        List<string> packages,
        bool isInstallOperation = false
    )
    {
        try
        {
            _logService.LogInformation($"Updating packages array with {packages.Count} packages");

            // Find the packages array section in the script
            int arrayStartIndex = scriptContent.IndexOf("$packages = @(");
            if (arrayStartIndex == -1)
            {
                _logService.LogWarning("Could not find $packages array in BloatRemoval.ps1");
                return scriptContent;
            }

            // Find the end of the packages array
            int arrayEndIndex = scriptContent.IndexOf(")", arrayStartIndex);
            if (arrayEndIndex == -1)
            {
                _logService.LogWarning("Could not find end of $packages array in BloatRemoval.ps1");
                return scriptContent;
            }

            // Extract the array content
            string arrayContent = scriptContent.Substring(
                arrayStartIndex,
                arrayEndIndex - arrayStartIndex + 1
            );

            // Parse the packages in the array
            var existingPackages = new List<string>();
            var lines = arrayContent.Split('\n');
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("'") || trimmedLine.StartsWith("\""))
                {
                    var package = trimmedLine.Trim('\'', '"', ' ', ',');
                    existingPackages.Add(package);
                }
            }

            bool modified = false;

            if (isInstallOperation)
            {
                // For install operations, REMOVE the package from the list
                foreach (var package in packages)
                {
                    // Extract the base name without version for package matching
                    string basePackageName = package;
                    if (package.Contains("~~~~"))
                    {
                        // Extract the base name before the version (~~~~)
                        basePackageName = package.Split('~')[0];
                        _logService.LogInformation(
                            $"Extracted base package name '{basePackageName}' from '{package}' for removal"
                        );
                    }

                    // Find any package in the list that matches the base name (regardless of version)
                    var matchingPackages = existingPackages
                        .Where(p =>
                            p.StartsWith(basePackageName, StringComparison.OrdinalIgnoreCase)
                            || (
                                p.Contains("~~~~")
                                && p.Split('~')[0]
                                    .Equals(basePackageName, StringComparison.OrdinalIgnoreCase)
                            )
                        )
                        .ToList();

                    foreach (var matchingPackage in matchingPackages)
                    {
                        existingPackages.Remove(matchingPackage);
                        _logService.LogInformation(
                            $"Removed package: {matchingPackage} from BloatRemoval.ps1"
                        );
                        modified = true;
                    }
                }

                if (!modified)
                {
                    _logService.LogInformation("No packages to remove from BloatRemoval.ps1");
                }
            }
            else
            {
                // For removal operations, ADD the package to the list
                foreach (var package in packages)
                {
                    if (!existingPackages.Contains(package, StringComparer.OrdinalIgnoreCase))
                    {
                        existingPackages.Add(package);
                        _logService.LogInformation($"Added package: {package} to BloatRemoval.ps1");
                        modified = true;
                    }
                }

                if (!modified)
                {
                    _logService.LogInformation("No new packages to add to BloatRemoval.ps1");
                    return scriptContent;
                }
            }

            // Rebuild the array content
            var newArrayContent = new StringBuilder();
            newArrayContent.AppendLine("$packages = @(");

            // Add packages without trailing comma on the last item
            for (int i = 0; i < existingPackages.Count; i++)
            {
                string package = existingPackages[i];
                if (i < existingPackages.Count - 1)
                {
                    newArrayContent.AppendLine($"    '{package}',");
                }
                else
                {
                    newArrayContent.AppendLine($"    '{package}'");
                }
            }

            newArrayContent.Append(")");

            // Replace the old array content with the new one
            return scriptContent.Substring(0, arrayStartIndex)
                + newArrayContent.ToString()
                + scriptContent.Substring(arrayEndIndex + 1);
        }
        catch (Exception ex)
        {
            _logService.LogError("Error updating packages array in script", ex);
            return scriptContent;
        }
    }

    /// <inheritdoc/>
    public string UpdateOptionalFeaturesInScript(
        string scriptContent,
        List<string> features,
        bool isInstallOperation = false
    )
    {
        try
        {
            _logService.LogInformation(
                $"Updating optional features with {features.Count} features"
            );

            // Check if the optional features section exists
            int sectionStartIndex = scriptContent.IndexOf("# Disable Optional Features");
            int optionalFeaturesArrayIndex = scriptContent.IndexOf("$optionalFeatures = @(");

            // If the section doesn't exist, create it
            if (sectionStartIndex == -1 && optionalFeaturesArrayIndex == -1)
            {
                _logService.LogInformation(
                    "Creating Optional Features section in BloatRemoval.ps1"
                );

                // Find a good place to insert the section (after the packages section)
                int packagesEndIndex = -1;

                // First, try to find the #endregion marker after the packages section
                int processPackagesIndex = scriptContent.IndexOf("# Process Packages");
                if (processPackagesIndex != -1)
                {
                    int endRegionIndex = scriptContent.IndexOf("#endregion", processPackagesIndex);
                    if (endRegionIndex != -1)
                    {
                        // Find the end of the line after the #endregion
                        int newlineIndex = scriptContent.IndexOf('\n', endRegionIndex);
                        if (newlineIndex != -1)
                        {
                            packagesEndIndex = newlineIndex + 1; // Include the newline
                        }
                        else
                        {
                            packagesEndIndex = scriptContent.Length;
                        }
                    }
                }

                // If we couldn't find the #endregion marker, look for the Registry settings section
                if (packagesEndIndex == -1)
                {
                    int registryIndex = scriptContent.IndexOf("# Registry settings");
                    if (registryIndex != -1)
                    {
                        packagesEndIndex = registryIndex;
                    }
                }

                // If we still can't find a good insertion point, look for the end of the packages foreach loop
                if (packagesEndIndex == -1)
                {
                    int packagesForEachIndex = scriptContent.IndexOf(
                        "foreach ($package in $packages)"
                    );
                    if (packagesForEachIndex != -1)
                    {
                        // Find the matching closing brace for the foreach loop
                        int openBraces = 0;
                        int closeBraces = 0;
                        int currentIndex = packagesForEachIndex;

                        while (currentIndex < scriptContent.Length)
                        {
                            if (scriptContent[currentIndex] == '{')
                            {
                                openBraces++;
                            }
                            else if (scriptContent[currentIndex] == '}')
                            {
                                closeBraces++;
                                if (closeBraces == openBraces && openBraces > 0)
                                {
                                    // Found the matching closing brace
                                    int braceIndex = currentIndex;

                                    // Find the end of the line after the closing brace
                                    int newlineIndex = scriptContent.IndexOf('\n', braceIndex);
                                    if (newlineIndex != -1)
                                    {
                                        packagesEndIndex = newlineIndex + 1; // Include the newline
                                    }
                                    else
                                    {
                                        packagesEndIndex = scriptContent.Length;
                                    }
                                    break;
                                }
                            }
                            currentIndex++;
                        }
                    }
                }

                // If we still couldn't find a good insertion point, just append to the end of the script
                if (packagesEndIndex == -1)
                {
                    _logService.LogWarning(
                        "Could not find a good insertion point for optional features section, appending to the end"
                    );
                    packagesEndIndex = scriptContent.Length;
                }

                // Create the optional features section
                var optionalFeaturesSection = new StringBuilder();
                optionalFeaturesSection.AppendLine();
                optionalFeaturesSection.AppendLine("# Disable Optional Features");
                optionalFeaturesSection.AppendLine("$optionalFeatures = @(");

                // Add features without trailing comma on the last item
                for (int i = 0; i < features.Count; i++)
                {
                    string feature = features[i];
                    if (i < features.Count - 1)
                    {
                        optionalFeaturesSection.AppendLine($"    '{feature}',");
                    }
                    else
                    {
                        optionalFeaturesSection.AppendLine($"    '{feature}'");
                    }
                }

                optionalFeaturesSection.AppendLine(")");
                optionalFeaturesSection.AppendLine();
                optionalFeaturesSection.AppendLine("foreach ($feature in $optionalFeatures) {");
                optionalFeaturesSection.AppendLine(
                    "    Write-Host \"Disabling optional feature: $feature\" -ForegroundColor Yellow"
                );
                optionalFeaturesSection.AppendLine(
                    "    Disable-WindowsOptionalFeature -Online -FeatureName $feature -NoRestart | Out-Null"
                );
                optionalFeaturesSection.AppendLine("}");

                // Insert the section
                return scriptContent.Substring(0, packagesEndIndex)
                    + optionalFeaturesSection.ToString()
                    + scriptContent.Substring(packagesEndIndex);
            }

            // If the section exists, update it
            int arrayStartIndex;
            if (sectionStartIndex != -1)
            {
                // If we found the section header, search for the array starting from there
                arrayStartIndex = scriptContent.IndexOf(
                    "$optionalFeatures = @(",
                    sectionStartIndex
                );
            }
            else
            {
                // If section header wasn't found, search the entire script
                arrayStartIndex = scriptContent.IndexOf("$optionalFeatures = @(");
            }
            if (arrayStartIndex == -1)
            {
                _logService.LogWarning(
                    "Could not find $optionalFeatures array in BloatRemoval.ps1"
                );
                return scriptContent;
            }

            // Find the end of the optional features array
            int arrayEndIndex = scriptContent.IndexOf(")", arrayStartIndex);
            if (arrayEndIndex == -1)
            {
                _logService.LogWarning(
                    "Could not find end of $optionalFeatures array in BloatRemoval.ps1"
                );
                return scriptContent;
            }

            // Extract the array content
            string arrayContent = scriptContent.Substring(
                arrayStartIndex,
                arrayEndIndex - arrayStartIndex + 1
            );

            // Parse the optional features in the array
            var optionalFeatures = new List<string>();
            var lines = arrayContent.Split('\n');
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("'") || trimmedLine.StartsWith("\""))
                {
                    var feature = trimmedLine.Trim('\'', '"', ' ', ',');
                    optionalFeatures.Add(feature);
                }
            }

            bool modified = false;

            if (isInstallOperation)
            {
                // For install operations, REMOVE the optional feature from the list
                foreach (var feature in features)
                {
                    // Extract the base name without version for feature matching
                    string baseFeatureName = feature;
                    if (feature.Contains("~~~~"))
                    {
                        // Extract the base name before the version (~~~~)
                        baseFeatureName = feature.Split('~')[0];
                        _logService.LogInformation(
                            $"Extracted base feature name '{baseFeatureName}' from '{feature}' for removal"
                        );
                    }

                    // Find any feature in the list that matches the base name (regardless of version)
                    var matchingFeatures = optionalFeatures
                        .Where(f =>
                            f.StartsWith(baseFeatureName, StringComparison.OrdinalIgnoreCase)
                            || (
                                f.Contains("~~~~")
                                && f.Split('~')[0]
                                    .Equals(baseFeatureName, StringComparison.OrdinalIgnoreCase)
                            )
                        )
                        .ToList();

                    foreach (var matchingFeature in matchingFeatures)
                    {
                        optionalFeatures.Remove(matchingFeature);
                        _logService.LogInformation(
                            $"Removed optional feature: {matchingFeature} from BloatRemoval.ps1"
                        );
                        modified = true;
                    }
                }

                if (!modified)
                {
                    _logService.LogInformation(
                        "No optional features to remove from BloatRemoval.ps1"
                    );
                }
            }
            else
            {
                // For removal operations, ADD the optional feature to the list
                foreach (var feature in features)
                {
                    if (!optionalFeatures.Contains(feature, StringComparer.OrdinalIgnoreCase))
                    {
                        optionalFeatures.Add(feature);
                        _logService.LogInformation(
                            $"Added optional feature: {feature} to BloatRemoval.ps1"
                        );
                        modified = true;
                    }
                }

                if (!modified)
                {
                    _logService.LogInformation(
                        "No new optional features to add to BloatRemoval.ps1"
                    );
                }
            }

            // Rebuild the array content
            var newArrayContent = new StringBuilder();
            newArrayContent.AppendLine("$optionalFeatures = @(");

            // Add features without trailing comma on the last item
            for (int i = 0; i < optionalFeatures.Count; i++)
            {
                string feature = optionalFeatures[i];
                if (i < optionalFeatures.Count - 1)
                {
                    newArrayContent.AppendLine($"    '{feature}',");
                }
                else
                {
                    newArrayContent.AppendLine($"    '{feature}'");
                }
            }

            newArrayContent.Append(")");

            // Replace the old array content with the new one
            return scriptContent.Substring(0, arrayStartIndex)
                + newArrayContent.ToString()
                + scriptContent.Substring(arrayEndIndex + 1);
        }
        catch (Exception ex)
        {
            _logService.LogError("Error updating optional features in script", ex);
            return scriptContent;
        }
    }

    /// <inheritdoc/>
    public string UpdateRegistrySettingsInScript(
        string scriptContent,
        Dictionary<string, List<AppRegistrySetting>> appsWithRegistry,
        bool isInstallOperation = false
    )
    {
        try
        {
            _logService.LogInformation(
                $"Updating registry settings for {appsWithRegistry.Count} apps"
            );

            // Find the registry settings section
            int registrySectionIndex = scriptContent.IndexOf("# Registry settings");
            if (registrySectionIndex == -1)
            {
                // If the registry settings section doesn't exist and this is an install operation,
                // we don't need to add anything since we're removing registry entries
                if (isInstallOperation)
                {
                    _logService.LogInformation(
                        "No registry settings section found during install operation - nothing to remove"
                    );
                    return scriptContent;
                }

                // If the registry settings section doesn't exist, create it
                _logService.LogInformation(
                    "Creating Registry settings section in BloatRemoval.ps1"
                );

                // Find a good place to insert the section (at the end of the script)
                int insertIndex = scriptContent.Length;

                // Create the registry settings section
                var registrySection = new StringBuilder();
                registrySection.AppendLine();
                registrySection.AppendLine("# Registry settings");
                registrySection.AppendLine("#endregion");

                // Add registry settings for each app
                foreach (var appEntry in appsWithRegistry)
                {
                    string appName = appEntry.Key;
                    List<AppRegistrySetting> settings = appEntry.Value;

                    if (settings == null || settings.Count == 0)
                    {
                        continue;
                    }

                    registrySection.AppendLine();
                    registrySection.AppendLine($"# Registry settings for {appName}");

                    foreach (var setting in settings)
                    {
                        if (setting.Path == null || setting.Name == null)
                        {
                            continue;
                        }

                        string regType = RegistryScriptHelper.GetRegTypeString(setting.ValueKind);
                        string regValue = FormatRegistryValue(setting.Value, setting.ValueKind);

                        registrySection.AppendLine(
                            $"reg add \"{setting.Path}\" /v \"{setting.Name}\" /t {regType} /d {regValue} /f"
                        );
                    }
                }

                // Insert the section
                return scriptContent + registrySection.ToString();
            }

            // If the registry settings section exists, update it
            StringBuilder updatedContent = new StringBuilder(scriptContent);

            // Process each app in the registry settings
            foreach (var appEntry in appsWithRegistry)
            {
                string appName = appEntry.Key;
                List<AppRegistrySetting> settings = appEntry.Value;

                // Check if this app already has registry settings in the script
                string appSectionHeader = $"# Registry settings for {appName}";
                int appSectionIndex = updatedContent
                    .ToString()
                    .IndexOf(appSectionHeader, registrySectionIndex);

                if (appSectionIndex == -1)
                {
                    // App doesn't have registry settings yet
                    if (isInstallOperation)
                    {
                        // If this is an install operation and there are no registry settings for this app,
                        // there's nothing to remove
                        _logService.LogInformation(
                            $"No registry settings found for {appName} during install operation - nothing to remove"
                        );
                        continue;
                    }

                    // For removal operations, add the registry settings
                    _logService.LogInformation(
                        $"Adding registry settings for {appName} to BloatRemoval.ps1"
                    );

                    // Find the end of the registry settings section or the start of the next major section
                    int endOfRegistrySection = updatedContent
                        .ToString()
                        .IndexOf("# Prevent apps from reinstalling", registrySectionIndex);
                    if (endOfRegistrySection == -1)
                    {
                        endOfRegistrySection = updatedContent.Length;
                    }

                    // Create the app registry settings section
                    var appRegistrySection = new StringBuilder();
                    appRegistrySection.AppendLine();
                    appRegistrySection.AppendLine(appSectionHeader);

                    foreach (var setting in settings)
                    {
                        if (setting.Path == null || setting.Name == null)
                        {
                            continue;
                        }

                        string regType = RegistryScriptHelper.GetRegTypeString(setting.ValueKind);
                        string regValue = FormatRegistryValue(setting.Value, setting.ValueKind);

                        appRegistrySection.AppendLine(
                            $"reg add \"{setting.Path}\" /v \"{setting.Name}\" /t {regType} /d {regValue} /f"
                        );
                    }

                    // Insert the app registry settings section
                    updatedContent.Insert(endOfRegistrySection, appRegistrySection.ToString());
                }
                else
                {
                    // App already has registry settings in the script
                    // Find the end of the app section (next app section or end of registry section)
                    int nextAppSectionIndex = updatedContent
                        .ToString()
                        .IndexOf(
                            "# Registry settings for",
                            appSectionIndex + appSectionHeader.Length
                        );
                    if (nextAppSectionIndex == -1)
                    {
                        nextAppSectionIndex = updatedContent
                            .ToString()
                            .IndexOf("# Prevent apps from reinstalling", appSectionIndex);
                        if (nextAppSectionIndex == -1)
                        {
                            nextAppSectionIndex = updatedContent.Length;
                        }
                    }

                    if (isInstallOperation)
                    {
                        // For install operations, remove the entire registry settings section for this app
                        _logService.LogInformation(
                            $"Removing registry settings for {appName} from BloatRemoval.ps1"
                        );
                        updatedContent.Remove(
                            appSectionIndex,
                            nextAppSectionIndex - appSectionIndex
                        );
                    }
                    else
                    {
                        // For removal operations, update the registry settings
                        _logService.LogInformation(
                            $"Updating registry settings for {appName} in BloatRemoval.ps1"
                        );

                        // Create the updated app registry settings section
                        var updatedAppRegistrySection = new StringBuilder();
                        updatedAppRegistrySection.AppendLine(appSectionHeader);

                        foreach (var setting in settings)
                        {
                            if (setting.Path == null || setting.Name == null)
                            {
                                continue;
                            }

                            string regType = RegistryScriptHelper.GetRegTypeString(
                                setting.ValueKind
                            );
                            string regValue = FormatRegistryValue(setting.Value, setting.ValueKind);

                            updatedAppRegistrySection.AppendLine(
                                $"reg add \"{setting.Path}\" /v \"{setting.Name}\" /t {regType} /d {regValue} /f"
                            );
                        }

                        // Replace the old app registry settings section with the new one
                        updatedContent.Remove(
                            appSectionIndex,
                            nextAppSectionIndex - appSectionIndex
                        );
                        updatedContent.Insert(
                            appSectionIndex,
                            updatedAppRegistrySection.ToString()
                        );
                    }
                }
            }

            return updatedContent.ToString();
        }
        catch (Exception ex)
        {
            _logService.LogError("Error updating registry settings in script", ex);
            return scriptContent;
        }
    }

    /// <summary>
    /// Formats a registry value for use in a reg.exe command.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <param name="valueKind">The registry value kind.</param>
    /// <returns>The formatted value.</returns>
    private string FormatRegistryValue(object value, RegistryValueKind valueKind)
    {
        if (value == null)
        {
            return "\"\"";
        }

        switch (valueKind)
        {
            case RegistryValueKind.String:
            case RegistryValueKind.ExpandString:
                return $"\"{value}\"";
            case RegistryValueKind.DWord:
            case RegistryValueKind.QWord:
                return value.ToString();
            case RegistryValueKind.Binary:
                if (value is byte[] bytes)
                {
                    return BitConverter.ToString(bytes).Replace("-", ",");
                }
                return "\"\"";
            case RegistryValueKind.MultiString:
                if (value is string[] strings)
                {
                    return $"\"{string.Join("\\0", strings)}\\0\"";
                }
                return "\"\"";
            default:
                return $"\"{value}\"";
        }
    }
}
