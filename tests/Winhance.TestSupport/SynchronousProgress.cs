namespace Winhance.TestSupport;

// Progress<T> posts to the captured synchronization context, so a test that read its list straight
// after the call would race it.
public sealed class SynchronousProgress<T>(Action<T> onReport) : IProgress<T>
{
    public void Report(T value) => onReport(value);
}
