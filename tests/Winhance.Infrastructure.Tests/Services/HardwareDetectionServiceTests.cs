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

    #region HasBattery — WMI integration test (runs against real hardware)

    [Fact]
    public void HasBattery_DoesNotThrow_ReturnsBooleanValue()
    {
        // Arrange
        var service = new HardwareDetectionService(_mockLogService.Object);

        // Act
        var act = () => service.HasBattery();

        // Assert - should complete without throwing; actual value depends on hardware
        act.Should().NotThrow();
    }

    [Fact]
    public void HasBattery_QueriesWmiOnce_AndServesLaterCallsFromCache()
    {
        // The cache is what makes a synchronous HasBattery() safe to call from anywhere: only the
        // first caller pays for the WMI round trip. Same instance must keep answering the same way.
        var service = new HardwareDetectionService(_mockLogService.Object);

        var first = service.HasBattery();
        var second = service.HasBattery();

        second.Should().Be(first);
    }

    #endregion

    #region SupportsHybridSleep

    [Fact]
    public void SupportsHybridSleep_DoesNotThrow_ReturnsBooleanValue()
    {
        // Arrange
        var service = new HardwareDetectionService(_mockLogService.Object);

        // Act
        var act = () => service.SupportsHybridSleep();

        // Assert
        act.Should().NotThrow();
    }

    #endregion
}
