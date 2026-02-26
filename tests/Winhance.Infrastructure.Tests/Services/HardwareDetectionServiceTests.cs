using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class HardwareDetectionServiceTests
{
    private readonly Mock<ILogService> _mockLogService = new();

    #region Constructor

    [Fact]
    public void Constructor_NullLogService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new HardwareDetectionService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logService");
    }

    [Fact]
    public void Constructor_ValidLogService_CreatesInstance()
    {
        // Act
        var service = new HardwareDetectionService(_mockLogService.Object);

        // Assert
        service.Should().NotBeNull();
    }

    #endregion

    #region HasBatteryAsync — WMI integration test (runs against real hardware)

    [Fact]
    public async Task HasBatteryAsync_DoesNotThrow_ReturnsBooleanValue()
    {
        // Arrange
        var service = new HardwareDetectionService(_mockLogService.Object);

        // Act
        var act = () => service.HasBatteryAsync();

        // Assert - should complete without throwing; actual value depends on hardware
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region HasLidAsync — WMI integration test

    [Fact]
    public async Task HasLidAsync_DoesNotThrow_ReturnsBooleanValue()
    {
        // Arrange
        var service = new HardwareDetectionService(_mockLogService.Object);

        // Act
        var act = () => service.HasLidAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region SupportsBrightnessControlAsync

    [Fact]
    public async Task SupportsBrightnessControlAsync_DoesNotThrow_ReturnsBooleanValue()
    {
        // Arrange
        var service = new HardwareDetectionService(_mockLogService.Object);

        // Act
        var act = () => service.SupportsBrightnessControlAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region SupportsHybridSleepAsync

    [Fact]
    public async Task SupportsHybridSleepAsync_DoesNotThrow_ReturnsBooleanValue()
    {
        // Arrange
        var service = new HardwareDetectionService(_mockLogService.Object);

        // Act
        var act = () => service.SupportsHybridSleepAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion
}
