using System;
using System.Globalization;
using System.Windows.Data;

namespace Winhance.WPF.Features.Common.Converters
{
    /// <summary>
    /// Converter that selects between two ViewModels based on a boolean condition
    /// </summary>
    public class TabViewModelSelector : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 3)
                return null;

            bool isFirstTabSelected = (bool)values[0];
            object firstViewModel = values[1];
            object secondViewModel = values[2];

            return isFirstTabSelected ? firstViewModel : secondViewModel;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
