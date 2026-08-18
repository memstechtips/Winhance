namespace Winhance.Core.Features.Common.Catalog;

// All promoted = show-all; none promoted = hide-all; no subkeys, no IsPromoted values, or a mix = Custom.
public sealed class SystemTrayDetector : IStateDetector
{
    private const string KeyPath = @"HKEY_CURRENT_USER\Control Panel\NotifyIconSettings";

    private readonly string _showAllLabel;
    private readonly string _hideAllLabel;

    public SystemTrayDetector(string showAllLabel, string hideAllLabel)
    {
        _showAllLabel = showAllLabel;
        _hideAllLabel = hideAllLabel;
    }

    public string? Detect(Setting setting, IDetectionContext context)
    {
        var subKeys = context.GetSubKeyNames(KeyPath);
        if (subKeys.Length == 0)
            return null; // Custom

        int total = 0, promoted = 0;
        foreach (var subKey in subKeys)
        {
            var raw = context.GetValue($@"{KeyPath}\{subKey}", "IsPromoted");
            if (raw == null)
                continue;
            total++;
            if (Convert.ToInt32(raw) == 1)
                promoted++;
        }

        if (total == 0)
            return null;                 // Custom (no IsPromoted values)
        if (promoted == total)
            return _showAllLabel;
        if (promoted == 0)
            return _hideAllLabel;
        return null;                     // Custom (mixed)
    }
}
