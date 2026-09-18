namespace Winhance.Core.Features.Common.Catalog;

public static class CatalogValidator
{
    public static IReadOnlyList<CatalogValidationError> Validate(Setting setting)
    {
        var errors = new List<CatalogValidationError>();
        var id = setting.Id;

        foreach (var k in setting.Targets.GroupBy(t => t.Key).Where(g => g.Count() > 1).Select(g => g.Key))
            errors.Add(new CatalogValidationError(id, $"Duplicate target key '{k}'."));

        var fallbackCount = setting.States.Count(s => s.IsFallback);
        if (fallbackCount > 1)
            errors.Add(new CatalogValidationError(id, $"At most one state may set IsFallback; found {fallbackCount}."));

        foreach (var ctx in setting.Contexts)
        {
            var rec = setting.States.Count(s => s.HasRole(RoleKind.Recommended, ctx));
            if (rec > 1)
                errors.Add(new CatalogValidationError(id, $"At most one Recommended state per context ({ctx}); found {rec}."));
            var def = setting.States.Count(s => s.HasRole(RoleKind.WindowsDefault, ctx));
            if (def > 1)
                errors.Add(new CatalogValidationError(id, $"At most one WindowsDefault state per context ({ctx}); found {def}."));
        }

        foreach (var s in setting.States.Where(s => !s.IsFallback && s.Set.Count == 0 && !setting.IsAnswerFileOnly && setting.Detector is null))
            errors.Add(new CatalogValidationError(id, $"State '{s.Label}' has an empty Set, is not IsFallback and the setting has no Detector — it would be undetectable."));

        // A detect-only state is not a choice, so it cannot be RECOMMENDED or be what Windows
        // ships - both roles are claims about a state the user can end up in deliberately. Read the raw
        // Roles rather than HasRole so a BUILD-SCOPED role is caught too (HasRole deliberately ignores those).
        foreach (var st in setting.States.Where(st => st.IsDetectOnly))
            foreach (var role in st.Roles.Where(r => r.Kind is RoleKind.Recommended or RoleKind.WindowsDefault))
                errors.Add(new CatalogValidationError(id,
                    $"State '{st.Label}' is IsDetectOnly and cannot carry the {role.Kind} role - it is not a state the user can choose."));

        // Each state's Set keys must line up with the detectable target keys.
        // A non-fallback state must cover EVERY target (so two states can't ambiguously both match by
        // one omitting a discriminating key). A fallback state is the last-resort catch-all, so it may
        // carry a partial (or empty) representative Set — it's exempt from the "missing" check. Any
        // state referencing an UNKNOWN key is always a typo, fallback or not. The whole block is
        // skipped when a custom Detector handles detection, or there are no targets. A keyed selection's
        // state names one option by its key alone, so it is exempt from the "missing" check too.
        if (setting.Detector is null && setting.Targets.Count > 0)
        {
            var targetKeys = setting.Targets.Select(t => t.Key).ToHashSet();
            foreach (var s in setting.States.Where(s => s.Set.Count > 0))
            {
                var stateKeys = s.Set.Keys.ToHashSet();
                var missing = targetKeys.Except(stateKeys).ToList();
                var extra = stateKeys.Except(targetKeys).ToList();
                if (missing.Count > 0 && !s.IsFallback && !setting.IsAnswerFileOnly && setting.Control != ControlKind.KeyedSelection)
                    errors.Add(new CatalogValidationError(id, $"State '{s.Label}' is missing target key(s): {string.Join(", ", missing)}."));
                if (extra.Count > 0)
                    errors.Add(new CatalogValidationError(id, $"State '{s.Label}' references unknown target key(s): {string.Join(", ", extra)}."));
            }
        }

        foreach (var l in setting.States.SelectMany(st => st.Links).Where(l => l.OtherId == id))
            errors.Add(new CatalogValidationError(id, $"Link cannot target its own setting (self-loop) — kind {l.Kind}."));
        foreach (var st in setting.States)
            if (st.Controls is { } controls && controls.ContainsKey(id))
                errors.Add(new CatalogValidationError(id, $"State '{st.Label}' Controls cannot reference its own setting."));
        if (setting.UiParentId == id)
            errors.Add(new CatalogValidationError(id, "UiParentId cannot be its own setting."));
        foreach (var (name, _) in Gates(setting).Where(g => g.Gate.OtherId == id))
            errors.Add(new CatalogValidationError(id, $"{name} cannot reference its own setting."));

        // Setting-level Effects are the Action mechanism - a stateless one-shot, never detected. If a
        // setting carries any, it must have no States, Targets, or Detector. Conversely a setting that detects
        // nothing (no States, no Targets, no Detector) and does nothing (no Effects) is an authoring bug. A
        // range setting (a Target, no states, no effects) is exempt because it has a Target, and a keyed selection
        // because its feature service both lists and applies for it.
        if (setting.Effects.Count > 0
            && (setting.States.Count > 0 || setting.Targets.Count > 0 || setting.Detector is not null))
            errors.Add(new CatalogValidationError(id,
                "Setting-level Effects are only for stateless Actions: a setting with Effects must have no States, Targets, or Detector."));

        if (setting.Effects.Count == 0 && setting.States.Count == 0
            && setting.Targets.Count == 0 && setting.Detector is null && setting.Options is null)
            errors.Add(new CatalogValidationError(id,
                "Setting detects nothing and does nothing: a 0-state, 0-target, detector-less setting must carry at least one setting-level Effect."));

        if (setting.Control == ControlKind.KeyedSelection)
        {
            var list = setting.Options!;
            var stateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var st in setting.States)
            {
                if (KeyedOptions.KeyOf(setting, st) is not { } key)
                    errors.Add(new CatalogValidationError(id,
                        $"State '{st.Label}' on a keyed selection names no key: its Set must give a string to a read and written target whose From is Key."));
                else if (!stateKeys.Add(key))
                    errors.Add(new CatalogValidationError(id, $"State '{st.Label}' repeats the key '{key}'."));
                if (st.Roles.Count > 0 || st.Effects.Count > 0 || st.Controls is not null || st.Links.Count > 0)
                    errors.Add(new CatalogValidationError(id,
                        $"State '{st.Label}' on a keyed selection is a named option and may not carry roles, effects, controls or links."));
            }
            if (setting.Numeric is not null)
                errors.Add(new CatalogValidationError(id,
                    "A keyed selection may not declare a Numeric range; it would be silently ignored."));
            // A power plan is activated rather than written, so its list declares no written target.
            if (list.Source != OptionSource.PowerPlans && !setting.Targets.OfType<RegTarget>().Any(t => !t.ReadOnly))
                errors.Add(new CatalogValidationError(id,
                    "A keyed selection must write the chosen option to at least one registry target that is not ReadOnly."));

            bool listsSubkeys = list.Source is OptionSource.TimeZones or OptionSource.KeyboardLayouts;
            if (listsSubkeys && (string.IsNullOrEmpty(list.Path) || string.IsNullOrEmpty(list.LabelValue)))
                errors.Add(new CatalogValidationError(id,
                    $"A {list.Source} list reads its options from registry subkeys, so it must name their Path and LabelValue."));
            if (!listsSubkeys && (list.Path is not null || list.LabelValue is not null))
                errors.Add(new CatalogValidationError(id,
                    $"A {list.Source} list does not read registry subkeys; its Path and LabelValue would be silently ignored."));
            if (list.Keys.Count > 0 && list.Source != OptionSource.Colors)
                errors.Add(new CatalogValidationError(id,
                    $"Only a Colors list declares fixed Keys; a {list.Source} list would silently ignore them."));
        }
        else
        {
            foreach (var target in setting.Targets.Where(t => t.From != OptionValue.Key))
                errors.Add(new CatalogValidationError(id,
                    $"Target '{target.Key}' takes From {target.From}, which only a keyed selection's service computes."));
        }

        if (setting.Control == ControlKind.TextBox)
        {
            bool slideshow = setting.Targets.OfType<DesktopSlideshowTarget>().Any();
            if (setting.States.Count > 0)
                errors.Add(new CatalogValidationError(id, $"A TextBox setting may not declare states; found {setting.States.Count}."));
            if (!setting.Targets.Any(t => t is AutounattendTarget) && !slideshow)
                errors.Add(new CatalogValidationError(id, "A TextBox setting must declare at least one target to write to."));
            if (setting.Targets.Any(t => t is not AutounattendTarget && t is not DesktopSlideshowTarget && t is not RegTarget { ReadOnly: true }))
                errors.Add(new CatalogValidationError(id, "A TextBox setting writes to the answer file or the desktop slideshow; every target must be an answer-file target, a desktop slideshow, or a ReadOnly registry target that seeds it."));
            if (setting.Targets.OfType<DesktopSlideshowTarget>().Count() > 1)
                errors.Add(new CatalogValidationError(id, "A TextBox setting names one desktop slideshow, not two."));
            // The answer file has no element for the slideshow folder, so a script sets it on a new install.
            if (slideshow && setting.CustomStateScripts.Count == 0)
                errors.Add(new CatalogValidationError(id, "A TextBox setting on the desktop slideshow must carry the CustomStateScripts the answer file runs for the folder."));
            if (slideshow)
            {
                foreach (var key in new[] { "BackgroundType", "SlideshowDirectoryPath1" })
                {
                    if (!setting.Targets.Any(t => t is RegTarget { ReadOnly: true } && t.Key == key))
                        errors.Add(new CatalogValidationError(id, $"A TextBox setting on the desktop slideshow must declare a ReadOnly registry target keyed '{key}'; the current album is read from it, and detection fails without it."));
                }
            }
            if (setting.TextBox!.SeedKey is { } seedKey && !SeedsFrom(setting, seedKey))
                errors.Add(new CatalogValidationError(id, $"TextBox.SeedKey names '{seedKey}', which is not a ReadOnly registry target on this setting."));
        }

        // Checks the property, not Control: an option list or a Numeric wins the derivation over a TextBox.
        if (setting.TextBox is not null && (setting.Numeric is not null || setting.Options is not null))
            errors.Add(new CatalogValidationError(id, "A TextBox setting may not declare a Numeric range or an option list; it would be silently ignored."));

        if (setting.Control == ControlKind.List)
        {
            if (setting.States.Count > 0)
                errors.Add(new CatalogValidationError(id, $"A List setting may not declare states; found {setting.States.Count}."));
            if (setting.Targets.OfType<AutounattendElement>().Count() != 1
                || setting.Targets.Any(t => t is not AutounattendElement && t is not RegTarget { ReadOnly: true }))
                errors.Add(new CatalogValidationError(id, "A List setting declares exactly one answer-file element, plus ReadOnly registry targets for its seed."));
            if (setting.List!.Fields.Count == 0)
                errors.Add(new CatalogValidationError(id, "A List setting must declare at least one field to type into."));
            foreach (var k in setting.List.Fields.GroupBy(f => f.Key).Where(g => g.Count() > 1).Select(g => g.Key))
                errors.Add(new CatalogValidationError(id, $"Duplicate field key '{k}'."));
            if (setting.List.Seed is { } seed)
                foreach (var key in seed.Values)
                    if (!SeedsFrom(setting, key))
                        errors.Add(new CatalogValidationError(id, $"List.Seed names '{key}', which is not a ReadOnly registry target on this setting."));
        }

        if (setting.List is not null && (setting.TextBox is not null || setting.Numeric is not null || setting.Options is not null))
            errors.Add(new CatalogValidationError(id, "A List setting may not declare a TextBox rule, a Numeric range or an option list; the list would be silently ignored."));

        // A selection that borrows Checked or Unchecked would read as a check box to every two-state consumer.
        if (setting.Control != ControlKind.CheckBox
            && setting.States.Any(s => s.Label == TwoState.OnLabel(ControlKind.CheckBox) || s.Label == TwoState.OffLabel(ControlKind.CheckBox)))
            errors.Add(new CatalogValidationError(id,
                "Checked/Unchecked may only appear as the exact CheckBox pair."));

        if (setting.IsAnswerFileOnly && setting.States.Count > 0
            && setting.States.Count(s => s.HasRole(RoleKind.WindowsDefault)) != 1)
            errors.Add(new CatalogValidationError(id, "An answer-file-only setting needs exactly one WindowsDefault state to seed from."));

        // A keyed selection may mix them: its key is the same string on both sides (a time-zone id, a culture name).
        bool anyUnattend = setting.Targets.Any(t => t is AutounattendTarget)
            || setting.Effects.Any(e => e is AutounattendCommand or AutounattendFirstLogonCommand)
            || setting.States.Any(s => s.Effects.Any(e => e is AutounattendCommand or AutounattendFirstLogonCommand));
        if (anyUnattend && !setting.IsAnswerFileOnly && setting.Control != ControlKind.KeyedSelection)
            errors.Add(new CatalogValidationError(id, "Setting mixes an answer-file mechanism with a machine mechanism."));

        // On any other live setting a ReadOnly target is one the apply silently skips.
        foreach (var reg in setting.Targets.OfType<RegTarget>())
        {
            if (reg.ReadOnly && reg.ApplyOnly)
                errors.Add(new CatalogValidationError(id, $"Registry target '{reg.Key}' is both ReadOnly and ApplyOnly."));
            if (reg.ClearedOnApply && !reg.ReadOnly)
                errors.Add(new CatalogValidationError(id, $"Registry target '{reg.Key}' is ClearedOnApply but not ReadOnly."));
            if (reg.ReadOnly && !setting.IsAnswerFileOnly && setting.Control is not (ControlKind.TextBox or ControlKind.List or ControlKind.KeyedSelection))
                errors.Add(new CatalogValidationError(id, $"Registry target '{reg.Key}' is ReadOnly, which seeds a card from this PC; a live setting reads and writes its targets."));
        }

        var autounattendKeys = setting.Targets.OfType<AutounattendTarget>().Select(t => t.Key).ToHashSet();
        foreach (var s in setting.States)
            foreach (var (key, value) in s.Set)
                if (autounattendKeys.Contains(key) && value.AcceptsAnyPresent)
                    errors.Add(new CatalogValidationError(id,
                        $"State '{s.Label}' uses Exists for answer-file target '{key}'; an element is written with Of(text) or skipped with Absent."));

        return errors;
    }

    // A seed names a ReadOnly registry target, or the desktop slideshow, which is read back as well as written.
    private static bool SeedsFrom(Setting setting, string targetKey) =>
        setting.Targets.Any(t => t is RegTarget { ReadOnly: true } reg && reg.Key == targetKey)
        || setting.Targets.Any(t => t is DesktopSlideshowTarget && t.Key == targetKey);

    // The Link graph must be acyclic: an auto-applied requirement that loops back would recurse without this guard.
    public static IReadOnlyList<CatalogValidationError> ValidateCatalog(IReadOnlyList<Setting> settings)
    {
        var errors = new List<CatalogValidationError>();

        foreach (var g in settings.GroupBy(s => s.Id).Where(g => g.Count() > 1))
            errors.Add(new CatalogValidationError(g.Key, $"Duplicate setting Id '{g.Key}' ({g.Count()} settings)."));

        var ids = new HashSet<string>(settings.Select(s => s.Id));

        foreach (var s in settings)
        {
            foreach (var l in s.States.SelectMany(st => st.Links).Where(l => !ids.Contains(l.OtherId)))
                errors.Add(new CatalogValidationError(s.Id, $"Link target '{l.OtherId}' is not a known setting."));
            foreach (var st in s.States)
                if (st.Controls is { } controls)
                    foreach (var childId in controls.Keys.Where(c => !ids.Contains(c)))
                        errors.Add(new CatalogValidationError(s.Id, $"Controls child '{childId}' is not a known setting."));
            if (s.UiParentId is { } parent && !ids.Contains(parent))
                errors.Add(new CatalogValidationError(s.Id, $"UiParentId '{parent}' is not a known setting."));
            foreach (var (name, gate) in Gates(s).Where(g => !ids.Contains(g.Gate.OtherId)))
                errors.Add(new CatalogValidationError(s.Id, $"{name} target '{gate.OtherId}' is not a known setting."));

            // EVERY STATE LABEL ONE SETTING NAMES ON ANOTHER MUST RESOLVE, and must not resolve to a
            // state nothing may DEMAND. Two questions about the same lookup, so they are asked together
            // for each of the three ways a label crosses a setting boundary.
            //
            // Without the existence check a label that matches no state fails silently and permanently:
            // gaming-performance-prefetch once required gaming-sysmain-service in "Enabled", a label that setting
            // has never had (its states are Disabled/Manual/Automatic), so ResolveReverseCascade saw a requirement
            // that could never be met and reset Prefetch to its Windows default on ANY SysMain change. A dangling
            // label is never intentional - it is a rename or a copy/paste - and it produces behaviour no reader of
            // the catalog would predict.
            //
            // "Is it detect-only" applies to Controls and Links but NOT to the gates: those two DEMAND a
            // state (an apply that targets an unchoosable one writes nothing), while a gate merely OBSERVES
            // one. "Usable while the master reads Mixed" is a perfectly sane thing for a gate to say.
            foreach (var l in s.States.SelectMany(st => st.Links))
            {
                var other = settings.FirstOrDefault(o => o.Id == l.OtherId);
                if (other is null)
                    continue; // unknown target - already reported above
                var required = other.States.FirstOrDefault(os => os.Label == l.RequiredState);
                if (required is null)
                    errors.Add(new CatalogValidationError(s.Id,
                        $"Link {l.Kind} '{l.OtherId}' names required state '{l.RequiredState}', which is not a state on that setting."));
                else if (required.IsDetectOnly)
                    errors.Add(new CatalogValidationError(s.Id,
                        $"Link {l.Kind} '{l.OtherId}' names required state '{l.RequiredState}', which is a detect-only state on that setting."));
            }

            foreach (var st in s.States)
            {
                if (st.Controls is not { } controls)
                    continue;
                foreach (var entry in controls)
                {
                    var child = settings.FirstOrDefault(c => c.Id == entry.Key);
                    if (child is null)
                        continue; // unknown child - already reported above
                    var wanted = child.States.FirstOrDefault(cs => cs.Label == entry.Value);
                    if (wanted is null)
                        errors.Add(new CatalogValidationError(s.Id,
                            $"State '{st.Label}' Controls '{entry.Key}' into '{entry.Value}', which is not a state on that setting."));
                    else if (wanted.IsDetectOnly)
                        errors.Add(new CatalogValidationError(s.Id,
                            $"State '{st.Label}' Controls '{entry.Key}' into '{entry.Value}', which is a detect-only state on that setting."));
                }
            }

            foreach (var (name, declaredGate) in Gates(s))
            {
                if (settings.FirstOrDefault(o => o.Id == declaredGate.OtherId) is not { } gateTarget)
                    continue; // unknown target - already reported above
                foreach (var label in declaredGate.States.Where(label => gateTarget.States.All(os => os.Label != label)))
                    errors.Add(new CatalogValidationError(s.Id,
                        $"{name} names state '{label}' on '{declaredGate.OtherId}', which is not a state on that setting."));
            }
        }

        errors.AddRange(DetectLinkCycles(settings, ids));
        return errors;
    }

    private static IEnumerable<(string Name, StateGate Gate)> Gates(Setting setting)
    {
        if (setting.EnabledWhen is { } enabledWhen)
            yield return (nameof(Setting.EnabledWhen), enabledWhen);
        if (setting.VisibleWhen is { } visibleWhen)
            yield return (nameof(Setting.VisibleWhen), visibleWhen);
    }

    private static IReadOnlyList<CatalogValidationError> DetectLinkCycles(IReadOnlyList<Setting> settings, HashSet<string> ids)
    {
        var adj = new Dictionary<string, List<string>>();
        foreach (var s in settings)
        {
            if (!adj.ContainsKey(s.Id)) adj[s.Id] = new List<string>();
            foreach (var l in s.States.SelectMany(st => st.Links))
                if (ids.Contains(l.OtherId) && !adj[s.Id].Contains(l.OtherId))
                    adj[s.Id].Add(l.OtherId);
        }

        var color = new Dictionary<string, int>();   // 0=white, 1=gray (on stack), 2=black
        var reported = new HashSet<string>();
        var errors = new List<CatalogValidationError>();

        foreach (var node in adj.Keys)
            if (!color.ContainsKey(node))
                Visit(node, adj, color, errors, reported);

        return errors;
    }

    private static void Visit(string node, Dictionary<string, List<string>> adj,
        Dictionary<string, int> color, List<CatalogValidationError> errors, HashSet<string> reported)
    {
        color[node] = 1;
        foreach (var next in adj[node])
        {
            if (!color.TryGetValue(next, out var c) || c == 0)
                Visit(next, adj, color, errors, reported);
            else if (c == 1 && reported.Add(next))
                errors.Add(new CatalogValidationError(next, $"Link relationship cycle detected involving '{next}'."));
        }
        color[node] = 2;
    }
}
