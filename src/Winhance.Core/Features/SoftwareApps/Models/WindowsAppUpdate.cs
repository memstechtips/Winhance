using Winhance.Core.Features.SoftwareApps.Utilities;

namespace Winhance.Core.Features.SoftwareApps.Models;

// An update claim comes from winget for a built-in app that is also in its own winget source, or from the Store catalog
// for a Store app (the msstore source reports every package as Version Unknown). The Teams bootstrapper and a
// placeholder install read 1.0.0.1 or 0.0.0.0 until the Store fills them in, which would be called outdated.
public static class WindowsAppUpdate
{
    private static readonly string[] PlaceholderVersions = ["1.0.0.1", "0.0.0.0"];

    public static bool Applies(InstalledApp? app) =>
        app is { UpdateVia: PackageSources.WinGet or PackageSources.MsStore, AvailableVersion: { } available, Version: { } installed }
        && !PlaceholderVersions.Contains(installed.Trim())
        && PackageVersion.Compare(available, installed) > 0;

    // Edge has two entries under one catalog row: the legacy AppX stub and the Win32 build that carries the update.
    public static InstalledApp? Pick(IEnumerable<InstalledApp> candidates)
    {
        var all = candidates.ToList();
        return all.FirstOrDefault(Applies) ?? all.FirstOrDefault();
    }
}
