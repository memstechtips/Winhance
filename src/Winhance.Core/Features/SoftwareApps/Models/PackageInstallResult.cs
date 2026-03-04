namespace Winhance.Core.Features.SoftwareApps.Models;

public enum InstallFailureReason
{
    None,
    PackageNotFound,
    HashMismatchOrInstallError,
    DownloadError,
    NetworkError,
    BlockedByPolicy,
    NoApplicableInstallers,
    AgreementsNotAccepted,
    WinGetNotAvailable,
    UserCancelled,
    Other
}

public record PackageInstallResult(
    bool Success,
    InstallFailureReason FailureReason = InstallFailureReason.None,
    string? ErrorMessage = null)
{
    public static PackageInstallResult Succeeded() => new(true);

    public static PackageInstallResult Failed(InstallFailureReason reason, string? message = null)
        => new(false, reason, message);

    public bool IsChocolateyFallbackCandidate => !Success && FailureReason is not
        (InstallFailureReason.None or
         InstallFailureReason.UserCancelled or
         InstallFailureReason.BlockedByPolicy);
}
