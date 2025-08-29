using System;
using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Models
{
    /// <summary>
    /// Represents the result of a package installation operation.
    /// </summary>
    public class InstallationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the installation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets an optional message providing additional information about the result.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the package ID that was installed.
        /// </summary>
        public string PackageId { get; set; }

        /// <summary>
        /// Gets or sets the version of the package that was installed.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the exit code from the installation process.
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// Gets or sets the standard output from the installation process.
        /// </summary>
        public string Output { get; set; }

        /// <summary>
        /// Gets or sets the error output from the installation process.
        /// </summary>
        public string? Error { get; set; }
    }


    /// <summary>
    /// Represents options for package installation.
    /// </summary>
    public class InstallationOptions
    {
        /// <summary>
        /// Gets or sets the version of the package to install. If null, the latest version is installed.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the source to install the package from.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to accept package agreements automatically.
        /// </summary>
        public bool AcceptPackageAgreements { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to accept source agreements automatically.
        /// </summary>
        public bool AcceptSourceAgreements { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to run the installation in silent mode.
        /// </summary>
        public bool Silent { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to force the installation even if the package is already installed.
        /// </summary>
        public bool Force { get; set; }

        /// <summary>
        /// Gets or sets the installation scope (user or machine).
        /// </summary>
        public PackageInstallScope Scope { get; set; } = PackageInstallScope.User;

        /// <summary>
        /// Gets or sets the installation location.
        /// </summary>
        public string Location { get; set; }
    }


    /// <summary>
    /// Represents the result of a package upgrade operation.
    /// </summary>
    public class UpgradeResult : InstallationResult
    {
        /// <summary>
        /// Gets or sets the version that was upgraded from.
        /// </summary>
        public string PreviousVersion { get; set; }
    }

    /// <summary>
    /// Represents options for package upgrades.
    /// </summary>
    public class UpgradeOptions : InstallationOptions
    {
        // Inherits all properties from InstallationOptions
    }

    /// <summary>
    /// Represents the result of a package uninstallation operation.
    /// </summary>
    public class UninstallationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the uninstallation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets an optional message providing additional information about the result.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the package ID that was uninstalled.
        /// </summary>
        public string PackageId { get; set; }

        /// <summary>
        /// Gets or sets the version of the package that was uninstalled.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the exit code from the uninstallation process.
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// Gets or sets the standard output from the uninstallation process.
        /// </summary>
        public string Output { get; set; }

        /// <summary>
        /// Gets or sets the error output from the uninstallation process.
        /// </summary>
        public string Error { get; set; }
    }


    /// <summary>
    /// Represents options for package uninstallation.
    /// </summary>
    public class UninstallationOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether to run the uninstallation in silent mode.
        /// </summary>
        public bool Silent { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to force the uninstallation.
        /// </summary>
        public bool Force { get; set; }

        /// <summary>
        /// Gets or sets the installation scope (user or machine) to uninstall from.
        /// </summary>
        public PackageInstallScope Scope { get; set; } = PackageInstallScope.User;
    }

    /// <summary>
    /// Represents information about a package.
    /// </summary>
    public class PackageInfo
    {
        /// <summary>
        /// Gets or sets the package ID.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the package name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the package version.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the package source.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the package is installed.
        /// </summary>
        public bool IsInstalled { get; set; }

        /// <summary>
        /// Gets or sets the installed version if different from the available version.
        /// </summary>
        public string InstalledVersion { get; set; }
    }


    /// <summary>
    /// Represents options for package search.
    /// </summary>
    public class SearchOptions
    {
        /// <summary>
        /// Gets or sets the maximum number of results to return.
        /// </summary>
        public int? Count { get; set; }

        /// <summary>
        /// Gets or sets the source to search in.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to include packages that are already installed.
        /// </summary>
        public bool IncludeInstalled { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to include packages that are not installed.
        /// </summary>
        public bool IncludeAvailable { get; set; } = true;
    }

    /// <summary>
    /// Represents the installation scope for a package.
    /// </summary>
    public enum PackageInstallScope
    {
        /// <summary>
        /// Install for the current user only.
        /// </summary>
        User,

        /// <summary>
        /// Install for all users (requires elevation).
        /// </summary>
        Machine
    }

    /// <summary>
    /// Represents the progress of a package installation.
    /// </summary>
    public class InstallationProgress
    {
        /// <summary>
        /// Gets or sets the current progress percentage (0-100).
        /// </summary>
        public int Percentage { get; set; }

        /// <summary>
        /// Gets or sets the current status message.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets the current operation being performed.
        /// </summary>
        public string Operation { get; set; }

        /// <summary>
        /// Gets or sets the package ID being processed.
        /// </summary>
        public string PackageId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the progress is indeterminate.
        /// </summary>
        public bool IsIndeterminate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the operation was cancelled.
        /// </summary>
        public bool IsCancelled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether an error occurred during the operation.
        /// </summary>
        public bool IsError { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the operation failed due to connectivity issues.
        /// </summary>
        public bool IsConnectivityIssue { get; set; }
    }

    /// <summary>
    /// Represents the progress of a package upgrade operation.
    /// </summary>
    public class UpgradeProgress
    {
        /// <summary>
        /// Gets or sets the percentage of completion (0-100).
        /// </summary>
        public int Percentage { get; set; }

        /// <summary>
        /// Gets or sets the status message.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the progress is indeterminate.
        /// </summary>
        public bool IsIndeterminate { get; set; }

        /// <summary>
        /// Gets or sets the package ID being processed.
        /// </summary>
        public string PackageId { get; set; }
    }

    /// <summary>
    /// Represents the progress of a package uninstallation operation.
    /// </summary>
    public class UninstallationProgress
    {
        /// <summary>
        /// Gets or sets the percentage of completion (0-100).
        /// </summary>
        public int Percentage { get; set; }

        /// <summary>
        /// Gets or sets the status message.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the progress is indeterminate.
        /// </summary>
        public bool IsIndeterminate { get; set; }

        /// <summary>
        /// Gets or sets the package ID being processed.
        /// </summary>
        public string PackageId { get; set; }
    }
}
