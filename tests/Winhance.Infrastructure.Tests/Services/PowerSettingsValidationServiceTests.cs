using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class PowerSettingsValidationServiceTests
{
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<IPowerSettingsQueryService> _mockQueryService = new();
    private readonly Mock<IWindowsRegistryService> _mockRegistry = new();
    private readonly PowerSettingsValidationService _sut;

    public PowerSettingsValidationServiceTests()
    {
        _sut = new PowerSettingsValidationService(
            _mockLog.Object,
            _mockQueryService.Object,
            _mockRegistry.Object);
    }

    private static SettingDefinition CreateSetting(
        string id,
        bool validateExistence = true,
        List<PowerCfgSetting>? powerCfgSettings = null)
    {
        return new SettingDefinition
        {
            Id = id,
            Name = $"Setting {id}",
            Description = $"Desc {id}",
            ValidateExistence = validateExistence,
            PowerCfgSettings = powerCfgSettings
        };
    }

    // ── FilterSettingsByExistenceAsync ──

    [Fact]
    public async Task FilterSettingsByExistenceAsync_WhenBulkPowerValuesEmpty_ReturnsAllSettings()
    {
        // No power settings returned from query service
        _mockQueryService
            .Setup(q => q.GetAllPowerSettingsACDCAsync("SCHEME_CURRENT"))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>());

        var settings = new List<SettingDefinition>
        {
            CreateSetting("s1"),
            CreateSetting("s2")
        };

        var result = await _sut.FilterSettingsByExistenceAsync(settings);

        result.Should().HaveCount(2);
        _mockLog.Verify(
            l => l.Log(LogLevel.Warning, It.Is<string>(s => s.Contains("Could not get bulk power settings")), It.IsAny<Exception?>()),
            Times.Once);
    }

    [Fact]
    public async Task FilterSettingsByExistenceAsync_SettingWithoutValidation_IsAlwaysIncluded()
    {
        _mockQueryService
            .Setup(q => q.GetAllPowerSettingsACDCAsync("SCHEME_CURRENT"))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>
            {
                { "some-guid", (1, 1) }
            });

        var settings = new List<SettingDefinition>
        {
            CreateSetting("no-validate", validateExistence: false)
        };

        var result = await _sut.FilterSettingsByExistenceAsync(settings);

        result.Should().HaveCount(1);
        result.First().Id.Should().Be("no-validate");
    }

    [Fact]
    public async Task FilterSettingsByExistenceAsync_SettingWithNoPowerCfg_IsIncluded()
    {
        _mockQueryService
            .Setup(q => q.GetAllPowerSettingsACDCAsync("SCHEME_CURRENT"))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>
            {
                { "some-guid", (1, 1) }
            });

        // ValidateExistence=true but no PowerCfgSettings
        var settings = new List<SettingDefinition>
        {
            CreateSetting("no-power-cfg", validateExistence: true, powerCfgSettings: null)
        };

        var result = await _sut.FilterSettingsByExistenceAsync(settings);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task FilterSettingsByExistenceAsync_SettingExistsInBulk_IsIncluded()
    {
        var settingGuid = "guid-abc";
        _mockQueryService
            .Setup(q => q.GetAllPowerSettingsACDCAsync("SCHEME_CURRENT"))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>
            {
                { settingGuid, (50, 50) }
            });

        var settings = new List<SettingDefinition>
        {
            CreateSetting("exists", powerCfgSettings: new List<PowerCfgSetting>
            {
                new PowerCfgSetting { SettingGuid = settingGuid }
            })
        };

        var result = await _sut.FilterSettingsByExistenceAsync(settings);

        result.Should().HaveCount(1);
        result.First().Id.Should().Be("exists");
    }

    [Fact]
    public async Task FilterSettingsByExistenceAsync_SettingNotInBulk_NoEnablement_IsFilteredOut()
    {
        _mockQueryService
            .Setup(q => q.GetAllPowerSettingsACDCAsync("SCHEME_CURRENT"))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>
            {
                { "other-guid", (1, 1) }
            });

        var settings = new List<SettingDefinition>
        {
            CreateSetting("missing", powerCfgSettings: new List<PowerCfgSetting>
            {
                new PowerCfgSetting { SettingGuid = "non-existent-guid" }
            })
        };

        var result = await _sut.FilterSettingsByExistenceAsync(settings);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FilterSettingsByExistenceAsync_HardwareControlled_IsFilteredOut()
    {
        var settingGuid = "hw-guid";
        _mockQueryService
            .Setup(q => q.GetAllPowerSettingsACDCAsync("SCHEME_CURRENT"))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>
            {
                { settingGuid, (50, 50) }
            });
        _mockQueryService
            .Setup(q => q.IsSettingHardwareControlledAsync(It.Is<PowerCfgSetting>(p => p.SettingGuid == settingGuid)))
            .ReturnsAsync(true);

        var settings = new List<SettingDefinition>
        {
            CreateSetting("hw-controlled", powerCfgSettings: new List<PowerCfgSetting>
            {
                new PowerCfgSetting { SettingGuid = settingGuid, CheckForHardwareControl = true }
            })
        };

        var result = await _sut.FilterSettingsByExistenceAsync(settings);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FilterSettingsByExistenceAsync_EmptySettingsList_ReturnsEmpty()
    {
        _mockQueryService
            .Setup(q => q.GetAllPowerSettingsACDCAsync("SCHEME_CURRENT"))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>
            {
                { "guid", (1, 1) }
            });

        var result = await _sut.FilterSettingsByExistenceAsync(Enumerable.Empty<SettingDefinition>());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FilterSettingsByExistenceAsync_EnablementRegistryApplied_RevalidatesAndIncludes()
    {
        var settingGuid = "hidden-guid";

        // First bulk call: setting not found
        // Second bulk call (after enablement): setting found
        var callCount = 0;
        _mockQueryService
            .Setup(q => q.GetAllPowerSettingsACDCAsync("SCHEME_CURRENT"))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new Dictionary<string, (int? acValue, int? dcValue)>
                    {
                        { "other-guid", (1, 1) }
                    };
                }
                return new Dictionary<string, (int? acValue, int? dcValue)>
                {
                    { "other-guid", (1, 1) },
                    { settingGuid, (0, 0) }
                };
            });

        var enablementSetting = new RegistrySetting
        {
            KeyPath = @"HKLM\SYSTEM\CurrentControlSet\Control\Power",
            ValueName = "HiddenSetting",
            ValueType = Microsoft.Win32.RegistryValueKind.DWord,
            EnabledValue = 1
        };

        _mockRegistry
            .Setup(r => r.ApplySetting(enablementSetting, true, null))
            .Returns(true);

        var settings = new List<SettingDefinition>
        {
            CreateSetting("hidden", powerCfgSettings: new List<PowerCfgSetting>
            {
                new PowerCfgSetting
                {
                    SettingGuid = settingGuid,
                    EnablementRegistrySetting = enablementSetting
                }
            })
        };

        var result = await _sut.FilterSettingsByExistenceAsync(settings);

        result.Should().HaveCount(1);
        result.First().Id.Should().Be("hidden");
    }
}
