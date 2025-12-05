using System;
using System.Globalization;
using System.Windows.Data;
using Winhance.WPF.Features.Common.Services;
using MahApps.Metro.IconPacks;

namespace Winhance.WPF.Features.Common.Converters
{
    public class SectionIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string sectionName)
            {
                return FeatureRegistry.GetIcon(sectionName);
            }
            return PackIconMaterialKind.Cog;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
