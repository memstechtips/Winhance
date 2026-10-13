using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.ViewManagement;
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
            new PropertyMetadata(false, (d, _) => ((RocketLoader)d).Update()));

    private static readonly UISettings UiSettings = new();

    public RocketLoader()
    {
        InitializeComponent();
        Visibility = Visibility.Collapsed;
        AutomationProperties.SetName(this, App.Services.GetRequiredService<ILocalizationService>().GetString("Common_Loading"));
        Loaded += (_, _) => Update();
        Unloaded += (_, _) => FlightStoryboard.Stop();
    }

    // Shows and hides the control as well, so a caller binds this alone.
    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private void Update()
    {
        Visibility = IsActive ? Visibility.Visible : Visibility.Collapsed;

        // With Windows' animation effects off the rocket stands still, as the website does for reduced motion.
        if (IsActive && IsLoaded && UiSettings.AnimationsEnabled)
            FlightStoryboard.Begin();
        else
            FlightStoryboard.Stop();
    }
}
