using System.Text.RegularExpressions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

// What one read of the PC found. WinGetIds is null when winget could not be asked; WinGetPackages is null when only its
// CLI could, which knows ids alone. Components is null when no definition needed DISM.
internal sealed record ScanSources(
    IReadOnlyList<UninstallEntry> RegistryEntries,
    IReadOnlySet<string> AppxNames,
    IReadOnlySet<string>? WinGetIds,
    IReadOnlySet<string> ChocoInstalled,
    InstalledWindowsComponents? Components)
{
    // A system component is hidden from Apps & Features, so only its key name can claim it.
    internal IReadOnlySet<string> RegistryKeyNames { get; } = RegistryEntries.Select(e => e.KeyName).ToHashSet(StringComparer.OrdinalIgnoreCase);

    internal IReadOnlySet<string> RegistryDisplayNames { get; } = RegistryEntries
        .Where(e => !e.IsSystemComponent && e.DisplayName.Length > 0)
        .Select(e => e.DisplayName)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
}

// Whether each definition is installed, decided from what the scan already read.
internal static class InstalledStatus
{
    internal static Dictionary<string, bool> Windows(IReadOnlyList<ItemDefinition> definitions, ScanSources sources, ILogService logService)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var capability in definitions.Where(d => !string.IsNullOrEmpty(d.CapabilityName)))
        {
            result[capability.Id] = sources.Components?.Capabilities?
                .Any(c => c.StartsWith(capability.CapabilityName!, StringComparison.OrdinalIgnoreCase)) == true;
        }

        foreach (var feature in definitions.Where(d => !string.IsNullOrEmpty(d.OptionalFeatureName)))
            result[feature.Id] = sources.Components?.Features?.Contains(feature.OptionalFeatureName!) == true;

        // WebView2 has no AppX package, so a Windows app with only an uninstall key counts too
        var apps = definitions.Where(d => d.AppxPackageName?.Length > 0
            || (string.IsNullOrEmpty(d.CapabilityName) && string.IsNullOrEmpty(d.OptionalFeatureName)
                && (!string.IsNullOrEmpty(d.RegistrySubKeyName) || !string.IsNullOrEmpty(d.RegistryDisplayName)))).ToList();
        foreach (var app in apps)
        {
            result[app.Id] = app.AppxPackageName?.Any(sources.AppxNames.Contains) == true
                || MatchesWinGet(app, sources.WinGetIds)
                // A Click-to-Run install (OneNote) is neither AppX nor winget; its uninstall key is its only trace.
                || (!string.IsNullOrEmpty(app.RegistrySubKeyName) && sources.RegistryKeyNames.Any(k => MatchesPattern(k, app.RegistrySubKeyName)))
                || (!string.IsNullOrEmpty(app.RegistryDisplayName) && sources.RegistryDisplayNames.Any(n => MatchesPattern(n, app.RegistryDisplayName)));
        }

        logService.LogInformation($"Windows status: {result.Count(r => r.Value)} of the {result.Count} checked are installed");
        return result;
    }

    internal static Dictionary<string, bool> External(IReadOnlyList<ItemDefinition> definitions, ScanSources sources, ILogService logService)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
            result[definition.Id] = IsInstalledExternally(definition, sources, logService);

        logService.LogInformation($"External status: {result.Count(r => r.Value)} of the {result.Count} checked are installed");
        return result;
    }

    // Checked in order, and the first source that answers sets DetectedVia: the uninstall follows the route that found it.
    private static bool IsInstalledExternally(ItemDefinition definition, ScanSources sources, ILogService logService)
    {
        if (MatchesWinGet(definition, sources.WinGetIds))
        {
            // WinGet's list can false-positive when a sibling build (AutoHotkey v1 installed from a URL) shares the
            // vendor's uninstall entry that winget fingerprints as v2, so a definition with a registry fingerprint needs it to agree.
            var hasFingerprint = !string.IsNullOrEmpty(definition.RegistryDisplayName) || !string.IsNullOrEmpty(definition.RegistrySubKeyName);
            var registryAgrees = !hasFingerprint
                || (!string.IsNullOrEmpty(definition.RegistryDisplayName) && sources.RegistryDisplayNames.Any(n => MatchesPattern(n, definition.RegistryDisplayName)))
                || (!string.IsNullOrEmpty(definition.RegistrySubKeyName) && sources.RegistryKeyNames.Any(k => MatchesPattern(k, definition.RegistrySubKeyName)));
            if (registryAgrees)
            {
                definition.DetectedVia = DetectionSource.WinGet;
                return true;
            }

            logService.LogInformation($"WinGet reported {definition.Name} installed, but registry fingerprint did not match - treating as false positive");
        }

        if (!string.IsNullOrEmpty(definition.ChocoPackageId) && sources.ChocoInstalled.Contains(definition.ChocoPackageId))
        {
            definition.DetectedVia = DetectionSource.Chocolatey;
            return true;
        }

        if (definition.AppxPackageName?.Any(sources.AppxNames.Contains) == true)
        {
            definition.DetectedVia = DetectionSource.AppX;
            return true;
        }

        if (sources.RegistryEntries.Any(entry => MatchesUninstallEntry(definition, entry)))
        {
            definition.DetectedVia = DetectionSource.Registry;
            return true;
        }

        if (definition.DetectionPaths?.Any(path => PathExists(Environment.ExpandEnvironmentVariables(path))) == true)
        {
            definition.DetectedVia = DetectionSource.FileSystem;
            return true;
        }

        return false;
    }

    private static bool PathExists(string path) => Directory.Exists(path) || File.Exists(path);

    private static bool MatchesWinGet(ItemDefinition definition, IReadOnlySet<string>? winGetIds) =>
        winGetIds is not null
        && (definition.WinGetPackageId?.Any(winGetIds.Contains) == true
            || (!string.IsNullOrEmpty(definition.MsStoreId) && winGetIds.Contains(definition.MsStoreId)));

    internal static bool MatchesPattern(string input, string pattern)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        var patterns = pattern.Split('|');
        return patterns.Any(p =>
        {
            var regexPattern = Regex.Escape(p)
                .Replace(@"\{version}", ".+?")
                .Replace(@"\{arch}", ".+?")
                .Replace(@"\{locale}", ".+?");
            return Regex.IsMatch(input, $"^{regexPattern}$", RegexOptions.IgnoreCase);
        });
    }

    // The installed status and the Installed Apps scan both use this, so a row the status calls installed is the same
    // program the scan joins to it. A system component is hidden from Apps & Features, so only a subkey pattern
    // claims one.
    internal static bool MatchesUninstallEntry(ItemDefinition definition, UninstallEntry entry)
    {
        if (!string.IsNullOrEmpty(definition.RegistrySubKeyName) && MatchesPattern(entry.KeyName, definition.RegistrySubKeyName))
            return true;
        if (entry.IsSystemComponent)
            return false;
        return string.Equals(entry.KeyName, definition.Name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(entry.DisplayName, definition.Name, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrEmpty(definition.RegistryDisplayName) && MatchesPattern(entry.DisplayName, definition.RegistryDisplayName));
    }
}
