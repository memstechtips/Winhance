using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

/// <summary>Machine-independent RULE conformance for AvailabilityCompatibility.DeriveCompatibilityMessage on
/// CONSTRUCTED Availability instances -- no defs, no old filter. Pins every derivation-rule branch so the helper
/// stays honest after the Plan-4 teardown deletes the old-model equivalence oracle. This class SURVIVES
/// teardown.</summary>
public class CompatibilityMessageConformanceTests
{
    private static readonly WinBuild Unbounded = new(int.MaxValue, int.MaxValue);

    private static Availability Avail(params BuildRange[] ranges) => new() { Builds = ranges };

    private static string? Derive(Availability a, WinBuild b) =>
        AvailabilityCompatibility.DeriveCompatibilityMessage(a, b);

    [Fact]
    public void Win10_machine_with_win11_required_ranges_yields_Windows11Only()
    {
        Assert.Equal("Compatibility_Windows11Only",
            Derive(Avail(BuildRange.Windows11), new WinBuild(19045)));

        // A Min ABOVE the boundary still reads as Windows-11-only on a Windows 10 machine: the old branch
        // order fired IsWindows11Only before the build-bound branches, and every shipped build-bounded def is
        // also IsWindows11Only.
        Assert.Equal("Compatibility_Windows11Only",
            Derive(Avail(new BuildRange(new WinBuild(26100), Unbounded)), new WinBuild(19045)));
    }

    [Fact]
    public void Win11_machine_with_win10_only_range_yields_Windows10Only()
    {
        Assert.Equal("Compatibility_Windows10Only",
            Derive(Avail(BuildRange.Windows10), new WinBuild(22621)));
    }

    [Fact]
    public void Lower_bound_violation_yields_MinBuild()
    {
        Assert.Equal("Compatibility_MinBuild|26100",
            Derive(Avail(new BuildRange(new WinBuild(26100), Unbounded)), new WinBuild(22621)));
    }

    [Fact]
    public void Equal_build_lower_revision_yields_MinBuild_with_revision()
    {
        Assert.Equal("Compatibility_MinBuild|26100.4484",
            Derive(Avail(new BuildRange(new WinBuild(26100, 4484), Unbounded)), new WinBuild(26100, 3000)));
    }

    [Fact]
    public void Upper_bound_violation_with_os_boundary_min_yields_MaxBuild()
    {
        // A Min of exactly (22000, 0) is the OS-boundary clamp, NOT an interior window bound, so an upper
        // violation reads as MaxBuild (the start-menu-layout shape), not BuildRange.
        Assert.Equal("Compatibility_MaxBuild|26120",
            Derive(Avail(new BuildRange(new WinBuild(22000), new WinBuild(26120, int.MaxValue))), new WinBuild(26200)));
    }

    [Fact]
    public void Equal_build_higher_revision_yields_MaxBuild_with_revision()
    {
        Assert.Equal("Compatibility_MaxBuild|26100.500",
            Derive(Avail(new BuildRange(new WinBuild(22000), new WinBuild(26100, 500))), new WinBuild(26100, 600)));
    }

    [Fact]
    public void Single_interior_window_yields_BuildRange()
    {
        var window = Avail(new BuildRange(new WinBuild(22621), new WinBuild(26099, int.MaxValue)));

        // Above the window on a Windows 11 machine (the taskbar-copilot shape).
        Assert.Equal("Compatibility_BuildRange|22621-26099", Derive(window, new WinBuild(26200)));

        // Below the window but still a Windows 11 machine: the old model had no min/max bounds for a
        // SupportedBuildRanges def, so the range text is the message on either side of the window.
        Assert.Equal("Compatibility_BuildRange|22621-26099", Derive(window, new WinBuild(22000)));
    }

    [Fact]
    public void Multiple_ranges_yield_joined_BuildRange()
    {
        var multi = Avail(BuildRange.Between(19041, 19045), BuildRange.Between(22621, 26099));
        Assert.Equal("Compatibility_BuildRange|19041-19045 or 22621-26099", Derive(multi, new WinBuild(20000)));
    }

    [Fact]
    public void Allowed_build_yields_null()
    {
        Assert.Null(Derive(Avail(BuildRange.Windows11), new WinBuild(26100)));
    }

    [Fact]
    public void Empty_builds_yields_null()
    {
        Assert.Null(Derive(Availability.Everywhere, new WinBuild(19045)));
    }
}
