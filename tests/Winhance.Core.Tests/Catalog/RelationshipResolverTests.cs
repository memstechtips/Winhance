using System.Collections.Generic;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class RelationshipResolverTests
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
        new() { Id = id, Display = new() { Name = id, Description = id }, States = states, Links = links };

    // currentStateOf that knows nothing (everything "unknown")
    private static string? None(string _) => null;

    [Fact]
    public void Activation_fires_requires_when_not_met()
    {
        var s = S("a", new[] { St("On"), St("Off", isDefault: true) },
            new Link("b", LinkKind.Requires, "On"));
        var actions = RelationshipResolver.ResolveForward(s, "On", None);
        Assert.Contains(actions, x => x.SettingId == "b" && x.StateLabel == "On");
    }

    [Fact]
    public void Requires_already_met_fires_nothing()
    {
        var s = S("a", new[] { St("On"), St("Off", isDefault: true) },
            new Link("b", LinkKind.Requires, "On"));
        var actions = RelationshipResolver.ResolveForward(s, "On", id => id == "b" ? "On" : null);
        Assert.DoesNotContain(actions, x => x.SettingId == "b");
    }

    [Fact]
    public void Applying_the_default_state_fires_nothing()
    {
        var s = S("a", new[] { St("On"), St("Off", isDefault: true) },
            new Link("b", LinkKind.Requires, "On"));
        Assert.Empty(RelationshipResolver.ResolveForward(s, "Off", None));
    }

    [Fact]
    public void Enables_always_fires_with_force_even_if_met()
    {
        var s = S("a", new[] { St("On"), St("Off", isDefault: true) },
            new Link("b", LinkKind.Enables, "On"));
        var actions = RelationshipResolver.ResolveForward(s, "On", id => "On"); // b already On
        var act = Assert.Single(actions, x => x.SettingId == "b");
        Assert.True(act.Force);
        Assert.Equal("On", act.StateLabel);
    }

    [Fact]
    public void Controls_on_the_state_drive_children()
    {
        var s = S("a",
            new[]
            {
                St("Deny", controls: new Dictionary<string, string> { ["c1"] = "Off", ["c2"] = "Off" }),
                St("Allow", isDefault: true),
            });
        var actions = RelationshipResolver.ResolveForward(s, "Deny", None);
        Assert.Contains(actions, x => x.SettingId == "c1" && x.StateLabel == "Off");
        Assert.Contains(actions, x => x.SettingId == "c2" && x.StateLabel == "Off");
    }

    [Fact]
    public void Unknown_target_state_returns_empty()
    {
        var s = S("a", new[] { St("On"), St("Off", isDefault: true) });
        Assert.Empty(RelationshipResolver.ResolveForward(s, "Nope", None));
    }
}
