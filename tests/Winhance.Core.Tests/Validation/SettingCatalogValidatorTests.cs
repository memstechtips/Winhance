using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Validation;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Win32;

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
    public void Catalog_satisfies_invariants(string featureName, SettingGroup group)
    {
        var violations = SettingCatalogValidator.Validate(group);

        foreach (var v in violations)
            _output.WriteLine($"[{featureName}] {v.SettingId}: {v.Message}");

        violations.Should().BeEmpty($"feature '{featureName}' has {violations.Count} catalog violation(s)");
    }

    [Fact]
    public void Validate_InvalidRegistryPath_ReturnsViolation()
    {
        var group = new SettingGroup
        {
            Name = "Test",
            FeatureId = "test-feature",
            Settings = new List<SettingDefinition>
            {
                new SettingDefinition
                {
                    Id = "test-setting",
                    Name = "Test",
                    Description = "Test Description",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"INVALID\Path",
                            RecommendedValue = 1,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord
                        }
                    }
                }
            }
        };

        var violations = SettingCatalogValidator.Validate(group);
        violations.Should().ContainSingle(v => v.Message.Contains("Invalid registry hive"));
    }
}
