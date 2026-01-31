using Microsoft.UI.Dispatching;

namespace Winhance.UI.Features.Common.Interfaces;

/// <summary>
/// Service for dispatching actions to the UI thread.
/// Required because WinUI 3 uses DispatcherQueue instead of WPF's Dispatcher.
/// </summary>
public interface IDispatcherService
{
    /// <summary>
    /// Initializes the dispatcher service with the DispatcherQueue from the main window.
    /// Must be called from MainWindow constructor after window is created.
    /// </summary>
    /// <param name="dispatcherQueue">The DispatcherQueue from the main window.</param>
    void Initialize(DispatcherQueue dispatcherQueue);

    /// <summary>
    /// Gets whether the calling thread has access to the UI thread.
    /// </summary>
    bool HasThreadAccess { get; }

    /// <summary>
    /// Runs an action on the UI thread synchronously if already on UI thread,
    /// or enqueues it if on a background thread.
    /// </summary>
    /// <param name="action">The action to run on the UI thread.</param>
    void RunOnUIThread(Action action);

    /// <summary>
    /// Runs an action on the UI thread with the specified priority.
    /// </summary>
    /// <param name="priority">The priority for the dispatcher queue.</param>
    /// <param name="action">The action to run on the UI thread.</param>
    void RunOnUIThread(DispatcherQueuePriority priority, Action action);

    /// <summary>
    /// Runs an async action on the UI thread and awaits completion.
    /// </summary>
    /// <param name="asyncAction">The async action to run on the UI thread.</param>
    /// <returns>A task representing the completion of the action.</returns>
    Task RunOnUIThreadAsync(Func<Task> asyncAction);

    /// <summary>
    /// Runs an async action on the UI thread with the specified priority and awaits completion.
    /// </summary>
    /// <param name="priority">The priority for the dispatcher queue.</param>
    /// <param name="asyncAction">The async action to run on the UI thread.</param>
    /// <returns>A task representing the completion of the action.</returns>
    Task RunOnUIThreadAsync(DispatcherQueuePriority priority, Func<Task> asyncAction);

    /// <summary>
    /// Creates a timer on the dispatcher queue.
    /// Replaces WPF's DispatcherTimer.
    /// </summary>
    /// <returns>A DispatcherQueueTimer.</returns>
    DispatcherQueueTimer CreateTimer();
}
