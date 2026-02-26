using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class HardwareCompatibilityFilterTests
{
    private readonly Mock<IHardwareDetectionService> _mockHardwareDetection = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly HardwareCompatibilityFilter _filter;

    public HardwareCompatibilityFilterTests()
    {
        // Default: desktop machine (no battery, no lid, no brightness, no hybrid sleep)
        _mockHardwareDetection.Setup(h => h.HasBatteryAsync()).ReturnsAsync(false);
        _mockHardwareDetection.Setup(h => h.HasLidAsync()).ReturnsAsync(false);
        _mockHardwareDetection.Setup(h => h.SupportsBrightnessControlAsync()).ReturnsAsync(false);
        _mockHardwareDetection.Setup(h => h.SupportsHybridSleepAsync()).ReturnsAsync(false);

        _filter = new HardwareCompatibilityFilter(
            _mockHardwareDetection.Object,
            _mockLogService.Object);
    }

    #region Constructor

    [Fact]
    public void Constructor_NullHardwareDetectionService_ThrowsArgumentNullException()
    {
        var act = () => new HardwareCompatibilityFilter(null!, _mockLogService.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("hardwareDetectionService");
    }

    [Fact]
    public void Constructor_NullLogService_ThrowsArgumentNullException()
    {
        var act = () => new HardwareCompatibilityFilter(_mockHardwareDetection.Object, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logService");
    }

    #endregion

    #region FilterSettingsByHardwareAsync

    [Fact]
    public async Task FilterSettingsByHardwareAsync_NoRestrictions_ReturnsAllSettings()
    {
        // Arrange
        var settings = new List<SettingDefinition>
        {
            CreateSetting("setting1"),
            CreateSetting("setting2"),
            CreateSetting("setting3")
        };

        // Act
        var result = await _filter.FilterSettingsByHardwareAsync(settings);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task FilterSettingsByHardwareAsync_RequiresBatteryOnDesktop_FilteredOut()
    {
        // Arrange - no battery on this machine (default mock)
        var settings = new List<SettingDefinition>
        {
            CreateSetting("batteryOnly", requiresBattery: true),
            CreateSetting("normal")
        };

        // Act
        var result = await _filter.FilterSettingsByHardwareAsync(settings);

        // Assert
        result.Should().ContainSingle()
            .Which.Id.Should().Be("normal");
    }

    [Fact]
    public async Task FilterSettingsByHardwareAsync_RequiresLidOnDesktop_FilteredOut()
    {
        // Arrange - no lid on this machine (default mock)
        var settings = new List<SettingDefinition>
        {
            CreateSetting("lidOnly", requiresLid: true),
            CreateSetting("normal")
        };

        // Act
        var result = await _filter.FilterSettingsByHardwareAsync(settings);

        // Assert
        result.Should().ContainSingle()
            .Which.Id.Should().Be("normal");
    }

    [Fact]
    public async Task FilterSettingsByHardwareAsync_RequiresDesktopOnLaptop_FilteredOut()
    {
        // Arrange - simulate laptop (has battery + lid)
        _mockHardwareDetection.Setup(h => h.HasBatteryAsync()).ReturnsAsync(true);
        _mockHardwareDetection.Setup(h => h.HasLidAsync()).ReturnsAsync(true);

        // Need a new filter instance since detection results are cached per instance
        var filter = new HardwareCompatibilityFilter(
            _mockHardwareDetection.Object,
            _mockLogService.Object);

        var settings = new List<SettingDefinition>
        {
            CreateSetting("desktopOnly", requiresDesktop: true),
            CreateSetting("normal")
        };

        // Act
        var result = await filter.FilterSettingsByHardwareAsync(settings);

        // Assert
        result.Should().ContainSingle()
            .Which.Id.Should().Be("normal");
    }

    [Fact]
    public async Task FilterSettingsByHardwareAsync_RequiresBrightnessWithoutSupport_FilteredOut()
    {
        // Arrange - no brightness support (default mock)
        var settings = new List<SettingDefinition>
        {
            CreateSetting("brightness", requiresBrightness: true),
            CreateSetting("normal")
        };

        // Act
        var result = await _filter.FilterSettingsByHardwareAsync(settings);

        // Assert
        result.Should().ContainSingle()
            .Which.Id.Should().Be("normal");
    }

    [Fact]
    public async Task FilterSettingsByHardwareAsync_RequiresHybridSleepWithoutSupport_FilteredOut()
    {
        // Arrange - no hybrid sleep (default mock)
        var settings = new List<SettingDefinition>
        {
            CreateSetting("hybridSleep", requiresHybridSleep: true),
            CreateSetting("normal")
        };

        // Act
        var result = await _filter.FilterSettingsByHardwareAsync(settings);

        // Assert
        result.Should().ContainSingle()
            .Which.Id.Should().Be("normal");
    }

    [Fact]
    public async Task FilterSettingsByHardwareAsync_CachesDetectionResults_OnlyQueriesOnce()
    {
        // Arrange
        var settings = new List<SettingDefinition> { CreateSetting("s1") };

        // Act - call twice
        await _filter.FilterSettingsByHardwareAsync(settings);
        await _filter.FilterSettingsByHardwareAsync(settings);

        // Assert - detection methods called only once due to caching
        _mockHardwareDetection.Verify(h => h.HasBatteryAsync(), Times.Once);
        _mockHardwareDetection.Verify(h => h.HasLidAsync(), Times.Once);
    }

    #endregion

    #region Helpers

    private static SettingDefinition CreateSetting(
        string id,
        bool requiresBattery = false,
        bool requiresLid = false,
        bool requiresDesktop = false,
        bool requiresBrightness = false,
        bool requiresHybridSleep = false)
    {
        return new SettingDefinition
        {
            Id = id,
            Name = id,
            Description = $"Test setting {id}",
            RequiresBattery = requiresBattery,
            RequiresLid = requiresLid,
            RequiresDesktop = requiresDesktop,
            RequiresBrightnessSupport = requiresBrightness,
            RequiresHybridSleepCapable = requiresHybridSleep
        };
    }

    #endregion
}
