using Microsoft.UI.Xaml.Controls;

namespace Winhance.UI.Features.Common.Controls;

// Built only while a zoom change shows, and dropped when the timer hides it. It sits outside ZoomHost so the zoom
// level it reports does not scale it.
public sealed partial class ZoomIndicator : UserControl
{
    public ZoomIndicator() => InitializeComponent();

    public void SetText(string text) => ZoomText.Text = text;
}
