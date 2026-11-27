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
    // winget installed a Store package the app had open: Windows holds the new version until the app next starts.
    public bool RegistrationDeferred { get; init; }

    public static PackageInstallResult Succeeded() => new(true);

    public static PackageInstallResult Failed(InstallFailureReason reason, string? message = null)
        => new(false, reason, message);
}
