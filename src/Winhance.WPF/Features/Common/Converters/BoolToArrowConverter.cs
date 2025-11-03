using System;
using System.Globalization;
using System.Windows.Data;

namespace Winhance.WPF.Features.Common.Converters
{
    /// <summary>
    /// Converts a boolean value to an arrow character for UI display.
    /// True returns an up arrow, False returns a down arrow.
    /// </summary>
    public class BoolToArrowConverter : IValueConverter
    {
        public static BoolToArrowConverter Instance { get; } = new BoolToArrowConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isExpanded)
            {
                // Up arrow for expanded, down arrow for collapsed
                return isExpanded ? "\uE70E" : "\uE70D";
            }

            // Default to down arrow if value is not a boolean
            return "\uE70D";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // This converter doesn't support converting back
            throw new NotImplementedException();
        }
    }
}
