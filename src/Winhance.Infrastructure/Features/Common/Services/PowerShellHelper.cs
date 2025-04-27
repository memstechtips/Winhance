using System;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Helper class for common PowerShell execution patterns.
    /// </summary>
    public static class PowerShellHelper
    {
        /// <summary>
        /// Executes a PowerShell script with progress reporting and error handling.
        /// </summary>
        /// <param name="powerShell">The PowerShell instance to use.</param>
        /// <param name="script">The script to execute.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The collection of PSObjects returned by the script.</returns>
        public static async Task<Collection<PSObject>> ExecuteScriptAsync(
            this PowerShell powerShell,
            string script,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (powerShell == null)
            {
                throw new ArgumentNullException(nameof(powerShell));
            }

            if (string.IsNullOrWhiteSpace(script))
            {
                throw new ArgumentException("Script cannot be null or empty", nameof(script));
            }

            powerShell.AddScript(script);

            // Add event handlers for progress reporting
            if (progress != null)
            {
                powerShell.Streams.Information.DataAdded += (sender, e) =>
                {
                    var info = powerShell.Streams.Information[e.Index];
                    progress.Report(new TaskProgressDetail
                    {
                        DetailedMessage = info.MessageData.ToString(),
                        LogLevel = LogLevel.Info
                    });
                };

                powerShell.Streams.Error.DataAdded += (sender, e) =>
                {
                    var error = powerShell.Streams.Error[e.Index];
                    progress.Report(new TaskProgressDetail
                    {
                        DetailedMessage = error.Exception?.Message ?? error.ToString(),
                        LogLevel = LogLevel.Error
                    });
                };

                powerShell.Streams.Warning.DataAdded += (sender, e) =>
                {
                    var warning = powerShell.Streams.Warning[e.Index];
                    progress.Report(new TaskProgressDetail
                    {
                        DetailedMessage = warning.Message,
                        LogLevel = LogLevel.Warning
                    });
                };

                powerShell.Streams.Progress.DataAdded += (sender, e) =>
                {
                    var progressRecord = powerShell.Streams.Progress[e.Index];
                    var percentComplete = progressRecord.PercentComplete;
                    if (percentComplete >= 0 && percentComplete <= 100)
                    {
                        progress.Report(new TaskProgressDetail
                        {
                            Progress = percentComplete,
                            StatusText = progressRecord.Activity,
                            DetailedMessage = progressRecord.StatusDescription
                        });
                    }
                };
            }

            // Execute the script
            return await Task.Run(() => powerShell.Invoke(), cancellationToken);
        }

        /// <summary>
        /// Executes a PowerShell script asynchronously.
        /// </summary>
        /// <param name="powerShell">The PowerShell instance to use.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The data collection of PSObjects returned by the script.</returns>
        public static async Task<PSDataCollection<PSObject>> InvokeAsync(
            this PowerShell powerShell,
            CancellationToken cancellationToken = default)
        {
            if (powerShell == null)
            {
                throw new ArgumentNullException(nameof(powerShell));
            }

            var output = new PSDataCollection<PSObject>();
            var asyncResult = powerShell.BeginInvoke<PSObject, PSObject>(null, output);

            await Task.Run(() =>
            {
                while (!asyncResult.IsCompleted)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        powerShell.Stop();
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    Thread.Sleep(100);
                }
                powerShell.EndInvoke(asyncResult);
            }, cancellationToken);

            return output;
        }

        /// <summary>
        /// Parses a result string in the format "STATUS|Message|RebootRequired".
        /// </summary>
        /// <param name="resultString">The result string to parse.</param>
        /// <param name="itemName">The name of the item being installed.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="logService">The log service for logging.</param>
        /// <returns>An operation result indicating success or failure with error details.</returns>
        public static OperationResult<bool> ParseResultString(
            string resultString,
            string itemName,
            IProgress<TaskProgressDetail>? progress = null,
            ILogService? logService = null)
        {
            if (string.IsNullOrEmpty(resultString))
            {
                progress?.Report(new TaskProgressDetail
                {
                    StatusText = "Script returned no result",
                    LogLevel = LogLevel.Error
                });
                logService?.LogError($"Empty result returned when processing: {itemName}");
                return OperationResult<bool>.Failed("Script returned no result");
            }

            var parts = resultString.Split('|');
            if (parts.Length < 2)
            {
                progress?.Report(new TaskProgressDetail
                {
                    StatusText = "Error processing script result",
                    LogLevel = LogLevel.Error
                });
                logService?.LogError($"Unexpected script output format for {itemName}: {resultString}");
                return OperationResult<bool>.Failed("Unexpected script output format: " + resultString);
            }

            string status = parts[0];
            string message = parts[1];
            bool rebootRequired = parts.Length > 2 && bool.TryParse(parts[2], out bool req) && req;

            if (status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                progress?.Report(new TaskProgressDetail
                {
                    Progress = 100,
                    StatusText = $"Successfully processed: {itemName}",
                    DetailedMessage = message
                });
                logService?.LogSuccess($"Successfully processed: {itemName}. {message}");

                if (rebootRequired)
                {
                    progress?.Report(new TaskProgressDetail
                    {
                        StatusText = "A system restart is required to complete the installation",
                        DetailedMessage = "Please restart your computer to complete the installation",
                        LogLevel = LogLevel.Warning
                    });
                    logService?.LogWarning($"A system restart is required for {itemName}");
                }
                return OperationResult<bool>.Succeeded(true);
            }
            else // FAILURE
            {
                progress?.Report(new TaskProgressDetail
                {
                    Progress = 0,
                    StatusText = $"Failed to process: {itemName}",
                    DetailedMessage = message,
                    LogLevel = LogLevel.Error
                });
                logService?.LogError($"Failed to process: {itemName}. {message}");
                return OperationResult<bool>.Failed(message);
            }
        }
    }
}