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
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Services;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Common.Interfaces;
using IConfigReviewService = Winhance.Core.Features.Common.Interfaces.IConfigReviewService;
using ILocalizationService = Winhance.Core.Features.Common.Interfaces.ILocalizationService;
using IUserPreferencesService = Winhance.Core.Features.Common.Interfaces.IUserPreferencesService;
using IBulkSettingsActionService = Winhance.Core.Features.Common.Interfaces.IBulkSettingsActionService;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Optimize.Pages;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Optimize;

public sealed partial class OptimizePage : Page
{
    // Maps section keys to their icon resource keys:
    // - "Path" suffix = Material Design SVG path for PathIcon
    // - "Symbol" suffix = FluentIcons.Common.Symbol enum name for SymbolIcon
    private static readonly Dictionary<string, string> SectionIconResourceKeys = new()
    {
        { "Privacy", "PrivacyIconPath" },
        { "Power", "PowerIconPath" },
        { "Gaming", "GamingIconPath" },
        { "Update", "UpdateIconSymbol" },
        { "Notification", "NotificationIconPath" },
        { "Sound", "SoundIconSymbol" }
    };

    // Maps section keys to feature IDs for badge tracking
    private static readonly Dictionary<string, string> SectionFeatureIds = new()
    {
        { "Privacy", FeatureIds.Privacy },
        { "Power", FeatureIds.Power },
        { "Gaming", FeatureIds.GamingPerformance },
        { "Update", FeatureIds.Update },
        { "Notification", FeatureIds.Notifications },
        { "Sound", FeatureIds.Sound }
    };

    private IConfigReviewService? _configReviewService;
    private IUserPreferencesService? _userPreferencesService;
    private ILocalizationService? _localizationService;
    private IBulkSettingsActionService? _bulkSettingsActionService;
    private Dictionary<string, InfoBadge>? _flyoutBadges;
    private ISubscriptionToken? _settingAppliedSubscription;
    private ISubscriptionToken? _settingsRefreshedSubscription;
    private bool _isTechnicalDetailsVisible;
    private bool _isInfoBadgesVisible = true;
    private bool _isNewBadgesVisible = true;
    private bool _showOnlyChanges;

    public OptimizeViewModel ViewModel { get; }

    public OptimizePage()
    {
        try
        {
            StartupLogger.Log("OptimizePage", "Constructor starting...");
            this.InitializeComponent();
            StartupLogger.Log("OptimizePage", "InitializeComponent done, getting ViewModel...");
            ViewModel = App.Services.GetRequiredService<OptimizeViewModel>();
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateBreadcrumbMenuItems();

            _flyoutBadges = new()
            {
                { "Privacy", FlyoutBadgePrivacy },
                { "Power", FlyoutBadgePower },
                { "Gaming", FlyoutBadgeGaming },
                { "Update", FlyoutBadgeUpdate },
                { "Notification", FlyoutBadgeNotification },
                { "Sound", FlyoutBadgeSound }
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

            StartupLogger.Log("OptimizePage", "ViewModel obtained, constructor complete");
        }
        catch (Exception ex)
        {
            StartupLogger.Log("OptimizePage", $"Constructor EXCEPTION: {ex}");
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
        SetFlyoutButtonText(FlyoutTextSound, FlyoutButtonSound, "Sound");
        SetFlyoutButtonText(FlyoutTextUpdate, FlyoutButtonUpdate, "Update");
        SetFlyoutButtonText(FlyoutTextNotification, FlyoutButtonNotification, "Notification");
        SetFlyoutButtonText(FlyoutTextPrivacy, FlyoutButtonPrivacy, "Privacy");
        SetFlyoutButtonText(FlyoutTextPower, FlyoutButtonPower, "Power");
        SetFlyoutButtonText(FlyoutTextGaming, FlyoutButtonGaming, "Gaming");
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
            StartupLogger.Log("OptimizePage", "OnNavigatedTo starting...");
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
                    DispatcherQueue.TryEnqueue(() => { UpdateOverviewBadgePills(); UpdateOverviewNewBadges(); });
                });
                _settingsRefreshedSubscription?.Dispose();
                _settingsRefreshedSubscription = eventBus.Subscribe<SettingsRefreshedEvent>(e =>
                {
                    DispatcherQueue.TryEnqueue(() => SyncViewStateToSettings());
                });
            }
            UpdateBreadcrumbMenuItems();

            // Ensure we're showing overview on initial navigation
            ViewModel.CurrentSectionKey = "Overview";
            UpdateContentVisibility();

            StartupLogger.Log("OptimizePage", "Calling ViewModel.InitializeAsync...");
            await ViewModel.InitializeAsync();

            // Set localized labels for dropdown menus
            SetDropdownLabels();

            // Initialize technical details toggle state
            await InitializeTechnicalDetailsToggleAsync();

            // Initialize info badges toggle state
            await InitializeInfoBadgesAsync();

            // Initialize NEW badges toggle state
            await InitializeNewBadgesAsync();

            // Always update badges: shows them if in review mode, collapses them if not
            UpdateOverviewBadges();
            UpdateBreadcrumbBadges();
            UpdateOverviewBadgePills();
            UpdateOverviewNewBadges();

            // Re-apply Show Only Changes filter if still active from before navigation
            if (_showOnlyChanges)
                ApplyShowOnlyChangesFilter();

            StartupLogger.Log("OptimizePage", "OnNavigatedTo complete");
        }
        catch (Exception ex)
        {
            StartupLogger.Log("OptimizePage", $"OnNavigatedTo EXCEPTION: {ex}");
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
        _settingsRefreshedSubscription?.Dispose();
        _settingsRefreshedSubscription = null;
        ViewModel.OnNavigatedFrom();
    }

    public void NavigateToSection(string sectionKey, string? searchText = null)
    {
        Type? pageType = sectionKey switch
        {
            "Sound" => typeof(SoundOptimizePage),
            "Update" => typeof(UpdateOptimizePage),
            "Notification" => typeof(NotificationOptimizePage),
            "Privacy" => typeof(PrivacyOptimizePage),
            "Power" => typeof(PowerOptimizePage),
            "Gaming" => typeof(GamingOptimizePage),
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
            nameof(SoundOptimizePage) => "Sound",
            nameof(UpdateOptimizePage) => "Update",
            nameof(NotificationOptimizePage) => "Notification",
            nameof(PrivacyOptimizePage) => "Privacy",
            nameof(PowerOptimizePage) => "Power",
            nameof(GamingOptimizePage) => "Gaming",
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
                Application.Current.Resources.TryGetValue(resourceKey, out var resourceValue) &&
                resourceValue is string iconData)
            {
                // Check icon type based on the resource key naming convention
                bool isSymbol = resourceKey.EndsWith("Symbol");

                BreadcrumbSectionIconBox.Visibility = isSymbol ? Visibility.Collapsed : Visibility.Visible;
                BreadcrumbSectionSymbol.Visibility = isSymbol ? Visibility.Visible : Visibility.Collapsed;

                if (isSymbol)
                {
                    if (Enum.TryParse<FluentIcons.Common.Symbol>(iconData, ignoreCase: true, out var symbol))
                    {
                        BreadcrumbSectionSymbol.Symbol = symbol;
                    }
                }
                else
                {
                    var geometry = (Microsoft.UI.Xaml.Media.Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(
                        typeof(Microsoft.UI.Xaml.Media.Geometry), iconData);
                    BreadcrumbSectionIcon.Data = geometry;
                }
            }

            UpdateBreadcrumbBadges();
        }
    }

    // Overview card click handlers
    private void SoundCard_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("Sound");
    }

    private void UpdateCard_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("Update");
    }

    private void NotificationCard_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("Notification");
    }

    private void PrivacyCard_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("Privacy");
    }

    private void PowerCard_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("Power");
    }

    private void GamingCard_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("Gaming");
    }

    // Breadcrumb handlers
    private void BreadcrumbOverview_Click(object sender, RoutedEventArgs e)
    {
        NavigateToOverview();
    }

    private void NavigateSound_Click(object sender, RoutedEventArgs e)
    {
        BreadcrumbFlyout.Hide();
        NavigateToSection("Sound");
    }

    private void NavigateUpdate_Click(object sender, RoutedEventArgs e)
    {
        BreadcrumbFlyout.Hide();
        NavigateToSection("Update");
    }

    private void NavigateNotification_Click(object sender, RoutedEventArgs e)
    {
        BreadcrumbFlyout.Hide();
        NavigateToSection("Notification");
    }

    private void NavigatePrivacy_Click(object sender, RoutedEventArgs e)
    {
        BreadcrumbFlyout.Hide();
        NavigateToSection("Privacy");
    }

    private void NavigatePower_Click(object sender, RoutedEventArgs e)
    {
        BreadcrumbFlyout.Hide();
        NavigateToSection("Power");
    }

    private void NavigateGaming_Click(object sender, RoutedEventArgs e)
    {
        BreadcrumbFlyout.Hide();
        NavigateToSection("Gaming");
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
            PrivacyBadge.Visibility = Visibility.Collapsed;
            PowerBadge.Visibility = Visibility.Collapsed;
            GamingBadge.Visibility = Visibility.Collapsed;
            UpdateBadge.Visibility = Visibility.Collapsed;
            NotificationBadge.Visibility = Visibility.Collapsed;
            SoundBadge.Visibility = Visibility.Collapsed;
            return;
        }

        UpdateFeatureBadge(PrivacyBadge, FeatureIds.Privacy);
        UpdateFeatureBadge(PowerBadge, FeatureIds.Power);
        UpdateFeatureBadge(GamingBadge, FeatureIds.GamingPerformance);
        UpdateFeatureBadge(UpdateBadge, FeatureIds.Update);
        UpdateFeatureBadge(NotificationBadge, FeatureIds.Notifications);
        UpdateFeatureBadge(SoundBadge, FeatureIds.Sound);
    }

    private void UpdateOverviewBadgePills()
    {
        UpdateFeatureOverviewPills(
            ViewModel.PrivacyViewModel,
            PrivacyOverviewBadges,
            PrivacyRecommendedPill, PrivacyRecommendedText,
            PrivacyDefaultPill, PrivacyDefaultText,
            PrivacyCustomPill, PrivacyCustomText);
        UpdateFeatureOverviewPills(
            ViewModel.PowerViewModel,
            PowerOverviewBadges,
            PowerRecommendedPill, PowerRecommendedText,
            PowerDefaultPill, PowerDefaultText,
            PowerCustomPill, PowerCustomText);
        UpdateFeatureOverviewPills(
            ViewModel.GamingViewModel,
            GamingOverviewBadges,
            GamingRecommendedPill, GamingRecommendedText,
            GamingDefaultPill, GamingDefaultText,
            GamingCustomPill, GamingCustomText);
        UpdateFeatureOverviewPills(
            ViewModel.UpdateViewModel,
            UpdateOverviewPills,
            UpdateRecommendedPill, UpdateRecommendedText,
            UpdateDefaultPill, UpdateDefaultText,
            UpdateCustomPill, UpdateCustomText);
        UpdateFeatureOverviewPills(
            ViewModel.NotificationViewModel,
            NotificationOverviewBadges,
            NotificationRecommendedPill, NotificationRecommendedText,
            NotificationDefaultPill, NotificationDefaultText,
            NotificationCustomPill, NotificationCustomText);
        UpdateFeatureOverviewPills(
            ViewModel.SoundViewModel,
            SoundOverviewBadges,
            SoundRecommendedPill, SoundRecommendedText,
            SoundDefaultPill, SoundDefaultText,
            SoundCustomPill, SoundCustomText);
    }

    private void UpdateOverviewNewBadges()
    {
        UpdateFeatureNewBadge(ViewModel.PrivacyViewModel, PrivacyNewBadge, PrivacyNewText);
        UpdateFeatureNewBadge(ViewModel.PowerViewModel, PowerNewBadge, PowerNewText);
        UpdateFeatureNewBadge(ViewModel.GamingViewModel, GamingNewBadge, GamingNewText);
        UpdateFeatureNewBadge(ViewModel.UpdateViewModel, UpdateNewBadge, UpdateNewText);
        UpdateFeatureNewBadge(ViewModel.NotificationViewModel, NotificationNewBadge, NotificationNewText);
        UpdateFeatureNewBadge(ViewModel.SoundViewModel, SoundNewBadge, SoundNewText);
    }

    private void UpdateFeatureNewBadge(
        ISettingsFeatureViewModel feature,
        Border badge, TextBlock text)
    {
        if (!_isNewBadgesVisible)
        {
            badge.Visibility = Visibility.Collapsed;
            return;
        }

        var summary = FeatureBadgeAggregator.Aggregate(feature);
        if (summary.NewCount > 0)
        {
            badge.Visibility = Visibility.Visible;
            text.Text = $"{_localizationService?.GetString("Badge_New") ?? "NEW"} {summary.NewCount}";
        }
        else
        {
            badge.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateFeatureOverviewPills(
        ISettingsFeatureViewModel feature,
        StackPanel container,
        Border recommendedPill, TextBlock recommendedText,
        Border defaultPill, TextBlock defaultText,
        Border customPill, TextBlock customText)
    {
        if (!_isInfoBadgesVisible)
        {
            container.Visibility = Visibility.Collapsed;
            return;
        }

        var summary = FeatureBadgeAggregator.Aggregate(feature);
        int total = summary.TotalWithBadgeData;
        bool showAny = false;

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
        foreach (var section in OptimizeViewModel.Sections)
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
        foreach (var section in OptimizeViewModel.Sections)
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
        foreach (var section in OptimizeViewModel.Sections)
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

    /// <summary>
    /// Re-applies page-level view state (badge visibility, technical details)
    /// to all settings after they have been recreated by a reload.
    /// </summary>
    private void SyncViewStateToSettings()
    {
        foreach (var section in OptimizeViewModel.Sections)
        {
            var sectionVm = ViewModel.GetSectionViewModel(section.Key);
            if (sectionVm == null) continue;
            foreach (var setting in sectionVm.Settings)
            {
                setting.IsInfoBadgeGloballyVisible = _isInfoBadgesVisible;
                setting.IsNewBadgeGloballyVisible = _isNewBadgesVisible;
                setting.IsTechnicalDetailsGloballyVisible = _isTechnicalDetailsVisible;
            }
        }

        UpdateOverviewBadgePills();
        UpdateOverviewNewBadges();
    }

    // View menu handlers
    private async void ViewTechnicalDetails_Click(object sender, RoutedEventArgs e)
    {
        _isTechnicalDetailsVisible = TechnicalDetailsToggleItem.IsChecked;

        // Update all settings across all sections
        foreach (var section in OptimizeViewModel.Sections)
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

        foreach (var section in OptimizeViewModel.Sections)
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

        foreach (var section in OptimizeViewModel.Sections)
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
        UpdateOverviewNewBadges();
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
            ? OptimizeViewModel.Sections.Where(s => s.Key == ViewModel.CurrentSectionKey)
            : OptimizeViewModel.Sections;

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
            ? OptimizeViewModel.Sections.Where(s => s.Key == ViewModel.CurrentSectionKey)
            : OptimizeViewModel.Sections;

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
            ? OptimizeViewModel.Sections.Where(s => s.Key == ViewModel.CurrentSectionKey)
            : OptimizeViewModel.Sections;

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
