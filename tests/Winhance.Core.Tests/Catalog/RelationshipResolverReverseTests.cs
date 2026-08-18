using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class RelationshipResolverReverseTests
{
    private static SettingState St(string label, bool isDefault = false,
        IReadOnlyDictionary<string, string>? controls = null) =>
        new()
        {
            Label = label,
            Controls = controls,
            Roles = isDefault ? new[] { new StateRole(RoleKind.WindowsDefault) } : System.Array.Empty<StateRole>(),
        };

    private static Setting S(string id, IReadOnlyList<SettingState> states, params Link[] links) =>
        new()
        {
            Id = id,
            Display = new() { Name = id, Description = id },
            // Links live per-state - place them on the active/non-default states (mirrors the converter).
            States = links.Length == 0
                ? states
                : states.Select(s => s.HasRole(RoleKind.WindowsDefault) ? s : s with { Links = links }).ToList(),
        };

    [Fact]
    public void Broken_requirement_resets_an_active_dependent()
    {
        var a = S("a", new[] { St("On"), St("Off", isDefault: true) }, new Link("b", LinkKind.Requires, "On"));
        var actions = RelationshipResolver.ResolveReverseCascade("b", "Off", new[] { a },
            id => id == "a" ? "On" : "Off", default);
        Assert.Contains(actions, x => x.SettingId == "a" && x.StateLabel == "Off" && x.IsReset);
    }

    [Fact]
    public void Requirement_still_met_resets_nothing()
    {
        var a = S("a", new[] { St("On"), St("Off", isDefault: true) }, new Link("b", LinkKind.Requires, "On"));
        Assert.Empty(RelationshipResolver.ResolveReverseCascade("b", "On", new[] { a }, id => "On", default));
    }

    [Fact]
    public void Dependent_already_at_default_is_not_reset()
    {
        var a = S("a", new[] { St("On"), St("Off", isDefault: true) }, new Link("b", LinkKind.Requires, "On"));
        Assert.Empty(RelationshipResolver.ResolveReverseCascade("b", "Off", new[] { a }, id => "Off", default));
    }

    [Fact]
    public void Reverse_cascade_opt_out_is_respected()
    {
        var a = S("a", new[] { St("On"), St("Off", isDefault: true) },
            new Link("b", LinkKind.Requires, "On") { ReverseCascade = false });
        Assert.Empty(RelationshipResolver.ResolveReverseCascade("b", "Off", new[] { a }, id => id == "a" ? "On" : "Off", default));
    }

    [Fact]
    public void Parent_snaps_when_all_children_match_an_option()
    {
        var parent = S("p", new[]
        {
            St("Deny", controls: new Dictionary<string, string> { ["c1"] = "Off", ["c2"] = "Off" }),
            St("Allow", isDefault: true, controls: new Dictionary<string, string> { ["c1"] = "On", ["c2"] = "On" }),
        });
        var actions = RelationshipResolver.ResolveReverseSync("c1", new[] { parent },
            id => id == "p" ? "Allow" : "Off");
        Assert.Contains(actions, x => x.SettingId == "p" && x.StateLabel == "Deny" && !x.IsReset);
    }

    [Fact]
    public void Parent_does_not_snap_when_children_are_mixed()
    {
        var parent = S("p", new[]
        {
            St("Deny", controls: new Dictionary<string, string> { ["c1"] = "Off", ["c2"] = "Off" }),
            St("Allow", isDefault: true, controls: new Dictionary<string, string> { ["c1"] = "On", ["c2"] = "On" }),
        });
        Assert.Empty(RelationshipResolver.ResolveReverseSync("c1", new[] { parent },
            id => id switch { "c1" => "Off", "c2" => "On", _ => "Allow" }));
    }

    [Fact]
    public void Parent_already_in_matching_state_is_not_reapplied()
    {
        var parent = S("p", new[]
        {
            St("Deny", controls: new Dictionary<string, string> { ["c1"] = "Off", ["c2"] = "Off" }),
            St("Allow", isDefault: true, controls: new Dictionary<string, string> { ["c1"] = "On", ["c2"] = "On" }),
        });
        Assert.Empty(RelationshipResolver.ResolveReverseSync("c1", new[] { parent },
            id => id == "p" ? "Deny" : "Off"));
    }

    [Fact]
    public void Setting_not_controlling_the_child_is_ignored()
    {
        var other = S("o", new[] { St("x"), St("y", isDefault: true) });
        Assert.Empty(RelationshipResolver.ResolveReverseSync("c1", new[] { other }, id => "Off"));
    }
}
