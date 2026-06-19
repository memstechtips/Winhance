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
}
