using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Verification;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Verification.Methods
{
    /// <summary>
    /// Verifies software installations by checking common installation directories.
    /// </summary>
    public class FileSystemVerificationMethod : VerificationMethodBase, IVerificationMethod
    {
        private static readonly string[] CommonInstallPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs"
            ),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                @"Microsoft\Windows\Start Menu\Programs"
            ),
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSystemVerificationMethod"/> class.
        /// </summary>
        public FileSystemVerificationMethod()
            : base("FileSystem", priority: 20) { }

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

                        // First try: Check if the app is in PATH (for CLI tools)
                        try
                        {
                            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                            var paths = pathEnv.Split(Path.PathSeparator);


                            foreach (var path in paths)
                            {
                                if (string.IsNullOrEmpty(path))
                                    continue;


                                var exePath = Path.Combine(path, $"{packageId}.exe");
                                if (File.Exists(exePath))
                                {
                                    return new VerificationResult
                                    {
                                        IsVerified = true,
                                        Message = $"Found in PATH: {exePath}",
                                        MethodUsed = "FileSystem",
                                        AdditionalInfo = new
                                        {
                                            InstallPath = path,
                                            ExecutablePath = exePath,
                                        },
                                    };
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Ignore PATH check errors and continue with other methods
                        }

                        // Second try: Check common installation directories
                        foreach (var basePath in CommonInstallPaths)
                        {
                            if (!Directory.Exists(basePath))
                                continue;

                            try
                            {
                                // Check for exact match in directory names
                                var matchingDirs = Directory
                                    .EnumerateDirectories(
                                        basePath,
                                        "*",
                                        SearchOption.TopDirectoryOnly
                                    )
                                    .Where(d =>
                                        Path.GetFileName(d)
                                            ?.Equals(packageId, StringComparison.OrdinalIgnoreCase)
                                            == true
                                        || Path.GetFileName(d)
                                            ?.Contains(
                                                packageId,
                                                StringComparison.OrdinalIgnoreCase
                                            ) == true
                                    )
                                    .ToList();

                                if (matchingDirs.Any())
                                {
                                    return new VerificationResult
                                    {
                                        IsVerified = true,
                                        Message = $"Found in file system: {matchingDirs.First()}",
                                        MethodUsed = "FileSystem",
                                        AdditionalInfo = new
                                        {
                                            InstallPath = matchingDirs.First(),
                                            AllMatchingPaths = matchingDirs.ToArray(),
                                        },
                                    };
                                }


                                // Check for Microsoft Store apps in Packages directory
                                if (basePath.Contains("LocalApplicationData"))
                                {
                                    var packagesPath = Path.Combine(basePath, "Packages");
                                    if (Directory.Exists(packagesPath))
                                    {
                                        var storeAppDirs = Directory
                                            .EnumerateDirectories(
                                                packagesPath,
                                                "*",
                                                SearchOption.TopDirectoryOnly
                                            )
                                            .Where(d =>
                                                Path.GetFileName(d)
                                                    ?.IndexOf(packageId, StringComparison.OrdinalIgnoreCase) >= 0
                                            )
                                            .ToList();


                                        foreach (var dir in storeAppDirs)
                                        {
                                            var manifestPath = Path.Combine(dir, "AppxManifest.xml");
                                            if (File.Exists(manifestPath))
                                            {
                                                return new VerificationResult
                                                {
                                                    IsVerified = true,
                                                    Message = $"Found Microsoft Store app: {dir}",
                                                    MethodUsed = "FileSystem",
                                                    AdditionalInfo = new
                                                    {
                                                        InstallPath = dir,
                                                        ManifestPath = manifestPath,
                                                    },
                                                };
                                            }
                                        }
                                    }
                                }
                            }
                            catch (UnauthorizedAccessException)
                            {
                                // Skip directories we can't access
                                continue;
                            }
                            catch (Exception)
                            {
                                // Skip directories with other errors
                                continue;
                            }
                        }

                        return VerificationResult.Failure(
                            $"Package '{packageId}' not found in common installation directories"
                        );
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
            // For file system, we can't reliably determine version from directory name
            // So we'll just check for presence and let other methods verify version
            return await VerifyPresenceAsync(packageId, cancellationToken).ConfigureAwait(false);
        }
    }
}
