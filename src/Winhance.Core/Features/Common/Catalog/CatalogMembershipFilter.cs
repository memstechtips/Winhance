using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>The machine capabilities a catalog membership filter needs - the catalog-model equivalent of the
/// probes the old HardwareCompatibilityFilter read from IHardwareDetectionService (battery / hybrid-sleep).</summary>
public readonly record struct HardwareCaps(bool HasBattery, bool SupportsHybridSleep);

/// <summary>Pure catalog-membership gating: reproduces the old WindowsCompatibilityFilter (OS build) +
/// HardwareCompatibilityFilter (hardware caps) decisions from <see cref="Setting.Availability"/>, so the settings
/// registry can be sourced from the catalog instead of the old SettingDefinition providers. Does NOT include the
/// powercfg-existence filter (machine-dependent + mutating - deferred to its own slice); callers that need existence
/// still ran the old PowerSettingsValidationService.</summary>
public static class CatalogMembershipFilter
{
    /// <summary>Checks the setting's Availability.Hardware requirements against the machine caps. Only the two
    /// requirements any catalog setting actually uses are gated - Battery and HybridSleepCapable; the carrier-less
    /// Lid / Desktop / BrightnessSupport gates were removed as unused (re-add on demand when a setting needs one).</summary>
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

    /// <summary>True when the setting is shown on a machine at the given build with the given hardware caps - the OS
    /// gate (Availability.Allows) AND the hardware gate (PassesHardware). Existence gating is NOT applied here.</summary>
    public static bool IsAvailable(Setting s, WinBuild build, HardwareCaps caps) =>
        s.Availability.Allows(build) && PassesHardware(s.Availability, caps);

    /// <summary>Membership when the OS-build gate is RELAXED - the "show settings for other Windows
    /// versions" scope. The hardware gate still applies (and existence, applied separately by the registry);
    /// only the build range is ignored. The catalog-model equivalent of the old registry's OS-version-bypassed
    /// set (FilterSettingsByWindowsVersion applyFilter:false decorates but does not remove).</summary>
    public static bool IsAvailableIgnoringOsBuild(Setting s, HardwareCaps caps) =>
        PassesHardware(s.Availability, caps);
}
