using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Winhance.WPF.Converters;

public class InstalledStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? "Installed" : "Not Installed";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}