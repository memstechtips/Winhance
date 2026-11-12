using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

internal enum WinGetOperation
{
    Install,
    Upgrade,
    Uninstall,
    Download,
}

// One winget run's output: the banner that frames the command in the task terminal, the per-line
// translation that drives both the terminal and the progress bar, and the footer carrying the exit
// code. Install, uninstall and download share it, so a package downloaded onto install media reads
// in the terminal exactly as the same package installed from External Software does.
// An instance holds one run's throttle and phase state, so it is never reused across commands.
internal sealed class WinGetOutputStream(
    WinGetOperation operation,
    string displayName,
    ITaskProgressService? taskProgressService,
    ILocalizationService localization,
    ILogService? logService)
{
    private DateTime _lastProgressReport = DateTime.MinValue;
    private WinGetProgressParser.WinGetPhase? _lastLoggedPhase;

    // winget exits 0 when it could only stage a Store package that is in use, so this line is the only sign of it.
    internal const string RegistrationDeferredLine = "InstallFlowRegistrationDeferred";

    internal bool RegistrationDeferred { get; private set; }

    // These arrive as permanent output when ConPTY re-emits a \r\n-terminated progress bar, but are already handled by onProgressLine.
    internal static bool IsProgressBarLine(string line)
    {
        foreach (char c in line)
        {
            if (c >= '█' && c <= '▏') return true;
            if (c == '░') return true;
        }
        return false;
    }

    internal void WriteHeader(string exePath, string arguments)
    {
        logService?.LogInformation($"Running: {exePath} {arguments}");
        Terminal($"Command: {exePath} {arguments}");
        Terminal($"Start Time: \"{DateTime.Now:yyyy/MM/dd HH:mm:ss}\"");
        Terminal("---");
    }

    internal void WriteFooter(WinGetCliRunner.WinGetCliResult result)
    {
        Terminal("---");

        // A bare -1 (0xFFFFFFFF) is Winhance's own kill, not a winget verdict —
        // say so, or support transcripts read like a winget bug (see issue #675).
        var terminationNote = WinGetCliRunner.DescribeTermination(result, localization);
        if (terminationNote != null)
        {
            logService?.LogWarning(terminationNote);
            Terminal(terminationNote);
        }

        Terminal($"End Time: \"{DateTime.Now:yyyy/MM/dd HH:mm:ss}\"");
        Terminal($"Process return value: \"{result.ExitCode}\" (0x{result.ExitCode:X8})");
    }

    internal void HandleOutputLine(string line)
    {
        try
        {
            if (line.Trim() == RegistrationDeferredLine)
                RegistrationDeferred = true;

            // When ConPTY is active, winget's first progress bar render uses \r\n.
            // The \r fires onProgressLine (transient), then \n fires onOutputLine
            // (permanent) for the same content. Skip the permanent duplicate here
            // so the terminal doesn't show a frozen ghost progress bar.
            if (IsProgressBarLine(line))
                return;

            var displayLine = WinGetProgressParser.TranslateLine(line);
            var progress = WinGetProgressParser.ParseLine(line);

            if (progress == null)
            {
                if (displayLine != null)
                    Terminal(displayLine);
                return;
            }

            if (progress.Phase != _lastLoggedPhase && IsWorthLogging(progress.Phase))
            {
                _lastLoggedPhase = progress.Phase;
                if (displayLine != null)
                    logService?.LogInformation(displayLine);
            }

            // Complete is never throttled, or the bar can stop short of the end.
            var now = DateTime.UtcNow;
            if (progress.Phase != WinGetProgressParser.WinGetPhase.Complete
                && (now - _lastProgressReport).TotalMilliseconds < 250)
                return;

            _lastProgressReport = now;
            taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
            {
                Progress = PercentFor(progress),
                StatusText = StatusFor(progress.Phase),
                TerminalOutput = progress.Percent.HasValue && progress.Phase != WinGetProgressParser.WinGetPhase.Complete
                    ? null : (displayLine ?? line),
            });
        }
        catch (Exception ex)
        {
            logService?.LogWarning($"Progress reporting error (ignored): {ex.Message}");
        }
    }

    // winget's actual error text is useless to users, and to bug reports, if it only hits the log file.
    internal void HandleErrorLine(string line)
    {
        logService?.LogWarning($"winget stderr: {line}");
        Terminal(line);
    }

    // The next progress update replaces this status text when winget starts reporting again.
    internal void ReportStall()
    {
        var text = localization.GetString("Progress_WinGet_NoProgress");
        logService?.LogInformation(text);
        taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail { StatusText = text, TerminalOutput = text });
    }

    internal void ForwardProgressLine(string line) =>
        WinGetProgressParser.Forward(line, taskProgressService, logService);

    private bool IsWorthLogging(WinGetProgressParser.WinGetPhase phase) =>
        phase is WinGetProgressParser.WinGetPhase.Found
            or WinGetProgressParser.WinGetPhase.Complete
            or WinGetProgressParser.WinGetPhase.Error
        || phase == ActivePhase;

    private WinGetProgressParser.WinGetPhase ActivePhase => operation switch
    {
        WinGetOperation.Uninstall => WinGetProgressParser.WinGetPhase.Uninstalling,
        WinGetOperation.Download => WinGetProgressParser.WinGetPhase.Downloading,
        _ => WinGetProgressParser.WinGetPhase.Installing,
    };

    // Download spends the whole run in one phase, so it spreads over the range install splits between
    // fetching and installing.
    private int PercentFor(WinGetProgressParser.WinGetProgressInfo progress) => (operation, progress.Phase) switch
    {
        (_, WinGetProgressParser.WinGetPhase.Found) => 30,
        (_, WinGetProgressParser.WinGetPhase.Complete) => 100,
        (WinGetOperation.Install or WinGetOperation.Upgrade, WinGetProgressParser.WinGetPhase.Downloading) => 30 + (int)((progress.Percent ?? 0) * 0.4),
        (WinGetOperation.Install or WinGetOperation.Upgrade, WinGetProgressParser.WinGetPhase.Installing) => 70 + (int)((progress.Percent ?? 0) * 0.25),
        (WinGetOperation.Uninstall, WinGetProgressParser.WinGetPhase.Uninstalling) => 60,
        (WinGetOperation.Download, WinGetProgressParser.WinGetPhase.Downloading) => 30 + (int)((progress.Percent ?? 0) * 0.65),
        _ => 50,
    };

    private string StatusFor(WinGetProgressParser.WinGetPhase phase) => (operation, phase) switch
    {
        (_, WinGetProgressParser.WinGetPhase.Found) => localization.GetString("Progress_WinGet_FoundPackage", displayName),
        (WinGetOperation.Install or WinGetOperation.Upgrade, WinGetProgressParser.WinGetPhase.Downloading) => localization.GetString("Progress_Downloading", displayName),
        (WinGetOperation.Install, WinGetProgressParser.WinGetPhase.Installing) => localization.GetString("Progress_Installing", displayName),
        (WinGetOperation.Install, WinGetProgressParser.WinGetPhase.Complete) => localization.GetString("Progress_WinGet_InstalledSuccess", displayName),
        (WinGetOperation.Upgrade, WinGetProgressParser.WinGetPhase.Installing) => localization.GetString("Progress_Updating", displayName),
        (WinGetOperation.Upgrade, WinGetProgressParser.WinGetPhase.Complete) => localization.GetString("Progress_UpdatedSuccess", displayName),
        (WinGetOperation.Uninstall, WinGetProgressParser.WinGetPhase.Uninstalling) => localization.GetString("Progress_Uninstalling", displayName),
        (WinGetOperation.Uninstall, WinGetProgressParser.WinGetPhase.Complete) => localization.GetString("Progress_WinGet_UninstalledSuccess", displayName),
        (WinGetOperation.Download, WinGetProgressParser.WinGetPhase.Downloading) => localization.GetString("Progress_Downloading", displayName),
        (WinGetOperation.Download, WinGetProgressParser.WinGetPhase.Complete) => localization.GetString("Progress_WinGet_DownloadedSuccess", displayName),
        _ => localization.GetString("Progress_Processing", displayName),
    };

    private void Terminal(string text) =>
        taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail { TerminalOutput = text });
}
