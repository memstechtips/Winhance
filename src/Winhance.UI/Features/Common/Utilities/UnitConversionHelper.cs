namespace Winhance.UI.Features.Common.Utilities;

/// <summary>
/// Shared utility for converting power setting values between system units and display units.
/// </summary>
internal static class UnitConversionHelper
{
    /// <summary>
    /// Converts a raw powercfg API value to display units based on the display unit string.
    /// For example, converts 1200 seconds to 20 minutes when display units are "Minutes".
    /// </summary>
    public static int ConvertFromSystemUnits(int systemValue, string? displayUnits)
    {
        return displayUnits?.ToLowerInvariant() switch
        {
            "minutes" => systemValue / 60,
            "hours" => systemValue / 3600,
            "milliseconds" => systemValue * 1000,
            _ => systemValue
        };
    }
}
