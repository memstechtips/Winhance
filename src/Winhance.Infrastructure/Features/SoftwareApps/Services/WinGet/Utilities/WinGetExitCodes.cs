using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

// Values and names from winget-cli doc/windows/package-manager/winget/returnCodes.md.
internal static class WinGetExitCodes
{
    public const int Ok = 0;

    public const int CancelSignalReceived = unchecked((int)0x8A150005);
    // SHELLEXEC_INSTALL_FAILED: the package's own installer ran and returned an error.
    public const int InstallerFailed = unchecked((int)0x8A150006);
    public const int DownloadFailed = unchecked((int)0x8A150008);
    public const int NoApplicableInstallers = unchecked((int)0x8A150010);
    public const int InstallerHashMismatch = unchecked((int)0x8A150011);
    public const int PackageNotFound = unchecked((int)0x8A150014);
    // The id matched more than one installed package, such as a per-machine and a per-user install of one app.
    public const int MultiplePackagesFound = unchecked((int)0x8A150016);
    public const int StoreBlockedByPolicy = unchecked((int)0x8A15001B);
    public const int StoreAppBlockedByPolicy = unchecked((int)0x8A15001C);
    public const int UpdateNotApplicable = unchecked((int)0x8A15002B);
    public const int NoUninstallInfoFound = unchecked((int)0x8A15002F);
    public const int ExecUninstallCommandFailed = unchecked((int)0x8A150030);
    public const int BlockedByPolicy = unchecked((int)0x8A15003A);
    public const int PackageAgreementsNotAccepted = unchecked((int)0x8A150041);
    public const int SourceAgreementsNotAccepted = unchecked((int)0x8A150046);
    public const int FailedToOpenAllSources = unchecked((int)0x8A15004B);
    public const int PackageAlreadyInstalled = unchecked((int)0x8A150061);
    public const int ZeroByteInstaller = unchecked((int)0x8A150086);
    public const int NoNetwork = unchecked((int)0x8A150107);
    public const int RebootRequiredToFinish = unchecked((int)0x8A150109);
    public const int RebootInitiated = unchecked((int)0x8A15010B);
    public const int CancelledByUser = unchecked((int)0x8A15010C);
    public const int InstallBlockedByPolicy = unchecked((int)0x8A15010F);

    // winget will not remove a user-scope package from an elevated session; the same command run unelevated works.
    public const int NoAdminUninstallForUserScope = unchecked((int)0x8A15007D);

    public static bool IsSuccess(int exitCode)
        => exitCode is Ok or RebootRequiredToFinish or RebootInitiated or PackageAlreadyInstalled or UpdateNotApplicable;

    // WinGet wraps any non-zero uninstaller exit into EXEC_UNINSTALL_COMMAND_FAILED even when the uninstall
    // succeeded (Chromium-based apps always return 19), so these are verified by checking whether the package is still installed.
    public static bool IsUninstallVerifiable(int exitCode)
        => exitCode == ExecUninstallCommandFailed || exitCode == NoUninstallInfoFound;

    public static InstallFailureReason MapExitCode(int exitCode) => exitCode switch
    {
        _ when IsSuccess(exitCode) => InstallFailureReason.None,
        PackageNotFound => InstallFailureReason.PackageNotFound,
        InstallerFailed or InstallerHashMismatch => InstallFailureReason.HashMismatchOrInstallError,
        DownloadFailed or ZeroByteInstaller => InstallFailureReason.DownloadError,
        NoNetwork => InstallFailureReason.NetworkError,
        BlockedByPolicy or InstallBlockedByPolicy or StoreBlockedByPolicy or StoreAppBlockedByPolicy => InstallFailureReason.BlockedByPolicy,
        NoApplicableInstallers => InstallFailureReason.NoApplicableInstallers,
        PackageAgreementsNotAccepted or SourceAgreementsNotAccepted => InstallFailureReason.AgreementsNotAccepted,
        CancelSignalReceived or CancelledByUser => InstallFailureReason.UserCancelled,
        _ => InstallFailureReason.Other
    };
}
