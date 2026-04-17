using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Validation;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Core.Tests.Validation;

public class SettingCatalogValidatorTests
{
    private readonly ITestOutputHelper _output;

    public SettingCatalogValidatorTests(ITestOutputHelper output) => _output = output;

    public static IEnumerable<object[]> AllGroups()
    {
        yield return new object[] { "GamingAndPerformance", GamingAndPerformanceOptimizations.GetGamingAndPerformanceOptimizations() };
        yield return new object[] { "Notification", NotificationOptimizations.GetNotificationOptimizations() };
        yield return new object[] { "Power", PowerOptimizations.GetPowerOptimizations() };
        yield return new object[] { "PrivacyAndSecurity", PrivacyAndSecurityOptimizations.GetPrivacyAndSecurityOptimizations() };
        yield return new object[] { "Sound", SoundOptimizations.GetSoundOptimizations() };
        yield return new object[] { "Update", UpdateOptimizations.GetUpdateOptimizations() };
        yield return new object[] { "Explorer", ExplorerCustomizations.GetExplorerCustomizations() };
        yield return new object[] { "StartMenu", StartMenuCustomizations.GetStartMenuCustomizations() };
        yield return new object[] { "Taskbar", TaskbarCustomizations.GetTaskbarCustomizations() };
        yield return new object[] { "WindowsTheme", WindowsThemeCustomizations.GetWindowsThemeCustomizations() };
    }

    [Theory]
    [MemberData(nameof(AllGroups))]
    public void Selection_settings_satisfy_catalog_invariants(string featureName, SettingGroup group)
    {
        var violations = SettingCatalogValidator.Validate(group);

        foreach (var v in violations)
            _output.WriteLine($"[{featureName}] {v.SettingId}: {v.Message}");

        violations.Should().BeEmpty($"feature '{featureName}' has {violations.Count} catalog violation(s)");
    }
}
