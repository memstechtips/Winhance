namespace Winhance.TestSupport;

// A clock that only moves when the test says so. Its timers fire inside Advance, at most once per call, as a
// timer that falls behind runs its missed ticks once.
public sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];
    private long _timestamp;
    private DateTimeOffset _utcNow = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _timestamp;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        lock (_timers)
            _timers.Add(timer);
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        _timestamp += by.Ticks;
        _utcNow += by;
        ManualTimer[] timers;
        lock (_timers)
            timers = [.. _timers];
        foreach (var timer in timers)
            timer.FireIfDue(_utcNow);
    }

    private sealed class ManualTimer(ManualTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        private DateTimeOffset? _due;
        private TimeSpan _period;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            _due = dueTime == Timeout.InfiniteTimeSpan ? null : clock._utcNow + dueTime;
            _period = period;
            return true;
        }

        public void FireIfDue(DateTimeOffset now)
        {
            if (_due is not { } due || due > now)
                return;
            _due = _period == Timeout.InfiniteTimeSpan || _period == TimeSpan.Zero ? null : now + _period;
            callback(state);
        }

        public void Dispose()
        {
            _due = null;
            lock (clock._timers)
                clock._timers.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
