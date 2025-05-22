using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Verification;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Verification.Methods
{
    /// <summary>
    /// Verifies if a package is installed by checking the Windows Registry.
    /// This checks both 64-bit and 32-bit registry views.
    /// </summary>
    public class RegistryVerificationMethod : VerificationMethodBase
    {
        private const string UninstallKeyPath =
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        private const string UninstallKeyPathWow6432Node =
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistryVerificationMethod"/> class.
        /// </summary>
        public RegistryVerificationMethod()
            : base("Registry", priority: 10) { }

        /// <inheritdoc />
        protected override async Task<VerificationResult> VerifyPresenceAsync(
            string packageId,
            CancellationToken cancellationToken = default
        )
        {
            return await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Check 64-bit registry view
                    using (
                        var baseKey = RegistryKey.OpenBaseKey(
                            RegistryHive.LocalMachine,
                            RegistryView.Registry64
                        )
                    )
                    using (var uninstallKey = baseKey.OpenSubKey(UninstallKeyPath, writable: false))
                    {
                        var result = CheckRegistryKey(uninstallKey, packageId, null);
                        if (result.IsVerified)
                        {
                            return result;
                        }
                    }

                    // Check 32-bit registry view
                    using (
                        var baseKey = RegistryKey.OpenBaseKey(
                            RegistryHive.LocalMachine,
                            RegistryView.Registry32
                        )
                    )
                    using (
                        var uninstallKey = baseKey.OpenSubKey(
                            UninstallKeyPathWow6432Node,
                            writable: false
                        )
                    )
                    {
                        return CheckRegistryKey(uninstallKey, packageId, null);
                    }
                },
                cancellationToken
            );
        }

        /// <summary>
        /// Checks a registry key for the presence of a package.
        /// </summary>
        /// <param name="uninstallKey">The registry key to check.</param>
        /// <param name="packageId">The package ID to look for.</param>
        /// <param name="version">The version to check for, or null to check for any version.</param>
        /// <returns>A <see cref="VerificationResult"/> indicating whether the package was found.</returns>
        private static VerificationResult CheckRegistryKey(
            RegistryKey uninstallKey,
            string packageId,
            string version
        )
        {
            if (uninstallKey == null)
            {
                return VerificationResult.Failure("Registry key not found");
            }

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                using (var subKey = uninstallKey.OpenSubKey(subKeyName))
                {
                    var displayName = subKey?.GetValue("DisplayName") as string;
                    if (string.IsNullOrEmpty(displayName))
                    {
                        continue;
                    }

                    // Check if the display name matches the package ID (case insensitive)
                    if (displayName.IndexOf(packageId, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // If a version is specified, check if it matches
                        if (version != null)
                        {
                            var displayVersion = subKey.GetValue("DisplayVersion") as string;
                            if (
                                !string.Equals(
                                    displayVersion,
                                    version,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            {
                                continue;
                            }
                        }

                        var installLocation = subKey.GetValue("InstallLocation") as string;
                        var uninstallString = subKey.GetValue("UninstallString") as string;
                        var foundVersion = subKey.GetValue("DisplayVersion") as string;

                        return VerificationResult.Success(foundVersion);
                    }
                }
            }

            return VerificationResult.Failure($"Package '{packageId}' not found in registry");
        }

        /// <inheritdoc />
        protected override async Task<VerificationResult> VerifyVersionAsync(
            string packageId,
            string version,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException(
                    "Version cannot be null or whitespace.",
                    nameof(version)
                );
            }

            return await VerifyPresenceAsync(packageId, cancellationToken).ConfigureAwait(false);
        }
    }
}
