using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Service that manages task progress reporting across the application.
    /// </summary>
    public class TaskProgressService : ITaskProgressService
    {
        private readonly ILogService _logService;
        private int _currentProgress;
        private string _currentStatusText;
        private bool _isTaskRunning;
        private bool _isIndeterminate;
        private List<string> _logMessages = new List<string>();
        private List<string> _terminalOutputLines = new List<string>();
        private bool _lastTerminalLineWasProgress;
        private CancellationTokenSource? _cancellationSource;

        // Queue sticky state
        private int _queueTotal;
        private int _queueCurrent;
        private string? _queueNextItemName;

        // Multi-script slot state
        private int _activeScriptSlotCount;
        private string[]? _scriptSlotNames;

        // Skip-next flag
        private volatile bool _skipNextRequested;

        /// <summary>
        /// Gets whether a task is currently running.
        /// </summary>
        public bool IsTaskRunning => _isTaskRunning;

        /// <summary>
        /// Gets the current progress value (0-100).
        /// </summary>
        public int CurrentProgress => _currentProgress;

        /// <summary>
        /// Gets the current status text.
        /// </summary>
        public string CurrentStatusText => _currentStatusText;

        /// <summary>
        /// Gets whether the current task is in indeterminate mode.
        /// </summary>
        public bool IsIndeterminate => _isIndeterminate;

        /// <summary>
        /// Gets the cancellation token source for the current task.
        /// </summary>
        public CancellationTokenSource? CurrentTaskCancellationSource => _cancellationSource;

        /// <summary>
        /// Event raised when progress changes.
        /// </summary>
        public event EventHandler<TaskProgressDetail>? ProgressUpdated;

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskProgressService"/> class.
        /// </summary>
        /// <param name="logService">The log service.</param>
        public TaskProgressService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _currentProgress = 0;
            _currentStatusText = string.Empty;
            _isTaskRunning = false;
            _isIndeterminate = false;
        }

        /// <summary>
        /// Starts a new task with the specified name.
        /// </summary>
        /// <param name="taskName">The name of the task.</param>
        /// <param name="isIndeterminate">Whether the task progress is indeterminate.</param>
        /// <returns>A cancellation token source for the task.</returns>
        public CancellationTokenSource StartTask(string taskName, bool isIndeterminate = false)
        {
            // Cancel any existing task
            CancelCurrentTask();

            if (string.IsNullOrEmpty(taskName))
            {
                throw new ArgumentException("Task name cannot be null or empty.", nameof(taskName));
            }

            _cancellationSource = new CancellationTokenSource();
            _currentProgress = 0;
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

            _logService.Log(LogLevel.Info, $"[TASKPROGRESSSERVICE] Task started: {taskName}"); // Corrected Log call
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

        /// <summary>
        /// Updates the progress of the current task.
        /// </summary>
        /// <param name="progressPercentage">The progress percentage (0-100).</param>
        /// <param name="statusText">The status text.</param>
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

            _currentProgress = progressPercentage;
            if (!string.IsNullOrEmpty(statusText))
            {
                _currentStatusText = statusText;
                _logService.Log(
                    LogLevel.Info,
                    $"Task progress ({progressPercentage}%): {statusText}"
                ); // Corrected Log call
                AddLogMessage($"Task progress ({progressPercentage}%): {statusText}");
            }
            else
            {
                _logService.Log(LogLevel.Info, $"Task progress: {progressPercentage}%"); // Corrected Log call
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

        /// <summary>
        /// Updates the progress with detailed information.
        /// </summary>
        /// <param name="detail">The detailed progress information.</param>
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
                        nameof(detail.Progress),
                        "Progress must be between 0 and 100."
                    );
                }

                _currentProgress = (int)detail.Progress.Value;
            }

            if (!string.IsNullOrEmpty(detail.StatusText))
            {
                _currentStatusText = detail.StatusText;
            }

            _isIndeterminate = detail.IsIndeterminate;

            // Accumulate terminal output lines for the details dialog,
            // filtering out noise (blank lines).
            // Always remove the last progress line
            // before adding ANY new line. This handles:
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
                    _terminalOutputLines.Add(detail.TerminalOutput);
                    _lastTerminalLineWasProgress = detail.IsProgressIndicator;
                }
            }

            if (!string.IsNullOrEmpty(detail.DetailedMessage))
            {
                _logService.Log(detail.LogLevel, detail.DetailedMessage); // Corrected Log call
                AddLogMessage(detail.DetailedMessage);
            }
            OnProgressChanged(detail);
        }

        /// <summary>
        /// Completes the current task.
        /// </summary>
        public void CompleteTask()
        {
            if (!_isTaskRunning)
            {
                return;
            }

            _currentProgress = 100;

            _isTaskRunning = false;
            _isIndeterminate = false;
            _queueTotal = 0;
            _queueCurrent = 0;
            _queueNextItemName = null;
            _skipNextRequested = false;

            _logService.Log(LogLevel.Info, $"Task completed: {_currentStatusText}"); // Corrected Log call
            AddLogMessage($"Task completed: {_currentStatusText}");

            OnProgressChanged(
                new TaskProgressDetail
                {
                    Progress = 100,
                    StatusText = _currentStatusText,
                    DetailedMessage = "Task completed",
                }
            );

            // Dispose cancellation token source
            _cancellationSource?.Dispose();
            _cancellationSource = null;
        }

        /// <summary>
        /// Adds a log message.
        /// </summary>
        /// <param name="message">The message content.</param>
        private void AddLogMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            _logMessages.Add(message);
        }

        /// <summary>
        /// Gets a snapshot of all terminal output lines accumulated during the current (or last) task.
        /// These are the raw output lines from winget/process stdout.
        /// </summary>
        public IReadOnlyList<string> GetTerminalOutputLines() => _terminalOutputLines.AsReadOnly();

        /// <summary>
        /// Cancels the current task.
        /// </summary>
        public void CancelCurrentTask()
        {
            if (_cancellationSource != null && !_cancellationSource.IsCancellationRequested)
            {
                _cancellationSource.Cancel();
                AddLogMessage("Task cancelled by user");
            }
        }

        /// <summary>
        /// Creates a progress reporter for detailed progress.
        /// </summary>
        /// <returns>The progress reporter.</returns>
        public IProgress<TaskProgressDetail> CreateDetailedProgress()
        {
            return new Progress<TaskProgressDetail>(UpdateDetailedProgress);
        }

        /// <summary>
        /// Creates a progress reporter for PowerShell progress.
        /// </summary>
        /// <returns>The progress reporter.</returns>
        public IProgress<TaskProgressDetail> CreatePowerShellProgress()
        {
            return new Progress<TaskProgressDetail>(UpdateDetailedProgress);
        }

        /// <summary>
        /// Starts a multi-script task with the specified script names.
        /// </summary>
        public CancellationTokenSource StartMultiScriptTask(string[] scriptNames)
        {
            CancelCurrentTask();

            if (scriptNames == null || scriptNames.Length == 0)
                throw new ArgumentException("At least one script name is required.", nameof(scriptNames));

            _cancellationSource = new CancellationTokenSource();
            _isTaskRunning = true;
            _activeScriptSlotCount = scriptNames.Length;
            _scriptSlotNames = scriptNames;
            _currentProgress = 0;
            _currentStatusText = string.Empty;
            _logMessages.Clear();
            _terminalOutputLines.Clear();
            _lastTerminalLineWasProgress = false;
            _queueTotal = 0;
            _queueCurrent = 0;
            _queueNextItemName = null;
            _skipNextRequested = false;

            _logService.Log(LogLevel.Info, $"[TASKPROGRESSSERVICE] Multi-script task started with {scriptNames.Length} slots");

            // Fire initial progress for each slot
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

        /// <summary>
        /// Creates a progress reporter for a specific script slot.
        /// Must be called on the UI thread so Progress&lt;T&gt; captures the SynchronizationContext.
        /// </summary>
        public IProgress<TaskProgressDetail> CreateScriptProgress(int slotIndex)
        {
            var slotCount = _activeScriptSlotCount;
            var slotName = _scriptSlotNames != null && slotIndex < _scriptSlotNames.Length
                ? _scriptSlotNames[slotIndex] : null;
            return new Progress<TaskProgressDetail>(detail =>
            {
                detail.ScriptSlotIndex = slotIndex;
                detail.ScriptSlotCount = slotCount;

                // Prefix terminal output with script name when multiple scripts run in parallel
                if (slotName != null && slotCount > 1 && !string.IsNullOrEmpty(detail.TerminalOutput))
                    detail.TerminalOutput = $"[{slotName}] {detail.TerminalOutput}";

                // Accumulate terminal output for the details dialog
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
                    _terminalOutputLines.Add(detail.TerminalOutput);
                    _lastTerminalLineWasProgress = detail.IsProgressIndicator;
                }

                // Fire directly without sticky queue logic
                ProgressUpdated?.Invoke(this, detail);
            });
        }

        /// <summary>
        /// Completes the multi-script task and resets slot state.
        /// </summary>
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

        /// <summary>
        /// Checks and clears the skip-next flag (atomic check-and-clear).
        /// </summary>
        /// <returns>True if a skip was requested since the last call.</returns>
        public bool ConsumeSkipNextRequest()
        {
            if (!_skipNextRequested) return false;
            _skipNextRequested = false;
            return true;
        }

        /// <summary>
        /// Raises the ProgressUpdated event, applying sticky queue state.
        /// Multi-script updates (ScriptSlotCount &gt; 0) bypass sticky queue logic.
        /// </summary>
        protected virtual void OnProgressChanged(TaskProgressDetail detail)
        {
            // Multi-script updates bypass sticky queue logic entirely
            if (detail.ScriptSlotCount > 0)
            {
                ProgressUpdated?.Invoke(this, detail);
                return;
            }

            // Update sticky queue state if incoming detail has queue info
            if (detail.QueueTotal > 0)
            {
                _queueTotal = detail.QueueTotal;
                _queueCurrent = detail.QueueCurrent;
                _queueNextItemName = detail.QueueNextItemName;
            }

            // Always populate queue info from sticky state if we're in a queue
            if (_queueTotal > 1)
            {
                detail.QueueTotal = _queueTotal;
                detail.QueueCurrent = _queueCurrent;
                detail.QueueNextItemName = _queueNextItemName;
            }

            ProgressUpdated?.Invoke(this, detail);
        }

        /// <summary>
        /// Returns true if the line is noise that doesn't add value
        /// (e.g. blank/whitespace-only lines).
        /// Spinner characters (-, \, |, /) are NOT filtered — they are
        /// delivered as IsProgressIndicator=true and animate in-place via
        /// the removal pattern in the live terminal dialog.
        /// </summary>
        private static bool IsTerminalNoise(string line)
        {
            var trimmed = line.Trim();
            return string.IsNullOrEmpty(trimmed);
        }

        /// <summary>
        /// Detects whether a line looks like a progress bar (contains Unicode block elements).
        /// Used to catch the duplicate first progress bar line that winget sometimes emits
        /// with \n before switching to \r.
        /// </summary>
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
}
