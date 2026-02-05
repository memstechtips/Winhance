using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Winhance.UI.Features.Common.Controls;

// ComboBox subclass that fixes dropdown popup positioning issues in WinUI3.
// When a ComboBox dropdown opens, the selected item moves into the popup,
// causing the main control to re-layout at a smaller size and misposition the popup.
// This fix caches the width before dropdown opens and reapplies it.
// See: https://github.com/microsoft/microsoft-ui-xaml/issues/9567
public class ComboBoxEx : ComboBox
{
    private double _cachedWidth;

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
