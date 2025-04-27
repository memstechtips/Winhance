using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Winhance.WPF.Converters;

public class InstalledStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value 
        ? new SolidColorBrush(Color.FromRgb(0, 255, 60)) // Electric Green (#00FF3C) 
        : new SolidColorBrush(Color.FromRgb(255, 40, 0)); // Ferrari Red (#FF2800)
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}