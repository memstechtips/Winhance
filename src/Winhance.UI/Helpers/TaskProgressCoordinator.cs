using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.ViewModels;

namespace Winhance.UI.Helpers;

/// <summary>
/// Coordinates task progress display across 1-3 TaskProgressControl slots.
/// Extracted from MainWindow to reduce code-behind complexity.
/// </summary>
internal sealed class TaskProgressCoordinator
{
    private readonly TaskProgressControl _control1;
    private readonly TaskProgressControl _control2;
    private readonly TaskProgressControl _control3;
    private readonly ILogService _logService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly CancellationTokenSource?[] _hideDelayCts = new CancellationTokenSource?[3];

    public TaskProgressCoordinator(
        TaskProgressControl control1,
        TaskProgressControl control2,
        TaskProgressControl control3,
        ILogService logService,
        DispatcherQueue dispatcherQueue)
    {
        _control1 = control1;
        _control2 = control2;
        _control3 = control3;
        _logService = logService;
        _dispatcherQueue = dispatcherQueue;
    }

    /// <summary>
    /// Handles TaskProgressViewModel property changes, updating the primary TaskProgressControl.
    /// </summary>
    public void HandlePropertyChanged(TaskProgressViewModel tp, string? propertyName)
    {
        if (propertyName == nameof(TaskProgressViewModel.IsLoading))
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                // Skip single-task IsLoading updates when multi-script mode is active
                if (tp.ActiveScriptCount > 0) return;

                var taskProgressService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetService<ITaskProgressService>(App.Services);
                var isActuallyRunning = taskProgressService?.IsTaskRunning == true;

                if (tp.IsLoading)
                {
                    _control1.IsProgressVisible = Visibility.Visible;
                    if (isActuallyRunning)
                    {
                        _control1.CanCancel = Visibility.Visible;
                        _control1.IsTaskRunning = true;
                        _control1.CancelCommand = tp.CancelCommand;
                        _control1.CancelText = tp.CancelButtonLabel;
                    }
                    else if (tp.IsTaskFailed)
                    {
                        // Failure state: show Close button to dismiss the bar
                        _control1.CanCancel = Visibility.Visible;
                        _control1.IsTaskRunning = true;
                        _control1.CancelCommand = tp.CloseFailedTaskCommand;
                        _control1.CancelText = tp.CloseButtonLabel;
                    }
                    else
                    {
                        _control1.CanCancel = Visibility.Collapsed;
                        _control1.IsTaskRunning = false;
                    }
                }
                else
                {
                    _control1.IsProgressVisible = Visibility.Collapsed;
                    _control1.CanCancel = Visibility.Collapsed;
                    _control1.IsTaskRunning = false;
                }
            });
        }
        else if (propertyName == nameof(TaskProgressViewModel.AppName))
        {
            _dispatcherQueue.TryEnqueue(() => _control1.AppName = tp.AppName);
        }
        else if (propertyName == nameof(TaskProgressViewModel.LastTerminalLine))
        {
            _dispatcherQueue.TryEnqueue(() => _control1.LastTerminalLine = tp.LastTerminalLine);
        }
        else if (propertyName == nameof(TaskProgressViewModel.CancelButtonLabel))
        {
            _dispatcherQueue.TryEnqueue(() => _control1.CancelText = tp.CancelButtonLabel);
        }
        else if (propertyName == nameof(TaskProgressViewModel.QueueStatusText))
        {
            _dispatcherQueue.TryEnqueue(() => _control1.QueueStatusText = tp.QueueStatusText);
        }
        else if (propertyName == nameof(TaskProgressViewModel.QueueNextItemName))
        {
            _dispatcherQueue.TryEnqueue(() => _control1.QueueNextItemName = tp.QueueNextItemName);
        }
        else if (propertyName == nameof(TaskProgressViewModel.IsQueueVisible))
        {
            _dispatcherQueue.TryEnqueue(() =>
                _control1.IsQueueInfoVisible = tp.IsQueueVisible ? Visibility.Visible : Visibility.Collapsed);
        }
        else if (propertyName == nameof(TaskProgressViewModel.ActiveScriptCount))
        {
            _dispatcherQueue.TryEnqueue(() => UpdateMultiScriptControls(tp.ActiveScriptCount));
        }
    }

    /// <summary>
    /// Routes multi-script progress updates to the correct TaskProgressControl slot.
    /// Adds a 2-second delay before hiding a slot on completion.
    /// </summary>
    public void HandleScriptProgressReceived(int slotIndex, TaskProgressDetail detail)
    {
        var control = slotIndex switch
        {
            0 => _control1,
            1 => _control2,
            2 => _control3,
            _ => null
        };
        if (control == null) return;

        // Cancel any pending hide-delay for this slot when new data arrives
        CancelPendingHide(slotIndex);

        // Slot completed -- keep visible briefly so the user can see the result
        if (detail.IsCompletion)
        {
            control.IsTaskRunning = false;
            control.CanCancel = Visibility.Collapsed;
            var cts = new CancellationTokenSource();
            _hideDelayCts[slotIndex] = cts;
            HideControlAfterDelayAsync(control, 2000, cts.Token).FireAndForget(_logService);
            return;
        }

        if (!string.IsNullOrEmpty(detail.StatusText))
            control.AppName = detail.StatusText;
        control.LastTerminalLine = detail.TerminalOutput ?? "";
        if (detail.QueueTotal > 1)
        {
            control.IsQueueInfoVisible = Visibility.Visible;
            control.QueueStatusText = $"{detail.QueueCurrent} / {detail.QueueTotal}";
            control.QueueNextItemName = !string.IsNullOrEmpty(detail.QueueNextItemName)
                ? $"Next: {detail.QueueNextItemName}" : "";
        }
    }

    /// <summary>
    /// Shows/hides multi-script progress controls based on active slot count.
    /// </summary>
    private void UpdateMultiScriptControls(int activeCount)
    {
        _control1.IsProgressVisible = activeCount >= 1 ? Visibility.Visible : Visibility.Collapsed;
        _control1.IsTaskRunning = activeCount >= 1;
        _control1.CanCancel = Visibility.Collapsed;

        _control2.IsProgressVisible = activeCount >= 2 ? Visibility.Visible : Visibility.Collapsed;
        _control2.IsTaskRunning = activeCount >= 2;
        _control2.CanCancel = Visibility.Collapsed;

        _control3.IsProgressVisible = activeCount >= 3 ? Visibility.Visible : Visibility.Collapsed;
        _control3.IsTaskRunning = activeCount >= 3;
        _control3.CanCancel = Visibility.Collapsed;
    }

    /// <summary>
    /// Cancels any pending hide-delay for the given slot.
    /// </summary>
    private void CancelPendingHide(int slotIndex)
    {
        var old = Interlocked.Exchange(ref _hideDelayCts[slotIndex], null);
        old?.Cancel();
        old?.Dispose();
    }

    /// <summary>
    /// Hides a TaskProgressControl after the specified delay.
    /// </summary>
    private async Task HideControlAfterDelayAsync(TaskProgressControl control, int delayMs, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            _dispatcherQueue.TryEnqueue(() =>
            {
                control.IsProgressVisible = Visibility.Collapsed;
            });
        }
        catch (OperationCanceledException)
        {
            // Hide was cancelled because a new task started on this slot
        }
    }
}
