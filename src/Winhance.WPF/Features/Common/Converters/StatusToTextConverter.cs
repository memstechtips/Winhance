using System;
using System.Globalization;
using System.Windows.Data;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.WPF.Features.Common.Converters
{
    public class StatusToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RegistrySettingStatus status)
            {
                return status switch
                {
                    RegistrySettingStatus.Applied => "Applied",
                    RegistrySettingStatus.NotApplied => "Not Applied",
                    RegistrySettingStatus.Modified => "Modified",
                    RegistrySettingStatus.Error => "Error",
                    _ => "Unknown"
                };
            }

            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
