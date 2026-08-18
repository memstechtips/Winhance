namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Detects Over-the-Shoulder (OTS) UAC elevation and provides
/// the interactive console user's identity and folder paths.
/// </summary>
public interface IInteractiveUserService
{
    /// <summary>
    /// Whether the app is running as a different user than the interactive console user (OTS elevation).
    /// </summary>
    bool IsOtsElevation { get; }

    /// <summary>
    /// The interactive (console) user's SID string, or null if not OTS.
    /// </summary>
    string? InteractiveUserSid { get; }

    /// <summary>
    /// The interactive user's username (e.g. "Standard"), or Environment.UserName if not OTS.
    /// </summary>
    string InteractiveUserName { get; }

    /// <summary>
    /// Returns the interactive user's equivalent of a SpecialFolder path.
    /// Falls back to Environment.GetFolderPath() if not OTS.
    /// Supports: LocalApplicationData, Programs, UserProfile.
    /// </summary>
    string GetInteractiveUserFolderPath(Environment.SpecialFolder folder);

    /// <summary>
    /// Whether an interactive user token is available for process creation.
    /// Only true when OTS is detected and the token was successfully obtained from explorer.exe.
    /// </summary>
    bool HasInteractiveUserToken { get; }

    /// <summary>
    /// Runs a process as the interactive user (using the stored explorer.exe token).
    /// Falls back to normal process execution if no token is available.
    /// </summary>
    Task<InteractiveProcessResult> RunProcessAsInteractiveUserAsync(
        string fileName,
        string arguments,
        Action<string>? onOutputLine = null,
        Action<string>? onErrorLine = null,
        CancellationToken cancellationToken = default,
        int timeoutMs = 300_000,
        Action<string>? onProgressLine = null);

    /// <summary>
    /// Launches a GUI process as the interactive user without waiting for it to exit.
    /// Falls back to normal Process.Start if not OTS or no token is available.
    /// </summary>
    void LaunchProcessAsInteractiveUser(string fileName, string arguments = "");

    /// <summary>
    /// Duplicates a primary token from the RUNNING shell so the shell can be relaunched at the user's
    /// integrity level later. Returns null when no token could be captured.
    ///
    /// Two things make this different from <see cref="LaunchProcessAsInteractiveUser"/>, and both are
    /// the point:
    ///
    /// (1) It is NOT gated on OTS elevation. The common case is an admin running Winhance elevated as
    ///     themselves, where <see cref="IsOtsElevation"/> is false and that method degrades to
    ///     Process.Start - which cannot start the shell, because Winhance is always elevated and
    ///     Windows will not run the shell at high integrity.
    ///
    /// (2) It MUST be called BEFORE the shell is terminated. The token is harvested from the live
    ///     explorer.exe process; once the shell is gone there is nothing left to harvest from, which
    ///     is exactly when you need it.
    /// </summary>
    IShellRelaunchToken? CaptureShellRelaunchToken();
}

/// <summary>
/// A primary token duplicated from the shell process, used to relaunch the shell at the interactive
/// user's integrity level after Winhance has terminated it. Dispose closes the underlying handle.
/// </summary>
public interface IShellRelaunchToken : IDisposable
{
    /// <summary>Launches <paramref name="fileName"/> with the captured token. False if creation failed.</summary>
    bool TryLaunch(string fileName, string arguments = "");
}

/// <summary>
/// Result of running a process as the interactive user.
/// </summary>
public record InteractiveProcessResult(int ExitCode, string StandardOutput, string StandardError);
