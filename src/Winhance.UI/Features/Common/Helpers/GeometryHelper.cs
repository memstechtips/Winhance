using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace Winhance.UI.Features.Common.Helpers;

// XamlBindingHelper.ConvertValue is the only path parser WinUI exposes to code, and it returns object.
public static class GeometryHelper
{
    // Throws on malformed path data, so a bad parse surfaces instead of rendering nothing.
    public static Geometry FromPathData(string pathData) =>
        (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), pathData);

    // Throws when the key is absent - the keys are the app's own constants.
    public static Geometry FromResource(string resourceKey) =>
        FromPathData((string)Application.Current.Resources[resourceKey]);
}
