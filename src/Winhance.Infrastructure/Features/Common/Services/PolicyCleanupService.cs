using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class PolicyCleanupService : IPolicyCleanupService
{
    private readonly ICatalogSettingsRegistry _catalogRegistry;
    private readonly IWindowsRegistryService _registryService;
    private readonly ILogService _logService;

    public PolicyCleanupService(
        ICatalogSettingsRegistry catalogRegistry,
        IWindowsRegistryService registryService,
        ILogService logService)
    {
        _catalogRegistry = catalogRegistry;
        _registryService = registryService;
        _logService = logService;
    }

    public int CleanupPolicyKeys()
    {
        var policyKeyPaths = CollectPolicyKeyPaths();

        _logService.Log(LogLevel.Info, $"[PolicyCleanup] Found {policyKeyPaths.Count} unique policy key paths to clean up");

        int deletedCount = 0;
        foreach (var keyPath in policyKeyPaths)
        {
            try
            {
                if (_registryService.KeyExists(keyPath))
                {
                    if (_registryService.DeleteKey(keyPath))
                    {
                        deletedCount++;
                        _logService.Log(LogLevel.Info, $"[PolicyCleanup] Deleted policy key: {keyPath}");
                    }
                    else
                    {
                        _logService.Log(LogLevel.Warning, $"[PolicyCleanup] Failed to delete policy key: {keyPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"[PolicyCleanup] Error deleting policy key '{keyPath}': {ex.Message}");
            }
        }

        _logService.Log(LogLevel.Info, $"[PolicyCleanup] Cleanup complete: {deletedCount} policy keys deleted");
        return deletedCount;
    }

    internal HashSet<string> CollectPolicyKeyPaths()
    {
        var policyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // OS-build-relaxed scope (hardware + existence still apply): policy keys are cleaned up regardless of the
        // current Windows version.
        var allSettings = _catalogRegistry.GetAll(new CatalogScope(IncludeOtherOsVersions: true, IncludeOtherHardware: false));

        foreach (var featureSettings in allSettings.Values)
        {
            foreach (var setting in featureSettings)
            {
                // Group-policy registry keys live on RegTargets (toggles/selections; a mirror carries every
                // Path) and on RegistryWriteEffects (an Action's registry writes are setting-level effects;
                // per-state scanned defensively). The powercfg EnablementKey is a nested RegTarget on
                // PowerCfgTarget, not a top-level Target, so OfType<RegTarget>() correctly excludes it.
                foreach (var target in setting.Targets.OfType<RegTarget>())
                {
                    if (!target.IsGroupPolicy)
                        continue;

                    foreach (var path in target.Paths)
                        if (!string.IsNullOrEmpty(path))
                            policyPaths.Add(path);
                }

                foreach (var effect in setting.Effects.OfType<RegistryWriteEffect>())
                    if (effect.IsGroupPolicy && !string.IsNullOrEmpty(effect.Path))
                        policyPaths.Add(effect.Path);

                foreach (var state in setting.States)
                    foreach (var effect in state.Effects.OfType<RegistryWriteEffect>())
                        if (effect.IsGroupPolicy && !string.IsNullOrEmpty(effect.Path))
                            policyPaths.Add(effect.Path);
            }
        }

        // Deduplicate: if we have both a parent and child path, keep only the parent
        // e.g. keep "...\WindowsUpdate" and remove "...\WindowsUpdate\AU"
        var deduplicatedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in policyPaths.OrderBy(p => p.Length))
        {
            bool isChildOfExisting = deduplicatedPaths.Any(existing =>
                path.StartsWith(existing + @"\", StringComparison.OrdinalIgnoreCase));

            if (!isChildOfExisting)
            {
                deduplicatedPaths.Add(path);
            }
        }

        return deduplicatedPaths;
    }
}
