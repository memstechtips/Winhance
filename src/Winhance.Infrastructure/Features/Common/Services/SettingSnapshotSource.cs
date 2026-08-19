using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;

namespace Winhance.Infrastructure.Features.Common.Services;

internal sealed class SettingSnapshotSource : ISettingSnapshotSource
{
    private readonly ICatalogSettingsRegistry _registry;
    private readonly ICatalogSettingStateProvider _states;
    private readonly ILogService _log;

    public SettingSnapshotSource(ICatalogSettingsRegistry registry, ICatalogSettingStateProvider states, ILogService log)
    {
        _registry = registry;
        _states = states;
        _log = log;
    }

    public async Task<IReadOnlyList<SettingChoice>> CaptureAsync(CatalogScope scope)
    {
        await _registry.InitializeAsync().ConfigureAwait(false);
        var choices = new List<SettingChoice>();

        foreach (var (featureId, settings) in _registry.GetAll(includeOtherOsVersions: scope.IncludeOtherOsVersions))
        {
            if (settings.Count == 0) continue;
            if (!FeatureDefinitions.OptimizeFeatures.Contains(featureId) && !FeatureDefinitions.CustomizeFeatures.Contains(featureId))
            {
                _log.Log(LogLevel.Warning, $"Feature {featureId} is neither Optimize nor Customize, skipping");
                continue;
            }

            var states = await _states.GetStatesAsync(settings).ConfigureAwait(false);
            foreach (var setting in settings)
            {
                if (!states.TryGetValue(setting.Id, out var state)) continue;
                if (Choose(setting, state) is { } value)
                    choices.Add(new SettingChoice(setting.Id, value));
            }
        }

        _log.Log(LogLevel.Info, $"Snapshot captured {choices.Count} setting choices");
        return choices;
    }

    private static ChoiceValue? Choose(Setting setting, SettingStateResult state)
    {
        var powerCfg = setting.Targets.OfType<PowerCfgTarget>().FirstOrDefault();
        bool separate = powerCfg?.Mode == PowerModeSupport.Separate;

        switch (setting.Control)
        {
            case ControlKind.Toggle:
            case ControlKind.Action:
                return new ChoiceValue.Toggle(state.IsEnabled);

            case ControlKind.PowerPlan:
                return string.IsNullOrEmpty(state.DynamicSelection)
                    ? null
                    : new ChoiceValue.PowerPlan(state.DynamicSelection, state.DynamicSelectionName ?? "Unknown");

            case ControlKind.Selection:
                if (separate && (state.AcValue is not null || state.DcValue is not null))
                    return new ChoiceValue.AcDcOption(IndexOfPowerValue(setting, powerCfg!, state.AcValue), IndexOfPowerValue(setting, powerCfg!, state.DcValue));
                if (state.CurrentValue is not int index)
                    return new ChoiceValue.Option(0);
                if (index == ComboBoxConstants.CustomStateIndex)
                {
                    var custom = new Dictionary<string, object>();
                    if (state.Readings is not null)
                    {
                        foreach (var key in setting.Targets.OfType<RegTarget>().Select(rt => rt.ValueName ?? "KeyExists"))
                        {
                            if (state.Readings.TryGetValue(key, out var v) && v is not null)
                                custom[key] = v;
                        }
                    }
                    return custom.Count > 0 ? new ChoiceValue.CustomValues(custom) : null;
                }
                return new ChoiceValue.Option(index);

            case ControlKind.Slider:
                if (state.CurrentValue is null) return null;
                if (separate)
                {
                    // A desktop reads no DC value; the file has always carried the AC value for both contexts then.
                    return state.AcValue is { } ac
                        ? new ChoiceValue.AcDcNumber(ac, state.DcValue ?? ac)
                        : null;
                }
                return new ChoiceValue.Number(Convert.ToInt32(state.CurrentValue));

            default:
                return null;
        }
    }

    // A Separate-mode powercfg selection detects a raw AC/DC value; the file stores the option index whose
    // Set payload for the powercfg key equals it (the catalog authors one state per option).
    private static int IndexOfPowerValue(Setting setting, PowerCfgTarget powerCfg, int? value)
    {
        if (value is not { } v) return 0;
        for (int i = 0; i < setting.States.Count; i++)
        {
            if (setting.States[i].Set.TryGetValue(powerCfg.Key, out var sv) && sv.WritePayload is { } p && Convert.ToInt32(p) == v)
                return i;
        }
        return 0;
    }
}
