using System.Text;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
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

        var result = _sut.FindPowerPlanSetting(config);

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
        var result = _sut.FindPowerPlanSetting(config);

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
        var result = _sut.FindPowerPlanSetting(config);

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
        var result = _sut.FindPowerPlanSetting(config);

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
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

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

        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>
        {
            { FeatureIds.Power, Array.Empty<Setting>() }
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

        // The dict carries the REAL catalog setting power-display-timeout
        // (PowerOptimizationsCatalog.cs: subgroup 7516b95f-f776-4464-8c53-06167f40cc99, setting
        // 3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e, no hardware gate). The emitted GUIDs and description come
        // from the catalog Setting, the AC/DC values from the query mock.
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>
        {
            { FeatureIds.Power, new[] { SettingCatalog.Find("power-display-timeout")! } }
        };

        _powerSettingsQueryService.Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(new PowerPlan { Guid = "active-guid", Name = "Balanced" });
        _powerSettingsQueryService.Setup(s => s.GetAllPowerSettingsACDCAsync("active-guid"))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>
            {
                { "3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e", (10, 5) }
            });
        _hardwareDetectionService.Setup(s => s.HasBatteryAsync()).ReturnsAsync(true);

        var sb = new StringBuilder();
        var result = await _sut.AppendPowerSettingsSectionAsync(sb, config, allSettings, "    ");

        result.Should().BeTrue();
        var output = sb.ToString();
        output.Should().Contain("7516b95f-f776-4464-8c53-06167f40cc99");
        output.Should().Contain("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");
        output.Should().Contain("AC=10; DC=5");
        output.Should().Contain("Specifies the period of inactivity before Windows turns off the display");
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

        // Battery gating reads the catalog Setting's Availability.Hardware, so this fact rides the REAL
        // battery-gated catalog setting critical-battery-notification (PowerOptimizationsCatalog.cs:
        // Hardware = [ Battery ], subgroup e73a048d-bf27-4f12-9731-8b2076e8891f, setting
        // 5dbb7c9f-38e9-40d2-9749-4f8a0e9f640f). The ungated power-display-timeout rides along as a control
        // so a dead pipeline cannot pass this test vacuously.
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>
        {
            {
                FeatureIds.Power, new[]
                {
                    SettingCatalog.Find("critical-battery-notification")!,
                    SettingCatalog.Find("power-display-timeout")!
                }
            }
        };

        _powerSettingsQueryService.Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(new PowerPlan { Guid = "active-guid", Name = "Balanced" });
        _powerSettingsQueryService.Setup(s => s.GetAllPowerSettingsACDCAsync("active-guid"))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>
            {
                { "5dbb7c9f-38e9-40d2-9749-4f8a0e9f640f", (10, 5) },
                { "3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e", (20, 15) }
            });
        _hardwareDetectionService.Setup(s => s.HasBatteryAsync()).ReturnsAsync(false);

        var sb = new StringBuilder();
        await _sut.AppendPowerSettingsSectionAsync(sb, config, allSettings, "    ");

        // The control setting is emitted; the battery-gated one is skipped. Its GUIDs appear nowhere else
        // in the section output (they are not in the hardcoded hidden-settings enablement array).
        var output = sb.ToString();
        output.Should().Contain("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");
        output.Should().NotContain("e73a048d-bf27-4f12-9731-8b2076e8891f");
        output.Should().NotContain("5dbb7c9f-38e9-40d2-9749-4f8a0e9f640f");
    }

}
