using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class SupportAskScheduleTests
{
    private readonly Dictionary<string, string> _stored = new();
    private readonly ManualTimeProvider _clock = new();
    private readonly SupportAskSchedule _schedule;

    public SupportAskScheduleTests()
    {
        var prefs = new Mock<IUserPreferencesService>();
        prefs.Setup(p => p.GetPreferenceAsync(It.IsAny<string>(), It.IsAny<string>()))
             .ReturnsAsync((string key, string fallback) => _stored.TryGetValue(key, out var value) ? value : fallback);
        prefs.Setup(p => p.SetPreferenceAsync(It.IsAny<string>(), It.IsAny<string>()))
             .ReturnsAsync((string key, string value) =>
             {
                 _stored[key] = value;
                 return OperationResult.Succeeded();
             });
        _schedule = new SupportAskSchedule(prefs.Object, _clock);
    }

    [Fact]
    public async Task NeverAsked_Asks()
    {
        (await _schedule.ShouldAskOnExitAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task AskedAnHourAgo_StaysQuiet()
    {
        await _schedule.MarkAskedAsync();
        _clock.Advance(TimeSpan.FromHours(1));

        (await _schedule.ShouldAskOnExitAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task AskedADayAgo_AsksAgain()
    {
        await _schedule.MarkAskedAsync();
        _clock.Advance(TimeSpan.FromHours(24));

        (await _schedule.ShouldAskOnExitAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task ClockSetBack_AsksInsteadOfWaitingForItToCatchUp()
    {
        _clock.Advance(TimeSpan.FromDays(10));
        await _schedule.MarkAskedAsync();
        _clock.Advance(TimeSpan.FromDays(-5));

        (await _schedule.ShouldAskOnExitAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task UnreadableStoredTime_IsTreatedAsNeverAsked()
    {
        _stored[UserPreferenceKeys.SupportAskedAt] = "yesterday-ish";

        (await _schedule.ShouldAskOnExitAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task StoredTimes_AreRoundTripText()
    {
        await _schedule.MarkAskedAsync();

        DateTimeOffset.Parse(_stored[UserPreferenceKeys.SupportAskedAt], null, System.Globalization.DateTimeStyles.RoundtripKind)
            .Should().Be(_clock.GetUtcNow());
    }
}
