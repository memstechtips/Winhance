using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class CatalogRelationshipValidatorTests
{
    private static Setting S(string id, IReadOnlyList<Link>? links = null, string? uiParent = null,
        IReadOnlyDictionary<string, string>? controls = null)
    {
        // Phase 6.6: Links live per-state. Host any test links on a state so the validator (which reads
        // States.SelectMany(st => st.Links)) sees them; reuse the controls state if present.
        var states = new List<SettingState>();
        if (controls != null)
            states.Add(new SettingState { Label = "on", Controls = controls });
        if (links is { Count: > 0 })
        {
            if (states.Count > 0)
                states[0] = states[0] with { Links = links };
            else
                states.Add(new SettingState { Label = "on", Links = links });
        }
        return new()
        {
            Id = id,
            Display = new() { Name = id, Description = id },
            UiParentId = uiParent,
            States = states,
        };
    }

    [Fact]
    public void Link_self_loop_is_an_error()
    {
        var errs = CatalogValidator.Validate(S("a", links: new[] { new Link("a", LinkKind.Requires, "on") }));
        Assert.Contains(errs, e => e.Message.Contains("self-loop"));
    }

    [Fact]
    public void UiParent_self_reference_is_an_error()
    {
        Assert.Contains(CatalogValidator.Validate(S("a", uiParent: "a")), e => e.Message.Contains("UiParentId cannot be its own"));
    }

    [Fact]
    public void Controls_self_reference_is_an_error()
    {
        var errs = CatalogValidator.Validate(S("a", controls: new Dictionary<string, string> { ["a"] = "on" }));
        Assert.Contains(errs, e => e.Message.Contains("Controls cannot reference its own"));
    }

    [Fact]
    public void Duplicate_ids_are_an_error()
    {
        var errs = CatalogValidator.ValidateCatalog(new[] { S("a"), S("a") });
        Assert.Contains(errs, e => e.Message.Contains("Duplicate setting Id"));
    }

    [Fact]
    public void Link_to_missing_setting_is_an_error()
    {
        var errs = CatalogValidator.ValidateCatalog(new[] { S("a", links: new[] { new Link("ghost", LinkKind.Requires, "on") }) });
        Assert.Contains(errs, e => e.Message.Contains("Link target 'ghost' is not a known setting"));
    }

    [Fact]
    public void Controls_to_missing_child_is_an_error()
    {
        var errs = CatalogValidator.ValidateCatalog(new[] { S("a", controls: new Dictionary<string, string> { ["ghost"] = "on" }) });
        Assert.Contains(errs, e => e.Message.Contains("Controls child 'ghost' is not a known setting"));
    }

    [Fact]
    public void UiParent_to_missing_setting_is_an_error()
    {
        var errs = CatalogValidator.ValidateCatalog(new[] { S("a", uiParent: "ghost") });
        Assert.Contains(errs, e => e.Message.Contains("UiParentId 'ghost' is not a known setting"));
    }

    [Fact]
    public void Link_cycle_is_detected()
    {
        var a = S("a", links: new[] { new Link("b", LinkKind.Requires, "on") });
        var b = S("b", links: new[] { new Link("a", LinkKind.Requires, "on") });
        Assert.Contains(CatalogValidator.ValidateCatalog(new[] { a, b }), e => e.Message.Contains("cycle detected"));
    }

    [Fact]
    public void Acyclic_graph_has_no_cycle_error()
    {
        var a = S("a", links: new[] { new Link("b", LinkKind.Requires, "on") });
        var b = S("b", links: new[] { new Link("c", LinkKind.Requires, "on") });
        var c = S("c");
        Assert.DoesNotContain(CatalogValidator.ValidateCatalog(new[] { a, b, c }), e => e.Message.Contains("cycle detected"));
    }

    [Fact]
    public void Valid_catalog_has_no_errors()
    {
        var a = S("a", links: new[] { new Link("b", LinkKind.Requires, "on") }, uiParent: "b");
        var b = S("b");
        Assert.Empty(CatalogValidator.ValidateCatalog(new[] { a, b }));
    }
}
