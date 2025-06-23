using System;
using System.Collections.Generic;
using System.Text;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.ScriptGeneration;

/// <summary>
/// Implementation of IBloatRemovalScriptContentModifier that provides methods for modifying script content.
/// </summary>
public class BloatRemovalScriptContentModifier : IBloatRemovalScriptContentModifier
{
    private readonly ILogService _logService;

    /// <summary>
    /// Initializes a new instance of the <see cref="BloatRemovalScriptContentModifier"/> class.
    /// </summary>
    /// <param name="logService">The logging service.</param>
    public BloatRemovalScriptContentModifier(ILogService logService)
    {
        _logService = logService;
    }

    /// <inheritdoc/>
    public string RemoveCapabilityFromScript(string scriptContent, string capabilityName)
    {
        // Find the capabilities array section in the script
        int arrayStartIndex = scriptContent.IndexOf("$capabilities = @(");
        if (arrayStartIndex == -1)
        {
            _logService.LogWarning("Could not find $capabilities array in BloatRemoval.ps1");
            return scriptContent;
        }

        // Find the end of the capabilities array
        int arrayEndIndex = scriptContent.IndexOf(")", arrayStartIndex);
        if (arrayEndIndex == -1)
        {
            _logService.LogWarning("Could not find end of $capabilities array in BloatRemoval.ps1");
            return scriptContent;
        }

        // Extract the array content
        string arrayContent = scriptContent.Substring(
            arrayStartIndex,
            arrayEndIndex - arrayStartIndex + 1
        );

        // Parse the capabilities in the array
        var capabilities = new List<string>();
        var lines = arrayContent.Split('\n');
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("'") || trimmedLine.StartsWith("\""))
            {
                var capability = trimmedLine.Trim('\'', '"', ' ', ',');
                capabilities.Add(capability);
            }
        }

        // Check if the capability is in the array
        bool removed =
            capabilities.RemoveAll(c =>
                c.Equals(capabilityName, StringComparison.OrdinalIgnoreCase)
            ) > 0;

        if (!removed)
        {
            // Capability not found in the array
            return scriptContent;
        }

        // Rebuild the array content
        var newArrayContent = new StringBuilder();
        newArrayContent.AppendLine("$capabilities = @(");

        // Add capabilities without trailing comma on the last item
        for (int i = 0; i < capabilities.Count; i++)
        {
            string capability = capabilities[i];
            if (i < capabilities.Count - 1)
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

    /// <inheritdoc/>
    public string RemovePackageFromScript(string scriptContent, string packageName)
    {
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
        var packages = new List<string>();
        var lines = arrayContent.Split('\n');
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("'") || trimmedLine.StartsWith("\""))
            {
                var package = trimmedLine.Trim('\'', '"', ' ', ',');
                packages.Add(package);
            }
        }

        // Check if the package is in the array
        bool removed =
            packages.RemoveAll(p => p.Equals(packageName, StringComparison.OrdinalIgnoreCase)) > 0;

        if (!removed)
        {
            // Package not found in the array
            return scriptContent;
        }

        // Rebuild the array content
        var newArrayContent = new StringBuilder();
        newArrayContent.AppendLine("$packages = @(");

        // Add packages without trailing comma on the last item
        for (int i = 0; i < packages.Count; i++)
        {
            string package = packages[i];
            if (i < packages.Count - 1)
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

    /// <inheritdoc/>
    public string RemoveOptionalFeatureFromScript(string scriptContent, string featureName)
    {
        // Check if the optional features section exists
        int sectionStartIndex = scriptContent.IndexOf("# Disable Optional Features");
        if (sectionStartIndex == -1)
        {
            // Optional features section doesn't exist, so nothing to remove
            return scriptContent;
        }

        // Find the optional features array
        int arrayStartIndex = scriptContent.IndexOf("$optionalFeatures = @(", sectionStartIndex);
        if (arrayStartIndex == -1)
        {
            _logService.LogWarning("Could not find $optionalFeatures array in BloatRemoval.ps1");
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

        // Check if the feature is in the array
        bool removed =
            optionalFeatures.RemoveAll(f =>
                f.Equals(featureName, StringComparison.OrdinalIgnoreCase)
            ) > 0;

        if (!removed)
        {
            // Feature not found in the array
            return scriptContent;
        }

        // If the array is now empty, remove the entire optional features section
        if (optionalFeatures.Count == 0)
        {
            // Find the end of the optional features section
            int sectionEndIndex = scriptContent.IndexOf(
                "foreach ($feature in $optionalFeatures) {",
                arrayEndIndex
            );
            if (sectionEndIndex != -1)
            {
                sectionEndIndex = scriptContent.IndexOf("}", sectionEndIndex);
                if (sectionEndIndex != -1)
                {
                    sectionEndIndex = scriptContent.IndexOf('\n', sectionEndIndex);
                    if (sectionEndIndex != -1)
                    {
                        // Remove the entire section
                        return scriptContent.Substring(0, sectionStartIndex)
                            + scriptContent.Substring(sectionEndIndex + 1);
                    }
                }
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

    /// <inheritdoc/>
    public string RemoveAppRegistrySettingsFromScript(string scriptContent, string appName)
    {
        // Find the registry settings section
        int registrySection = scriptContent.IndexOf("# Registry settings");
        if (registrySection == -1)
        {
            return scriptContent;
        }

        // Generate possible section headers for this app
        var possibleSectionHeaders = new List<string>
        {
            $"# Registry settings for {appName}",
            $"# Registry settings for Microsoft.{appName}",
        };

        // Add variations without "Microsoft." prefix if it already has it
        if (appName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase))
        {
            string nameWithoutPrefix = appName.Substring("Microsoft.".Length);
            possibleSectionHeaders.Add($"# Registry settings for {nameWithoutPrefix}");
        }

        // Add common variations
        possibleSectionHeaders.Add($"# Registry settings for {appName}_8wekyb3d8bbwe");

        // For Copilot specifically, add known variations
        if (appName.Contains("Copilot", StringComparison.OrdinalIgnoreCase))
        {
            possibleSectionHeaders.Add("# Registry settings for Copilot");
            possibleSectionHeaders.Add("# Registry settings for Microsoft.Copilot");
            possibleSectionHeaders.Add("# Registry settings for Windows.Copilot");
        }

        // For Xbox/GamingApp specifically, add known variations
        if (
            appName.Contains("Xbox", StringComparison.OrdinalIgnoreCase)
            || appName.Equals("Microsoft.GamingApp", StringComparison.OrdinalIgnoreCase)
        )
        {
            possibleSectionHeaders.Add("# Registry settings for Xbox");
            possibleSectionHeaders.Add("# Registry settings for Microsoft.Xbox");
            possibleSectionHeaders.Add("# Registry settings for GamingApp");
        }

        // Find the earliest matching section header
        int appSection = -1;
        string matchedHeader = null;

        foreach (var header in possibleSectionHeaders)
        {
            int index = scriptContent.IndexOf(header, registrySection);
            if (index != -1 && (appSection == -1 || index < appSection))
            {
                appSection = index;
                matchedHeader = header;
            }
        }

        if (appSection == -1)
        {
            _logService.LogInformation($"No registry settings found for {appName} in the script");
            return scriptContent;
        }

        _logService.LogInformation(
            $"Found registry settings for {appName} with header: {matchedHeader}"
        );

        // Find the end of the app section (next section or end of file)
        int nextSection = scriptContent.IndexOf(
            "# Registry settings for",
            appSection + matchedHeader.Length
        );
        if (nextSection == -1)
        {
            // Look for the next major section
            nextSection = scriptContent.IndexOf("# Prevent apps from reinstalling", appSection);
            if (nextSection == -1)
            {
                // If no next section, just return the original content
                _logService.LogWarning(
                    $"Could not find end of registry settings section for {appName}"
                );
                return scriptContent;
            }
        }

        // Remove the app registry settings section
        _logService.LogInformation($"Removing registry settings for {appName} from script");
        return scriptContent.Substring(0, appSection) + scriptContent.Substring(nextSection);
    }
}
