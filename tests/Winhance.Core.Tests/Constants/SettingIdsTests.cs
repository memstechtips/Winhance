using FluentAssertions;
using Winhance.Core.Features.Common.Constants;
using Xunit;

namespace Winhance.Core.Tests.Constants;

public class SettingIdsTests
{
    // The 3 def-provider parity facts (SettingIds.X == the old def's Id) were retired with the
    // SettingDefinition teardown (Plan-4 T7b); the id contract is now pinned by the catalog authoring/
    // conformance suites. These two def-free invariants survive.
    [Fact]
    public void Constants_AreNonEmpty()
    {
        SettingIds.PowerPlanSelection.Should().NotBeNullOrWhiteSpace();
        SettingIds.ThemeModeWindows.Should().NotBeNullOrWhiteSpace();
        SettingIds.UpdatesPolicyMode.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Constants_AreDistinct()
    {
        var ids = new[] { SettingIds.PowerPlanSelection, SettingIds.ThemeModeWindows, SettingIds.UpdatesPolicyMode };
        ids.Should().OnlyHaveUniqueItems();
    }
}
