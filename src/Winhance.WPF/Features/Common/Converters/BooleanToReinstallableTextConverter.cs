using System;
using System.Globalization;
using System.Windows.Data;

namespace Winhance.WPF.Converters
{
    /// <summary>
    /// Converts a boolean value indicating whether an item can be reinstalled to a descriptive text.
    /// </summary>
    public class BooleanToReinstallableTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? "Is Installable" : "Is Not Installable";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
