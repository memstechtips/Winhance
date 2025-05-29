using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Verification;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services
{
    /// <summary>
    /// Adapter that implements the legacy IWinGetInstallationService using the new IWinGetInstaller.
    /// </summary>
    public class WinGetInstallationServiceAdapter : IWinGetInstallationService, IDisposable
    {
        private readonly IWinGetInstaller _winGetInstaller;
        private readonly ITaskProgressService _taskProgressService;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="WinGetInstallationServiceAdapter"/> class.
        /// </summary>
        /// <param name="winGetInstaller">The WinGet installer service.</param>
        /// <param name="taskProgressService">The task progress service.</param>
        public WinGetInstallationServiceAdapter(
            IWinGetInstaller winGetInstaller,
            ITaskProgressService taskProgressService)
        {
            _winGetInstaller = winGetInstaller ?? throw new ArgumentNullException(nameof(winGetInstaller));
            _taskProgressService = taskProgressService ?? throw new ArgumentNullException(nameof(taskProgressService));
        }

        /// <inheritdoc/>
        public async Task<bool> InstallWithWingetAsync(
            string packageName,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default,
            string? displayName = null)
        {
            if (string.IsNullOrWhiteSpace(packageName))
                throw new ArgumentException("Package name cannot be null or empty", nameof(packageName));

            var displayNameToUse = displayName ?? packageName;
            var progressWrapper = new ProgressAdapter(progress);
            
            try
            {
                // Report initial progress
                progressWrapper.Report(0, $"Starting installation of {displayNameToUse}...");

                // Check if WinGet is installed first
                bool wingetInstalled = await IsWinGetInstalledAsync().ConfigureAwait(false);
                if (!wingetInstalled)
                {
                    progressWrapper.Report(10, "WinGet is not installed. Installing WinGet first...");
                    
                    // Install WinGet
                    await InstallWinGetAsync(progress).ConfigureAwait(false);
                    
                    // Verify WinGet installation was successful
                    wingetInstalled = await IsWinGetInstalledAsync().ConfigureAwait(false);
                    if (!wingetInstalled)
                    {
                        progressWrapper.Report(0, "Failed to install WinGet. Cannot proceed with application installation.");
                        return false;
                    }
                    
                    progressWrapper.Report(30, $"WinGet installed successfully. Continuing with {displayNameToUse} installation...");
                }

                // Now use the WinGet installer to install the package
                var result = await _winGetInstaller.InstallPackageAsync(
                    packageName,
                    new InstallationOptions
                    {
                        // Configure installation options as needed
                        Silent = true
                    },
                    displayNameToUse,  // Pass the display name to use in progress reporting
                    cancellationToken)
                    .ConfigureAwait(false);

                if (result.Success)
                {
                    progressWrapper.Report(100, $"Successfully installed {displayNameToUse}");
                    return true;
                }
                
                progressWrapper.Report(0, $"Failed to install {displayNameToUse}: {result.Message}");
                return false;
            }
            catch (Exception ex)
            {
                progressWrapper.Report(0, $"Error installing {displayNameToUse}: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task InstallWinGetAsync(IProgress<TaskProgressDetail>? progress = null)
        {
            // Check if WinGet is already installed
            if (await IsWinGetInstalledAsync().ConfigureAwait(false))
            {
                progress?.Report(new TaskProgressDetail { StatusText = "WinGet is already installed" });
                return;
            }

            var progressWrapper = new ProgressAdapter(progress);
            
            try
            {
                progressWrapper.Report(0, "Downloading WinGet installer...");
                
                // Directly call the WinGetInstaller's method to install WinGet
                // This is more efficient than using a search operation to trigger installation
                try 
                {
                    progressWrapper.Report(20, "Installing WinGet...");
                    
                    // Directly trigger WinGet installation
                    var installResult = await _winGetInstaller.TryInstallWinGetAsync(CancellationToken.None)
                        .ConfigureAwait(false);
                    
                    if (installResult)
                    {
                        progressWrapper.Report(80, "WinGet installation in progress...");
                        progressWrapper.Report(100, "WinGet installed successfully");
                    }
                    else
                    {
                        progressWrapper.Report(0, "Failed to install WinGet");
                        throw new InvalidOperationException("WinGet installation failed");
                    }
                }
                catch (Exception ex)
                {
                    progressWrapper.Report(0, $"Error installing WinGet: {ex.Message}");
                    throw;
                }
                
                // Verify WinGet installation
                bool isInstalled = await IsWinGetInstalledAsync().ConfigureAwait(false);
                if (!isInstalled)
                {
                    progressWrapper.Report(0, "WinGet installation verification failed");
                    throw new InvalidOperationException("WinGet installation could not be verified");
                }
            }
            catch (Exception ex)
            {
                progressWrapper.Report(0, $"Error installing WinGet: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> IsWinGetInstalledAsync()
        {
            try
            {
                // Use a simple version check to see if WinGet is working
                // This is more direct than doing a search operation
                var result = await _winGetInstaller.ExecuteWinGetCommandAsync(
                    "winget.exe",
                    "-v",
                    null,
                    CancellationToken.None).ConfigureAwait(false);
                
                return result.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="WinGetInstallationServiceAdapter"/> 
        /// and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources here if needed
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Adapter to convert between IProgress<TaskProgressDetail> and IProgress<int>
        /// </summary>
        private class ProgressAdapter
        {
            private readonly IProgress<TaskProgressDetail>? _progress;

            public ProgressAdapter(IProgress<TaskProgressDetail>? progress)
            {
                _progress = progress;
            }

            public void Report(int progress, string status)
            {
                _progress?.Report(new TaskProgressDetail
                {
                    Progress = progress,
                    StatusText = status,
                    LogLevel = progress == 100 ? LogLevel.Info : LogLevel.Debug
                });
            }
        }
    }
}
