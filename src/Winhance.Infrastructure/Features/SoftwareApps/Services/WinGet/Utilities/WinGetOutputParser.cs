using System;
using System.IO;
using System.Text.RegularExpressions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities
{
    /// <summary>
    /// Parses WinGet command output and generates appropriate progress updates.
    /// </summary>
    public class WinGetOutputParser
    {
        private readonly ILogService _logService;
        private InstallationState _currentState = InstallationState.Starting;
        private string _downloadFileName;
        private bool _isVerifying;
        private string _lastProgressLine;
        private int _lastPercentage;
        private bool _hasStarted = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="WinGetOutputParser"/> class.
        /// </summary>
        /// <param name="logService">Optional log service for debugging.</param>
        public WinGetOutputParser(ILogService logService = null)
        {
            _logService = logService;
        }

        // We're using a simpler approach now that directly filters out download information from logs

        /// <summary>
        /// Parses a line of WinGet output and generates an appropriate progress update.
        /// Uses an indeterminate progress indicator and displays raw WinGet output.
        /// </summary>
        /// <param name="outputLine">The line of output to parse.</param>
        /// <returns>An InstallationProgress object with the current progress information, or null if no update is needed.</returns>
        public InstallationProgress ParseOutputLine(string outputLine)
        {
            if (string.IsNullOrWhiteSpace(outputLine))
            {
                return null;
            }

            // Skip logging specific types of output to reduce log noise
            bool shouldLog = true;
            
            // Skip version outputs (like "v1.9.25200")
            if (outputLine.StartsWith("v") && outputLine.Length <= 15)
            {
                shouldLog = false;
            }
            // Skip progress bar outputs and download information
            else if (outputLine.Contains("Γûê") || outputLine.Contains("Γû") || 
                    outputLine.Trim() == "-" || outputLine.Trim() == "\\" || 
                    outputLine.Trim() == "|" || outputLine.Trim() == "/" ||
                    outputLine.Contains("Download information") ||
                    (outputLine.Contains("MB") && outputLine.Contains("/")) ||
                    (outputLine.Contains("KB") && outputLine.Contains("/")))
            {
                shouldLog = false;
                
                // Only log the initial download URL, not the progress bars
                if (outputLine.Contains("Downloading ") && outputLine.Contains("http"))
                {
                    _logService?.LogInformation($"Downloading: {outputLine.Trim()}");
                }
                
                // Still update progress state for progress reporting
                if (outputLine.Contains("MB") || outputLine.Contains("KB"))
                {
                    _currentState = InstallationState.Downloading;
                }
            }
            
            if (shouldLog)
            {
                _logService?.LogInformation($"WinGet output: {outputLine}");
            }
            
            // If this is the first output line, transition from Starting state
            if (!_hasStarted)
            {
                _hasStarted = true;
                _currentState = InstallationState.Resolving;
                
                return new InstallationProgress
                {
                    Status = GetStatusMessage(_currentState),
                    Percentage = 0,
                    IsIndeterminate = true,
                };
            }

            // Check for verification messages
            if (outputLine.Contains("Verifying"))
            {
                _logService?.LogInformation("Verification step detected");
                _currentState = InstallationState.Verifying;
                _isVerifying = true;

                return new InstallationProgress
                {
                    Status = GetStatusMessage(_currentState),
                    Percentage = 0,
                    IsIndeterminate = true,
                };
            }

            // Check for installation messages
            if (
                outputLine.Contains("Installing")
                || outputLine.Contains("installation")
                || (_isVerifying && !outputLine.Contains("Verifying"))
            )
            {
                _logService?.LogInformation("Installation step detected");
                _currentState = InstallationState.Installing;
                _isVerifying = false;

                return new InstallationProgress
                {
                    Status = GetStatusMessage(_currentState),
                    Percentage = 0,
                    IsIndeterminate = true,
                };
            }

            // Check for download information
            if (
                (
                    outputLine.Contains("Downloading")
                    || outputLine.Contains("download")
                    || outputLine.Contains("KB")
                    || outputLine.Contains("MB")
                    || outputLine.Contains("GB")
                )
            )
            {
                // Only log the initial download URL, not the progress bars
                if (outputLine.Contains("Downloading ") && outputLine.Contains("http"))
                {
                    _logService?.LogInformation($"Downloading: {outputLine.Trim()}");
                }
                
                // Set the current state to Downloading
                _currentState = InstallationState.Downloading;

                // Create a progress update with a generic downloading message
                return new InstallationProgress
                {
                    Status = "Downloading package files. This might take a while, please wait...",
                    Percentage = 0, // Not used in indeterminate mode
                    IsIndeterminate = true, // Use indeterminate progress
                    // Set Operation to help identify this is a download operation
                    Operation = "Downloading"
                };
            }

            // Check for installation status
            if (outputLine.Contains("%"))
            {
                var percentageMatch = Regex.Match(outputLine, @"(\d+)%");
                if (percentageMatch.Success)
                {
                    int percentage = int.Parse(percentageMatch.Groups[1].Value);
                    _lastPercentage = percentage;
                    _lastProgressLine = outputLine;

                    return new InstallationProgress
                    {
                        Status = GetStatusMessage(_currentState),
                        Percentage = percentage,
                        IsIndeterminate = false,
                    };
                }
            }

            // Check for completion
            if (
                outputLine.Contains("Successfully installed")
                || outputLine.Contains("completed successfully")
                || outputLine.Contains("installation complete")
            )
            {
                _logService?.LogInformation("Installation completed successfully");
                _currentState = InstallationState.Completing;

                return new InstallationProgress
                {
                    Status = "Installation completed successfully!",
                    Percentage = 100,
                    IsIndeterminate = false,
                    // Note: The installation is complete, but we don't have an IsComplete property
                    // so we just set the percentage to 100 and a clear status message
                };
            }

            // Check for errors
            if (
                outputLine.Contains("error")
                || outputLine.Contains("failed")
                || outputLine.Contains("Error:")
                || outputLine.Contains("Failed:")
            )
            {
                _logService?.LogError($"Installation error detected: {outputLine}");

                return new InstallationProgress
                {
                    Status = $"Error: {outputLine.Trim()}",
                    Percentage = 0,
                    IsIndeterminate = false,
                    // Note: We don't have HasError or ErrorMessage properties
                    // so we just include the error in the Status
                };
            }

            // For other lines, return the last progress if available
            if (!string.IsNullOrEmpty(_lastProgressLine) && _lastPercentage > 0)
            {
                return new InstallationProgress
                {
                    Status = GetStatusMessage(_currentState),
                    Percentage = _lastPercentage,
                    IsIndeterminate = false,
                };
            }

            // For other lines, just return the current state
            return new InstallationProgress
            {
                Status = GetStatusMessage(_currentState),
                Percentage = 0,
                IsIndeterminate = true,
            };
        }

        /// <summary>
        /// Gets a status message appropriate for the current installation state.
        /// </summary>
        private string GetStatusMessage(InstallationState state)
        {
            switch (state)
            {
                case InstallationState.Starting:
                    return "Preparing for installation...";
                case InstallationState.Resolving:
                    return "Resolving package dependencies...";
                case InstallationState.Downloading:
                    return "Downloading package files. This might take a while, please wait...";
                case InstallationState.Verifying:
                    return "Verifying package integrity...";
                case InstallationState.Installing:
                    return "Installing application...";
                case InstallationState.Configuring:
                    return "Configuring application settings...";
                case InstallationState.Completing:
                    return "Finalizing installation...";
                default:
                    return "Processing...";
            }
        }
    }

    /// <summary>
    /// Represents the different states of a WinGet installation process.
    /// </summary>
    public enum InstallationState
    {
        /// <summary>
        /// The installation process is starting.
        /// </summary>
        Starting,

        /// <summary>
        /// The package dependencies are being resolved.
        /// </summary>
        Resolving,

        /// <summary>
        /// Package files are being downloaded.
        /// </summary>
        Downloading,

        /// <summary>
        /// Package integrity is being verified.
        /// </summary>
        Verifying,

        /// <summary>
        /// The application is being installed.
        /// </summary>
        Installing,

        /// <summary>
        /// The application is being configured.
        /// </summary>
        Configuring,

        /// <summary>
        /// The installation process is completing.
        /// </summary>
        Completing,
    }
}
