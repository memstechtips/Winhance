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
    ToggleMenuFlyoutItem ShowOnlyChangesToggle,
    MenuFlyoutSeparator ShowOnlyChangesSeparator);

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

    protected abstract string LogTag { get; }

    // x:Bind in the XAML still resolves against each page's concrete property, so bindings are unaffected by this indirection.
    protected abstract ISectionPageViewModel PageViewModel { get; }

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
        chrome.TechnicalDetailsToggle.Click += ViewTechnicalDetails_Click;
        chrome.InfoBadgesToggle.Click += ViewInfoBadges_Click;
        chrome.NewBadgesToggle.Click += ViewNewBadges_Click;
        chrome.ShowOnlyChangesToggle.Click += ViewShowOnlyChanges_Click;
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
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        try
        {
            StartupLogger.Log(LogTag, "OnNavigatedTo starting...");
            base.OnNavigatedTo(e);

            // Re-subscribe in case OnNavigatedFrom unsubscribed (page is cached)
            if (_configReviewService != null)
            {
                _configReviewService.ReviewModeChanged -= OnReviewModeChanged;
                _configReviewService.ReviewModeChanged += OnReviewModeChanged;
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

            StartupLogger.Log(LogTag, "Calling ViewModel.InitializeAsync...");
            await PageViewModel.InitializeAsync();

            SetDropdownLabels();
            await InitializeViewTogglesAsync();

            if (_showOnlyChanges)
                ApplyShowOnlyChangesFilter();

            StartupLogger.Log(LogTag, "OnNavigatedTo complete");
        }
        catch (Exception ex)
        {
            StartupLogger.Log(LogTag, $"OnNavigatedTo EXCEPTION: {ex}");
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        if (_configReviewService != null)
        {
            _configReviewService.ReviewModeChanged -= OnReviewModeChanged;
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

        // Cascade the "Show Only Changes" filter to the newly navigated sub-page
        // so it doesn't need a toggle off/on to re-apply.
        if (_showOnlyChanges)
            ApplyShowOnlyChangesFilter();
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
        }

        _chrome.TechnicalDetailsToggle.IsChecked = _isTechnicalDetailsVisible;
        _chrome.InfoBadgesToggle.IsChecked = _isInfoBadgesVisible;
        _chrome.NewBadgesToggle.IsChecked = _isNewBadgesVisible;

        SyncViewStateToSettings();
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

    private async Task SavePreferenceAsync(string key, bool value)
    {
        if (_userPreferencesService != null)
            await _userPreferencesService.SetPreferenceAsync(key, value);
    }

    protected void ViewShowOnlyChanges_Click(object sender, RoutedEventArgs e)
    {
        _showOnlyChanges = _chrome.ShowOnlyChangesToggle.IsChecked;
        ApplyShowOnlyChangesFilter();
    }

    private void ApplyShowOnlyChangesFilter()
    {
        foreach (var setting in SettingsInScope())
        {
            if (_showOnlyChanges)
            {
                // Visibility is decided by the service's diff dictionary — the same
                // source that drives TotalChanges / ReviewedChanges and the Apply gate.
                // Per-VM flags lag behind the service when a sub-page's ViewModels haven't been hydrated
                // yet and would hide rows the user still needs to review (issue #665). See ReviewModeFilter
                // for context.
                setting.IsVisible = ReviewModeFilter.ShouldShowInReviewQueue(
                    setting.SettingId, _configReviewService);
            }
            else
            {
                setting.UpdateVisibility(PageViewModel.SearchText ?? string.Empty);
            }
        }
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
            StartupLogger.Log(LogTag, $"ApplyRecommended_Click EXCEPTION: {ex}");
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
            StartupLogger.Log(LogTag, $"ResetDefaults_Click EXCEPTION: {ex}");
        }
    }

    private async Task<bool> ConfirmAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = Localized("QuickActions_ConfirmTitle", "Confirm Action"),
            Content = message,
            PrimaryButtonText = "OK",
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
        var settings = SettingsInScope().ToList();

        int count = settings.Count(s => recommended ? s.HasRecommendedQuickSetTarget : s.HasDefaultQuickSetTarget);
        if (count == 0) return;

        if (!await ConfirmAsync(string.Format(
                Localized("QuickActions_ConfirmMessage", "This will change {0} settings on this page. Continue?"),
                count)))
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

        var settingIds = SettingIdsInScope();

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
                ApplyShowOnlyChangesFilter();
            }
        }
    }
}
