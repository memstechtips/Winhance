using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Winhance.Core.Features.Common.Enums;

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

    /// <summary>
    /// Converts an SettingDefinition to Visibility, hiding main power settings that are shown above.
    /// </summary>
    public class AdvancedSettingVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts an SettingDefinition to Visibility.
        /// </summary>
        /// <param name="value">The SettingDefinition value.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="parameter">The converter parameter.</param>
        /// <param name="culture">The culture information.</param>
        /// <returns>Visibility.Collapsed for main power settings (power plan, display, sleep), otherwise Visibility.Visible.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Skip the main power settings that are already shown in the primary section
            if (value != null)
            {
                var setting = value.GetType().GetProperty("Id")?.GetValue(value)?.ToString();
                if (!string.IsNullOrEmpty(setting))
                {
                    // Hide these settings as they're shown in the primary power settings section
                    var hiddenSettings = new[]
                    {
                        "active-power-plan",
                        "display-timeout",
                        "sleep-timeout",
                        "3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e", // Display timeout GUID
                        "29f6c1db-86da-48c5-9fdb-f2b67b1f44da"  // Sleep timeout GUID
                    };

                    if (Array.Exists(hiddenSettings, s => s.Equals(setting, StringComparison.OrdinalIgnoreCase)))
                    {
                        return Visibility.Collapsed;
                    }
                }
            }
            return Visibility.Visible;
        }

        /// <summary>
        /// Converts back from Visibility to SettingDefinition.
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
