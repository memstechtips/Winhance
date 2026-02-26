using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Winhance.IntegrationTests.Helpers;
using Xunit;

namespace Winhance.IntegrationTests.ScriptGeneration;

[Trait("Category", "Integration")]
public class ScriptBuilderTests
{
    private readonly Mock<IPowerSettingsQueryService> _powerSettingsQuery = new();
    private readonly Mock<IHardwareDetectionService> _hardwareDetection = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IComboBoxResolver> _comboBoxResolver = new();
    private readonly Mock<IPowerShellRunner> _powerShellRunner = new();
    private readonly AutounattendScriptBuilder _builder;

    public ScriptBuilderTests()
    {
        // PowerShell validation is a no-op in tests
        _powerShellRunner
            .Setup(p => p.ValidateScriptSyntaxAsync(It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        _hardwareDetection.Setup(h => h.HasBatteryAsync()).ReturnsAsync(false);
        _hardwareDetection.Setup(h => h.HasLidAsync()).ReturnsAsync(false);

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
            _comboBoxResolver.Object,
            _powerShellRunner.Object);
    }

    [Fact]
    public async Task Build_WithWindowsApps_ContainsAppRemoval()
    {
        // Arrange
        var config = new UnifiedConfigurationFile
        {
            WindowsApps = TestSettingFactory.CreateSection(true,
                TestSettingFactory.CreateAppItem("app1", "Clipchamp",
                    appxPackageName: "Clipchamp.Clipchamp")),
        };
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        // Act
        var script = await _builder.BuildWinhancementsScriptAsync(config, allSettings);

        // Assert
        script.Should().Contain("Clipchamp.Clipchamp");
        script.Should().Contain("Get-AppxPackage");
    }

    [Fact]
    public async Task Build_Script_HasBalancedBraces()
    {
        // Arrange
        var config = TestSettingFactory.CreateFullConfig();
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        // Act
        var script = await _builder.BuildWinhancementsScriptAsync(config, allSettings);

        // Assert
        var openBraces = script.Count(c => c == '{');
        var closeBraces = script.Count(c => c == '}');
        openBraces.Should().Be(closeBraces,
            $"script should have balanced braces but has {openBraces} open and {closeBraces} close");
    }

    [Fact]
    public async Task Build_Script_ContainsRequiredStructure()
    {
        // Arrange
        var config = TestSettingFactory.CreateFullConfig();
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        // Act
        var script = await _builder.BuildWinhancementsScriptAsync(config, allSettings);

        // Assert
        script.Should().Contain("Write-Log");
        script.Should().Contain("$scriptsDir");
        script.Should().Contain("$UserCustomizations");
        script.Should().Contain("UserCustomizations");
    }

    [Fact]
    public async Task Build_EmptyConfig_ProducesMinimalScript()
    {
        // Arrange
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        // Act
        var script = await _builder.BuildWinhancementsScriptAsync(config, allSettings);

        // Assert
        script.Should().NotBeNullOrEmpty();
        // Even empty config should have the header/setup structure
        script.Should().Contain("Write-Log");
        script.Should().Contain("if (-not $UserCustomizations)");
        script.Should().Contain("if ($UserCustomizations)");
    }

    [Fact]
    public async Task Build_WithOptimizeFeatures_ContainsRegistryCommands()
    {
        // Arrange — config with an Optimize toggle that has registry settings
        var toggleItem = TestSettingFactory.CreateToggleItem("privacy1", "Disable Telemetry", true);
        var config = new UnifiedConfigurationFile
        {
            Optimize = TestSettingFactory.CreateFeatureGroup(true, new Dictionary<string, ConfigSection>
            {
                ["Privacy"] = TestSettingFactory.CreateSection(true, toggleItem),
            }),
        };

        // Provide matching SettingDefinitions with real registry settings
        var settingDef = new SettingDefinition
        {
            Id = "privacy1",
            Name = "Disable Telemetry",
            Description = "Disables telemetry",
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                    ValueName = "AllowTelemetry",
                    RecommendedValue = 0,
                    DefaultValue = 1,
                    EnabledValue = 0,
                    DisabledValue = 1,
                    ValueType = Microsoft.Win32.RegistryValueKind.DWord,
                    IsPrimary = true,
                },
            },
        };
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            ["Privacy"] = new[] { settingDef },
        };

        // Act
        var script = await _builder.BuildWinhancementsScriptAsync(config, allSettings);

        // Assert
        script.Should().Contain("Set-RegistryValue");
        script.Should().Contain("AllowTelemetry");
    }

    [Fact]
    public async Task Build_WithPowerSettings_ContainsPowerCfgCommands()
    {
        // Arrange
        var powerItem = TestSettingFactory.CreateSelectionItem("power1", "Sleep Timeout",
            selectedIndex: 1,
            powerSettings: new Dictionary<string, object>
            {
                ["SubgroupGuid"] = "238c9fa8-0aad-41ed-83f4-97be242c8f20",
                ["SettingGuid"] = "29f6c1db-86da-48c5-9fdb-f2b67b1f44da",
                ["AcValue"] = 1800,
                ["DcValue"] = 900,
            });
        var config = new UnifiedConfigurationFile
        {
            Optimize = TestSettingFactory.CreateFeatureGroup(true, new Dictionary<string, ConfigSection>
            {
                ["Power"] = TestSettingFactory.CreateSection(true, powerItem),
            }),
        };

        var powerSettingDef = new SettingDefinition
        {
            Id = "power1",
            Name = "Sleep Timeout",
            Description = "Sleep timeout setting",
            InputType = InputType.Selection,
            PowerCfgSettings = new[]
            {
                new PowerCfgSetting
                {
                    SubgroupGuid = "238c9fa8-0aad-41ed-83f4-97be242c8f20",
                    SettingGuid = "29f6c1db-86da-48c5-9fdb-f2b67b1f44da",
                    RecommendedValueAC = 1800,
                    RecommendedValueDC = 900,
                },
            },
        };
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            ["Power"] = new[] { powerSettingDef },
        };

        // Set up mock to return AC/DC values for the setting GUID
        _powerSettingsQuery
            .Setup(p => p.GetAllPowerSettingsACDCAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>
            {
                ["29f6c1db-86da-48c5-9fdb-f2b67b1f44da"] = (1800, 900),
            });

        // Act
        var script = await _builder.BuildWinhancementsScriptAsync(config, allSettings);

        // Assert
        script.Should().Contain("powercfg");
    }
}
