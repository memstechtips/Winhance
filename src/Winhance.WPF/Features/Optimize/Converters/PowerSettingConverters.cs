using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.WPF.Features.Optimize.Converters
{
    /// <summary>
    /// Converts a boolean value to Visibility.
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean to Visibility.
        /// </summary>
        /// <param name="value">The boolean value.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="parameter">The converter parameter.</param>
        /// <param name="culture">The culture information.</param>
        /// <returns>Visibility.Visible if true, Visibility.Collapsed if false.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        /// <summary>
        /// Converts back from Visibility to boolean.
        /// </summary>
        /// <param name="value">The Visibility value.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="parameter">The converter parameter.</param>
        /// <param name="culture">The culture information.</param>
        /// <returns>True if Visible, False if Collapsed.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            return false;
        }
    }
    /// <summary>
    /// Converts a PowerSettingType to Visibility for enum type settings.
    /// </summary>
    public class EnumTypeToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a PowerSettingType to Visibility.
        /// </summary>
        /// <param name="value">The PowerSettingType value.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="parameter">The converter parameter.</param>
        /// <param name="culture">The culture information.</param>
        /// <returns>Visibility.Visible if the value is PowerSettingType.Enum, otherwise Visibility.Collapsed.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PowerSettingType settingType && settingType == PowerSettingType.Enum)
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        /// <summary>
        /// Converts back from Visibility to PowerSettingType.
        /// </summary>
        /// <param name="value">The Visibility value.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="parameter">The converter parameter.</param>
        /// <param name="culture">The culture information.</param>
        /// <returns>Not implemented.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a PowerSettingType to Visibility for numeric type settings.
    /// </summary>
    public class NumericTypeToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a PowerSettingType to Visibility.
        /// </summary>
        /// <param name="value">The PowerSettingType value.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="parameter">The converter parameter.</param>
        /// <param name="culture">The culture information.</param>
        /// <returns>Visibility.Visible if the value is PowerSettingType.Numeric, otherwise Visibility.Collapsed.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PowerSettingType settingType && settingType == PowerSettingType.Numeric)
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        /// <summary>
        /// Converts back from Visibility to PowerSettingType.
        /// </summary>
        /// <param name="value">The Visibility value.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="parameter">The converter parameter.</param>
        /// <param name="culture">The culture information.</param>
        /// <returns>Not implemented.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a boolean value to an expand/collapse string.
    /// </summary>
    public class BoolToExpandCollapseConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean to "Collapse" or "Expand".
        /// </summary>
        /// <param name="value">The boolean value.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="parameter">The converter parameter.</param>
        /// <param name="culture">The culture information.</param>
        /// <returns>"Collapse" if true, "Expand" if false.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isExpanded)
            {
                return isExpanded ? "Collapse" : "Expand";
            }
            return "Expand";
        }

        /// <summary>
        /// Converts back from string to boolean.
        /// </summary>
        /// <param name="value">The string value.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="parameter">The converter parameter.</param>
        /// <param name="culture">The culture information.</param>
        /// <returns>Not implemented.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a boolean value to the inverse of its visibility.
    /// </summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean to the inverse of its visibility.
        /// </summary>
        /// <param name="value">The boolean value.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="parameter">The converter parameter.</param>
        /// <param name="culture">The culture information.</param>
        /// <returns>Visibility.Collapsed if true, Visibility.Visible if false.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        /// <summary>
        /// Converts back from Visibility to boolean.
        /// </summary>
        /// <param name="value">The Visibility value.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="parameter">The converter parameter.</param>
        /// <param name="culture">The culture information.</param>
        /// <returns>Not implemented.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
