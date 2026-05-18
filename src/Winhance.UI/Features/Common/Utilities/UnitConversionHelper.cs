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
            "minutes" => systemValue / 60,        // powercfg stores time in seconds
            "hours" => systemValue / 3600,
            // USB selective suspend timeout (the sole "Milliseconds" setting today) is
            // stored natively in milliseconds in the registry, so the display unit matches
            // the system unit 1:1. Previously this branch returned `systemValue * 1000`,
            // which inflated RecDC=1000 to a display value of 1,000,000 — exceeding the
            // NumericRange MaxValue of 100,000, getting clamped by the NumberBox, then
            // re-applied to the registry as a corrupted value.
            "milliseconds" => systemValue,
            _ => systemValue
        };
    }
}
