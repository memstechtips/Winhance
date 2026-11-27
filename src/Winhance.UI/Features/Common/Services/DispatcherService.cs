using Microsoft.UI.Dispatching;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

// Late initialization: DI services are created during container build, BEFORE the Window exists, and
// DispatcherQueue.GetForCurrentThread() only works on the UI thread.
public class DispatcherService(ILogService logService) : IDispatcherService
{
    private DispatcherQueue? _dispatcherQueue;

    public bool HasThreadAccess => _dispatcherQueue?.HasThreadAccess ?? false;

    public void Initialize(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    public void RunOnUIThread(Action action)
    {
        EnsureInitialized();

        if (_dispatcherQueue!.HasThreadAccess)
        {
            action();
        }
        else
        {
            _dispatcherQueue.TryEnqueue(Logged(action));
        }
    }

    public void RunOnUIThread(DispatcherQueuePriority priority, Action action)
    {
        EnsureInitialized();

        if (_dispatcherQueue!.HasThreadAccess)
        {
            action();
        }
        else
        {
            _dispatcherQueue.TryEnqueue(priority, Logged(action));
        }
    }

    public void Enqueue(Action action)
    {
        EnsureInitialized();
        _dispatcherQueue!.TryEnqueue(Logged(action));
    }

    public async Task RunOnUIThreadAsync(Func<Task> asyncAction)
    {
        await RunOnUIThreadAsync(DispatcherQueuePriority.Normal, asyncAction);
    }

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

    public Task RunOnUIThreadWithContextAsync(Func<Task> asyncAction)
    {
        EnsureInitialized();

        var tcs = new TaskCompletionSource();

        void Start()
        {
            // A bare DispatcherQueue.TryEnqueue callback (and a background thread)
            // carry no SynchronizationContext, so `await` continuations inside
            // asyncAction would resume on thread-pool threads. Install the
            // dispatcher context so they marshal back to the UI thread.
            if (SynchronizationContext.Current is null)
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherQueueSynchronizationContext(_dispatcherQueue!));
            }

            InvokeAsync().FireAndForget(logService);
        }

        async Task InvokeAsync()
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
        }

        if (_dispatcherQueue!.HasThreadAccess)
        {
            Start();
        }
        else if (!_dispatcherQueue.TryEnqueue(Logged(Start)))
        {
            throw new InvalidOperationException("Failed to enqueue action to dispatcher queue.");
        }

        return tcs.Task;
    }

    public IDebounceTimer CreateDebounceTimer(TimeSpan interval, Action tick)
    {
        EnsureInitialized();

        var timer = _dispatcherQueue!.CreateTimer();
        timer.Interval = interval;
        timer.IsRepeating = false;
        timer.Tick += (_, _) => Logged(tick)();
        return new DebounceTimer(timer);
    }

    // An exception thrown in a dispatcher callback ends the process without reaching any Winhance handler, so this
    // is the one place that logs and rethrows: the log keeps the cause and the crash dump still gets the exception.
    internal DispatcherQueueHandler Logged(Action callback) => () =>
    {
        try
        {
            callback();
        }
        catch (Exception ex)
        {
            logService.LogError($"Dispatcher callback threw {ex.GetType().FullName}", ex);
            throw;
        }
    };

    private sealed class DebounceTimer(DispatcherQueueTimer timer) : IDebounceTimer
    {
        public void Restart()
        {
            timer.Stop();
            timer.Start();
        }

        public void Stop() => timer.Stop();
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
