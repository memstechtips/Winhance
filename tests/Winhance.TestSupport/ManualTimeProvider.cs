namespace Winhance.TestSupport;

// A clock that only moves when the test says so.
public sealed class ManualTimeProvider : TimeProvider
{
    private long _timestamp;
    private DateTimeOffset _utcNow = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _timestamp;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan by)
    {
        _timestamp += by.Ticks;
        _utcNow += by;
    }
}
