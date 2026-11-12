using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Winhance.UI.Features.Autounattend.ViewModels;

namespace Winhance.UI.Features.Autounattend;

public sealed partial class AutounattendSettingsView : UserControl
{
    public static readonly DependencyProperty HasNoSearchResultsProperty =
        DependencyProperty.Register(
            nameof(HasNoSearchResults),
            typeof(bool),
            typeof(AutounattendSettingsView),
            new PropertyMetadata(false));

    public bool HasNoSearchResults
    {
        get => (bool)GetValue(HasNoSearchResultsProperty);
        set => SetValue(HasNoSearchResultsProperty, value);
    }

    public AutounattendViewModel ViewModel { get; }

    public bool ScrollsWithPage
    {
        get => SettingsList.ScrollsWithPage;
        set => SettingsList.ScrollsWithPage = value;
    }

    public AutounattendSettingsView()
    {
        ViewModel = App.Services.GetRequiredService<AutounattendViewModel>();

        // Before the first layout, so the loader is built with the page: turned on from Loaded it showed half a
        // second late, and the page sat blank until then.
        ViewModel.IsLoading = true;
        this.InitializeComponent();
    }

    private CollectionViewSource? _groupedSource;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _groupedSource = new CollectionViewSource
        {
            Source = ViewModel.GroupedSettings,
            IsSourceGrouped = true,
        };
        SettingsList.GroupedSettingsSource = _groupedSource.View;

        // Covers both loads, so the loader does not drop out between them.
        ViewModel.IsLoading = true;
        try
        {
            // First: OnUnloaded can run during the awaits below, and a subscription opened after it is never released.
            await ViewModel.LoadIncludedAsync();

            // Not the one-shot load: leaving Builder empties this answer-file-only feature, and the base's load
            // latch would then leave the page blank for the rest of the session.
            await ViewModel.RefreshSettingsAsync();
        }
        finally
        {
            ViewModel.IsLoading = false;
        }
    }

    // The view model is a singleton, so a view left subscribed to its collection is never collected.
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.ReleaseIncluded();
        SettingsList.GroupedSettingsSource = null;
        if (_groupedSource is not null)
            _groupedSource.Source = null;
        _groupedSource = null;
    }
}
