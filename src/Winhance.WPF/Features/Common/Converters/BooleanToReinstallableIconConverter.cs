using System.Globalization;
using System.Windows.Data;
using MahApps.Metro.IconPacks;

namespace Winhance.WPF.Converters
{
    public class BooleanToReinstallableIconConverter : IValueConverter
    {
        private static readonly PackIconMaterialKind SyncIcon = PackIconMaterialKind.Sync;
        private static readonly PackIconMaterialKind SyncDisabledIcon = PackIconMaterialKind.SyncOff;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? SyncIcon : SyncDisabledIcon;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
