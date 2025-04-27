using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.WPF.Features.Common.Converters
{
    public class RegistryValueStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return DependencyProperty.UnsetValue;

            if (value is RegistrySettingStatus status)
            {
                return status switch
                {
                    RegistrySettingStatus.NotApplied => "Not Applied",
                    RegistrySettingStatus.Applied => "Applied",
                    RegistrySettingStatus.Modified => "Modified",
                    RegistrySettingStatus.Error => "Error",
                    RegistrySettingStatus.Unknown => "Unknown",
                    _ => "Unknown"
                };
            }

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RegistryValueStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return DependencyProperty.UnsetValue;

            if (value is RegistrySettingStatus status)
            {
                return status switch
                {
                    RegistrySettingStatus.NotApplied => new SolidColorBrush(Colors.Gray),
                    RegistrySettingStatus.Applied => new SolidColorBrush(Colors.Green),
                    RegistrySettingStatus.Modified => new SolidColorBrush(Colors.Orange),
                    RegistrySettingStatus.Error => new SolidColorBrush(Colors.Red),
                    RegistrySettingStatus.Unknown => new SolidColorBrush(Colors.Gray),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RegistryValueStatusToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return DependencyProperty.UnsetValue;

            if (value is RegistrySettingStatus status)
            {
                return status switch
                {
                    RegistrySettingStatus.NotApplied => "⚪", // Empty circle
                    RegistrySettingStatus.Applied => "✅", // Green checkmark
                    RegistrySettingStatus.Modified => "⚠️", // Warning
                    RegistrySettingStatus.Error => "❌", // Red X
                    RegistrySettingStatus.Unknown => "❓", // Question mark
                    _ => "❓"
                };
            }

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
