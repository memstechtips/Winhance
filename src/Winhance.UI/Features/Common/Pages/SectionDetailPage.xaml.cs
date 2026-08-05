using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.Common.Models;

namespace Winhance.UI.Features.Common.Pages;

/// <summary>
/// The settings list for one section, whichever section that is.
///
/// <para>This replaces ten page types — SoundOptimizePage, TaskbarCustomizePage and the eight
/// others — that were identical apart from a single binding path
/// (<c>ViewModel.SoundViewModel.GroupedSettings</c> vs <c>ViewModel.TaskbarViewModel.…</c>).
/// Which section to show now arrives as a navigation parameter instead of being encoded in the
/// choice of type, so adding a section no longer means adding two files.</para>
///
/// <para>The bindings are built here rather than in XAML because the paths differ per section, and
/// <c>x:Bind</c> resolves paths at compile time.</para>
/// </summary>
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
