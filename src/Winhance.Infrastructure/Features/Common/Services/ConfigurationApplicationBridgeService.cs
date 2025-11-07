using System;
using System.Linq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

public class ConfigurationApplicationBridgeService
{
    private readonly ISettingApplicationService _settingApplicationService;
    private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;
    private readonly ILogService _logService;

    public ConfigurationApplicationBridgeService(
        ISettingApplicationService settingApplicationService,
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        ILogService logService)
    {
        _settingApplicationService = settingApplicationService;
        _compatibleSettingsRegistry = compatibleSettingsRegistry;
        _logService = logService;
    }

    public async Task<bool> ApplyConfigurationSectionAsync(
        ConfigSection section,
        string sectionName,
        Func<string, object?, SettingDefinition, Task<(bool confirmed, bool checkboxResult)>>? confirmationHandler = null)
    {
        if (section?.Items == null || !section.Items.Any())
        {
            _logService.Log(LogLevel.Warning, $"Section '{sectionName}' is empty or null");
            return false;
        }

        _logService.Log(LogLevel.Info, $"Applying {section.Items.Count} settings from {sectionName} section");

        var waves = BuildDependencyWaves(section.Items);
        _logService.Log(LogLevel.Info, $"Organized {section.Items.Count} settings into {waves.Count} parallel wave(s)");

        int appliedCount = 0;
        int skippedOsCount = 0;
        int failCount = 0;

        foreach (var wave in waves)
        {
            var tasks = wave.Select(tuple => ApplySettingItemAsync(tuple.item, tuple.setting, confirmationHandler));
            var results = await Task.WhenAll(tasks);

            foreach (var result in results)
            {
                switch (result.status)
                {
                    case ApplyStatus.Applied:
                        appliedCount++;
                        break;
                    case ApplyStatus.SkippedOsIncompatible:
                        skippedOsCount++;
                        break;
                    case ApplyStatus.Failed:
                        failCount++;
                        break;
                }
            }

            _logService.Log(LogLevel.Debug, $"Wave completed: {results.Count(r => r.status == ApplyStatus.Applied)}/{wave.Count} applied");
        }

        if (skippedOsCount > 0)
        {
            _logService.Log(LogLevel.Info,
                $"Section '{sectionName}': {appliedCount} applied, {skippedOsCount} skipped (OS incompatible), {failCount} failed");
        }
        else
        {
            _logService.Log(LogLevel.Info,
                $"Section '{sectionName}': {appliedCount} applied, {failCount} failed");
        }

        return failCount == 0;
    }

    private object ResolveSelectionValue(SettingDefinition setting, ConfigurationItem item)
    {
        if (setting.Id == "power-plan-selection")
        {
            return ResolvePowerPlanValue(setting, item);
        }

        if (item.CustomStateValues != null && item.CustomStateValues.Count > 0)
        {
            return item.CustomStateValues;
        }

        if (item.PowerSettings != null &&
            item.PowerSettings.ContainsKey("ACIndex") &&
            item.PowerSettings.ContainsKey("DCIndex"))
        {
            var acIndex = Convert.ToInt32(item.PowerSettings["ACIndex"]);
            var dcIndex = Convert.ToInt32(item.PowerSettings["DCIndex"]);
            return (acIndex, dcIndex);
        }

        if (item.SelectedIndex.HasValue)
        {
            return item.SelectedIndex.Value;
        }

        return 0;
    }

    private object ResolvePowerPlanValue(SettingDefinition setting, ConfigurationItem item)
    {
        if (!string.IsNullOrEmpty(item.PowerPlanGuid))
        {
            return new Dictionary<string, object>
            {
                ["Guid"] = item.PowerPlanGuid,
                ["Name"] = item.PowerPlanName ?? "Unknown"
            };
        }

        _logService.Log(LogLevel.Error, "Config file is missing PowerPlanGuid for power-plan-selection.");
        throw new InvalidOperationException("Configuration file is invalid or corrupted.");
    }

    private object ResolveNumericRangeValue(ConfigurationItem item)
    {
        if (item.PowerSettings == null || item.PowerSettings.Count == 0)
            return null;

        var hasAcValue = item.PowerSettings.TryGetValue("ACValue", out var acVal);
        var hasDcValue = item.PowerSettings.TryGetValue("DCValue", out var dcVal);

        if (hasAcValue || hasDcValue)
        {
            return new Dictionary<string, object?>
            {
                ["ACValue"] = acVal,
                ["DCValue"] = dcVal ?? acVal
            };
        }

        if (item.PowerSettings.TryGetValue("Value", out var singleVal))
        {
            return singleVal;
        }

        return null;
    }

    private enum ApplyStatus
    {
        Applied,
        SkippedOsIncompatible,
        Failed
    }

    private List<List<(ConfigurationItem item, SettingDefinition setting)>> BuildDependencyWaves(List<ConfigurationItem> items)
    {
        var waves = new List<List<(ConfigurationItem, SettingDefinition)>>();
        var processedIds = new HashSet<string>();
        var remainingItems = new List<(ConfigurationItem item, SettingDefinition setting)>();

        var allSettings = _compatibleSettingsRegistry.GetAllFilteredSettings();

        foreach (var item in items)
        {
            if (string.IsNullOrEmpty(item.Id))
                continue;

            var setting = FindSettingById(item.Id, allSettings);
            if (setting != null)
            {
                remainingItems.Add((item, setting));
            }
        }

        while (remainingItems.Any())
        {
            var currentWave = new List<(ConfigurationItem, SettingDefinition)>();

            foreach (var (item, setting) in remainingItems.ToList())
            {
                var dependencies = setting.Dependencies?
                    .Where(d => d.DependencyType != SettingDependencyType.RequiresValueBeforeAnyChange)
                    .Select(d => d.RequiredSettingId)
                    .ToList() ?? new List<string>();

                bool canProcess = dependencies.All(depId => processedIds.Contains(depId));

                if (canProcess)
                {
                    currentWave.Add((item, setting));
                    processedIds.Add(item.Id);
                    remainingItems.Remove((item, setting));
                }
            }

            if (!currentWave.Any() && remainingItems.Any())
            {
                var circularSettingIds = string.Join(", ", remainingItems.Select(x => x.setting.Id));
                _logService.Log(LogLevel.Warning, $"Circular dependency detected in settings: {circularSettingIds}. Processing anyway.");
                currentWave.AddRange(remainingItems);
                remainingItems.Clear();
            }

            if (currentWave.Any())
            {
                waves.Add(currentWave);
            }
        }

        return waves;
    }

    private async Task<(ApplyStatus status, string itemName)> ApplySettingItemAsync(
        ConfigurationItem item,
        SettingDefinition setting,
        Func<string, object?, SettingDefinition, Task<(bool confirmed, bool checkboxResult)>>? confirmationHandler)
    {
        try
        {
            if (string.IsNullOrEmpty(item.Id))
            {
                _logService.Log(LogLevel.Warning, $"Skipping item '{item.Name}' - no ID");
                return (ApplyStatus.Failed, item.Name);
            }

            if (setting == null)
            {
                _logService.Log(LogLevel.Debug, $"Setting '{item.Id}' skipped (not compatible with this Windows version)");
                return (ApplyStatus.SkippedOsIncompatible, item.Name);
            }

            bool checkboxResult = false;
            if (setting.RequiresConfirmation && confirmationHandler != null)
            {
                var value = setting.InputType == InputType.Selection
                    ? (object)ResolveSelectionValue(setting, item)
                    : (object)(item.IsSelected ?? false);

                var (confirmed, checkbox) = await confirmationHandler(item.Id, value, setting);

                if (!confirmed)
                {
                    _logService.Log(LogLevel.Info, $"User skipped setting '{item.Id}' during config import");
                    return (ApplyStatus.Applied, item.Name);
                }

                checkboxResult = checkbox;
            }

            object valueToApply = null;

            if (setting.InputType == InputType.Selection)
            {
                valueToApply = ResolveSelectionValue(setting, item);
            }
            else if (setting.InputType == InputType.NumericRange)
            {
                valueToApply = ResolveNumericRangeValue(item);
            }

            if (setting.InputType == InputType.Action && !string.IsNullOrEmpty(setting.ActionCommand))
            {
                await _settingApplicationService.ApplySettingAsync(
                    item.Id,
                    false,
                    null,
                    checkboxResult,
                    setting.ActionCommand,
                    skipValuePrerequisites: true);
            }
            else
            {
                await _settingApplicationService.ApplySettingAsync(
                    item.Id,
                    item.IsSelected ?? false,
                    valueToApply,
                    checkboxResult,
                    skipValuePrerequisites: true);
            }

            _logService.Log(LogLevel.Debug, $"Applied setting: {item.Name}");
            return (ApplyStatus.Applied, item.Name);
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Failed to apply setting '{item.Name}': {ex.Message}");
            return (ApplyStatus.Failed, item.Name);
        }
    }

    private SettingDefinition FindSettingById(string id, IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> allSettings)
    {
        foreach (var featureSettings in allSettings.Values)
        {
            var setting = featureSettings.FirstOrDefault(s => s.Id == id);
            if (setting != null)
                return setting;
        }
        return null;
    }
}
