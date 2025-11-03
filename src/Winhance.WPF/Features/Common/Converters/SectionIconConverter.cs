using System;
using System.Globalization;
using System.Windows.Data;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Converters
{
    public class SectionIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string sectionName)
            {
                return FeatureCategoryIcons.GetIcon(sectionName);
            }
            return "Cog";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
