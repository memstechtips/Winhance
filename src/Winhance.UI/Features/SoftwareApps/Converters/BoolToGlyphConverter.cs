using Microsoft.UI.Xaml.Data;

namespace Winhance.UI.Features.SoftwareApps.Converters;

/// <summary>
/// Converts a boolean (CanBeReinstalled) to a glyph for the reinstallable icon.
/// </summary>
public partial class BoolToGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool canBeReinstalled)
        {
            // Checkmark for can reinstall, X for cannot
            return canBeReinstalled ? "\uE73E" : "\uE711";
        }
        return "\uE711"; // Default to X
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
