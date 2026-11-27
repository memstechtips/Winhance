using Winhance.Core.Features.Common.Catalog;
using Xunit;
using Winhance.TestSupport;

using Winhance.Core.Features.Common.Localization;
namespace Winhance.Core.Tests.Catalog;

public class RelationshipResolverReverseTests
{
    private static SettingState St(LocKey label, bool isDefault = false,
        IReadOnlyDictionary<string, LocKey>? controls = null) =>
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
            Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of(id)},
            // Links live per-state - place them on the active/non-default states (mirrors the converter).
            States = links.Length == 0
                ? states
                : states.Select(s => s.HasRole(RoleKind.WindowsDefault) ? s : s with { Links = links }).ToList(),
        };

    [Fact]
    public void ResolveReverseCascade_BrokenRequirement_ResetsActiveDependent()
    {
        var a = S("a", new[] { St(TestKeys.Of("On")), St(TestKeys.Of("Off"), isDefault: true) }, new Link("b", LinkKind.Requires, TestKeys.Of("On")));
        var actions = RelationshipResolver.ResolveReverseCascade("b", TestKeys.Of("Off"), new[] { a },
            id => id == "a" ? TestKeys.Of("On") : TestKeys.Of("Off"), default);
        Assert.Contains(actions, x => x.SettingId == "a" && x.StateLabel == TestKeys.Of("Off") && x.IsReset);
    }

    [Fact]
    public void ResolveReverseCascade_RequirementStillMet_ResetsNothing()
    {
        var a = S("a", new[] { St(TestKeys.Of("On")), St(TestKeys.Of("Off"), isDefault: true) }, new Link("b", LinkKind.Requires, TestKeys.Of("On")));
        Assert.Empty(RelationshipResolver.ResolveReverseCascade("b", TestKeys.Of("On"), new[] { a }, id => TestKeys.Of("On"), default));
    }

    [Fact]
    public void ResolveReverseCascade_DependentAlreadyAtDefault_IsNotReset()
    {
        var a = S("a", new[] { St(TestKeys.Of("On")), St(TestKeys.Of("Off"), isDefault: true) }, new Link("b", LinkKind.Requires, TestKeys.Of("On")));
        Assert.Empty(RelationshipResolver.ResolveReverseCascade("b", TestKeys.Of("Off"), new[] { a }, id => TestKeys.Of("Off"), default));
    }

    [Fact]
    public void ResolveReverseCascade_OptedOut_ResetsNothing()
    {
        var a = S("a", new[] { St(TestKeys.Of("On")), St(TestKeys.Of("Off"), isDefault: true) },
            new Link("b", LinkKind.Requires, TestKeys.Of("On")) { ReverseCascade = false });
        Assert.Empty(RelationshipResolver.ResolveReverseCascade("b", TestKeys.Of("Off"), new[] { a }, id => id == "a" ? TestKeys.Of("On") : TestKeys.Of("Off"), default));
    }

    [Fact]
    public void ResolveReverseSync_AllChildrenMatchAnOption_SnapsParent()
    {
        var parent = S("p", new[]
        {
            St(TestKeys.Of("Deny"), controls: new Dictionary<string, LocKey> { ["c1"] = TestKeys.Of("Off"), ["c2"] = TestKeys.Of("Off") }),
            St(TestKeys.Of("Allow"), isDefault: true, controls: new Dictionary<string, LocKey> { ["c1"] = TestKeys.Of("On"), ["c2"] = TestKeys.Of("On") }),
        });
        var actions = RelationshipResolver.ResolveReverseSync("c1", new[] { parent },
            id => id == "p" ? TestKeys.Of("Allow") : TestKeys.Of("Off"));
        Assert.Contains(actions, x => x.SettingId == "p" && x.StateLabel == TestKeys.Of("Deny") && !x.IsReset);
    }

    [Fact]
    public void ResolveReverseSync_MixedChildren_DoesNotSnapParent()
    {
        var parent = S("p", new[]
        {
            St(TestKeys.Of("Deny"), controls: new Dictionary<string, LocKey> { ["c1"] = TestKeys.Of("Off"), ["c2"] = TestKeys.Of("Off") }),
            St(TestKeys.Of("Allow"), isDefault: true, controls: new Dictionary<string, LocKey> { ["c1"] = TestKeys.Of("On"), ["c2"] = TestKeys.Of("On") }),
        });
        Assert.Empty(RelationshipResolver.ResolveReverseSync("c1", new[] { parent },
            id => id switch { "c1" => TestKeys.Of("Off"), "c2" => TestKeys.Of("On"), _ => TestKeys.Of("Allow") }));
    }

    [Fact]
    public void ResolveReverseSync_ParentAlreadyMatching_IsNotReapplied()
    {
        var parent = S("p", new[]
        {
            St(TestKeys.Of("Deny"), controls: new Dictionary<string, LocKey> { ["c1"] = TestKeys.Of("Off"), ["c2"] = TestKeys.Of("Off") }),
            St(TestKeys.Of("Allow"), isDefault: true, controls: new Dictionary<string, LocKey> { ["c1"] = TestKeys.Of("On"), ["c2"] = TestKeys.Of("On") }),
        });
        Assert.Empty(RelationshipResolver.ResolveReverseSync("c1", new[] { parent },
            id => id == "p" ? TestKeys.Of("Deny") : TestKeys.Of("Off")));
    }

    [Fact]
    public void ResolveReverseSync_SettingNotControllingChild_IsIgnored()
    {
        var other = S("o", new[] { St(TestKeys.Of("x")), St(TestKeys.Of("y"), isDefault: true) });
        Assert.Empty(RelationshipResolver.ResolveReverseSync("c1", new[] { other }, id => TestKeys.Of("Off")));
    }
}
