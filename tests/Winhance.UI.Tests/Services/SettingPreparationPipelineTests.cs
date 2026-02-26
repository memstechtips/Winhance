using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class SettingPreparationPipelineTests
{
    private readonly Mock<ICompatibleSettingsRegistry> _mockCompatibleSettingsRegistry = new();
    private readonly Mock<ISettingLocalizationService> _mockSettingLocalizationService = new();

    private SettingPreparationPipeline CreateService()
    {
        return new SettingPreparationPipeline(
            _mockCompatibleSettingsRegistry.Object,
            _mockSettingLocalizationService.Object);
    }

    // -------------------------------------------------------
    // PrepareSettings - basic filtering + localization
    // -------------------------------------------------------

    [Fact]
    public void PrepareSettings_FiltersSettingsByModuleIdAndLocalizesEach()
    {
        var settingA = new SettingDefinition
        {
            Id = "setting-a",
            Name = "Setting A",
            Description = "Desc A"
        };
        var settingB = new SettingDefinition
        {
            Id = "setting-b",
            Name = "Setting B",
            Description = "Desc B"
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Privacy"))
            .Returns(new[] { settingA, settingB });

        var localizedA = settingA with { Name = "Localized A" };
        var localizedB = settingB with { Name = "Localized B" };

        _mockSettingLocalizationService
            .Setup(l => l.LocalizeSetting(settingA))
            .Returns(localizedA);
        _mockSettingLocalizationService
            .Setup(l => l.LocalizeSetting(settingB))
            .Returns(localizedB);

        var service = CreateService();
        var result = service.PrepareSettings("Privacy");

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Localized A");
        result[1].Name.Should().Be("Localized B");
    }

    [Fact]
    public void PrepareSettings_CallsGetFilteredSettingsWithCorrectModuleId()
    {
        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Gaming"))
            .Returns(Enumerable.Empty<SettingDefinition>());

        var service = CreateService();
        service.PrepareSettings("Gaming");

        _mockCompatibleSettingsRegistry.Verify(
            r => r.GetFilteredSettings("Gaming"),
            Times.Once);
    }

    [Fact]
    public void PrepareSettings_CallsLocalizeSettingForEachSetting()
    {
        var settingA = new SettingDefinition
        {
            Id = "a",
            Name = "A",
            Description = "Desc"
        };
        var settingB = new SettingDefinition
        {
            Id = "b",
            Name = "B",
            Description = "Desc"
        };
        var settingC = new SettingDefinition
        {
            Id = "c",
            Name = "C",
            Description = "Desc"
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Power"))
            .Returns(new[] { settingA, settingB, settingC });

        _mockSettingLocalizationService
            .Setup(l => l.LocalizeSetting(It.IsAny<SettingDefinition>()))
            .Returns((SettingDefinition s) => s);

        var service = CreateService();
        service.PrepareSettings("Power");

        _mockSettingLocalizationService.Verify(
            l => l.LocalizeSetting(It.IsAny<SettingDefinition>()),
            Times.Exactly(3));
    }

    // -------------------------------------------------------
    // PrepareSettings - empty module
    // -------------------------------------------------------

    [Fact]
    public void PrepareSettings_WhenModuleHasNoSettings_ReturnsEmptyList()
    {
        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("EmptyModule"))
            .Returns(Enumerable.Empty<SettingDefinition>());

        var service = CreateService();
        var result = service.PrepareSettings("EmptyModule");

        result.Should().BeEmpty();
    }

    [Fact]
    public void PrepareSettings_WhenModuleHasNoSettings_DoesNotCallLocalize()
    {
        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("EmptyModule"))
            .Returns(Enumerable.Empty<SettingDefinition>());

        var service = CreateService();
        service.PrepareSettings("EmptyModule");

        _mockSettingLocalizationService.Verify(
            l => l.LocalizeSetting(It.IsAny<SettingDefinition>()),
            Times.Never);
    }

    // -------------------------------------------------------
    // PrepareSettings - returns IReadOnlyList
    // -------------------------------------------------------

    [Fact]
    public void PrepareSettings_ReturnsReadOnlyList()
    {
        var setting = new SettingDefinition
        {
            Id = "test",
            Name = "Test",
            Description = "Desc"
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Module"))
            .Returns(new[] { setting });

        _mockSettingLocalizationService
            .Setup(l => l.LocalizeSetting(setting))
            .Returns(setting);

        var service = CreateService();
        var result = service.PrepareSettings("Module");

        result.Should().BeAssignableTo<IReadOnlyList<SettingDefinition>>();
    }

    // -------------------------------------------------------
    // PrepareSettings - different module IDs are independent
    // -------------------------------------------------------

    [Fact]
    public void PrepareSettings_DifferentModuleIds_ReturnDifferentResults()
    {
        var privacySetting = new SettingDefinition
        {
            Id = "privacy-1",
            Name = "Privacy Setting",
            Description = "Desc"
        };
        var gamingSetting = new SettingDefinition
        {
            Id = "gaming-1",
            Name = "Gaming Setting",
            Description = "Desc"
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Privacy"))
            .Returns(new[] { privacySetting });
        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Gaming"))
            .Returns(new[] { gamingSetting });

        _mockSettingLocalizationService
            .Setup(l => l.LocalizeSetting(It.IsAny<SettingDefinition>()))
            .Returns((SettingDefinition s) => s);

        var service = CreateService();

        var privacyResult = service.PrepareSettings("Privacy");
        var gamingResult = service.PrepareSettings("Gaming");

        privacyResult.Should().ContainSingle().Which.Id.Should().Be("privacy-1");
        gamingResult.Should().ContainSingle().Which.Id.Should().Be("gaming-1");
    }

    // -------------------------------------------------------
    // PrepareSettings - preserves order from registry
    // -------------------------------------------------------

    [Fact]
    public void PrepareSettings_PreservesOrderFromRegistry()
    {
        var settings = Enumerable.Range(1, 5).Select(i => new SettingDefinition
        {
            Id = $"setting-{i}",
            Name = $"Setting {i}",
            Description = "Desc"
        }).ToArray();

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Module"))
            .Returns(settings);

        _mockSettingLocalizationService
            .Setup(l => l.LocalizeSetting(It.IsAny<SettingDefinition>()))
            .Returns((SettingDefinition s) => s);

        var service = CreateService();
        var result = service.PrepareSettings("Module");

        result.Select(s => s.Id).Should().ContainInOrder(
            "setting-1", "setting-2", "setting-3", "setting-4", "setting-5");
    }

    // -------------------------------------------------------
    // PrepareSettings - localization transforms are applied
    // -------------------------------------------------------

    [Fact]
    public void PrepareSettings_LocalizationTransformsDescriptionAndName()
    {
        var original = new SettingDefinition
        {
            Id = "telemetry",
            Name = "Telemetry",
            Description = "Disable telemetry"
        };

        var localized = original with
        {
            Name = "Telemetrie",
            Description = "Telemetrie deaktivieren"
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Privacy"))
            .Returns(new[] { original });

        _mockSettingLocalizationService
            .Setup(l => l.LocalizeSetting(original))
            .Returns(localized);

        var service = CreateService();
        var result = service.PrepareSettings("Privacy");

        result.Should().ContainSingle();
        result[0].Name.Should().Be("Telemetrie");
        result[0].Description.Should().Be("Telemetrie deaktivieren");
    }
}
