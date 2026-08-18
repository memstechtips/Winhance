namespace Winhance.UI.Features.Common.Utilities;

internal static class UnitConversionHelper
{
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

    // Minutes/hours multiply; milliseconds and everything else are 1:1, so
    // ConvertFromSystemUnits(ConvertToSystemUnits(x)) == x for any units.
    public static int ConvertToSystemUnits(int displayValue, string? displayUnits)
    {
        return displayUnits?.ToLowerInvariant() switch
        {
            "minutes" => displayValue * 60,        // powercfg stores time in seconds
            "hours" => displayValue * 3600,
            "milliseconds" => displayValue,
            _ => displayValue
        };
    }
}
