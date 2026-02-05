using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.Optimize.Models;
using Winhance.UI.Features.Optimize.Pages;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Optimize;

public sealed partial class OptimizePage : Page
{
    private static readonly string LogFile = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "startup-debug.log");
    private static void Log(string msg) { try { File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [OptimizePage] {msg}{Environment.NewLine}"); } catch { } }

    private static readonly Dictionary<string, string> SectionIconResourceKeys = new()
    {
        { "Privacy", "PrivacyIconPath" },
        { "Power", "PowerIconPath" },
        { "Gaming", "GamingIconPath" },
        { "Update", "UpdateIconPath" },
        { "Notification", "NotificationIconPath" },
        { "Sound", "SoundIconPath" }
    };

    public OptimizeViewModel ViewModel { get; }

    public OptimizePage()
    {
        try
        {
            Log("Constructor starting...");
            this.InitializeComponent();
            Log("InitializeComponent done, getting ViewModel...");
            ViewModel = App.Services.GetRequiredService<OptimizeViewModel>();
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateBreadcrumbMenuItems();
            Log("ViewModel obtained, constructor complete");
        }
        catch (Exception ex)
        {
            Log($"Constructor EXCEPTION: {ex}");
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
        MenuItemSound.Text = ViewModel.GetSectionDisplayName("Sound");
        MenuItemUpdate.Text = ViewModel.GetSectionDisplayName("Update");
        MenuItemNotification.Text = ViewModel.GetSectionDisplayName("Notification");
        MenuItemPrivacy.Text = ViewModel.GetSectionDisplayName("Privacy");
        MenuItemPower.Text = ViewModel.GetSectionDisplayName("Power");
        MenuItemGaming.Text = ViewModel.GetSectionDisplayName("Gaming");
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        try
        {
            Log("OnNavigatedTo starting...");
            base.OnNavigatedTo(e);

            // Ensure we're showing overview on initial navigation
            ViewModel.CurrentSectionKey = "Overview";
            UpdateContentVisibility();

            Log("Calling ViewModel.InitializeAsync...");
            await ViewModel.InitializeAsync();
            Log("OnNavigatedTo complete");
        }
        catch (Exception ex)
        {
            Log($"OnNavigatedTo EXCEPTION: {ex}");
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
                Application.Current.Resources.TryGetValue(resourceKey, out var pathDataObj) &&
                pathDataObj is string pathData)
            {
                var geometry = (Microsoft.UI.Xaml.Media.Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(
                    typeof(Microsoft.UI.Xaml.Media.Geometry), pathData);
                BreadcrumbSectionIcon.Data = geometry;
            }
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
        NavigateToSection("Sound");
    }

    private void NavigateUpdate_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("Update");
    }

    private void NavigateNotification_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("Notification");
    }

    private void NavigatePrivacy_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("Privacy");
    }

    private void NavigatePower_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("Power");
    }

    private void NavigateGaming_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("Gaming");
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
