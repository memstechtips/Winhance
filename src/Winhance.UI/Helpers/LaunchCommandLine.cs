using System.CommandLine;
using System.CommandLine.Parsing;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.Common.Services;

namespace Winhance.UI.Helpers;

public enum AppUpdateCheckLaunch
{
    Scheduled,
    Manual,
    Install,
}

public sealed record LaunchOptions(IReadOnlyList<string> InstallApps, string? InstallFolder, string? ConfigFile)
{
    public static LaunchOptions None { get; } = new([], null, null);

    public bool StartInTray { get; init; }

    public bool DiagMemory { get; init; }

    // Opens the window and closes it the way its X does, so the daily support dialog shows. The tray host passes it
    // when its Exit item is chosen and that dialog is due.
    public bool ExitFromTray { get; init; }

    // Re-remove the kept apps Windows put back. On its own it is a job with no window and no instance key; with --tray or
    // --reapply-profile it runs inside that launch.
    public bool Maintain { get; init; }

    public bool MaintainsAlone => Maintain && !StartInTray && !RunsInBackground;

    public string? Profile { get; init; }

    public bool ReapplyProfile { get; init; }

    public bool SwitchesProfile => Profile is not null || ReapplyProfile;

    public AppUpdateCheckLaunch? CheckAppUpdates { get; init; }

    // A job done hidden, after which the process leaves.
    public bool RunsInBackground => SwitchesProfile || CheckAppUpdates is not null;

    public bool StartsHidden => StartInTray || RunsInBackground;

    // --tray on its own starts the light tray host instead of the app.
    public bool RunsTrayHost => StartInTray && !RunsInBackground && !InstallsApps && ConfigFile is null;

    public InstallOptionsStore? InstallOptions { get; init; }

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

    private static readonly Option<string?> InstallOptionsArgument = new("--install-options")
    {
        Description = "How to install those apps, as the answer file wrote it.",
    };

    private static readonly Option<string?> ConfigFile = new("--config")
    {
        Description = "A saved .winhance config to apply.",
    };

    private static readonly Option<bool> Tray = new(SignInTask.TrayFlag)
    {
        Description = "Wait in the system tray. The sign-in task passes it. With a background job, wait there once it is done.",
    };

    private static readonly Option<bool> DiagMemory = new("--diag-memory")
    {
        Description = "Force a full GC before each memory log line so the live-instance counts are exact. For leak hunting.",
    };

    private static readonly Option<bool> ExitFromTray = new(TrayController.ExitFlag)
    {
        Description = "Open the window and close it as its X does, showing the support dialog when it is due. The tray host passes it.",
    };

    private static readonly Option<bool> Maintain = new(SignInTask.MaintainFlag)
    {
        Description = "Remove the kept apps that Windows put back. On its own: no window, then exit. With --tray or --reapply-profile it runs first. The sign-in task passes it.",
    };

    private static readonly Option<string?> SwitchProfile = new(TrayController.ProfileFlag)
    {
        Description = "Switch to this saved profile in the background, then exit unless --tray is also given.",
    };

    private static readonly Option<bool> Reapply = new(SignInTask.ReapplyFlag)
    {
        Description = "Re-apply the active profile in the background, then exit. The re-apply sign-in task passes it.",
    };

    private static readonly Option<AppUpdateCheckLaunch?> CheckAppUpdates = new(TrayController.CheckAppUpdatesFlag)
    {
        Description = "Check for app updates in the background, then exit unless --tray is also given. The tray passes it.",
    };

    private static readonly RootCommand Root = new("Winhance") { Options = { InstallApps, InstallFolder, InstallOptionsArgument, ConfigFile, Tray, DiagMemory, ExitFromTray, Maintain, SwitchProfile, Reapply, CheckAppUpdates } };

    // A second launch hands its command line to the running instance as one string, starting with the exe.
    internal static LaunchOptions ParseRedirected(string commandLine, Action<string> logError)
    {
        var args = CommandLineParser.SplitCommandLine(commandLine).ToList();
        if (args.Count > 0 && args[0].EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            args.RemoveAt(0);
        return Parse([.. args], logError);
    }

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
        return new LaunchOptions(ids, result.GetValue(InstallFolder), result.GetValue(ConfigFile))
        {
            StartInTray = result.GetValue(Tray),
            DiagMemory = result.GetValue(DiagMemory),
            ExitFromTray = result.GetValue(ExitFromTray),
            Maintain = result.GetValue(Maintain),
            Profile = result.GetValue(SwitchProfile),
            ReapplyProfile = result.GetValue(Reapply),
            CheckAppUpdates = result.GetValue(CheckAppUpdates),
            InstallOptions = InstallOptionsStore.FromArgument(result.GetValue(InstallOptionsArgument)),
        };
    }
}
