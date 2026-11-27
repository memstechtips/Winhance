using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Enums;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

// A card input control binds x:Bind straight through Setting, so the view model's own PropertyChanged drives every
// refresh. Its XAML root names this type, which is why it cannot be generic (see SectionPage).
public abstract class SettingBoundControl : UserControl
{
    public static readonly DependencyProperty SettingProperty = DependencyProperty.Register(
        nameof(Setting), typeof(SettingItemViewModel), typeof(SettingBoundControl), new PropertyMetadata(null));

    public SettingItemViewModel? Setting
    {
        get => (SettingItemViewModel?)GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    // x:Load takes a bool, and a binding cannot nest the view model's Visibility call inside a converter call.
    public static bool HasOverlay(SettingItemViewModel? setting, SettingDetectionOutcome outcome)
        => setting?.OverlayVisibilityFor(outcome) == Visibility.Visible;

    // x:Bind calls a function again when one of its arguments changes, so an argument named _ is the view model
    // property the result is read from: it is passed to trigger the call, not to be used.
    public static string InputAutomationName(SettingItemViewModel? setting, SettingInputMode mode, string _)
        => setting?.InputAutomationNameForMode(mode) ?? string.Empty;
}
