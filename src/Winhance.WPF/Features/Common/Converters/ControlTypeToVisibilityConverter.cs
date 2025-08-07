using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.WPF.Features.Common.Converters
{
    /// <summary>
    /// Converter that shows/hides controls based on ControlType
    /// </summary>
    public class EnumTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ControlType controlType)
            {
                return controlType == ControlType.ComboBox ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter that shows/hides numeric controls based on ControlType
    /// </summary>
    public class NumericTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ControlType controlType)
            {
                // Show numeric controls for NumericUpDown control type
                return controlType == ControlType.NumericUpDown ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter that shows/hides boolean controls based on ControlType
    /// </summary>
    public class BoolTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ControlType controlType)
            {
                // Show toggle for BinaryToggle control type
                return controlType == ControlType.BinaryToggle ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
