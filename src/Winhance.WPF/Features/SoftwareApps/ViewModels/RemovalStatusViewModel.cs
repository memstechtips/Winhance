using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Views;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels;

public class RemovalStatusViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IScheduledTaskService _scheduledTaskService;
    private readonly ILogService _logService;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _isActive;
    private bool _isLoading;
    private bool _disposed;

    public RemovalStatusViewModel(string name, string iconKind, string activeColor, string scriptFileName,
        string scheduledTaskName, IScheduledTaskService scheduledTaskService, ILogService logService)
    {
        _scheduledTaskService = scheduledTaskService;
        _logService = logService;

        Name = name;
        IconKind = iconKind;
        ActiveColor = activeColor;
        ScriptFileName = scriptFileName;
        ScheduledTaskName = scheduledTaskName;

        RemoveCommand = new AsyncRelayCommand(RemoveAsync);
    }

    public string Name { get; }
    public string IconKind { get; }
    public string ActiveColor { get; }
    public string ScriptFileName { get; }
    public string ScheduledTaskName { get; }

    public ICommand RemoveCommand { get; }

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

    public async Task StartStatusMonitoringAsync()
    {
        if (!_disposed && !_cancellationTokenSource.Token.IsCancellationRequested)
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
            var scriptTask = Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var scriptPath = Path.Combine(ScriptPaths.ScriptsDirectory, ScriptFileName);
                    return File.Exists(scriptPath);
                },
                cancellationToken
            );

            var taskTask = _scheduledTaskService.IsTaskRegisteredAsync(ScheduledTaskName);

            var minDelayTask = Task.Delay(500, cancellationToken);

            await Task.WhenAll(scriptTask, taskTask, minDelayTask);

            IsActive = await scriptTask || await taskTask;
        }
        catch (OperationCanceledException)
        {
            IsActive = false;
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error checking removal status for {Name}: {ex.Message}", ex);
            IsActive = false;
        }
        finally
        {
            if (!_disposed)
                IsLoading = false;
        }
    }

    private async Task RemoveAsync()
    {
        var confirmTitle = $"Remove {Name} Script and Task";
        var confirmMessage = $"Are you sure you want to remove the {Name} script and scheduled task? This action cannot be undone.";

        var dialogResult = CustomDialog.ShowConfirmation(confirmTitle, confirmTitle, confirmMessage, "");

        if (dialogResult != true)
            return;

        IsLoading = true;

        try
        {
            var errors = new List<string>();
            var success = true;

            try
            {
                var isRegistered = await _scheduledTaskService.IsTaskRegisteredAsync(ScheduledTaskName);
                if (isRegistered)
                {
                    var unregistered = await _scheduledTaskService.UnregisterScheduledTaskAsync(ScheduledTaskName);
                    if (unregistered)
                    {
                        _logService.LogInformation($"Unregistered scheduled task: {ScheduledTaskName}");
                    }
                    else
                    {
                        errors.Add($"Failed to unregister scheduled task: {ScheduledTaskName}");
                        success = false;
                    }
                }
            }
            catch (System.Exception ex)
            {
                errors.Add($"Error removing scheduled task: {ex.Message}");
                success = false;
            }

            try
            {
                var scriptPath = Path.Combine(ScriptPaths.ScriptsDirectory, ScriptFileName);
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
                _logService.LogInformation($"Successfully removed {Name} script and scheduled task");
                await CheckStatusAsync();
            }
            else
            {
                var errorMessage = string.Join("\n", errors);
                CustomDialog.ShowInformation("Removal Errors", "Removal Errors", $"Some errors occurred during removal:\n\n{errorMessage}", "");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Unexpected error during {Name} removal: {ex.Message}", ex);
            CustomDialog.ShowInformation("Unexpected Error", "Unexpected Error", $"An unexpected error occurred: {ex.Message}", "");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }
}