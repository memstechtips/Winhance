using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.UI.Features.Autounattend.ViewModels;

namespace Winhance.UI.Features.Autounattend;

// The summary cards for what the answer file takes from the other features. The Autounattend page hosts them under
// its own heading and loads them with its settings; the WIMUtil page sets LoadsItself and ShowsModeTags instead.
public sealed partial class IncludedFromFeaturesView : UserControl
{
    public static readonly DependencyProperty LoadsItselfProperty =
        DependencyProperty.Register(nameof(LoadsItself), typeof(bool), typeof(IncludedFromFeaturesView), new PropertyMetadata(false));

    public static readonly DependencyProperty ShowsModeTagsProperty =
        DependencyProperty.Register(nameof(ShowsModeTags), typeof(bool), typeof(IncludedFromFeaturesView), new PropertyMetadata(false));

    private bool _loadedItself;

    public IncludedFromFeaturesView()
    {
        ViewModel = App.Services.GetRequiredService<AutounattendViewModel>();
        InitializeComponent();
    }

    public AutounattendViewModel ViewModel { get; }

    public bool LoadsItself
    {
        get => (bool)GetValue(LoadsItselfProperty);
        set => SetValue(LoadsItselfProperty, value);
    }

    public bool ShowsModeTags
    {
        get => (bool)GetValue(ShowsModeTagsProperty);
        set => SetValue(ShowsModeTagsProperty, value);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowModeTags(ShowsModeTags);

        if (!LoadsItself)
            return;

        _loadedItself = true;

        // First: OnUnloaded can run during the await below, and a subscription opened after it is never released.
        await ViewModel.LoadIncludedAsync();

        // The own-installer cards and the counts come from the settings, which only this load reads here.
        await ViewModel.RefreshSettingsAsync();
    }

    private async void OnStartSeedChanged(object sender, SelectionChangedEventArgs e) =>
        await ViewModel.ChangeSeedAsync(((ComboBox)sender).SelectedIndex);

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (ShowsModeTags)
            ViewModel.ShowModeTags(false);

        if (!_loadedItself)
            return;

        _loadedItself = false;
        ViewModel.ReleaseIncluded();
    }
}
