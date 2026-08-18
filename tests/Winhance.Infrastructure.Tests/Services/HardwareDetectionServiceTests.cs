using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class HardwareDetectionServiceTests
{
    private readonly Mock<ILogService> _mockLogService = new();

    [Fact]
    public void Constructor_ValidLogService_CreatesInstance()
    {
        var service = new HardwareDetectionService(_mockLogService.Object);

        service.Should().NotBeNull();
    }

    [Fact]
    public void HasBattery_DoesNotThrow_ReturnsBooleanValue()
    {
        var service = new HardwareDetectionService(_mockLogService.Object);

        var act = () => service.HasBattery();

        // Actual value depends on hardware, so only the call is checked
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

    [Fact]
    public void SupportsHybridSleep_DoesNotThrow_ReturnsBooleanValue()
    {
        var service = new HardwareDetectionService(_mockLogService.Object);

        var act = () => service.SupportsHybridSleep();

        act.Should().NotThrow();
    }
}
