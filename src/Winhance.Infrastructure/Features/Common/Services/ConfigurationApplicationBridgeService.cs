using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Helpers;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class ConfigurationApplicationBridgeService : IConfigurationApplicationBridgeService
{
    private readonly ISettingApplicationService _settingApplicationService;
    private readonly ICatalogSettingsRegistry _catalogSettingsRegistry;
    private readonly ILogService _logService;
    private readonly IConfigImportState _configImportState;

    public ConfigurationApplicationBridgeService(
        ISettingApplicationService settingApplicationService,
        ICatalogSettingsRegistry catalogSettingsRegistry,
        ILogService logService,
        IConfigImportState configImportState)
    {
        _settingApplicationService = settingApplicationService;
        _catalogSettingsRegistry = catalogSettingsRegistry;
        _logService = logService;
        _configImportState = configImportState;
    }

    public async Task<bool> ApplyConfigurationSectionAsync(
        ConfigSection section,
        string sectionName,
        Func<string, object?, Task<(bool confirmed, bool checkboxResult)>>? confirmationHandler = null)
    {
        if (section?.Items == null || !section.Items.Any())
        {
            _logService.Log(LogLevel.Warning, $"Section '{sectionName}' is empty or null");
            return false;
        }

        _logService.Log(LogLevel.Info, $"Applying {section.Items.Count} settings from {sectionName} section");

        // If this section carries individual PowerCfg-backed items alongside the power-plan
        // selection, mark the import as the source of truth for power values. The apply funnel
        // (SettingApplicationService) reads this flag and skips the recommended re-apply after a
        // power-plan activation, which would otherwise duplicate (and race with) these individual
        // items in the same wave. Only set true here; the import orchestrators reset it (other
        // sections run in parallel).
        if (section.Items.Any(i =>
                !string.IsNullOrEmpty(i.Id) &&
                i.Id != SettingIds.PowerPlanSelection &&
                i.PowerSettings != null))
        {
            _configImportState.ImportSuppliesPowerValues = true;
        }

        var waves = BuildDependencyWaves(section.Items);
        _logService.Log(LogLevel.Info, $"Organized {section.Items.Count} settings into {waves.Count} parallel wave(s)");

        int appliedCount = 0;
        int skippedOsCount = 0;
        int failCount = 0;

        foreach (var wave in waves)
        {
            var tasks = wave.Select(tuple => ApplySettingItemAsync(tuple.item, tuple.setting, confirmationHandler));
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

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

    private object ResolveSelectionValue(Setting setting, ConfigurationItem item)
    {
        if (setting.Id == SettingIds.PowerPlanSelection)
        {
            return ResolvePowerPlanValue(item);
        }

        if (item.CustomStateValues != null && item.CustomStateValues.Count > 0)
        {
            return item.CustomStateValues;
        }

        if (item.PowerSettings != null &&
            item.PowerSettings.TryGetValue("ACIndex", out var acRaw) &&
            item.PowerSettings.TryGetValue("DCIndex", out var dcRaw))
        {
            var acIndex = Convert.ToInt32(UnwrapJsonElement(acRaw));
            var dcIndex = Convert.ToInt32(UnwrapJsonElement(dcRaw));
            return (acIndex, dcIndex);
        }

        if (item.SelectedIndex.HasValue)
        {
            return item.SelectedIndex.Value;
        }

        _logService.Log(LogLevel.Warning,
            $"Config item '{item.Id}' is a selection but carries no resolvable value " +
            "(no SelectedIndex, PowerSettings, or CustomStateValues); defaulting to option index 0. " +
            "This usually means a stale toggle-era config entry for a setting that is now a selection.");
        return 0;
    }

    private object ResolvePowerPlanValue(ConfigurationItem item)
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

    private object? ResolveNumericRangeValue(Setting setting, ConfigurationItem item)
    {
        if (item.PowerSettings == null || item.PowerSettings.Count == 0)
            return null;

        // PowerCfg-backed NumericRange settings are exported in SYSTEM units (raw powercfg
        // reads, e.g. seconds). PowerCfgApplier treats incoming dict/scalar values as DISPLAY
        // units and converts display→system itself, so we must convert system→display here —
        // exactly as RecommendedSettingsResolver.BuildPowerCfgApplyValue does for the manual
        // quick-set path. Non-PowerCfg NumericRange settings carry no PowerCfgTarget and pass
        // through unchanged.
        bool isPowerCfg = setting.Targets.OfType<PowerCfgTarget>().Any();
        string? displayUnits = isPowerCfg ? RecommendedSettingsResolver.GetPowerCfgDisplayUnits(setting) : null;

        var hasAcValue = item.PowerSettings.TryGetValue("ACValue", out var acVal);
        var hasDcValue = item.PowerSettings.TryGetValue("DCValue", out var dcVal);

        if (hasAcValue || hasDcValue)
        {
            return new Dictionary<string, object?>
            {
                ["ACValue"] = ConvertToDisplayIfPowerCfg(UnwrapJsonElement(acVal), isPowerCfg, displayUnits),
                ["DCValue"] = ConvertToDisplayIfPowerCfg(UnwrapJsonElement(dcVal ?? acVal), isPowerCfg, displayUnits)
            };
        }

        if (item.PowerSettings.TryGetValue("Value", out var singleVal))
        {
            return ConvertToDisplayIfPowerCfg(UnwrapJsonElement(singleVal), isPowerCfg, displayUnits);
        }

        return null;
    }

    private static object? ConvertToDisplayIfPowerCfg(object? value, bool isPowerCfg, string? displayUnits)
    {
        if (!isPowerCfg || value == null)
            return value;

        try
        {
            int systemValue = Convert.ToInt32(value);
            return RecommendedSettingsResolver.ConvertSystemToDisplayUnits(systemValue, displayUnits);
        }
        catch
        {
            return value;
        }
    }

    private static object? UnwrapJsonElement(object? value)
    {
        if (value is System.Text.Json.JsonElement je)
        {
            return je.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Number when je.TryGetInt32(out var i) => i,
                System.Text.Json.JsonValueKind.Number when je.TryGetInt64(out var l) => l,
                System.Text.Json.JsonValueKind.Number when je.TryGetDouble(out var d) => d,
                System.Text.Json.JsonValueKind.String => je.GetString(),
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                _ => value
            };
        }
        return value;
    }

    private enum ApplyStatus
    {
        Applied,
        SkippedOsIncompatible,
        Failed
    }

    private List<List<(ConfigurationItem item, Setting setting)>> BuildDependencyWaves(IReadOnlyList<ConfigurationItem> items)
    {
        var waves = new List<List<(ConfigurationItem, Setting)>>();
        var processedIds = new HashSet<string>();
        var remainingItems = new List<(ConfigurationItem item, Setting setting)>();

        foreach (var item in items)
        {
            if (string.IsNullOrEmpty(item.Id))
                continue;

            // Pair via the registry (alias-normalized + current-OS/hardware/existence scoped). A miss
            // drops silently.
            var setting = _catalogSettingsRegistry.GetById(item.Id);
            if (setting != null)
            {
                remainingItems.Add((item, setting));
            }
        }

        while (remainingItems.Count > 0)
        {
            var currentWave = new List<(ConfigurationItem, Setting)>();

            foreach (var (item, setting) in remainingItems.ToList())
            {
                var dependencies = GetWaveDependencyIds(setting);

                bool canProcess = dependencies.All(depId => processedIds.Contains(depId));

                if (canProcess)
                {
                    currentWave.Add((item, setting));
                    processedIds.Add(item.Id);
                    remainingItems.Remove((item, setting));
                }
            }

            if (currentWave.Count == 0 && remainingItems.Count > 0)
            {
                var circularSettingIds = string.Join(", ", remainingItems.Select(x => x.setting.Id));
                _logService.Log(LogLevel.Warning, $"Circular dependency detected in settings: {circularSettingIds}. Processing anyway.");
                currentWave.AddRange(remainingItems);
                remainingItems.Clear();
            }

            if (currentWave.Count > 0)
            {
                waves.Add(currentWave);
            }
        }

        return waves;
    }

    private static List<string> GetWaveDependencyIds(Setting setting)
        => setting.States
            .SelectMany(st => st.Links)
            .Where(l => l.Kind == LinkKind.Requires)
            .Select(l => l.OtherId)
            .Distinct()
            .ToList();

    private async Task<(ApplyStatus status, string itemName)> ApplySettingItemAsync(
        ConfigurationItem item,
        Setting setting,
        Func<string, object?, Task<(bool confirmed, bool checkboxResult)>>? confirmationHandler)
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

            // Control.PowerPlan routes through the Selection value path: the bridge does NOT skip power-plan-selection.
            bool requiresConfirmation = setting.Apply.RequiresConfirmation;
            bool isSelection = setting.Control is ControlKind.Selection or ControlKind.PowerPlan;
            bool isNumericRange = setting.Control == ControlKind.Slider;
            bool isAction = setting.Control == ControlKind.Action;

            bool checkboxResult = false;
            if (requiresConfirmation && confirmationHandler != null)
            {
                var value = isSelection
                    ? (object)ResolveSelectionValue(setting, item)
                    : (object)(item.IsSelected ?? false);

                var (confirmed, checkbox) = await confirmationHandler(item.Id, value).ConfigureAwait(false);

                if (!confirmed)
                {
                    _logService.Log(LogLevel.Info, $"User skipped setting '{item.Id}' during config import");
                    return (ApplyStatus.Applied, item.Name);
                }

                checkboxResult = checkbox;
            }

            object? valueToApply = null;

            if (isSelection)
            {
                valueToApply = ResolveSelectionValue(setting, item);
            }
            else if (isNumericRange)
            {
                valueToApply = ResolveNumericRangeValue(setting, item);
            }

            if (isAction)
            {
                // Action settings only apply when explicitly selected. An unselected Action has
                // no "reverse" semantic — falling through with Enable=false would write
                // DisabledValue (delete the keys the action set), which is destructive.
                if (!(item.IsSelected ?? false))
                {
                    _logService.Log(LogLevel.Debug, $"Skipping unselected Action setting: {item.Name}");
                    return (ApplyStatus.Applied, item.Name);
                }

                // Enable=true matches the runtime button-click flow (RunActionAsync).
                await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                {
                    SettingId = item.Id,
                    Enable = true,
                    CheckboxResult = checkboxResult,
                    SkipValuePrerequisites = true
                }).ConfigureAwait(false);
            }
            else
            {
                await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                {
                    SettingId = item.Id,
                    Enable = item.IsSelected ?? false,
                    Value = valueToApply,
                    CheckboxResult = checkboxResult,
                    SkipValuePrerequisites = true
                }).ConfigureAwait(false);
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

}
