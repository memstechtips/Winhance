using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.ViewModels;

namespace Winhance.UI.Features.SoftwareApps.ViewModels;

/// <summary>
/// Main ViewModel for the SoftwareApps page that manages Windows Apps and External Apps tabs.
/// </summary>
public partial class SoftwareAppsViewModel : BaseViewModel
{
    private readonly ILocalizationService _localizationService;
    private readonly ILogService _logService;

    public SoftwareAppsViewModel(
        WindowsAppsViewModel windowsAppsViewModel,
        ExternalAppsViewModel externalAppsViewModel,
        ILocalizationService localizationService,
        ILogService logService)
    {
        WindowsAppsViewModel = windowsAppsViewModel;
        ExternalAppsViewModel = externalAppsViewModel;
        _localizationService = localizationService;
        _logService = logService;

        WindowsAppsViewModel.PropertyChanged += ChildViewModel_PropertyChanged;
        ExternalAppsViewModel.PropertyChanged += ChildViewModel_PropertyChanged;
        WindowsAppsViewModel.SelectedItemsChanged += ChildViewModel_SelectedItemsChanged;
        ExternalAppsViewModel.SelectedItemsChanged += ChildViewModel_SelectedItemsChanged;
        _localizationService.LanguageChanged += OnLanguageChanged;

        UpdateButtonStates();
    }

    public WindowsAppsViewModel WindowsAppsViewModel { get; }
    public ExternalAppsViewModel ExternalAppsViewModel { get; }

    [ObservableProperty]
    private bool _isWindowsAppsTabSelected = true;

    [ObservableProperty]
    private bool _isExternalAppsTabSelected = false;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _canInstallItems = false;

    [ObservableProperty]
    private bool _canRemoveItems = false;

    public string RemoveButtonText => IsWindowsAppsTabSelected
        ? _localizationService.GetString("SoftwareApps_Button_RemoveSelected")
        : _localizationService.GetString("SoftwareApps_Button_UninstallSelected");

    public bool IsLoading => IsWindowsAppsTabSelected
        ? WindowsAppsViewModel.IsLoading
        : ExternalAppsViewModel.IsLoading;

    partial void OnSearchTextChanged(string value)
    {
        if (IsWindowsAppsTabSelected)
        {
            WindowsAppsViewModel.SearchText = value;
        }
        else
        {
            ExternalAppsViewModel.SearchText = value;
        }
    }

    partial void OnIsWindowsAppsTabSelectedChanged(bool value)
    {
        if (value)
        {
            IsExternalAppsTabSelected = false;
            WindowsAppsViewModel.SearchText = SearchText;
            ExternalAppsViewModel.SearchText = string.Empty;
        }
        OnPropertyChanged(nameof(RemoveButtonText));
        OnPropertyChanged(nameof(IsLoading));
        UpdateButtonStates();
    }

    partial void OnIsExternalAppsTabSelectedChanged(bool value)
    {
        if (value)
        {
            IsWindowsAppsTabSelected = false;
            ExternalAppsViewModel.SearchText = SearchText;
            WindowsAppsViewModel.SearchText = string.Empty;
        }
        OnPropertyChanged(nameof(RemoveButtonText));
        OnPropertyChanged(nameof(IsLoading));
        UpdateButtonStates();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(RemoveButtonText));
    }

    private void ChildViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WindowsAppsViewModel.HasSelectedItems) ||
            e.PropertyName == nameof(ExternalAppsViewModel.HasSelectedItems) ||
            e.PropertyName == nameof(WindowsAppsViewModel.IsTaskRunning) ||
            e.PropertyName == nameof(ExternalAppsViewModel.IsTaskRunning))
        {
            UpdateButtonStates();
        }
        else if (e.PropertyName == nameof(WindowsAppsViewModel.IsLoading) ||
                 e.PropertyName == nameof(ExternalAppsViewModel.IsLoading))
        {
            OnPropertyChanged(nameof(IsLoading));
        }
    }

    private void ChildViewModel_SelectedItemsChanged(object? sender, EventArgs e)
    {
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        bool isAnyTaskRunning = WindowsAppsViewModel.IsTaskRunning || ExternalAppsViewModel.IsTaskRunning;

        if (IsWindowsAppsTabSelected)
        {
            var hasSelected = WindowsAppsViewModel.HasSelectedItems;
            CanInstallItems = hasSelected && !isAnyTaskRunning;
            CanRemoveItems = hasSelected && !isAnyTaskRunning;
        }
        else if (IsExternalAppsTabSelected)
        {
            var hasSelected = ExternalAppsViewModel.HasSelectedItems;
            CanInstallItems = hasSelected && !isAnyTaskRunning;
            CanRemoveItems = hasSelected && !isAnyTaskRunning;
        }
        else
        {
            CanInstallItems = false;
            CanRemoveItems = false;
        }

        InstallSelectedItemsCommand.NotifyCanExecuteChanged();
        RemoveSelectedItemsCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        _logService.LogInformation("[SoftwareAppsViewModel] InitializeAsync started");

        try
        {
            if (!WindowsAppsViewModel.IsInitialized)
            {
                _logService.LogInformation("[SoftwareAppsViewModel] Loading WindowsAppsViewModel");
                await WindowsAppsViewModel.LoadAppsAndCheckInstallationStatusAsync();
            }

            if (!ExternalAppsViewModel.IsInitialized)
            {
                _logService.LogInformation("[SoftwareAppsViewModel] Loading ExternalAppsViewModel");
                await ExternalAppsViewModel.LoadAppsAndCheckInstallationStatusAsync();
            }

            _logService.LogInformation("[SoftwareAppsViewModel] InitializeAsync completed");
        }
        catch (Exception ex)
        {
            _logService.LogError($"[SoftwareAppsViewModel] Error in InitializeAsync: {ex.Message}", ex);
            throw;
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstallItems))]
    private async Task InstallSelectedItemsAsync()
    {
        if (IsWindowsAppsTabSelected)
        {
            await WindowsAppsViewModel.InstallAppsAsync();
        }
        else
        {
            await ExternalAppsViewModel.InstallAppsAsync();
        }
        UpdateButtonStates();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveItems))]
    private async Task RemoveSelectedItemsAsync()
    {
        if (IsWindowsAppsTabSelected)
        {
            await WindowsAppsViewModel.RemoveAppsAsync();
        }
        else
        {
            await ExternalAppsViewModel.UninstallAppsAsync();
        }
        UpdateButtonStates();
    }

    [RelayCommand]
    private async Task RefreshInstallationStatusAsync()
    {
        if (IsWindowsAppsTabSelected)
        {
            await WindowsAppsViewModel.RefreshInstallationStatusAsync();
        }
        else
        {
            await ExternalAppsViewModel.RefreshInstallationStatusAsync();
        }
    }

    [RelayCommand]
    public void SelectWindowsAppsTab()
    {
        IsWindowsAppsTabSelected = true;
    }

    [RelayCommand]
    public void SelectExternalAppsTab()
    {
        IsExternalAppsTabSelected = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _localizationService.LanguageChanged -= OnLanguageChanged;
            WindowsAppsViewModel.PropertyChanged -= ChildViewModel_PropertyChanged;
            ExternalAppsViewModel.PropertyChanged -= ChildViewModel_PropertyChanged;
            WindowsAppsViewModel.SelectedItemsChanged -= ChildViewModel_SelectedItemsChanged;
            ExternalAppsViewModel.SelectedItemsChanged -= ChildViewModel_SelectedItemsChanged;
        }
        base.Dispose(disposing);
    }
}
