using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services
{
    /// <summary>
    /// Base class for installation services that provides common functionality.
    /// </summary>
    /// <typeparam name="T">The type of item to install, which must implement IInstallableItem.</typeparam>
    public abstract class BaseInstallationService<T> : IInstallationService<T> where T : IInstallableItem
    {
        protected readonly ILogService _logService;
        protected readonly IPowerShellExecutionService _powerShellService;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseInstallationService{T}"/> class.
        /// </summary>
        /// <param name="logService">The log service.</param>
        /// <param name="powerShellService">The PowerShell execution service.</param>
        protected BaseInstallationService(
            ILogService logService,
            IPowerShellExecutionService powerShellService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
        }

        /// <inheritdoc/>
        public Task<OperationResult<bool>> InstallAsync(
            T item,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return InstallItemAsync(item, progress, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<OperationResult<bool>> CanInstallAsync(T item)
        {
            return CanInstallItemAsync(item);
        }

        /// <summary>
        /// Internal method to install an item.
        /// </summary>
        /// <param name="item">The item to install.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An operation result indicating success or failure with error details.</returns>
        protected async Task<OperationResult<bool>> InstallItemAsync(
            T item,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            try
            {
                // Report initial progress
                progress?.Report(
                    new TaskProgressDetail
                    {
                        Progress = 0,
                        StatusText = $"Starting installation of {item.DisplayName}...",
                        DetailedMessage = $"Preparing to install {item.DisplayName}",
                    }
                );

                _logService.LogInformation($"Attempting to install {item.DisplayName} ({item.PackageId})");

                // Perform the actual installation
                var result = await PerformInstallationAsync(item, progress, cancellationToken);

                if (result.Success)
                {
                    // Report completion
                    progress?.Report(
                        new TaskProgressDetail
                        {
                            Progress = 100,
                            StatusText = $"{item.DisplayName} installed successfully!",
                            DetailedMessage = $"Successfully installed {item.DisplayName}",
                        }
                    );

                    if (item.RequiresRestart)
                    {
                        progress?.Report(
                            new TaskProgressDetail
                            {
                                StatusText = "A system restart is required to complete the installation",
                                DetailedMessage = "Please restart your computer to complete the installation",
                                LogLevel = LogLevel.Warning,
                            }
                        );
                        _logService.LogWarning($"A system restart is required for {item.DisplayName}");
                    }

                    _logService.LogSuccess($"Successfully installed {item.DisplayName}");
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                progress?.Report(
                    new TaskProgressDetail
                    {
                        Progress = 0,
                        StatusText = $"Installation of {item.DisplayName} was cancelled",
                        DetailedMessage = "The installation was cancelled by the user",
                        LogLevel = LogLevel.Warning,
                    }
                );

                _logService.LogWarning($"Operation cancelled when installing {item.DisplayName}");
                return OperationResult<bool>.Failed("The installation was cancelled by the user");
            }
            catch (Exception ex)
            {
                progress?.Report(
                    new TaskProgressDetail
                    {
                        Progress = 0,
                        StatusText = $"Error installing {item.DisplayName}: {ex.Message}",
                        DetailedMessage = $"Exception during installation: {ex.Message}",
                        LogLevel = LogLevel.Error,
                    }
                );
                _logService.LogError($"Error installing {item.DisplayName}", ex);
                return OperationResult<bool>.Failed($"Error installing {item.DisplayName}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Checks if an item can be installed.
        /// </summary>
        /// <param name="item">The item to check.</param>
        /// <returns>An operation result indicating if the item can be installed, with error details if not.</returns>
        protected Task<OperationResult<bool>> CanInstallItemAsync(T item)
        {
            if (item == null)
            {
                return Task.FromResult(OperationResult<bool>.Failed("Item information cannot be null"));
            }

            // Basic implementation: Assume all items can be installed for now.
            // Derived classes can override this method to provide specific checks.
            return Task.FromResult(OperationResult<bool>.Succeeded(true));
        }

        /// <summary>
        /// Performs the actual installation of the item.
        /// </summary>
        /// <param name="item">The item to install.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An operation result indicating success or failure with error details.</returns>
        protected abstract Task<OperationResult<bool>> PerformInstallationAsync(
            T item,
            IProgress<TaskProgressDetail>? progress,
            CancellationToken cancellationToken);
    }
}