using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.AdvancedTools.ViewModels;

namespace Winhance.UI.Features.AdvancedTools;

/// <summary>
/// Page displaying advanced tools with overview cards and detail navigation.
/// </summary>
public sealed partial class AdvancedToolsPage : Page
{
    private static readonly Dictionary<string, string> SectionIconResourceKeys = new()
    {
        { "WimUtil", "WimUtilIconPath" },
        { "AutounattendXml", "AutounattendXmlIconPath" }
    };

    public AdvancedToolsViewModel ViewModel { get; }

    public AdvancedToolsPage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<AdvancedToolsViewModel>();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateBreadcrumbMenuItems();
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
        MenuItemWimUtil.Text = ViewModel.GetSectionDisplayName("WimUtil");
        MenuItemAutounattendXml.Text = ViewModel.GetSectionDisplayName("AutounattendXml");
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.CurrentSectionKey = "Overview";
        UpdateContentVisibility();
    }

    public void NavigateToSection(string sectionKey)
    {
        Type? pageType = sectionKey switch
        {
            "WimUtil" => typeof(WimUtilPage),
            "AutounattendXml" => typeof(AutounattendGeneratorPage),
            _ => null
        };

        if (pageType != null)
        {
            InnerContentFrame.Navigate(pageType);
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
            nameof(WimUtilPage) => "WimUtil",
            nameof(AutounattendGeneratorPage) => "AutounattendXml",
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
                var geometry = (Microsoft.UI.Xaml.Media.Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(
                    typeof(Microsoft.UI.Xaml.Media.Geometry), iconData);
                BreadcrumbSectionIcon.Data = geometry;
            }
        }
    }

    // Overview card click handlers
    private void WimUtilCard_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("WimUtil");
    }

    private void AutounattendXmlCard_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("AutounattendXml");
    }

    // Breadcrumb handlers
    private void BreadcrumbOverview_Click(object sender, RoutedEventArgs e)
    {
        NavigateToOverview();
    }

    private void NavigateWimUtil_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("WimUtil");
    }

    private void NavigateAutounattendXml_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("AutounattendXml");
    }
}
