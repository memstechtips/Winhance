using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.Common.Models;

namespace Winhance.UI.Features.Common.Pages;

// One page type for every section; which section to show arrives as a navigation parameter. The bindings
// are built here rather than in XAML because the paths differ per section and x:Bind resolves paths at
// compile time.
public sealed partial class SectionDetailPage : Page
{
    private SectionDetailNavigation? _navigation;
    private CollectionViewSource? _groupedSource;

    public SectionDetailPage()
    {
        this.InitializeComponent();
        Loaded += (_, _) => Connect();
        Unloaded += (_, _) => Disconnect();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is not SectionDetailNavigation navigation)
            return;

        _navigation = navigation;
        Connect();

        if (!string.IsNullOrWhiteSpace(navigation.SearchText))
        {
            navigation.PageViewModel.SearchText = navigation.SearchText;
        }

        // Lightweight refresh: re-read setting states from the system. Guarded inside the feature
        // ViewModel for Builder mode, which must not have its authored values overwritten.
        _ = navigation.Feature.RefreshSettingStatesAsync();
    }

    // The feature and page view models outlive this page, and the view and bindings subscribe to them. Leaving
    // the frame (Content = null raises no OnNavigatedFrom) or the whole section page drops them; coming back to a
    // cached section page connects again.
    private void Connect()
    {
        if (_navigation is not { } navigation || _groupedSource is not null)
            return;

        // Grouping has to go through a CollectionViewSource; the ListView needs its .View.
        _groupedSource = new CollectionViewSource
        {
            Source = navigation.Feature.GroupedSettings,
            IsSourceGrouped = true,
        };
        SettingsList.GroupedSettingsSource = _groupedSource.View;

        // Loading and no-results belong to the host page's ViewModel, which raises PropertyChanged
        // for both — hence real bindings rather than one-shot assignment.
        SettingsList.SetBinding(SettingsListView.IsLoadingProperty, new Binding
        {
            Source = navigation.PageViewModel,
            Path = new PropertyPath(nameof(navigation.PageViewModel.IsLoading)),
            Mode = BindingMode.OneWay,
        });
        SettingsList.SetBinding(SettingsListView.HasNoSearchResultsProperty, new Binding
        {
            Source = navigation.PageViewModel,
            Path = new PropertyPath(nameof(navigation.PageViewModel.HasNoSearchResults)),
            Mode = BindingMode.OneWay,
        });
        SettingsList.SetBinding(SettingsListView.NoResultsTextProperty, new Binding
        {
            Source = navigation.PageViewModel,
            Path = new PropertyPath(nameof(navigation.PageViewModel.NoResultsText)),
            Mode = BindingMode.OneWay,
        });
        SettingsList.SetBinding(SettingsListView.IsShowAllVisibleProperty, new Binding
        {
            Source = navigation.PageViewModel,
            Path = new PropertyPath(nameof(navigation.PageViewModel.IsShowAllVisible)),
            Mode = BindingMode.OneWay,
        });
        SettingsList.SetBinding(SettingsListView.ShowAllTextProperty, new Binding
        {
            Source = navigation.PageViewModel,
            Path = new PropertyPath(nameof(navigation.PageViewModel.ShowAllText)),
            Mode = BindingMode.OneWay,
        });
        SettingsList.ShowAllCommand = navigation.PageViewModel.ShowAllSettingsCommand;
    }

    private void Disconnect()
    {
        if (_groupedSource is null)
            return;

        SettingsList.GroupedSettingsSource = null;
        _groupedSource.Source = null;
        _groupedSource = null;
        SettingsList.ClearValue(SettingsListView.IsLoadingProperty);
        SettingsList.ClearValue(SettingsListView.HasNoSearchResultsProperty);
        SettingsList.ClearValue(SettingsListView.NoResultsTextProperty);
        SettingsList.ClearValue(SettingsListView.IsShowAllVisibleProperty);
        SettingsList.ClearValue(SettingsListView.ShowAllTextProperty);
        SettingsList.ShowAllCommand = null;
    }
}
