using FluentAssertions;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools.WimServices;

public class TransferRateTests
{
    private const long OneMebibyte = 1024 * 1024;

    [Fact]
    public void Update_UnderASecond_TakesNoSample()
    {
        var clock = new ManualTimeProvider();
        var rate = new TransferRate(clock);
        clock.Advance(TimeSpan.FromMilliseconds(500));

        rate.Update(OneMebibyte).Should().BeFalse();
        rate.BytesPerSecond.Should().BeNull();
        rate.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Update_EverySecond_AveragesTheLastTwoSamples()
    {
        var clock = new ManualTimeProvider();
        var rate = new TransferRate(clock);

        clock.Advance(TimeSpan.FromSeconds(1));
        rate.Update(2 * OneMebibyte).Should().BeTrue();
        rate.BytesPerSecond.Should().Be(2 * OneMebibyte);

        clock.Advance(TimeSpan.FromSeconds(1));
        rate.Update(6 * OneMebibyte).Should().BeTrue();
        rate.BytesPerSecond.Should().Be(3 * OneMebibyte);
        rate.ToString().Should().Be($"{3.0:F1} MB/s");
    }
}
