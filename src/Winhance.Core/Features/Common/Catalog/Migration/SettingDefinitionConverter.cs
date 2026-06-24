using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Catalog.Migration;

/// <summary>Throwaway migration tool: translates an old toggle SettingDefinition into the new Setting model
/// so the equivalence harness can compare old vs new. Deleted once the migration is complete.</summary>
public static class SettingDefinitionConverter
{
    public static Setting ConvertToggle(SettingDefinition def)
    {
        // A mirror = the same ValueName written under multiple KeyPaths -> one target, many paths.
        var groups = def.RegistrySettings.GroupBy(r => r.ValueName ?? "KeyExists").ToList();
        var targets = BuildTargets(groups);

        var enabledSet = new Dictionary<string, StateValue>();
        var disabledSet = new Dictionary<string, StateValue>();
        foreach (var g in groups)
        {
            var first = g.First();
            if (first.ValueName == null)
            {
                // ValueName == null toggles encode state as key existence, not a stored value. Mirror the
                // old detection's shape rule: inverted (rare) when DisabledValue carries the null sentinel
                // and EnabledValue does not; otherwise standard, where Enabled means the key is present.
                bool inverted = first.EnabledValue?.Contains(null) != true
                                && first.DisabledValue?.Contains(null) == true;
                enabledSet[g.Key] = inverted ? StateValue.Absent : StateValue.Exists;
                disabledSet[g.Key] = inverted ? StateValue.Exists : StateValue.Absent;
                continue;
            }
            enabledSet[g.Key] = ToStateValue(first.EnabledValue) ?? StateValue.Exists;
            disabledSet[g.Key] = ToStateValue(first.DisabledValue) ?? StateValue.Absent;
        }

        var rec = SettingDefinitionToggleState.GetRecommendedToggleState(def);
        var def_ = SettingDefinitionToggleState.GetDefaultToggleState(def);

        var enabled = new SettingState { Label = "Enabled", Set = enabledSet, Roles = RolesFor(true, rec, def_), Effects = BuildToggleEffects(def, isEnabled: true) };
        var disabled = new SettingState { Label = "Disabled", Set = disabledSet, Roles = RolesFor(false, rec, def_), IsFallback = true, Effects = BuildToggleEffects(def, isEnabled: false) };

        return new Setting
        {
            Id = def.Id,
            Display = BuildDisplay(def),
            Availability = BuildAvailability(def),
            Apply = BuildApply(def),
            Links = BuildLinks(def),
            UiParentId = def.ParentSettingId,
            Targets = targets.Cast<Target>().ToList(),
            States = new[] { enabled, disabled },
        };
    }

    /// <summary>Translates an old registry SELECTION (ComboBox) SettingDefinition into the new Setting model.
    /// One state per ComboBox option (Label = option DisplayName); each option's ValueMappings become the
    /// state's per-target accept Set; IsRecommended/IsDefault become roles; the IsDefault option becomes the
    /// catch-all fallback when the definition opts in via ResolveUnmatchedToDefault.</summary>
    public static Setting ConvertSelection(SettingDefinition def)
    {
        var groups = def.RegistrySettings.GroupBy(r => r.ValueName ?? "KeyExists").ToList();
        var targets = BuildTargets(groups);

        // Old ResolveRawValuesToIndex substitutes a target's DefaultValue when the live read is absent, so a
        // mapping value equal to that DefaultValue must also accept absence. Index DefaultValue by group key.
        var defaultByKey = groups.ToDictionary(g => g.Key, g => g.First().DefaultValue);

        // Old ResolveRawValuesToIndex resolves an all-targets-absent read to the IsDefault option (its "all
        // backing absent" path) even when ResolveUnmatchedToDefault is off. For a single-target selection
        // that collapses to "the one value is absent", so let that option's value also accept absence. Scoped
        // to single-target to avoid a multi-key option matching a partially-absent read (which old would not).
        bool singleTarget = groups.Count == 1;

        var options = def.ComboBox!.Options;
        var states = new List<SettingState>(options.Count);
        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            var opt = options[optionIndex];
            var set = new Dictionary<string, StateValue>();
            if (opt.ValueMappings is { } vm)
            {
                bool absorbAbsent = opt.IsDefault && singleTarget && !def.ResolveUnmatchedToDefault;
                foreach (var (key, expected) in vm)
                {
                    var sv = ToSelectionStateValue(expected, defaultByKey.TryGetValue(key, out var dv) ? dv : null);
                    if (absorbAbsent)
                        sv = sv.OrAbsent();
                    set[key] = sv;
                }
            }

            var roles = new List<StateRole>();
            if (opt.IsRecommended) roles.Add(new StateRole(RoleKind.Recommended));
            if (opt.IsDefault) roles.Add(new StateRole(RoleKind.WindowsDefault));

            states.Add(new SettingState
            {
                Label = opt.DisplayName,
                Set = set,
                Roles = roles,
                Effects = BuildSelectionOptionEffects(def, opt),
                Controls = def.SettingPresets is { } presets && presets.TryGetValue(optionIndex, out var childMap)
                    ? childMap.ToDictionary(kv => kv.Key, kv => kv.Value ? "Enabled" : "Disabled")
                    : null,
                // ResolveUnmatchedToDefault: an unrecognised live state resolves to the IsDefault option.
                IsFallback = def.ResolveUnmatchedToDefault && opt.IsDefault,
            });
        }

        return new Setting
        {
            Id = def.Id,
            Display = BuildDisplay(def),
            Availability = BuildAvailability(def),
            Apply = BuildApply(def),
            Links = BuildLinks(def),
            UiParentId = def.ParentSettingId,
            Targets = targets.Cast<Target>().ToList(),
            States = states,
        };
    }

    /// <summary>Translates an old scheduled-task toggle SettingDefinition into the new Setting model: a single
    /// <see cref="TaskTarget"/> with Enabled (task enabled) / Disabled (task disabled) states. Roles come from
    /// the task's RecommendedState/DefaultState. An absent task is an availability concern, handled by the
    /// caller (the old app marks the setting unavailable), not a detected state here.</summary>
    public static Setting ConvertScheduledTaskToggle(SettingDefinition def)
    {
        var task = def.ScheduledTaskSettings[0];
        var target = new TaskTarget("Task", task.TaskPath);

        var enabled = new SettingState
        {
            Label = "Enabled",
            Set = new Dictionary<string, StateValue> { ["Task"] = StateValue.Of(true) },
            Roles = RolesFor(true, task.RecommendedState, task.DefaultState),
        };
        var disabled = new SettingState
        {
            Label = "Disabled",
            Set = new Dictionary<string, StateValue> { ["Task"] = StateValue.Of(false) },
            Roles = RolesFor(false, task.RecommendedState, task.DefaultState),
            IsFallback = true,   // a present task that is not enabled is Disabled (mirrors the old detection)
        };

        return new Setting
        {
            Id = def.Id,
            Display = BuildDisplay(def),
            Availability = BuildAvailability(def),
            Apply = BuildApply(def),
            Links = BuildLinks(def),
            UiParentId = def.ParentSettingId,
            Targets = new List<Target> { target },
            States = new[] { enabled, disabled },
        };
    }

    /// <summary>Translates the system-tray-icons selection (DetectionType.SystemTrayIcons) into a Setting that
    /// detects via <see cref="SystemTrayDetector"/>: the "show all" / "hide all" labels are the options whose
    /// Script is Enabled / Disabled (the old code keys off the same Script field). A null detection (no subkeys,
    /// no IsPromoted values, or a mix) is Custom.</summary>
    public static Setting ConvertSystemTray(SettingDefinition def)
    {
        var options = def.ComboBox!.Options;
        string showAll = options.First(o => o.Script == ScriptOption.Enabled).DisplayName;
        string hideAll = options.First(o => o.Script == ScriptOption.Disabled).DisplayName;

        return new Setting
        {
            Id = def.Id,
            Display = BuildDisplay(def),
            Availability = BuildAvailability(def),
            Apply = BuildApply(def),
            Links = BuildLinks(def),
            UiParentId = def.ParentSettingId,
            States = options.Select(o => new SettingState { Label = o.DisplayName }).ToList(),
            Detector = new SystemTrayDetector(showAll, hideAll),
        };
    }

    /// <summary>Translates the system-restore toggle (DetectionType.SystemRestore) into a Setting that detects
    /// via <see cref="SystemRestoreDetector"/>: System Restore on for C: = Enabled, off = Disabled (the old
    /// toggle reads the same IsEnabledForC bool). Roles come from the toggle's recommended/default state.</summary>
    public static Setting ConvertSystemRestore(SettingDefinition def)
    {
        var rec = SettingDefinitionToggleState.GetRecommendedToggleState(def);
        var def_ = SettingDefinitionToggleState.GetDefaultToggleState(def);

        var enabled = new SettingState { Label = "Enabled", Roles = RolesFor(true, rec, def_) };
        var disabled = new SettingState { Label = "Disabled", Roles = RolesFor(false, rec, def_) };

        return new Setting
        {
            Id = def.Id,
            Display = BuildDisplay(def),
            Availability = BuildAvailability(def),
            Apply = BuildApply(def),
            Links = BuildLinks(def),
            UiParentId = def.ParentSettingId,
            States = new[] { enabled, disabled },
            Detector = new SystemRestoreDetector("Enabled", "Disabled"),
        };
    }

    /// <summary>Translates the dynamic power-plan-selection (LoadDynamicOptions) into a Setting that detects via
    /// PowerPlanDetector: its options are the machine's installed plans, so it carries no static states - the detector
    /// returns the active plan GUID at runtime.</summary>
    public static Setting ConvertPowerPlan(SettingDefinition def) => new Setting
    {
        Id = def.Id,
        Display = BuildDisplay(def),
        Availability = BuildAvailability(def),
        Apply = BuildApply(def),
        Links = BuildLinks(def),
        UiParentId = def.ParentSettingId,
        Detector = new PowerPlanDetector(),
    };

    /// <summary>Translates the DNS-server selection (DetectionType.DnsServer) into a Setting that detects via
    /// <see cref="DnsServerDetector"/>. The automatic label is the first option's DisplayName (the old code
    /// returns index 0 for DHCP / no adapter / no primary). The primary-IP -> label map is built FIRST-WINS so a
    /// duplicate primary resolves to the earliest option, matching the old first-match loop.</summary>
    public static Setting ConvertDnsServer(SettingDefinition def)
    {
        var options = def.ComboBox!.Options;
        string automaticLabel = options[0].DisplayName;

        var primaryIpToLabel = new Dictionary<string, string>();
        foreach (var opt in options)
        {
            if (opt.ScriptVariables is { } vars
                && vars.TryGetValue("primary", out var primary)
                && !primaryIpToLabel.ContainsKey(primary))
            {
                primaryIpToLabel[primary] = opt.DisplayName;
            }
        }

        return new Setting
        {
            Id = def.Id,
            Display = BuildDisplay(def),
            Availability = BuildAvailability(def),
            Apply = BuildApply(def),
            Links = BuildLinks(def),
            UiParentId = def.ParentSettingId,
            States = options.Select(o => new SettingState { Label = o.DisplayName }).ToList(),
            Detector = new DnsServerDetector(automaticLabel, primaryIpToLabel),
        };
    }

    /// <summary>Translates an old InputType.Action one-shot into the new Setting model: zero States/Targets,
    /// with setting-level Effects that run on click. Maps the ENABLED branch only (the old apply hardcodes
    /// enable=true): each RegistrySetting's EnabledValue -> a RegistryWriteEffect (unfolded, source order, so a
    /// per-setting IsGroupPolicy is preserved), then the enabled PowerShell / .reg / native-power effects, in
    /// that order (mirroring the old apply order: registry writes before script/native execution).</summary>
    public static Setting ConvertAction(SettingDefinition def)
    {
        var effects = new List<Effect>();

        foreach (var rs in def.RegistrySettings)
        {
            // Actions are plain value-writes only. A surgical-binary / composite / per-subkey write on an
            // Action is unsupported (none exist) - fail loud rather than emit a wrong op.
            if (rs.BitMask.HasValue || rs.ModifyByteOnly || rs.CompositeStringKey != null
                || rs.ApplyPerNetworkInterface || rs.ApplyPerMonitor || rs.ValueName == null)
                throw new System.NotSupportedException(
                    $"Action '{def.Id}' has a non-plain registry write (bit/byte/composite/per-subkey/key-existence) - not supported.");

            var nonNull = (rs.EnabledValue ?? System.Array.Empty<object?>()).Where(v => v != null).ToArray();
            if (nonNull.Length != 1)
                throw new System.NotSupportedException(
                    $"Action '{def.Id}' RegistrySetting '{rs.ValueName}' must have exactly one concrete EnabledValue; found {nonNull.Length}.");

            effects.Add(new RegistryWriteEffect(rs.KeyPath, rs.ValueName!, rs.ValueType, nonNull[0]!)
            {
                IsGroupPolicy = rs.IsGroupPolicy,
            });
        }

        foreach (var ps in def.PowerShellScripts)
            if (!string.IsNullOrEmpty(ps.EnabledScript))
                effects.Add(new ScriptEffect(ps.EnabledScript!, ps.RunContext));

        foreach (var rc in def.RegContents)
            if (!string.IsNullOrEmpty(rc.EnabledContent))
                effects.Add(new RegContentEffect(rc.EnabledContent!));

        foreach (var np in def.NativePowerApiSettings)
            effects.Add(new NativePowerEffect(np.InformationLevel, np.EnabledValue));

        return new Setting
        {
            Id = def.Id,
            Display = BuildDisplay(def),
            Availability = BuildAvailability(def),
            Apply = BuildApply(def),
            Links = BuildLinks(def),
            UiParentId = def.ParentSettingId,
            Effects = effects,
        };
    }

    /// <summary>Translates a powercfg SettingDefinition into the new Setting model. A Selection becomes one state
    /// per ComboBox option whose Set carries the option's PowerCfgValue, with context-scoped roles (Recommended /
    /// WindowsDefault per AC and DC) derived from the per-mode recommended/default VALUES (matching the old
    /// recommended-settings resolver, which finds the option whose PowerCfgValue equals RecommendedValueAC/DC -
    /// not the recommended-option label). A NumericRange becomes a stateless slider carrying a Numeric range +
    /// per-context recommended/default values. Both declare the AC and DC contexts and a single PowerCfgTarget.</summary>
    public static Setting ConvertPowerCfg(SettingDefinition def)
    {
        var pcs = def.PowerCfgSettings[0];
        var target = BuildPowerCfgTarget(pcs);

        if (def.InputType == InputType.NumericRange)
        {
            // Numeric values are stored in display units (what the slider shows): the per-mode recommended/default
            // come back from powercfg in system units and are converted, matching the old units conversion.
            string units = def.NumericRange?.Units ?? pcs.Units ?? string.Empty;
            var recommended = new List<ContextValue>();
            if (pcs.RecommendedValueAC is { } rac) recommended.Add(new ContextValue(PowerContext.AC, ConvertSystemToDisplay(rac, units)));
            if (pcs.RecommendedValueDC is { } rdc) recommended.Add(new ContextValue(PowerContext.DC, ConvertSystemToDisplay(rdc, units)));
            var windowsDefault = new List<ContextValue>();
            if (pcs.DefaultValueAC is { } dac) windowsDefault.Add(new ContextValue(PowerContext.AC, ConvertSystemToDisplay(dac, units)));
            if (pcs.DefaultValueDC is { } ddc) windowsDefault.Add(new ContextValue(PowerContext.DC, ConvertSystemToDisplay(ddc, units)));

            return new Setting
            {
                Id = def.Id,
                Display = BuildDisplay(def),
                Availability = BuildAvailability(def),
                Apply = BuildApply(def),
                Links = BuildLinks(def),
                UiParentId = def.ParentSettingId,
                Contexts = new[] { PowerContext.AC, PowerContext.DC },
                Targets = new List<Target> { target },
                Numeric = new Numeric
                {
                    Min = def.NumericRange!.MinValue,
                    Max = def.NumericRange.MaxValue,
                    Units = units,
                    Recommended = recommended,
                    WindowsDefault = windowsDefault,
                },
            };
        }

        var options = def.ComboBox!.Options;
        var states = new List<SettingState>(options.Count);
        foreach (var opt in options)
        {
            int value = System.Convert.ToInt32(opt.ValueMappings!["PowerCfgValue"]);
            // Context-scoped roles in a fixed order (Recommended AC, Recommended DC, WindowsDefault AC,
            // WindowsDefault DC) so the authored catalog matches the role sequence exactly. A null per-mode value
            // never equals a concrete option value, so an unset mode contributes no role.
            var roles = new List<StateRole>();
            if (pcs.RecommendedValueAC == value) roles.Add(new StateRole(RoleKind.Recommended, PowerContext.AC));
            if (pcs.RecommendedValueDC == value) roles.Add(new StateRole(RoleKind.Recommended, PowerContext.DC));
            if (pcs.DefaultValueAC == value) roles.Add(new StateRole(RoleKind.WindowsDefault, PowerContext.AC));
            if (pcs.DefaultValueDC == value) roles.Add(new StateRole(RoleKind.WindowsDefault, PowerContext.DC));

            states.Add(new SettingState
            {
                Label = opt.DisplayName,
                Set = new Dictionary<string, StateValue> { ["Power"] = StateValue.Of(value) },
                Roles = roles,
            });
        }

        return new Setting
        {
            Id = def.Id,
            Display = BuildDisplay(def),
            Availability = BuildAvailability(def),
            Apply = BuildApply(def),
            Links = BuildLinks(def),
            UiParentId = def.ParentSettingId,
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new List<Target> { target },
            States = states,
        };
    }

    /// <summary>Builds the single PowerCfgTarget for a powercfg setting: subgroup/setting GUIDs, the AC/DC mode,
    /// the display units, the optional enablement ("Attributes") key that unhides a hidden setting before reading,
    /// and the hardware-control flag.</summary>
    private static PowerCfgTarget BuildPowerCfgTarget(PowerCfgSetting pcs)
    {
        RegTarget? enablement = null;
        if (pcs.EnablementRegistrySetting is { } ers)
            enablement = new RegTarget(ers.ValueName ?? "KeyExists", new[] { ers.KeyPath }, ers.ValueName, ers.ValueType);

        return new PowerCfgTarget("Power", pcs.SubgroupGuid, pcs.SettingGuid, pcs.PowerModeSupport)
        {
            Units = pcs.Units,
            EnablementKey = enablement,
            CheckForHardwareControl = pcs.CheckForHardwareControl,
        };
    }

    /// <summary>Converts a powercfg system-unit value to the slider's display units, mirroring the old
    /// recommended-settings resolver: minutes and hours divide, everything else (milliseconds, percent) is 1:1.</summary>
    private static int ConvertSystemToDisplay(int systemValue, string? units) => units?.ToLowerInvariant() switch
    {
        "minutes" => systemValue / 60,
        "hours" => systemValue / 3600,
        _ => systemValue,
    };

    /// <summary>Everything the user sees: the source name/description/group, the icon (pack + glyph unified),
    /// the NEW-badge version, and the subjective-preference flag.</summary>
    private static Display BuildDisplay(SettingDefinition def) => new()
    {
        Name = def.Name,
        Description = def.Description,
        GroupName = def.GroupName,
        Icon = def.Icon is { } glyph
            ? new Icon(def.IconPack == "Fluent" ? IconPack.Fluent : IconPack.Material, glyph)
            : null,
        AddedInVersion = def.AddedInVersion,
        IsSubjectivePreference = def.IsSubjectivePreference,
    };

    /// <summary>Collapses the old OS/build gating flags into one build-range list (empty = every build).
    /// Windows 10/11-only are build thresholds; SupportedBuildRanges wins over min/max when present; min and
    /// max apply independently, with revision as a tie-break (mirrors the old compatibility filter).</summary>
    private static Availability BuildAvailability(SettingDefinition def)
    {
        // OS-only gating is a build threshold (Windows 11 starts at 22000). When an explicit build bound is
        // ALSO present the two INTERSECT (the old filter applies the OS rule and the bound together), so clamp
        // each bound to the OS range rather than emitting two independent ranges that would OR together.
        var osMin = def.IsWindows11Only ? new WinBuild(22000) : new WinBuild(0);
        var osMax = def.IsWindows10Only ? new WinBuild(21999, int.MaxValue) : new WinBuild(int.MaxValue, int.MaxValue);
        bool hasOs = def.IsWindows10Only || def.IsWindows11Only;

        var bounds = new List<BuildRange>();
        if (def.SupportedBuildRanges.Count > 0)
        {
            foreach (var r in def.SupportedBuildRanges)
                bounds.Add(BuildRange.Between(r.MinBuild, r.MaxBuild));
        }
        else if (def.MinimumBuildNumber is not null || def.MaximumBuildNumber is not null)
        {
            var bMin = def.MinimumBuildNumber is { } min ? new WinBuild(min, def.MinimumBuildRevision ?? 0) : new WinBuild(0);
            var bMax = def.MaximumBuildNumber is { } max ? new WinBuild(max, def.MaximumBuildRevision ?? int.MaxValue) : new WinBuild(int.MaxValue, int.MaxValue);
            bounds.Add(new BuildRange(bMin, bMax));
        }

        IReadOnlyList<BuildRange> builds;
        if (bounds.Count == 0)
        {
            builds = hasOs ? new[] { new BuildRange(osMin, osMax) } : System.Array.Empty<BuildRange>();
        }
        else
        {
            var result = new List<BuildRange>();
            foreach (var b in bounds)
            {
                var lo = b.Min >= osMin ? b.Min : osMin;   // max(bound.Min, osMin)
                var hi = b.Max <= osMax ? b.Max : osMax;   // min(bound.Max, osMax)
                if (lo <= hi) result.Add(new BuildRange(lo, hi));
            }
            builds = result;
        }

        // Power hardware-capability + unlock + existence gates (old RequiresBattery / RequiresHybridSleepCapable /
        // RequiresLid / RequiresDesktop / RequiresBrightnessSupport / RequiresAdvancedUnlock / ValidateExistence).
        // ValidateExistence defaults true catalog-wide but only governs powercfg settings, so it maps to the model
        // only for a powercfg-backed setting - a non-power setting keeps it unset (so its converted output is unchanged).
        var hardware = new List<HardwareRequirement>();
        if (def.RequiresBattery) hardware.Add(HardwareRequirement.Battery);
        if (def.RequiresHybridSleepCapable) hardware.Add(HardwareRequirement.HybridSleepCapable);
        if (def.RequiresLid) hardware.Add(HardwareRequirement.Lid);
        if (def.RequiresDesktop) hardware.Add(HardwareRequirement.Desktop);
        if (def.RequiresBrightnessSupport) hardware.Add(HardwareRequirement.BrightnessSupport);
        bool validatesExistence = def.PowerCfgSettings is { Count: > 0 } && def.ValidateExistence;

        if (builds.Count == 0 && hardware.Count == 0 && !def.RequiresAdvancedUnlock && !validatesExistence)
            return Availability.Everywhere;

        return new Availability
        {
            Builds = builds,
            Hardware = hardware,
            RequiresAdvancedUnlock = def.RequiresAdvancedUnlock,
            ValidatesExistence = validatesExistence,
        };
    }

    /// <summary>Maps the confirmation gate and the restart hints. The two old restart strings unify into one
    /// RestartTarget; a system reboot stays a separate flag because a setting may need both.</summary>
    private static ApplyBehavior BuildApply(SettingDefinition def)
    {
        RestartTarget? restart =
            !string.IsNullOrEmpty(def.RestartProcess) ? new RestartProcess(def.RestartProcess!)
            : !string.IsNullOrEmpty(def.RestartService) ? new RestartService(def.RestartService!)
            : null;

        if (!def.RequiresConfirmation && !def.RequiresRestart && restart is null)
            return ApplyBehavior.None;

        return new ApplyBehavior
        {
            RequiresConfirmation = def.RequiresConfirmation,
            RequiresReboot = def.RequiresRestart,
            Restart = restart,
        };
    }

    /// <summary>Maps the old directional dependencies + auto-enable into Links. A RequiresDisabled dependency
    /// carries no reverse cascade; auto-enable forces the target on without a reverse and re-applies it.</summary>
    private static IReadOnlyList<Link> BuildLinks(SettingDefinition def)
    {
        var links = new List<Link>();
        foreach (var dep in def.Dependencies)
        {
            // Only the two directional dependency kinds map to a Link. RequiresSpecificValue and
            // RequiresValueBeforeAnyChange are value-prerequisite relationships the old app handles on a
            // separate path; they have no Link representation, so leave them out rather than inventing an
            // enable-requirement with a reverse cascade.
            switch (dep.DependencyType)
            {
                case SettingDependencyType.RequiresEnabled:
                    links.Add(new Link(dep.RequiredSettingId, LinkKind.Requires, "Enabled"));
                    break;
                case SettingDependencyType.RequiresDisabled:
                    links.Add(new Link(dep.RequiredSettingId, LinkKind.Requires, "Disabled") { ReverseCascade = false });
                    break;
            }
        }
        if (def.AutoEnableSettingIds is { } auto)
        {
            foreach (var id in auto)
                links.Add(new Link(id, LinkKind.Enables, "Enabled") { ReverseCascade = false, Force = true });
        }
        return links;
    }

    /// <summary>Folds registry settings into targets: a mirror (same ValueName under several KeyPaths) is one
    /// target with many paths. ValueName == null groups under the "KeyExists" key (key-existence target).</summary>
    private static List<RegTarget> BuildTargets(List<IGrouping<string, RegistrySetting>> groups) =>
        groups.Select(g =>
        {
            var first = g.First();
            return new RegTarget(
                g.Key,
                g.Select(r => r.KeyPath).ToArray(),
                first.ValueName,
                first.ValueType)
            {
                ByteIndex = first.BinaryByteIndex,
                BitMask = first.BitMask,
                ByteOnly = first.ModifyByteOnly,
                CompositeStringKey = first.CompositeStringKey,
                PerNetworkInterface = first.ApplyPerNetworkInterface,
                PerMonitor = first.ApplyPerMonitor,
                IsGroupPolicy = first.IsGroupPolicy,
                // Old apply hardcoded "lock the key when the written value == 4 (service Start = Disabled)"; carry
                // that as data so the permanent model is self-describing once the converter is gone.
                LockWhenValue = first.LockKeyAccess ? 4 : (int?)null,
            };
        }).ToList();

    /// <summary>Maps one ComboBox ValueMapping scalar to the accept-value for a selection state. A null
    /// mapping means the value is absent; a concrete value matches that value when present, and also accepts
    /// absence when it equals the target's DefaultValue (old detection reads absent as the DefaultValue).</summary>
    private static StateValue ToSelectionStateValue(object? expected, object? defaultValue)
    {
        if (expected is null)
            return StateValue.Absent;
        var sv = StateValue.Of(expected);
        if (defaultValue is not null && CatalogValueComparer.AreEqual(defaultValue, expected))
            sv = sv.OrAbsent();
        return sv;
    }

    /// <summary>Maps a toggle's apply-only mechanisms to the per-state Effects the old apply runs for that state.
    /// PowerShell scripts and .reg imports only run when their body is non-empty (old guards with IsNullOrEmpty);
    /// native power always runs. Order (script -> regcontent -> native) mirrors the old apply's effect order.</summary>
    private static IReadOnlyList<Effect> BuildToggleEffects(SettingDefinition def, bool isEnabled)
    {
        var effects = new List<Effect>();

        foreach (var ps in def.PowerShellScripts)
        {
            var script = isEnabled ? ps.EnabledScript : ps.DisabledScript;
            if (!string.IsNullOrEmpty(script))
                effects.Add(new ScriptEffect(script!, ps.RunContext));
        }

        foreach (var rc in def.RegContents)
        {
            var content = isEnabled ? rc.EnabledContent : rc.DisabledContent;
            if (!string.IsNullOrEmpty(content))
                effects.Add(new RegContentEffect(content!));
        }

        foreach (var np in def.NativePowerApiSettings)
            effects.Add(new NativePowerEffect(np.InformationLevel, isEnabled ? np.EnabledValue : np.DisabledValue));

        return effects;
    }

    /// <summary>Maps a selection OPTION's apply-only script to the effect that option's state runs. The option's
    /// Script field selects which shared script body runs (mirrors the old SettingOperationExecutor selection path):
    /// Enabled -> EnabledScript, Disabled -> DisabledScript, None -> no script. The option's ScriptVariables are
    /// substituted into the body, and an empty body runs nothing. Selections carry no .reg/native effects in the
    /// catalog, so only scripts are mapped here. (An option with an UNSET Script is treated as None here; the old
    /// code instead fell through to its enable/disable default. Every catalog selection option sets Script
    /// explicitly, so this is unreachable - revisit if a script-bearing selection ever leaves an option Script unset.)</summary>
    private static IReadOnlyList<Effect> BuildSelectionOptionEffects(SettingDefinition def, ComboBoxOption opt)
    {
        var effects = new List<Effect>();

        if (opt.Script is not { } scriptOption || scriptOption == ScriptOption.None)
            return effects;

        foreach (var ps in def.PowerShellScripts)
        {
            var script = scriptOption == ScriptOption.Enabled ? ps.EnabledScript : ps.DisabledScript;
            script = SubstituteScriptVariables(script, opt.ScriptVariables);
            if (!string.IsNullOrEmpty(script))
                effects.Add(new ScriptEffect(script!, ps.RunContext));
        }

        return effects;
    }

    /// <summary>Substitutes a selection option's <c>{{key}}</c> placeholders into a script body, matching the old
    /// SettingOperationExecutor selection-script substitution. Returns the body unchanged when there are no
    /// variables or the body is empty.</summary>
    private static string? SubstituteScriptVariables(string? script, IReadOnlyDictionary<string, string>? variables)
    {
        if (string.IsNullOrEmpty(script) || variables is null)
            return script;
        foreach (var kvp in variables)
            script = script!.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
        return script;
    }

    private static IReadOnlyList<StateRole> RolesFor(bool isEnabledState, bool? recommendedIsEnabled, bool? defaultIsEnabled)
    {
        var roles = new List<StateRole>();
        if (recommendedIsEnabled is bool r && r == isEnabledState) roles.Add(new StateRole(RoleKind.Recommended));
        if (defaultIsEnabled is bool d && d == isEnabledState) roles.Add(new StateRole(RoleKind.WindowsDefault));
        return roles;
    }

    private static StateValue? ToStateValue(object?[]? arr)
    {
        if (arr is null) return null;
        var nonNull = arr.Where(v => v != null).ToArray();
        bool hasNull = arr.Any(v => v == null);
        if (nonNull.Length == 0) return StateValue.Absent;            // [null]
        if (nonNull.Length == 1) return hasNull ? StateValue.Of(nonNull[0]!).OrAbsent() : StateValue.Of(nonNull[0]!);
        return StateValue.OneOf(nonNull);                              // 2+ concrete values
    }
}
