using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace Winhance.UI.Features.Common.Helpers;

/// <summary>
/// Parses SVG path data into a <see cref="Geometry"/>. XamlBindingHelper.ConvertValue is the only
/// path parser WinUI exposes to code, and it returns object - so every call site had to repeat the
/// same cast and the same fully-qualified type names.
/// </summary>
public static class GeometryHelper
{
    /// <summary>Throws on malformed path data, matching the hard cast most call sites already
    /// used. StringToGeometryConverter previously used `as Geometry` and returned null on a bad
    /// parse, so that one converter now surfaces the error instead of rendering nothing.</summary>
    public static Geometry FromPathData(string pathData) =>
        (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), pathData);

    /// <summary>Path data held under an application resource key. Throws when the key is absent,
    /// which is the existing behaviour and correct - the keys are the app's own constants.</summary>
    public static Geometry FromResource(string resourceKey) =>
        FromPathData((string)Application.Current.Resources[resourceKey]);
}
