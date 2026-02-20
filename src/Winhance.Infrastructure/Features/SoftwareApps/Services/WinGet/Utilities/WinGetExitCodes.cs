using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities
{
    /// <summary>
    /// Maps WinGet CLI HRESULT exit codes to <see cref="InstallFailureReason"/>.
    /// Reference: https://github.com/microsoft/winget-cli/blob/master/doc/windows/package-manager/winget/returnCodes.md
    /// </summary>
    public static class WinGetExitCodes
    {
        // Success codes
        public const int Ok = 0;
        public const int RestartRequired = unchecked((int)0x8A150019);

        // Error codes (HRESULT values cast to int for exit code comparison)
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

        // Uninstall-specific error codes
        // The underlying uninstaller returned a non-zero exit code (e.g. Chromium-based
        // apps always return 19 even on successful uninstall).
        public const int ExecUninstallCommandFailed = unchecked((int)0x8A150030);
        public const int NoUninstallInfoFound = unchecked((int)0x8A15002F);

        public static bool IsSuccess(int exitCode)
            => exitCode == Ok || exitCode == RestartRequired
            || exitCode == AlreadyInstalled || exitCode == UpdateNotApplicable;

        /// <summary>
        /// Returns true if the exit code indicates a potential false-positive uninstall failure
        /// that should be verified by checking whether the package is actually still installed.
        /// WinGet wraps any non-zero exit code from the underlying uninstaller into
        /// EXEC_UNINSTALL_COMMAND_FAILED, even when the uninstall actually succeeded
        /// (e.g. Chromium-based apps always return exit code 19).
        /// </summary>
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
}
