using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Optimize.ViewModels;

/// <summary>
/// ViewModel for the Optimize page, coordinating all optimization feature ViewModels.
/// </summary>
public partial class OptimizeViewModel : ObservableObject
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
        !SoundViewModel.HasVisibleSettings &&
        !UpdateViewModel.HasVisibleSettings &&
        !NotificationViewModel.HasVisibleSettings &&
        !PrivacyViewModel.HasVisibleSettings &&
        !PowerViewModel.HasVisibleSettings &&
        !GamingViewModel.HasVisibleSettings;

    /// <summary>
    /// Sound optimization settings ViewModel.
    /// </summary>
    public SoundOptimizationsViewModel SoundViewModel { get; }

    /// <summary>
    /// Update optimization settings ViewModel.
    /// </summary>
    public UpdateOptimizationsViewModel UpdateViewModel { get; }

    /// <summary>
    /// Notification optimization settings ViewModel.
    /// </summary>
    public NotificationOptimizationsViewModel NotificationViewModel { get; }

    /// <summary>
    /// Privacy optimization settings ViewModel.
    /// </summary>
    public PrivacyOptimizationsViewModel PrivacyViewModel { get; }

    /// <summary>
    /// Power optimization settings ViewModel.
    /// </summary>
    public PowerOptimizationsViewModel PowerViewModel { get; }

    /// <summary>
    /// Gaming and performance optimization settings ViewModel.
    /// </summary>
    public GamingOptimizationsViewModel GamingViewModel { get; }

    public OptimizeViewModel(
        ILogService logService,
        SoundOptimizationsViewModel soundViewModel,
        UpdateOptimizationsViewModel updateViewModel,
        NotificationOptimizationsViewModel notificationViewModel,
        PrivacyOptimizationsViewModel privacyViewModel,
        PowerOptimizationsViewModel powerViewModel,
        GamingOptimizationsViewModel gamingViewModel)
    {
        _logService = logService;
        SoundViewModel = soundViewModel;
        UpdateViewModel = updateViewModel;
        NotificationViewModel = notificationViewModel;
        PrivacyViewModel = privacyViewModel;
        PowerViewModel = powerViewModel;
        GamingViewModel = gamingViewModel;
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
            _logService.Log(Core.Features.Common.Enums.LogLevel.Info, "OptimizeViewModel: Starting initialization");

            // Load all feature settings in parallel, catching individual failures
            var loadTasks = new (string Name, Func<Task> LoadTask)[]
            {
                ("Sound", () => SoundViewModel.LoadSettingsAsync()),
                ("Update", () => UpdateViewModel.LoadSettingsAsync()),
                ("Notification", () => NotificationViewModel.LoadSettingsAsync()),
                ("Privacy", () => PrivacyViewModel.LoadSettingsAsync()),
                ("Power", () => PowerViewModel.LoadSettingsAsync()),
                ("Gaming", () => GamingViewModel.LoadSettingsAsync())
            };

            foreach (var (name, loadTask) in loadTasks)
            {
                try
                {
                    await loadTask();
                }
                catch (Exception ex)
                {
                    _logService.Log(Core.Features.Common.Enums.LogLevel.Error,
                        $"OptimizeViewModel: Failed to load {name} settings - {ex.Message}");
                }
            }

            _isInitialized = true;

            // Log settings count for each feature to help debug visibility issues
            _logService.Log(Core.Features.Common.Enums.LogLevel.Info,
                $"OptimizeViewModel: Loaded settings - Sound:{SoundViewModel.SettingsCount}, " +
                $"Update:{UpdateViewModel.SettingsCount}, Notification:{NotificationViewModel.SettingsCount}, " +
                $"Privacy:{PrivacyViewModel.SettingsCount}, Power:{PowerViewModel.SettingsCount}, " +
                $"Gaming:{GamingViewModel.SettingsCount}");
            _logService.Log(Core.Features.Common.Enums.LogLevel.Info,
                $"OptimizeViewModel: HasVisibleSettings - Sound:{SoundViewModel.HasVisibleSettings}, " +
                $"Update:{UpdateViewModel.HasVisibleSettings}, Notification:{NotificationViewModel.HasVisibleSettings}, " +
                $"Privacy:{PrivacyViewModel.HasVisibleSettings}, Power:{PowerViewModel.HasVisibleSettings}, " +
                $"Gaming:{GamingViewModel.HasVisibleSettings}");
            _logService.Log(Core.Features.Common.Enums.LogLevel.Info, "OptimizeViewModel: Initialization complete");
        }
        catch (Exception ex)
        {
            _logService.Log(Core.Features.Common.Enums.LogLevel.Error, $"OptimizeViewModel: Initialization failed - {ex.Message}");
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
        SoundViewModel.ApplySearchFilter(value);
        UpdateViewModel.ApplySearchFilter(value);
        NotificationViewModel.ApplySearchFilter(value);
        PrivacyViewModel.ApplySearchFilter(value);
        PowerViewModel.ApplySearchFilter(value);
        GamingViewModel.ApplySearchFilter(value);

        OnPropertyChanged(nameof(HasNoSearchResults));
    }
}
