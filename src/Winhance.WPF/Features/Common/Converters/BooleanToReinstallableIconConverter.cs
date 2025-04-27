using System;
using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace Winhance.WPF.Converters
{
    /// <summary>
    /// Converts a boolean value indicating whether an item can be reinstalled to the appropriate MaterialDesign icon.
    /// </summary>
    public class BooleanToReinstallableIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Return the appropriate MaterialDesign PackIconKind
            return (bool)value ? PackIconKind.Sync : PackIconKind.SyncDisabled;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            throw new NotImplementedException();
        }
    }
}
