using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels;

public partial class AppItemViewModel : ObservableObject, ISelectable
{
    private readonly ItemDefinition _definition;
    private readonly IAppOperationService _appOperationService;
    private readonly IDialogService _dialogService;
    private readonly ILogService _logService;

    public AppItemViewModel(ItemDefinition definition, IAppOperationService appOperationService, IDialogService dialogService, ILogService logService)
    {
        _definition = definition;
        _appOperationService = appOperationService;
        _dialogService = dialogService;
        _logService = logService;

        InstallCommand = new AsyncRelayCommand(InstallAsync, () => !IsInstalling && !Definition.IsInstalled);
        UninstallCommand = new AsyncRelayCommand(UninstallAsync, () => !IsUninstalling && Definition.IsInstalled);
    }

    public ItemDefinition Definition => _definition;

    [ObservableProperty]
    private bool _isSelected;

    public string Name => Definition.Name;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private bool _isUninstalling;

    [ObservableProperty]
    private string _status = string.Empty;

    public string Description => Definition.Description;
    public string Category => Definition.Category;
    public string Id => Definition.Id;
    public bool IsInstalled
    {
        get => Definition.IsInstalled;
        set
        {
            if (Definition.IsInstalled != value)
            {
                Definition.IsInstalled = value;
                OnPropertyChanged();
            }
        }
    }
    public string Version => Definition.Version;
    public bool CanBeReinstalled => Definition.CanBeReinstalled;

    public IAsyncRelayCommand InstallCommand { get; }
    public IAsyncRelayCommand UninstallCommand { get; }

    private async Task InstallAsync()
    {
        var confirmed = await _dialogService.ShowConfirmationAsync(
            $"Are you sure you want to install {Name}?",
            "Confirm Installation");

        if (!confirmed) return;

        IsInstalling = true;
        Status = $"Installing {Name}...";

        try
        {
            var result = await _appOperationService.InstallAppAsync(Definition, CreateProgressReporter());

            if (result.Success)
            {
                Definition.IsInstalled = true;
                Status = "Installed";
            }
            else
            {
                Definition.LastOperationError = result.ErrorMessage;
                Status = "Install Failed";
            }
        }
        catch (Exception ex)
        {
            Definition.LastOperationError = ex.Message;
            Status = "Install Failed";
            _logService.LogError($"Install failed for {Name}", ex);
        }
        finally
        {
            IsInstalling = false;
            InstallCommand.NotifyCanExecuteChanged();
            UninstallCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task UninstallAsync()
    {
        var confirmed = await _dialogService.ShowConfirmationAsync(
            $"Are you sure you want to uninstall {Name}?",
            "Confirm Uninstall");

        if (!confirmed) return;

        IsUninstalling = true;
        Status = $"Uninstalling {Name}...";

        try
        {
            var result = await _appOperationService.UninstallAppAsync(Definition.Id, CreateProgressReporter());

            if (result.Success)
            {
                Definition.IsInstalled = false;
                Status = "Uninstalled";
            }
            else
            {
                Definition.LastOperationError = result.ErrorMessage;
                Status = "Uninstall Failed";
            }
        }
        catch (Exception ex)
        {
            Definition.LastOperationError = ex.Message;
            Status = "Uninstall Failed";
            _logService.LogError($"Uninstall failed for {Name}", ex);
        }
        finally
        {
            IsUninstalling = false;
            InstallCommand.NotifyCanExecuteChanged();
            UninstallCommand.NotifyCanExecuteChanged();
        }
    }

    private IProgress<TaskProgressDetail> CreateProgressReporter()
    {
        return new Progress<TaskProgressDetail>(detail =>
        {
            Status = detail.StatusText ?? Status;
        });
    }
}