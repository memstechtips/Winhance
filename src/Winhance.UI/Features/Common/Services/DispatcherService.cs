using Microsoft.UI.Dispatching;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

/// <summary>
/// Implementation of IDispatcherService for WinUI 3.
/// Provides UI thread dispatching capabilities using DispatcherQueue.
/// </summary>
/// <remarks>
/// This service uses late initialization because DI services are created during
/// container build, which happens BEFORE the Window exists. DispatcherQueue.GetForCurrentThread()
/// only works on the UI thread, so the DispatcherQueue must be set after window creation.
/// </remarks>
public class DispatcherService : IDispatcherService
{
    private DispatcherQueue? _dispatcherQueue;

    /// <inheritdoc/>
    public bool HasThreadAccess => _dispatcherQueue?.HasThreadAccess ?? false;

    /// <inheritdoc/>
    public void Initialize(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
    }

    /// <inheritdoc/>
    public void RunOnUIThread(Action action)
    {
        EnsureInitialized();

        if (_dispatcherQueue!.HasThreadAccess)
        {
            action();
        }
        else
        {
            _dispatcherQueue.TryEnqueue(() => action());
        }
    }

    /// <inheritdoc/>
    public void RunOnUIThread(DispatcherQueuePriority priority, Action action)
    {
        EnsureInitialized();

        if (_dispatcherQueue!.HasThreadAccess)
        {
            action();
        }
        else
        {
            _dispatcherQueue.TryEnqueue(priority, () => action());
        }
    }

    /// <inheritdoc/>
    public async Task RunOnUIThreadAsync(Func<Task> asyncAction)
    {
        await RunOnUIThreadAsync(DispatcherQueuePriority.Normal, asyncAction);
    }

    /// <inheritdoc/>
    public async Task RunOnUIThreadAsync(DispatcherQueuePriority priority, Func<Task> asyncAction)
    {
        EnsureInitialized();

        if (_dispatcherQueue!.HasThreadAccess)
        {
            await asyncAction();
            return;
        }

        var tcs = new TaskCompletionSource();

        var enqueued = _dispatcherQueue.TryEnqueue(priority, async () =>
        {
            try
            {
                await asyncAction();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        if (!enqueued)
        {
            throw new InvalidOperationException("Failed to enqueue action to dispatcher queue.");
        }

        await tcs.Task;
    }

    /// <inheritdoc/>
    public DispatcherQueueTimer CreateTimer()
    {
        EnsureInitialized();
        return _dispatcherQueue!.CreateTimer();
    }

    private void EnsureInitialized()
    {
        if (_dispatcherQueue == null)
        {
            throw new InvalidOperationException(
                "DispatcherService not initialized. Call Initialize() from MainWindow constructor after window creation.");
        }
    }
}
