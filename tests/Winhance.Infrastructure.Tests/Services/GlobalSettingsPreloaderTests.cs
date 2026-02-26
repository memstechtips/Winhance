using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class GlobalSettingsPreloaderTests
{
    private readonly Mock<ICompatibleSettingsRegistry> _compatibleRegistryMock;
    private readonly Mock<IGlobalSettingsRegistry> _globalRegistryMock;
    private readonly Mock<IDomainServiceRouter> _routerMock;
    private readonly Mock<ILogService> _logMock;
    private readonly GlobalSettingsPreloader _sut;

    public GlobalSettingsPreloaderTests()
    {
        _compatibleRegistryMock = new Mock<ICompatibleSettingsRegistry>();
        _globalRegistryMock = new Mock<IGlobalSettingsRegistry>();
        _routerMock = new Mock<IDomainServiceRouter>();
        _logMock = new Mock<ILogService>();

        _sut = new GlobalSettingsPreloader(
            _compatibleRegistryMock.Object,
            _globalRegistryMock.Object,
            _routerMock.Object,
            _logMock.Object);
    }

    private static SettingDefinition CreateSetting(string id) => new()
    {
        Id = id,
        Name = $"Setting {id}",
        Description = $"Description for {id}",
    };

    [Fact]
    public void IsPreloaded_InitiallyFalse()
    {
        _sut.IsPreloaded.Should().BeFalse();
    }

    [Fact]
    public async Task PreloadAllSettingsAsync_SetsIsPreloadedTrue()
    {
        _compatibleRegistryMock
            .Setup(r => r.GetAllBypassedSettings())
            .Returns(new Dictionary<string, IEnumerable<SettingDefinition>>());

        await _sut.PreloadAllSettingsAsync();

        _sut.IsPreloaded.Should().BeTrue();
    }

    [Fact]
    public async Task PreloadAllSettingsAsync_RegistersAllSettingsInGlobalRegistry()
    {
        var settings1 = new List<SettingDefinition>
        {
            CreateSetting("s1"),
            CreateSetting("s2"),
        };
        var settings2 = new List<SettingDefinition>
        {
            CreateSetting("s3"),
        };

        var allBypassed = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            ["FeatureA"] = settings1,
            ["FeatureB"] = settings2,
        };

        _compatibleRegistryMock
            .Setup(r => r.GetAllBypassedSettings())
            .Returns(allBypassed);

        await _sut.PreloadAllSettingsAsync();

        _globalRegistryMock.Verify(
            r => r.RegisterSettings("FeatureA", It.Is<List<SettingDefinition>>(
                list => list.Count == 2 && list[0].Id == "s1" && list[1].Id == "s2")),
            Times.Once);

        _globalRegistryMock.Verify(
            r => r.RegisterSettings("FeatureB", It.Is<List<SettingDefinition>>(
                list => list.Count == 1 && list[0].Id == "s3")),
            Times.Once);
    }

    [Fact]
    public async Task PreloadAllSettingsAsync_AddsSettingMappingsToRouter()
    {
        var settings = new List<SettingDefinition>
        {
            CreateSetting("dark-mode"),
            CreateSetting("accent-color"),
        };

        var allBypassed = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            ["WindowsTheme"] = settings,
        };

        _compatibleRegistryMock
            .Setup(r => r.GetAllBypassedSettings())
            .Returns(allBypassed);

        await _sut.PreloadAllSettingsAsync();

        _routerMock.Verify(
            r => r.AddSettingMappings("WindowsTheme", It.Is<IEnumerable<string>>(
                ids => ids.Contains("dark-mode") && ids.Contains("accent-color"))),
            Times.Once);
    }

    [Fact]
    public async Task PreloadAllSettingsAsync_CalledTwice_SkipsSecondPreload()
    {
        _compatibleRegistryMock
            .Setup(r => r.GetAllBypassedSettings())
            .Returns(new Dictionary<string, IEnumerable<SettingDefinition>>());

        await _sut.PreloadAllSettingsAsync();
        await _sut.PreloadAllSettingsAsync();

        _compatibleRegistryMock.Verify(
            r => r.GetAllBypassedSettings(),
            Times.Once);
    }

    [Fact]
    public async Task PreloadAllSettingsAsync_WhenFeatureThrows_ContinuesWithOtherFeatures()
    {
        var goodSettings = new List<SettingDefinition> { CreateSetting("good-setting") };

        // Create an enumerable that throws when iterated
        var badEnumerable = new Mock<IEnumerable<SettingDefinition>>();
        badEnumerable.Setup(e => e.GetEnumerator()).Throws(new InvalidOperationException("Bad feature"));

        var allBypassed = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            ["BadFeature"] = badEnumerable.Object,
            ["GoodFeature"] = goodSettings,
        };

        _compatibleRegistryMock
            .Setup(r => r.GetAllBypassedSettings())
            .Returns(allBypassed);

        await _sut.PreloadAllSettingsAsync();

        // Should still complete and set IsPreloaded despite the error
        _sut.IsPreloaded.Should().BeTrue();

        _logMock.Verify(
            l => l.Log(LogLevel.Warning, It.Is<string>(msg => msg.Contains("BadFeature")), null),
            Times.Once);
    }

    [Fact]
    public async Task PreloadAllSettingsAsync_WithNoBypassedSettings_StillSetsPreloaded()
    {
        _compatibleRegistryMock
            .Setup(r => r.GetAllBypassedSettings())
            .Returns(new Dictionary<string, IEnumerable<SettingDefinition>>());

        await _sut.PreloadAllSettingsAsync();

        _sut.IsPreloaded.Should().BeTrue();
        _globalRegistryMock.Verify(
            r => r.RegisterSettings(It.IsAny<string>(), It.IsAny<IEnumerable<ISettingItem>>()),
            Times.Never);
        _routerMock.Verify(
            r => r.AddSettingMappings(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()),
            Times.Never);
    }
}
