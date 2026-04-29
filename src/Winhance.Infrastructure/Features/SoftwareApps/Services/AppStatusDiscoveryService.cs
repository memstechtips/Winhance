using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Native;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class AppStatusDiscoveryService(
    ILogService logService,
    IWinGetBootstrapper winGetBootstrapper,
    IWinGetDetectionService winGetDetectionService,
    IChocolateyService chocolateyService,
    IInteractiveUserService interactiveUserService,
    IAppxPackageSource appxPackageSource) : IAppStatusDiscoveryService
{
    private HashSet<string>? _cachedWinGetPackageIds;

    public void InvalidateCache()
    {
        _cachedWinGetPackageIds = null;
    }

    public async Task<Dictionary<string, bool>> GetInstallationStatusBatchAsync(IEnumerable<ItemDefinition> definitions)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var definitionList = definitions.ToList();

        if (!definitionList.Any()) return result;

        try
        {
            var apps = definitionList.Where(d => d.AppxPackageName?.Length > 0).ToList();
            var capabilities = definitionList.Where(d => !string.IsNullOrEmpty(d.CapabilityName)).ToList();
            var features = definitionList.Where(d => !string.IsNullOrEmpty(d.OptionalFeatureName)).ToList();

            int capCount = 0, featCount = 0, appxCount = 0, wingetCount = 0;

            if (capabilities.Any())
            {
                var capabilityNames = capabilities.Select(c => c.CapabilityName!).ToList();
                var capabilityResults = await CheckCapabilitiesAsync(capabilityNames).ConfigureAwait(false);
                foreach (var capability in capabilities)
                {
                    if (capabilityResults.TryGetValue(capability.CapabilityName!, out bool isInstalled))
                    {
                        result[capability.Id] = isInstalled;
                        if (isInstalled)
                        {
                            capCount++;
                            logService.LogInformation($"Installed (Capability): {capability.Name} ({capability.CapabilityName})");
                        }
                    }
                }
            }

            if (features.Any())
            {
                var featureNames = features.Select(f => f.OptionalFeatureName!).ToList();
                var featureResults = await CheckFeaturesAsync(featureNames).ConfigureAwait(false);
                foreach (var feature in features)
                {
                    if (featureResults.TryGetValue(feature.OptionalFeatureName!, out bool isInstalled))
                    {
                        result[feature.Id] = isInstalled;
                        if (isInstalled)
                        {
                            featCount++;
                            logService.LogInformation($"Installed (Feature): {feature.Name} ({feature.OptionalFeatureName})");
                        }
                    }
                }
            }

            if (apps.Any())
            {
                var installedPackageNames = await appxPackageSource.GetInstalledPackageNamesAsync().ConfigureAwait(false);
                foreach (var app in apps)
                {
                    if (app.AppxPackageName!.Any(name => installedPackageNames.Contains(name)))
                    {
                        result[app.Id] = true;
                        appxCount++;
                        logService.LogInformation($"Installed (AppX): {app.Name} ({string.Join(", ", app.AppxPackageName!)})");
                    }
                }

                // WinGet fallback for apps not found by PackageManager
                var undetectedApps = apps
                    .Where(a => !result.ContainsKey(a.Id) || !result[a.Id])
                    .Where(a => (a.WinGetPackageId != null && a.WinGetPackageId.Any()) || !string.IsNullOrEmpty(a.MsStoreId))
                    .ToList();

                if (undetectedApps.Any())
                {
                    var winGetIds = await GetOrFetchWinGetPackageIdsAsync().ConfigureAwait(false);
                    if (winGetIds != null)
                    {
                        foreach (var app in undetectedApps)
                        {
                            var matchedById = app.WinGetPackageId?.Any(pkgId => winGetIds.Contains(pkgId)) == true;
                            var matchedByStoreId = !string.IsNullOrEmpty(app.MsStoreId) && winGetIds.Contains(app.MsStoreId);
                            if (matchedById || matchedByStoreId)
                            {
                                result[app.Id] = true;
                                wingetCount++;
                                var matchedId = matchedByStoreId
                                    ? app.MsStoreId!
                                    : app.WinGetPackageId!.First(pkgId => winGetIds.Contains(pkgId));
                                logService.LogInformation($"Installed (WinGet): {app.Name} ({matchedId})");
                            }
                        }
                    }
                }

                // Mark remaining unfound apps as not installed
                foreach (var app in apps)
                {
                    if (!result.ContainsKey(app.Id))
                        result[app.Id] = false;
                }
            }

            var notFoundCount = result.Count(kvp => !kvp.Value);
            logService.LogInformation(
                $"Detection complete: {capCount} via Capability, {featCount} via Feature, {appxCount} via AppX, {wingetCount} via WinGet, {notFoundCount} not found");

            return result;
        }
        catch (Exception ex)
        {
            logService.LogError("Error checking batch installation status", ex);
            return definitionList.ToDictionary(d => d.Id, d => false, StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task<Dictionary<string, bool>> GetInstallationStatusByIdAsync(IEnumerable<string> appIds)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var appIdList = appIds.ToList();

        if (!appIdList.Any()) return result;

        try
        {
            var installedPackageNames = await appxPackageSource.GetInstalledPackageNamesAsync().ConfigureAwait(false);
            foreach (var appId in appIdList)
            {
                if (installedPackageNames.Contains(appId))
                {
                    result[appId] = true;
                    logService.LogInformation($"Installed (AppX): {appId}");
                }
                else
                {
                    result[appId] = false;
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            logService.LogError("Error checking installation status by ID", ex);
            return appIdList.ToDictionary(id => id, id => false, StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<Dictionary<string, bool>> CheckCapabilitiesAsync(List<string> capabilities)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        try
        {
            logService.LogInformation($"[DISM-Detect] CheckCapabilitiesAsync: checking {capabilities.Count} capabilities: [{string.Join(", ", capabilities)}]");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var installedCapabilities = await DismSessionManager.ExecuteAsync<HashSet<string>>(session =>
            {
                DismApi.ThrowIfFailed(
                    DismApi.DismGetCapabilities(session, out IntPtr capPtr, out uint count),
                    "GetCapabilities");
                try
                {
                    var allCaps = DismApi.MarshalArray<DismApi.DISM_CAPABILITY>(capPtr, count);

                    var installed = new HashSet<string>(
                        allCaps.Where(c => c.State == DismApi.DismStateInstalled)
                               .Select(c => Marshal.PtrToStringUni(c.Name)!)
                               .Where(n => n != null),
                        StringComparer.OrdinalIgnoreCase);

                    logService.LogInformation($"[DISM-Detect] DismGetCapabilities: {installed.Count} installed out of {count} total");
                    return installed;
                }
                finally
                {
                    DismApi.DismDelete(capPtr);
                }
            }, cts.Token, msg => logService.LogDebug(msg)).ConfigureAwait(false);

            foreach (var capability in capabilities)
            {
                var match = installedCapabilities.Any(c =>
                    c.StartsWith(capability, StringComparison.OrdinalIgnoreCase));
                result[capability] = match;
                logService.LogInformation($"[DISM-Detect] Capability match: '{capability}' => {match}");
            }
        }
        catch (OperationCanceledException)
        {
            logService.LogWarning("[DISM-Detect] CheckCapabilitiesAsync timed out after 30s — DISM may be unresponsive on this system. Marking all capabilities as unknown.");
            foreach (var capability in capabilities)
                result[capability] = false;
        }
        catch (Exception ex)
        {
            logService.LogError($"[DISM-Detect] Error checking capabilities status: {ex.GetType().Name}: {ex.Message}", ex);
            foreach (var capability in capabilities)
                result[capability] = false;
        }

        return result;
    }

    private async Task<Dictionary<string, bool>> CheckFeaturesAsync(List<string> features)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        try
        {
            logService.LogInformation($"[DISM-Detect] CheckFeaturesAsync: checking {features.Count} features: [{string.Join(", ", features)}]");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var enabledFeatures = await DismSessionManager.ExecuteAsync<HashSet<string>>(session =>
            {
                DismApi.ThrowIfFailed(
                    DismApi.DismGetFeatures(session, null, 0, out IntPtr featPtr, out uint count),
                    "GetFeatures");
                try
                {
                    var allFeatures = DismApi.MarshalArray<DismApi.DISM_FEATURE>(featPtr, count);

                    var enabled = new HashSet<string>(
                        allFeatures.Where(f => f.State == DismApi.DismStateInstalled)
                                   .Select(f => Marshal.PtrToStringUni(f.FeatureName)!)
                                   .Where(n => n != null),
                        StringComparer.OrdinalIgnoreCase);

                    logService.LogInformation($"[DISM-Detect] DismGetFeatures: {enabled.Count} enabled out of {count} total");
                    return enabled;
                }
                finally
                {
                    DismApi.DismDelete(featPtr);
                }
            }, cts.Token, msg => logService.LogDebug(msg)).ConfigureAwait(false);

            foreach (var feature in features)
            {
                var match = enabledFeatures.Contains(feature);
                result[feature] = match;
                logService.LogInformation($"[DISM-Detect] Feature match: '{feature}' => {match}");
            }
        }
        catch (OperationCanceledException)
        {
            logService.LogWarning("[DISM-Detect] CheckFeaturesAsync timed out after 30s — DISM may be unresponsive on this system. Marking all features as unknown.");
            foreach (var feature in features)
                result[feature] = false;
        }
        catch (Exception ex)
        {
            logService.LogError($"[DISM-Detect] Error checking features status: {ex.GetType().Name}: {ex.Message}", ex);
            foreach (var feature in features)
                result[feature] = false;
        }

        return result;
    }

    private async Task<HashSet<string>?> GetOrFetchWinGetPackageIdsAsync()
    {
        if (_cachedWinGetPackageIds != null)
            return _cachedWinGetPackageIds;

        try
        {
            bool winGetReady = await winGetBootstrapper.EnsureWinGetReadyAsync().ConfigureAwait(false);
            if (!winGetReady)
            {
                logService.LogWarning("WinGet unavailable - skipping WinGet detection");
                return null;
            }

            _cachedWinGetPackageIds = await winGetDetectionService.GetInstalledPackageIdsAsync().ConfigureAwait(false);
            logService.LogInformation($"WinGet: Fetched {_cachedWinGetPackageIds.Count} installed package IDs");
            return _cachedWinGetPackageIds;
        }
        catch (Exception ex)
        {
            logService.LogWarning($"WinGet detection failed: {ex.Message}");
            return null;
        }
    }

    internal record RegistryUninstallInfo(
        HashSet<string> KeyNames,
        HashSet<string> DisplayNames,
        HashSet<string> AllKeyNames)
    {
        public Dictionary<string, string> IconHintsByName { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests whether input matches a pattern containing {version}, {arch}, {locale} placeholders.
    /// Each placeholder is replaced by a non-greedy wildcard (.+?) for regex matching.
    /// Patterns without placeholders perform exact case-insensitive comparison.
    /// </summary>
    internal static bool MatchesPattern(string input, string pattern)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        var regexPattern = Regex.Escape(pattern)
            .Replace(@"\{version}", ".+?")
            .Replace(@"\{arch}", ".+?")
            .Replace(@"\{locale}", ".+?");
        return Regex.IsMatch(input, $"^{regexPattern}$", RegexOptions.IgnoreCase);
    }

    #region External Apps Detection

    public async Task<Dictionary<string, bool>> GetExternalAppsInstallationStatusAsync(IEnumerable<ItemDefinition> definitions)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var definitionList = definitions.ToList();

        if (!definitionList.Any())
            return result;

        try
        {
            int wingetCount = 0, chocoCount = 0, appxCount = 0, registryCount = 0, fileSystemCount = 0;

            // Fetched lazily below and reused in Phase 4 to avoid a second registry scan.
            RegistryUninstallInfo? sharedRegInfo = null;

            // Phase 1: WinGet detection for apps with WinGetPackageId or MsStoreId
            var appsWithWinGetId = definitionList
                .Where(d => (d.WinGetPackageId != null && d.WinGetPackageId.Any()) || !string.IsNullOrEmpty(d.MsStoreId))
                .ToList();

            if (appsWithWinGetId.Any())
            {
                var winGetIds = await GetOrFetchWinGetPackageIdsAsync().ConfigureAwait(false);

                if (winGetIds != null)
                {
                    // If any Phase 1 app also carries a registry disambiguator, pull the registry
                    // uninstall info up front so we can require winget+registry agreement below.
                    // WinGet's installed-package list can false-positive when a sibling build
                    // (e.g. AutoHotkey v1 installed via direct URL) shares the vendor's uninstall
                    // entry that winget uses to fingerprint "AutoHotkey.AutoHotkey" (v2).
                    bool needRegistryDisambiguation = appsWithWinGetId.Any(d =>
                        !string.IsNullOrEmpty(d.RegistryDisplayName) || !string.IsNullOrEmpty(d.RegistrySubKeyName));
                    if (needRegistryDisambiguation)
                    {
                        sharedRegInfo = await GetRegistryUninstallInfoAsync().ConfigureAwait(false);
                    }

                    foreach (var def in appsWithWinGetId)
                    {
                        var matchedById = def.WinGetPackageId?.Any(pkgId => winGetIds.Contains(pkgId)) == true;
                        var matchedByStoreId = !string.IsNullOrEmpty(def.MsStoreId) && winGetIds.Contains(def.MsStoreId);
                        bool isInstalled = matchedById || matchedByStoreId;

                        // When the definition provides registry fingerprints, require registry
                        // agreement to accept a WinGet match. Rejected items fall through to
                        // later phases rather than being marked installed on a false positive.
                        if (isInstalled && sharedRegInfo != null &&
                            (!string.IsNullOrEmpty(def.RegistryDisplayName) || !string.IsNullOrEmpty(def.RegistrySubKeyName)))
                        {
                            bool registryAgrees =
                                (!string.IsNullOrEmpty(def.RegistryDisplayName)
                                    && sharedRegInfo.DisplayNames.Any(dn => MatchesPattern(dn, def.RegistryDisplayName!)))
                                || (!string.IsNullOrEmpty(def.RegistrySubKeyName)
                                    && sharedRegInfo.AllKeyNames.Any(k => MatchesPattern(k, def.RegistrySubKeyName!)));

                            if (!registryAgrees)
                            {
                                logService.LogInformation(
                                    $"WinGet reported {def.Name} installed, but registry fingerprint did not match - treating as false positive");
                                isInstalled = false;
                            }
                        }

                        result[def.Id] = isInstalled;

                        if (isInstalled)
                        {
                            def.DetectedVia = DetectionSource.WinGet;
                            wingetCount++;
                            var matchedPackageId = matchedByStoreId
                                ? def.MsStoreId!
                                : def.WinGetPackageId!.First(pkgId => winGetIds.Contains(pkgId));
                            logService.LogInformation($"Installed (WinGet): {def.Name} ({matchedPackageId})");
                        }
                    }

                    logService.LogInformation($"WinGet: Checked {appsWithWinGetId.Count} apps");
                }
                else
                {
                    logService.LogWarning("WinGet unavailable - cannot check installation status for apps with WinGetPackageId");
                }
            }

            // Phase 2: Chocolatey detection for apps not found by WinGet
            var appsForChocoCheck = definitionList
                .Where(d => !string.IsNullOrEmpty(d.ChocoPackageId)
                    && (!result.ContainsKey(d.Id) || !result[d.Id]))
                .ToList();

            if (appsForChocoCheck.Any())
            {
                try
                {
                    var chocoPackageIds = await chocolateyService.GetInstalledPackageIdsAsync().ConfigureAwait(false);

                    if (chocoPackageIds.Count > 0)
                    {
                        foreach (var def in appsForChocoCheck)
                        {
                            if (chocoPackageIds.Contains(def.ChocoPackageId!))
                            {
                                result[def.Id] = true;
                                def.DetectedVia = DetectionSource.Chocolatey;
                                chocoCount++;
                                logService.LogInformation($"Installed (Chocolatey): {def.Name} ({def.ChocoPackageId})");
                            }
                        }

                        logService.LogInformation($"Chocolatey: Checked {appsForChocoCheck.Count} apps");
                    }
                    else
                    {
                        logService.LogInformation("Chocolatey not installed or no packages found - skipping Chocolatey detection");
                    }
                }
                catch (Exception ex)
                {
                    logService.LogWarning($"Chocolatey detection failed: {ex.Message}");
                }
            }

            // Phase 3: AppX detection for apps with AppxPackageName
            var appsWithAppxName = definitionList
                .Where(d => d.AppxPackageName?.Length > 0)
                .Where(d => !result.ContainsKey(d.Id) || !result[d.Id])
                .ToList();

            if (appsWithAppxName.Any())
            {
                var installedPackageNames = await appxPackageSource.GetInstalledPackageNamesAsync().ConfigureAwait(false);
                foreach (var def in appsWithAppxName)
                {
                    if (def.AppxPackageName!.Any(name => installedPackageNames.Contains(name)))
                    {
                        result[def.Id] = true;
                        def.DetectedVia = DetectionSource.AppX;
                        appxCount++;
                        logService.LogInformation($"Installed (AppX): {def.Name} ({string.Join(", ", def.AppxPackageName!)})");
                    }
                }
            }

            // Phase 4: Registry fallback for apps not detected via WinGet, Chocolatey, or AppX
            var appsForRegistryCheck = definitionList
                .Where(d => !result.ContainsKey(d.Id) || !result[d.Id])
                .ToList();

            if (appsForRegistryCheck.Any())
            {
                var regInfo = sharedRegInfo ?? await GetRegistryUninstallInfoAsync().ConfigureAwait(false);

                // Pass 1: Exact def.Name match against KeyNames
                foreach (var def in appsForRegistryCheck.Where(d => !result.ContainsKey(d.Id) || !result[d.Id]))
                {
                    if (regInfo.KeyNames.Contains(def.Name))
                    {
                        result[def.Id] = true;
                        def.DetectedVia = DetectionSource.Registry;
                        if (regInfo.IconHintsByName.TryGetValue(def.Name, out var hint1))
                            def.InstalledBinaryHint ??= hint1;
                        registryCount++;
                        logService.LogInformation($"Installed (Registry Pass 1 - KeyName): {def.Name}");
                    }
                }

                // Pass 2: Exact def.Name match against DisplayNames
                foreach (var def in appsForRegistryCheck.Where(d => !result.ContainsKey(d.Id) || !result[d.Id]))
                {
                    if (regInfo.DisplayNames.Contains(def.Name))
                    {
                        result[def.Id] = true;
                        def.DetectedVia = DetectionSource.Registry;
                        if (regInfo.IconHintsByName.TryGetValue(def.Name, out var hint2))
                            def.InstalledBinaryHint ??= hint2;
                        registryCount++;
                        logService.LogInformation($"Installed (Registry Pass 2 - DisplayName): {def.Name}");
                    }
                }

                // Pass 3: RegistrySubKeyName pattern match against AllKeyNames
                foreach (var def in appsForRegistryCheck
                    .Where(d => !string.IsNullOrEmpty(d.RegistrySubKeyName))
                    .Where(d => !result.ContainsKey(d.Id) || !result[d.Id]))
                {
                    var matchedKey3 = regInfo.AllKeyNames.FirstOrDefault(k => MatchesPattern(k, def.RegistrySubKeyName!));
                    if (matchedKey3 != null)
                    {
                        result[def.Id] = true;
                        def.DetectedVia = DetectionSource.Registry;
                        if (regInfo.IconHintsByName.TryGetValue(matchedKey3, out var hint3))
                            def.InstalledBinaryHint ??= hint3;
                        registryCount++;
                        logService.LogInformation($"Installed (Registry Pass 3 - SubKeyName pattern): {def.Name}");
                    }
                }

                // Pass 4: RegistryDisplayName pattern match against DisplayNames
                foreach (var def in appsForRegistryCheck
                    .Where(d => !string.IsNullOrEmpty(d.RegistryDisplayName))
                    .Where(d => !result.ContainsKey(d.Id) || !result[d.Id]))
                {
                    var matchedDn4 = regInfo.DisplayNames.FirstOrDefault(dn => MatchesPattern(dn, def.RegistryDisplayName!));
                    if (matchedDn4 != null)
                    {
                        result[def.Id] = true;
                        def.DetectedVia = DetectionSource.Registry;
                        if (regInfo.IconHintsByName.TryGetValue(matchedDn4, out var hint4))
                            def.InstalledBinaryHint ??= hint4;
                        registryCount++;
                        logService.LogInformation($"Installed (Registry Pass 4 - DisplayName pattern): {def.Name}");
                    }
                }

                // Mark remaining undetected items as false
                foreach (var def in appsForRegistryCheck.Where(d => !result.ContainsKey(d.Id)))
                {
                    result[def.Id] = false;
                }
            }

            // Phase 5: File system path detection for portable apps
            var appsWithDetectionPaths = definitionList
                .Where(d => d.DetectionPaths?.Length > 0)
                .Where(d => !result.ContainsKey(d.Id) || !result[d.Id])
                .ToList();

            if (appsWithDetectionPaths.Any())
            {
                foreach (var def in appsWithDetectionPaths)
                {
                    foreach (var rawPath in def.DetectionPaths!)
                    {
                        var expandedPath = Environment.ExpandEnvironmentVariables(rawPath);
                        if (Directory.Exists(expandedPath) || File.Exists(expandedPath))
                        {
                            result[def.Id] = true;
                            def.DetectedVia = DetectionSource.FileSystem;
                            def.InstalledBinaryHint ??= expandedPath;
                            fileSystemCount++;
                            logService.LogInformation($"Installed (FileSystem): {def.Name} ({expandedPath})");
                            break;
                        }
                    }
                }
            }

            // Phase 6: Hint backfill — for entries detected as installed via WinGet,
            // Chocolatey, or AppX (Phases 1-3), the registry walk in Phase 4 was skipped
            // because they were already marked installed. That left InstalledBinaryHint
            // null even though most of these apps install via standard MSI/EXE installers
            // that DO create a normal uninstall key with DisplayIcon. Look up the hint
            // for any installed entry that's missing one, using the registry data we
            // already loaded (or load it on-demand if no prior phase needed it).
            var entriesNeedingHint = definitionList
                .Where(d => result.TryGetValue(d.Id, out bool installed) && installed
                            && string.IsNullOrEmpty(d.InstalledBinaryHint))
                .ToList();

            if (entriesNeedingHint.Count > 0)
            {
                var regInfo = sharedRegInfo ?? await GetRegistryUninstallInfoAsync().ConfigureAwait(false);
                int hintsBackfilled = 0;

                foreach (var def in entriesNeedingHint)
                {
                    string? hint = null;

                    // Direct name match (the dict is keyed by both KeyName and DisplayName).
                    if (regInfo.IconHintsByName.TryGetValue(def.Name, out var direct))
                    {
                        hint = direct;
                    }
                    // Pattern match by RegistrySubKeyName against KeyNames.
                    else if (!string.IsNullOrEmpty(def.RegistrySubKeyName))
                    {
                        foreach (var kvp in regInfo.IconHintsByName)
                        {
                            if (regInfo.AllKeyNames.Contains(kvp.Key)
                                && MatchesPattern(kvp.Key, def.RegistrySubKeyName!))
                            {
                                hint = kvp.Value;
                                break;
                            }
                        }
                    }
                    // Pattern match by RegistryDisplayName against DisplayNames.
                    if (hint is null && !string.IsNullOrEmpty(def.RegistryDisplayName))
                    {
                        foreach (var kvp in regInfo.IconHintsByName)
                        {
                            if (regInfo.DisplayNames.Contains(kvp.Key)
                                && MatchesPattern(kvp.Key, def.RegistryDisplayName!))
                            {
                                hint = kvp.Value;
                                break;
                            }
                        }
                    }

                    if (hint is not null)
                    {
                        def.InstalledBinaryHint = hint;
                        hintsBackfilled++;
                    }
                }

                if (hintsBackfilled > 0)
                    logService.LogInformation($"Backfilled InstalledBinaryHint for {hintsBackfilled}/{entriesNeedingHint.Count} installed entries detected via WinGet/Chocolatey/AppX");
            }

            var totalFound = result.Count(kvp => kvp.Value);
            logService.LogInformation(
                $"Status check complete: {totalFound}/{definitionList.Count} apps installed ({wingetCount} via WinGet, {chocoCount} via Chocolatey, {appxCount} via AppX, {registryCount} via Registry, {fileSystemCount} via FileSystem)");

            return result;
        }
        catch (Exception ex)
        {
            logService.LogError($"Error checking installation status: {ex.Message}", ex);
            return definitionList.ToDictionary(d => d.Id, d => false, StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<RegistryUninstallInfo> GetRegistryUninstallInfoAsync()
    {
        var keyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var displayNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allKeyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var iconHintsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        await Task.Run(() =>
        {
            // OTS: redirect HKCU to HKU\{interactive user SID} so we read
            // the standard user's uninstall keys, not the admin's.
            var hkcuUninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            RegistryKey hkcuHive;
            string hkcuPath;

            if (interactiveUserService.IsOtsElevation && interactiveUserService.InteractiveUserSid != null)
            {
                hkcuHive = Registry.Users;
                hkcuPath = $@"{interactiveUserService.InteractiveUserSid}\{hkcuUninstallPath}";
            }
            else
            {
                hkcuHive = Registry.CurrentUser;
                hkcuPath = hkcuUninstallPath;
            }

            var registryPaths = new[]
            {
                (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                (hkcuHive, hkcuPath)
            };

            foreach (var (hive, path) in registryPaths)
            {
                try
                {
                    using var key = hive.OpenSubKey(path);
                    if (key == null) continue;

                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        try
                        {
                            using var subKey = key.OpenSubKey(subKeyName);
                            if (subKey == null) continue;

                            // Always add to AllKeyNames (includes SystemComponent=1)
                            allKeyNames.Add(subKeyName);

                            var systemComponent = subKey.GetValue("SystemComponent");
                            if (systemComponent is int value && value == 1)
                                continue;

                            keyNames.Add(subKeyName);

                            var displayName = subKey.GetValue("DisplayName") as string;
                            if (!string.IsNullOrEmpty(displayName))
                                displayNames.Add(displayName);

                            // Capture DisplayIcon (strip ",index" suffix + quotes + env vars)
                            // or fall back to InstallLocation for Layer 1b icon extraction.
                            // Note: InstallLocation is typically a directory; Layer 1b's
                            // BinaryIconSource consumer is responsible for handling both
                            // file-path and directory-path hints (Shell APIs accept both,
                            // though directories return generic folder icons).
                            string? iconHint = (subKey.GetValue("DisplayIcon") as string)
                                           ?? (subKey.GetValue("InstallLocation") as string);
                            if (!string.IsNullOrWhiteSpace(iconHint))
                            {
                                var commaIdx = iconHint.IndexOf(',');
                                if (commaIdx >= 0) iconHint = iconHint.Substring(0, commaIdx);
                                iconHint = Environment.ExpandEnvironmentVariables(iconHint.Trim('"'));

                                if (!string.IsNullOrWhiteSpace(iconHint))
                                {
                                    iconHintsByName[subKeyName] = iconHint;
                                    if (!string.IsNullOrEmpty(displayName) && !iconHintsByName.ContainsKey(displayName))
                                        iconHintsByName[displayName] = iconHint;
                                }
                            }
                        }
                        catch (Exception ex) { logService.LogDebug($"Failed to read registry subkey '{subKeyName}': {ex.Message}"); }
                    }
                }
                catch (Exception ex) { logService.LogDebug($"Failed to open registry key for uninstall enumeration: {ex.Message}"); }
            }
        }).ConfigureAwait(false);

        return new RegistryUninstallInfo(keyNames, displayNames, allKeyNames)
        {
            IconHintsByName = iconHintsByName
        };
    }

    #endregion
}
