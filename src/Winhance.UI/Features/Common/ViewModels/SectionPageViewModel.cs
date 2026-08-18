using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.Core.Features.Common.Extensions;

namespace Winhance.UI.Features.Common.ViewModels;

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

    // The source the overview cards and the breadcrumb flyout are generated from; each item derives its own badges
    // from observed state, so nothing has to remember to refresh them.
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

    protected abstract string PageTitleKey { get; }

    protected abstract string PageDescriptionKey { get; }

    protected abstract string BreadcrumbRootFallback { get; }

    protected abstract string LogPrefix { get; }

    protected abstract IReadOnlyList<TSectionInfo> SectionDefinitions { get; }

    public string PageTitle => _localizationService.GetString(PageTitleKey);
    public string PageDescription => _localizationService.GetString(PageDescriptionKey);
    public string BreadcrumbRootText => _localizationService.GetStringOrDefault(PageTitleKey, BreadcrumbRootFallback);
    public string SearchPlaceholder => _localizationService.GetStringOrDefault("Common_Search_Placeholder", "Type here to search...");
    public bool IsNotLoading => !IsLoading;
    public bool IsInDetailPage => CurrentSectionKey != "Overview";
    public string CurrentSectionName => GetSectionDisplayName(CurrentSectionKey);

    // Lets the breadcrumb bind its icon and review badge to the same derived state the card uses, instead of a
    // second imperative path computing the same answers.
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
        GC.SuppressFinalize(this);
    }

    // Call from the derived constructor after SectionDefinitions is available.
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

    // Asking the sections is deliberate: they are the only thing that knows what they hold; a separate id-to-section
    // map would be a second copy of that fact, free to drift the moment a setting moves.
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

        var currentViewModel = GetSectionViewModel(CurrentSectionKey);

        foreach (var section in SectionDefinitions)
        {
            var viewModel = GetSectionViewModel(section.Key);
            if (viewModel == null || viewModel == currentViewModel)
                continue;

            foreach (var setting in viewModel.Settings)
            {
                if (setting.Name?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true ||
                    setting.Description?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true)
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
