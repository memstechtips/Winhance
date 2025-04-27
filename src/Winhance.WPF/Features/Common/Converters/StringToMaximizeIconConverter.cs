using System;
using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace Winhance.WPF.Features.Common.Converters
{
    public class StringToMaximizeIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string iconName)
            {
                switch (iconName)
                {
                    case "WindowMaximize":
                        return PackIconKind.WindowMaximize;
                    case "WindowRestore":
                        return PackIconKind.WindowRestore;
                    default:
                        return PackIconKind.WindowMaximize;
                }
            }
            
            return PackIconKind.WindowMaximize;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PackIconKind kind)
            {
                switch (kind)
                {
                    case PackIconKind.WindowMaximize:
                        return "WindowMaximize";
                    case PackIconKind.WindowRestore:
                        return "WindowRestore";
                    default:
                        return "WindowMaximize";
                }
            }
            
            return "WindowMaximize";
        }
    }
}
