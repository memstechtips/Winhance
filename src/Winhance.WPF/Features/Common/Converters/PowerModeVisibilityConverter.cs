using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Converters;

public class PowerModeVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 4 || values.Any(v => v == null))
            return Visibility.Collapsed;

        if (values[0] is not InputType inputType ||
            values[1] is not bool supportsSeparateACDC ||
            values[2] is not bool hasBattery ||
            values[3] is not string mode)
            return Visibility.Collapsed;

        var controlType = parameter as string ?? "";

        var isCorrectControlType = controlType switch
        {
            "ComboBox" => inputType == InputType.Selection,
            "NumericUpDown" => inputType == InputType.NumericRange,
            _ => false
        };

        if (!isCorrectControlType)
            return Visibility.Collapsed;

        return mode switch
        {
            "NonPower" => !supportsSeparateACDC ? Visibility.Visible : Visibility.Collapsed,
            "PowerSingleAC" => (supportsSeparateACDC && !hasBattery) ? Visibility.Visible : Visibility.Collapsed,
            "PowerDual" => (supportsSeparateACDC && hasBattery) ? Visibility.Visible : Visibility.Collapsed,
            _ => Visibility.Collapsed
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}