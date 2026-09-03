namespace Winhance.TestSupport;

// A clock that only moves when the test says so.
public sealed class ManualTimeProvider : TimeProvider
{
    private long _timestamp;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _timestamp;

    public void Advance(TimeSpan by) => _timestamp += by.Ticks;
}
