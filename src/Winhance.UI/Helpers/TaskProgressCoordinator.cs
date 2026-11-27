using Microsoft.UI.Xaml;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.ViewModels;

namespace Winhance.UI.Helpers;

internal sealed class TaskProgressCoordinator
{
    private readonly TaskProgressControl _control1;
    private readonly TaskProgressControl _control2;
    private readonly TaskProgressControl _control3;
    private readonly ITaskProgressService _taskProgressService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IDebounceTimer?[] _hideTimers = new IDebounceTimer?[3];

    public TaskProgressCoordinator(
        TaskProgressControl control1,
        TaskProgressControl control2,
        TaskProgressControl control3,
        ITaskProgressService taskProgressService,
        IDispatcherService dispatcherService)
    {
        _control1 = control1;
        _control2 = control2;
        _control3 = control3;
        _taskProgressService = taskProgressService;
        _dispatcherService = dispatcherService;
    }

    public void HandlePropertyChanged(TaskProgressViewModel tp, string? propertyName)
    {
        if (propertyName is nameof(TaskProgressViewModel.IsLoading) or nameof(TaskProgressViewModel.IsTaskFailed)
            or nameof(TaskProgressViewModel.IsCancelling) or nameof(TaskProgressViewModel.CancelButtonLabel)
            or nameof(TaskProgressViewModel.CloseButtonLabel) or nameof(TaskProgressViewModel.CancellingLabel))
        {
            _dispatcherService.Enqueue(() => RenderSingleTask(tp));
        }
        else if (propertyName == nameof(TaskProgressViewModel.AppName))
        {
            _dispatcherService.Enqueue(() => _control1.AppName = tp.AppName);
        }
        else if (propertyName == nameof(TaskProgressViewModel.LastTerminalLine))
        {
            _dispatcherService.Enqueue(() => _control1.LastTerminalLine = tp.LastTerminalLine);
        }
        else if (propertyName == nameof(TaskProgressViewModel.QueueStatusText))
        {
            _dispatcherService.Enqueue(() => _control1.QueueStatusText = tp.QueueStatusText);
        }
        else if (propertyName == nameof(TaskProgressViewModel.QueueNextItemName))
        {
            _dispatcherService.Enqueue(() => _control1.QueueNextItemName = tp.QueueNextItemName);
        }
        else if (propertyName == nameof(TaskProgressViewModel.IsQueueVisible))
        {
            _dispatcherService.Enqueue(() =>
                _control1.IsQueueInfoVisible = tp.IsQueueVisible ? Visibility.Visible : Visibility.Collapsed);
        }
        else if (propertyName == nameof(TaskProgressViewModel.ActiveScriptCount))
        {
            _dispatcherService.Enqueue(() => UpdateMultiScriptControls(tp.ActiveScriptCount));
        }
    }

    private void RenderSingleTask(TaskProgressViewModel tp)
    {
        // Multi-script mode drives the controls itself.
        if (tp.ActiveScriptCount > 0) return;

        if (!tp.IsLoading)
        {
            _control1.IsProgressVisible = Visibility.Collapsed;
            _control1.CanCancel = Visibility.Collapsed;
            _control1.IsTaskRunning = false;
            return;
        }

        _control1.IsProgressVisible = Visibility.Visible;
        if (tp.IsTaskFailed)
        {
            _control1.CanCancel = Visibility.Visible;
            _control1.IsTaskRunning = true;
            _control1.CancelCommand = tp.CloseFailedTaskCommand;
            _control1.CancelText = tp.CloseButtonLabel;
        }
        else if (_taskProgressService.IsTaskRunning)
        {
            // While cancelling, the button stays in place, disabled, until the current step returns.
            _control1.CanCancel = Visibility.Visible;
            _control1.IsTaskRunning = !tp.IsCancelling;
            _control1.CancelCommand = tp.CancelCommand;
            _control1.CancelText = tp.IsCancelling ? tp.CancellingLabel : tp.CancelButtonLabel;
        }
        else
        {
            _control1.CanCancel = Visibility.Collapsed;
            _control1.IsTaskRunning = false;
        }
    }

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

        _hideTimers[slotIndex]?.Stop();

        // Slot completed -- keep visible briefly so the user can see the result
        if (detail.IsCompletion)
        {
            control.IsTaskRunning = false;
            control.CanCancel = Visibility.Collapsed;
            _hideTimers[slotIndex] ??= _dispatcherService.CreateDebounceTimer(
                TimeSpan.FromMilliseconds(2000), () => control.IsProgressVisible = Visibility.Collapsed);
            _hideTimers[slotIndex]!.Restart();
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
}
