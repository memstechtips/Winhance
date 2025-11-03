using System;
using System.Globalization;
using System.Windows.Data;
using MahApps.Metro.IconPacks;

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
                        return PackIconMaterialKind.WindowMaximize;
                    case "WindowRestore":
                        return PackIconMaterialKind.WindowRestore;
                    default:
                        return PackIconMaterialKind.WindowMaximize;
                }
            }

            return PackIconMaterialKind.WindowMaximize;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PackIconMaterialKind kind)
            {
                switch (kind)
                {
                    case PackIconMaterialKind.WindowMaximize:
                        return "WindowMaximize";
                    case PackIconMaterialKind.WindowRestore:
                        return "WindowRestore";
                    default:
                        return "WindowMaximize";
                }
            }

            return "WindowMaximize";
        }
    }
}
