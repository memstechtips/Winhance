using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Winhance.WPF.Features.Common.Converters
{
    /// <summary>
    /// Converts a boolean value (IsDarkTheme) to the appropriate themed icon path
    /// </summary>
    public class BooleanToThemeIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isDarkTheme = false;

            // Handle different input types for theme
            if (value is bool boolValue)
            {
                isDarkTheme = boolValue;
                Debug.WriteLine($"BooleanToThemeIconConverter: isDarkTheme = {isDarkTheme} (from bool)");
            }
            else if (value is string stringValue)
            {
                isDarkTheme = stringValue.Equals("Dark", StringComparison.OrdinalIgnoreCase);
                Debug.WriteLine($"BooleanToThemeIconConverter: isDarkTheme = {isDarkTheme} (from string '{stringValue}')");
            }
            else
            {
                Debug.WriteLine($"BooleanToThemeIconConverter: value is {(value == null ? "null" : value.GetType().Name)}");
            }

            // Parameter should be in format "darkIconPath|lightIconPath"
            if (parameter is string paramString)
            {
                string[] iconPaths = paramString.Split('|');
                if (iconPaths.Length >= 2)
                {
                    string darkIconPath = iconPaths[0];
                    string lightIconPath = iconPaths[1];

                    string selectedPath = isDarkTheme ? darkIconPath : lightIconPath;
                    Debug.WriteLine($"BooleanToThemeIconConverter: Selected path = {selectedPath}");

                    // If the target type is BitmapImage, create and return it
                    if (targetType == typeof(BitmapImage))
                    {
                        return new BitmapImage(new Uri(selectedPath, UriKind.Relative));
                    }

                    // Otherwise return the path string
                    return selectedPath;
                }
            }

            // Default fallback icon path
            Debug.WriteLine("BooleanToThemeIconConverter: Using fallback icon path");
            return "/Resources/AppIcons/winhance-rocket.ico";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // This converter doesn't support converting back
            throw new NotImplementedException();
        }
    }
}
