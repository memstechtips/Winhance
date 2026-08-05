using System.Runtime.CompilerServices;

namespace Winhance.TestSupport;

/// <summary>
/// Locates the repo on disk from a test's own compile-time path.
///
/// [CallerFilePath] rather than AppContext.BaseDirectory because the build output does not always
/// sit inside the repo - WINHANCE_LOCAL_BUILD_ROOT redirects it to %LOCALAPPDATA% when the repo is
/// on a network share, and the bin folder then resolves nothing. The attribute is filled in by the
/// compiler at the CALL SITE, so this keeps working from any test project.
/// </summary>
public static class RepoPaths
{
    /// <summary>The directory holding Winhance.sln.</summary>
    public static string SolutionDir([CallerFilePath] string callerPath = "")
    {
        var dir = Path.GetDirectoryName(callerPath);
        while (dir is not null && !File.Exists(Path.Combine(dir, "Winhance.sln")))
            dir = Path.GetDirectoryName(dir);

        return dir ?? throw new InvalidOperationException(
            "Could not find Winhance.sln walking up from " + callerPath);
    }

    /// <summary>The shipped localization JSON files - en.json plus one per translation.</summary>
    public static string LocalizationDir([CallerFilePath] string callerPath = "") =>
        Path.Combine(SolutionDir(callerPath), "src", "Winhance.UI", "Features", "Common", "Localization");
}
