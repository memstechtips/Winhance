using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Converts an integer count to a Visibility value, with the count inverted.
/// Returns Visible if the count is 0, otherwise returns Collapsed.
/// </summary>
public class InverseCountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int count)
        {
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
