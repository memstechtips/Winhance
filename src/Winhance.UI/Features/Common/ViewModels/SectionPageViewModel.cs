using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.Core.Features.Common.Extensions;

namespace Winhance.UI.Features.Common.ViewModels;

/// <summary>
/// Base ViewModel for section-based pages (Optimize, Customize).
/// Handles initialization, search, navigation, and localization for pages
/// that display a collection of feature ViewModels organized into sections.
/// </summary>
public abstract partial class SectionPageViewModel<TSectionInfo>
    : ObservableObject, ISectionPageViewModel, IDisposable
    where TSectionInfo : ISectionInfo
{
    private bool _disposed;
    private readonly ILogService _logService;
    private readonly ILocalizationService _localizationService;
    private readonly IConfigReviewBadgeService _badgeService;
    private readonly IConfigReviewModeService _reviewModeService;
    private readonly IReadOnlyList<ISettingsFeatureViewModel> _featureViewModels;
    private readonly Dictionary<string, ISettingsFeatureViewModel> _viewModelBySectionKey;
    private bool _isInitialized;

    /// <summary>
    /// One item per section, in declaration order — the source the overview cards and the breadcrumb
    /// flyout are generated from. Each item derives its own badges from observed state, so nothing
    /// has to remember to refresh them after a change.
    /// </summary>
    public IReadOnlyList<SectionOverviewItemViewModel> OverviewItems { get; private set; } =
        Array.Empty<SectionOverviewItemViewModel>();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; }

    [ObservableProperty]
    public partial string CurrentSectionKey { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<SearchSuggestionItem> SearchSuggestions { get; set; }

    /// <summary>Localization key for the page title (e.g., "Category_Optimize_Title").</summary>
    protected abstract string PageTitleKey { get; }

    /// <summary>Localization key for the page description/status text.</summary>
    protected abstract string PageDescriptionKey { get; }

    /// <summary>Fallback text for the breadcrumb root when localization is missing.</summary>
    protected abstract string BreadcrumbRootFallback { get; }

    /// <summary>Log prefix for initialization messages (e.g., "OptimizeViewModel").</summary>
    protected abstract string LogPrefix { get; }

    /// <summary>The section definitions for this page.</summary>
    protected abstract IReadOnlyList<TSectionInfo> SectionDefinitions { get; }

    public string PageTitle => _localizationService.GetString(PageTitleKey);
    public string PageDescription => _localizationService.GetString(PageDescriptionKey);
    public string BreadcrumbRootText => _localizationService.GetStringOrDefault(PageTitleKey, BreadcrumbRootFallback);
    public string SearchPlaceholder => _localizationService.GetStringOrDefault("Common_Search_Placeholder", "Type here to search...");
    public bool IsNotLoading => !IsLoading;
    public bool IsInDetailPage => CurrentSectionKey != "Overview";
    public string CurrentSectionName => GetSectionDisplayName(CurrentSectionKey);

    /// <summary>
    /// The open section's overview item, or null on the overview itself. Lets the breadcrumb bind its
    /// icon and review badge to the same derived state the card uses, instead of a second imperative
    /// path computing the same answers into named elements.
    /// </summary>
    public SectionOverviewItemViewModel? CurrentSectionItem =>
        OverviewItems.FirstOrDefault(item => item.SectionKey == CurrentSectionKey);

    public bool HasNoSearchResults
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return false;

            var currentVm = GetSectionViewModel(CurrentSectionKey);
            if (currentVm != null)
                return !currentVm.HasVisibleSettings;

            return _featureViewModels.All(vm => !vm.HasVisibleSettings);
        }
    }

    protected SectionPageViewModel(
        ILogService logService,
        ILocalizationService localizationService,
        IEnumerable<ISettingsFeatureViewModel> featureViewModels,
        IConfigReviewBadgeService badgeService,
        IConfigReviewModeService reviewModeService)
    {
        _logService = logService;
        _localizationService = localizationService;
        _badgeService = badgeService;
        _reviewModeService = reviewModeService;
        _featureViewModels = featureViewModels.ToList();

        _viewModelBySectionKey = new Dictionary<string, ISettingsFeatureViewModel>();

        SearchSuggestions = new ObservableCollection<SearchSuggestionItem>();
        IsLoading = true;
        CurrentSectionKey = "Overview";
        SearchText = string.Empty;

        _localizationService.LanguageChanged += OnLanguageChanged;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _localizationService.LanguageChanged -= OnLanguageChanged;

        // Each item holds subscriptions to the badge/review services and to every setting it
        // observes; leaving them attached would keep this page's cards recomputing forever.
        foreach (var item in OverviewItems)
            item.Dispose();
    }

    /// <summary>
    /// Must be called from the derived constructor after SectionDefinitions is available,
    /// to populate the section-key → VM dictionary.
    /// </summary>
    protected void InitializeSectionMappings()
    {
        var byModuleId = _featureViewModels.ToDictionary(vm => vm.ModuleId);
        var overviewItems = new List<SectionOverviewItemViewModel>();

        foreach (var section in SectionDefinitions)
        {
            if (!byModuleId.TryGetValue(section.ModuleId, out var vm))
                continue;

            _viewModelBySectionKey[section.Key] = vm;
            overviewItems.Add(new SectionOverviewItemViewModel(
                section.Key,
                section.ModuleId,
                section.IconGlyphKey,
                vm,
                _badgeService,
                _reviewModeService,
                _localizationService));
        }

        OverviewItems = overviewItems;
    }

    /// <summary>
    /// Looks up a feature ViewModel by its module ID.
    /// Useful for derived classes to populate named XAML-bound properties.
    /// </summary>
    protected ISettingsFeatureViewModel GetFeatureByModuleId(string moduleId)
    {
        return _featureViewModels.First(vm => vm.ModuleId == moduleId);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageDescription));
        OnPropertyChanged(nameof(BreadcrumbRootText));
        OnPropertyChanged(nameof(SearchPlaceholder));
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        try
        {
            IsLoading = true;
            _logService.Log(Core.Features.Common.Enums.LogLevel.Info, $"{LogPrefix}: Starting initialization");

            foreach (var vm in _featureViewModels)
            {
                try
                {
                    await vm.LoadSettingsAsync();
                }
                catch (Exception ex)
                {
                    _logService.Log(Core.Features.Common.Enums.LogLevel.Error,
                        $"{LogPrefix}: Failed to load {vm.DisplayName} settings - {ex.Message}");
                }
            }

            _isInitialized = true;

            var counts = string.Join(", ", _featureViewModels.Select(vm => $"{vm.DisplayName}:{vm.SettingsCount}"));
            _logService.Log(Core.Features.Common.Enums.LogLevel.Info, $"{LogPrefix}: Loaded settings - {counts}");
            _logService.Log(Core.Features.Common.Enums.LogLevel.Info, $"{LogPrefix}: Initialization complete");
        }
        catch (Exception ex)
        {
            _logService.Log(Core.Features.Common.Enums.LogLevel.Error, $"{LogPrefix}: Initialization failed - {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsNotLoading));
        }
    }

    public void OnNavigatedFrom()
    {
        SearchText = string.Empty;
    }

    public ISettingsFeatureViewModel? GetSectionViewModel(string sectionKey)
    {
        return _viewModelBySectionKey.GetValueOrDefault(sectionKey);
    }

    /// <summary>
    /// Which of this page's sections holds a setting, or null when none does — the setting lives on
    /// the other page, or under an id this page has never loaded.
    ///
    /// Asking the sections is deliberate: they are the only thing that knows what they hold, and a
    /// separate id-to-section map maintained alongside them would be a second copy of that fact,
    /// free to drift the moment a setting moves between sections.
    /// </summary>
    public string? FindSectionForSetting(string settingId)
    {
        if (string.IsNullOrEmpty(settingId)) return null;

        foreach (var (sectionKey, viewModel) in _viewModelBySectionKey)
        {
            foreach (var setting in viewModel.Settings)
            {
                if (string.Equals(setting.SettingId, settingId, StringComparison.OrdinalIgnoreCase))
                    return sectionKey;
            }
        }
        return null;
    }

    public string GetSectionDisplayName(string sectionKey)
    {
        var section = SectionDefinitions.FirstOrDefault(s => s.Key == sectionKey);
        if (section != null)
        {
            return GetSectionViewModel(sectionKey)?.DisplayName ?? section.DisplayName;
        }
        return "Overview";
    }

    private void UpdateSearchSuggestions(string searchText)
    {
        SearchSuggestions.Clear();

        if (string.IsNullOrWhiteSpace(searchText) || searchText.Length < 2)
            return;

        var searchLower = searchText.ToLowerInvariant();
        var currentViewModel = GetSectionViewModel(CurrentSectionKey);

        foreach (var section in SectionDefinitions)
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
        OnPropertyChanged(nameof(CurrentSectionItem));
        OnPropertyChanged(nameof(HasNoSearchResults));

        if (!string.IsNullOrEmpty(SearchText))
        {
            SearchText = string.Empty;
        }
        else
        {
            var targetViewModel = GetSectionViewModel(value);
            targetViewModel?.ApplySearchFilter(string.Empty);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        var currentViewModel = GetSectionViewModel(CurrentSectionKey);
        if (currentViewModel != null)
        {
            currentViewModel.ApplySearchFilter(value);
        }
        else
        {
            foreach (var vm in _featureViewModels)
            {
                vm.ApplySearchFilter(value);
            }
        }

        UpdateSearchSuggestions(value);
        OnPropertyChanged(nameof(HasNoSearchResults));
    }
}
