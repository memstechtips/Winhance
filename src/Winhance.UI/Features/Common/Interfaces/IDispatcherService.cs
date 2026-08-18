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

    Task RunOnUIThreadAsync(Func<Task> asyncAction);

    Task RunOnUIThreadAsync(DispatcherQueuePriority priority, Func<Task> asyncAction);

    // Installs a DispatcherQueueSynchronizationContext for the duration, so every await inside marshals back to
    // the UI thread. Use for multi-stage UI work triggered from a background thread or a bare
    // DispatcherQueue.TryEnqueue callback - neither installs a SynchronizationContext, so plain RunOnUIThreadAsync
    // would let continuations after real awaits resume on thread-pool threads.
    Task RunOnUIThreadWithContextAsync(Func<Task> asyncAction);
}
