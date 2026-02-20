using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.Models;

namespace Winhance.UI.Features.SoftwareApps.ViewModels;

/// <summary>
/// ViewModel for an individual app item in the software apps list.
/// </summary>
public partial class AppItemViewModel : ObservableObject, ISelectable
{
    private readonly ItemDefinition _definition;
    private readonly IAppOperationService _appOperationService;
    private readonly IDialogService _dialogService;
    private readonly ILogService _logService;
    private readonly ILocalizationService _localizationService;
    private readonly IDispatcherService _dispatcherService;

    public AppItemViewModel(
        ItemDefinition definition,
        IAppOperationService appOperationService,
        IDialogService dialogService,
        ILogService logService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService)
    {
        _definition = definition;
        _appOperationService = appOperationService;
        _dialogService = dialogService;
        _logService = logService;
        _localizationService = localizationService;
        _dispatcherService = dispatcherService;

        Status = string.Empty;

        InstallCommand = new AsyncRelayCommand(InstallAsync, () => !IsInstalling && !Definition.IsInstalled);
        UninstallCommand = new AsyncRelayCommand(UninstallAsync, () => !IsUninstalling && Definition.IsInstalled);
        OpenWebsiteCommand = new AsyncRelayCommand(OpenWebsiteAsync, () => !string.IsNullOrEmpty(Definition.WebsiteUrl));

        _localizationService.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(InstalledStatusText));
        OnPropertyChanged(nameof(ReinstallableStatusText));
    }

    public ItemDefinition Definition => _definition;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public string Name => Definition.Name;

    [ObservableProperty]
    public partial bool IsInstalling { get; set; }

    [ObservableProperty]
    public partial bool IsUninstalling { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; }

    public string Description => Definition.Description;
    public string GroupName => Definition.GroupName ?? string.Empty;
    public string Id => Definition.Id;

    public bool IsInstalled
    {
        get => Definition.IsInstalled;
        set
        {
            if (Definition.IsInstalled != value)
            {
                Definition.IsInstalled = value;
                _dispatcherService.RunOnUIThread(() =>
                {
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(InstalledStatusText));
                });
            }
        }
    }

    public string Version => Definition.Version;
    public bool CanBeReinstalled => Definition.CanBeReinstalled;

    public string InstalledStatusText => _localizationService.GetString(
        IsInstalled ? "Status_Installed" : "Status_NotInstalled");

    public string ReinstallableStatusText => _localizationService.GetString(
        CanBeReinstalled ? "Status_CanReinstall" : "Status_CannotReinstall");

    public string ItemTypeDescription
    {
        get
        {
            if (!string.IsNullOrEmpty(Definition.CapabilityName))
                return "Legacy Capability";

            if (!string.IsNullOrEmpty(Definition.OptionalFeatureName))
                return "Optional Feature";

            if (!string.IsNullOrEmpty(Definition.AppxPackageName))
                return "AppX Package";

            return string.Empty;
        }
    }

    public string PackageName => Definition.AppxPackageName
        ?? (Definition.WinGetPackageId?.FirstOrDefault())
        ?? Definition.CapabilityName
        ?? Definition.OptionalFeatureName
        ?? string.Empty;

    public string? WebsiteUrl => Definition.WebsiteUrl;

    public IAsyncRelayCommand InstallCommand { get; }
    public IAsyncRelayCommand UninstallCommand { get; }
    public IAsyncRelayCommand OpenWebsiteCommand { get; }

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
                IsInstalled = true;
                Status = "Installed";
            }
            else
            {
                if (result.ErrorMessage?.Contains("cancelled", StringComparison.OrdinalIgnoreCase) == true)
                {
                    Status = "Installation Cancelled";
                }
                else
                {
                    Definition.LastOperationError = result.ErrorMessage;
                    Status = "Install Failed";
                }
            }
        }
        catch (OperationCanceledException)
        {
            Status = "Installation Cancelled";
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
                IsInstalled = false;
                Status = "Uninstalled";
            }
            else
            {
                if (result.ErrorMessage?.Contains("cancelled", StringComparison.OrdinalIgnoreCase) == true)
                {
                    Status = "Uninstall Cancelled";
                }
                else
                {
                    Definition.LastOperationError = result.ErrorMessage;
                    Status = "Uninstall Failed";
                }
            }
        }
        catch (OperationCanceledException)
        {
            Status = "Uninstall Cancelled";
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

    private async Task OpenWebsiteAsync()
    {
        if (string.IsNullOrEmpty(Definition.WebsiteUrl))
            return;

        try
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri(Definition.WebsiteUrl));
            _logService.LogInformation($"Opened website for {Name}: {Definition.WebsiteUrl}");
        }
        catch (Exception ex)
        {
            _logService.LogError($"Failed to open website for {Name}: {ex.Message}", ex);
        }
    }
}
