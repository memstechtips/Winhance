using System;
using System.Collections.Generic;
using System.Linq;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>
/// Works out the follow-on applies that one setting change triggers through its relationships — pure; no
/// I/O. Forward relationships fire only when the owner moves to a non-default ("active") state, matching
/// the app's existing enable-triggered behaviour.
/// </summary>
public static class RelationshipResolver
{
    /// <summary>
    /// The applies triggered by putting <paramref name="setting"/> into <paramref name="targetStateLabel"/>:
    /// its Requires prerequisites (only when not already met), its Enables targets (always, force), and the
    /// children its target state Controls. Empty when the target state is the WindowsDefault (a deactivation)
    /// or unknown. <paramref name="currentStateOf"/> returns a setting's current state label (null = unknown).
    /// </summary>
    public static IReadOnlyList<ApplyAction> ResolveForward(
        Setting setting, string targetStateLabel, Func<string, string?> currentStateOf)
    {
        var actions = new List<ApplyAction>();

        var targetState = setting.States.FirstOrDefault(s => s.Label == targetStateLabel);
        if (targetState is null)
            return actions;

        if (targetState.HasRole(RoleKind.WindowsDefault))
            return actions; // applying the default state is a deactivation — no forward triggers

        foreach (var link in setting.Links.Where(l => l.Kind == LinkKind.Requires))
            if (currentStateOf(link.OtherId) != link.RequiredState)
                actions.Add(new ApplyAction(link.OtherId, link.RequiredState, link.Force));

        foreach (var link in setting.Links.Where(l => l.Kind == LinkKind.Enables))
            actions.Add(new ApplyAction(link.OtherId, link.RequiredState, Force: true));

        if (targetState.Controls is { } controls)
            foreach (var (childId, childState) in controls)
                actions.Add(new ApplyAction(childId, childState));

        return actions;
    }

    /// <summary>
    /// When <paramref name="changedSettingId"/> moves to <paramref name="newStateLabel"/>, the dependents
    /// whose Requires link on it is now broken (and that opt into reverse cascade) reset to their own
    /// default state. Only dependents currently away from their default are reset.
    /// </summary>
    public static IReadOnlyList<ApplyAction> ResolveReverseCascade(
        string changedSettingId, string newStateLabel,
        IReadOnlyList<Setting> allSettings, Func<string, string?> currentStateOf)
    {
        var actions = new List<ApplyAction>();

        foreach (var dependent in allSettings)
        {
            bool broken = dependent.Links.Any(l =>
                l.Kind == LinkKind.Requires &&
                l.OtherId == changedSettingId &&
                l.ReverseCascade &&
                l.RequiredState != newStateLabel);
            if (!broken)
                continue;

            var defaultState = dependent.States.FirstOrDefault(s => s.HasRole(RoleKind.WindowsDefault))?.Label;
            if (defaultState != null && currentStateOf(dependent.Id) != defaultState)
                actions.Add(new ApplyAction(dependent.Id, defaultState));
        }

        return actions;
    }

    /// <summary>
    /// When <paramref name="changedChildId"/> changes, snap any parent that Controls it to the first of the
    /// parent's states whose Controls are now ALL satisfied by the children's current states. Parents
    /// already in that state, and parents where no state fully matches, are left as they are.
    /// </summary>
    public static IReadOnlyList<ApplyAction> ResolveReverseSync(
        string changedChildId, IReadOnlyList<Setting> allSettings, Func<string, string?> currentStateOf)
    {
        var actions = new List<ApplyAction>();

        foreach (var parent in allSettings)
        {
            bool controlsChild = parent.States.Any(st => st.Controls?.ContainsKey(changedChildId) == true);
            if (!controlsChild)
                continue;

            foreach (var state in parent.States.Where(s => s.Controls is { Count: > 0 }))
            {
                bool allSatisfied = state.Controls!.All(kv => currentStateOf(kv.Key) == kv.Value);
                if (allSatisfied)
                {
                    if (currentStateOf(parent.Id) != state.Label)
                        actions.Add(new ApplyAction(parent.Id, state.Label));
                    break; // first fully-matching state wins
                }
            }
        }

        return actions;
    }
}
