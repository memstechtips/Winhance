namespace Winhance.Core.Features.Common.Catalog;

public sealed record WindowsRelease(string Name, int Build);

public static class WindowsReleases
{
    // Newest first: the first row is what Builder authors for until the user picks another.
    public static readonly IReadOnlyList<WindowsRelease> All =
    [
        new("Windows 11 26H2", 26300),
        new("Windows 11 25H2", 26200),
        new("Windows 11 24H2", 26100),
        new("Windows 11 23H2", 22631),
        new("Windows 10 22H2", 19045),
    ];

    public static WindowsRelease Latest => All[0];

    public static WindowsRelease? ForBuild(int build) => All.FirstOrDefault(r => r.Build == build);

    // The highest revision, because Setup installs the media's revision and Windows Update then brings it current,
    // which is when a revision-gated setting (start-all-apps-view needs 26100.7171) starts to work.
    public static WinBuild TargetOf(int build) => new(build, int.MaxValue);
}
