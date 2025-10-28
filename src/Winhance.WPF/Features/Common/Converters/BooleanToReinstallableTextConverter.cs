using System;
using System.Globalization;
using System.Windows.Data;

namespace Winhance.WPF.Converters
{
    public class BooleanToReinstallableTextConverter : IValueConverter
    {
        private const string YesText = "Can be reinstalled";
        private const string NoText = "Cannot be reinstalled";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? YesText : NoText;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
