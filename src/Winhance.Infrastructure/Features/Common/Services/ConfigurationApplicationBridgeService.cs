using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
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
        switch (ConfigFileMapper.DecodeValue(setting, item))
        {
            case ChoiceValue.PowerPlan p:
                return new Dictionary<string, object> { ["Guid"] = p.Guid, ["Name"] = p.Name };
            case ChoiceValue.CustomValues c:
                return new Dictionary<string, object>(c.Values);
            case ChoiceValue.AcDcOption a:
                return (a.AcIndex, a.DcIndex);
            case ChoiceValue.Option o:
                return o.Index;
            case null when setting.Id == SettingIds.PowerPlanSelection:
                _logService.Log(LogLevel.Error, "Config file is missing PowerPlanGuid for power-plan-selection.");
                throw new InvalidOperationException("Configuration file is invalid or corrupted.");
            default:
                _logService.Log(LogLevel.Warning,
                    $"Config item '{item.Id}' is a selection but carries no resolvable value " +
                    "(no SelectedIndex, PowerSettings, or CustomStateValues); defaulting to option index 0. " +
                    "This usually means a stale toggle-era config entry for a setting that is now a selection.");
                return 0;
        }
    }

    // The file holds powercfg values in SYSTEM units; the apply funnel expects DISPLAY units (it converts back),
    // so convert here exactly as the quick-set path does. Non-powercfg sliders pass through unchanged.
    private static object? ResolveNumericRangeValue(Setting setting, ConfigurationItem item)
    {
        bool isPowerCfg = setting.Targets.OfType<PowerCfgTarget>().Any();
        string? displayUnits = isPowerCfg ? RecommendedSettingsResolver.GetPowerCfgDisplayUnits(setting) : null;
        int Display(int system) => displayUnits is null ? system : RecommendedSettingsResolver.ConvertSystemToDisplayUnits(system, displayUnits);

        return ConfigFileMapper.DecodeValue(setting, item) switch
        {
            ChoiceValue.AcDcNumber n => new Dictionary<string, object?> { ["ACValue"] = Display(n.Ac), ["DCValue"] = Display(n.Dc) },
            ChoiceValue.Number n => Display(n.Value),
            _ => null,
        };
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
