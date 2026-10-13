namespace Winhance.Core.Features.Common.Helpers;

// The portable installer drops portable.marker beside Winhance.exe. A portable copy then keeps everything it writes
// for itself in a Data folder there, so the folder can move between PCs. Files Windows or a scheduled task must find
// later (removal scripts, OpenWebSearch, answer-file staging, apps Winhance installs) stay on the PC either way.
public static class AppDataPaths
{
    public const string PortableMarkerFileName = "portable.marker";

    private static readonly string? PortableRoot = PortableRootFor(AppContext.BaseDirectory, File.Exists);

    public static string MachineRoot =>
        Root(PortableRoot, Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));

    // The caller picks whose LocalAppData: the interactive user's under OTS elevation, or the process user's.
    public static string UserRoot(string localAppData) => Root(PortableRoot, localAppData);

    internal static string? PortableRootFor(string appDirectory, Func<string, bool> fileExists) =>
        fileExists(Path.Combine(appDirectory, PortableMarkerFileName)) ? Path.Combine(appDirectory, "Data") : null;

    internal static string Root(string? portableRoot, string installedBase) =>
        portableRoot ?? Path.Combine(installedBase, "Winhance");
}
