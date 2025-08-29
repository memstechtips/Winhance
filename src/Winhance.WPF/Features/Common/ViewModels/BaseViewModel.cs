using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Common.ViewModels
{
    /// <summary>
    /// Base view model class that implements INotifyPropertyChanged and provides common functionality.
    /// </summary>
    public abstract class BaseViewModel : ObservableObject, IDisposable, IViewModel
    {
        private readonly ITaskProgressService _progressService;
        private readonly ILogService _logService;
        private readonly IEventBus _eventBus;
        private bool _isDisposed;
        private bool _isLoading;
        private string _statusText = string.Empty;
        private double _currentProgress;
        private bool _isIndeterminate;
        private ObservableCollection<LogMessageViewModel> _logMessages =
            new ObservableCollection<LogMessageViewModel>();
        private bool _areDetailsExpanded;
        private bool _canCancelTask;
        private bool _isTaskRunning;
        private ICommand _cancelCommand;

        /// <summary>
        /// Gets or sets whether the view model is loading.
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            protected set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Gets the command to cancel the current task.
        /// </summary>
        public ICommand CancelCommand =>
            _cancelCommand ??= new RelayCommand(CancelCurrentTask, () => CanCancelTask);

        /// <summary>
        /// Gets or sets the status text.
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            protected set => SetProperty(ref _statusText, value);
        }

        /// <summary>
        /// Gets or sets the current progress value (0-100).
        /// </summary>
        public double CurrentProgress
        {
            get => _currentProgress;
            protected set => SetProperty(ref _currentProgress, value);
        }

        /// <summary>
        /// Gets or sets whether the progress is indeterminate.
        /// </summary>
        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            protected set => SetProperty(ref _isIndeterminate, value);
        }

        /// <summary>
        /// Gets the collection of log messages.
        /// </summary>
        public ObservableCollection<LogMessageViewModel> LogMessages => _logMessages;

        /// <summary>
        /// Gets or sets whether the details are expanded.
        /// </summary>
        public bool AreDetailsExpanded
        {
            get => _areDetailsExpanded;
            set => SetProperty(ref _areDetailsExpanded, value);
        }

        /// <summary>
        /// Gets or sets whether the task can be cancelled.
        /// </summary>
        public bool CanCancelTask
        {
            get => _canCancelTask;
            protected set => SetProperty(ref _canCancelTask, value);
        }

        /// <summary>
        /// Gets or sets whether a task is running.
        /// </summary>
        public bool IsTaskRunning
        {
            get => _isTaskRunning;
            protected set => SetProperty(ref _isTaskRunning, value);
        }

        /// <summary>
        /// Gets the command to cancel the current task.
        /// </summary>
        public ICommand CancelTaskCommand { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseViewModel"/> class.
        /// </summary>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="eventBus">The event bus.</param>
        protected BaseViewModel(
            ITaskProgressService progressService,
            ILogService logService,
            IEventBus eventBus
        )
        {
            _progressService =
                progressService ?? throw new ArgumentNullException(nameof(progressService));
            _logService = logService;
            _eventBus = eventBus;

            // Subscribe to progress service events
            _progressService.ProgressUpdated += ProgressService_ProgressUpdated;
            _progressService.LogMessageAdded += ProgressService_LogMessageAdded;

            // Initialize commands
            CancelTaskCommand = new RelayCommand(CancelCurrentTask, () => CanCancelTask);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseViewModel"/> class.
        /// This constructor is for backward compatibility.
        /// </summary>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="eventBus">The event bus.</param>
        protected BaseViewModel(ITaskProgressService progressService, IEventBus eventBus)
            : this(progressService, null, eventBus)
        {
            // This constructor is for backward compatibility
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseViewModel"/> class.
        /// This constructor is for backward compatibility.
        /// </summary>
        /// <param name="progressService">The task progress service.</param>
        protected BaseViewModel(ITaskProgressService progressService)
            : this(progressService, null, null)
        {
            // This constructor is for backward compatibility
        }

        /// <summary>
        /// Disposes of the resources used by the ViewModel
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of the resources used by the ViewModel
        /// </summary>
        /// <param name="disposing">Whether to dispose of managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Unsubscribe from events
                    _progressService.ProgressUpdated -= ProgressService_ProgressUpdated;
                    _progressService.LogMessageAdded -= ProgressService_LogMessageAdded;

                    // No need to unregister from event bus as it's handled by subscription tokens
                }

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Finalizer to ensure resources are released
        /// </summary>
        ~BaseViewModel()
        {
            Dispose(false);
        }

        /// <summary>
        /// Called when the view model is navigated to
        /// </summary>
        /// <param name="parameter">Navigation parameter</param>
        public virtual void OnNavigatedTo(object? parameter = null)
        {
            // Base implementation does nothing
            // Derived classes should override if they need to handle parameters
        }

        /// <summary>
        /// Handles the ProgressUpdated event of the progress service.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="detail">The progress detail.</param>
        private void ProgressService_ProgressUpdated(
            object? sender,
            Winhance.Core.Features.Common.Models.TaskProgressDetail detail
        )
        {
            // Update local properties
            IsLoading = _progressService.IsTaskRunning;
            StatusText = detail.StatusText ?? string.Empty;
            CurrentProgress = detail.Progress ?? 0;
            IsIndeterminate = detail.IsIndeterminate;
            IsTaskRunning = _progressService.IsTaskRunning;
            CanCancelTask =
                _progressService.IsTaskRunning
                && _progressService.CurrentTaskCancellationSource != null;

            // Publish event for UI updates that may occur in other threads
            if (_eventBus != null)
            {
                _eventBus.Publish(new TaskProgressEvent
                {
                    Progress = detail.Progress ?? 0,
                    StatusText = detail.StatusText ?? string.Empty,
                    IsIndeterminate = detail.IsIndeterminate,
                    IsTaskRunning = _progressService.IsTaskRunning,
                    CanCancel =
                        _progressService.IsTaskRunning
                        && _progressService.CurrentTaskCancellationSource != null,
                });
            }
        }

        /// <summary>
        /// Handles the LogMessageAdded event of the progress service.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="message">The log message.</param>
        private void ProgressService_LogMessageAdded(object? sender, string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            // Add to local collection
            var logMessageViewModel = new LogMessageViewModel
            {
                Message = message,
                Level = LogLevel.Info, // Default to Info since we don't have level information
                Timestamp = DateTime.Now,
            };

            LogMessages.Add(logMessageViewModel);

            // Auto-expand details on error or warning (we can't determine this from the string message)

            // Publish event for UI updates that may occur in other threads
            if (_eventBus != null)
            {
                _eventBus.Publish(new LogEvent
                {
                    Message = message,
                    Level = LogLevel.Info, // Default to Info since we don't have level information
                    Exception = null,
                });
            }
            ;
        }

        /// <summary>
        /// Cancels the current task.
        /// </summary>
        protected void CancelCurrentTask()
        {
            if (_progressService.CurrentTaskCancellationSource != null && CanCancelTask)
            {
                _logService.LogInformation("User requested task cancellation");
                _progressService.CancelCurrentTask();
            }
        }

        /// <summary>
        /// Executes an operation with progress reporting.
        /// </summary>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="taskName">The name of the task.</param>
        /// <param name="isIndeterminate">Whether the progress is indeterminate.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected async Task ExecuteWithProgressAsync(
            Func<ITaskProgressService, Task> operation,
            string taskName,
            bool isIndeterminate = false
        )
        {
            try
            {
                _progressService.StartTask(taskName, isIndeterminate);
                await operation(_progressService);
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error in {taskName}: {ex.Message}", ex);
                _progressService.AddLogMessage($"Error: {ex.Message}");
                throw;
            }
            finally
            {
                if (_progressService.IsTaskRunning)
                {
                    _progressService.CompleteTask();
                }
            }
        }

        /// <summary>
        /// Executes an operation with progress reporting and returns a result.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="taskName">The name of the task.</param>
        /// <param name="isIndeterminate">Whether the progress is indeterminate.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected async Task<T> ExecuteWithProgressAsync<T>(
            Func<ITaskProgressService, Task<T>> operation,
            string taskName,
            bool isIndeterminate = false
        )
        {
            try
            {
                _progressService.StartTask(taskName, isIndeterminate);
                return await operation(_progressService);
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error in {taskName}: {ex.Message}", ex);
                _progressService.AddLogMessage($"Error: {ex.Message}");
                throw;
            }
            finally
            {
                if (_progressService.IsTaskRunning)
                {
                    _progressService.CompleteTask();
                }
            }
        }

        /// <summary>
        /// Executes an operation with detailed progress reporting.
        /// </summary>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="taskName">The name of the task.</param>
        /// <param name="isIndeterminate">Whether the progress is indeterminate.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected async Task ExecuteWithProgressAsync(
            Func<
                IProgress<Winhance.Core.Features.Common.Models.TaskProgressDetail>,
                CancellationToken,
                Task
            > operation,
            string taskName,
            bool isIndeterminate = false
        )
        {
            try
            {
                // Clear previous log messages
                LogMessages.Clear();

                _progressService.StartTask(taskName, isIndeterminate);
                var progress = _progressService.CreateDetailedProgress();
                var cancellationToken = _progressService.CurrentTaskCancellationSource.Token;

                await operation(progress, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _progressService.AddLogMessage("Operation cancelled by user");
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error in {taskName}: {ex.Message}", ex);
                _progressService.AddLogMessage($"Error: {ex.Message}");
                throw;
            }
            finally
            {
                if (_progressService.IsTaskRunning)
                {
                    _progressService.CompleteTask();
                }
            }
        }

        /// <summary>
        /// Executes an operation with detailed progress reporting and returns a result.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="taskName">The name of the task.</param>
        /// <param name="isIndeterminate">Whether the progress is indeterminate.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected async Task<T> ExecuteWithProgressAsync<T>(
            Func<
                IProgress<Winhance.Core.Features.Common.Models.TaskProgressDetail>,
                CancellationToken,
                Task<T>
            > operation,
            string taskName,
            bool isIndeterminate = false
        )
        {
            try
            {
                // Clear previous log messages
                LogMessages.Clear();

                _progressService.StartTask(taskName, isIndeterminate);
                var progress = _progressService.CreateDetailedProgress();
                var cancellationToken = _progressService.CurrentTaskCancellationSource.Token;

                return await operation(progress, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _progressService.AddLogMessage("Operation cancelled by user");
                throw;
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error in {taskName}: {ex.Message}", ex);
                _progressService.AddLogMessage($"Error: {ex.Message}");
                throw;
            }
            finally
            {
                if (_progressService.IsTaskRunning)
                {
                    _progressService.CompleteTask();
                }
            }
        }

        /// <summary>
        /// Gets the progress service.
        /// </summary>
        protected ITaskProgressService ProgressService => _progressService;

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        protected void LogInfo(string message)
        {
            _logService?.LogInformation(message);
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The exception associated with the error, if any.</param>
        protected void LogError(string message, Exception? exception = null)
        {
            _logService?.LogError(message, exception);
        }
    }
}