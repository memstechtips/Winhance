using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Winhance.WPF.Features.Common.Converters
{
    /// <summary>
    /// Converts a boolean value to a theme string ("Dark" or "Light") or theme-specific colors
    /// </summary>
    public class BooleanToThemeConverter : IValueConverter, IMultiValueConverter
    {
        // Default track colors
        private static readonly Color DarkTrackColor = Color.FromRgb(68, 68, 68);
        private static readonly Color LightTrackColor = Color.FromRgb(204, 204, 204);

        // Checked track colors
        private static readonly Color DarkCheckedTrackColor = Color.FromRgb(85, 85, 85);
        private static readonly Color LightCheckedTrackColor = Color.FromRgb(66, 66, 66);

        // Knob colors
        private static readonly Color DefaultKnobColor = Color.FromRgb(255, 255, 255); // White for unchecked
        private static readonly Color DarkCheckedKnobColor = Color.FromRgb(255, 222, 0); // Yellow for dark theme checked
        private static readonly Color LightCheckedKnobColor = Color.FromRgb(66, 66, 66); // Gray for light theme checked

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isDarkTheme = false;
            bool isChecked = false;

            // Handle different input types for theme
            if (value is bool boolValue)
            {
                isDarkTheme = boolValue;
            }
            else if (value is string stringValue)
            {
                isDarkTheme = stringValue.Equals("Dark", StringComparison.OrdinalIgnoreCase);
            }

            // Extract checked state from parameter if provided
            if (parameter is string paramString)
            {
                string[] parts = paramString.Split(':');
                string elementType = parts[0];

                // Check if we have checked state information
                if (parts.Length > 1)
                {
                    isChecked = parts[1].Equals("Checked", StringComparison.OrdinalIgnoreCase);
                }

                if (targetType == typeof(Brush) || targetType == typeof(SolidColorBrush))
                {
                    if (elementType.Equals("Track", StringComparison.OrdinalIgnoreCase))
                    {
                        if (isChecked)
                        {
                            return new SolidColorBrush(isDarkTheme ? DarkCheckedTrackColor : LightCheckedTrackColor);
                        }
                        else
                        {
                            return new SolidColorBrush(isDarkTheme ? DarkTrackColor : LightTrackColor);
                        }
                    }
                    else if (elementType.Equals("Knob", StringComparison.OrdinalIgnoreCase))
                    {
                        if (isChecked)
                        {
                            // Use the hardcoded colors for better reliability
                            return new SolidColorBrush(isDarkTheme ? DarkCheckedKnobColor : LightCheckedKnobColor);
                        }
                        else
                        {
                            // Use white for unchecked knobs in both themes
                            return new SolidColorBrush(DefaultKnobColor);
                        }
                    }
                }
            }

            // Default behavior - return theme string
            return isDarkTheme ? "Dark" : "Light";
        }

        // Implementation for IMultiValueConverter
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2)
                return Binding.DoNothing;

            bool isDarkTheme = false;
            bool isChecked = false;

            // First value is the theme
            if (values[0] is bool themeBool)
            {
                isDarkTheme = themeBool;
            }
            else if (values[0] is string themeString)
            {
                isDarkTheme = themeString.Equals("Dark", StringComparison.OrdinalIgnoreCase);
            }

            // Second value is the checked state
            if (values[1] is bool checkedBool)
            {
                isChecked = checkedBool;
            }

            // Determine which element we're styling based on parameter
            string elementType = parameter as string ?? "Track";

            if (targetType == typeof(Brush) || targetType == typeof(SolidColorBrush))
            {
                if (elementType.Equals("Track", StringComparison.OrdinalIgnoreCase))
                {
                    if (isChecked)
                    {
                        return new SolidColorBrush(isDarkTheme ? DarkCheckedTrackColor : LightCheckedTrackColor);
                    }
                    else
                    {
                        return new SolidColorBrush(isDarkTheme ? DarkTrackColor : LightTrackColor);
                    }
                }
                else if (elementType.Equals("Knob", StringComparison.OrdinalIgnoreCase))
                {
                    if (isChecked)
                    {
                        // Use the hardcoded colors for better reliability
                        return new SolidColorBrush(isDarkTheme ? DarkCheckedKnobColor : LightCheckedKnobColor);
                    }
                    else
                    {
                        // Use white for unchecked knobs in both themes
                        return new SolidColorBrush(DefaultKnobColor);
                    }
                }
            }

            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string themeString)
            {
                return themeString.Equals("Dark", StringComparison.OrdinalIgnoreCase);
            }

            return false; // Default to Light theme (false) if value is not a string
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            // We don't need to implement this for our use case
            return targetTypes.Select(t => Binding.DoNothing).ToArray();
        }
    }
}