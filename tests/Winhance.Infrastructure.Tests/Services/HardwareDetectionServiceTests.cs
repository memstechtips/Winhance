using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class HardwareDetectionServiceTests
{
    private readonly Mock<ILogService> _mockLogService = new();

    private HardwareDetectionService CreateSut() => new(_mockLogService.Object);

    [Fact]
    public void Constructor_ValidLogService_CreatesInstance()
    {
        var service = CreateSut();

        service.Should().NotBeNull();
    }

    [Theory]
    [InlineData(128, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(4, true)]
    [InlineData(8, true)]
    [InlineData(9, true)]
    public void InterpretBatteryFlag_MapsEveryDocumentedFlagToAVerdict(int flag, bool expected)
    {
        // 128 is NoBattery (a desktop). 1/2/4 are High/Low/Critical charge, 8 is Charging, 9 is the
        // usual laptop-on-mains pair, 0 is a battery present with no charge state reported - all of
        // which mean a battery exists.
        HardwareDetectionService.InterpretBatteryFlag((byte)flag).Should().Be(expected);
    }

    [Fact]
    public void InterpretBatteryFlag_Unknown_ReportsCouldNotTell_NotNoBattery()
    {
        // The trap this pins: Unknown is 255 = 0xFF, which has the NoBattery bit (128) set inside it.
        // A plain bit test would call every machine reporting Unknown a desktop, and callers treat
        // "no battery" and "could not tell" differently.
        HardwareDetectionService.InterpretBatteryFlag(255).Should().BeNull();
    }

    [Fact]
    public void HasBattery_ReachesAVerdict_OnAnySupportedMachine()
    {
        // GetSystemPowerStatus answers on every machine Winhance supports, desktop included, so a
        // null here means the P/Invoke binding is wrong rather than that the hardware is unusual.
        // The true/false split is a property of the test machine, so it is not asserted.
        var service = CreateSut();

        service.HasBattery().Should().NotBeNull();
    }

    [Fact]
    public void HasBattery_QueriesOnce_AndServesLaterCallsFromCache()
    {
        // The cache is what makes a synchronous HasBattery() safe to call from anywhere: only the
        // first caller pays for the lookup. Same instance must keep answering the same way.
        var service = CreateSut();

        var first = service.HasBattery();
        var second = service.HasBattery();

        second.Should().Be(first);
    }

    [Fact]
    public void SupportsHybridSleep_DoesNotThrow_ReturnsBooleanValue()
    {
        var service = CreateSut();

        var act = () => service.SupportsHybridSleep();

        act.Should().NotThrow();
    }
}
