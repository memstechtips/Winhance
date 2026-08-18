namespace Winhance.Core.Features.Common.Catalog;

public readonly record struct WinBuild(int Build, int Revision = 0) : IComparable<WinBuild>
{
    public int CompareTo(WinBuild o) => Build != o.Build ? Build.CompareTo(o.Build) : Revision.CompareTo(o.Revision);
    public static bool operator <(WinBuild a, WinBuild b) => a.CompareTo(b) < 0;
    public static bool operator >(WinBuild a, WinBuild b) => a.CompareTo(b) > 0;
    public static bool operator <=(WinBuild a, WinBuild b) => a.CompareTo(b) <= 0;
    public static bool operator >=(WinBuild a, WinBuild b) => a.CompareTo(b) >= 0;
}

// Windows 11 starts at build 22000.
public sealed record BuildRange(WinBuild Min, WinBuild Max)
{
    public bool Contains(WinBuild b) => b >= Min && b <= Max;
    public static readonly BuildRange Windows10 = new(new(0), new(21999, int.MaxValue));
    public static readonly BuildRange Windows11 = new(new(22000), new(int.MaxValue, int.MaxValue));
    public static BuildRange Between(int min, int max) => new(new(min), new(max, int.MaxValue));
}

public sealed record Availability
{
    public IReadOnlyList<BuildRange> Builds { get; init; } = Array.Empty<BuildRange>();
    public static readonly Availability Everywhere = new();
    public bool Allows(WinBuild build) => Builds.Count == 0 || Builds.Any(r => r.Contains(build));

    public IReadOnlyList<HardwareRequirement> Hardware { get; init; } = Array.Empty<HardwareRequirement>();

    // Presentation gate only; does not affect detection.
    public bool RequiresAdvancedUnlock { get; init; }

    // Only meaningful for powercfg settings.
    public bool ValidatesExistence { get; init; }
}

public enum HardwareRequirement { Battery, HybridSleepCapable }
