using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.SoftwareApps.ViewModels;

public class RemovalStatusViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IScheduledTaskService _scheduledTaskService;
    private readonly ILogService _logService;
    private readonly IFileSystemService _fileSystemService;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _isActive;
    private bool _isLoading;
    private bool _disposed;

    public RemovalStatusViewModel(string name, string iconPath, string activeColor, string scriptFileName,
        string scheduledTaskName, IScheduledTaskService scheduledTaskService, ILogService logService, IFileSystemService fileSystemService)
    {
        _scheduledTaskService = scheduledTaskService;
        _logService = logService;
        _fileSystemService = fileSystemService;

        Name = name;
        IconPath = iconPath;
        ActiveColor = activeColor;
        ScriptFileName = scriptFileName;
        ScheduledTaskName = scheduledTaskName;

        RemoveCommand = new AsyncRelayCommand(RemoveAsync);
    }

    public string Name { get; }
    public string IconPath { get; }
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
                    var scriptPath = _fileSystemService.CombinePath(ScriptPaths.ScriptsDirectory, ScriptFileName);
                    return _fileSystemService.FileExists(scriptPath);
                },
                cancellationToken
            );

            var taskTask = _scheduledTaskService.IsTaskRegisteredAsync(ScheduledTaskName);

            var minDelayTask = Task.Delay(500, cancellationToken);

            await Task.WhenAll(scriptTask, taskTask, minDelayTask).ConfigureAwait(false);

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
        IsLoading = true;

        try
        {
            try
            {
                var isRegistered = await _scheduledTaskService.IsTaskRegisteredAsync(ScheduledTaskName);
                if (isRegistered)
                {
                    var unregisterResult = await _scheduledTaskService.UnregisterScheduledTaskAsync(ScheduledTaskName);
                    if (unregisterResult.Success)
                    {
                        _logService.LogInformation($"Unregistered scheduled task: {ScheduledTaskName}");
                    }
                    else
                    {
                        _logService.LogError($"Failed to unregister scheduled task: {ScheduledTaskName}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error removing scheduled task {ScheduledTaskName}: {ex.Message}", ex);
            }

            try
            {
                var scriptPath = _fileSystemService.CombinePath(ScriptPaths.ScriptsDirectory, ScriptFileName);
                if (_fileSystemService.FileExists(scriptPath))
                {
                    _fileSystemService.DeleteFile(scriptPath);
                    _logService.LogInformation($"Deleted script file: {scriptPath}");
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error removing script file {ScriptFileName}: {ex.Message}", ex);
            }

            await CheckStatusAsync();
        }
        catch (Exception ex)
        {
            _logService.LogError($"Unexpected error during {Name} removal: {ex.Message}", ex);
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
