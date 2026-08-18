using System.Runtime.CompilerServices;

namespace Winhance.TestSupport;

// [CallerFilePath] rather than AppContext.BaseDirectory because WINHANCE_LOCAL_BUILD_ROOT redirects the build
// output to %LOCALAPPDATA% when the repo is on a network share, and the bin folder then resolves nothing.
public static class RepoPaths
{
    public static string SolutionDir([CallerFilePath] string callerPath = "")
    {
        var dir = Path.GetDirectoryName(callerPath);
        while (dir is not null && !File.Exists(Path.Combine(dir, "Winhance.sln")))
            dir = Path.GetDirectoryName(dir);

        return dir ?? throw new InvalidOperationException(
            "Could not find Winhance.sln walking up from " + callerPath);
    }

    public static string LocalizationDir([CallerFilePath] string callerPath = "") =>
        Path.Combine(SolutionDir(callerPath), "src", "Winhance.UI", "Features", "Common", "Localization");
}
