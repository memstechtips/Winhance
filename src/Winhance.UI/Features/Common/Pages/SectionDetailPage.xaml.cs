using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.Common.Models;

namespace Winhance.UI.Features.Common.Pages;

// One page type for every section (it replaced ten identical page types differing in one binding path); which
// section to show arrives as a navigation parameter. The bindings are built here rather than in XAML because the
// paths differ per section and x:Bind resolves paths at compile time.
public sealed partial class SectionDetailPage : Page
{
    public SectionDetailPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is not SectionDetailNavigation navigation)
            return;

        // Grouping has to go through a CollectionViewSource; the ListView needs its .View.
        var groupedSource = new CollectionViewSource
        {
            Source = navigation.Feature.GroupedSettings,
            IsSourceGrouped = true,
        };
        SettingsList.GroupedSettingsSource = groupedSource.View;

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

        if (!string.IsNullOrWhiteSpace(navigation.SearchText))
        {
            navigation.PageViewModel.SearchText = navigation.SearchText;
        }

        // Lightweight refresh: re-read setting states from the system. Guarded inside the feature
        // ViewModel for Builder mode, which must not have its authored values overwritten.
        _ = navigation.Feature.RefreshSettingStatesAsync();
    }
}
