using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Views;
using Winhance.WPF.Features.SoftwareApps.Services;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels
{
    /// <summary>
    /// ViewModel for individual removal status items in the help content
    /// </summary>
    public class RemovalStatusViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IScriptPathDetectionService _scriptPathDetectionService;
        private readonly IScheduledTaskService _scheduledTaskService;
        private readonly ILogService _logService;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _isActive;
        private bool _isLoading;
        private bool _disposed;

        public RemovalStatusViewModel(
            string name,
            string iconKind,
            string activeColor,
            string scriptFileName,
            string scheduledTaskName,
            IScriptPathDetectionService scriptPathDetectionService,
            IScheduledTaskService scheduledTaskService,
            ILogService logService
        )
        {
            Name = name;
            IconKind = iconKind;
            ActiveColor = activeColor;
            ScriptFileName = scriptFileName;
            ScheduledTaskName = scheduledTaskName;
            _scriptPathDetectionService = scriptPathDetectionService;
            _scheduledTaskService = scheduledTaskService;
            _logService = logService;

            _cancellationTokenSource = new CancellationTokenSource();
            RemoveCommand = new AsyncRelayCommand(RemoveAsync);

            // Don't start status check automatically - will be started when needed
            // This prevents background tasks from running when Help dialog is closed
        }

        public string Name { get; }
        public string IconKind { get; }
        public string ActiveColor { get; }
        public string ScriptFileName { get; }
        public string ScheduledTaskName { get; }

        public bool IsActive
        {
            get => _isActive;
            private set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand RemoveCommand { get; }

        /// <summary>
        /// Refreshes the status of this removal item
        /// </summary>
        public async Task RefreshStatusAsync()
        {
            if (!_disposed)
            {
                await CheckStatusAsync(_cancellationTokenSource.Token);
            }
        }

        private async Task CheckStatusAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed || cancellationToken.IsCancellationRequested)
                return;

            IsLoading = true;
            try
            {
                // Run script and task checks in parallel for speed
                var scriptTask = Task.Run(
                    () =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var scriptPath = Path.Combine(_scriptPathDetectionService.GetScriptsDirectory(), ScriptFileName);
                        return File.Exists(scriptPath);
                    },
                    cancellationToken
                );

                var taskTask = _scheduledTaskService.IsTaskRegisteredAsync(ScheduledTaskName);

                // Wait for both checks to complete
                // Add minimum delay to make loading animation visible
                var minDelayTask = Task.Delay(500, cancellationToken); // 500ms minimum
                await Task.WhenAll(scriptTask, taskTask, minDelayTask);

                var scriptExists = scriptTask.Result;
                var taskExists = taskTask.Result;

                // Active if both script and task exist
                IsActive = scriptExists && taskExists;

                _logService.LogInformation(
                                    $"[{Name}] Status: Script={scriptExists}, Task={taskExists}, Active={IsActive}"
                                );
            }
            catch (System.Exception ex)
            {
                _logService.LogError($"Error checking status for {Name}", ex);
                IsActive = false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RemoveAsync()
        {
            try
            {
                if (!IsActive)
                    return;

                var confirmed = CustomDialog.ShowConfirmation(
                                    "Confirm Removal",
                                    $"Are you sure you want to delete the {Name} removal script and its scheduled task?",
                                    "This will stop the automatic removal of this application/feature.",
                                    ""
                                );

                if (confirmed != true)
                    return;

                bool success = true;
                var errors = new System.Collections.Generic.List<string>();

                // Remove scheduled task
                try
                {
                    var taskRemoved = await _scheduledTaskService.UnregisterScheduledTaskAsync(
                                            ScheduledTaskName
                                        );
                    if (!taskRemoved)
                    {
                        errors.Add($"Failed to remove scheduled task: {ScheduledTaskName}");
                        success = false;
                    }
                }
                catch (System.Exception ex)
                {
                    errors.Add($"Error removing scheduled task: {ex.Message}");
                    success = false;
                }

                // Remove script file
                try
                {
                    var scriptPath = Path.Combine(_scriptPathDetectionService.GetScriptsDirectory(), ScriptFileName);
                    if (File.Exists(scriptPath))
                    {
                        File.Delete(scriptPath);
                        _logService.LogInformation($"Deleted script file: {scriptPath}");
                    }
                }
                catch (System.Exception ex)
                {
                    errors.Add($"Error removing script file: {ex.Message}");
                    success = false;
                }

                if (success)
                {
                    CustomDialog.ShowInformation(
                        "Removal Complete",
                        $"Successfully removed {Name} removal script and scheduled task.",
                        "",
                        "");

                    // Refresh status to reflect changes
                    await RefreshStatusAsync();
                }
                else
                {
                    var errorMessage =
                                            $"Some errors occurred while removing {Name}:\n\n"
                                            + string.Join("\n", errors);
                    CustomDialog.ShowInformation("Removal Errors", errorMessage, "", "");
                }
            }
            catch (System.Exception ex)
            {
                _logService.LogError($"Error in RemoveAsync for {Name}", ex);
                CustomDialog.ShowInformation(
                                    "Error",
                                    $"An unexpected error occurred while removing {Name}: {ex.Message}",
                                    "",
                                    ""
                                );
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _disposed = true;
            }
        }
    }
}
