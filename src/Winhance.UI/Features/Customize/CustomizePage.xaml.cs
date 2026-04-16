using System.ComponentModel;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Services;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Common.Interfaces;
using IConfigReviewService = Winhance.Core.Features.Common.Interfaces.IConfigReviewService;
using ILocalizationService = Winhance.Core.Features.Common.Interfaces.ILocalizationService;
using IUserPreferencesService = Winhance.Core.Features.Common.Interfaces.IUserPreferencesService;
using IBulkSettingsActionService = Winhance.Core.Features.Common.Interfaces.IBulkSettingsActionService;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Customize.Pages;
using Winhance.UI.Features.Customize.ViewModels;

namespace Winhance.UI.Features.Customize;

public sealed partial class CustomizePage : Page
{
    private static readonly Dictionary<string, string> SectionIconResourceKeys = new()
    {
        { "Explorer", "ExplorerIconPath" },
        { "StartMenu", "StartMenuIconPath" },
        { "Taskbar", "TaskbarIconPath" },
        { "WindowsTheme", "WindowsThemeIconPath" }
    };

    // Maps section keys to feature IDs for badge tracking
    private static readonly Dictionary<string, string> SectionFeatureIds = new()
    {
        { "Explorer", FeatureIds.ExplorerCustomization },
        { "StartMenu", FeatureIds.StartMenu },
        { "Taskbar", FeatureIds.Taskbar },
        { "WindowsTheme", FeatureIds.WindowsTheme }
    };

    private IConfigReviewService? _configReviewService;
    private IUserPreferencesService? _userPreferencesService;
    private ILocalizationService? _localizationService;
    private IBulkSettingsActionService? _bulkSettingsActionService;
    private Dictionary<string, InfoBadge>? _flyoutBadges;
    private bool _isTechnicalDetailsVisible;
    private bool _isInfoBadgesVisible = true;
    private bool _isNewBadgesVisible = true;
    private bool _showOnlyChanges;
    private ISubscriptionToken? _settingAppliedSubscription;

    public CustomizeViewModel ViewModel { get; }

    public CustomizePage()
    {
        try
        {
            StartupLogger.Log("CustomizePage", "Constructor starting...");
            this.InitializeComponent();
            StartupLogger.Log("CustomizePage", "InitializeComponent done, getting ViewModel...");
            ViewModel = App.Services.GetRequiredService<CustomizeViewModel>();
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateBreadcrumbMenuItems();

            _flyoutBadges = new()
            {
                { "WindowsTheme", FlyoutBadgeWindowsTheme },
                { "Taskbar", FlyoutBadgeTaskbar },
                { "StartMenu", FlyoutBadgeStartMenu },
                { "Explorer", FlyoutBadgeExplorer }
            };

            _configReviewService = App.Services.GetService<IConfigReviewService>();
            if (_configReviewService != null)
            {
                _configReviewService.ReviewModeChanged += OnReviewModeChanged;
                _configReviewService.BadgeStateChanged += OnBadgeStateChanged;
            }

            _userPreferencesService = App.Services.GetService<IUserPreferencesService>();
            _localizationService = App.Services.GetService<ILocalizationService>();
            _bulkSettingsActionService = App.Services.GetService<IBulkSettingsActionService>();

            StartupLogger.Log("CustomizePage", "ViewModel obtained, constructor complete");
        }
        catch (Exception ex)
        {
            StartupLogger.Log("CustomizePage", $"Constructor EXCEPTION: {ex}");
            throw;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.BreadcrumbRootText))
        {
            UpdateBreadcrumbMenuItems();
        }
    }

    private void UpdateBreadcrumbMenuItems()
    {
        SetFlyoutButtonText(FlyoutTextWindowsTheme, FlyoutButtonWindowsTheme, "WindowsTheme");
        SetFlyoutButtonText(FlyoutTextTaskbar, FlyoutButtonTaskbar, "Taskbar");
        SetFlyoutButtonText(FlyoutTextStartMenu, FlyoutButtonStartMenu, "StartMenu");
        SetFlyoutButtonText(FlyoutTextExplorer, FlyoutButtonExplorer, "Explorer");
    }

    private void SetFlyoutButtonText(TextBlock textBlock, Button button, string sectionKey)
    {
        var displayName = ViewModel.GetSectionDisplayName(sectionKey);
        textBlock.Text = displayName;
        AutomationProperties.SetName(button, displayName);
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        try
        {
            StartupLogger.Log("CustomizePage", "OnNavigatedTo starting...");
            base.OnNavigatedTo(e);

            // Re-subscribe in case OnNavigatedFrom unsubscribed (page is cached)
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            if (_configReviewService != null)
            {
                _configReviewService.ReviewModeChanged -= OnReviewModeChanged;
                _configReviewService.ReviewModeChanged += OnReviewModeChanged;
                _configReviewService.BadgeStateChanged -= OnBadgeStateChanged;
                _configReviewService.BadgeStateChanged += OnBadgeStateChanged;
            }

            var eventBus = App.Services.GetService<IEventBus>();
            if (eventBus != null)
            {
                _settingAppliedSubscription?.Dispose();
                _settingAppliedSubscription = eventBus.Subscribe<SettingAppliedEvent>(e =>
                {
                    DispatcherQueue.TryEnqueue(() => UpdateOverviewBadgePills());
                });
            }

            UpdateBreadcrumbMenuItems();

            // Ensure we're showing overview on initial navigation
            ViewModel.CurrentSectionKey = "Overview";
            UpdateContentVisibility();

            StartupLogger.Log("CustomizePage", "Calling ViewModel.InitializeAsync...");
            await ViewModel.InitializeAsync();

            // Set localized labels for dropdown menus
            SetDropdownLabels();

            // Initialize technical details toggle state
            await InitializeTechnicalDetailsToggleAsync();

            // Initialize info badges toggle state
            await InitializeInfoBadgesAsync();

            // Initialize new badges toggle state
            await InitializeNewBadgesAsync();

            // Always update badges: shows them if in review mode, collapses them if not
            UpdateOverviewBadges();
            UpdateBreadcrumbBadges();
            UpdateOverviewBadgePills();

            // Re-apply Show Only Changes filter if still active from before navigation
            if (_showOnlyChanges)
                ApplyShowOnlyChangesFilter();

            StartupLogger.Log("CustomizePage", "OnNavigatedTo complete");
        }
        catch (Exception ex)
        {
            StartupLogger.Log("CustomizePage", $"OnNavigatedTo EXCEPTION: {ex}");
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        if (_configReviewService != null)
        {
            _configReviewService.ReviewModeChanged -= OnReviewModeChanged;
            _configReviewService.BadgeStateChanged -= OnBadgeStateChanged;
        }
        _settingAppliedSubscription?.Dispose();
        _settingAppliedSubscription = null;
        ViewModel.OnNavigatedFrom();
    }

    public void NavigateToSection(string sectionKey, string? searchText = null)
    {
        Type? pageType = sectionKey switch
        {
            "Explorer" => typeof(ExplorerCustomizePage),
            "StartMenu" => typeof(StartMenuCustomizePage),
            "Taskbar" => typeof(TaskbarCustomizePage),
            "WindowsTheme" => typeof(WindowsThemeCustomizePage),
            _ => null
        };

        if (pageType != null)
        {
            // Pre-apply search filter before navigation
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var targetViewModel = ViewModel.GetSectionViewModel(sectionKey);
                targetViewModel?.ApplySearchFilter(searchText);
            }

            InnerContentFrame.Navigate(pageType, searchText);

            // Mark feature as visited when user actually navigates into it
            if (_configReviewService?.IsInReviewMode == true &&
                SectionFeatureIds.TryGetValue(sectionKey, out var featureId))
            {
                _configReviewService.MarkFeatureVisited(featureId);
            }
        }
        else
        {
            NavigateToOverview();
        }
    }

    public void NavigateToOverview()
    {
        ViewModel.CurrentSectionKey = "Overview";
        InnerContentFrame.Content = null;
        UpdateContentVisibility();
    }

    private void InnerContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        ViewModel.CurrentSectionKey = e.SourcePageType.Name switch
        {
            nameof(ExplorerCustomizePage) => "Explorer",
            nameof(StartMenuCustomizePage) => "StartMenu",
            nameof(TaskbarCustomizePage) => "Taskbar",
            nameof(WindowsThemeCustomizePage) => "WindowsTheme",
            _ => "Overview"
        };

        UpdateContentVisibility();
    }

    private void UpdateContentVisibility()
    {
        var isInDetailPage = ViewModel.IsInDetailPage;

        OverviewContent.Visibility = isInDetailPage ? Visibility.Collapsed : Visibility.Visible;
        InnerContentFrame.Visibility = isInDetailPage ? Visibility.Visible : Visibility.Collapsed;

        BreadcrumbSeparator.Visibility = isInDetailPage ? Visibility.Visible : Visibility.Collapsed;
        BreadcrumbSection.Visibility = isInDetailPage ? Visibility.Visible : Visibility.Collapsed;

        if (isInDetailPage)
        {
            BreadcrumbSectionText.Text = ViewModel.CurrentSectionName;
            AutomationProperties.SetName(BreadcrumbSection, ViewModel.CurrentSectionName);

            if (SectionIconResourceKeys.TryGetValue(ViewModel.CurrentSectionKey, out var resourceKey) &&
                Application.Current.Resources.TryGetValue(resourceKey, out var pathDataObj) &&
                pathDataObj is string pathData)
            {
                var geometry = (Microsoft.UI.Xaml.Media.Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(
                    typeof(Microsoft.UI.Xaml.Media.Geometry), pathData);
                BreadcrumbSectionIcon.Data = geometry;
            }

            UpdateBreadcrumbBadges();
        }
    }

    // Overview card click handlers
    private void ExplorerCard_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("Explorer");
    }

    private void StartMenuCard_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("StartMenu");
    }

    private void TaskbarCard_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("Taskbar");
    }

    private void WindowsThemeCard_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("WindowsTheme");
    }

    // Breadcrumb handlers
    private void BreadcrumbOverview_Click(object sender, RoutedEventArgs e)
    {
        NavigateToOverview();
    }

    private void NavigateExplorer_Click(object sender, RoutedEventArgs e)
    {
        BreadcrumbFlyout.Hide();
        NavigateToSection("Explorer");
    }

    private void NavigateStartMenu_Click(object sender, RoutedEventArgs e)
    {
        BreadcrumbFlyout.Hide();
        NavigateToSection("StartMenu");
    }

    private void NavigateTaskbar_Click(object sender, RoutedEventArgs e)
    {
        BreadcrumbFlyout.Hide();
        NavigateToSection("Taskbar");
    }

    private void NavigateWindowsTheme_Click(object sender, RoutedEventArgs e)
    {
        BreadcrumbFlyout.Hide();
        NavigateToSection("WindowsTheme");
    }

    // Review mode badge handlers
    private void OnReviewModeChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateOverviewBadges();
            UpdateBreadcrumbBadges();
            UpdateQuickActionsForReviewMode();
        });
    }

    private void OnBadgeStateChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => { UpdateOverviewBadges(); UpdateBreadcrumbBadges(); });
    }

    private void UpdateOverviewBadges()
    {
        if (_configReviewService == null || !_configReviewService.IsInReviewMode)
        {
            WindowsThemeBadge.Visibility = Visibility.Collapsed;
            TaskbarBadge.Visibility = Visibility.Collapsed;
            StartMenuBadge.Visibility = Visibility.Collapsed;
            ExplorerBadge.Visibility = Visibility.Collapsed;
            return;
        }

        UpdateFeatureBadge(WindowsThemeBadge, FeatureIds.WindowsTheme);
        UpdateFeatureBadge(TaskbarBadge, FeatureIds.Taskbar);
        UpdateFeatureBadge(StartMenuBadge, FeatureIds.StartMenu);
        UpdateFeatureBadge(ExplorerBadge, FeatureIds.ExplorerCustomization);
    }

    private void UpdateOverviewBadgePills()
    {
        UpdateFeatureOverviewPills(
            ViewModel.WindowsThemeViewModel,
            WindowsThemeOverviewBadges, WindowsThemeNewPill, WindowsThemeNewText,
            WindowsThemeRecommendedPill, WindowsThemeRecommendedText,
            WindowsThemeDefaultPill, WindowsThemeDefaultText,
            WindowsThemeCustomPill, WindowsThemeCustomText);
        UpdateFeatureOverviewPills(
            ViewModel.TaskbarViewModel,
            TaskbarOverviewBadges, TaskbarNewPill, TaskbarNewText,
            TaskbarRecommendedPill, TaskbarRecommendedText,
            TaskbarDefaultPill, TaskbarDefaultText,
            TaskbarCustomPill, TaskbarCustomText);
        UpdateFeatureOverviewPills(
            ViewModel.StartMenuViewModel,
            StartMenuOverviewBadges, StartMenuNewPill, StartMenuNewText,
            StartMenuRecommendedPill, StartMenuRecommendedText,
            StartMenuDefaultPill, StartMenuDefaultText,
            StartMenuCustomPill, StartMenuCustomText);
        UpdateFeatureOverviewPills(
            ViewModel.ExplorerViewModel,
            ExplorerOverviewBadges, ExplorerNewPill, ExplorerNewText,
            ExplorerRecommendedPill, ExplorerRecommendedText,
            ExplorerDefaultPill, ExplorerDefaultText,
            ExplorerCustomPill, ExplorerCustomText);
    }

    private void UpdateFeatureOverviewPills(
        ISettingsFeatureViewModel feature,
        StackPanel container,
        Border newPill, TextBlock newText,
        Border recommendedPill, TextBlock recommendedText,
        Border defaultPill, TextBlock defaultText,
        Border customPill, TextBlock customText)
    {
        if (!_isInfoBadgesVisible && !_isNewBadgesVisible)
        {
            container.Visibility = Visibility.Collapsed;
            return;
        }

        var summary = FeatureBadgeAggregator.Aggregate(feature);
        int total = summary.TotalWithBadgeData;
        bool showAny = false;

        // NEW pill
        if (_isNewBadgesVisible && summary.NewCount > 0)
        {
            newPill.Visibility = Visibility.Visible;
            newText.Text = $"{_localizationService?.GetString("Badge_New") ?? "NEW"} {summary.NewCount}";
            showAny = true;
        }
        else
        {
            newPill.Visibility = Visibility.Collapsed;
        }

        // InfoBadge pills
        if (_isInfoBadgesVisible && total > 0)
        {
            showAny = true;
            recommendedPill.Visibility = Visibility.Visible;
            recommendedText.Text = $"{_localizationService?.GetString("InfoBadge_Recommended") ?? "Recommended"} {summary.RecommendedCount}/{total}";
            recommendedPill.Opacity = summary.RecommendedCount > 0 ? 1.0 : 0.4;

            defaultPill.Visibility = Visibility.Visible;
            defaultText.Text = $"{_localizationService?.GetString("InfoBadge_Default") ?? "Default"} {summary.DefaultCount}/{total}";
            defaultPill.Opacity = summary.DefaultCount > 0 ? 1.0 : 0.4;

            if (summary.CustomCount > 0)
            {
                customPill.Visibility = Visibility.Visible;
                customText.Text = $"{_localizationService?.GetString("InfoBadge_Custom") ?? "Custom"} {summary.CustomCount}/{total}";
            }
            else
            {
                customPill.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            recommendedPill.Visibility = Visibility.Collapsed;
            defaultPill.Visibility = Visibility.Collapsed;
            customPill.Visibility = Visibility.Collapsed;
        }

        container.Visibility = showAny ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateBreadcrumbBadges()
    {
        if (_configReviewService == null || !_configReviewService.IsInReviewMode)
        {
            BreadcrumbSectionBadge.Visibility = Visibility.Collapsed;
            if (_flyoutBadges != null)
            {
                foreach (var badge in _flyoutBadges.Values)
                    badge.Visibility = Visibility.Collapsed;
            }
            return;
        }

        // Update all flyout item badges
        if (_flyoutBadges != null)
        {
            foreach (var (sectionKey, badge) in _flyoutBadges)
            {
                if (SectionFeatureIds.TryGetValue(sectionKey, out var featureId))
                    UpdateFeatureBadge(badge, featureId);
            }
        }

        // Update current section badge on DropDownButton
        if (SectionFeatureIds.TryGetValue(ViewModel.CurrentSectionKey, out var currentFeatureId))
            UpdateFeatureBadge(BreadcrumbSectionBadge, currentFeatureId);
        else
            BreadcrumbSectionBadge.Visibility = Visibility.Collapsed;
    }

    private void UpdateFeatureBadge(InfoBadge badge, string featureId)
    {
        var diffCount = _configReviewService!.GetFeatureDiffCount(featureId);
        if (diffCount > 0)
        {
            badge.Visibility = Visibility.Visible;

            if (_configReviewService.IsFeatureFullyReviewed(featureId))
            {
                // Fully reviewed: show checkmark icon only (no count number)
                badge.Value = -1;
                if (Application.Current.Resources.TryGetValue("SuccessIconInfoBadgeStyle", out var successStyle) && successStyle is Style ss)
                    badge.Style = ss;
            }
            else
            {
                // Not fully reviewed: show attention badge with pending (unreviewed) count
                var pendingCount = _configReviewService.GetFeaturePendingDiffCount(featureId);
                badge.Value = pendingCount;
                if (Application.Current.Resources.TryGetValue("AttentionValueInfoBadgeStyle", out var attentionStyle) && attentionStyle is Style ats)
                    badge.Style = ats;
            }
        }
        else if (_configReviewService.IsFeatureInConfig(featureId))
        {
            // Feature is in config but has 0 diffs - show success checkmark only
            badge.Value = -1;
            badge.Visibility = Visibility.Visible;

            if (Application.Current.Resources.TryGetValue("SuccessIconInfoBadgeStyle", out var style) && style is Style badgeStyle)
            {
                badge.Style = badgeStyle;
            }
        }
        else
        {
            badge.Visibility = Visibility.Collapsed;
        }
    }

    // Dropdown menu labels
    private void SetDropdownLabels()
    {
        QuickActionsLabel.Text = _localizationService?.GetString("QuickActions_Menu") ?? "Quick Actions";
        ApplyRecommendedItem.Text = _localizationService?.GetString("QuickActions_ApplyRecommended") ?? "Apply Recommended Settings";
        ResetDefaultsItem.Text = _localizationService?.GetString("QuickActions_ResetDefaults") ?? "Reset to Windows Defaults";
        ViewMenuLabel.Text = _localizationService?.GetString("View_Menu") ?? "View";
        TechnicalDetailsToggleItem.Text = _localizationService?.GetString("View_TechnicalDetails") ?? "Technical Details";
        ToolTipService.SetToolTip(TechnicalDetailsToggleItem, _localizationService?.GetString("View_TechnicalDetails_Tooltip") ?? "Show or hide technical details for each setting");
        InfoBadgesToggleItem.Text = _localizationService?.GetString("View_InfoBadges") ?? "InfoBadges";
        ToolTipService.SetToolTip(InfoBadgesToggleItem, _localizationService?.GetString("View_InfoBadges_Tooltip") ?? "Show or hide status badges on settings cards");
        NewBadgesToggleItem.Text = _localizationService?.GetString("View_NewBadges") ?? "NEW Badges";
        ToolTipService.SetToolTip(NewBadgesToggleItem, _localizationService?.GetString("View_NewBadges_Tooltip") ?? "Show or hide NEW badges on settings added in this release");
        ShowOnlyChangesToggleItem.Text = _localizationService?.GetString("View_ShowOnlyChanges") ?? "Show Only Changes";
        ToolTipService.SetToolTip(ShowOnlyChangesToggleItem, _localizationService?.GetString("View_ShowOnlyChanges_Tooltip") ?? "Show only settings with pending changes from the imported config");
        UpdateQuickActionsForReviewMode();
    }

    // Technical Details toggle
    private async Task InitializeTechnicalDetailsToggleAsync()
    {
        if (_userPreferencesService != null)
        {
            _isTechnicalDetailsVisible = await _userPreferencesService.GetPreferenceAsync(
                UserPreferenceKeys.ShowTechnicalDetails, false);
        }

        // Sync all settings (handles cross-page toggle changes)
        foreach (var section in CustomizeViewModel.Sections)
        {
            var sectionVm = ViewModel.GetSectionViewModel(section.Key);
            if (sectionVm == null) continue;
            foreach (var setting in sectionVm.Settings)
            {
                setting.IsTechnicalDetailsGloballyVisible = _isTechnicalDetailsVisible;
            }
        }

        TechnicalDetailsToggleItem.IsChecked = _isTechnicalDetailsVisible;
    }

    private async Task InitializeInfoBadgesAsync()
    {
        if (_userPreferencesService != null)
        {
            _isInfoBadgesVisible = await _userPreferencesService.GetPreferenceAsync(
                UserPreferenceKeys.ShowInfoBadges, true);
        }

        // Sync all settings
        foreach (var section in CustomizeViewModel.Sections)
        {
            var sectionVm = ViewModel.GetSectionViewModel(section.Key);
            if (sectionVm == null) continue;
            foreach (var setting in sectionVm.Settings)
            {
                setting.IsInfoBadgeGloballyVisible = _isInfoBadgesVisible;
            }
        }

        InfoBadgesToggleItem.IsChecked = _isInfoBadgesVisible;
    }

    private async Task InitializeNewBadgesAsync()
    {
        if (_userPreferencesService != null)
        {
            _isNewBadgesVisible = await _userPreferencesService.GetPreferenceAsync(
                UserPreferenceKeys.ShowNewBadges, true);
        }

        // Sync all settings
        foreach (var section in CustomizeViewModel.Sections)
        {
            var sectionVm = ViewModel.GetSectionViewModel(section.Key);
            if (sectionVm == null) continue;
            foreach (var setting in sectionVm.Settings)
            {
                setting.IsNewBadgeGloballyVisible = _isNewBadgesVisible;
            }
        }

        NewBadgesToggleItem.IsChecked = _isNewBadgesVisible;
    }

    // View menu handlers
    private async void ViewTechnicalDetails_Click(object sender, RoutedEventArgs e)
    {
        _isTechnicalDetailsVisible = TechnicalDetailsToggleItem.IsChecked;

        // Update all settings across all sections
        foreach (var section in CustomizeViewModel.Sections)
        {
            var sectionVm = ViewModel.GetSectionViewModel(section.Key);
            if (sectionVm == null) continue;
            foreach (var setting in sectionVm.Settings)
            {
                setting.IsTechnicalDetailsGloballyVisible = _isTechnicalDetailsVisible;
            }
        }

        if (_userPreferencesService != null)
        {
            await _userPreferencesService.SetPreferenceAsync(
                UserPreferenceKeys.ShowTechnicalDetails, _isTechnicalDetailsVisible);
        }
    }

    private async void ViewInfoBadges_Click(object sender, RoutedEventArgs e)
    {
        _isInfoBadgesVisible = InfoBadgesToggleItem.IsChecked;

        foreach (var section in CustomizeViewModel.Sections)
        {
            var sectionVm = ViewModel.GetSectionViewModel(section.Key);
            if (sectionVm == null) continue;
            foreach (var setting in sectionVm.Settings)
            {
                setting.IsInfoBadgeGloballyVisible = _isInfoBadgesVisible;
            }
        }

        if (_userPreferencesService != null)
        {
            await _userPreferencesService.SetPreferenceAsync(
                UserPreferenceKeys.ShowInfoBadges, _isInfoBadgesVisible);
        }

        UpdateOverviewBadgePills();
    }

    private async void ViewNewBadges_Click(object sender, RoutedEventArgs e)
    {
        _isNewBadgesVisible = NewBadgesToggleItem.IsChecked;

        foreach (var section in CustomizeViewModel.Sections)
        {
            var sectionVm = ViewModel.GetSectionViewModel(section.Key);
            if (sectionVm == null) continue;
            foreach (var setting in sectionVm.Settings)
            {
                setting.IsNewBadgeGloballyVisible = _isNewBadgesVisible;
            }
        }

        if (_userPreferencesService != null)
        {
            await _userPreferencesService.SetPreferenceAsync(
                UserPreferenceKeys.ShowNewBadges, _isNewBadgesVisible);
        }

        UpdateOverviewBadgePills();
    }

    // Show Only Changes filter (review mode)
    private void ViewShowOnlyChanges_Click(object sender, RoutedEventArgs e)
    {
        _showOnlyChanges = ShowOnlyChangesToggleItem.IsChecked;
        ApplyShowOnlyChangesFilter();
    }

    private void ApplyShowOnlyChangesFilter()
    {
        var sectionsToFilter = ViewModel.IsInDetailPage
            ? CustomizeViewModel.Sections.Where(s => s.Key == ViewModel.CurrentSectionKey)
            : CustomizeViewModel.Sections;

        foreach (var section in sectionsToFilter)
        {
            var sectionVm = ViewModel.GetSectionViewModel(section.Key);
            if (sectionVm == null) continue;
            foreach (var setting in sectionVm.Settings)
            {
                if (_showOnlyChanges)
                {
                    setting.IsVisible = setting.HasReviewDiff || setting.HasReviewAction;
                }
                else
                {
                    setting.UpdateVisibility(ViewModel.SearchText ?? string.Empty);
                }
            }
        }
    }

    // Quick Actions handlers
    private async void ApplyRecommended_Click(object sender, RoutedEventArgs e)
    {
        if (_configReviewService?.IsInReviewMode == true)
            await ExecuteReviewBulkActionAsync(approved: true);
        else
            await ExecuteBulkActionAsync(BulkActionType.ApplyRecommended);
    }

    private async void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        if (_configReviewService?.IsInReviewMode == true)
            await ExecuteReviewBulkActionAsync(approved: false);
        else
            await ExecuteBulkActionAsync(BulkActionType.ResetToDefaults);
    }

    private async Task ExecuteBulkActionAsync(BulkActionType actionType)
    {
        if (_bulkSettingsActionService == null) return;

        var settingIds = GetCurrentPageSettingIds();

        var count = await _bulkSettingsActionService.GetAffectedCountAsync(settingIds, actionType);
        if (count == 0) return;

        var confirmMessage = string.Format(
            _localizationService?.GetString("QuickActions_ConfirmMessage") ?? "This will change {0} settings on this page. Continue?",
            count);

        var dialog = new ContentDialog
        {
            Title = _localizationService?.GetString("QuickActions_ConfirmTitle") ?? "Confirm Action",
            Content = confirmMessage,
            PrimaryButtonText = "OK",
            CloseButtonText = _localizationService?.GetString("Button_Cancel") ?? "Cancel",
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        int applied = actionType == BulkActionType.ApplyRecommended
            ? await _bulkSettingsActionService.ApplyRecommendedAsync(settingIds)
            : await _bulkSettingsActionService.ResetToDefaultsAsync(settingIds);
    }

    private async Task ExecuteReviewBulkActionAsync(bool approved)
    {
        if (_configReviewService == null) return;

        var settingIds = GetCurrentPageSettingIds();

        int diffCount = 0;
        foreach (var id in settingIds)
        {
            if (_configReviewService.GetDiffForSetting(id) != null)
                diffCount++;
        }

        if (diffCount == 0) return;

        var messageKey = approved ? "QuickActions_AcceptConfirmMessage" : "QuickActions_RejectConfirmMessage";
        var confirmMessage = string.Format(
            _localizationService?.GetString(messageKey) ?? (approved ? "This will accept {0} changes on this page. Continue?" : "This will reject {0} changes on this page. Continue?"),
            diffCount);

        var dialog = new ContentDialog
        {
            Title = _localizationService?.GetString("QuickActions_ConfirmTitle") ?? "Confirm Action",
            Content = confirmMessage,
            PrimaryButtonText = "OK",
            CloseButtonText = _localizationService?.GetString("Button_Cancel") ?? "Cancel",
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

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
        var sectionsToSearch = ViewModel.IsInDetailPage
            ? CustomizeViewModel.Sections.Where(s => s.Key == ViewModel.CurrentSectionKey)
            : CustomizeViewModel.Sections;

        foreach (var section in sectionsToSearch)
        {
            var sectionVm = ViewModel.GetSectionViewModel(section.Key);
            if (sectionVm == null) continue;
            foreach (var setting in sectionVm.Settings)
            {
                if (setting.SettingId == settingId)
                {
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
        }
    }

    private void UpdateQuickActionsForReviewMode()
    {
        if (_configReviewService?.IsInReviewMode == true)
        {
            ApplyRecommendedItem.Text = _localizationService?.GetString("QuickActions_AcceptAll") ?? "Accept All Changes";
            ResetDefaultsItem.Text = _localizationService?.GetString("QuickActions_RejectAll") ?? "Reject All Changes";
            // Swap icons: Accept = checkmark (E73E), Reject = dismiss (E711)
            ApplyRecommendedIcon.Glyph = "\uE73E";
            ResetDefaultsItem.Icon = new FontIcon { Glyph = "\uE711", FontSize = 14 };
            ShowOnlyChangesSeparator.Visibility = Visibility.Visible;
            ShowOnlyChangesToggleItem.Visibility = Visibility.Visible;
        }
        else
        {
            ApplyRecommendedItem.Text = _localizationService?.GetString("QuickActions_ApplyRecommended") ?? "Apply Recommended Settings";
            ResetDefaultsItem.Text = _localizationService?.GetString("QuickActions_ResetDefaults") ?? "Reset to Windows Defaults";
            ApplyRecommendedIcon.Glyph = "\uE735";
            ResetDefaultsItem.Icon = new PathIcon
            {
                Data = (Microsoft.UI.Xaml.Media.Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(
                    typeof(Microsoft.UI.Xaml.Media.Geometry),
                    (string)Application.Current.Resources["WindowsLogoIconPath"])
            };
            ShowOnlyChangesSeparator.Visibility = Visibility.Collapsed;
            ShowOnlyChangesToggleItem.Visibility = Visibility.Collapsed;
            ShowOnlyChangesToggleItem.IsChecked = false;
            if (_showOnlyChanges)
            {
                _showOnlyChanges = false;
                ApplyShowOnlyChangesFilter();
            }
        }
    }

    private List<string> GetCurrentPageSettingIds()
    {
        var settingIds = new List<string>();
        var sectionsToInclude = ViewModel.IsInDetailPage
            ? CustomizeViewModel.Sections.Where(s => s.Key == ViewModel.CurrentSectionKey)
            : CustomizeViewModel.Sections;

        foreach (var section in sectionsToInclude)
        {
            var sectionVm = ViewModel.GetSectionViewModel(section.Key);
            if (sectionVm == null) continue;
            foreach (var setting in sectionVm.Settings)
            {
                if (!string.IsNullOrEmpty(setting.SettingId))
                {
                    settingIds.Add(setting.SettingId);
                }
            }
        }

        return settingIds;
    }

    // Search handlers
    private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is SearchSuggestionItem suggestion)
        {
            NavigateToSection(suggestion.SectionKey, suggestion.SettingName);
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is SearchSuggestionItem suggestion)
        {
            NavigateToSection(suggestion.SectionKey, suggestion.SettingName);
        }
    }
}
