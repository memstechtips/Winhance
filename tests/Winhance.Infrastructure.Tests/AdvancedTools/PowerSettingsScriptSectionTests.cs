using System.Text;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.AdvancedTools.ScriptSections;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools;

public class PowerSettingsScriptSectionTests
{
    private readonly Mock<IPowerSettingsQueryService> _powerSettingsQueryService = new();
    private readonly Mock<IHardwareDetectionService> _hardwareDetectionService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly PowerSettingsScriptSection _sut;

    public PowerSettingsScriptSectionTests()
    {
        _sut = new PowerSettingsScriptSection(
            _powerSettingsQueryService.Object,
            _hardwareDetectionService.Object,
            _logService.Object);
    }

    // ---------------------------------------------------------------
    // FindPowerPlanSetting
    // ---------------------------------------------------------------

    [Fact]
    public void FindPowerPlanSetting_NoPowerFeature_ReturnsNull()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        var result = _sut.FindPowerPlanSetting(config, allSettings);

        result.Should().BeNull();
    }

    [Fact]
    public void FindPowerPlanSetting_PowerFeatureWithoutPowerPlanSelection_ReturnsNull()
    {
        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    {
                        FeatureIds.Power, new ConfigSection
                        {
                            Items = new List<ConfigurationItem>
                            {
                                new ConfigurationItem { Id = "other-setting" }
                            }
                        }
                    }
                }
            }
        };
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        var result = _sut.FindPowerPlanSetting(config, allSettings);

        result.Should().BeNull();
    }

    [Fact]
    public void FindPowerPlanSetting_PowerPlanSelectionWithEmptyGuid_ReturnsNull()
    {
        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    {
                        FeatureIds.Power, new ConfigSection
                        {
                            Items = new List<ConfigurationItem>
                            {
                                new ConfigurationItem
                                {
                                    Id = "power-plan-selection",
                                    PowerPlanGuid = null
                                }
                            }
                        }
                    }
                }
            }
        };
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        var result = _sut.FindPowerPlanSetting(config, allSettings);

        result.Should().BeNull();
    }

    [Fact]
    public void FindPowerPlanSetting_ValidPowerPlanSelection_ReturnsConfigItem()
    {
        var expectedItem = new ConfigurationItem
        {
            Id = "power-plan-selection",
            PowerPlanGuid = "test-guid-1234",
            PowerPlanName = "Test Plan"
        };

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    {
                        FeatureIds.Power, new ConfigSection
                        {
                            Items = new List<ConfigurationItem> { expectedItem }
                        }
                    }
                }
            }
        };
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        var result = _sut.FindPowerPlanSetting(config, allSettings);

        result.Should().NotBeNull();
        result!.PowerPlanGuid.Should().Be("test-guid-1234");
    }

    // ---------------------------------------------------------------
    // AppendPowerSettingsSectionAsync - No power plan and no power settings
    // ---------------------------------------------------------------

    [Fact]
    public async Task AppendPowerSettingsSectionAsync_NoPowerPlanNoSettings_ReturnsFalse()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

        _powerSettingsQueryService.Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(new PowerPlan { Guid = "test-guid", Name = "Balanced" });
        _powerSettingsQueryService.Setup(s => s.GetAllPowerSettingsACDCAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>());

        var sb = new StringBuilder();

        var result = await _sut.AppendPowerSettingsSectionAsync(sb, config, allSettings, "    ");

        result.Should().BeFalse();
    }

    // ---------------------------------------------------------------
    // AppendPowerSettingsSectionAsync - With power plan setting
    // ---------------------------------------------------------------

    [Fact]
    public async Task AppendPowerSettingsSectionAsync_WithPowerPlan_EmitsPowerPlanCreation()
    {
        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    {
                        FeatureIds.Power, new ConfigSection
                        {
                            Items = new List<ConfigurationItem>
                            {
                                new ConfigurationItem
                                {
                                    Id = "power-plan-selection",
                                    PowerPlanGuid = "custom-plan-guid",
                                    PowerPlanName = "My Power Plan"
                                }
                            }
                        }
                    }
                }
            }
        };

        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            { FeatureIds.Power, Array.Empty<SettingDefinition>() }
        };

        _powerSettingsQueryService.Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(new PowerPlan { Guid = "active-guid", Name = "Balanced" });
        _powerSettingsQueryService.Setup(s => s.GetAllPowerSettingsACDCAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>());
        _hardwareDetectionService.Setup(s => s.HasBatteryAsync()).ReturnsAsync(false);

        var sb = new StringBuilder();
        var result = await _sut.AppendPowerSettingsSectionAsync(sb, config, allSettings, "    ");

        result.Should().BeTrue();
        var output = sb.ToString();
        output.Should().Contain("POWER PLAN");
        output.Should().Contain("custom-plan-guid");
        output.Should().Contain("My Power Plan");
    }

    // ---------------------------------------------------------------
    // AppendPowerSettingsSectionAsync - With power settings data
    // ---------------------------------------------------------------

    [Fact]
    public async Task AppendPowerSettingsSectionAsync_WithPowerSettings_EmitsSettingsArray()
    {
        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    {
                        FeatureIds.Power, new ConfigSection
                        {
                            Items = new List<ConfigurationItem>
                            {
                                new ConfigurationItem
                                {
                                    Id = "power-plan-selection",
                                    PowerPlanGuid = "plan-guid",
                                    PowerPlanName = "Plan"
                                }
                            }
                        }
                    }
                }
            }
        };

        var settingDef = new SettingDefinition
        {
            Id = "test-power-setting",
            Name = "Test Power",
            Description = "A test power setting",
            PowerCfgSettings = new[]
            {
                new PowerCfgSetting
                {
                    SubgroupGuid = "sub-guid",
                    SettingGuid = "setting-guid"
                }
            }
        };

        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            { FeatureIds.Power, new[] { settingDef } }
        };

        _powerSettingsQueryService.Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(new PowerPlan { Guid = "active-guid", Name = "Balanced" });
        _powerSettingsQueryService.Setup(s => s.GetAllPowerSettingsACDCAsync("active-guid"))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>
            {
                { "setting-guid", (10, 5) }
            });
        _hardwareDetectionService.Setup(s => s.HasBatteryAsync()).ReturnsAsync(true);

        var sb = new StringBuilder();
        var result = await _sut.AppendPowerSettingsSectionAsync(sb, config, allSettings, "    ");

        result.Should().BeTrue();
        var output = sb.ToString();
        output.Should().Contain("sub-guid");
        output.Should().Contain("setting-guid");
        output.Should().Contain("powercfg");
    }

    // ---------------------------------------------------------------
    // AppendPowerSettingsSectionAsync - Skips battery-required settings
    // ---------------------------------------------------------------

    [Fact]
    public async Task AppendPowerSettingsSectionAsync_BatteryRequired_NoBattery_SkipsSetting()
    {
        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    {
                        FeatureIds.Power, new ConfigSection
                        {
                            Items = new List<ConfigurationItem>
                            {
                                new ConfigurationItem
                                {
                                    Id = "power-plan-selection",
                                    PowerPlanGuid = "plan-guid",
                                    PowerPlanName = "Plan"
                                }
                            }
                        }
                    }
                }
            }
        };

        var batterySettingDef = new SettingDefinition
        {
            Id = "battery-setting",
            Name = "Battery",
            Description = "Requires battery",
            RequiresBattery = true,
            PowerCfgSettings = new[]
            {
                new PowerCfgSetting
                {
                    SubgroupGuid = "battery-sub",
                    SettingGuid = "battery-set"
                }
            }
        };

        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            { FeatureIds.Power, new[] { batterySettingDef } }
        };

        _powerSettingsQueryService.Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(new PowerPlan { Guid = "active-guid", Name = "Balanced" });
        _powerSettingsQueryService.Setup(s => s.GetAllPowerSettingsACDCAsync("active-guid"))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>
            {
                { "battery-set", (10, 5) }
            });
        _hardwareDetectionService.Setup(s => s.HasBatteryAsync()).ReturnsAsync(false);

        var sb = new StringBuilder();
        await _sut.AppendPowerSettingsSectionAsync(sb, config, allSettings, "    ");

        // The battery setting should be skipped, so the output will only have
        // the power plan section header but no settings array entries for battery-sub/battery-set
        sb.ToString().Should().NotContain("battery-sub");
    }

    // ---------------------------------------------------------------
    // AppendPowerSettingsSectionAsync - Skips brightness settings
    // ---------------------------------------------------------------

    [Fact]
    public async Task AppendPowerSettingsSectionAsync_RequiresBrightness_AlwaysSkipped()
    {
        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    {
                        FeatureIds.Power, new ConfigSection
                        {
                            Items = new List<ConfigurationItem>
                            {
                                new ConfigurationItem
                                {
                                    Id = "power-plan-selection",
                                    PowerPlanGuid = "plan-guid",
                                    PowerPlanName = "Plan"
                                }
                            }
                        }
                    }
                }
            }
        };

        var brightnessSettingDef = new SettingDefinition
        {
            Id = "brightness-setting",
            Name = "Brightness",
            Description = "Requires brightness support",
            RequiresBrightnessSupport = true,
            PowerCfgSettings = new[]
            {
                new PowerCfgSetting
                {
                    SubgroupGuid = "bright-sub",
                    SettingGuid = "bright-set"
                }
            }
        };

        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            { FeatureIds.Power, new[] { brightnessSettingDef } }
        };

        _powerSettingsQueryService.Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(new PowerPlan { Guid = "active-guid", Name = "Balanced" });
        _powerSettingsQueryService.Setup(s => s.GetAllPowerSettingsACDCAsync("active-guid"))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>
            {
                { "bright-set", (50, 30) }
            });
        _hardwareDetectionService.Setup(s => s.HasBatteryAsync()).ReturnsAsync(true);

        var sb = new StringBuilder();
        await _sut.AppendPowerSettingsSectionAsync(sb, config, allSettings, "    ");

        sb.ToString().Should().NotContain("bright-sub");
    }
}
