using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Services;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.Pages;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

// Handed to the base once, in the derived page's constructor, because XAML generates those fields into the
// DERIVED partial class where the base cannot see them. One record beats an abstract property per element: one
// block to read, one place a page can fail to supply something. OverviewScroller is also where the
// PageUp/PageDown handling is attached.
public sealed record SectionPageChrome(
    ScrollView OverviewScroller,
    UIElement OverviewContent,
    bool HasSectionContent,
    Frame ContentFrame,
    AutoSuggestBox SearchBox,
    Button BreadcrumbRoot,
    UIElement BreadcrumbSeparator,
    UIElement BreadcrumbSection,
    TextBlock BreadcrumbSectionText,
    Flyout BreadcrumbFlyout,
    TextBlock QuickActionsLabel,
    DropDownButton QuickActionsButton,
    MenuFlyoutItem ApplyRecommendedItem,
    FontIcon ApplyRecommendedIcon,
    MenuFlyoutItem ResetDefaultsItem,
    TextBlock ViewMenuLabel,
    DropDownButton ViewMenuButton,
    ToggleMenuFlyoutItem TechnicalDetailsToggle,
    ToggleMenuFlyoutItem InfoBadgesToggle,
    ToggleMenuFlyoutItem NewBadgesToggle,
    MenuFlyoutSeparator StateFilterSeparator,
    RadioMenuFlyoutItem StateFilterAllItem,
    RadioMenuFlyoutItem StateFilterRecommendedItem,
    RadioMenuFlyoutItem StateFilterWindowsDefaultItem,
    RadioMenuFlyoutItem StateFilterCustomItem,
    ToggleMenuFlyoutItem ShowOnlyChangesToggle,
    MenuFlyoutSeparator ShowOnlyChangesSeparator,
    MenuFlyoutItem IncludeAllItem,
    MenuFlyoutItem ExcludeAllItem,
    MenuFlyoutSeparator IncludeActionsSeparator);

// Shared base for the section pages; SectionPageShell holds the markup. Not generic on purpose: WinUI 3 XAML
// has no x:TypeArguments (WPF-only), and the XAML codegen writes the partial class's base type from the root
// element tag, so a page's XAML root must name this type literally and it cannot take a type parameter -
// ISectionPageViewModel does that job. Handlers are protected rather than private because the generated Connect
// method wires XAML events as this.Handler, which reaches an inherited protected member.
public abstract class SectionPage : Page
{
    private SectionPageChrome _chrome = null!;

    private IConfigReviewService? _configReviewService;
    private IUserPreferencesService? _userPreferencesService;
    private ILocalizationService? _localizationService;
    private IBulkSettingsActionService? _bulkSettingsActionService;
    private IApplicationModeService? _applicationModeService;

    private ISubscriptionToken? _settingsRefreshedSubscription;
    private ISubscriptionToken? _settingLinkSubscription;

    private bool _isTechnicalDetailsVisible;
    private bool _isInfoBadgesVisible = true;
    private bool _isNewBadgesVisible = true;
    private bool _showOnlyChanges;
    private SettingStateFilter _stateFilter;
    private (RadioMenuFlyoutItem Item, SettingStateFilter Filter)[] _stateFilterItems = [];

    // x:Bind in the XAML still resolves against each page's concrete property, so bindings are unaffected by this indirection.
    protected abstract ISectionPageViewModel PageViewModel { get; }

    private void LogStartup(string message) => StartupLogger.Log($"{GetType().Name}: {message}");

    // Call from the derived constructor after InitializeComponent (which creates the shell) and after the ViewModel
    // has been resolved. The handlers are attached here, once, against the shell's parts, because the shared markup
    // cannot see this class.
    protected void InitializeSectionPage(SectionPageShell shell)
    {
        shell.ViewModel = PageViewModel;
        var chrome = shell.Chrome;
        _chrome = chrome;

        chrome.BreadcrumbRoot.Click += BreadcrumbOverview_Click;
        chrome.ApplyRecommendedItem.Click += ApplyRecommended_Click;
        chrome.ResetDefaultsItem.Click += ResetDefaults_Click;
        chrome.IncludeAllItem.Click += IncludeAll_Click;
        chrome.ExcludeAllItem.Click += ExcludeAll_Click;
        chrome.TechnicalDetailsToggle.Click += ViewTechnicalDetails_Click;
        chrome.InfoBadgesToggle.Click += ViewInfoBadges_Click;
        chrome.NewBadgesToggle.Click += ViewNewBadges_Click;
        chrome.ShowOnlyChangesToggle.Click += ViewShowOnlyChanges_Click;

        _stateFilterItems =
        [
            (chrome.StateFilterAllItem, SettingStateFilter.All),
            (chrome.StateFilterRecommendedItem, SettingStateFilter.Recommended),
            (chrome.StateFilterWindowsDefaultItem, SettingStateFilter.WindowsDefault),
            (chrome.StateFilterCustomItem, SettingStateFilter.Custom),
        ];
        // Answer-file cards author a file rather than read this PC, so their badge state is nothing to filter by.
        var stateFilterVisibility = IsAnswerFilePage ? Visibility.Collapsed : Visibility.Visible;
        chrome.StateFilterSeparator.Visibility = stateFilterVisibility;
        foreach (var (item, _) in _stateFilterItems)
        {
            // Radio groups are matched by name across the whole UI thread, and Optimize and Customize are both
            // cached pages with this same menu.
            item.GroupName = GetType().Name + "StateFilter";
            item.Visibility = stateFilterVisibility;
            item.Click += ViewStateFilter_Click;
        }
        chrome.ContentFrame.Navigated += InnerContentFrame_Navigated;
        chrome.SearchBox.SuggestionChosen += SearchBox_SuggestionChosen;
        chrome.SearchBox.QuerySubmitted += SearchBox_QuerySubmitted;

        // Raised from inside the overview/flyout item templates, whose instances do not exist
        // yet — the shell forwards them with the original sender, whose Tag is the section key.
        shell.SectionCardClicked += SectionCard_Click;
        shell.SectionFlyoutItemClicked += SectionFlyoutItem_Click;
        shell.OutcomeBannerNavigationRequested += OnOutcomeBannerNavigationRequested;

        // PageUp/PageDown fast-scroll + Home/End jump for the overview scroller (issue #581).
        PageScrollHelper.Attach(this, chrome.OverviewScroller);

        _configReviewService = App.Services.GetService<IConfigReviewService>();
        if (_configReviewService != null)
        {
            _configReviewService.ReviewModeChanged += OnReviewModeChanged;
        }

        _userPreferencesService = App.Services.GetService<IUserPreferencesService>();
        _localizationService = App.Services.GetService<ILocalizationService>();
        _bulkSettingsActionService = App.Services.GetService<IBulkSettingsActionService>();
        _applicationModeService = App.Services.GetService<IApplicationModeService>();
        if (_applicationModeService != null)
        {
            _applicationModeService.ModeChanged += OnApplicationModeChanged;
        }
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        try
        {
            LogStartup("OnNavigatedTo starting...");
            base.OnNavigatedTo(e);

            // Re-subscribe in case OnNavigatedFrom unsubscribed (page is cached)
            if (_configReviewService != null)
            {
                _configReviewService.ReviewModeChanged -= OnReviewModeChanged;
                _configReviewService.ReviewModeChanged += OnReviewModeChanged;
            }

            if (_applicationModeService != null)
            {
                _applicationModeService.ModeChanged -= OnApplicationModeChanged;
                _applicationModeService.ModeChanged += OnApplicationModeChanged;
            }

            var eventBus = App.Services.GetService<IEventBus>();
            if (eventBus != null)
            {
                // A Technical Details requirement chip naming another setting was clicked. Only
                // this page knows which of its sections holds it, so resolution happens here.
                _settingLinkSubscription?.Dispose();
                _settingLinkSubscription = eventBus.Subscribe<SettingLinkRequestedEvent>(evt =>
                {
                    DispatcherQueue.TryEnqueue(() => GoToLinkedSetting(evt.SettingId, evt.SettingName));
                });
                _settingsRefreshedSubscription?.Dispose();
                _settingsRefreshedSubscription = eventBus.Subscribe<SettingsRefreshedEvent>(_ =>
                {
                    DispatcherQueue.TryEnqueue(SyncViewStateToSettings);
                });
                // No SettingAppliedEvent subscription: the overview cards derive their own state
                // from the settings they observe, so an apply reaches them without this page.
            }

            PageViewModel.CurrentSectionKey = "Overview";
            UpdateContentVisibility();

            LogStartup("Calling ViewModel.InitializeAsync...");
            await PageViewModel.InitializeAsync();

            SetDropdownLabels();
            await InitializeViewTogglesAsync();

            LogStartup("OnNavigatedTo complete");
        }
        catch (Exception ex)
        {
            LogStartup($"OnNavigatedTo EXCEPTION: {ex}");
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        if (_configReviewService != null)
        {
            _configReviewService.ReviewModeChanged -= OnReviewModeChanged;
        }
        if (_applicationModeService != null)
        {
            _applicationModeService.ModeChanged -= OnApplicationModeChanged;
        }
        _settingsRefreshedSubscription?.Dispose();
        _settingsRefreshedSubscription = null;
        _settingLinkSubscription?.Dispose();
        _settingLinkSubscription = null;
        PageViewModel.OnNavigatedFrom();
    }

    // Falls back to the section currently open when this page does not hold the setting - it is on the other page,
    // and pre-applying the search where the user already is beats navigating them somewhere it also is not.
    private void GoToLinkedSetting(string settingId, string settingName)
    {
        var sectionKey = PageViewModel.FindSectionForSetting(settingId) ?? PageViewModel.CurrentSectionKey;
        if (!string.IsNullOrEmpty(sectionKey)) NavigateToSection(sectionKey, settingName);
    }

    protected void NavigateToSection(string sectionKey, string? searchText = null)
    {
        // The one section is already on screen; opening the detail frame would swap it out for a second copy.
        if (_chrome.HasSectionContent)
        {
            PageViewModel.SearchText = searchText ?? string.Empty;
            return;
        }

        var item = PageViewModel.OverviewItems.FirstOrDefault(i => i.SectionKey == sectionKey);
        if (item == null)
        {
            NavigateToOverview();
            return;
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            item.Feature.ApplySearchFilter(searchText);
        }

        _chrome.ContentFrame.Navigate(
            typeof(SectionDetailPage),
            new SectionDetailNavigation(sectionKey, searchText, PageViewModel, item.Feature));

        if (_configReviewService?.IsInReviewMode == true && FeatureIdFor(sectionKey) is { } featureId)
        {
            _configReviewService.MarkFeatureVisited(featureId);
        }
    }

    protected void NavigateToOverview()
    {
        PageViewModel.CurrentSectionKey = "Overview";
        _chrome.ContentFrame.Content = null;
        UpdateContentVisibility();
    }

    private string? FeatureIdFor(string sectionKey) =>
        PageViewModel.OverviewItems.FirstOrDefault(i => i.SectionKey == sectionKey)?.FeatureId;

    protected void InnerContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        // The parameter names the section; one page type serves them all, so the type cannot.
        PageViewModel.CurrentSectionKey = e.Parameter is SectionDetailNavigation nav
            ? nav.SectionKey
            : "Overview";
        UpdateContentVisibility();
    }

    private void UpdateContentVisibility()
    {
        var isInDetailPage = PageViewModel.IsInDetailPage;

        _chrome.OverviewContent.Visibility = isInDetailPage ? Visibility.Collapsed : Visibility.Visible;
        _chrome.ContentFrame.Visibility = isInDetailPage ? Visibility.Visible : Visibility.Collapsed;
        _chrome.BreadcrumbSeparator.Visibility = isInDetailPage ? Visibility.Visible : Visibility.Collapsed;
        _chrome.BreadcrumbSection.Visibility = isInDetailPage ? Visibility.Visible : Visibility.Collapsed;

        if (isInDetailPage)
        {
            // The icon and review badge beside this are bound to ViewModel.CurrentSectionItem.
            _chrome.BreadcrumbSectionText.Text = PageViewModel.CurrentSectionName;
            AutomationProperties.SetName(_chrome.BreadcrumbSection, PageViewModel.CurrentSectionName);
        }
    }

    protected void SectionCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string sectionKey })
            NavigateToSection(sectionKey);
    }

    protected void SectionFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        _chrome.BreadcrumbFlyout.Hide();
        if (sender is FrameworkElement { Tag: string sectionKey })
            NavigateToSection(sectionKey);
    }

    protected void BreadcrumbOverview_Click(object sender, RoutedEventArgs e) => NavigateToOverview();

    protected void OnOutcomeBannerNavigationRequested(object? sender, FeatureOutcomeNavigationEventArgs e)
        => NavigateToSection(e.SectionKey, e.SettingName);

    private void OnReviewModeChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(UpdateQuickActionsForReviewMode);
    }

    private void OnApplicationModeChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(UpdateQuickActionsForReviewMode);
    }

    // Suggestions carry the section that holds the setting, so this resolves without consulting the ViewModel.
    protected void SearchBox_SuggestionChosen(
        AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is SearchSuggestionItem suggestion)
            NavigateToSection(suggestion.SectionKey, suggestion.SettingName);
    }

    // Only acts on a chosen suggestion - a free-text query has no section to navigate to, and the live filter is
    // already applied by the two-way binding.
    protected void SearchBox_QuerySubmitted(
        AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is SearchSuggestionItem suggestion)
            NavigateToSection(suggestion.SectionKey, suggestion.SettingName);
    }

    private string Localized(string key, string fallback) =>
        _localizationService?.TryGetString(key, out var value) == true ? value : fallback;

    private void SetDropdownLabels()
    {
        _chrome.QuickActionsLabel.Text = Localized("QuickActions_Menu", "Quick Actions");
        AutomationProperties.SetName(_chrome.QuickActionsButton, _chrome.QuickActionsLabel.Text);
        _chrome.ViewMenuLabel.Text = Localized("View_Menu", "View");
        AutomationProperties.SetName(_chrome.ViewMenuButton, _chrome.ViewMenuLabel.Text);

        _chrome.TechnicalDetailsToggle.Text = Localized("View_TechnicalDetails", "Technical Details");
        ToolTipService.SetToolTip(_chrome.TechnicalDetailsToggle,
            Localized("View_TechnicalDetails_Tooltip", "Show or hide technical details for each setting"));
        _chrome.InfoBadgesToggle.Text = Localized("View_InfoBadges", "InfoBadges");
        ToolTipService.SetToolTip(_chrome.InfoBadgesToggle,
            Localized("View_InfoBadges_Tooltip", "Show or hide status badges on settings cards"));
        _chrome.NewBadgesToggle.Text = Localized("View_NewBadges", "NEW Badges");
        ToolTipService.SetToolTip(_chrome.NewBadgesToggle,
            Localized("View_NewBadges_Tooltip", "Show or hide NEW badges on settings added in this release"));
        _chrome.ShowOnlyChangesToggle.Text = Localized("View_ShowOnlyChanges", "Show Only Changes");
        ToolTipService.SetToolTip(_chrome.ShowOnlyChangesToggle,
            Localized("View_ShowOnlyChanges_Tooltip", "Show only settings with pending changes from the imported config"));
        _chrome.StateFilterAllItem.Text = Localized("View_StateFilter_All", "All settings");
        _chrome.StateFilterRecommendedItem.Text = Localized("InfoBadge_Recommended", "Recommended");
        _chrome.StateFilterWindowsDefaultItem.Text = Localized("View_StateFilter_WindowsDefault", "Windows default");
        _chrome.StateFilterCustomItem.Text = Localized("View_StateFilter_Custom", "Custom");

        _chrome.IncludeAllItem.Text = Localized("QuickActions_IncludeAll", "Include All Settings");
        _chrome.ExcludeAllItem.Text = Localized("QuickActions_ExcludeAll", "Exclude All Settings");

        UpdateQuickActionsForReviewMode();
    }

    private IEnumerable<SettingItemViewModel> AllSettings() =>
        PageViewModel.OverviewItems.SelectMany(item => item.Feature.Settings);

    private async Task InitializeViewTogglesAsync()
    {
        if (_userPreferencesService != null)
        {
            _isTechnicalDetailsVisible = await _userPreferencesService.GetPreferenceAsync(
                UserPreferenceKeys.ShowTechnicalDetails, false);
            _isInfoBadgesVisible = await _userPreferencesService.GetPreferenceAsync(
                UserPreferenceKeys.ShowInfoBadges, true);
            _isNewBadgesVisible = await _userPreferencesService.GetPreferenceAsync(
                UserPreferenceKeys.ShowNewBadges, true);
            if (!IsAnswerFilePage)
            {
                var savedStateFilter = await _userPreferencesService.GetPreferenceAsync(
                    UserPreferenceKeys.SettingsStateFilter, nameof(SettingStateFilter.All));
                _stateFilter = Enum.TryParse<SettingStateFilter>(savedStateFilter, out var stateFilter) && Enum.IsDefined(stateFilter)
                    ? stateFilter
                    : SettingStateFilter.All;
            }
        }

        _chrome.TechnicalDetailsToggle.IsChecked = _isTechnicalDetailsVisible;
        _chrome.InfoBadgesToggle.IsChecked = _isInfoBadgesVisible;
        _chrome.NewBadgesToggle.IsChecked = _isNewBadgesVisible;
        foreach (var (item, filter) in _stateFilterItems)
            item.IsChecked = filter == _stateFilter;

        SyncViewStateToSettings();
        ApplyViewFilter();
    }

    private void SyncViewStateToSettings()
    {
        foreach (var setting in AllSettings())
        {
            setting.IsInfoBadgeGloballyVisible = _isInfoBadgesVisible;
            setting.IsNewBadgeGloballyVisible = _isNewBadgesVisible;
            setting.IsTechnicalDetailsGloballyVisible = _isTechnicalDetailsVisible;
        }

        foreach (var item in PageViewModel.OverviewItems)
        {
            item.AreInfoBadgesVisible = _isInfoBadgesVisible;
            item.AreNewBadgesVisible = _isNewBadgesVisible;
        }
    }

    protected async void ViewTechnicalDetails_Click(object sender, RoutedEventArgs e)
    {
        _isTechnicalDetailsVisible = _chrome.TechnicalDetailsToggle.IsChecked;
        SyncViewStateToSettings();
        await SavePreferenceAsync(UserPreferenceKeys.ShowTechnicalDetails, _isTechnicalDetailsVisible);
    }

    protected async void ViewInfoBadges_Click(object sender, RoutedEventArgs e)
    {
        _isInfoBadgesVisible = _chrome.InfoBadgesToggle.IsChecked;
        SyncViewStateToSettings();
        await SavePreferenceAsync(UserPreferenceKeys.ShowInfoBadges, _isInfoBadgesVisible);
    }

    protected async void ViewNewBadges_Click(object sender, RoutedEventArgs e)
    {
        _isNewBadgesVisible = _chrome.NewBadgesToggle.IsChecked;
        SyncViewStateToSettings();
        await SavePreferenceAsync(UserPreferenceKeys.ShowNewBadges, _isNewBadgesVisible);
    }

    private async Task SavePreferenceAsync<T>(string key, T value)
    {
        if (_userPreferencesService != null)
            await _userPreferencesService.SetPreferenceAsync(key, value);
    }

    protected async void ViewStateFilter_Click(object sender, RoutedEventArgs e)
    {
        _stateFilter = _stateFilterItems.First(pair => ReferenceEquals(pair.Item, sender)).Filter;
        ApplyViewFilter();
        await SavePreferenceAsync(UserPreferenceKeys.SettingsStateFilter, _stateFilter.ToString());
    }

    protected void ViewShowOnlyChanges_Click(object sender, RoutedEventArgs e)
    {
        _showOnlyChanges = _chrome.ShowOnlyChangesToggle.IsChecked;
        ApplyViewFilter();
    }

    // Each feature owns its cards' visibility, so the filter is handed down and the feature combines it with its
    // own search; a section opened later already holds it.
    private void ApplyViewFilter()
    {
        foreach (var (item, _) in _stateFilterItems)
            item.IsEnabled = !_showOnlyChanges;

        // Read from the service's diff dictionary, the source behind TotalChanges and the Apply gate. Per-card
        // flags lag behind it for a section whose ViewModels are not hydrated yet, and would hide rows the user
        // still has to review (#665).
        Func<string, bool>? onlyChanges = _showOnlyChanges
            ? id => ReviewModeFilter.ShouldShowInReviewQueue(id, _configReviewService)
            : null;

        foreach (var item in PageViewModel.OverviewItems)
            item.Feature.SetViewFilter(_stateFilter, onlyChanges);
        PageViewModel.StateFilter = _stateFilter;
    }

    // The open section's settings when one is open, otherwise every section's.
    private IEnumerable<SettingItemViewModel> SettingsInScope()
    {
        var items = PageViewModel.IsInDetailPage
            ? PageViewModel.OverviewItems.Where(i => i.SectionKey == PageViewModel.CurrentSectionKey)
            : PageViewModel.OverviewItems;

        return items.SelectMany(i => i.Feature.Settings);
    }

    // The try/catch is not optional here: async void means an escaped exception is unobserved and
    // takes the process down. Every path below ends at ContentDialog.ShowAsync(), which throws when a
    // second dialog opens while one is already showing - a double-click on the flyout item.

    protected async void ApplyRecommended_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_configReviewService?.IsInReviewMode == true)
                await ExecuteReviewBulkActionAsync(approved: true);
            else if (_applicationModeService.Capabilities().AuthorsIntent)
                await ExecuteBuilderBulkActionAsync(BulkActionType.ApplyRecommended);
            else
                await ExecuteBulkActionAsync(BulkActionType.ApplyRecommended);
        }
        catch (Exception ex)
        {
            LogStartup($"ApplyRecommended_Click EXCEPTION: {ex}");
        }
    }

    protected async void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_configReviewService?.IsInReviewMode == true)
                await ExecuteReviewBulkActionAsync(approved: false);
            else if (_applicationModeService.Capabilities().AuthorsIntent)
                await ExecuteBuilderBulkActionAsync(BulkActionType.ResetToDefaults);
            else
                await ExecuteBulkActionAsync(BulkActionType.ResetToDefaults);
        }
        catch (Exception ex)
        {
            LogStartup($"ResetDefaults_Click EXCEPTION: {ex}");
        }
    }

    private async Task<bool> ConfirmAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = Localized("QuickActions_ConfirmTitle", "Confirm Action"),
            Content = message,
            PrimaryButtonText = Localized("Button_OK", "OK"),
            CloseButtonText = Localized("Button_Cancel", "Cancel"),
            XamlRoot = this.XamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    // UI only: each setting routes through the same guarded pipeline as the per-card quick-set buttons, which in
    // Builder mode records an edit instead of applying.
    private async Task ExecuteBuilderBulkActionAsync(BulkActionType actionType)
    {
        bool recommended = actionType == BulkActionType.ApplyRecommended;
        var settings = SettingsInScope()
            .Where(s => PageViewModel.TakesQuickActions(s) && s.WouldQuickSetChange(recommended))
            .ToList();
        if (settings.Count == 0) return;

        if (!await ConfirmAsync(string.Format(
                Localized("QuickActions_ConfirmMessage", "This will change {0} settings on this page. Continue?"),
                settings.Count)))
            return;

        foreach (var setting in settings)
        {
            if (recommended) setting.TrySetToRecommended();
            else setting.TrySetToDefault();
        }

        // No explicit re-aggregation here: the overview cards observe these settings, so a
        // Builder edit reaches them even though nothing publishes SettingAppliedEvent.
    }

    private async Task ExecuteBulkActionAsync(BulkActionType actionType)
    {
        if (_bulkSettingsActionService == null) return;

        // Cards already at the target are left out, so the count is what changes. The service then drops any
        // the PC cannot take (another Windows version shown with the version filter off, other hardware).
        bool recommended = actionType == BulkActionType.ApplyRecommended;
        var settingIds = SettingsInScope()
            .Where(s => s.WouldQuickSetChange(recommended))
            .Select(s => s.SettingId)
            .ToList();

        var count = await _bulkSettingsActionService.GetAffectedCountAsync(settingIds, actionType);
        if (count == 0) return;

        if (!await ConfirmAsync(string.Format(
                Localized("QuickActions_ConfirmMessage", "This will change {0} settings on this page. Continue?"),
                count)))
            return;

        if (actionType == BulkActionType.ApplyRecommended)
            await _bulkSettingsActionService.ApplyRecommendedAsync(settingIds);
        else
            await _bulkSettingsActionService.ResetToDefaultsAsync(settingIds);
    }

    private async Task ExecuteReviewBulkActionAsync(bool approved)
    {
        if (_configReviewService == null) return;

        var settingIds = SettingIdsInScope();
        int diffCount = settingIds.Count(id => _configReviewService.GetDiffForSetting(id) != null);
        if (diffCount == 0) return;

        var messageKey = approved ? "QuickActions_AcceptConfirmMessage" : "QuickActions_RejectConfirmMessage";
        var fallback = approved
            ? "This will accept {0} changes on this page. Continue?"
            : "This will reject {0} changes on this page. Continue?";

        if (!await ConfirmAsync(string.Format(Localized(messageKey, fallback), diffCount)))
            return;

        foreach (var id in settingIds)
        {
            var diff = _configReviewService.GetDiffForSetting(id);
            if (diff == null) continue;

            _configReviewService.SetSettingApproval(id, approved);

            if (diff.IsActionSetting)
                _configReviewService.SetActionApproval(id, approved);

            UpdateSettingViewModelReviewState(id, approved);
        }
    }

    private void UpdateSettingViewModelReviewState(string settingId, bool approved)
    {
        foreach (var setting in SettingsInScope())
        {
            if (setting.SettingId != settingId) continue;

            if (setting.HasReviewDiff)
            {
                setting.IsReviewApproved = approved;
                setting.IsReviewRejected = !approved;
            }
            if (setting.HasReviewAction)
            {
                setting.IsReviewActionApproved = approved;
                setting.IsReviewActionRejected = !approved;
            }
            return;
        }
    }

    private List<string> SettingIdsInScope() =>
        SettingsInScope()
            .Select(s => s.SettingId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();

    private void UpdateQuickActionsForReviewMode()
    {
        if (_configReviewService?.IsInReviewMode == true)
        {
            _chrome.ApplyRecommendedItem.Text = Localized("QuickActions_AcceptAll", "Accept All Changes");
            _chrome.ResetDefaultsItem.Text = Localized("QuickActions_RejectAll", "Reject All Changes");
            // Swap icons: Accept = checkmark (E73E), Reject = dismiss (E711)
            _chrome.ApplyRecommendedIcon.Glyph = "";
            _chrome.ResetDefaultsItem.Icon = new FontIcon { Glyph = "", FontSize = 14 };
            _chrome.ShowOnlyChangesSeparator.Visibility = Visibility.Visible;
            _chrome.ShowOnlyChangesToggle.Visibility = Visibility.Visible;
        }
        else
        {
            _chrome.ApplyRecommendedItem.Text = Localized("QuickActions_ApplyRecommended", "Apply Recommended Settings");
            _chrome.ResetDefaultsItem.Text = Localized("QuickActions_ResetDefaults", "Reset to Windows Defaults");
            _chrome.ApplyRecommendedIcon.Glyph = "";
            _chrome.ResetDefaultsItem.Icon = new PathIcon
            {
                Data = GeometryHelper.FromResource("WindowsLogoIconPath")
            };
            _chrome.ShowOnlyChangesSeparator.Visibility = Visibility.Collapsed;
            _chrome.ShowOnlyChangesToggle.Visibility = Visibility.Collapsed;
            _chrome.ShowOnlyChangesToggle.IsChecked = false;
            if (_showOnlyChanges)
            {
                _showOnlyChanges = false;
                ApplyViewFilter();
            }
        }

        UpdateIncludeActionsForAuthoring();
    }

    private bool IsAnswerFilePage => PageViewModel.OverviewItems
        .All(item => FeatureDefinitions.AutounattendFeatures.Contains(item.FeatureId));

    // An answer-file setting is always in the file, so a page of them has nothing to include or leave out.
    private void UpdateIncludeActionsForAuthoring()
    {
        var visibility = _applicationModeService.Capabilities().AuthorsIntent && !IsAnswerFilePage
            ? Visibility.Visible
            : Visibility.Collapsed;

        _chrome.IncludeActionsSeparator.Visibility = visibility;
        _chrome.IncludeAllItem.Visibility = visibility;
        _chrome.ExcludeAllItem.Visibility = visibility;
    }

    protected void IncludeAll_Click(object sender, RoutedEventArgs e) => SetIncludedInScope(true);

    protected void ExcludeAll_Click(object sender, RoutedEventArgs e) => SetIncludedInScope(false);

    private void SetIncludedInScope(bool included)
    {
        // Feature.Settings is the flat list, children included, so each gets IsIncluded directly; IncludeState
        // would cascade over children this loop already reaches.
        foreach (var setting in SettingsInScope())
        {
            setting.IsIncluded = included;
        }
    }
}
