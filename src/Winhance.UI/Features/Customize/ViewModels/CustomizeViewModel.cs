using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Customize.Models;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Customize.ViewModels;

/// <summary>
/// ViewModel for the Customize page, coordinating all customization feature ViewModels.
/// </summary>
public partial class CustomizeViewModel : ObservableObject
{
    private readonly ILogService _logService;
    private readonly ILocalizationService _localizationService;
    private bool _isInitialized;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; }

    [ObservableProperty]
    public partial string CurrentSectionKey { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<SearchSuggestionItem> SearchSuggestions { get; set; }

    /// <summary>
    /// Gets the localized page title.
    /// </summary>
    public string PageTitle => _localizationService.GetString("Category_Customize_Title");

    /// <summary>
    /// Gets the localized page description.
    /// </summary>
    public string PageDescription => _localizationService.GetString("Category_Customize_StatusText");

    /// <summary>
    /// Gets the localized breadcrumb root text.
    /// </summary>
    public string BreadcrumbRootText => _localizationService.GetString("Category_Customize_Title") ?? "Customizations";

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
                "Explorer" => !ExplorerViewModel.HasVisibleSettings,
                "StartMenu" => !StartMenuViewModel.HasVisibleSettings,
                "Taskbar" => !TaskbarViewModel.HasVisibleSettings,
                "WindowsTheme" => !WindowsThemeViewModel.HasVisibleSettings,
                _ => !ExplorerViewModel.HasVisibleSettings &&
                     !StartMenuViewModel.HasVisibleSettings &&
                     !TaskbarViewModel.HasVisibleSettings &&
                     !WindowsThemeViewModel.HasVisibleSettings
            };
        }
    }

    /// <summary>
    /// Section definitions for navigation.
    /// </summary>
    public static readonly List<CustomizeSectionInfo> Sections = new()
    {
        new("Explorer", "ExplorerIconGlyph", "Explorer"),
        new("StartMenu", "StartMenuIconGlyph", "Start Menu"),
        new("Taskbar", "TaskbarIconGlyph", "Taskbar"),
        new("WindowsTheme", "WindowsThemeIconGlyph", "Windows Theme"),
    };

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
        ILocalizationService localizationService,
        ExplorerCustomizationsViewModel explorerViewModel,
        StartMenuCustomizationsViewModel startMenuViewModel,
        TaskbarCustomizationsViewModel taskbarViewModel,
        WindowsThemeCustomizationsViewModel windowsThemeViewModel)
    {
        _logService = logService;
        _localizationService = localizationService;
        ExplorerViewModel = explorerViewModel;
        StartMenuViewModel = startMenuViewModel;
        TaskbarViewModel = taskbarViewModel;
        WindowsThemeViewModel = windowsThemeViewModel;

        // Initialize partial property defaults (collections first, then
        // properties with change handlers that may reference them)
        SearchSuggestions = new ObservableCollection<SearchSuggestionItem>();
        IsLoading = true;
        CurrentSectionKey = "Overview";
        SearchText = string.Empty;

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
            _logService.Log(Core.Features.Common.Enums.LogLevel.Info, "CustomizeViewModel: Starting initialization");

            // Load all feature settings, catching individual failures
            var loadTasks = new (string Name, Func<Task> LoadTask)[]
            {
                ("Explorer", () => ExplorerViewModel.LoadSettingsAsync()),
                ("StartMenu", () => StartMenuViewModel.LoadSettingsAsync()),
                ("Taskbar", () => TaskbarViewModel.LoadSettingsAsync()),
                ("WindowsTheme", () => WindowsThemeViewModel.LoadSettingsAsync())
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
                        $"CustomizeViewModel: Failed to load {name} settings - {ex.Message}");
                }
            }

            _isInitialized = true;

            // Log settings count for each feature
            _logService.Log(Core.Features.Common.Enums.LogLevel.Info,
                $"CustomizeViewModel: Loaded settings - Explorer:{ExplorerViewModel.SettingsCount}, " +
                $"StartMenu:{StartMenuViewModel.SettingsCount}, Taskbar:{TaskbarViewModel.SettingsCount}, " +
                $"WindowsTheme:{WindowsThemeViewModel.SettingsCount}");
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

    /// <summary>
    /// Gets the ViewModel for the specified section key.
    /// </summary>
    public BaseSettingsFeatureViewModel? GetSectionViewModel(string sectionKey)
    {
        return sectionKey switch
        {
            "Explorer" => ExplorerViewModel,
            "StartMenu" => StartMenuViewModel,
            "Taskbar" => TaskbarViewModel,
            "WindowsTheme" => WindowsThemeViewModel,
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
            ExplorerViewModel.ApplySearchFilter(value);
            StartMenuViewModel.ApplySearchFilter(value);
            TaskbarViewModel.ApplySearchFilter(value);
            WindowsThemeViewModel.ApplySearchFilter(value);
        }

        // Update suggestions (searches all sections, excludes current section if on detail page)
        UpdateSearchSuggestions(value);

        OnPropertyChanged(nameof(HasNoSearchResults));
    }
}
