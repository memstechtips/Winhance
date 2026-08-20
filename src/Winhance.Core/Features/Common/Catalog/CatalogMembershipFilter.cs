namespace Winhance.Core.Features.Common.Catalog;

public readonly record struct HardwareCaps(bool HasBattery, bool SupportsHybridSleep);

// Does NOT include the powercfg-existence filter (machine-dependent and mutating); that is applied separately.
public static class CatalogMembershipFilter
{
    public static bool PassesHardware(Availability a, HardwareCaps c)
    {
        foreach (var req in a.Hardware)
        {
            switch (req)
            {
                case HardwareRequirement.Battery when !c.HasBattery: return false;
                case HardwareRequirement.HybridSleepCapable when !c.SupportsHybridSleep: return false;
            }
        }
        return true;
    }

    public static bool IsAvailable(Setting s, WinBuild build, HardwareCaps caps) =>
        s.Availability.Allows(build) && PassesHardware(s.Availability, caps);

    // The hardware gate still applies; only the build range is ignored.
    public static bool IsAvailableIgnoringOsBuild(Setting s, HardwareCaps caps) =>
        PassesHardware(s.Availability, caps);
}
