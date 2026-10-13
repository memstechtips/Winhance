using System.CommandLine;
using Winhance.UI.Features.Common.Services;

namespace Winhance.UI.Helpers;

public sealed record LaunchOptions(IReadOnlyList<string> InstallApps, string? InstallFolder, string? ConfigFile)
{
    public static LaunchOptions None { get; } = new([], null, null);

    public bool StartInTray { get; init; }

    public bool InstallsApps => InstallApps.Count > 0 || InstallFolder is not null;
}

// Each flag Winhance accepts is one option here; add one only when something starts Winhance with it.
internal static class LaunchCommandLine
{
    private static readonly Option<string?> InstallApps = new("--install-apps")
    {
        Description = "External Apps ids to install, comma-separated.",
    };

    private static readonly Option<string?> InstallFolder = new("--install-folder")
    {
        Description = "A folder of staged installers to run after the apps.",
    };

    private static readonly Option<string?> ConfigFile = new("--config")
    {
        Description = "A saved .winhance config to apply.",
    };

    private static readonly Option<bool> Tray = new(TrayController.StartInTrayFlag)
    {
        Description = "Start hidden in the system tray. The sign-in task passes it.",
    };

    private static readonly RootCommand Root = new("Winhance") { Options = { InstallApps, InstallFolder, ConfigFile, Tray } };

    internal static LaunchOptions Parse(string[] args, Action<string> logError)
    {
        var result = Root.Parse(args);
        if (result.Errors.Count > 0)
        {
            foreach (var error in result.Errors)
            {
                logError(error.Message);
            }

            return LaunchOptions.None;
        }

        var ids = result.GetValue(InstallApps)
            ?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
        return new LaunchOptions(ids, result.GetValue(InstallFolder), result.GetValue(ConfigFile)) { StartInTray = result.GetValue(Tray) };
    }
}
