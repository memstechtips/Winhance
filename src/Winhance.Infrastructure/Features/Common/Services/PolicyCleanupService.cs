using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

public class PolicyCleanupService : IPolicyCleanupService
{
    private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;
    private readonly IWindowsRegistryService _registryService;
    private readonly ILogService _logService;

    public PolicyCleanupService(
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        IWindowsRegistryService registryService,
        ILogService logService)
    {
        _compatibleSettingsRegistry = compatibleSettingsRegistry;
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

        var allSettings = _compatibleSettingsRegistry.GetAllBypassedSettings();

        foreach (var featureSettings in allSettings.Values)
        {
            foreach (var setting in featureSettings)
            {
                if (setting.RegistrySettings == null)
                    continue;

                foreach (var regSetting in setting.RegistrySettings)
                {
                    if (!regSetting.IsGroupPolicy || string.IsNullOrEmpty(regSetting.KeyPath))
                        continue;

                    policyPaths.Add(regSetting.KeyPath);
                }
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
