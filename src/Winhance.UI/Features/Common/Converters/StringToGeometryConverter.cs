using Microsoft.UI.Xaml.Data;
using Winhance.UI.Features.Common.Helpers;

namespace Winhance.UI.Features.Common.Converters;

public sealed partial class StringToGeometryConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string pathData && !string.IsNullOrEmpty(pathData))
        {
            return GeometryHelper.FromPathData(pathData);
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
