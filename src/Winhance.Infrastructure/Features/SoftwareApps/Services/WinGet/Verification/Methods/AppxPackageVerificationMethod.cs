using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Verification;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Verification.Methods
{
    /// <summary>
    /// Verifies UWP app installations by querying the AppX package manager.
    /// </summary>
    public class AppxPackageVerificationMethod : VerificationMethodBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AppxPackageVerificationMethod"/> class.
        /// </summary>
        public AppxPackageVerificationMethod()
            : base("AppxPackage", priority: 15) { }

        /// <inheritdoc/>
        protected override async Task<VerificationResult> VerifyPresenceAsync(
            string packageId,
            CancellationToken cancellationToken
        )
        {
            return await Task.Run(
                    () =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            var packages = GetAppxPackages();
                            var matchingPackage = packages.FirstOrDefault(p =>
                                p.Name.Equals(packageId, StringComparison.OrdinalIgnoreCase)
                                || p.PackageFullName.StartsWith(
                                    packageId,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            );

                            if (matchingPackage != null)
                            {
                                return new VerificationResult
                                {
                                    IsVerified = true,
                                    Message =
                                        $"Found UWP package: {matchingPackage.Name} (Version: {matchingPackage.Version})",
                                    MethodUsed = "AppxPackage",
                                    AdditionalInfo = matchingPackage,
                                };
                            }

                            return VerificationResult.Failure(
                                $"UWP package '{packageId}' not found"
                            );
                        }
                        catch (Exception ex)
                        {
                            return VerificationResult.Failure(
                                $"Error checking UWP packages: {ex.Message}"
                            );
                        }
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        /// <inheritdoc/>
        protected override async Task<VerificationResult> VerifyVersionAsync(
            string packageId,
            string version,
            CancellationToken cancellationToken
        )
        {
            var result = await VerifyPresenceAsync(packageId, cancellationToken)
                .ConfigureAwait(false);
            if (!result.IsVerified)
                return result;

            // Extract the version from the additional info
            var installedVersion = (result.AdditionalInfo as AppxPackageInfo)?.Version;
            if (string.IsNullOrEmpty(installedVersion))
                return VerificationResult.Failure(
                    $"Could not determine installed version for UWP package '{packageId}'",
                    "AppxPackage"
                );

            // Simple version comparison (this could be enhanced with proper version comparison logic)
            if (!installedVersion.Equals(version, StringComparison.OrdinalIgnoreCase))
                return VerificationResult.Failure(
                    $"Version mismatch for UWP package '{packageId}'. Installed: {installedVersion}, Expected: {version}",
                    "AppxPackage"
                );

            return result;
        }

        private static List<AppxPackageInfo> GetAppxPackages()
        {
            var packages = new List<AppxPackageInfo>();

            try
            {
                // Method 1: Using Get-AppxPackage via PowerShell (more reliable for current user)
                using (var ps = System.Management.Automation.PowerShell.Create())
                {
                    var results = ps.AddCommand("Get-AppxPackage").Invoke();
                    foreach (var result in results)
                    {
                        packages.Add(
                            new AppxPackageInfo
                            {
                                Name = result.Properties["Name"]?.Value?.ToString(),
                                PackageFullName = result
                                    .Properties["PackageFullName"]
                                    ?.Value?.ToString(),
                                Version = result.Properties["Version"]?.Value?.ToString(),
                                InstallLocation = result
                                    .Properties["InstallLocation"]
                                    ?.Value?.ToString(),
                            }
                        );
                    }
                }
            }
            catch
            {
                // Fall back to WMI if PowerShell fails
                try
                {
                    var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Product");
                    foreach (var obj in searcher.Get().Cast<ManagementObject>())
                    {
                        packages.Add(
                            new AppxPackageInfo
                            {
                                Name = obj["Name"]?.ToString(),
                                Version = obj["Version"]?.ToString(),
                                InstallLocation = obj["InstallLocation"]?.ToString(),
                            }
                        );
                    }
                }
                catch
                {
                    // If both methods fail, return an empty list
                }
            }

            return packages;
        }
    }

    /// <summary>
    /// Represents information about an AppX package.
    /// </summary>
    public class AppxPackageInfo
    {
        /// <summary>
        /// Gets or sets the name of the package.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the full name of the package.
        /// </summary>
        public string PackageFullName { get; set; }

        /// <summary>
        /// Gets or sets the version of the package.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the installation location of the package.
        /// </summary>
        public string InstallLocation { get; set; }
    }
}
