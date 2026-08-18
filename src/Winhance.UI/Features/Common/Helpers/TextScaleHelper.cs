using Windows.UI.ViewManagement;

namespace Winhance.UI.Features.Common.Helpers;

// WinUI 3 applies UISettings.TextScaleFactor to TextBlock font sizes automatically, but fixed container
// dimensions baked into XAML (UniformWrapPanel ItemWidth/ItemHeight, fixed Grid.Height in DataTemplates) do NOT
// scale; this supplies the factor so code-behind can grow them. Read once at startup - a slider change needs an
// app restart, like most Win32/WinUI apps.
internal static class TextScaleHelper
{
    private static readonly double _factor = ReadFactor();

    public static double Factor => _factor;

    public static bool IsScaled => _factor > 1.0 + 0.001;

    private static double ReadFactor()
    {
        try
        {
            return new UISettings().TextScaleFactor;
        }
        catch
        {
            // UISettings can throw in some elevated/limited contexts; fall back
            // to no scaling rather than break layout.
            return 1.0;
        }
    }
}
