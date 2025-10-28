using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Winhance.WPF.Converters;

public class InstalledStatusToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenBrush = new SolidColorBrush(Color.FromRgb(0, 255, 60));
    private static readonly SolidColorBrush RedBrush = new SolidColorBrush(Color.FromRgb(255, 40, 0));
    private static readonly SolidColorBrush GrayBrush = new SolidColorBrush(Colors.Gray);

    static InstalledStatusToColorConverter()
    {
        GreenBrush.Freeze();
        RedBrush.Freeze();
        GrayBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isInstalled)
        {
            return isInstalled ? GreenBrush : RedBrush;
        }
        return GrayBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}