using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class CatalogRelationshipValidatorTests
{
    private static Setting S(string id, IReadOnlyList<Link>? links = null, string? uiParent = null,
        IReadOnlyDictionary<string, string>? controls = null) =>
        new()
        {
            Id = id, Display = new() { Name = id, Description = id },
            Links = links ?? new List<Link>(),
            UiParentId = uiParent,
            States = controls == null
                ? new List<SettingState>()
                : new List<SettingState> { new() { Label = "on", Controls = controls } },
        };

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
