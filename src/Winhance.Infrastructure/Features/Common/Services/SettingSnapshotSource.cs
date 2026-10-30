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
    private readonly IOptionProviderRegistry _options;
    private readonly IFileStore _files;
    private readonly IHardwareDetectionService _hardware;
    private readonly ISystemDetectionContextFactory _contexts;

    public SettingSnapshotSource(ICatalogSettingsRegistry registry, ICatalogSettingStateProvider states, ILogService log, IOptionProviderRegistry options, IFileStore files, IHardwareDetectionService hardware, ISystemDetectionContextFactory contexts)
    {
        _registry = registry;
        _states = states;
        _log = log;
        _options = options;
        _files = files;
        _hardware = hardware;
        _contexts = contexts;
    }

    public async Task<IReadOnlyList<SettingChoice>> CaptureAsync(CatalogScope scope)
    {
        await _registry.InitializeAsync().ConfigureAwait(false);
        var choices = new List<SettingChoice>();

        foreach (var (featureId, settings) in _registry.GetAll(scope))
        {
            if (settings.Count == 0) continue;
            if (!FeatureDefinitions.IsSettingsFeature(featureId)
                && !FeatureDefinitions.AutounattendFeatures.Contains(featureId))
            {
                _log.Log(LogLevel.Warning, $"Feature {featureId} is not Optimize, Customize or Autounattend, skipping");
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

    public async Task<IReadOnlyDictionary<string, SettingMatch>> CompareAsync(IReadOnlyList<SettingChoice> wanted, IReadOnlyList<CarriedFile> carried, CatalogScope scope)
    {
        await _registry.InitializeAsync().ConfigureAwait(false);
        var settings = wanted
            .Select(choice => (choice, setting: _registry.GetById(SettingIdAliases.Normalize(choice.SettingId), scope)))
            .Where(pair => pair.setting is not null)
            .ToList();
        var states = await _states.GetStatesAsync(settings.Select(pair => pair.setting!).ToList()).ConfigureAwait(false);

        var result = new Dictionary<string, SettingMatch>(StringComparer.Ordinal);
        foreach (var (choice, setting) in settings)
        {
            var file = carried.FirstOrDefault(f => f.SettingId == choice.SettingId)?.Base64;
            result[choice.SettingId] = states.TryGetValue(setting!.Id, out var state)
                ? await MatchAsync(setting, state, choice.Value, file).ConfigureAwait(false)
                : SettingMatch.Unknown;
        }
        return result;
    }

    // Matches lets a profile switch skip the setting, so only a clean reading of one declared state may say it.
    // A catch-all fallback can stand for values Winhance does not recognise, a Selection that read no index would
    // come back as option 0, and an Action has no state to read.
    internal async Task<SettingMatch> MatchAsync(Setting setting, SettingStateResult state, ChoiceValue wanted, string? carriedBase64)
    {
        if (setting.Control == ControlKind.Action)
            return SettingMatch.Unknown;
        // These two compare the raw readings and the adapter, not a declared state, and servers set by hand match
        // no declared state, so they come before the clean-reading rule.
        if (wanted is ChoiceValue.CustomValues custom)
            return state.Outcome is SettingDetectionOutcome.Resolved or SettingDetectionOutcome.Custom && state.Readings is { } readings
                ? ConfigFileMapper.CustomValuesEqual(custom.Values, readings) ? SettingMatch.Matches : SettingMatch.Differs
                : SettingMatch.Unknown;
        // Applying automatic DNS keeps the servers of an adapter with a manual IP, so that is as far as it gets.
        if (wanted is ChoiceValue.Option { Index: var auto } && IsAutomaticDns(setting, auto) && _contexts.Create().ActiveAdapterHasManualIp())
            return SettingMatch.Matches;
        if (state.Outcome != SettingDetectionOutcome.Resolved || state.CatchAll)
            return SettingMatch.Unknown;
        if (setting.Control == ControlKind.Selection && state.CurrentValue is not int && state.AcValue is null)
            return SettingMatch.Unknown;
        // A state that writes nothing leaves whatever the machine had, so no reading can show it was reached.
        if (wanted is ChoiceValue.Option { Index: var index }
            && setting.States.ElementAtOrDefault(index) is { Set.Count: 0, Effects.Count: 0, ResetSet: null })
            return SettingMatch.Unknown;

        var reading = Choose(setting, state);
        // PowerCfgApplier writes no DC value on a machine without a battery, so the DC half reads back whatever the
        // plan already held.
        if (reading is not null && _hardware.HasBattery() == false)
            (reading, wanted) = (AcOnly(reading), AcOnly(wanted));

        return (reading, wanted) switch
        {
            (null or ChoiceValue.CustomValues or ChoiceValue.List, _) or (_, ChoiceValue.List) => SettingMatch.Unknown,
            (ChoiceValue.Keyed read, ChoiceValue.Keyed want) =>
                await KeyedChoiceMatch.IsSameAsync(_options, _files, setting, want, read, carriedBase64).ConfigureAwait(false) ? SettingMatch.Matches : SettingMatch.Differs,
            var (read, want) => Same(read, want) ? SettingMatch.Matches : SettingMatch.Differs,
        };
    }

    // A file can hold one value where the machine reads separate AC and DC values, or the other way round; the
    // import writes the single value to both, so that is the reading it leaves behind.
    private static bool Same(ChoiceValue read, ChoiceValue want) => (read, want) switch
    {
        (ChoiceValue.AcDcOption r, ChoiceValue.Option w) => r.AcIndex == w.Index && r.DcIndex == w.Index,
        (ChoiceValue.Option r, ChoiceValue.AcDcOption w) => r.Index == w.AcIndex && r.Index == w.DcIndex,
        (ChoiceValue.AcDcNumber r, ChoiceValue.Number w) => r.Ac == w.Value && r.Dc == w.Value,
        (ChoiceValue.Number r, ChoiceValue.AcDcNumber w) => r.Value == w.Ac && r.Value == w.Dc,
        _ => Equals(read, want),
    };

    private static bool IsAutomaticDns(Setting setting, int index) =>
        setting.Targets.OfType<DnsTarget>().Any()
        && setting.States.ElementAtOrDefault(index) is { Set.Count: > 0 } state
        && state.Set.Values.All(v => v == StateValue.Absent);

    private static ChoiceValue AcOnly(ChoiceValue value) => value switch
    {
        ChoiceValue.AcDcOption v => new ChoiceValue.Option(v.AcIndex),
        ChoiceValue.AcDcNumber v => new ChoiceValue.Number(v.Ac),
        _ => value,
    };

    private static ChoiceValue? Choose(Setting setting, SettingStateResult state)
    {
        // A setting Winhance could not read has no intent to record. Writing one anyway lands as an explicit
        // "off" in the file and actively disables it on the next machine, which is what Undetermined means the
        // app must not do: applying would write blind over data it could not read. Custom and Malformed are
        // both actionable readings and stay.
        if (!state.Success || state.Outcome == SettingDetectionOutcome.Undetermined)
            return null;

        var powerCfg = setting.Targets.OfType<PowerCfgTarget>().FirstOrDefault();
        bool separate = powerCfg?.Mode == PowerModeSupport.Separate;

        switch (setting.Control)
        {
            case ControlKind.Toggle:
            case ControlKind.Action:
                return new ChoiceValue.Toggle(state.IsEnabled);

            case ControlKind.CheckBox:
                return new ChoiceValue.CheckBox(state.IsEnabled);

            case ControlKind.KeyedSelection:
                // Saved with the key so a machine that does not offer it can still name it in the review diff.
                if (string.IsNullOrEmpty(state.DynamicSelection))
                    return null;
                var offered = state.DynamicOptions?
                    .FirstOrDefault(o => string.Equals(o.Value, state.DynamicSelection, StringComparison.OrdinalIgnoreCase));
                return new ChoiceValue.Keyed(state.DynamicSelection, offered?.Label ?? state.DynamicSelection);

            case ControlKind.Selection:
                if (separate && (state.AcValue is not null || state.DcValue is not null))
                {
                    // A desktop reads no DC value. Option 0 is a real option - on a TimeIntervals-backed
                    // setting it is "Never" - so a context that read nothing carries the AC option instead,
                    // the way the slider does.
                    return IndexOfPowerValue(setting, powerCfg!, state.AcValue) is { } acIndex
                        ? new ChoiceValue.AcDcOption(acIndex, IndexOfPowerValue(setting, powerCfg!, state.DcValue) ?? acIndex)
                        : null;
                }
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
                    if (custom.Count > 0)
                        return new ChoiceValue.CustomValues(custom);

                    // A setting with no RegTarget has nothing to key by ValueName, so the loop above finds
                    // nothing; typed DNS servers sit in the readings under their targets' keys instead.
                    // DetectedIndex is dropped on purpose: it says only "this matched no option", which is
                    // not portable. Carrying it would hand the system-tray setting a Custom choice whose
                    // script promotes every icon - the "Show all icons" option, not the state being captured.
                    var portable = CustomStateValueReconstructor.Build(setting, state)
                        .Where(kv => kv.Value is not null && kv.Key != "DetectedIndex")
                        .ToDictionary(kv => kv.Key, kv => kv.Value!);
                    return portable.Count > 0 ? new ChoiceValue.CustomValues(portable) : null;
                }
                // A preset spells out only its own keys (DNS servers); the machine's other field values (encryption)
                // would be lost under a bare index, so the file carries every reading instead.
                if (setting.Fields.Count > 0 && state.Readings is { } readings && index >= 0 && index < setting.States.Count
                    && readings.Any(kv => kv.Value is not null && !setting.States[index].Set.ContainsKey(kv.Key)))
                {
                    return new ChoiceValue.CustomValues(readings.Where(kv => kv.Value is not null).ToDictionary(kv => kv.Key, kv => kv.Value!));
                }
                return new ChoiceValue.Option(index);

            case ControlKind.Slider:
                if (state.CurrentValue is null) return null;
                if (separate)
                {
                    // A desktop reads no DC value; the file carries the AC value for both contexts then.
                    return state.AcValue is { } ac
                        ? new ChoiceValue.AcDcNumber(ac, state.DcValue ?? ac)
                        : null;
                }
                return new ChoiceValue.Number(Convert.ToInt32(state.CurrentValue));

            case ControlKind.TextBox:
                // An empty box is what the user typed; only an unread setting has nothing to record.
                return state.CurrentValue is string typed ? new ChoiceValue.Text(typed) : null;

            case ControlKind.List:
                // An empty list means "create no rows"; an unread setting records that rather than dropping out.
                return state.CurrentValue as ChoiceValue.List ?? new ChoiceValue.List([]);

            default:
                return null;
        }
    }

    // A Separate-mode powercfg selection detects a raw AC/DC value; the file stores the option index whose
    // Set payload for the powercfg key equals it (the catalog authors one state per option). Null is "that
    // context reported nothing", for the caller to resolve; a reading matching no option still lands on 0.
    private static int? IndexOfPowerValue(Setting setting, PowerCfgTarget powerCfg, int? value)
    {
        if (value is not { } v) return null;
        for (int i = 0; i < setting.States.Count; i++)
        {
            if (setting.States[i].Set.TryGetValue(powerCfg.Key, out var sv) && sv.WritePayload is { } p && Convert.ToInt32(p) == v)
                return i;
        }
        return 0;
    }
}
