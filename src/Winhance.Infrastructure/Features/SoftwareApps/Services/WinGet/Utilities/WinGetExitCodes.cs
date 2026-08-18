using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

// Reference: winget-cli doc/windows/package-manager/winget/returnCodes.md
public static class WinGetExitCodes
{
    public const int Ok = 0;
    public const int RestartRequired = unchecked((int)0x8A150019);

    public const int PackageNotFound = unchecked((int)0x8A150014);
    public const int InstallerHashMismatch = unchecked((int)0x8A150005);
    public const int DownloadError = unchecked((int)0x8A150007);
    public const int BlockedByPolicy = unchecked((int)0x8A150016);
    public const int NoApplicableInstallers = unchecked((int)0x8A15001B);
    public const int PackageAgreementsNotAccepted = unchecked((int)0x8A15000E);
    public const int NetworkError = unchecked((int)0x8A150006);
    public const int AlreadyInstalled = unchecked((int)0x8A150015);
    public const int UpdateNotApplicable = unchecked((int)0x8A15002A);
    public const int PackageNotInstalled = unchecked((int)0x8A150013);
    public const int FailedToOpenAllSources = unchecked((int)0x8A15004B);
    public const int ManifestError = unchecked((int)0x8A15000B);
    public const int OperationCancelled = unchecked((int)0x8A15002B);

    public const int ExecUninstallCommandFailed = unchecked((int)0x8A150030);
    public const int NoUninstallInfoFound = unchecked((int)0x8A15002F);

    public static bool IsSuccess(int exitCode)
        => exitCode == Ok || exitCode == RestartRequired
        || exitCode == AlreadyInstalled || exitCode == UpdateNotApplicable;

    // WinGet wraps any non-zero uninstaller exit into EXEC_UNINSTALL_COMMAND_FAILED even when the uninstall
    // succeeded (Chromium-based apps always return 19), so these are verified by checking whether the package is still installed.
    public static bool IsUninstallVerifiable(int exitCode)
        => exitCode == ExecUninstallCommandFailed || exitCode == NoUninstallInfoFound;

    public static InstallFailureReason MapExitCode(int exitCode) => exitCode switch
    {
        Ok => InstallFailureReason.None,
        RestartRequired => InstallFailureReason.None,
        AlreadyInstalled => InstallFailureReason.None,
        PackageNotFound => InstallFailureReason.PackageNotFound,
        PackageNotInstalled => InstallFailureReason.PackageNotFound,
        InstallerHashMismatch => InstallFailureReason.HashMismatchOrInstallError,
        DownloadError => InstallFailureReason.DownloadError,
        NetworkError => InstallFailureReason.NetworkError,
        BlockedByPolicy => InstallFailureReason.BlockedByPolicy,
        NoApplicableInstallers => InstallFailureReason.NoApplicableInstallers,
        PackageAgreementsNotAccepted => InstallFailureReason.AgreementsNotAccepted,
        ManifestError => InstallFailureReason.Other,
        OperationCancelled => InstallFailureReason.UserCancelled,
        _ => InstallFailureReason.Other
    };
}
