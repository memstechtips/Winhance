using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;

namespace Winhance.UI.Features.Common.Controls;

// ComboBox subclass that fixes dropdown popup positioning issues in WinUI3.
// When a ComboBox dropdown opens, the selected item moves into the popup,
// causing the main control to re-layout at a smaller size and misposition the popup.
// This fix caches the width before dropdown opens and reapplies it.
// See: https://github.com/microsoft/microsoft-ui-xaml/issues/9567
//
// Also overrides arrow key behavior: when closed, Up/Down keys are not consumed
// so they can bubble up to the parent ListView for item-to-item navigation.
public class ComboBoxEx : ComboBox
{
    private double _cachedWidth;

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        // When dropdown is closed, let Up/Down arrow keys bubble up to parent
        // ListView for navigation between settings cards instead of cycling selection
        if (!IsDropDownOpen && (e.Key == VirtualKey.Up || e.Key == VirtualKey.Down))
        {
            // Don't call base — let the event bubble for ListView navigation
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnDropDownOpened(object e)
    {
        // Use cached width, or fall back to ActualWidth if not yet measured
        var widthToApply = _cachedWidth > 0 ? _cachedWidth : ActualWidth;
        if (widthToApply > 0)
            Width = widthToApply;

        base.OnDropDownOpened(e);
    }

    protected override void OnDropDownClosed(object e)
    {
        Width = double.NaN;
        base.OnDropDownClosed(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var baseSize = base.MeasureOverride(availableSize);

        // Cache width if it's a valid measurement (not just the chevron minimum)
        if (baseSize.Width > 64)
            _cachedWidth = baseSize.Width;

        return baseSize;
    }
}
