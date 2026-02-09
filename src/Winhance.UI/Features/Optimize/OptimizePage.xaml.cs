using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Services;
using IConfigReviewService = Winhance.Core.Features.Common.Interfaces.IConfigReviewService;
using Winhance.UI.Features.Optimize.Models;
using Winhance.UI.Features.Optimize.Pages;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Optimize;

public sealed partial class OptimizePage : Page
{
    // Maps section keys to their icon resource keys (PathIcon paths end with "Path", FontIcon glyphs end with "Glyph")
    private static readonly Dictionary<string, string> SectionIconResourceKeys = new()
    {
        { "Privacy", "PrivacyIconPath" },
        { "Power", "PowerIconPath" },
        { "Gaming", "GamingIconPath" },
        { "Update", "UpdateIconGlyph" },
        { "Notification", "NotificationIconPath" },
        { "Sound", "SoundIconGlyph" }
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
    private Dictionary<string, InfoBadge>? _flyoutBadges;

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
        FlyoutTextSound.Text = ViewModel.GetSectionDisplayName("Sound");
        FlyoutTextUpdate.Text = ViewModel.GetSectionDisplayName("Update");
        FlyoutTextNotification.Text = ViewModel.GetSectionDisplayName("Notification");
        FlyoutTextPrivacy.Text = ViewModel.GetSectionDisplayName("Privacy");
        FlyoutTextPower.Text = ViewModel.GetSectionDisplayName("Power");
        FlyoutTextGaming.Text = ViewModel.GetSectionDisplayName("Gaming");
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        try
        {
            StartupLogger.Log("OptimizePage", "OnNavigatedTo starting...");
            base.OnNavigatedTo(e);

            // Ensure we're showing overview on initial navigation
            ViewModel.CurrentSectionKey = "Overview";
            UpdateContentVisibility();

            StartupLogger.Log("OptimizePage", "Calling ViewModel.InitializeAsync...");
            await ViewModel.InitializeAsync();

            // Update badges if already in review mode (events fired before page existed)
            if (_configReviewService?.IsInReviewMode == true)
            {
                UpdateOverviewBadges();
                UpdateBreadcrumbBadges();
            }

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

            if (SectionIconResourceKeys.TryGetValue(ViewModel.CurrentSectionKey, out var resourceKey) &&
                Application.Current.Resources.TryGetValue(resourceKey, out var resourceValue) &&
                resourceValue is string iconData)
            {
                // Check if this is a glyph icon or a path icon based on the resource key naming convention
                bool isGlyph = resourceKey.EndsWith("Glyph");

                BreadcrumbSectionIconBox.Visibility = isGlyph ? Visibility.Collapsed : Visibility.Visible;
                BreadcrumbSectionGlyph.Visibility = isGlyph ? Visibility.Visible : Visibility.Collapsed;

                if (isGlyph)
                {
                    BreadcrumbSectionGlyph.Glyph = iconData;
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
