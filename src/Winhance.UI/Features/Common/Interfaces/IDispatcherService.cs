using Microsoft.UI.Dispatching;

namespace Winhance.UI.Features.Common.Interfaces;

public interface IDispatcherService
{
    // Call from the MainWindow constructor after the window is created.
    void Initialize(DispatcherQueue dispatcherQueue);

    bool HasThreadAccess { get; }

    // Synchronous when already on the UI thread, enqueued otherwise.
    void RunOnUIThread(Action action);

    void RunOnUIThread(DispatcherQueuePriority priority, Action action);

    // Queued even when already on the UI thread, so the work in progress finishes first.
    void Enqueue(Action action);

    Task RunOnUIThreadAsync(Func<Task> asyncAction);

    Task RunOnUIThreadAsync(DispatcherQueuePriority priority, Func<Task> asyncAction);

    // Installs a DispatcherQueueSynchronizationContext for the duration, so every await inside marshals back to
    // the UI thread. Use for multi-stage UI work triggered from a background thread or a bare
    // DispatcherQueue.TryEnqueue callback - neither installs a SynchronizationContext, so plain RunOnUIThreadAsync
    // would let continuations after real awaits resume on thread-pool threads.
    Task RunOnUIThreadWithContextAsync(Func<Task> asyncAction);

    // A one-shot timer that ticks on the UI thread. Each Restart pushes the tick back by the whole interval, so a
    // burst of restarts ticks once.
    IDebounceTimer CreateDebounceTimer(TimeSpan interval, Action tick);
}

public interface IDebounceTimer
{
    void Restart();

    void Stop();
}
