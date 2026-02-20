using System;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces
{
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
        /// The interactive user's profile folder path (e.g. C:\Users\Standard), or current user's if not OTS.
        /// </summary>
        string InteractiveUserProfilePath { get; }

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
            int timeoutMs = 300_000);
    }

    /// <summary>
    /// Result of running a process as the interactive user.
    /// </summary>
    public record InteractiveProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
