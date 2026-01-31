using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Customize.ViewModels;

/// <summary>
/// ViewModel for the Customize page, coordinating all customization feature ViewModels.
/// </summary>
public partial class CustomizeViewModel : ObservableObject
{
    private readonly ILogService _logService;
    private bool _isInitialized;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// Gets whether the page is not loading (inverse of IsLoading for visibility binding).
    /// </summary>
    public bool IsNotLoading => !IsLoading;

    /// <summary>
    /// Gets whether there are no search results.
    /// </summary>
    public bool HasNoSearchResults => !string.IsNullOrWhiteSpace(SearchText) &&
        !ExplorerViewModel.HasVisibleSettings &&
        !StartMenuViewModel.HasVisibleSettings &&
        !TaskbarViewModel.HasVisibleSettings &&
        !WindowsThemeViewModel.HasVisibleSettings;

    /// <summary>
    /// Explorer customization settings ViewModel.
    /// </summary>
    public ExplorerCustomizationsViewModel ExplorerViewModel { get; }

    /// <summary>
    /// Start Menu customization settings ViewModel.
    /// </summary>
    public StartMenuCustomizationsViewModel StartMenuViewModel { get; }

    /// <summary>
    /// Taskbar customization settings ViewModel.
    /// </summary>
    public TaskbarCustomizationsViewModel TaskbarViewModel { get; }

    /// <summary>
    /// Windows Theme customization settings ViewModel.
    /// </summary>
    public WindowsThemeCustomizationsViewModel WindowsThemeViewModel { get; }

    public CustomizeViewModel(
        ILogService logService,
        ExplorerCustomizationsViewModel explorerViewModel,
        StartMenuCustomizationsViewModel startMenuViewModel,
        TaskbarCustomizationsViewModel taskbarViewModel,
        WindowsThemeCustomizationsViewModel windowsThemeViewModel)
    {
        _logService = logService;
        ExplorerViewModel = explorerViewModel;
        StartMenuViewModel = startMenuViewModel;
        TaskbarViewModel = taskbarViewModel;
        WindowsThemeViewModel = windowsThemeViewModel;
    }

    /// <summary>
    /// Initializes all feature ViewModels and loads settings.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        try
        {
            IsLoading = true;
            _logService.Log(Core.Features.Common.Enums.LogLevel.Info, "CustomizeViewModel: Starting initialization");

            // Load all feature settings in parallel
            var tasks = new[]
            {
                ExplorerViewModel.LoadSettingsAsync(),
                StartMenuViewModel.LoadSettingsAsync(),
                TaskbarViewModel.LoadSettingsAsync(),
                WindowsThemeViewModel.LoadSettingsAsync()
            };

            await Task.WhenAll(tasks);

            _isInitialized = true;
            _logService.Log(Core.Features.Common.Enums.LogLevel.Info, "CustomizeViewModel: Initialization complete");
        }
        catch (Exception ex)
        {
            _logService.Log(Core.Features.Common.Enums.LogLevel.Error, $"CustomizeViewModel: Initialization failed - {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsNotLoading));
        }
    }

    /// <summary>
    /// Called when navigating away from the page.
    /// </summary>
    public void OnNavigatedFrom()
    {
        SearchText = string.Empty;
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotLoading));
    }

    partial void OnSearchTextChanged(string value)
    {
        // Apply search filter to all feature ViewModels
        ExplorerViewModel.ApplySearchFilter(value);
        StartMenuViewModel.ApplySearchFilter(value);
        TaskbarViewModel.ApplySearchFilter(value);
        WindowsThemeViewModel.ApplySearchFilter(value);

        OnPropertyChanged(nameof(HasNoSearchResults));
    }
}
