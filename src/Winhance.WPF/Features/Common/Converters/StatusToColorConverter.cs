using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.WPF.Features.Common.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Handle RegistrySettingStatus
            if (value is RegistrySettingStatus status)
            {
                return status switch
                {
                    RegistrySettingStatus.Applied => new SolidColorBrush(Color.FromRgb(0, 255, 60)), // Electric Green (#00FF3C)
                    RegistrySettingStatus.NotApplied => new SolidColorBrush(Color.FromRgb(255, 40, 0)), // Ferrari Red (#FF2800)
                    RegistrySettingStatus.Modified => new SolidColorBrush(Colors.Orange),
                    RegistrySettingStatus.Error => new SolidColorBrush(Colors.Gray),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            
            // Handle boolean for reinstallability status
            if (value is bool canBeReinstalled)
            {
                return canBeReinstalled 
                    ? new SolidColorBrush(Color.FromRgb(0, 165, 255)) // Blue (#00A5FF)
                    : new SolidColorBrush(Color.FromRgb(255, 40, 0));  // Ferrari Red (#FF2800)
            }

            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
