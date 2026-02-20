using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Converts a string path data value to a Geometry object for use with Path elements.
/// </summary>
public partial class StringToGeometryConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string pathData && !string.IsNullOrEmpty(pathData))
        {
            return XamlBindingHelper.ConvertValue(typeof(Geometry), pathData) as Geometry;
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
