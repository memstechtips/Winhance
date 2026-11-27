using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Controls;

// Only transforms and opacity move, so the compositor runs the flight on its own thread: it keeps going while the
// UI thread builds a long page, which is exactly when it is on screen.
public sealed partial class RocketLoader : UserControl
{
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(
            nameof(IsActive),
            typeof(bool),
            typeof(RocketLoader),
            new PropertyMetadata(false));

    public RocketLoader()
    {
        InitializeComponent();
        AutomationProperties.SetName(this, App.Services.GetRequiredService<ILocalizationService>().GetString("Common_Loading"));
    }

    // Builds the flight while true and drops it while false, so a caller binds this alone.
    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    // Not gated on Windows' animation effects: Winhance's own performance settings turn them off, which would
    // freeze the loader on the very machines that wait longest.
    private void RocketFlight_Loaded(object sender, RoutedEventArgs e) => Flight(sender).Begin();

    private void RocketFlight_Unloaded(object sender, RoutedEventArgs e) => Flight(sender).Stop();

    private static Storyboard Flight(object rocketFlight) => (Storyboard)((Grid)rocketFlight).Resources["FlightStoryboard"];
}
