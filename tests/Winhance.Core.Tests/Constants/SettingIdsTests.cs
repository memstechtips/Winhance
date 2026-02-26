using FluentAssertions;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;

namespace Winhance.Core.Tests.Constants;

public class SettingIdsTests
{
    [Fact]
    public void PowerPlanSelection_MatchesDefinitionInPowerOptimizations()
    {
        var settings = PowerOptimizations.GetPowerOptimizations().Settings;
        settings.Should().Contain(s => s.Id == SettingIds.PowerPlanSelection);
    }

    [Fact]
    public void ThemeModeWindows_MatchesDefinitionInWindowsThemeCustomizations()
    {
        var settings = WindowsThemeCustomizations.GetWindowsThemeCustomizations().Settings;
        settings.Should().Contain(s => s.Id == SettingIds.ThemeModeWindows);
    }

    [Fact]
    public void UpdatesPolicyMode_MatchesDefinitionInUpdateOptimizations()
    {
        var settings = UpdateOptimizations.GetUpdateOptimizations().Settings;
        settings.Should().Contain(s => s.Id == SettingIds.UpdatesPolicyMode);
    }

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
