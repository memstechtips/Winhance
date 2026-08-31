namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

// Bytes per second over the last one-second sample, averaged with the sample before it. A stick
// full of small files swings between 0.5 and 5 MB/s from one second to the next, and a readout that
// follows every swing reads as broken.
internal sealed class TransferRate
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);

    private readonly TimeProvider _time;
    private long _sampleTimestamp;
    private long _sampleBytes;

    public TransferRate(TimeProvider time)
    {
        _time = time;
        _sampleTimestamp = time.GetTimestamp();
    }

    public double? BytesPerSecond { get; private set; }

    // True when a fresh sample was taken, which is the caller's cue to redraw.
    public bool Update(long bytesDone)
    {
        var elapsed = _time.GetElapsedTime(_sampleTimestamp);
        if (elapsed < SampleInterval)
        {
            return false;
        }

        var sample = (bytesDone - _sampleBytes) / elapsed.TotalSeconds;
        BytesPerSecond = BytesPerSecond is { } previous ? (previous + sample) / 2 : sample;
        _sampleBytes = bytesDone;
        _sampleTimestamp = _time.GetTimestamp();
        return true;
    }

    public override string ToString() =>
        BytesPerSecond is { } bytesPerSecond ? $"{bytesPerSecond / (1024 * 1024):F1} MB/s" : string.Empty;
}
