using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.Customize.Models;
using Winhance.UI.Features.Customize.Pages;
using Winhance.UI.Features.Customize.ViewModels;

namespace Winhance.UI.Features.Customize;

/// <summary>
/// Shell page for customizing Windows appearance and behavior.
/// Shows overview cards by default, uses inner Frame for detail pages.
/// </summary>
public sealed partial class CustomizePage : Page
{
    private static readonly string LogFile = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "startup-debug.log");
    private static void Log(string msg) { try { File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [CustomizePage] {msg}{Environment.NewLine}"); } catch { } }

    // Map section keys to their icon path resource keys in FeatureIcons.xaml
    private static readonly Dictionary<string, string> SectionIconResourceKeys = new()
    {
        { "Explorer", "ExplorerIconPath" },
        { "StartMenu", "StartMenuIconPath" },
        { "Taskbar", "TaskbarIconPath" },
        { "WindowsTheme", "WindowsThemeIconPath" }
    };

    public CustomizeViewModel ViewModel { get; }

    public CustomizePage()
    {
        try
        {
            Log("Constructor starting...");
            this.InitializeComponent();
            Log("InitializeComponent done, getting ViewModel...");
            ViewModel = App.Services.GetRequiredService<CustomizeViewModel>();
            UpdateBreadcrumbMenuItems();
            Log("ViewModel obtained, constructor complete");
        }
        catch (Exception ex)
        {
            Log($"Constructor EXCEPTION: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Updates the breadcrumb dropdown menu items with localized text.
    /// </summary>
    private void UpdateBreadcrumbMenuItems()
    {
        MenuItemWindowsTheme.Text = ViewModel.GetSectionDisplayName("WindowsTheme");
        MenuItemTaskbar.Text = ViewModel.GetSectionDisplayName("Taskbar");
        MenuItemStartMenu.Text = ViewModel.GetSectionDisplayName("StartMenu");
        MenuItemExplorer.Text = ViewModel.GetSectionDisplayName("Explorer");
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

    /// <summary>
    /// Navigates to a specific section by key.
    /// </summary>
    public void NavigateToSection(string sectionKey, string? searchText = null)
    {
        Type pageType = sectionKey switch
        {
            "Explorer" => typeof(ExplorerCustomizePage),
            "StartMenu" => typeof(StartMenuCustomizePage),
            "Taskbar" => typeof(TaskbarCustomizePage),
            "WindowsTheme" => typeof(WindowsThemeCustomizePage),
            _ => null
        };

        if (pageType != null)
        {
            InnerContentFrame.Navigate(pageType, searchText);
        }
        else
        {
            // Navigate to overview
            NavigateToOverview();
        }
    }

    /// <summary>
    /// Returns to the overview (hides detail frame, shows overview cards).
    /// </summary>
    public void NavigateToOverview()
    {
        ViewModel.CurrentSectionKey = "Overview";
        InnerContentFrame.Content = null;
        UpdateContentVisibility();
    }

    private void InnerContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        // Update the ViewModel's current section key based on the navigated page
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

        // Toggle between overview and detail frame
        OverviewContent.Visibility = isInDetailPage ? Visibility.Collapsed : Visibility.Visible;
        InnerContentFrame.Visibility = isInDetailPage ? Visibility.Visible : Visibility.Collapsed;

        // Update breadcrumb visibility
        BreadcrumbSeparator.Visibility = isInDetailPage ? Visibility.Visible : Visibility.Collapsed;
        BreadcrumbSection.Visibility = isInDetailPage ? Visibility.Visible : Visibility.Collapsed;

        // Update the section dropdown icon and text
        if (isInDetailPage)
        {
            BreadcrumbSectionText.Text = ViewModel.CurrentSectionName;

            // Get the icon path data from application resources (FeatureIcons.xaml)
            if (SectionIconResourceKeys.TryGetValue(ViewModel.CurrentSectionKey, out var resourceKey) &&
                Application.Current.Resources.TryGetValue(resourceKey, out var pathDataObj) &&
                pathDataObj is string pathData)
            {
                // Parse the path data string into a Geometry
                var geometry = (Microsoft.UI.Xaml.Media.Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(
                    typeof(Microsoft.UI.Xaml.Media.Geometry), pathData);
                BreadcrumbSectionIcon.Data = geometry;
            }
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
        NavigateToSection("Explorer");
    }

    private void NavigateStartMenu_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("StartMenu");
    }

    private void NavigateTaskbar_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("Taskbar");
    }

    private void NavigateWindowsTheme_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("WindowsTheme");
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
