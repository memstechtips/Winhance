using Windows.UI.ViewManagement;

namespace Winhance.UI.Features.Common.Helpers;

/// <summary>
/// Reads the Windows system text-scale factor (Settings → Ease of Access →
/// Make text bigger) and exposes helpers for scaling fixed layout dimensions
/// to match.
///
/// WinUI 3 already applies <see cref="UISettings.TextScaleFactor"/> automatically
/// to TextBlock font sizes (TextBlock.IsTextScaleFactorEnabled defaults to true),
/// but container dimensions baked into XAML with fixed widths/heights (e.g.
/// UniformWrapPanel ItemWidth/ItemHeight, fixed Grid.Height inside DataTemplates)
/// do NOT auto-scale. This helper provides the factor so code-behind can grow
/// those containers to match the scaled text.
///
/// Pulled once at app startup; runtime slider changes require an app restart
/// (matches how most Win32/WinUI 3 apps handle this setting).
/// </summary>
internal static class TextScaleHelper
{
    private static readonly double _factor = ReadFactor();

    /// <summary>System text-scale factor (1.0 = 100%, 1.5 = 150%, etc.).</summary>
    public static double Factor => _factor;

    /// <summary>True when the user has bumped text scale above 100%.</summary>
    public static bool IsScaled => _factor > 1.0 + 0.001;

    /// <summary>Multiplies the given base dimension by the current text-scale factor.</summary>
    public static double Scale(double baseValue) => baseValue * _factor;

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
