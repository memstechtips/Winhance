using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>The machine capabilities a catalog membership filter needs - the catalog-model equivalent of the
/// probes HardwareCompatibilityFilter reads from IHardwareDetectionService (battery / lid / brightness /
/// hybrid-sleep).</summary>
public readonly record struct HardwareCaps(bool HasBattery, bool HasLid, bool SupportsBrightness, bool SupportsHybridSleep);

/// <summary>Pure catalog-membership gating: reproduces the old WindowsCompatibilityFilter (OS build) +
/// HardwareCompatibilityFilter (hardware caps) decisions from <see cref="Setting.Availability"/>, so the settings
/// registry can be sourced from the catalog instead of the old SettingDefinition providers. Does NOT include the
/// powercfg-existence filter (machine-dependent + mutating - deferred to its own slice); callers that need existence
/// still run PowerSettingsValidationService.</summary>
public static class CatalogMembershipFilter
{
    /// <summary>Mirrors HardwareCompatibilityFilter's five checks against Setting.Availability.Hardware.</summary>
    public static bool PassesHardware(Availability a, HardwareCaps c)
    {
        foreach (var req in a.Hardware)
        {
            switch (req)
            {
                case HardwareRequirement.Battery when !c.HasBattery: return false;
                case HardwareRequirement.Lid when !c.HasLid: return false;
                case HardwareRequirement.Desktop when c.HasBattery || c.HasLid: return false;
                case HardwareRequirement.BrightnessSupport when !c.SupportsBrightness: return false;
                case HardwareRequirement.HybridSleepCapable when !c.SupportsHybridSleep: return false;
            }
        }
        return true;
    }

    /// <summary>True when the setting is shown on a machine at the given build with the given hardware caps - the OS
    /// gate (Availability.Allows) AND the hardware gate (PassesHardware). Existence gating is NOT applied here.</summary>
    public static bool IsAvailable(Setting s, WinBuild build, HardwareCaps caps) =>
        s.Availability.Allows(build) && PassesHardware(s.Availability, caps);
}
