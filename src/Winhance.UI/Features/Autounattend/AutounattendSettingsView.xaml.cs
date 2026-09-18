using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Winhance.UI.Features.Autounattend.ViewModels;

namespace Winhance.UI.Features.Autounattend;

public sealed partial class AutounattendSettingsView : UserControl
{
    public static readonly DependencyProperty FooterProperty =
        DependencyProperty.Register(
            nameof(Footer),
            typeof(object),
            typeof(AutounattendSettingsView),
            new PropertyMetadata(null));

    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    public AutounattendViewModel ViewModel { get; }

    public AutounattendSettingsView()
    {
        ViewModel = App.Services.GetRequiredService<AutounattendViewModel>();

        this.InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        SettingsList.GroupedSettingsSource = new CollectionViewSource
        {
            Source = ViewModel.GroupedSettings,
            IsSourceGrouped = true,
        }.View;

        // First: OnUnloaded can run during the reload below, and a subscription opened after it is never released.
        await ViewModel.LoadIncludedAsync();

        // Not the one-shot load: leaving Builder empties this answer-file-only feature, and the base's load
        // latch would then leave the page blank for the rest of the session.
        await ViewModel.RefreshSettingsAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.ReleaseIncluded();
    }
}
