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

    // A portable copy's first run takes the installed copy's settings, backups and change history, once. The
    // originals stay, because an installed Winhance on this PC still reads them. Logs and caches rebuild themselves.
    public static IReadOnlyList<string> CopyInstalledData(string localAppData) =>
        PortableRoot is null
            ? []
            : CopyInstalledData(
                PortableRoot,
                Root(null, localAppData),
                Root(null, Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)));

    internal static IReadOnlyList<string> CopyInstalledData(string portableRoot, string installedUserRoot, string installedMachineRoot)
    {
        var copied = new List<string>();
        foreach (var folder in new[] { "Config", "Backup" })
        {
            var source = Path.Combine(installedUserRoot, folder);
            var target = Path.Combine(portableRoot, folder);
            if (!Directory.Exists(source) || Directory.Exists(target))
                continue;

            // Into a temp folder first, so a copy cut short is retried next run instead of counting as done.
            var staging = target + ".copying";
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var destination = Path.Combine(staging, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }
            Directory.CreateDirectory(staging);
            Directory.Move(staging, target);
            copied.Add(target);
        }

        var history = Path.Combine(installedMachineRoot, "ChangeHistory.txt");
        var historyTarget = Path.Combine(portableRoot, "ChangeHistory.txt");
        if (File.Exists(history) && !File.Exists(historyTarget))
        {
            Directory.CreateDirectory(portableRoot);
            File.Copy(history, historyTarget);
            copied.Add(historyTarget);
        }

        return copied;
    }
}
