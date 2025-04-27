using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.WPF.Features.Common.Converters
{
    public class RemoveActionToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // If the action type is Remove, return Visible, otherwise return Collapsed
            if (value is RegistryActionType actionType)
            {
                return actionType == RegistryActionType.Remove ? Visibility.Visible : Visibility.Collapsed;
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
