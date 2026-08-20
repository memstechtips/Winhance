using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

// UiParentId nesting is at most ONE level; the settings-page orphan-drop and ConfigReviewService's render
// predicate check only the DIRECT parent. A 2-level chain (C -> B -> A, A dropped by detection) would render C
// nowhere while the direct-parent check still counts it - reopening the "uncompleteable config review" bug for
// the deepest child.
public class CatalogUiParentConformanceTests
{
    [Fact]
    public void UiParentId_nesting_is_at_most_one_level()
    {
        var children = SettingCatalog.All.Where(s => !string.IsNullOrEmpty(s.UiParentId)).ToList();

        // Guard against a vacuous pass: there ARE sub-settings in the catalog today.
        Assert.NotEmpty(children);

        var violations = new List<string>();
        foreach (var child in children)
        {
            var parent = SettingCatalog.Find(child.UiParentId!);
            if (parent is null)
                violations.Add($"{child.Id}: UiParentId '{child.UiParentId}' references no catalog setting.");
            else if (!string.IsNullOrEmpty(parent.UiParentId))
                violations.Add($"{child.Id}: parent '{parent.Id}' itself has a UiParentId (2-level nesting).");
        }

        Assert.True(
            violations.Count == 0,
            "UiParentId must be at most one level deep (a sub-setting's parent must be top-level):\n"
                + string.Join("\n", violations));
    }
}
