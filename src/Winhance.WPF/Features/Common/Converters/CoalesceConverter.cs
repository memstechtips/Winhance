using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace Winhance.WPF.Features.Common.Converters
{
    public class CoalesceConverter : IMultiValueConverter
    {
        public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return values.FirstOrDefault(v => v != null && v != System.Windows.DependencyProperty.UnsetValue);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
