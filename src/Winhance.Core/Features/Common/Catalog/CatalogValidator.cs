using System.Collections.Generic;
using System.Linq;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>
/// Validates a Setting's authoring against the structural invariants the detection engine relies on.
/// Pure; returns ALL violations (empty list = valid). Some rules (e.g. acyclic relationship graphs and
/// "referenced localization key exists") will be added once those parts of the model exist.
/// </summary>
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

        return errors;
    }
}
