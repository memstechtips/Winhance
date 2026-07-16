using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.Utilities;

namespace Winhance.UI.Features.Optimize.ViewModels;

/// <summary>
/// Immutable snapshot of a setting's current live state, passed by the view-model when it asks the
/// technical-details panel to rebuild. The panel is fed from the <see cref="Setting"/> model + this
/// snapshot (the VM's already-resolved current state). No live reads happen here.
/// </summary>
internal sealed record TechnicalDetailsSnapshot(
    InputType InputType,
    bool IsSelected,
    int? SelectedIndex,
    int NumericValue,
    int AcValue,
    int DcValue,
    int AcNumericValue,
    int DcNumericValue,
    bool SupportsSeparateACDC,
    bool HasBattery,
    IReadOnlyList<ComboBoxDisplayOption> Options);

/// <summary>
/// Builds the technical-details panel ("docs inside the app") from the <see cref="Setting"/> model:
/// an option->value table (which choice writes which value, with role + current markers), the registry
/// target locations, the power (AC/DC, display units) section, scheduled tasks, and per-state script /
/// reg-content effects. Driven directly by the view-model via <see cref="Update"/>. Owns the regedit-launch command.
/// </summary>
internal sealed class TechnicalDetailsManager : IDisposable
{
    private readonly Func<string> _getSettingId;
    private readonly Action<IReadOnlyList<TechnicalDetailSection>> _setSections;
    private readonly ILogService _logService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IRegeditLauncher? _regeditLauncher;
    private readonly ILocalizationService _localizationService;
    private readonly TechnicalDetailLabels _labels;

    public IRelayCommand<string> OpenRegeditCommand { get; }

    public TechnicalDetailsManager(
        Func<string> getSettingId,
        Action<IReadOnlyList<TechnicalDetailSection>> setSections,
        ILogService logService,
        IDispatcherService dispatcherService,
        IRegeditLauncher? regeditLauncher,
        IEventBus? eventBus,
        ILocalizationService localizationService,
        TechnicalDetailLabels? labels = null)
    {
        _getSettingId = getSettingId;
        _setSections = setSections;
        _logService = logService;
        _dispatcherService = dispatcherService;
        _regeditLauncher = regeditLauncher;
        _localizationService = localizationService;
        _labels = labels ?? new TechnicalDetailLabels();

        OpenRegeditCommand = new RelayCommand<string>(OpenRegeditAtPath);
        // eventBus is no longer used: the panel is driven directly by the view-model via Update(), not by a
        // TooltipUpdatedEvent subscription. The param stays to avoid a ctor-call change.
        _ = eventBus;
    }

    /// <summary>Rebuilds the panel from the setting model + current-state snapshot, on the UI thread.</summary>
    public void Update(Setting? setting, TechnicalDetailsSnapshot snapshot)
    {
        _dispatcherService.RunOnUIThread(DispatcherQueuePriority.Low,
            () => BuildSections(setting, snapshot));
    }

    private void BuildSections(Setting? setting, TechnicalDetailsSnapshot snap)
    {
        try
        {
            if (setting is null)
            {
                // Unpaired setting: nothing to document.
                _setSections(Array.Empty<TechnicalDetailSection>());
                return;
            }

            var sections = new List<TechnicalDetailSection>();

            var optionRows = BuildOptionRows(setting, snap);
            if (optionRows.Count > 0)
                sections.Add(new TechnicalDetailSection(DetailRowType.Option, _labels.SectionOptions, true, optionRows));

            var powerPlanRows = BuildPowerPlanRows(setting, snap);
            if (powerPlanRows.Count > 0)
                sections.Add(new TechnicalDetailSection(DetailRowType.Info, _labels.SectionPowerPlans, true, powerPlanRows));

            var registryRows = BuildRegistryRows(setting, snap);
            if (registryRows.Count > 0)
                sections.Add(new TechnicalDetailSection(DetailRowType.Registry, _labels.SectionRegistry, false, registryRows));

            var powerRows = BuildPowerRows(setting, snap);
            if (powerRows.Count > 0)
                sections.Add(new TechnicalDetailSection(DetailRowType.PowerConfig, _labels.SectionPowerSettings, false, powerRows));

            var taskRows = BuildTaskRows(setting, snap);
            if (taskRows.Count > 0)
                sections.Add(new TechnicalDetailSection(DetailRowType.ScheduledTask, _labels.SectionScheduledTasks, false, taskRows));

            var scriptRows = BuildScriptRows(setting);
            if (scriptRows.Count > 0)
                sections.Add(new TechnicalDetailSection(DetailRowType.PowerShellScript, _labels.SectionScripts, false, scriptRows));

            var regContentRows = BuildRegContentRows(setting);
            if (regContentRows.Count > 0)
                sections.Add(new TechnicalDetailSection(DetailRowType.RegContent, _labels.SectionRegContent, false, regContentRows));

            var targetRows = BuildTargetRows(setting);
            if (targetRows.Count > 0)
                sections.Add(new TechnicalDetailSection(DetailRowType.Info, _labels.SectionTargets, false, targetRows));

            var effectRows = BuildEffectRows(setting);
            if (effectRows.Count > 0)
                sections.Add(new TechnicalDetailSection(DetailRowType.Info, _labels.SectionEffects, false, effectRows));

            var relationshipRows = BuildRelationshipRows(setting);
            if (relationshipRows.Count > 0)
                sections.Add(new TechnicalDetailSection(DetailRowType.Info, _labels.SectionRelationships, false, relationshipRows));

            var applyRows = BuildApplyBehaviorRows(setting);
            if (applyRows.Count > 0)
                sections.Add(new TechnicalDetailSection(DetailRowType.Info, _labels.SectionApplyBehavior, false, applyRows));

            _setSections(sections);
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error,
                $"[TechnicalDetails] BuildSections failed for '{_getSettingId()}': {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // --- Options table (the headline): one row per authored state, for state-based Selection / Toggle.
    // Power (AC/DC) settings are documented in the Power section instead, so they're excluded here.
    private List<TechnicalDetailRow> BuildOptionRows(Setting setting, TechnicalDetailsSnapshot snap)
    {
        var rows = new List<TechnicalDetailRow>();
        if (setting.States.Count == 0) return rows;                       // Action / dynamic-option / numeric
        if (setting.Targets.OfType<PowerCfgTarget>().Any()) return rows;   // powercfg -> Power section

        bool isToggle = snap.InputType == InputType.Toggle || snap.InputType == InputType.CheckBox;
        var states = setting.States;
        for (int i = 0; i < states.Count; i++)
        {
            var state = states[i];
            string label;
            bool isCurrent;
            if (isToggle)
            {
                // Toggle states carry the literal labels "Enabled"/"Disabled"; present them as On/Off.
                bool enabledState = string.Equals(state.Label, "Enabled", StringComparison.Ordinal);
                label = enabledState ? _labels.On : _labels.Off;
                isCurrent = enabledState == snap.IsSelected;
            }
            else
            {
                // Selection: reuse the localized ComboBox option label (1:1 with States, same order).
                label = i < snap.Options.Count ? snap.Options[i].DisplayText : state.Label;
                isCurrent = snap.SelectedIndex is int sel && sel == i;
            }

            rows.Add(new TechnicalDetailRow
            {
                RowType = DetailRowType.Option,
                OptionLabel = label,
                OptionValue = FormatStateValues(state),
                OptionRole = FormatRole(state, PowerContext.Always),
                IsCurrentOption = isCurrent,
                CurrentLabelText = _labels.Current
            });
        }
        return rows;
    }

    // --- Power Plans: for a ControlKind.PowerPlan setting (power-plan-selection) whose options are produced
    // at runtime (PowerPlanOptionSource), document each offered scheme - name + GUID + installed + which is
    // active. Sourced from the VM snapshot ComboBoxDisplayOptions, whose RUNTIME Value is the scheme GUID
    // (string) and Tag is a PowerPlanComboBoxOption (ExistsOnSystem/IsActive). Builder mode instead uses the
    // int dropdown index as Value and a raw PowerPlan_ loc-key DisplayText (config-authoring, no live GUID),
    // so its index-valued options are skipped: this section is runtime documentation and renders nothing in
    // Builder mode - matching the setting rendering nothing there today.
    private List<TechnicalDetailRow> BuildPowerPlanRows(Setting setting, TechnicalDetailsSnapshot snap)
    {
        var rows = new List<TechnicalDetailRow>();
        if (setting.Control != ControlKind.PowerPlan) return rows;

        foreach (var opt in snap.Options)
        {
            // Only the runtime dropdown carries the scheme GUID as a string Value; skip the Builder-mode
            // index-valued options (they have no live GUID and a raw loc-key label).
            if (opt.Value is not string guid || guid.Length == 0) continue;

            var tag = opt.Tag as Winhance.Core.Features.Common.Models.PowerPlanComboBoxOption;
            var primary = tag?.IsActive == true
                ? $"{opt.DisplayText} [{_labels.PowerPlanActive}]"
                : opt.DisplayText;
            var installed = tag?.ExistsOnSystem == true ? _labels.PowerPlanInstalled : _labels.PowerPlanNotInstalled;
            rows.Add(InfoRow(primary, $"{guid}  |  {installed}"));
        }

        if (rows.Count > 0)
            rows.Add(InfoRow(_labels.PowerPlanApplyNote, string.Empty));
        return rows;
    }

    // --- Registry target locations + per-key Current/Recommended/Default sourced from the state roles.
    private List<TechnicalDetailRow> BuildRegistryRows(Setting setting, TechnicalDetailsSnapshot snap)
    {
        var rows = new List<TechnicalDetailRow>();
        var current = CurrentState(setting, snap);
        var recommended = RoleState(setting, RoleKind.Recommended, PowerContext.Always);
        var windowsDefault = RoleState(setting, RoleKind.WindowsDefault, PowerContext.Always);

        foreach (var reg in setting.Targets.OfType<RegTarget>())
        {
            var path = reg.Paths.Count > 0 ? reg.Paths[0] : string.Empty;
            bool keyExists = false;
            try
            {
                keyExists = !string.IsNullOrEmpty(path) && (_regeditLauncher?.KeyExists(path) ?? false);
            }
            catch (Exception kex)
            {
                _logService.Log(LogLevel.Warning,
                    $"[TechnicalDetails] KeyExists failed for '{path}': {kex.GetType().Name}: {kex.Message}");
            }

            rows.Add(new TechnicalDetailRow
            {
                RowType = DetailRowType.Registry,
                RegistryPath = path,
                ValueName = reg.ValueName ?? "(Default)",
                ValueType = reg.Type.ToString(),
                CurrentValue = ValueForKey(current, reg.Key),
                RecommendedValue = ValueForKey(recommended, reg.Key),
                DefaultValue = ValueForKey(windowsDefault, reg.Key),
                PathLabel = _labels.Path,
                ValueLabel = _labels.Value,
                CurrentLabel = _labels.Current,
                RecommendedLabel = _labels.Recommended,
                DefaultLabel = _labels.Default,
                OpenRegeditCommand = OpenRegeditCommand,
                RegeditIconSource = RegeditIconProvider.CachedIcon,
                CanOpenRegedit = keyExists
            });
        }
        return rows;
    }

    // --- Power (powercfg) section: GUIDs + units + Current/Recommended/Default per AC/DC, in DISPLAY units
    // for a numeric (slider) power setting, or the raw option value for a selection power setting.
    // NOTE: PowerCfgTarget carries no friendly GUID aliases -> the
    // alias columns are left blank; the raw subgroup/setting GUIDs are still shown.
    private List<TechnicalDetailRow> BuildPowerRows(Setting setting, TechnicalDetailsSnapshot snap)
    {
        var rows = new List<TechnicalDetailRow>();
        bool isNumeric = setting.Numeric is not null;

        foreach (var pcfg in setting.Targets.OfType<PowerCfgTarget>())
        {
            string currentAc, currentDc, recAc, recDc, defAc, defDc;
            string units = setting.Numeric?.Units ?? pcfg.Units ?? string.Empty;

            if (isNumeric)
            {
                var num = setting.Numeric!;
                currentAc = snap.SupportsSeparateACDC
                    ? snap.AcNumericValue.ToString()
                    : snap.NumericValue.ToString();
                currentDc = snap.SupportsSeparateACDC && snap.HasBattery
                    ? snap.DcNumericValue.ToString()
                    : string.Empty;
                recAc = ContextValueText(num.Recommended, snap.SupportsSeparateACDC ? PowerContext.AC : PowerContext.Always);
                recDc = snap.SupportsSeparateACDC && snap.HasBattery ? ContextValueText(num.Recommended, PowerContext.DC) : string.Empty;
                defAc = ContextValueText(num.WindowsDefault, snap.SupportsSeparateACDC ? PowerContext.AC : PowerContext.Always);
                defDc = snap.SupportsSeparateACDC && snap.HasBattery ? ContextValueText(num.WindowsDefault, PowerContext.DC) : string.Empty;
            }
            else
            {
                // Selection power setting: the value is the raw option int each state writes for this target.
                currentAc = StateValueText(StateAtIndex(setting, snap.AcValue), pcfg.Key);
                currentDc = snap.HasBattery ? StateValueText(StateAtIndex(setting, snap.DcValue), pcfg.Key) : string.Empty;
                recAc = StateValueText(RoleState(setting, RoleKind.Recommended, PowerContext.AC), pcfg.Key);
                recDc = snap.HasBattery ? StateValueText(RoleState(setting, RoleKind.Recommended, PowerContext.DC), pcfg.Key) : string.Empty;
                defAc = StateValueText(RoleState(setting, RoleKind.WindowsDefault, PowerContext.AC), pcfg.Key);
                defDc = snap.HasBattery ? StateValueText(RoleState(setting, RoleKind.WindowsDefault, PowerContext.DC), pcfg.Key) : string.Empty;
            }

            rows.Add(new TechnicalDetailRow
            {
                RowType = DetailRowType.PowerConfig,
                CurrentLabel = _labels.Current,
                RecommendedLabel = _labels.Recommended,
                DefaultLabel = _labels.Default,
                SubgroupLabel = _labels.PowerCfgSubgroup,
                SettingLabel = _labels.PowerCfgSetting,
                SubgroupGuid = pcfg.SubgroupGuid,
                SettingGuid = pcfg.SettingGuid,
                SubgroupAlias = string.Empty,
                SettingAlias = string.Empty,
                PowerUnits = units,
                CurrentAC = currentAc,
                CurrentDC = currentDc,
                RecommendedAC = recAc,
                RecommendedDC = recDc,
                DefaultAC = defAc,
                DefaultDC = defDc
            });
        }
        return rows;
    }

    // --- Scheduled tasks: the task locations + the toggle's current/recommended/default enabled-ness.
    private List<TechnicalDetailRow> BuildTaskRows(Setting setting, TechnicalDetailsSnapshot snap)
    {
        var rows = new List<TechnicalDetailRow>();
        var current = CurrentState(setting, snap);
        var recommended = RoleState(setting, RoleKind.Recommended, PowerContext.Always);
        var windowsDefault = RoleState(setting, RoleKind.WindowsDefault, PowerContext.Always);

        foreach (var task in setting.Targets.OfType<TaskTarget>())
        {
            rows.Add(new TechnicalDetailRow
            {
                RowType = DetailRowType.ScheduledTask,
                TaskPath = task.TaskPath,
                PathLabel = _labels.Path,
                CurrentLabel = _labels.Current,
                RecommendedLabel = _labels.Recommended,
                DefaultLabel = _labels.Default,
                CurrentState = TaskStateText(current, task.Key),
                RecommendedState = TaskStateText(recommended, task.Key),
                DefaultState = TaskStateText(windowsDefault, task.Key)
            });
        }
        return rows;
    }

    // --- PowerShell script effects (per-state + setting-level), on-apply only.
    private List<TechnicalDetailRow> BuildScriptRows(Setting setting)
    {
        var rows = new List<TechnicalDetailRow>();
        foreach (var (state, effect) in EnumerateEffects(setting).Where(e => e.effect is ScriptEffect))
        {
            var script = (ScriptEffect)effect;
            if (string.IsNullOrWhiteSpace(script.Script)) continue;
            rows.Add(new TechnicalDetailRow
            {
                RowType = DetailRowType.PowerShellScript,
                ScriptLabel = ScriptLabelFor(state),
                ScriptBody = script.Script
            });
        }
        return rows;
    }

    // --- .reg content effects (per-state + setting-level), on-apply only.
    private List<TechnicalDetailRow> BuildRegContentRows(Setting setting)
    {
        var rows = new List<TechnicalDetailRow>();
        foreach (var (state, effect) in EnumerateEffects(setting).Where(e => e.effect is RegContentEffect))
        {
            var reg = (RegContentEffect)effect;
            if (string.IsNullOrWhiteSpace(reg.Content)) continue;
            rows.Add(new TechnicalDetailRow
            {
                RowType = DetailRowType.RegContent,
                ContentLabel = ContentLabelFor(state),
                ContentBody = reg.Content
            });
        }
        return rows;
    }

    // --- Targets: every read/write location the setting touches, with its full technical metadata.
    private List<TechnicalDetailRow> BuildTargetRows(Setting setting)
    {
        var rows = new List<TechnicalDetailRow>();
        foreach (var target in setting.Targets)
        {
            string primary;
            var meta = new List<string>();
            switch (target)
            {
                case RegTarget reg:
                    primary = $"{_labels.TargetRegistry}: {(reg.Paths.Count > 0 ? reg.Paths[0] : string.Empty)}";
                    if (reg.Paths.Count > 1) meta.Add($"+{reg.Paths.Count - 1} mirror");
                    meta.Add($"{reg.ValueName ?? "(Default)"} ({reg.Type})");
                    if (reg.IsGroupPolicy) meta.Add(_labels.MetaGroupPolicy);
                    if (reg.ByteIndex is int bi)
                        meta.Add($"byte {bi}" + (reg.BitMask is byte bm ? $" bit 0x{bm:X2}" : string.Empty));
                    if (!string.IsNullOrEmpty(reg.CompositeStringKey)) meta.Add($"sub-key {reg.CompositeStringKey}");
                    if (reg.PerNetworkInterface) meta.Add("per-NIC");
                    if (reg.PerMonitor) meta.Add("per-monitor");
                    if (reg.ApplyOnly) meta.Add("apply-only");
                    break;
                case PowerCfgTarget pcfg:
                    primary = $"{_labels.TargetPower}: {pcfg.SubgroupGuid} / {pcfg.SettingGuid}";
                    if (!string.IsNullOrEmpty(pcfg.Units)) meta.Add(pcfg.Units!);
                    meta.Add(pcfg.Mode.ToString());
                    if (pcfg.EnablementKey is not null) meta.Add("enablement key");
                    if (pcfg.CheckForHardwareControl) meta.Add("hardware-controlled");
                    break;
                case TaskTarget task:
                    primary = $"{_labels.TargetTask}: {task.TaskPath}";
                    break;
                default:
                    primary = target.Key;
                    break;
            }
            if (target.AppliesTo.Count > 0) meta.Add("OS-specific");

            rows.Add(new TechnicalDetailRow
            {
                RowType = DetailRowType.Info,
                InfoPrimary = primary,
                InfoSecondary = string.Join(" | ", meta)
            });
        }
        return rows;
    }

    // --- Effects: the apply-only side-effects beyond the script / reg-content already shown above
    // (a registry write performed on apply, or a native power-API write).
    private List<TechnicalDetailRow> BuildEffectRows(Setting setting)
    {
        var rows = new List<TechnicalDetailRow>();
        foreach (var (_, effect) in EnumerateEffects(setting))
        {
            switch (effect)
            {
                case RegistryWriteEffect rw:
                    var name = string.IsNullOrEmpty(rw.ValueName) ? "(Default)" : rw.ValueName;
                    rows.Add(InfoRow(_labels.EffectRegistryWrite,
                        $"{rw.Path}\\{name} = {FormatConcreteValueText(rw.Value)}"
                        + (rw.IsGroupPolicy ? $" ({_labels.MetaGroupPolicy})" : string.Empty)));
                    break;
                case NativePowerEffect np:
                    rows.Add(InfoRow(_labels.EffectNativePower, $"level {np.InformationLevel} = {np.Value}"));
                    break;
            }
        }

        AddWallpaperRows(setting, rows);
        return rows;
    }

    // --- Wallpaper effects (theme-mode-windows Light/Dark, OS-divergent): one row per owning state,
    // joining every OS variant path. Grouped-by-state presentation (panel-UX call, 2026-07-16).
    private void AddWallpaperRows(Setting setting, List<TechnicalDetailRow> rows)
    {
        AddWallpaperRow(null, setting.Effects, rows);
        foreach (var state in setting.States)
            AddWallpaperRow(state, state.Effects, rows);
    }

    private void AddWallpaperRow(SettingState? state, IReadOnlyList<Effect> effects, List<TechnicalDetailRow> rows)
    {
        var wallpapers = effects.OfType<WallpaperEffect>().ToList();
        if (wallpapers.Count == 0) return;

        var primary = state is { Label: { Length: > 0 } stateLabel }
            ? $"{_labels.EffectWallpaper} ({stateLabel})"
            : _labels.EffectWallpaper;
        var detail = string.Join("  |  ", wallpapers.Select(wp =>
        {
            var os = DescribeBuildRanges(wp.AppliesTo);
            return os.Length == 0 ? wp.Path : $"{os}: {wp.Path}";
        }));
        rows.Add(InfoRow(primary, detail));
    }

    /// <summary>Human OS label for a wallpaper effect build scope: "Windows 11"/"Windows 10" for the two
    /// standard ranges, a raw build-range otherwise, or "" when unconditional (every build).</summary>
    private static string DescribeBuildRanges(IReadOnlyList<BuildRange> ranges) =>
        ranges.Count == 0 ? string.Empty : string.Join(", ", ranges.Select(DescribeBuildRange));

    private static string DescribeBuildRange(BuildRange range)
    {
        if (range == BuildRange.Windows11) return "Windows 11";
        if (range == BuildRange.Windows10) return "Windows 10";
        return $"builds {range.Min.Build}-{range.Max.Build}";
    }

    // --- Relationships: nested-under parent + the requires/enables links + the child settings a state drives.
    private List<TechnicalDetailRow> BuildRelationshipRows(Setting setting)
    {
        var rows = new List<TechnicalDetailRow>();
        var seen = new HashSet<string>();

        if (!string.IsNullOrEmpty(setting.UiParentId))
            rows.Add(InfoRow(LocalizeSettingName(setting.UiParentId!), _labels.RelNestedUnder));

        foreach (var link in setting.States.SelectMany(s => s.Links))
        {
            if (!seen.Add($"link:{link.Kind}:{link.OtherId}:{link.RequiredState}")) continue;
            var verb = link.Kind == LinkKind.Requires ? _labels.RelRequires : _labels.RelEnables;
            rows.Add(InfoRow(LocalizeSettingName(link.OtherId), $"{verb}: {link.RequiredState}"));
        }

        foreach (var state in setting.States)
        {
            if (state.Controls is null) continue;
            foreach (var kv in state.Controls)
            {
                if (!seen.Add($"controls:{kv.Key}:{kv.Value}")) continue;
                rows.Add(InfoRow(LocalizeSettingName(kv.Key), $"{_labels.RelControls}: {kv.Value}"));
            }
        }
        return rows;
    }

    // --- Apply Behavior: the apply-time gates/side-effects declared on Setting.Apply (a confirmation prompt,
    // a recommended reboot, and/or a process/service restart for the change to take effect). Documented for NO
    // setting before this - e.g. theme-mode-windows carries RequiresConfirmation + a RestartProcess("Explorer").
    private List<TechnicalDetailRow> BuildApplyBehaviorRows(Setting setting)
    {
        var rows = new List<TechnicalDetailRow>();
        var apply = setting.Apply;

        if (apply.RequiresConfirmation)
            rows.Add(InfoRow(_labels.ApplyRequiresConfirmation, _labels.ApplyRequiresConfirmationDetail));
        if (apply.RequiresReboot)
            rows.Add(InfoRow(_labels.ApplyRequiresReboot, _labels.ApplyRequiresRebootDetail));

        switch (apply.Restart)
        {
            case RestartProcess p:
                rows.Add(InfoRow(_labels.ApplyRestartProcess, p.Name));
                break;
            case RestartService s:
                rows.Add(InfoRow(_labels.ApplyRestartService, s.Name));
                break;
        }
        return rows;
    }

    private static TechnicalDetailRow InfoRow(string primary, string secondary) =>
        new() { RowType = DetailRowType.Info, InfoPrimary = primary, InfoSecondary = secondary };

    /// <summary>Localizes a related setting's display name (Setting_{id}_Name); falls back to the raw id
    /// when there's no translation (GetString returns "[key]" on a miss).</summary>
    private string LocalizeSettingName(string settingId)
    {
        var key = $"Setting_{settingId}_Name";
        var name = _localizationService.GetString(key);
        return string.IsNullOrEmpty(name) || name == key || name == $"[{key}]" ? settingId : name;
    }

    // ---------------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------------

    /// <summary>The state the system is currently in, or null (Custom / no state matched).</summary>
    private static SettingState? CurrentState(Setting setting, TechnicalDetailsSnapshot snap)
    {
        if (snap.InputType == InputType.Toggle || snap.InputType == InputType.CheckBox)
        {
            var wanted = snap.IsSelected ? "Enabled" : "Disabled";
            return setting.States.FirstOrDefault(s => string.Equals(s.Label, wanted, StringComparison.Ordinal));
        }
        if (snap.SelectedIndex is int idx && idx >= 0 && idx < setting.States.Count)
            return setting.States[idx];
        return null;
    }

    private static SettingState? StateAtIndex(Setting setting, int index) =>
        index >= 0 && index < setting.States.Count ? setting.States[index] : null;

    private static SettingState? RoleState(Setting setting, RoleKind kind, PowerContext context) =>
        setting.States.FirstOrDefault(s => s.HasRole(kind, context));

    /// <summary>The localized role marker for a state in a given context ("Recommended"/"Default"/both/"").</summary>
    private string FormatRole(SettingState state, PowerContext context)
    {
        var parts = new List<string>(2);
        if (state.HasRole(RoleKind.Recommended, context)) parts.Add(_labels.Recommended);
        if (state.HasRole(RoleKind.WindowsDefault, context)) parts.Add(_labels.Default);
        return string.Join(", ", parts);
    }

    /// <summary>All the values a state writes, joined (one entry per target in its Set).</summary>
    private string FormatStateValues(SettingState state)
    {
        if (state.Set.Count == 0) return string.Empty;
        // Skip targets with no concrete value (StateValue.Exists / presence-only) so the join
        // doesn't leave a dangling ", " around an empty cell.
        return string.Join(", ", state.Set.Values.Select(FormatStateValue).Where(s => s.Length > 0));
    }

    /// <summary>One target's write value, with the "or not set" / "deletes key" suffixes.</summary>
    private string FormatStateValue(StateValue value)
    {
        if (value.WritePayload is not null)
        {
            var text = FormatConcreteValueText(value.WritePayload);
            return value.AcceptsAbsent ? $"{text} ({_labels.OrNotSet})" : text;
        }
        if (value.DeleteOnWrite) return _labels.DeletesKey;   // StateValue.Absent
        return string.Empty;                                  // StateValue.Exists (rare) -> no concrete value
    }

    /// <summary>The value a specific state writes for one target key, or "" when it doesn't touch it.</summary>
    private string ValueForKey(SettingState? state, string key) =>
        state is not null && state.Set.TryGetValue(key, out var v) ? FormatStateValue(v) : string.Empty;

    /// <summary>The raw (no-unit) written value for a selection power setting's target key.</summary>
    private string StateValueText(SettingState? state, string key) =>
        state is not null && state.Set.TryGetValue(key, out var v) && v.WritePayload is not null
            ? FormatConcreteValueText(v.WritePayload)
            : string.Empty;

    /// <summary>On/Off text for a task target a state writes (present-and-deletes => Off, present => On).</summary>
    private string TaskStateText(SettingState? state, string key)
    {
        if (state is null || !state.Set.TryGetValue(key, out var v)) return string.Empty;
        return v.DeleteOnWrite ? _labels.Off : _labels.On;
    }

    private static string ContextValueText(IReadOnlyList<ContextValue> values, PowerContext context)
    {
        var match = values.FirstOrDefault(v => v.Context == context)
                    ?? values.FirstOrDefault(v => v.Context == PowerContext.Always);
        return match is not null ? match.Value.ToString() : string.Empty;
    }

    private static string FormatConcreteValueText(object value)
    {
        if (value is byte[] bytes)
            return bytes.Length == 0 ? "(empty)" : string.Join(" ", bytes.Select(b => b.ToString("X2")));
        var text = value.ToString() ?? string.Empty;
        return text.Length == 0 ? "\"\"" : text;
    }

    /// <summary>Setting-level effects (state == null) followed by each state's effects.</summary>
    private static IEnumerable<(SettingState? state, Effect effect)> EnumerateEffects(Setting setting)
    {
        foreach (var e in setting.Effects)
            yield return (null, e);
        foreach (var state in setting.States)
            foreach (var e in state.Effects)
                yield return (state, e);
    }

    private string ScriptLabelFor(SettingState? state) =>
        state is null ? _labels.ScriptOnApply
        : string.Equals(state.Label, "Disabled", StringComparison.Ordinal) ? _labels.ScriptOnDisable
        : _labels.ScriptOnEnable;

    private string ContentLabelFor(SettingState? state) =>
        state is not null && string.Equals(state.Label, "Disabled", StringComparison.Ordinal)
            ? _labels.RegContentOnDisable
            : _labels.RegContentOnEnable;

    private void OpenRegeditAtPath(string? path)
    {
        if (!string.IsNullOrEmpty(path))
            _regeditLauncher?.OpenAtPath(path);
    }

    public void Dispose()
    {
        // No subscriptions to release (the panel is VM-driven).
    }
}
