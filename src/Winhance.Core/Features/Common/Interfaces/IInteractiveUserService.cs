namespace Winhance.Core.Features.Common.Interfaces;

// OTS = Over-the-Shoulder UAC elevation: the app runs as a different user than the interactive console user.
public interface IInteractiveUserService
{
    bool IsOtsElevation { get; }

    string? InteractiveUserSid { get; }

    string InteractiveUserName { get; }

    // Falls back to Environment.GetFolderPath when not OTS.
    string GetInteractiveUserFolderPath(Environment.SpecialFolder folder);

    // Only true when OTS is detected and the token was obtained from explorer.exe.
    bool HasInteractiveUserToken { get; }

    // Falls back to normal process execution when no token is available.
    Task<InteractiveProcessResult> RunProcessAsInteractiveUserAsync(
        string fileName,
        string arguments,
        Action<string>? onOutputLine = null,
        Action<string>? onErrorLine = null,
        CancellationToken cancellationToken = default,
        int timeoutMs = 300_000,
        Action<string>? onProgressLine = null);

    // Falls back to Process.Start when not OTS or no token is available.
    void LaunchProcessAsInteractiveUser(string fileName, string arguments = "");

    // NOT gated on OTS: the common case is an admin running Winhance elevated as themselves, where
    // LaunchProcessAsInteractiveUser degrades to Process.Start - which cannot start the shell, because Winhance is
    // always elevated and Windows will not run the shell at high integrity. MUST be called BEFORE the shell is
    // terminated: the token is harvested from the live explorer.exe.
    IShellRelaunchToken? CaptureShellRelaunchToken();
}

// Dispose closes the underlying handle.
public interface IShellRelaunchToken : IDisposable
{
    bool TryLaunch(string fileName, string arguments = "");
}

public record InteractiveProcessResult(int ExitCode, string StandardOutput, string StandardError);
