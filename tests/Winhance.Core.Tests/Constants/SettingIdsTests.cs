using FluentAssertions;
using Winhance.Core.Features.Common.Constants;
using Xunit;

namespace Winhance.Core.Tests.Constants;

public class SettingIdsTests
{
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
