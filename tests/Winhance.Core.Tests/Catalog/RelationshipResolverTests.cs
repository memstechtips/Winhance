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
        new()
        {
            Id = id,
            Display = new() { Name = id, Description = id },
            // Links live per-state: place them on the active/non-default states, mirroring the converter.
            States = links.Length == 0
                ? states
                : states.Select(s => s.HasRole(RoleKind.WindowsDefault) ? s : s with { Links = links }).ToList(),
        };

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
    public void Default_on_owner_fires_requires_from_its_windowsdefault_state()
    {
        // Regression: a default-ON setting whose ACTIVE state IS its WindowsDefault must still fire its
        // prerequisite when applied. Proves the old "skip forward triggers on the WindowsDefault state" is gone.
        var onDefault = new SettingState
        {
            Label = "On",
            Roles = new[] { new StateRole(RoleKind.WindowsDefault) },
            Links = new[] { new Link("b", LinkKind.Requires, "On") },
        };
        var s = new Setting
        {
            Id = "a",
            Display = new() { Name = "a", Description = "a" },
            States = new[] { onDefault, St("Off") },
        };
        var actions = RelationshipResolver.ResolveForward(s, "On", None);
        Assert.Contains(actions, x => x.SettingId == "b" && x.StateLabel == "On");
    }

    [Fact]
    public void Enables_always_fires_with_force_even_if_met()
    {
        var s = S("a", new[] { St("On"), St("Off", isDefault: true) },
            new Link("b", LinkKind.Enables, "On"));
        var actions = RelationshipResolver.ResolveForward(s, "On", id => "On");
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

    // Mirrors visual-effects-mode: the WindowsDefault state ("LetWindows") carries its OWN preset (Controls), and
    // the neutral "Custom" (no Controls, NOT the default) is a separate state. Snap-to-neutral must pick the
    // no-Controls state, NOT the WindowsDefault one.
    private static Setting Master() => S("m", new[]
    {
        St("LetWindows", isDefault: true, controls: new Dictionary<string, string> { ["c1"] = "On", ["c2"] = "Off" }),
        St("Appearance", controls: new Dictionary<string, string> { ["c1"] = "On", ["c2"] = "On" }),
        St("Performance", controls: new Dictionary<string, string> { ["c1"] = "Off", ["c2"] = "Off" }),
        St("Custom"),
    });

    [Fact]
    public void ReverseSync_snaps_parent_to_a_matching_preset()
    {
        string? Cur(string id) => id switch { "c1" => "Off", "c2" => "Off", "m" => "Custom", _ => null };
        var actions = RelationshipResolver.ResolveReverseSync("c1", new[] { Master() }, Cur);
        Assert.Contains(actions, x => x.SettingId == "m" && x.StateLabel == "Performance");
    }

    [Fact]
    public void ReverseSync_snaps_parent_to_the_no_controls_neutral_not_the_windows_default()
    {
        // c1=Off,c2=On matches no preset; master currently "Appearance" -> drops to the neutral "Custom"
        // (the no-Controls state), NOT to the WindowsDefault "LetWindows" (which carries its own preset).
        string? Cur(string id) => id switch { "c1" => "Off", "c2" => "On", "m" => "Appearance", _ => null };
        var act = Assert.Single(RelationshipResolver.ResolveReverseSync("c1", new[] { Master() }, Cur));
        Assert.Equal("m", act.SettingId);
        Assert.Equal("Custom", act.StateLabel);
    }

    [Fact]
    public void ReverseSync_no_action_when_parent_already_at_target()
    {
        string? Cur(string id) => id switch { "c1" => "Off", "c2" => "On", "m" => "Custom", _ => null };
        Assert.Empty(RelationshipResolver.ResolveReverseSync("c1", new[] { Master() }, Cur));
    }

    [Fact]
    public void ReverseSync_ignores_parents_that_dont_control_the_child()
    {
        var other = S("other", new[] { St("On"), St("Off", isDefault: true) });
        Assert.Empty(RelationshipResolver.ResolveReverseSync("c1", new[] { other }, None));
    }
}
