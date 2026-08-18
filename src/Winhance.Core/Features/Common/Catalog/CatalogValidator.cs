namespace Winhance.Core.Features.Common.Catalog;

public static class CatalogValidator
{
    public static IReadOnlyList<CatalogValidationError> Validate(Setting setting)
    {
        var errors = new List<CatalogValidationError>();
        var id = setting.Id;

        // R5: no duplicate Target.Key
        foreach (var k in setting.Targets.GroupBy(t => t.Key).Where(g => g.Count() > 1).Select(g => g.Key))
            errors.Add(new CatalogValidationError(id, $"Duplicate target key '{k}'."));

        // R1: at most one IsFallback state
        var fallbackCount = setting.States.Count(s => s.IsFallback);
        if (fallbackCount > 1)
            errors.Add(new CatalogValidationError(id, $"At most one state may set IsFallback; found {fallbackCount}."));

        // R2: per context, at most one Recommended and one WindowsDefault
        foreach (var ctx in setting.Contexts)
        {
            var rec = setting.States.Count(s => s.HasRole(RoleKind.Recommended, ctx));
            if (rec > 1)
                errors.Add(new CatalogValidationError(id, $"At most one Recommended state per context ({ctx}); found {rec}."));
            var def = setting.States.Count(s => s.HasRole(RoleKind.WindowsDefault, ctx));
            if (def > 1)
                errors.Add(new CatalogValidationError(id, $"At most one WindowsDefault state per context ({ctx}); found {def}."));
        }

        // R3: every non-fallback state must have a non-empty Set (else it is undetectable)
        foreach (var s in setting.States.Where(s => !s.IsFallback && s.Set.Count == 0))
            errors.Add(new CatalogValidationError(id, $"State '{s.Label}' has an empty Set and is not IsFallback — it would be undetectable."));

        // R7: a detect-only state is not a choice, so it cannot be RECOMMENDED or be what Windows
        // ships - both roles are claims about a state the user can end up in deliberately. Read the raw
        // Roles rather than HasRole so a BUILD-SCOPED role is caught too (HasRole deliberately ignores those).
        foreach (var st in setting.States.Where(st => st.IsDetectOnly))
            foreach (var role in st.Roles.Where(r => r.Kind is RoleKind.Recommended or RoleKind.WindowsDefault))
                errors.Add(new CatalogValidationError(id,
                    $"State '{st.Label}' is IsDetectOnly and cannot carry the {role.Kind} role - it is not a state the user can choose."));

        // R4: each state's Set keys must line up with the detectable target keys.
        // A non-fallback state must cover EVERY target (so two states can't ambiguously both match by
        // one omitting a discriminating key). A fallback state is the last-resort catch-all, so it may
        // carry a partial (or empty) representative Set — it's exempt from the "missing" check. Any
        // state referencing an UNKNOWN key is always a typo, fallback or not. The whole block is
        // skipped when a custom Detector handles detection, or there are no targets.
        if (setting.Detector is null && setting.Targets.Count > 0)
        {
            var targetKeys = setting.Targets.Select(t => t.Key).ToHashSet();
            foreach (var s in setting.States.Where(s => s.Set.Count > 0))
            {
                var stateKeys = s.Set.Keys.ToHashSet();
                var missing = targetKeys.Except(stateKeys).ToList();
                var extra = stateKeys.Except(targetKeys).ToList();
                if (missing.Count > 0 && !s.IsFallback)
                    errors.Add(new CatalogValidationError(id, $"State '{s.Label}' is missing target key(s): {string.Join(", ", missing)}."));
                if (extra.Count > 0)
                    errors.Add(new CatalogValidationError(id, $"State '{s.Label}' references unknown target key(s): {string.Join(", ", extra)}."));
            }
        }

        // Self-references: a setting cannot relate to itself. (Links now live per-state.)
        foreach (var l in setting.States.SelectMany(st => st.Links).Where(l => l.OtherId == id))
            errors.Add(new CatalogValidationError(id, $"Link cannot target its own setting (self-loop) — kind {l.Kind}."));
        foreach (var st in setting.States)
            if (st.Controls is { } controls && controls.ContainsKey(id))
                errors.Add(new CatalogValidationError(id, $"State '{st.Label}' Controls cannot reference its own setting."));
        if (setting.UiParentId == id)
            errors.Add(new CatalogValidationError(id, "UiParentId cannot be its own setting."));
        if (setting.EnabledWhen?.OtherId == id)
            errors.Add(new CatalogValidationError(id, "EnabledWhen cannot reference its own setting."));

        // R6: setting-level Effects are the Action mechanism - a stateless one-shot, never detected. If a
        // setting carries any, it must have no States, Targets, or Detector. Conversely a setting that detects
        // nothing (no States, no Targets, no Detector) and does nothing (no Effects) is an authoring bug. A
        // range setting (a Target, no states, no effects) is exempt because it has a Target.
        if (setting.Effects.Count > 0
            && (setting.States.Count > 0 || setting.Targets.Count > 0 || setting.Detector is not null))
            errors.Add(new CatalogValidationError(id,
                "Setting-level Effects are only for stateless Actions: a setting with Effects must have no States, Targets, or Detector."));

        if (setting.Effects.Count == 0 && setting.States.Count == 0
            && setting.Targets.Count == 0 && setting.Detector is null)
            errors.Add(new CatalogValidationError(id,
                "Setting detects nothing and does nothing: a 0-state, 0-target, detector-less setting must carry at least one setting-level Effect."));

        return errors;
    }

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
            if (s.EnabledWhen is { } gate && !ids.Contains(gate.OtherId))
                errors.Add(new CatalogValidationError(s.Id, $"EnabledWhen target '{gate.OtherId}' is not a known setting."));

            // EVERY STATE LABEL ONE SETTING NAMES ON ANOTHER MUST RESOLVE, and must not resolve to a
            // state nothing may DEMAND. Two questions about the same lookup, so they are asked together
            // for each of the three ways a label crosses a setting boundary.
            //
            // "Does it exist" was missing until now - only the setting ID was ever checked - so a label
            // that matched no state failed silently and permanently: gaming-performance-prefetch required
            // gaming-sysmain-service in "Enabled", a label that setting has never had (its states are
            // Disabled/Manual/Automatic), so ResolveReverseCascade saw a requirement that could never be
            // met and reset Prefetch to its Windows default on ANY SysMain change. A dangling label is
            // never intentional - it is a rename or a copy/paste - and it produces behaviour no reader of
            // the catalog would predict.
            //
            // "Is it detect-only" applies to Controls and Links but NOT to EnabledWhen: those two DEMAND a
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

            if (s.EnabledWhen is { } declaredGate
                && settings.FirstOrDefault(o => o.Id == declaredGate.OtherId) is { } gateTarget)
            {
                foreach (var label in declaredGate.States.Where(label => gateTarget.States.All(os => os.Label != label)))
                    errors.Add(new CatalogValidationError(s.Id,
                        $"EnabledWhen names state '{label}' on '{declaredGate.OtherId}', which is not a state on that setting."));
            }
        }

        errors.AddRange(DetectLinkCycles(settings, ids));
        return errors;
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
