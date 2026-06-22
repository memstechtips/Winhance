using System;
using System.Collections.Generic;
using System.Linq;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>A Windows build point: build number plus revision, compared build-first then revision.</summary>
public readonly record struct WinBuild(int Build, int Revision = 0) : IComparable<WinBuild>
{
    public int CompareTo(WinBuild o) => Build != o.Build ? Build.CompareTo(o.Build) : Revision.CompareTo(o.Revision);
    public static bool operator <(WinBuild a, WinBuild b) => a.CompareTo(b) < 0;
    public static bool operator >(WinBuild a, WinBuild b) => a.CompareTo(b) > 0;
    public static bool operator <=(WinBuild a, WinBuild b) => a.CompareTo(b) <= 0;
    public static bool operator >=(WinBuild a, WinBuild b) => a.CompareTo(b) >= 0;
}

/// <summary>An inclusive build range. Replaces the old IsWindows10Only / IsWindows11Only +
/// Min/MaxBuildNumber + Min/MaxBuildRevision + SupportedBuildRanges. Windows 11 starts at build 22000.</summary>
public sealed record BuildRange(WinBuild Min, WinBuild Max)
{
    public bool Contains(WinBuild b) => b >= Min && b <= Max;
    public static readonly BuildRange Windows10 = new(new(0), new(21999, int.MaxValue));
    public static readonly BuildRange Windows11 = new(new(22000), new(int.MaxValue, int.MaxValue));
    public static BuildRange From(int build, int rev = 0) => new(new(build, rev), new(int.MaxValue, int.MaxValue));
    public static BuildRange Upto(int build, int rev = int.MaxValue) => new(new(0), new(build, rev));
    public static BuildRange Between(int min, int max) => new(new(min), new(max, int.MaxValue));
}

/// <summary>When a setting is shown on this machine. Empty Builds = every build. The power work adds
/// hardware-capability and advanced-unlock gates to this record (no new top-level field).</summary>
public sealed record Availability
{
    public IReadOnlyList<BuildRange> Builds { get; init; } = Array.Empty<BuildRange>();
    public static readonly Availability Everywhere = new();
    public bool Allows(WinBuild build) => Builds.Count == 0 || Builds.Any(r => r.Contains(build));
}
