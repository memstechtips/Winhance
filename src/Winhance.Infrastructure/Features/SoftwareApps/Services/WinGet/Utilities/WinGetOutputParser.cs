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

            // Check for network-related errors first
            if (IsNetworkRelatedError(trimmedLine))
            {
                return new InstallationProgress
                {
                    Status = $"Network issue detected while processing {_appName}",
                    LastLine = trimmedLine,
                    IsActive = true,
                    IsError = true,
                    IsConnectivityIssue = true
                };
            }

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

            // Check for specific installation phases in the last line
            if (!string.IsNullOrEmpty(_lastLine))
            {
                var lowerLine = _lastLine.ToLowerInvariant();
                
                if (lowerLine.Contains("downloading") || lowerLine.Contains("download"))
                    return $"Downloading {_appName}...";
                if (lowerLine.Contains("extracting") || lowerLine.Contains("extract"))
                    return $"Extracting {_appName}...";
                if (lowerLine.Contains("installing") || lowerLine.Contains("install"))
                    return $"Installing {_appName}...";
                if (lowerLine.Contains("configuring") || lowerLine.Contains("configure"))
                    return $"Configuring {_appName}...";
                if (lowerLine.Contains("verifying") || lowerLine.Contains("verify"))
                    return $"Verifying {_appName} installation...";
                if (lowerLine.Contains("finalizing") || lowerLine.Contains("finalize"))
                    return $"Finalizing {_appName} installation...";
            }

            return isUninstall ? $"Uninstalling {_appName}..." : $"Installing {_appName}...";
        }

        private bool ContainsProgressBar(string line)
        {
            // Check if line contains the corrupted progress bar characters or percentage
            return line.Contains("â–") || // Contains any corrupted block character
                   (line.Contains("%") && line.Length > 10); // Contains percentage and looks like progress
        }

        private bool IsNetworkRelatedError(string line)
        {
            if (string.IsNullOrEmpty(line))
                return false;

            var lowerLine = line.ToLowerInvariant();
            return lowerLine.Contains("network") ||
                   lowerLine.Contains("timeout") ||
                   lowerLine.Contains("connection") ||
                   lowerLine.Contains("dns") ||
                   lowerLine.Contains("resolve") ||
                   lowerLine.Contains("unreachable") ||
                   lowerLine.Contains("offline") ||
                   lowerLine.Contains("proxy") ||
                   lowerLine.Contains("certificate") ||
                   lowerLine.Contains("ssl") ||
                   lowerLine.Contains("tls") ||
                   lowerLine.Contains("download failed") ||
                   lowerLine.Contains("no internet") ||
                   lowerLine.Contains("connectivity") ||
                   lowerLine.Contains("could not download") ||
                   lowerLine.Contains("failed to download") ||
                   lowerLine.Contains("unable to connect") ||
                   lowerLine.Contains("connection refused") ||
                   lowerLine.Contains("host not found") ||
                   lowerLine.Contains("name resolution failed");
        }

        public void Clear()
        {
            _lastLine = "";
        }
    }
}