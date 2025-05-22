using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Verification;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Verification.Methods
{
    /// <summary>
    /// Verifies software installations by querying WinGet.
    /// </summary>
    public class WinGetVerificationMethod : VerificationMethodBase
    {
        private const string WinGetExe = "winget.exe";
        private static readonly string[] WinGetPaths = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Microsoft\WindowsApps"
            ),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                @"WindowsApps\Microsoft.DesktopAppInstaller_8wekyb3d8bbwe"
            ),
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="WinGetVerificationMethod"/> class.
        /// </summary>
        public WinGetVerificationMethod()
            : base("WinGet", priority: 5) // Higher priority as it's the most reliable source
        { }

        /// <inheritdoc/>
        protected override async Task<VerificationResult> VerifyPresenceAsync(
            string packageId,
            CancellationToken cancellationToken
        )
        {
            // First try with the exact match approach using winget list
            try
            {
                var exactMatchResult = await ExecuteWinGetCommandAsync(
                    "list",
                    $"--id {packageId} --exact",
                    cancellationToken
                );

                // Check if the package is found in the output
                if (
                    exactMatchResult.ExitCode == 0
                    && !string.IsNullOrWhiteSpace(exactMatchResult.Output)
                    && exactMatchResult.Output.Contains(packageId)
                )
                {
                    // Extract version if possible
                    string version = "unknown";
                    string source = "unknown";

                    var lines = exactMatchResult.Output.Split(
                        new[] { '\r', '\n' },
                        StringSplitOptions.RemoveEmptyEntries
                    );

                    if (lines.Length >= 2)
                    {
                        var parts = lines[1]
                            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1)
                            version = parts[1];
                        if (parts.Length > 2)
                            source = parts[2];
                    }

                    return new VerificationResult
                    {
                        IsVerified = true,
                        Message =
                            $"Found in WinGet: {packageId} (Version: {version}, Source: {source})",
                        MethodUsed = "WinGet",
                        AdditionalInfo = new
                        {
                            PackageId = packageId,
                            Version = version,
                            Source = source,
                        },
                    };
                }

                // If exact match failed, try a more flexible approach with just the package ID
                var flexibleResult = await ExecuteWinGetCommandAsync(
                    "list",
                    $"\"{packageId}\"",
                    cancellationToken
                );

                if (
                    flexibleResult.ExitCode == 0
                    && !string.IsNullOrWhiteSpace(flexibleResult.Output)
                )
                {
                    // Check if any line contains the package ID
                    var lines = flexibleResult.Output.Split(
                        new[] { '\r', '\n' },
                        StringSplitOptions.RemoveEmptyEntries
                    );

                    foreach (var line in lines)
                    {
                        if (line.IndexOf(packageId, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            // Found a match
                            var parts = line.Split(
                                new[] { ' ' },
                                StringSplitOptions.RemoveEmptyEntries
                            );
                            string version = parts.Length > 1 ? parts[1] : "unknown";
                            string source = parts.Length > 2 ? parts[2] : "unknown";

                            return new VerificationResult
                            {
                                IsVerified = true,
                                Message =
                                    $"Found in WinGet: {packageId} (Version: {version}, Source: {source})",
                                MethodUsed = "WinGet",
                                AdditionalInfo = new
                                {
                                    PackageId = packageId,
                                    Version = version,
                                    Source = source,
                                },
                            };
                        }
                    }
                }

                // If we got here, the package wasn't found
                return VerificationResult.Failure($"Package '{packageId}' not found via WinGet");
            }
            catch (Exception ex)
            {
                return VerificationResult.Failure($"Error querying WinGet: {ex.Message}");
            }
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
            var installedVersion = (string)((dynamic)result.AdditionalInfo)?.Version;
            if (string.IsNullOrEmpty(installedVersion))
                return VerificationResult.Failure(
                    $"Could not determine installed version for '{packageId}'",
                    "WinGet"
                );

            // Simple version comparison (this could be enhanced with proper version comparison logic)
            if (!installedVersion.Equals(version, StringComparison.OrdinalIgnoreCase))
                return VerificationResult.Failure(
                    $"Version mismatch for '{packageId}'. Installed: {installedVersion}, Expected: {version}",
                    "WinGet"
                );

            return result;
        }

        private static async Task<(int ExitCode, string Output)> ExecuteWinGetCommandAsync(
            string command,
            string arguments,
            CancellationToken cancellationToken
        )
        {
            var winGetPath = FindWinGetPath();
            if (string.IsNullOrEmpty(winGetPath))
                throw new InvalidOperationException(
                    "WinGet is not installed or could not be found"
                );

            var startInfo = new ProcessStartInfo
            {
                FileName = winGetPath,
                Arguments = $"{command} {arguments}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using (var process = new Process { StartInfo = startInfo })
            using (var outputWaitHandle = new System.Threading.ManualResetEvent(false))
            using (var errorWaitHandle = new System.Threading.ManualResetEvent(false))
            {
                var output = new StringBuilder();
                var error = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        output.AppendLine(e.Data);
                    else
                        outputWaitHandle.Set();
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        error.AppendLine(e.Data);
                    else
                        errorWaitHandle.Set();
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for the process to exit or the cancellation token to be triggered
                await Task.Run(
                        () =>
                        {
                            while (!process.WaitForExit(100))
                            {
                                if (cancellationToken.IsCancellationRequested)
                                {
                                    try
                                    {
                                        process.Kill();
                                    }
                                    catch
                                    { /* Ignore */
                                    }
                                    cancellationToken.ThrowIfCancellationRequested();
                                }
                            }
                        },
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                outputWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                errorWaitHandle.WaitOne(TimeSpan.FromSeconds(5));

                // If there was an error, include it in the output
                if (!string.IsNullOrWhiteSpace(error.ToString()))
                    output.AppendLine("Error: ").Append(error);

                return (process.ExitCode, output.ToString().Trim());
            }
        }

        private static string FindWinGetPath()
        {
            // Check if winget is in the PATH
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            if (
                pathEnv
                    .Split(Path.PathSeparator)
                    .Any(p => !string.IsNullOrEmpty(p) && File.Exists(Path.Combine(p, WinGetExe)))
            )
            {
                return WinGetExe;
            }

            // Check common installation paths
            foreach (var basePath in WinGetPaths)
            {
                var fullPath = Path.Combine(basePath, WinGetExe);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            return null;
        }
    }
}
