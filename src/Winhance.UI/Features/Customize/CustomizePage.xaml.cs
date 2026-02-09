using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Services;
using IConfigReviewService = Winhance.Core.Features.Common.Interfaces.IConfigReviewService;
using Winhance.UI.Features.Customize.Models;
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
    private Dictionary<string, InfoBadge>? _flyoutBadges;

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
        FlyoutTextWindowsTheme.Text = ViewModel.GetSectionDisplayName("WindowsTheme");
        FlyoutTextTaskbar.Text = ViewModel.GetSectionDisplayName("Taskbar");
        FlyoutTextStartMenu.Text = ViewModel.GetSectionDisplayName("StartMenu");
        FlyoutTextExplorer.Text = ViewModel.GetSectionDisplayName("Explorer");
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        try
        {
            StartupLogger.Log("CustomizePage", "OnNavigatedTo starting...");
            base.OnNavigatedTo(e);

            // Ensure we're showing overview on initial navigation
            ViewModel.CurrentSectionKey = "Overview";
            UpdateContentVisibility();

            StartupLogger.Log("CustomizePage", "Calling ViewModel.InitializeAsync...");
            await ViewModel.InitializeAsync();

            // Update badges if already in review mode (events fired before page existed)
            if (_configReviewService?.IsInReviewMode == true)
            {
                UpdateOverviewBadges();
                UpdateBreadcrumbBadges();
            }

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
        DispatcherQueue.TryEnqueue(() => { UpdateOverviewBadges(); UpdateBreadcrumbBadges(); });
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
