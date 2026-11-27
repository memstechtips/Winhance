using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Winhance.UI.Features.Common.Controls;

// Owns the overlay knob's hover animation: the icon is named here, which x:Name inside a DataTemplate would not allow.
public sealed partial class SettingToggleSwitch : SettingBoundControl
{
    // Matches the native ToggleSwitch's knob-grow on pointer-over.
    private const double HoverScale = 1.15;
    private const double RestScale = 1.0;

    public SettingToggleSwitch()
    {
        InitializeComponent();
    }

    private void OnToggled(object sender, RoutedEventArgs e) => Setting?.OnToggleSwitchToggled(sender);

    private void OnOverlayClicked(object sender, RoutedEventArgs e) => Setting?.OnCustomToggleClicked();

    private void OnOverlayPointerEntered(object sender, PointerRoutedEventArgs e) => AnimateKnob(HoverScale);

    // Wired to PointerExited AND PointerCanceled AND PointerCaptureLost so the knob can never stick enlarged.
    private void OnOverlayPointerExited(object sender, PointerRoutedEventArgs e) => AnimateKnob(RestScale);

    private void AnimateKnob(double scale)
    {
        if (KnobIcon.RenderTransform is not CompositeTransform transform)
            return;

        var duration = new Duration(TimeSpan.FromMilliseconds(100));
        var storyboard = new Storyboard();
        foreach (var property in new[] { "ScaleX", "ScaleY" })
        {
            // EnableDependentAnimation: a storyboard targeting the CompositeTransform object directly is
            // classified as a dependent animation; without the flag it silently does nothing.
            var animation = new DoubleAnimation { To = scale, Duration = duration, EnableDependentAnimation = true };
            Storyboard.SetTarget(animation, transform);
            Storyboard.SetTargetProperty(animation, property);
            storyboard.Children.Add(animation);
        }
        storyboard.Begin();
    }
}
