using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.TechnicalDetails;

// Pure: no dispatcher, logging or live reads, so every rule is unit-testable. Null when a setting has nothing to document.
public static class TechnicalDetailsBuilder
{
    public static OptionMatrix? Build(
        Setting? setting,
        SettingStateSnapshot snapshot,
        ILocalizationService loc,
        WinBuild build)
    {
        if (setting is null) return null;
        return BuildMatrix(new BuildContext(setting, snapshot, loc, build));
    }

    // ---------------------------------------------------------------------------------------------
    // The option matrix: options as rows, the values they write as columns
    // ---------------------------------------------------------------------------------------------

    // Every mechanism shares ONE table, grouped by destination, so a setting that touches both the registry and a
    // power plan documents both side by side.
    private static OptionMatrix? BuildMatrix(BuildContext ctx)
    {
        var setting = ctx.Setting;

        // Two shapes carry no States and so have no options to make rows from. Each names its own
        // rows instead, and both come back as an OptionMatrix so they share every bit of the
        // rendering below rather than growing a second table.
        if (setting.Control == ControlKind.PowerPlan) return BuildPowerPlanMatrix(ctx);
        if (setting.Control == ControlKind.Action) return BuildActionMatrix(ctx);
        if (setting.States.Count == 0) return BuildNumericMatrix(ctx);

        var targets = setting.Targets.Where(t => t is RegTarget or TaskTarget or PowerCfgTarget).ToList();
        // No target means no column to build, but the OPTIONS are still real and still carry
        // their roles. Dropping them left a panel listing one "When set to X" code block per
        // option with no way to tell which X is recommended, which is the Windows default, or
        // which one the system is on -- the one thing every other panel states. Same rows as the
        // full path below builds, with no cells to put in them.
        if (targets.Count == 0)
            return HasDocumentableContent(ctx)
                ? Matrix(ctx, [], [], OptionRows(ctx, perContext: false, _ => []))
                : null;

        var columns = new List<MatrixColumn>();
        var groups = new List<MatrixColumnGroup>();

        // Registry columns, grouped by their FULL path list. Grouping on the first path alone would
        // collapse two mirrors that share a first path but differ in the second, and then print one
        // of the two tails as though it were both.
        var regTargets = targets.OfType<RegTarget>().ToList();
        foreach (var byPaths in regTargets.GroupBy(PathKey, StringComparer.OrdinalIgnoreCase))
        {
            var start = columns.Count;
            foreach (var reg in byPaths)
                columns.Add(RegistryColumn(ctx, reg));

            var pathLabel = ctx.Text(TechnicalDetailKeys.LabelPath, "Path");
            groups.Add(new MatrixColumnGroup
            {
                Label = ctx.Text(TechnicalDetailKeys.GroupRegistry, "Registry"),
                Kind = MatrixGroupKind.Registry,
                Description = ctx.Text(TechnicalDetailKeys.DescRegistry,
                    "Read to determine which option is active, and written when you apply one."),
                // Every path, not just the first: a mirrored value really is written to all of them,
                // and saying so is what the "mirrored" chip used to gesture at without the detail.
                Paths = [.. byPaths.First().Paths.Select(p => new MatrixPath(p, pathLabel))],
                StartColumn = start,
                ColumnSpan = columns.Count - start,
                OpenRegeditTooltip = ctx.Text(TechnicalDetailKeys.OpenRegedit, "Open in Registry Editor"),
            });
        }

        var taskTargets = targets.OfType<TaskTarget>().ToList();
        foreach (var task in taskTargets)
        {
            var start = columns.Count;
            columns.Add(new MatrixColumn
            {
                Header = LeafName(task.TaskPath),
                Kind = MatrixColumnKind.Task,
            });
            groups.Add(new MatrixColumnGroup
            {
                Label = ctx.Text(TechnicalDetailKeys.GroupScheduledTask, "Scheduled task"),
                Kind = MatrixGroupKind.ScheduledTask,
                Description = ctx.Text(TechnicalDetailKeys.DescScheduledTask,
                    "Read to determine which option is active, and enabled or disabled when you apply one."),
                Paths = [new MatrixPath(task.TaskPath, ctx.Text(TechnicalDetailKeys.LabelPath, "Path"))],
                StartColumn = start,
                ColumnSpan = 1,
            });
        }

        // Powercfg. One column, not one per context: an option writes the same value plugged in as
        // it does on battery. What differs per context is WHICH option is current, recommended or
        // default, and that is carried on the role badges instead.
        var powerTargets = targets.OfType<PowerCfgTarget>().ToList();
        foreach (var pcfg in powerTargets)
        {
            var start = columns.Count;
            columns.Add(new MatrixColumn
            {
                Header = ctx.Text(TechnicalDetailKeys.ColumnPowerValue, "Value"),
                TypeName = setting.Numeric?.Units ?? pcfg.Units ?? string.Empty,
                Kind = MatrixColumnKind.Power,
                Chips = PowerChips(ctx, pcfg),
            });
            groups.Add(new MatrixColumnGroup
            {
                Label = ctx.Text(TechnicalDetailKeys.GroupPower, "Power setting"),
                Kind = MatrixGroupKind.Power,
                Description = ctx.Text(TechnicalDetailKeys.DescPower,
                    "Read from your active power plan to determine which option is active, and written with powercfg when you apply one."),
                Paths =
                [
                    new MatrixPath(pcfg.SubgroupGuid, ctx.Text(TechnicalDetailKeys.PowerCfgSubgroup, "Subgroup")),
                    new MatrixPath(pcfg.SettingGuid, ctx.Text(TechnicalDetailKeys.PowerCfgSetting, "Setting")),
                ],
                StartColumn = start,
                ColumnSpan = 1,
            });
        }

        // Script / .reg columns: a check per option rather than a chip crowding the option name.
        bool anyScript = setting.States.Any(s => s.Effects.OfType<ScriptEffect>().Any());
        bool anyRegFile = setting.States.Any(s => s.Effects.OfType<RegContentEffect>().Any());
        if (anyScript || anyRegFile)
        {
            var start = columns.Count;
            if (anyScript)
                columns.Add(new MatrixColumn
                {
                    Header = ctx.Text(TechnicalDetailKeys.ColumnScript, "Script"),
                    Kind = MatrixColumnKind.Script,
                });
            if (anyRegFile)
                columns.Add(new MatrixColumn
                {
                    Header = ctx.Text(TechnicalDetailKeys.ColumnRegFile, ".reg file"),
                    Kind = MatrixColumnKind.RegFile,
                });
            groups.Add(new MatrixColumnGroup
            {
                Label = ctx.Text(TechnicalDetailKeys.GroupAlsoRuns, "Also runs"),
                Kind = MatrixGroupKind.AlsoRuns,
                StartColumn = start,
                ColumnSpan = columns.Count - start,
            });
        }

        var orderedRegTargets = regTargets
            .GroupBy(PathKey, StringComparer.OrdinalIgnoreCase)
            .SelectMany(g => g)
            .ToList();

        // Only a powercfg setting has a per-context answer to "which option is active". Everything
        // else has one answer whatever the machine is running on.
        bool perContext = powerTargets.Count > 0;

        var options = OptionRows(ctx, perContext, i =>
        {
            var state = setting.States[i];
            var cells = new List<MatrixCell>(columns.Count);
            foreach (var reg in orderedRegTargets)
                cells.Add(new MatrixCell(ValueCell(ctx, state, reg.Key)));
            foreach (var task in taskTargets)
                cells.Add(new MatrixCell(TaskStateText(ctx, state, task.Key)));
            // A powercfg value lives in the state's Set under the target key, exactly as a registry
            // value does, so the same formatter answers both.
            foreach (var pcfg in powerTargets)
                cells.Add(new MatrixCell(ValueCell(ctx, state, pcfg.Key)));
            if (anyScript)
                cells.Add(state.Effects.OfType<ScriptEffect>().Any() ? MatrixCell.Check : MatrixCell.Empty);
            if (anyRegFile)
                cells.Add(state.Effects.OfType<RegContentEffect>().Any() ? MatrixCell.Check : MatrixCell.Empty);
            return cells;
        });

        var (readingLabel, readingCells) = BuildReading(ctx, orderedRegTargets, taskTargets, columns.Count);

        return Matrix(ctx, groups, columns, options, readingLabel, readingCells);
    }

    private static List<MatrixOption> OptionRows(
        BuildContext ctx, bool perContext, Func<int, IReadOnlyList<MatrixCell>> cells)
    {
        var states = ctx.Setting.States;
        var options = new List<MatrixOption>(states.Count);
        for (int i = 0; i < states.Count; i++)
        {
            var current = CurrentFor(ctx, i, perContext);
            var recommended = RoleFor(ctx, i, RoleKind.Recommended, perContext);
            var windowsDefault = RoleFor(ctx, i, RoleKind.WindowsDefault, perContext);

            options.Add(new MatrixOption
            {
                Label = ctx.OptionLabel(i),
                Cells = cells(i),
                IsCurrent = current.Applies,
                CurrentContext = current.Context,
                IsRecommended = recommended.Applies,
                RecommendedContext = recommended.Context,
                IsWindowsDefault = windowsDefault.Applies,
                DefaultContext = windowsDefault.Context,
            });
        }
        return options;
    }

    // Agreeing contexts report no qualifier - "Recommended" says more than "Recommended (plugged in), Recommended (on battery)".
    private static (bool Applies, string Context) RoleFor(
        BuildContext ctx, int index, RoleKind kind, bool perContext)
    {
        var setting = ctx.Setting;
        if (!perContext)
            return (setting.States[index].HasRole(kind, ctx.Build, PowerContext.Always), string.Empty);

        bool ac = RoleStateIndex(setting, kind, PowerContext.AC, ctx.Build) == index;
        if (!SeparateContexts(ctx)) return (ac, string.Empty);

        bool dc = RoleStateIndex(setting, kind, PowerContext.DC, ctx.Build) == index;
        return Qualify(ctx, ac, dc);
    }

    private static (bool Applies, string Context) CurrentFor(BuildContext ctx, int index, bool perContext)
    {
        if (!perContext) return (ctx.IsCurrentState(index), string.Empty);

        var snap = ctx.Snapshot;
        // Same rule as everywhere else: unresolved detection makes no claim about the current state.
        if (snap.Outcome != SettingDetectionOutcome.Resolved) return (false, string.Empty);

        bool ac = snap.AcValue == index;
        if (!SeparateContexts(ctx)) return (ac, string.Empty);

        return Qualify(ctx, ac, snap.DcValue == index);
    }

    // A desktop reports no battery and no separate AC/DC support, so qualifying every badge with "plugged in" would be noise.
    private static bool SeparateContexts(BuildContext ctx) =>
        ctx.Snapshot.SupportsSeparateACDC && ctx.Snapshot.HasBattery;

    private static (bool Applies, string Context) Qualify(BuildContext ctx, bool ac, bool dc) => (ac, dc) switch
    {
        (true, true) => (true, string.Empty),
        (true, false) => (true, ctx.Text(TechnicalDetailKeys.PowerPluggedIn, "Plugged In")),
        (false, true) => (true, ctx.Text(TechnicalDetailKeys.PowerOnBattery, "On Battery")),
        _ => (false, string.Empty),
    };

    private static IReadOnlyList<MatrixChip> PowerChips(BuildContext ctx, PowerCfgTarget pcfg)
    {
        var chips = new List<MatrixChip>();
        if (pcfg.EnablementKey is not null)
            chips.Add(ctx.Chip(TechnicalDetailKeys.ChipEnablementKey, "needs unlocking",
                TechnicalDetailKeys.ChipEnablementKeyTooltip,
                "Windows hides this power setting by default. Winhance unhides it before reading or writing it."));
        if (pcfg.CheckForHardwareControl)
            chips.Add(ctx.Chip(TechnicalDetailKeys.ChipHardwareControlled, "hardware may override",
                TechnicalDetailKeys.ChipHardwareControlledTooltip,
                "Your PC's firmware or drivers can override this, so Windows may not honour the value."));
        // No "separate on battery" chip. On a machine with a battery the role badges already say
        // "(On Battery)" where the contexts differ, and on one without -- where it was still being
        // shown -- there is no battery for anything to be separate on.
        return chips;
    }

    private static IReadOnlyList<MatrixChip> BuildRequirements(BuildContext ctx)
    {
        var chips = new List<MatrixChip>();
        var setting = ctx.Setting;
        var apply = setting.Apply;

        if (apply.RequiresConfirmation)
            chips.Add(ctx.Chip(TechnicalDetailKeys.ApplyConfirmation, "Asks for confirmation",
                TechnicalDetailKeys.ApplyConfirmationDetail, "Prompts before applying this setting"));
        if (apply.RequiresReboot)
            chips.Add(ctx.Chip(TechnicalDetailKeys.ApplyReboot, "Requires a system restart",
                TechnicalDetailKeys.ApplyRebootDetail, "Reboot for the change to fully take effect"));

        switch (apply.Restart)
        {
            case RestartProcess process:
                // Winhance defers this now: applying raises the bar at the bottom of the window and
                // the user restarts when ready. "Restarts a process" described the old behaviour,
                // which killed the shell out from under them.
                chips.Add(new MatrixChip(
                    ctx.Format(TechnicalDetailKeys.ApplyRestartChip, "{0} restart", process.Name),
                    ctx.Format(TechnicalDetailKeys.ApplyRestartChipDeferred,
                        "{0} has to restart for this to take effect. Winhance offers that in a bar at the "
                        + "bottom of the window once you have applied it, so you choose when.", process.Name)));
                break;
            case RestartService service:
                chips.Add(new MatrixChip(
                    ctx.Format(TechnicalDetailKeys.ApplyRestartChip, "{0} restart", service.Name),
                    ctx.Format(TechnicalDetailKeys.ApplyRestartChipService,
                        "Winhance restarts {0} when you apply this setting.", service.Name)));
                break;
        }

        var seen = new HashSet<string>();
        foreach (var link in setting.States.SelectMany(s => s.Links))
        {
            if (!seen.Add($"link:{link.Kind}:{link.OtherId}:{link.RequiredState}")) continue;
            var verb = link.Kind == LinkKind.Requires
                ? ctx.Text(TechnicalDetailKeys.RelRequires, "Requires")
                : ctx.Text(TechnicalDetailKeys.RelEnables, "Enables");
            var other = ctx.SettingName(link.OtherId);
            var automatic = ctx.Text(TechnicalDetailKeys.RelSetAutomatically, "set automatically");
            chips.Add(new MatrixChip($"{verb}: {other} ({automatic})", $"{other} = {link.RequiredState}")
            {
                LinkSettingId = link.OtherId,
                LinkText = other,
            });
        }

        // Controls joins the same chips. "This also sets X" and "this requires X" are one fact to a
        // reader -- that changing this setting changes another one too. Which of the two shapes the
        // catalog reached for is not something they should have to care about.
        foreach (var state in setting.States)
        {
            if (state.Controls is null) continue;
            foreach (var pair in state.Controls)
            {
                if (!seen.Add($"controls:{pair.Key}:{pair.Value}")) continue;
                var controlled = ctx.SettingName(pair.Key);
                chips.Add(new MatrixChip(
                    $"{ctx.Text(TechnicalDetailKeys.RelControls, "Sets")}: {controlled} ({pair.Value})",
                    $"{controlled} = {pair.Value}")
                {
                    LinkSettingId = pair.Key,
                    LinkText = controlled,
                });
            }
        }

        return chips;
    }

    // An Action carries no States: one row, the action itself. Its writes hang off Setting.Effects, so each column
    // is a RegTarget synthesised per RegistryWriteEffect - the same mapping ApplyPlanBuilder.BuildAction uses, so
    // the table cannot document a shape the engine does not perform.
    private static OptionMatrix? BuildActionMatrix(BuildContext ctx)
    {
        var setting = ctx.Setting;
        var writes = setting.Effects.OfType<RegistryWriteEffect>().ToList();

        // A script-only Action (start-menu-clean-10) has no registry write to make a column from, and a row
        // with no cells would be a label floating in an empty table. It keeps the target-less shape it has
        // today: its chips, its notes and its script.
        if (writes.Count == 0)
            return HasDocumentableContent(ctx) ? Matrix(ctx, [], [], []) : null;

        var columns = new List<MatrixColumn>();
        var groups = new List<MatrixColumnGroup>();
        var ordered = new List<RegistryWriteEffect>();

        // Grouped by path, as the target-driven table groups by path list. An effect names ONE path - there is
        // no mirror shape on an effect - so each group carries the single path its columns write to.
        foreach (var byPath in writes.GroupBy(w => w.Path, StringComparer.OrdinalIgnoreCase))
        {
            var start = columns.Count;
            foreach (var write in byPath)
            {
                ordered.Add(write);
                columns.Add(RegistryColumn(ctx, AsRegTarget(write)));
            }
            groups.Add(new MatrixColumnGroup
            {
                Label = ctx.Text(TechnicalDetailKeys.GroupRegistry, "Registry"),
                Kind = MatrixGroupKind.Registry,
                // No description. The registry one says the value is READ to determine which option is active,
                // which is untrue of an Action - it is never detected - and no existing string says "written
                // only". The per-column "written, not read" chip states it instead, with the explanation on
                // its tooltip, which is where the panel puts per-column facts anyway.
                Paths = [new MatrixPath(byPath.Key, ctx.Text(TechnicalDetailKeys.LabelPath, "Path"))],
                StartColumn = start,
                ColumnSpan = columns.Count - start,
                OpenRegeditTooltip = ctx.Text(TechnicalDetailKeys.OpenRegedit, "Open in Registry Editor"),
            });
        }

        // The row is the action, labelled with the setting's own name - the words on the button the user just
        // pressed - rather than a new "apply this action" string, which would mean editing 29 language files.
        // No role badges: current, recommended and Windows-default all answer "which option is this setting
        // on", and a one-shot is not on one.
        var row = new MatrixOption
        {
            Label = ctx.Text(SettingLocalizationKeys.Name(setting), setting.Display.Name),
            Cells = [.. ordered.Select(write => new MatrixCell(FormatConcreteValue(write.Value)))],
        };

        // No live-readings row either: it exists to say what is on the machine when detection matched no
        // option, and an Action is never detected, so there is no reading to report.
        return Matrix(ctx, groups, columns, [row]);
    }

    // Mirrors ApplyPlanBuilder.BuildAction exactly; ApplyOnly is added because an Action is never detected (written, never read back).
    private static RegTarget AsRegTarget(RegistryWriteEffect write) =>
        new RegTarget(write.ValueName, new[] { write.Path }, write.ValueName, write.Kind)
        {
            IsGroupPolicy = write.IsGroupPolicy,
            ApplyOnly = true,
            AppliesTo = write.AppliesTo,
        };

    // No options to make rows from, so the rows are the values worth naming: the Windows default, the Winhance
    // recommendation and the current value - per power context, the same rule the powercfg options follow.
    private static OptionMatrix? BuildNumericMatrix(BuildContext ctx)
    {
        var setting = ctx.Setting;
        if (setting.Numeric is not { } numeric)
            return HasDocumentableContent(ctx) ? Matrix(ctx, [], [], []) : null;

        // Only powercfg numerics are documented this way. A numeric registry setting has no group to
        // hang the row under, and inventing one would say more than we know.
        var pcfg = setting.Targets.OfType<PowerCfgTarget>().FirstOrDefault();
        if (pcfg is null) return null;

        var snap = ctx.Snapshot;
        var units = numeric.Units ?? pcfg.Units ?? string.Empty;
        var separate = SeparateContexts(ctx);

        // Every notable value, tagged with the role and context that make it notable. Grouping by
        // value afterwards is what merges "recommended plugged in" and "recommended on battery" into
        // one unqualified badge when they happen to agree.
        var marks = new List<(int Value, RoleSlot Slot, PowerContext Context)>();
        void Mark(int? value, RoleSlot slot, PowerContext context)
        {
            if (value is int number) marks.Add((number, slot, context));
        }

        Mark(ContextValue(numeric.Recommended, PowerContext.AC), RoleSlot.Recommended, PowerContext.AC);
        Mark(ContextValue(numeric.WindowsDefault, PowerContext.AC), RoleSlot.Default, PowerContext.AC);
        Mark(snap.SupportsSeparateACDC ? snap.AcNumericValue : snap.NumericValue, RoleSlot.Current, PowerContext.AC);
        if (separate)
        {
            Mark(ContextValue(numeric.Recommended, PowerContext.DC), RoleSlot.Recommended, PowerContext.DC);
            Mark(ContextValue(numeric.WindowsDefault, PowerContext.DC), RoleSlot.Default, PowerContext.DC);
            Mark(snap.DcNumericValue, RoleSlot.Current, PowerContext.DC);
        }
        if (marks.Count == 0) return null;

        var options = new List<MatrixOption>();
        foreach (var byValue in marks.GroupBy(m => m.Value).OrderBy(g => g.Key))
        {
            var current = SlotContext(ctx, byValue, RoleSlot.Current, separate);
            var recommended = SlotContext(ctx, byValue, RoleSlot.Recommended, separate);
            var windowsDefault = SlotContext(ctx, byValue, RoleSlot.Default, separate);

            options.Add(new MatrixOption
            {
                Label = units.Length > 0 ? $"{byValue.Key} {units}" : byValue.Key.ToString(),
                Cells = [new MatrixCell(byValue.Key.ToString())],
                IsCurrent = current.Applies,
                CurrentContext = current.Context,
                IsRecommended = recommended.Applies,
                RecommendedContext = recommended.Context,
                IsWindowsDefault = windowsDefault.Applies,
                DefaultContext = windowsDefault.Context,
            });
        }

        var columns = new List<MatrixColumn>
        {
            new()
            {
                Header = ctx.Text(TechnicalDetailKeys.ColumnPowerValue, "Value"),
                TypeName = units,
                Kind = MatrixColumnKind.Power,
                Chips = [.. PowerChips(ctx, pcfg), RangeChip(ctx, numeric, units)],
            },
        };

        return Matrix(ctx, PowerGroups(ctx, pcfg, columnSpan: 1), columns, options);
    }

    private static (bool Applies, string Context) SlotContext(
        BuildContext ctx, IEnumerable<(int Value, RoleSlot Slot, PowerContext Context)> marks,
        RoleSlot slot, bool separate)
    {
        var contexts = marks.Where(m => m.Slot == slot).Select(m => m.Context).ToList();
        if (contexts.Count == 0) return (false, string.Empty);
        if (!separate) return (true, string.Empty);
        return Qualify(ctx, contexts.Contains(PowerContext.AC), contexts.Contains(PowerContext.DC));
    }

    private enum RoleSlot { Current, Recommended, Default }

    // An open-ended maximum is written "0+" rather than printing int.MaxValue.
    private static MatrixChip RangeChip(BuildContext ctx, Numeric numeric, string units)
    {
        var bounded = numeric.Max < int.MaxValue;
        var range = bounded ? $"{numeric.Min}-{numeric.Max}" : $"{numeric.Min}+";
        var text = units.Length > 0 ? $"{range} {units}" : range;
        return new MatrixChip(
            ctx.Format(TechnicalDetailKeys.ChipNumericRange, "any value {0}", text),
            ctx.Text(TechnicalDetailKeys.ChipNumericRangeTooltip,
                "This setting takes a number rather than a fixed list of options."));
    }

    // The rows come from the live dropdown - the only place the scheme GUID exists.
    private static OptionMatrix? BuildPowerPlanMatrix(BuildContext ctx)
    {
        var options = new List<MatrixOption>();
        foreach (var option in ctx.Snapshot.Options)
        {
            // Builder mode carries an index and a raw loc-key label rather than a GUID, and that is
            // not live documentation of anything.
            if (option.Value is not string guid || guid.Length == 0) continue;

            var plan = option.Tag as PowerPlanComboBoxOption;
            options.Add(new MatrixOption
            {
                Label = option.DisplayText,
                Cells =
                [
                    new MatrixCell(guid),
                    new MatrixCell(plan?.ExistsOnSystem == true
                        ? ctx.Text(TechnicalDetailKeys.PowerPlanInstalled, "Installed on system")
                        : ctx.Text(TechnicalDetailKeys.PowerPlanNotInstalled, "Not installed")),
                ],
                IsCurrent = plan?.IsActive == true,
            });
        }
        if (options.Count == 0) return null;

        var columns = new List<MatrixColumn>
        {
            new() { Header = ctx.Text(TechnicalDetailKeys.ColumnPowerPlanScheme, "Scheme GUID"), Kind = MatrixColumnKind.Power },
            new() { Header = ctx.Text(TechnicalDetailKeys.ColumnPowerPlanStatus, "Status"), Kind = MatrixColumnKind.Power },
        };

        var groups = new List<MatrixColumnGroup>
        {
            new()
            {
                Label = ctx.Text(TechnicalDetailKeys.GroupPowerPlan, "Power plan"),
                Kind = MatrixGroupKind.PowerPlan,
                Description = ctx.Text(TechnicalDetailKeys.SectionPowerPlansDescription,
                    "Applying selects this power scheme, creating it if it isn't installed."),
                StartColumn = 0,
                ColumnSpan = columns.Count,
            },
        };

        return Matrix(ctx, groups, columns, options);
    }

    private static List<MatrixColumnGroup> PowerGroups(BuildContext ctx, PowerCfgTarget pcfg, int columnSpan) =>
    [
        new()
        {
            Label = ctx.Text(TechnicalDetailKeys.GroupPower, "Power setting"),
            Kind = MatrixGroupKind.Power,
            Description = ctx.Text(TechnicalDetailKeys.DescPower,
                "Read from your active power plan to determine which option is active, and written with powercfg when you apply one."),
            Paths =
            [
                new MatrixPath(pcfg.SubgroupGuid, ctx.Text(TechnicalDetailKeys.PowerCfgSubgroup, "Subgroup")),
                new MatrixPath(pcfg.SettingGuid, ctx.Text(TechnicalDetailKeys.PowerCfgSetting, "Setting")),
            ],
            StartColumn = 0,
            ColumnSpan = columnSpan,
        },
    ];

    private static OptionMatrix Matrix(
        BuildContext ctx,
        IReadOnlyList<MatrixColumnGroup> groups,
        IReadOnlyList<MatrixColumn> columns,
        IReadOnlyList<MatrixOption> options,
        string readingLabel = "",
        IReadOnlyList<MatrixCell>? readingCells = null) => new()
    {
        Groups = groups,
        Columns = columns,
        Options = options,
        OptionHeader = ctx.Text(TechnicalDetailKeys.ColumnOption, "Option"),
        RoleHeader = ctx.Text(TechnicalDetailKeys.ColumnRole, "Role"),
        PathLabel = ctx.Text(TechnicalDetailKeys.LabelPath, "Path"),
        ValueNameLabel = ctx.Text(TechnicalDetailKeys.LabelValueName, "Value name"),
        ValueTypeLabel = ctx.Text(TechnicalDetailKeys.LabelValueType, "Value type"),
        TaskLabel = ctx.Text(TechnicalDetailKeys.LabelTask, "Task"),
        // Suppressed on the same reasoning as the description below. With neither columns nor
        // options the word heads nothing at all, and on a setting documented purely by its side
        // effects it was the whole content of the header band.
        SettingLabel = columns.Count == 0 && options.Count == 0
            ? string.Empty
            : ctx.Text(TechnicalDetailKeys.SectionOptions, "Options"),
        // Only true when there IS a grid to the right. A setting documented purely by its scripts
        // and side effects has no columns, and the sentence would describe something not on screen.
        // An Action is the other exclusion: its table has a grid but no options, so "selecting an
        // option" describes something the user cannot do - they press a button and it runs.
        SettingDescription = columns.Count == 0 || ctx.Setting.Control == ControlKind.Action
            ? string.Empty
            : ctx.Text(TechnicalDetailKeys.SectionOptionsDescription,
                "Selecting an option makes the changes shown to the right."),
        Requirements = BuildRequirements(ctx),
        Notes = BuildNotes(ctx),
        CodeBlocks = BuildCodeBlocks(ctx),
        // A setting that asks before it runs does these only if you say yes -- the wallpaper prompt
        // on theme mode, the "also apply recommended settings" prompt on the Start menu and taskbar
        // cleaners. Confirmation is what makes them conditional, so it is what picks the heading.
        NotesHeading = ctx.Setting.Apply.RequiresConfirmation
            ? ctx.Text(TechnicalDetailKeys.NotesHeadingConditional,
                "Also happens when you apply, if you agree to the prompt")
            : ctx.Text(TechnicalDetailKeys.NotesHeading, "Also happens when you apply"),
        NotesDetailHeader = ctx.Text(TechnicalDetailKeys.NotesDetailHeader, "Details"),
        ReadingLabel = readingLabel,
        ReadingCells = readingCells ?? [],
        CurrentLabel = ctx.Current,
        RecommendedLabel = ctx.Recommended,
        DefaultLabel = ctx.Default,
        CurrentTooltip = ctx.Text(TechnicalDetailKeys.CurrentTooltip, "This is what your system is set to now"),
        RecommendedTooltip = ctx.Text(TechnicalDetailKeys.RecommendedTooltip, "What Winhance suggests for most people"),
        DefaultTooltip = ctx.Text(TechnicalDetailKeys.DefaultTooltip, "How Windows ships out of the box"),
    };

    private static bool HasDocumentableContent(BuildContext ctx) =>
        BuildRequirements(ctx).Count > 0 || BuildNotes(ctx).Count > 0 || BuildCodeBlocks(ctx).Count > 0;

    // Catalogs author task states as Of(true)/Of(false), so the bool payload is the answer; a state that deletes the
    // target counts as disabled.
    private static string TaskStateText(BuildContext ctx, SettingState? state, string key)
    {
        if (state is null || !state.Set.TryGetValue(key, out var value)) return string.Empty;
        bool enabled = value.WritePayload switch
        {
            bool b => b,
            int i => i != 0,
            string s when bool.TryParse(s, out var parsed) => parsed,
            _ => !value.DeleteOnWrite,
        };
        if (value.DeleteOnWrite) enabled = false;
        return enabled
            ? ctx.Text(TechnicalDetailKeys.TaskEnabled, "Enabled")
            : ctx.Text(TechnicalDetailKeys.TaskDisabled, "Disabled");
    }

    private static IReadOnlyList<MatrixNote> BuildNotes(BuildContext ctx)
    {
        var notes = new List<MatrixNote>();
        bool isAction = ctx.Setting.Control == ControlKind.Action;

        foreach (var (_, effect) in EnumerateEffects(ctx.Setting))
        {
            switch (effect)
            {
                // An Action's registry writes are the action itself and now have a column each in the
                // table above. Repeating them here said the action's own work "also happens when you
                // apply", which is exactly what made this band read as a list of side effects.
                case RegistryWriteEffect when isAction:
                    break;
                case RegistryWriteEffect write:
                    var name = string.IsNullOrEmpty(write.ValueName) ? "(Default)" : write.ValueName;
                    var suffix = write.IsGroupPolicy
                        ? $" ({ctx.Text(TechnicalDetailKeys.ChipGroupPolicy, "Group Policy")})"
                        : string.Empty;
                    notes.Add(new MatrixNote(
                        ctx.Text(TechnicalDetailKeys.EffectRegistryWrite, "Writes registry value"),
                        $"{write.Path}\\{name} = {FormatConcreteValue(write.Value)}{suffix}"));
                    break;
                case NativePowerEffect power:
                    notes.Add(new MatrixNote(
                        ctx.Text(TechnicalDetailKeys.EffectNativePower, "Native power write"),
                        $"level {power.InformationLevel} = {power.Value}"));
                    break;
            }
        }

        AddWallpaperNote(ctx, null, ctx.Setting.Effects, notes);
        for (int i = 0; i < ctx.Setting.States.Count; i++)
            AddWallpaperNote(ctx, i, ctx.Setting.States[i].Effects, notes);

        AddConfirmCheckboxNotes(ctx, notes);

        return notes;
    }

    // The one place that can say WHICH settings the confirmation checkbox applies, by name and target state - the
    // same source as the apply funnel: the feature's settings minus the trigger, each at its Recommended state.
    // Core-only: ICatalogSettingsRegistry indexes SettingCatalog.ByFeature, which is Core. The registry additionally
    // gates on hardware and powercfg existence, which Core cannot see; no feature owning an Action has a setting
    // gated on either, so the two lists agree. The OS-build gate IS applied here.
    private static void AddConfirmCheckboxNotes(BuildContext ctx, List<MatrixNote> notes)
    {
        var setting = ctx.Setting;
        if (setting.Control != ControlKind.Action || !setting.Apply.RequiresConfirmation) return;

        // No checkbox string means the prompt has no checkbox, so there is nothing to describe. Text
        // hands back the fallback on a miss, so an empty fallback makes the miss testable.
        if (ctx.Text($"Setting_{setting.Id}_ConfirmCheckbox", string.Empty).Length == 0) return;

        var featureId = FeatureIdOf(setting.Id);
        if (featureId is null || !SettingCatalog.ByFeature.TryGetValue(featureId, out var siblings)) return;

        foreach (var sibling in siblings)
        {
            if (sibling.Id == setting.Id) continue;
            // RecommendedSettingsApplier skips Actions - a one-shot carries no recommendable state - so
            // promising one here would name a setting the tick will not apply.
            if (sibling.Control == ControlKind.Action) continue;
            if (!sibling.Availability.Allows(ctx.Build)) continue;

            var state = RecommendedStateLabel(ctx, sibling);
            if (state is null) continue;   // nothing recommended: the applier skips it, so the panel does too

            notes.Add(new MatrixNote(
                ctx.Text(SettingLocalizationKeys.Name(sibling), sibling.Display.Name), state));
        }
    }

    // Resolved the same two ways the applier resolves it, so the panel cannot promise a state the apply would not
    // reach: toggles via the build-aware CatalogToggleState.GetRecommended, selections via the first UNCONDITIONAL
    // Recommended role. Null for the powercfg sliders, whose recommended value is built in Infrastructure.
    private static string? RecommendedStateLabel(BuildContext ctx, Setting setting)
    {
        if (setting.Control == ControlKind.Toggle)
            return CatalogToggleState.GetRecommended(setting, ctx.Build) switch
            {
                true => ctx.Text(TechnicalDetailKeys.On, "On"),
                false => ctx.Text(TechnicalDetailKeys.Off, "Off"),
                _ => null,
            };

        if (setting.Control != ControlKind.Selection) return null;

        for (int i = 0; i < setting.States.Count; i++)
        {
            if (!setting.States[i].HasRole(RoleKind.Recommended)) continue;
            // A catalog state Label may itself BE a localization key (the power Template_* options);
            // everything else uses the per-setting option key. Same two-step SettingLocalizationService
            // uses, so the row reads exactly like the sibling's own dropdown.
            var label = setting.States[i].Label;
            var key = SettingLocalizationKeys.IsLocalizationKey(label)
                ? label
                : SettingLocalizationKeys.OptionDisplay(setting, i);
            return ctx.Text(key, label);
        }
        return null;
    }

    private static string? FeatureIdOf(string settingId)
    {
        foreach (var (featureId, settings) in SettingCatalog.ByFeature)
            foreach (var setting in settings)
                if (setting.Id == settingId) return featureId;
        return null;
    }

    private static void AddWallpaperNote(BuildContext ctx, int? stateIndex, IReadOnlyList<Effect> effects, List<MatrixNote> notes)
    {
        var wallpapers = effects.OfType<WallpaperEffect>().ToList();
        if (wallpapers.Count == 0) return;

        var label = ctx.Text(TechnicalDetailKeys.EffectWallpaper, "Sets desktop wallpaper");
        var primary = stateIndex is int index ? $"{label} ({ctx.OptionLabel(index)})" : label;

        // A row each, rather than several paths joined by a pipe into one cell. Two wallpapers for
        // two Windows versions are two facts, and the table already knows how to show rows.
        foreach (var wallpaper in wallpapers)
        {
            notes.Add(new MatrixNote(primary, wallpaper.Path)
            {
                Scope = DescribeBuildRanges(wallpaper.AppliesTo),
            });
        }
    }

    // Columns and cells are built by two separate passes over the same grouping, so both must key on this. NUL joins
    // the paths because it is the one character a registry path cannot contain; a space would make ["A B"] and
    // ["A", "B"] the same key.
    private static string PathKey(RegTarget target) => string.Join('\0', target.Paths);

    private static MatrixColumn RegistryColumn(BuildContext ctx, RegTarget reg)
    {
        var chips = new List<MatrixChip>();
        if (reg.IsGroupPolicy)
            // The tooltip leads with what the user will actually see, because they WILL see it: once
            // a policy value is set, Windows Settings starts reporting the matching option as managed
            // and greys it out, which reads like something has taken over the PC.
            chips.Add(ctx.Chip(TechnicalDetailKeys.ChipGroupPolicy, "Group Policy",
                TechnicalDetailKeys.ChipGroupPolicyTooltip,
                "A Group Policy value. Windows treats these as managed settings, so the matching option in "
                + "Windows Settings may appear greyed out or say it is managed by your organisation. That is "
                + "expected. On a work-managed device, your organisation's policy can also override it."));
        if (ctx.IsRegContentDriven)
            chips.Add(ctx.Chip(TechnicalDetailKeys.ChipDetectionOnly, "read only to detect",
                TechnicalDetailKeys.ChipDetectionOnlyTooltip,
                "Winhance reads this value to determine which option is active. The change itself is made by the registry file below."));
        else if (reg.ApplyOnly)
            chips.Add(ctx.Chip(TechnicalDetailKeys.ChipApplyOnly, "written, not read",
                TechnicalDetailKeys.ChipApplyOnlyTooltip,
                "Winhance writes this value when you apply, but does not read it back to decide the current state."));
        // No "mirrored" chip: the group header above this column now lists every path the value is
        // written to, each with its own button. A chip saying "there is more than one place" was a
        // worse answer than showing the places.
        if (reg.ByteIndex is int byteIndex)
            chips.Add(ctx.ChipFormat(TechnicalDetailKeys.ChipPartOfValue, "byte {0}", byteIndex,
                TechnicalDetailKeys.ChipPartOfValueTooltip,
                "Only one byte inside a larger value changes; the rest of the value is left alone."));
        if (!string.IsNullOrEmpty(reg.CompositeStringKey))
            chips.Add(ctx.ChipFormat(TechnicalDetailKeys.ChipSubKey, "sub-key {0}", reg.CompositeStringKey!,
                TechnicalDetailKeys.ChipSubKeyTooltip,
                "The value holds several settings at once; only this named part of it changes."));
        if (reg.PerNetworkInterface)
            chips.Add(ctx.Chip(TechnicalDetailKeys.ChipPerNetworkInterface, "per adapter",
                TechnicalDetailKeys.ChipPerNetworkInterfaceTooltip,
                "Applied separately to every network adapter on this PC."));
        if (reg.PerMonitor)
            chips.Add(ctx.Chip(TechnicalDetailKeys.ChipPerMonitor, "per monitor",
                TechnicalDetailKeys.ChipPerMonitorTooltip, "Applied separately to every connected monitor."));
        if (reg.AppliesTo.Count > 0)
            chips.Add(ctx.Chip(TechnicalDetailKeys.ChipOsSpecific, "version-specific",
                TechnicalDetailKeys.ChipOsSpecificTooltip,
                "Only used on certain Windows versions. On others this value is left alone."));

        // Catalogs use an EMPTY value name for a key's unnamed default value, not null, so a
        // null-check alone silently produced a blank column header.
        var named = !string.IsNullOrEmpty(reg.ValueName);
        return new MatrixColumn
        {
            Header = named ? reg.ValueName! : ctx.Text(TechnicalDetailKeys.DefaultValueName, "(Default)"),
            TypeName = reg.Type.ToString(),
            Chips = chips,
            HeaderTooltip = named ? string.Empty : ctx.Text(TechnicalDetailKeys.DefaultValueNameTooltip,
                "A registry key has one unnamed value, shown as (Default) in Registry Editor. This setting uses that one."),
        };
    }

    private static string LeafName(string path)
    {
        var index = path.LastIndexOf('\\');
        return index >= 0 && index < path.Length - 1 ? path[(index + 1)..] : path;
    }

    private static string ValueCell(BuildContext ctx, SettingState state, string key) =>
        state.Set.TryGetValue(key, out var value) ? FormatStateValue(ctx, value) : string.Empty;

    private static (string Label, IReadOnlyList<MatrixCell> Cells) BuildReading(
        BuildContext ctx, IReadOnlyList<RegTarget> regTargets, IReadOnlyList<TaskTarget> taskTargets, int columnCount)
    {
        var snap = ctx.Snapshot;
        if (snap.Outcome == SettingDetectionOutcome.Resolved) return (string.Empty, []);

        var label = snap.Outcome switch
        {
            SettingDetectionOutcome.Malformed =>
                ctx.Text(TechnicalDetailKeys.ReadingMalformed, "On your system now (stored in the wrong format)"),
            SettingDetectionOutcome.Undetermined =>
                ctx.Text(TechnicalDetailKeys.ReadingUndetermined, "Winhance could not read this"),
            _ => ctx.Text(TechnicalDetailKeys.ReadingCustom, "On your system now (matches no option)"),
        };

        var unreadable = ctx.Text(TechnicalDetailKeys.ReadingUnreadable, "unknown");
        var absent = ctx.Text(TechnicalDetailKeys.ReadingAbsent, "not set");

        var cells = new List<MatrixCell>(columnCount);
        foreach (var reg in regTargets)
        {
            // Readings are keyed by the registry VALUE NAME (or "KeyExists" for a key-presence
            // target), not by the catalog target key.
            var readingKey = string.IsNullOrEmpty(reg.ValueName) ? "KeyExists" : reg.ValueName!;
            if (snap.Readings is null || !snap.Readings.TryGetValue(readingKey, out var value))
                cells.Add(new MatrixCell(unreadable));
            else
                cells.Add(new MatrixCell(value is null ? absent : FormatConcreteValue(value)));
        }
        foreach (var _ in taskTargets) cells.Add(new MatrixCell(unreadable));
        while (cells.Count < columnCount) cells.Add(MatrixCell.Empty);

        return (label, cells);
    }

    // ---------------------------------------------------------------------------------------------
    // Power (powercfg) — its own section, since AC/DC does not fit the option matrix
    // ---------------------------------------------------------------------------------------------

    // ---------------------------------------------------------------------------------------------
    // Scripts and .reg payloads, labelled by the option that runs them
    // ---------------------------------------------------------------------------------------------

    // Ordered by kind rather than by declaration so scripts and .reg payloads never interleave under one heading.
    private static IReadOnlyList<MatrixCodeBlock> BuildCodeBlocks(BuildContext ctx)
    {
        var blocks = new List<MatrixCodeBlock>();
        // Separate descriptions rather than one shared line: a script is RUN, a .reg file is
        // IMPORTED, and a sentence vague enough to cover both would tell the reader less. Both
        // strings already exist in all 29 language files, so this costs no new key.
        Collect(CodeKind.PowerShell,
            ctx.Text(TechnicalDetailKeys.SectionScripts, "PowerShell"),
            ctx.Text(TechnicalDetailKeys.SectionScriptsDescription,
                "Winhance runs this script when you apply the matching option."));
        Collect(CodeKind.RegFile,
            ctx.Text(TechnicalDetailKeys.SectionRegContent, "Registry files"),
            ctx.Text(TechnicalDetailKeys.SectionRegContentDescription,
                "Winhance imports this registry file when you apply the matching option."));
        return blocks;

        void Collect(CodeKind kind, string heading, string description)
        {
            foreach (var (state, effect) in EnumerateEffects(ctx.Setting))
            {
                var body = effect switch
                {
                    ScriptEffect script when kind == CodeKind.PowerShell => script.Script,
                    RegContentEffect reg when kind == CodeKind.RegFile => reg.Content,
                    _ => null,
                };
                if (string.IsNullOrWhiteSpace(body)) continue;
                blocks.Add(new MatrixCodeBlock(heading, ctx.CodeLabel(state), body, kind, description));
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Value formatting
    // ---------------------------------------------------------------------------------------------

    private static string FormatStateValue(BuildContext ctx, StateValue value)
    {
        if (value.WritePayload is not null)
        {
            var text = FormatConcreteValue(value.WritePayload);
            return value.AcceptsAbsent ? $"{text} ({ctx.Text(TechnicalDetailKeys.OrNotSet, "or not set")})" : text;
        }
        if (value.DeleteOnWrite) return ctx.Text(TechnicalDetailKeys.DeletesKey, "deletes key");
        return string.Empty;                                           // presence-only
    }

    private static string FormatConcreteValue(object value)
    {
        if (value is byte[] bytes)
            return bytes.Length == 0 ? "(empty)" : string.Join(" ", bytes.Select(b => b.ToString("X2")));
        var text = value.ToString() ?? string.Empty;
        return text.Length == 0 ? "\"\"" : text;
    }

    private static int? ContextValue(IReadOnlyList<ContextValue> values, PowerContext context)
    {
        var match = values.FirstOrDefault(v => v.Context == context)
                    ?? values.FirstOrDefault(v => v.Context == PowerContext.Always);
        return match?.Value;
    }

    private static int RoleStateIndex(Setting setting, RoleKind kind, PowerContext context, WinBuild build)
    {
        for (int i = 0; i < setting.States.Count; i++)
            if (setting.States[i].HasRole(kind, build, context)) return i;
        return -1;
    }

    private static IEnumerable<(int? stateIndex, Effect effect)> EnumerateEffects(Setting setting)
    {
        foreach (var effect in setting.Effects)
            yield return (null, effect);
        for (int i = 0; i < setting.States.Count; i++)
            foreach (var effect in setting.States[i].Effects)
                yield return (i, effect);
    }

    private static string DescribeBuildRanges(IReadOnlyList<BuildRange> ranges) =>
        ranges.Count == 0 ? string.Empty : string.Join(", ", ranges.Select(DescribeBuildRange));

    private static string DescribeBuildRange(BuildRange range)
    {
        if (range == BuildRange.Windows11) return "Windows 11";
        if (range == BuildRange.Windows10) return "Windows 10";
        return $"builds {range.Min.Build}-{range.Max.Build}";
    }

    private sealed class BuildContext
    {
        private readonly ILocalizationService _loc;

        public BuildContext(Setting setting, SettingStateSnapshot snapshot, ILocalizationService loc, WinBuild build)
        {
            Setting = setting;
            Snapshot = snapshot;
            Build = build;
            _loc = loc;

            Current = Text(TechnicalDetailKeys.Current, "Current");
            Recommended = Text(TechnicalDetailKeys.Recommended, "Recommended");
            Default = Text(TechnicalDetailKeys.Default, "Default");

            IsRegContentDriven = setting.Effects.OfType<RegContentEffect>().Any()
                || setting.States.Any(s => s.Effects.OfType<RegContentEffect>().Any());
        }

        public Setting Setting { get; }
        public SettingStateSnapshot Snapshot { get; }
        public WinBuild Build { get; }

        public string Current { get; }
        public string Recommended { get; }
        public string Default { get; }

        // A .reg-import setting's RegTargets are detection probes, not what it writes.
        public bool IsRegContentDriven { get; }

        public string Text(string key, string fallback) =>
            _loc.TryGetString(key, out var value) && !string.IsNullOrEmpty(value) ? value : fallback;

        public MatrixChip Chip(string key, string fallback, string tooltipKey, string tooltipFallback) =>
            new(Text(key, fallback), Text(tooltipKey, tooltipFallback));

        public MatrixChip ChipFormat(string key, string fallback, object arg, string tooltipKey, string tooltipFallback) =>
            new(Format(key, fallback, arg), Text(tooltipKey, tooltipFallback));

        // Substitutes {0} by hand: the pattern comes from a translation file a human edited, and one stray brace in any
        // of 29 languages would make string.Format throw on a machine we'll never see.
        public string Format(string key, string fallback, object arg) =>
            Text(key, fallback).Replace("{0}", arg.ToString() ?? string.Empty);

        // Never derived by matching English text against a state label.
        public string OptionLabel(int index)
        {
            if (IsToggle)
            {
                var state = index >= 0 && index < Setting.States.Count ? Setting.States[index] : null;
                bool enabled = state is not null && state.Label == "Enabled";
                return enabled ? Text(TechnicalDetailKeys.On, "On") : Text(TechnicalDetailKeys.Off, "Off");
            }
            if (index >= 0 && index < Snapshot.Options.Count) return Snapshot.Options[index].DisplayText;
            return index >= 0 && index < Setting.States.Count ? Setting.States[index].Label : string.Empty;
        }

        public string CodeLabel(int? stateIndex) => stateIndex is int index
            ? Format(TechnicalDetailKeys.CodeWhenSetTo, "When set to {0}", OptionLabel(index))
            : Text(TechnicalDetailKeys.OnApply, "On Apply");

        // Never true when detection did not resolve - that is what makes the live-readings row the only statement about the current state.
        public bool IsCurrentState(int index)
        {
            if (Snapshot.Outcome != SettingDetectionOutcome.Resolved) return false;
            if (IsToggle)
            {
                var state = Setting.States[index];
                return (state.Label == "Enabled") == Snapshot.IsSelected;
            }
            return Snapshot.SelectedIndex == index;
        }

        public string SettingName(string settingId) => Text($"Setting_{settingId}_Name", settingId);

        // ControlKind is derived from the setting's shape and is the catalog's own definition of a toggle
        // (exactly two states labelled Enabled/Disabled), so it can't drift from what detection relies on.
        private bool IsToggle => Setting.Control == ControlKind.Toggle;
    }
}
