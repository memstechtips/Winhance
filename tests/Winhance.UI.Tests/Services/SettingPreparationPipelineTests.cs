using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

// Slice B2: the pipeline no longer localizes (SettingLocalizationService.LocalizeSetting was retired; display
// localization moved to SettingViewModelFactory on the catalog path). It is now a thin compatibility-filter
// pass-through over ICompatibleSettingsRegistry.GetFilteredSettings, so these tests assert exactly that.
public class SettingPreparationPipelineTests
{
    private readonly Mock<ICompatibleSettingsRegistry> _mockCompatibleSettingsRegistry = new();

    private SettingPreparationPipeline CreateService()
    {
        return new SettingPreparationPipeline(_mockCompatibleSettingsRegistry.Object);
    }

    [Fact]
    public void PrepareSettings_ReturnsTheFilteredSettingsForTheModule()
    {
        var settingA = new SettingDefinition { Id = "setting-a", Name = "Setting A", Description = "Desc A" };
        var settingB = new SettingDefinition { Id = "setting-b", Name = "Setting B", Description = "Desc B" };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Privacy"))
            .Returns(new[] { settingA, settingB });

        var service = CreateService();
        var result = service.PrepareSettings("Privacy");

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("setting-a");
        result[1].Id.Should().Be("setting-b");
    }

    [Fact]
    public void PrepareSettings_CallsGetFilteredSettingsWithCorrectModuleId()
    {
        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Gaming"))
            .Returns(Enumerable.Empty<SettingDefinition>());

        var service = CreateService();
        service.PrepareSettings("Gaming");

        _mockCompatibleSettingsRegistry.Verify(r => r.GetFilteredSettings("Gaming"), Times.Once);
    }

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
    public void PrepareSettings_ReturnsReadOnlyList()
    {
        var setting = new SettingDefinition { Id = "test", Name = "Test", Description = "Desc" };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Module"))
            .Returns(new[] { setting });

        var service = CreateService();
        var result = service.PrepareSettings("Module");

        result.Should().BeAssignableTo<IReadOnlyList<SettingDefinition>>();
    }

    [Fact]
    public void PrepareSettings_DifferentModuleIds_ReturnDifferentResults()
    {
        var privacySetting = new SettingDefinition { Id = "privacy-1", Name = "Privacy Setting", Description = "Desc" };
        var gamingSetting = new SettingDefinition { Id = "gaming-1", Name = "Gaming Setting", Description = "Desc" };

        _mockCompatibleSettingsRegistry.Setup(r => r.GetFilteredSettings("Privacy")).Returns(new[] { privacySetting });
        _mockCompatibleSettingsRegistry.Setup(r => r.GetFilteredSettings("Gaming")).Returns(new[] { gamingSetting });

        var service = CreateService();

        var privacyResult = service.PrepareSettings("Privacy");
        var gamingResult = service.PrepareSettings("Gaming");

        privacyResult.Should().ContainSingle().Which.Id.Should().Be("privacy-1");
        gamingResult.Should().ContainSingle().Which.Id.Should().Be("gaming-1");
    }

    [Fact]
    public void PrepareSettings_PreservesOrderFromRegistry()
    {
        var settings = Enumerable.Range(1, 5).Select(i => new SettingDefinition
        {
            Id = $"setting-{i}",
            Name = $"Setting {i}",
            Description = "Desc"
        }).ToArray();

        _mockCompatibleSettingsRegistry.Setup(r => r.GetFilteredSettings("Module")).Returns(settings);

        var service = CreateService();
        var result = service.PrepareSettings("Module");

        result.Select(s => s.Id).Should().ContainInOrder(
            "setting-1", "setting-2", "setting-3", "setting-4", "setting-5");
    }
}
