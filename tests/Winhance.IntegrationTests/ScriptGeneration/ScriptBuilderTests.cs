using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Winhance.IntegrationTests.Helpers;
using Xunit;

namespace Winhance.IntegrationTests.ScriptGeneration;

[Trait("Category", "Integration")]
public class ScriptBuilderTests
{
    private static readonly string[] ClipchampPackage = ["Clipchamp.Clipchamp"];

    private readonly Mock<IPowerSettingsQueryService> _powerSettingsQuery = new();
    private readonly Mock<IHardwareDetectionService> _hardwareDetection = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IPowerShellRunner> _powerShellRunner = new();
    private readonly AutounattendScriptBuilder _builder;

    public ScriptBuilderTests()
    {
        // PowerShell validation is a no-op in tests
        _powerShellRunner
            .Setup(p => p.ValidateScriptSyntaxAsync(It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        _hardwareDetection.Setup(h => h.HasBattery()).Returns(false);

        _powerSettingsQuery
            .Setup(p => p.GetActivePowerPlanAsync())
            .ReturnsAsync(new Winhance.Core.Features.Optimize.Models.PowerPlan
            {
                Name = "Balanced",
                Guid = "381b4222-f694-41f0-9685-ff5bb260df2e",
                IsActive = true,
            });
        _powerSettingsQuery
            .Setup(p => p.GetAllPowerSettingsACDCAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>());

        _builder = new AutounattendScriptBuilder(
            _powerSettingsQuery.Object,
            _hardwareDetection.Object,
            _logService.Object,
            _powerShellRunner.Object,
            new Mock<IWindowsVersionService>().Object);
    }

    [Fact]
    public async Task Build_WithWindowsApps_ContainsAppRemoval()
    {
        var config = new WinhanceConfigFile
        {
            WindowsApps = TestSettingFactory.CreateSection(true,
                TestSettingFactory.CreateAppItem("app1", "Clipchamp",
                    appxPackageName: ClipchampPackage)),
        };
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var script = await _builder.BuildWinhancementsScriptAsync(config, allSettings);

        script.Should().Contain("Clipchamp.Clipchamp");
        script.Should().Contain("Get-AppxPackage");
    }

    [Fact]
    public async Task Build_Script_HasBalancedBraces()
    {
        var config = TestSettingFactory.CreateFullConfig();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var script = await _builder.BuildWinhancementsScriptAsync(config, allSettings);

        var openBraces = script.Count(c => c == '{');
        var closeBraces = script.Count(c => c == '}');
        openBraces.Should().Be(closeBraces,
            $"script should have balanced braces but has {openBraces} open and {closeBraces} close");
    }

    [Fact]
    public async Task Build_Script_ContainsRequiredStructure()
    {
        var config = TestSettingFactory.CreateFullConfig();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var script = await _builder.BuildWinhancementsScriptAsync(config, allSettings);

        script.Should().Contain("Write-Log");
        script.Should().Contain("$scriptsDir");
        script.Should().Contain("$UserCustomizations");
        script.Should().Contain("UserCustomizations");
    }

    [Fact]
    public async Task Build_EmptyConfig_ProducesMinimalScript()
    {
        var config = new WinhanceConfigFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var script = await _builder.BuildWinhancementsScriptAsync(config, allSettings);

        script.Should().NotBeNullOrEmpty();
        script.Should().Contain("Write-Log");
        script.Should().Contain("if (-not $UserCustomizations)");
        script.Should().Contain("if ($UserCustomizations)");
    }

    [Fact]
    public async Task Build_WithOptimizeFeatures_ContainsRegistryCommands()
    {
        // The pipeline runs on the catalog Setting dict, so the fixture passes the REAL catalog toggle
        // security-remote-assistance (HKLM DWORD fAllowToGetHelp) directly; the emit reads the CATALOG RegTarget
        // and state values.
        var toggleItem = TestSettingFactory.CreateToggleItem("security-remote-assistance", "Remote Assistance", true);
        var config = new WinhanceConfigFile
        {
            Optimize = TestSettingFactory.CreateFeatureGroup(true, new Dictionary<string, ConfigSection>
            {
                ["Privacy"] = TestSettingFactory.CreateSection(true, toggleItem),
            }),
        };

        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>
        {
            ["Privacy"] = new[] { SettingCatalog.Find("security-remote-assistance")! },
        };

        var script = await _builder.BuildWinhancementsScriptAsync(config, allSettings);

        script.Should().Contain("Set-RegistryValue");
        script.Should().Contain("fAllowToGetHelp");
    }

    [Fact]
    public async Task Build_WithPowerSettings_ContainsPowerCfgCommands()
    {
        // The REAL catalog setting power-display-timeout (PowerOptimizationsCatalog.cs:
        // subgroup 7516b95f-f776-4464-8c53-06167f40cc99, setting 3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e,
        // no hardware gate) rides the Setting dict directly; the emit reads the catalog
        // PowerCfgTarget GUIDs and takes the AC/DC values from the bulk query mock.
        var powerItem = TestSettingFactory.CreateSelectionItem("power-display-timeout", "Turn off the display",
            selectedIndex: 1,
            powerSettings: new Dictionary<string, object>
            {
                ["SubgroupGuid"] = "7516b95f-f776-4464-8c53-06167f40cc99",
                ["SettingGuid"] = "3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e",
                ["AcValue"] = 1800,
                ["DcValue"] = 900,
            });
        var config = new WinhanceConfigFile
        {
            Optimize = TestSettingFactory.CreateFeatureGroup(true, new Dictionary<string, ConfigSection>
            {
                ["Power"] = TestSettingFactory.CreateSection(true, powerItem),
            }),
        };

        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>
        {
            ["Power"] = new[] { SettingCatalog.Find("power-display-timeout")! },
        };

        _powerSettingsQuery
            .Setup(p => p.GetAllPowerSettingsACDCAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>
            {
                ["3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e"] = (1800, 900),
            });

        var script = await _builder.BuildWinhancementsScriptAsync(config, allSettings);

        script.Should().Contain("powercfg");
        script.Should().Contain("7516b95f-f776-4464-8c53-06167f40cc99");
        script.Should().Contain("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");
    }
}
