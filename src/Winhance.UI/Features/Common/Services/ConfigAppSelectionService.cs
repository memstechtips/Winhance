using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.SoftwareApps.ViewModels;

namespace Winhance.UI.Features.Common.Services;

public class ConfigAppSelectionService : IConfigAppSelectionService
{
    private readonly ILogService _logService;
    private readonly WindowsAppsViewModel _windowsAppsVM;
    private readonly ExternalAppsViewModel _externalAppsVM;

    public ConfigAppSelectionService(
        ILogService logService,
        WindowsAppsViewModel windowsAppsVM,
        ExternalAppsViewModel externalAppsVM)
    {
        _logService = logService;
        _windowsAppsVM = windowsAppsVM;
        _externalAppsVM = externalAppsVM;
    }

    public async Task SelectWindowsAppsFromConfigAsync(ConfigSection windowsAppsSection)
    {
        if (!_windowsAppsVM.IsInitialized)
            await _windowsAppsVM.LoadItemsAsync();

        foreach (var vmItem in _windowsAppsVM.Items)
            vmItem.IsSelected = false;

        if (windowsAppsSection?.Items != null)
        {
            foreach (var configItem in windowsAppsSection.Items)
            {
                var vmItem = FindMatchingWindowsApp(_windowsAppsVM.Items, configItem);
                if (vmItem != null)
                    vmItem.IsSelected = configItem.IsSelected ?? true;
            }
        }

        var selectedCount = _windowsAppsVM.Items.Count(i => i.IsSelected);
        _logService.Log(LogLevel.Info, $"Selected {selectedCount} Windows Apps from config");
    }

    public async Task<(bool shouldContinue, bool saveScripts)> ConfirmWindowsAppsRemovalAsync()
    {
        var selectedCount = _windowsAppsVM.Items.Count(i => i.IsSelected);
        if (selectedCount == 0) return (true, true);

        var (confirmed, saveScripts) = await _windowsAppsVM.ShowRemovalSummaryAndConfirm();
        return (confirmed, saveScripts);
    }

    public Task ClearWindowsAppsSelectionAsync()
    {
        foreach (var vmItem in _windowsAppsVM.Items)
            vmItem.IsSelected = false;

        return Task.CompletedTask;
    }

    public async Task SelectExternalAppsFromConfigAsync(ConfigSection externalAppsSection)
    {
        if (!_externalAppsVM.IsInitialized)
            await _externalAppsVM.LoadItemsAsync();

        foreach (var vmItem in _externalAppsVM.Items)
            vmItem.IsSelected = false;

        if (externalAppsSection?.Items != null)
        {
            foreach (var configItem in externalAppsSection.Items)
            {
                var vmItem = FindMatchingExternalApp(_externalAppsVM.Items, configItem);
                if (vmItem != null)
                    vmItem.IsSelected = true;
            }
        }

        var selectedCount = _externalAppsVM.Items.Count(i => i.IsSelected);
        _logService.Log(LogLevel.Info, $"Selected {selectedCount} External Apps from config");
    }

    public async Task ProcessExternalAppsInstallationAsync(ConfigSection externalAppsSection)
    {
        if (!_externalAppsVM.IsInitialized)
            await _externalAppsVM.LoadItemsAsync();

        foreach (var vmItem in _externalAppsVM.Items)
            vmItem.IsSelected = false;

        if (externalAppsSection?.Items != null)
        {
            foreach (var configItem in externalAppsSection.Items)
            {
                var vmItem = FindMatchingExternalApp(_externalAppsVM.Items, configItem);
                if (vmItem != null)
                    vmItem.IsSelected = true;
            }
        }

        var selectedCount = _externalAppsVM.Items.Count(i => i.IsSelected);
        if (selectedCount > 0)
        {
            _logService.Log(LogLevel.Info, "Starting external apps installation in background");
            await _externalAppsVM.InstallApps(skipConfirmation: true);
        }
    }

    public async Task ProcessExternalAppsRemovalAsync(ConfigSection externalAppsSection)
    {
        if (!_externalAppsVM.IsInitialized)
            await _externalAppsVM.LoadItemsAsync();

        foreach (var vmItem in _externalAppsVM.Items)
            vmItem.IsSelected = false;

        if (externalAppsSection?.Items != null)
        {
            foreach (var configItem in externalAppsSection.Items)
            {
                var vmItem = FindMatchingExternalApp(_externalAppsVM.Items, configItem);
                if (vmItem != null)
                    vmItem.IsSelected = true;
            }
        }

        var selectedCount = _externalAppsVM.Items.Count(i => i.IsSelected);
        if (selectedCount > 0)
        {
            _logService.Log(LogLevel.Info, "Starting external apps uninstallation");
            await _externalAppsVM.UninstallAppsAsync();
        }
    }

    public async Task ProcessExternalAppsFromUserSelectionAsync(List<string> selectedAppIds)
    {
        if (!_externalAppsVM.IsInitialized)
            await _externalAppsVM.LoadItemsAsync();

        // Set VM selections to match captured user choices
        foreach (var vmItem in _externalAppsVM.Items)
            vmItem.IsSelected = selectedAppIds.Contains(vmItem.Id ?? vmItem.Name);

        var selectedCount = _externalAppsVM.Items.Count(i => i.IsSelected);
        if (selectedCount > 0)
        {
            _logService.Log(LogLevel.Info, $"Starting external apps installation for {selectedCount} user-selected apps");
            await _externalAppsVM.InstallApps(skipConfirmation: true);
        }
    }

    private static AppItemViewModel? FindMatchingWindowsApp(IEnumerable<AppItemViewModel> vmItems, ConfigurationItem configItem)
    {
        return vmItems.FirstOrDefault(i =>
            (!string.IsNullOrEmpty(configItem.AppxPackageName) && i.Definition?.AppxPackageName == configItem.AppxPackageName) ||
            (!string.IsNullOrEmpty(configItem.CapabilityName) && i.Definition?.CapabilityName == configItem.CapabilityName) ||
            (!string.IsNullOrEmpty(configItem.OptionalFeatureName) && i.Definition?.OptionalFeatureName == configItem.OptionalFeatureName) ||
            i.Id == configItem.Id);
    }

    private static AppItemViewModel? FindMatchingExternalApp(IEnumerable<AppItemViewModel> vmItems, ConfigurationItem configItem)
    {
        return vmItems.FirstOrDefault(i =>
            (!string.IsNullOrEmpty(configItem.WinGetPackageId) &&
             i.Definition?.WinGetPackageId != null &&
             i.Definition.WinGetPackageId.Contains(configItem.WinGetPackageId)) ||
            i.Id == configItem.Id);
    }
}
