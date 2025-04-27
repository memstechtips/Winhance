using System;
using System.Globalization;
using System.Windows.Data;
using Winhance.WPF.Features.Common.Resources;

namespace Winhance.WPF.Features.Common.Converters
{
    /// <summary>
    /// Converts an icon name to a Material Symbols Unicode character.
    /// </summary>
    public class IconNameToSymbolConverter : IValueConverter
    {
        public static IconNameToSymbolConverter Instance { get; } = new IconNameToSymbolConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string iconName)
            {
                return MaterialSymbols.GetIcon(iconName);
            }
            
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
