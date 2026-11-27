using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Extensions;

namespace Winhance.UI.ViewModels;

public partial class TaskProgressViewModel : ObservableObject, IDisposable
{
    private static readonly string[] LocalizedProperties =
    [
        nameof(CancelButtonLabel), nameof(CloseButtonLabel), nameof(CancellingLabel),
    ];

    private bool _disposed;
    private readonly ITaskProgressService _taskProgressService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    // Set only once a failed task has ended and left output to read, so the bar can offer Close instead of Cancel.
    [ObservableProperty]
    public partial bool IsTaskFailed { get; set; }

    // Cancel was clicked; the task ends when its current step returns, which can take seconds.
    [ObservableProperty]
    public partial bool IsCancelling { get; set; }

    private IDebounceTimer? _hideTimer;

    [ObservableProperty]
    public partial string AppName { get; set; }

    [ObservableProperty]
    public partial string LastTerminalLine { get; set; }

    [ObservableProperty]
    public partial string QueueStatusText { get; set; }

    [ObservableProperty]
    public partial string QueueNextItemName { get; set; }

    [ObservableProperty]
    public partial bool IsQueueVisible { get; set; }

    [ObservableProperty]
    public partial int ActiveScriptCount { get; set; }

    public event Action<int, TaskProgressDetail>? ScriptProgressReceived;

    public string CancelButtonLabel =>
        _localizationService.GetStringOrDefault("Button_Cancel", "Cancel");

    public string CloseButtonLabel =>
        _localizationService.GetStringOrDefault("Button_Close", "Close");

    public string CancellingLabel =>
        _localizationService.GetStringOrDefault("Progress_Cancelling", "Cancelling...");

    public TaskProgressViewModel(
        ITaskProgressService taskProgressService,
        IDispatcherService dispatcherService,
        IDialogService dialogService,
        ILocalizationService localizationService)
    {
        _taskProgressService = taskProgressService;
        _dispatcherService = dispatcherService;
        _dialogService = dialogService;
        _localizationService = localizationService;

        AppName = string.Empty;
        LastTerminalLine = string.Empty;
        QueueStatusText = string.Empty;
        QueueNextItemName = string.Empty;

        _taskProgressService.ProgressUpdated += OnProgressUpdated;
        _localizationService.LanguageChanged += OnLanguageChanged;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _taskProgressService.ProgressUpdated -= OnProgressUpdated;
        _localizationService.LanguageChanged -= OnLanguageChanged;
        _hideTimer?.Stop();
        _hideTimer = null;
        GC.SuppressFinalize(this);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var name in LocalizedProperties)
            OnPropertyChanged(name);

        if (IsTaskFailed)
            LastTerminalLine = _localizationService.GetString("Progress_ClickToSeeDetails");
    }

    [RelayCommand]
    private void Cancel()
    {
        if (!_taskProgressService.IsTaskRunning)
            return;

        _taskProgressService.CancelCurrentTask();
        IsCancelling = true;
    }

    [RelayCommand]
    private void CloseFailedTask()
    {
        IsTaskFailed = false;
        IsLoading = false;
    }

    [RelayCommand]
    private async Task ShowDetailsAsync()
    {
        var terminalLines = _taskProgressService.GetTerminalOutputLines();
        var title = _localizationService.GetString("Dialog_TerminalOutput_Title");
        await _dialogService.ShowTaskOutputDialogAsync(title, terminalLines);

        if (!_taskProgressService.IsTaskRunning)
        {
            IsTaskFailed = false;
            IsLoading = false;
        }
    }

    private void OnProgressUpdated(object? sender, TaskProgressDetail detail)
    {
        _dispatcherService.RunOnUIThread(() =>
        {
            if (detail.ScriptSlotCount > 0)
            {
                ActiveScriptCount = detail.ScriptSlotCount;
                ScriptProgressReceived?.Invoke(detail.ScriptSlotIndex, detail);
            }
            else if (ActiveScriptCount > 0 && detail.ScriptSlotIndex == -1)
            {
                // Multi-script task completed (ScriptSlotCount went to 0)
                ActiveScriptCount = 0;
            }
            else
            {
                var wasRunning = IsLoading;
                var isNowRunning = _taskProgressService.IsTaskRunning;

                if (isNowRunning)
                {
                    _hideTimer?.Stop();
                    IsTaskFailed = false;
                    if (!wasRunning)
                        IsCancelling = false;

                    IsLoading = true;
                    if (!string.IsNullOrEmpty(detail.StatusText))
                        AppName = detail.StatusText;
                    LastTerminalLine = detail.TerminalOutput ?? string.Empty;
                }
                else if (wasRunning && !IsTaskFailed)
                {
                    var wasCancelled = IsCancelling || _taskProgressService.CurrentTaskCancellationSource
                        ?.IsCancellationRequested == true;
                    // FailTask and CancelTask end a task at progress 0, CompleteTask at 100.
                    var failed = detail.Progress == 0;
                    IsCancelling = false;

                    if (wasCancelled)
                    {
                        IsLoading = false;
                    }
                    else if (failed && _taskProgressService.GetTerminalOutputLines().Count > 0)
                    {
                        // The task has ended; the bar stays only to lead to the output it left.
                        IsTaskFailed = true;
                        LastTerminalLine = _localizationService.GetString("Progress_ClickToSeeDetails");
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(detail.StatusText))
                            AppName = detail.StatusText;
                        LastTerminalLine = detail.TerminalOutput ?? string.Empty;
                        _hideTimer ??= _dispatcherService.CreateDebounceTimer(TimeSpan.FromMilliseconds(2000), HideIfIdle);
                        _hideTimer.Restart();
                    }
                }

                if (detail.QueueTotal > 1)
                {
                    IsQueueVisible = true;
                    QueueStatusText = $"{detail.QueueCurrent} / {detail.QueueTotal}";
                    QueueNextItemName = !string.IsNullOrEmpty(detail.QueueNextItemName)
                        ? $"Next: {detail.QueueNextItemName}"
                        : string.Empty;
                }
                else
                {
                    IsQueueVisible = false;
                    QueueStatusText = string.Empty;
                    QueueNextItemName = string.Empty;
                }
            }
        });
    }

    private void HideIfIdle()
    {
        if (!_taskProgressService.IsTaskRunning)
            IsLoading = false;
    }
}
