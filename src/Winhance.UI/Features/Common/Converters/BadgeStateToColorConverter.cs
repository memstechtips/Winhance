using System;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Winhance.Core.Features.Common.Enums;
using Windows.UI;

namespace Winhance.UI.Features.Common.Converters;

public partial class BadgeStateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is SettingBadgeState state)
        {
            return state switch
            {
                SettingBadgeState.Recommended => new SolidColorBrush(Colors.Green),
                SettingBadgeState.Default => new SolidColorBrush(Colors.Gray),
                SettingBadgeState.Custom => new SolidColorBrush(Color.FromArgb(255, 255, 165, 0)),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
