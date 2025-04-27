using System;

namespace Winhance.Core.Features.SoftwareApps.Enums
{
    /// <summary>
    /// Defines the types of errors that can occur during installation operations.
    /// </summary>
    public enum InstallationErrorType
    {
        /// <summary>
        /// An unknown error occurred during installation.
        /// </summary>
        UnknownError,

        /// <summary>
        /// A network-related error occurred during installation.
        /// </summary>
        NetworkError,

        /// <summary>
        /// A permission-related error occurred during installation.
        /// </summary>
        PermissionError,

        /// <summary>
        /// The package was not found in the repositories.
        /// </summary>
        PackageNotFoundError,

        /// <summary>
        /// WinGet is not installed and could not be installed automatically.
        /// </summary>
        WinGetNotInstalledError,

        /// <summary>
        /// The package is already installed.
        /// </summary>
        AlreadyInstalledError,

        /// <summary>
        /// The installation was cancelled by the user.
        /// </summary>
        CancelledByUserError,

        /// <summary>
        /// The system is in a state that prevents installation.
        /// </summary>
        SystemStateError,

        /// <summary>
        /// The package is corrupted or invalid.
        /// </summary>
        PackageCorruptedError,

        /// <summary>
        /// The package dependencies could not be resolved.
        /// </summary>
        DependencyResolutionError
    }
}