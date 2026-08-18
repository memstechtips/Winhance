using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class TaskProgressService : ITaskProgressService, IMultiScriptProgressService
{
    private const int MaxTerminalOutputLines = 10_000;

    private readonly ILogService _logService;
    private string _currentStatusText;
    private bool _isTaskRunning;
    private bool _isIndeterminate;
    private List<string> _logMessages = new List<string>();
    private List<string> _terminalOutputLines = new List<string>();
    private bool _lastTerminalLineWasProgress;
    private CancellationTokenSource? _cancellationSource;

    private int _queueTotal;
    private int _queueCurrent;
    private string? _queueNextItemName;

    private int _activeScriptSlotCount;
    private string[]? _scriptSlotNames;

    private volatile bool _skipNextRequested;

    public bool IsTaskRunning => _isTaskRunning;

    public string CurrentStatusText => _currentStatusText;

    public bool IsIndeterminate => _isIndeterminate;

    public CancellationTokenSource? CurrentTaskCancellationSource => _cancellationSource;

    public event EventHandler<TaskProgressDetail>? ProgressUpdated;

    public TaskProgressService(ILogService logService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _currentStatusText = string.Empty;
        _isTaskRunning = false;
        _isIndeterminate = false;
    }

    public CancellationTokenSource StartTask(string taskName, bool isIndeterminate = false)
    {
        CancelCurrentTask();

        if (string.IsNullOrEmpty(taskName))
        {
            throw new ArgumentException("Task name cannot be null or empty.", nameof(taskName));
        }

        _cancellationSource = new CancellationTokenSource();
        _currentStatusText = taskName;
        _isTaskRunning = true;
        _isIndeterminate = isIndeterminate;
        _logMessages.Clear();
        _terminalOutputLines.Clear();
        _lastTerminalLineWasProgress = false;
        _queueTotal = 0;
        _queueCurrent = 0;
        _queueNextItemName = null;
        _skipNextRequested = false;

        _logService.Log(LogLevel.Info, $"[TASKPROGRESSSERVICE] Task started: {taskName}");
        AddLogMessage($"[TASKPROGRESSSERVICE] Task started: {taskName}");
        OnProgressChanged(
            new TaskProgressDetail
            {
                Progress = 0,
                StatusText = taskName,
                IsIndeterminate = isIndeterminate,
            }
        );

        return _cancellationSource;
    }

    public void UpdateProgress(int progressPercentage, string? statusText = null)
    {
        if (!_isTaskRunning)
        {
            return;
        }

        if (progressPercentage < 0 || progressPercentage > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(progressPercentage),
                "Progress must be between 0 and 100."
            );
        }

        if (!string.IsNullOrEmpty(statusText))
        {
            _currentStatusText = statusText;
            _logService.Log(
                LogLevel.Info,
                $"Task progress ({progressPercentage}%): {statusText}"
            );
            AddLogMessage($"Task progress ({progressPercentage}%): {statusText}");
        }
        else
        {
            _logService.Log(LogLevel.Info, $"Task progress: {progressPercentage}%");
            AddLogMessage($"Task progress: {progressPercentage}%");
        }
        OnProgressChanged(
            new TaskProgressDetail
            {
                Progress = progressPercentage,
                StatusText = _currentStatusText,
            }
        );
    }

    public void UpdateDetailedProgress(TaskProgressDetail detail)
    {
        if (!_isTaskRunning)
        {
            return;
        }

        if (detail.Progress.HasValue)
        {
            if (detail.Progress.Value < 0 || detail.Progress.Value > 100)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(detail),
                    "Progress must be between 0 and 100."
                );
            }
        }

        if (!string.IsNullOrEmpty(detail.StatusText))
        {
            _currentStatusText = detail.StatusText;
        }

        _isIndeterminate = detail.IsIndeterminate;

        // Always remove the last progress line before adding ANY new line. This handles:
        //   progress → progress: replacement (progress bar filling)
        //   progress → permanent: cleanup (stale progress/spinner removed)
        //   permanent → permanent: normal append
        //   permanent → progress: normal append
        if (!string.IsNullOrEmpty(detail.TerminalOutput))
        {
            if (IsTerminalNoise(detail.TerminalOutput))
            {
                detail.TerminalOutput = null; // Suppress noise from event subscribers
            }
            else
            {
                if (_lastTerminalLineWasProgress && _terminalOutputLines.Count > 0)
                {
                    _terminalOutputLines.RemoveAt(_terminalOutputLines.Count - 1);
                }
                else if (detail.IsProgressIndicator && _terminalOutputLines.Count > 0)
                {
                    // The first progress bar sometimes arrives as a permanent line
                    // (winget's initial render uses \n before switching to \r).
                    // Detect and remove it so it doesn't duplicate.
                    var lastLine = _terminalOutputLines[_terminalOutputLines.Count - 1];
                    if (LooksLikeProgressBar(lastLine))
                    {
                        _terminalOutputLines.RemoveAt(_terminalOutputLines.Count - 1);
                    }
                }
                AddTerminalOutputLine(detail.TerminalOutput);
                _lastTerminalLineWasProgress = detail.IsProgressIndicator;
            }
        }

        if (!string.IsNullOrEmpty(detail.DetailedMessage))
        {
            _logService.Log(detail.LogLevel, detail.DetailedMessage);
            AddLogMessage(detail.DetailedMessage);
        }
        OnProgressChanged(detail);
    }

    public void CompleteTask()
    {
        if (!_isTaskRunning)
        {
            return;
        }

        _isTaskRunning = false;
        _isIndeterminate = false;
        _queueTotal = 0;
        _queueCurrent = 0;
        _queueNextItemName = null;
        _skipNextRequested = false;

        _logService.Log(LogLevel.Info, $"Task completed: {_currentStatusText}");
        AddLogMessage($"Task completed: {_currentStatusText}");

        OnProgressChanged(
            new TaskProgressDetail
            {
                Progress = 100,
                StatusText = _currentStatusText,
                DetailedMessage = "Task completed",
            }
        );

        _cancellationSource?.Dispose();
        _cancellationSource = null;
    }

    private void AddLogMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        _logMessages.Add(message);
    }

    public IReadOnlyList<string> GetTerminalOutputLines() => _terminalOutputLines.AsReadOnly();

    public void CancelCurrentTask()
    {
        if (_cancellationSource != null && !_cancellationSource.IsCancellationRequested)
        {
            _cancellationSource.Cancel();
            AddLogMessage("Task cancelled by user");
        }
    }

    public IProgress<TaskProgressDetail> CreateDetailedProgress()
    {
        return new Progress<TaskProgressDetail>(UpdateDetailedProgress);
    }

    public IProgress<TaskProgressDetail> CreatePowerShellProgress()
    {
        return new Progress<TaskProgressDetail>(UpdateDetailedProgress);
    }

    public CancellationTokenSource StartMultiScriptTask(string[] scriptNames)
    {
        CancelCurrentTask();

        if (scriptNames == null || scriptNames.Length == 0)
            throw new ArgumentException("At least one script name is required.", nameof(scriptNames));

        _cancellationSource = new CancellationTokenSource();
        _isTaskRunning = true;
        _activeScriptSlotCount = scriptNames.Length;
        _scriptSlotNames = scriptNames;
        _currentStatusText = string.Empty;
        _logMessages.Clear();
        _terminalOutputLines.Clear();
        _lastTerminalLineWasProgress = false;
        _queueTotal = 0;
        _queueCurrent = 0;
        _queueNextItemName = null;
        _skipNextRequested = false;

        _logService.Log(LogLevel.Info, $"[TASKPROGRESSSERVICE] Multi-script task started with {scriptNames.Length} slots");

        for (int i = 0; i < scriptNames.Length; i++)
        {
            ProgressUpdated?.Invoke(this, new TaskProgressDetail
            {
                ScriptSlotIndex = i,
                ScriptSlotCount = scriptNames.Length,
                StatusText = scriptNames[i],
                IsIndeterminate = true,
                IsActive = true
            });
        }

        return _cancellationSource;
    }

    public IProgress<TaskProgressDetail> CreateScriptProgress(int slotIndex)
    {
        var slotCount = _activeScriptSlotCount;
        var slotName = _scriptSlotNames != null && slotIndex < _scriptSlotNames.Length
            ? _scriptSlotNames[slotIndex] : null;
        return new Progress<TaskProgressDetail>(detail =>
        {
            detail.ScriptSlotIndex = slotIndex;
            detail.ScriptSlotCount = slotCount;

            if (slotName != null && slotCount > 1 && !string.IsNullOrEmpty(detail.TerminalOutput))
                detail.TerminalOutput = $"[{slotName}] {detail.TerminalOutput}";

            if (!string.IsNullOrEmpty(detail.TerminalOutput)
                && !IsTerminalNoise(detail.TerminalOutput))
            {
                if (_lastTerminalLineWasProgress && _terminalOutputLines.Count > 0)
                {
                    _terminalOutputLines.RemoveAt(_terminalOutputLines.Count - 1);
                }
                else if (detail.IsProgressIndicator && _terminalOutputLines.Count > 0)
                {
                    var lastLine = _terminalOutputLines[_terminalOutputLines.Count - 1];
                    if (LooksLikeProgressBar(lastLine))
                    {
                        _terminalOutputLines.RemoveAt(_terminalOutputLines.Count - 1);
                    }
                }
                AddTerminalOutputLine(detail.TerminalOutput);
                _lastTerminalLineWasProgress = detail.IsProgressIndicator;
            }

            // Fire directly without sticky queue logic
            ProgressUpdated?.Invoke(this, detail);
        });
    }

    public void CompleteMultiScriptTask()
    {
        _isTaskRunning = false;
        _activeScriptSlotCount = 0;
        _scriptSlotNames = null;
        _queueTotal = 0;
        _queueCurrent = 0;
        _queueNextItemName = null;
        _skipNextRequested = false;

        _logService.Log(LogLevel.Info, "[TASKPROGRESSSERVICE] Multi-script task completed");

        // Signal completion: ScriptSlotCount=0 tells UI to hide all controls
        ProgressUpdated?.Invoke(this, new TaskProgressDetail
        {
            ScriptSlotIndex = -1,
            ScriptSlotCount = 0,
            Progress = 100,
            StatusText = "Completed",
            DetailedMessage = "Multi-script task completed"
        });

        _cancellationSource?.Dispose();
        _cancellationSource = null;
    }

    public bool ConsumeSkipNextRequest()
    {
        if (!_skipNextRequested) return false;
        _skipNextRequested = false;
        return true;
    }

    // Multi-script updates (ScriptSlotCount > 0) bypass the sticky-queue logic.
    protected virtual void OnProgressChanged(TaskProgressDetail detail)
    {
        if (detail.ScriptSlotCount > 0)
        {
            ProgressUpdated?.Invoke(this, detail);
            return;
        }

        if (detail.QueueTotal > 0)
        {
            _queueTotal = detail.QueueTotal;
            _queueCurrent = detail.QueueCurrent;
            _queueNextItemName = detail.QueueNextItemName;
        }

        if (_queueTotal > 1)
        {
            detail.QueueTotal = _queueTotal;
            detail.QueueCurrent = _queueCurrent;
            detail.QueueNextItemName = _queueNextItemName;
        }

        ProgressUpdated?.Invoke(this, detail);
    }

    // Spinner characters (-, \, |, /) are NOT filtered - they are delivered as IsProgressIndicator=true and animate
    // in place in the live terminal dialog.
    private static bool IsTerminalNoise(string line)
    {
        var trimmed = line.Trim();
        return string.IsNullOrEmpty(trimmed);
    }

    private void AddTerminalOutputLine(string line)
    {
        if (_terminalOutputLines.Count >= MaxTerminalOutputLines)
        {
            var removeCount = MaxTerminalOutputLines / 10;
            _terminalOutputLines.RemoveRange(0, removeCount);
        }
        _terminalOutputLines.Add(line);
    }

    // Catches the duplicate first progress-bar line winget sometimes emits with \n before switching to \r.
    private static bool LooksLikeProgressBar(string line)
    {
        foreach (char c in line)
        {
            if (c >= '\u2588' && c <= '\u258F') return true;
            if (c == '\u2591') return true; // ░ (unfilled track)
        }
        return false;
    }
}
