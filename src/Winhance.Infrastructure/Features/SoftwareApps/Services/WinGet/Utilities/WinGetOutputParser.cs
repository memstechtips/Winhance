using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities
{
    public class WinGetOutputParser
    {
        private string _lastLine = "";
        private readonly string _appName;

        public WinGetOutputParser(string appName = null)
        {
            _appName = appName;
        }

        public InstallationProgress ParseOutputLine(string outputLine)
        {
            if (string.IsNullOrWhiteSpace(outputLine))
                return null;

            string trimmedLine = outputLine.Trim();

            // If line contains progress bar characters, don't show the raw output
            if (ContainsProgressBar(trimmedLine))
            {
                _lastLine = ""; // Hide the corrupted progress bar
            }
            else
            {
                _lastLine = trimmedLine;
            }

            // Check for completion
            if (outputLine.Contains("Successfully installed") ||
                outputLine.Contains("Successfully uninstalled") ||
                outputLine.Contains("completed successfully") ||
                outputLine.Contains("installation complete") ||
                outputLine.Contains("uninstallation complete"))
            {
                return new InstallationProgress
                {
                    Status = outputLine.Contains("uninstall") ? "Uninstallation completed successfully!" : "Installation completed successfully!",
                    LastLine = _lastLine,
                    IsActive = false
                };
            }

            // Check if this is an uninstall operation
            bool isUninstall = outputLine.Contains("uninstall") || outputLine.Contains("Uninstall") ||
                              _lastLine.Contains("uninstall") || _lastLine.Contains("Uninstall");

            return new InstallationProgress
            {
                Status = GetStatusMessage(isUninstall),
                LastLine = _lastLine,
                IsActive = true
            };
        }

        private string GetStatusMessage(bool isUninstall)
        {
            if (string.IsNullOrEmpty(_appName))
            {
                return isUninstall ? "Uninstalling..." : "Installing...";
            }

            return isUninstall ? $"Uninstalling {_appName}..." : $"Installing {_appName}...";
        }

        private bool ContainsProgressBar(string line)
        {
            // Check if line contains the corrupted progress bar characters or percentage
            return line.Contains("â–") || // Contains any corrupted block character
                   (line.Contains("%") && line.Length > 10); // Contains percentage and looks like progress
        }

        public void Clear()
        {
            _lastLine = "";
        }
    }
}