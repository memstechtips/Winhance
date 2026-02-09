using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.Models;

namespace Winhance.UI.Features.Optimize.ViewModels;

/// <summary>
/// ViewModel for the Optimize page, coordinating all optimization feature ViewModels.
/// </summary>
public partial class OptimizeViewModel : ObservableObject
{
    private readonly ILogService _logService;
    private readonly ILocalizationService _localizationService;
    private bool _isInitialized;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _currentSectionKey = "Overview";

    [ObservableProperty]
    private ObservableCollection<SearchSuggestionItem> _searchSuggestions = new();

    /// <summary>
    /// Gets the localized page title.
    /// </summary>
    public string PageTitle => _localizationService.GetString("Category_Optimize_Title");

    /// <summary>
    /// Gets the localized page description.
    /// </summary>
    public string PageDescription => _localizationService.GetString("Category_Optimize_StatusText");

    /// <summary>
    /// Gets the localized breadcrumb root text.
    /// </summary>
    public string BreadcrumbRootText => _localizationService.GetString("Category_Optimize_Title") ?? "Optimizations";

    /// <summary>
    /// Gets the localized search placeholder text.
    /// </summary>
    public string SearchPlaceholder => _localizationService.GetString("Common_Search_Placeholder") ?? "Type here to search...";

    /// <summary>
    /// Gets whether the page is not loading (inverse of IsLoading for visibility binding).
    /// </summary>
    public bool IsNotLoading => !IsLoading;

    /// <summary>
    /// Gets whether navigation is currently on a detail page (not overview).
    /// </summary>
    public bool IsInDetailPage => CurrentSectionKey != "Overview";

    /// <summary>
    /// Gets the display name of the current section.
    /// </summary>
    public string CurrentSectionName => GetSectionDisplayName(CurrentSectionKey);

    /// <summary>
    /// Gets whether there are no search results in the current section.
    /// </summary>
    public bool HasNoSearchResults
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return false;

            return CurrentSectionKey switch
            {
                "Sound" => !SoundViewModel.HasVisibleSettings,
                "Update" => !UpdateViewModel.HasVisibleSettings,
                "Notification" => !NotificationViewModel.HasVisibleSettings,
                "Privacy" => !PrivacyViewModel.HasVisibleSettings,
                "Power" => !PowerViewModel.HasVisibleSettings,
                "Gaming" => !GamingViewModel.HasVisibleSettings,
                _ => !SoundViewModel.HasVisibleSettings &&
                     !UpdateViewModel.HasVisibleSettings &&
                     !NotificationViewModel.HasVisibleSettings &&
                     !PrivacyViewModel.HasVisibleSettings &&
                     !PowerViewModel.HasVisibleSettings &&
                     !GamingViewModel.HasVisibleSettings
            };
        }
    }

    /// <summary>
    /// Section definitions for navigation.
    /// </summary>
    public static readonly List<OptimizeSectionInfo> Sections = new()
    {
        new("Privacy", "PrivacyIconPath", "Privacy & Security"),
        new("Power", "PowerIconPath", "Power"),
        new("Gaming", "GamingIconPath", "Gaming and Performance"),
        new("Update", "UpdateIconGlyph", "Updates"),
        new("Notification", "NotificationIconPath", "Notifications"),
        new("Sound", "SoundIconGlyph", "Sound"),
    };

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
        ILocalizationService localizationService,
        SoundOptimizationsViewModel soundViewModel,
        UpdateOptimizationsViewModel updateViewModel,
        NotificationOptimizationsViewModel notificationViewModel,
        PrivacyOptimizationsViewModel privacyViewModel,
        PowerOptimizationsViewModel powerViewModel,
        GamingOptimizationsViewModel gamingViewModel)
    {
        _logService = logService;
        _localizationService = localizationService;
        SoundViewModel = soundViewModel;
        UpdateViewModel = updateViewModel;
        NotificationViewModel = notificationViewModel;
        PrivacyViewModel = privacyViewModel;
        PowerViewModel = powerViewModel;
        GamingViewModel = gamingViewModel;

        // Subscribe to language changes to update localized strings
        _localizationService.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>
    /// Handles language changes to update localized strings.
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageDescription));
        OnPropertyChanged(nameof(BreadcrumbRootText));
        OnPropertyChanged(nameof(SearchPlaceholder));
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

            // Load all feature settings, catching individual failures
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

            // Log settings count for each feature
            _logService.Log(Core.Features.Common.Enums.LogLevel.Info,
                $"OptimizeViewModel: Loaded settings - Sound:{SoundViewModel.SettingsCount}, " +
                $"Update:{UpdateViewModel.SettingsCount}, Notification:{NotificationViewModel.SettingsCount}, " +
                $"Privacy:{PrivacyViewModel.SettingsCount}, Power:{PowerViewModel.SettingsCount}, " +
                $"Gaming:{GamingViewModel.SettingsCount}");
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

    /// <summary>
    /// Gets the ViewModel for the specified section key.
    /// </summary>
    public BaseSettingsFeatureViewModel? GetSectionViewModel(string sectionKey)
    {
        return sectionKey switch
        {
            "Sound" => SoundViewModel,
            "Update" => UpdateViewModel,
            "Notification" => NotificationViewModel,
            "Privacy" => PrivacyViewModel,
            "Power" => PowerViewModel,
            "Gaming" => GamingViewModel,
            _ => null
        };
    }

    /// <summary>
    /// Gets the display name for the specified section key.
    /// </summary>
    public string GetSectionDisplayName(string sectionKey)
    {
        var section = Sections.FirstOrDefault(s => s.Key == sectionKey);
        if (section != null)
        {
            return GetSectionViewModel(sectionKey)?.DisplayName ?? section.DisplayName;
        }
        return "Overview";
    }

    /// <summary>
    /// Updates search suggestions from other sections.
    /// </summary>
    private void UpdateSearchSuggestions(string searchText)
    {
        SearchSuggestions.Clear();

        if (string.IsNullOrWhiteSpace(searchText) || searchText.Length < 2)
            return;

        var searchLower = searchText.ToLowerInvariant();
        var currentViewModel = GetSectionViewModel(CurrentSectionKey);

        // Search in other sections (not the current one)
        foreach (var section in Sections)
        {
            var viewModel = GetSectionViewModel(section.Key);
            if (viewModel == null || viewModel == currentViewModel)
                continue;

            foreach (var setting in viewModel.Settings)
            {
                if (setting.Name?.ToLowerInvariant().Contains(searchLower) == true ||
                    setting.Description?.ToLowerInvariant().Contains(searchLower) == true)
                {
                    SearchSuggestions.Add(new SearchSuggestionItem(
                        setting.Name ?? "Unknown",
                        section.Key,
                        viewModel.DisplayName,
                        section.IconGlyphKey
                    ));

                    // Limit suggestions
                    if (SearchSuggestions.Count >= 5)
                        return;
                }
            }
        }
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotLoading));
    }

    partial void OnCurrentSectionKeyChanged(string value)
    {
        OnPropertyChanged(nameof(IsInDetailPage));
        OnPropertyChanged(nameof(CurrentSectionName));
        OnPropertyChanged(nameof(HasNoSearchResults));

        // Clear search when navigating
        if (!string.IsNullOrEmpty(SearchText))
        {
            SearchText = string.Empty;
        }
        else
        {
            // SearchText is already empty, but the target ViewModel may have
            // stale visibility state from a previous search. Reset it explicitly.
            var targetViewModel = GetSectionViewModel(value);
            targetViewModel?.ApplySearchFilter(string.Empty);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        // Apply search filter to the current section's ViewModel
        var currentViewModel = GetSectionViewModel(CurrentSectionKey);
        if (currentViewModel != null)
        {
            currentViewModel.ApplySearchFilter(value);
        }
        else
        {
            // On overview, filter all ViewModels
            SoundViewModel.ApplySearchFilter(value);
            UpdateViewModel.ApplySearchFilter(value);
            NotificationViewModel.ApplySearchFilter(value);
            PrivacyViewModel.ApplySearchFilter(value);
            PowerViewModel.ApplySearchFilter(value);
            GamingViewModel.ApplySearchFilter(value);
        }

        // Update suggestions (searches all sections, excludes current section if on detail page)
        UpdateSearchSuggestions(value);

        OnPropertyChanged(nameof(HasNoSearchResults));
    }
}
