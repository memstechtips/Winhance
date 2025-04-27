using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;

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
        private CancellationTokenSource _cancellationSource;

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
        /// Event raised when progress changes (compatibility with App.xaml.cs).
        /// </summary>
        public event EventHandler<TaskProgressEventArgs>? ProgressChanged;

        /// <summary>
        /// Event raised when a log message is added.
        /// </summary>
        public event EventHandler<string>? LogMessageAdded;

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

            _logService.Log(LogLevel.Info, $"Task started: {taskName}"); // Corrected Log call
            AddLogMessage($"Task started: {taskName}");
            OnProgressChanged(new TaskProgressDetail
            {
                Progress = 0,
                StatusText = taskName,
                IsIndeterminate = isIndeterminate
            });
            
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
                Debug.WriteLine("Warning: Attempting to update progress when no task is running.");
                return;
            }

            if (progressPercentage < 0 || progressPercentage > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(progressPercentage), "Progress must be between 0 and 100.");
            }

            _currentProgress = progressPercentage;
            if (!string.IsNullOrEmpty(statusText))
            {
                _currentStatusText = statusText;
                _logService.Log(LogLevel.Info, $"Task progress ({progressPercentage}%): {statusText}"); // Corrected Log call
                AddLogMessage($"Task progress ({progressPercentage}%): {statusText}");
            }
            else
            {
                _logService.Log(LogLevel.Info, $"Task progress: {progressPercentage}%"); // Corrected Log call
                AddLogMessage($"Task progress: {progressPercentage}%");
            }
            OnProgressChanged(new TaskProgressDetail
            {
                Progress = progressPercentage,
                StatusText = _currentStatusText
            });
        }

        /// <summary>
        /// Updates the progress with detailed information.
        /// </summary>
        /// <param name="detail">The detailed progress information.</param>
        public void UpdateDetailedProgress(TaskProgressDetail detail)
        {
            if (!_isTaskRunning)
            {
                Debug.WriteLine("Warning: Attempting to update detailed progress when no task is running.");
                return;
            }
            
            if (detail.Progress.HasValue)
            {
                if (detail.Progress.Value < 0 || detail.Progress.Value > 100)
                {
                    throw new ArgumentOutOfRangeException(nameof(detail.Progress), "Progress must be between 0 and 100.");
                }
                
                _currentProgress = (int)detail.Progress.Value;
            }
            
            if (!string.IsNullOrEmpty(detail.StatusText))
            {
                _currentStatusText = detail.StatusText;
            }
            
            _isIndeterminate = detail.IsIndeterminate;
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
                Debug.WriteLine("Warning: Attempting to complete a task when no task is running.");
                return;
            }

            _currentProgress = 100;

            _isTaskRunning = false;
            _isIndeterminate = false;

            _logService.Log(LogLevel.Info, $"Task completed: {_currentStatusText}"); // Corrected Log call
            AddLogMessage($"Task completed: {_currentStatusText}");

            OnProgressChanged(new TaskProgressDetail
            {
                Progress = 100,
                StatusText = _currentStatusText,
                DetailedMessage = "Task completed"
            });
            
            // Dispose cancellation token source
            _cancellationSource?.Dispose();
            _cancellationSource = null;
        }

        /// <summary>
        /// Adds a log message.
        /// </summary>
        /// <param name="message">The message content.</param>
        public void AddLogMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            
            _logMessages.Add(message);
            LogMessageAdded?.Invoke(this, message);
        }

        /// <summary>
        /// Cancels the current task.
        /// </summary>
        public void CancelCurrentTask()
        {
            if (_cancellationSource != null && !_cancellationSource.IsCancellationRequested)
            {
                _cancellationSource.Cancel();
                AddLogMessage("Task cancelled by user");
                
                // Don't dispose here, as the task might still be using it
                // It will be disposed in CompleteTask or when a new task starts
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
        /// Creates a progress adapter for PowerShell progress data.
        /// </summary>
        /// <returns>A progress adapter for PowerShell progress data.</returns>
        public IProgress<PowerShellProgressData> CreatePowerShellProgressAdapter()
        {
            return new Progress<PowerShellProgressData>(data => {
                var detail = new TaskProgressDetail();
                
                // Map PowerShell progress data to task progress detail
                if (data.PercentComplete.HasValue)
                {
                    detail.Progress = data.PercentComplete.Value;
                }
                
                if (!string.IsNullOrEmpty(data.Activity))
                {
                    detail.StatusText = data.Activity;
                    if (!string.IsNullOrEmpty(data.StatusDescription))
                    {
                        detail.StatusText += $": {data.StatusDescription}";
                    }
                }
                
                detail.DetailedMessage = data.Message ?? data.CurrentOperation;
                
                // Map stream type to log level
                switch (data.StreamType)
                {
                    case PowerShellStreamType.Error:
                        detail.LogLevel = LogLevel.Error;
                        break;
                    case PowerShellStreamType.Warning:
                        detail.LogLevel = LogLevel.Warning;
                        break;
                    case PowerShellStreamType.Verbose:
                    case PowerShellStreamType.Debug:
                        detail.LogLevel = LogLevel.Debug;
                        break;
                    default:
                        detail.LogLevel = LogLevel.Info;
                        break;
                }
                
                UpdateDetailedProgress(detail);
            });
        }

        /// <summary>
        /// Raises the ProgressUpdated event.
        /// </summary>
        protected virtual void OnProgressChanged(TaskProgressDetail detail)
        {
            ProgressUpdated?.Invoke(this, detail);
            ProgressChanged?.Invoke(this, TaskProgressEventArgs.FromTaskProgressDetail(detail, _isTaskRunning));
        }
    }
}
