using System;
using Winhance.Core.Features.SoftwareApps.Enums;

namespace Winhance.Core.Features.SoftwareApps.Helpers
{
    /// <summary>
    /// Helper class for installation error handling.
    /// </summary>
    public static class InstallationErrorHelper
    {
        /// <summary>
        /// Gets a user-friendly error message based on the error type.
        /// </summary>
        /// <param name="errorType">The type of installation error.</param>
        /// <returns>A user-friendly error message.</returns>
        public static string GetUserFriendlyErrorMessage(InstallationErrorType errorType)
        {
            return errorType switch
            {
                InstallationErrorType.NetworkError => 
                    "Network connection error. Please check your internet connection and try again.",
                
                InstallationErrorType.PermissionError => 
                    "Permission denied. Please run the application with administrator privileges.",
                
                InstallationErrorType.PackageNotFoundError => 
                    "Package not found. The requested package may not be available in the repositories.",
                
                InstallationErrorType.WinGetNotInstalledError => 
                    "WinGet is not installed and could not be installed automatically.",
                
                InstallationErrorType.AlreadyInstalledError => 
                    "The package is already installed.",
                
                InstallationErrorType.CancelledByUserError => 
                    "The installation was cancelled by the user.",
                
                InstallationErrorType.SystemStateError => 
                    "The system is in a state that prevents installation. Please restart your computer and try again.",
                
                InstallationErrorType.PackageCorruptedError => 
                    "The package is corrupted or invalid. Please try reinstalling or contact the package maintainer.",
                
                InstallationErrorType.DependencyResolutionError => 
                    "The package dependencies could not be resolved. Some required components may be missing.",
                
                InstallationErrorType.UnknownError or _ => 
                    "An unknown error occurred during installation. Please check the logs for more details."
            };
        }

        /// <summary>
        /// Determines the error type based on the exception message.
        /// </summary>
        /// <param name="exceptionMessage">The exception message.</param>
        /// <returns>The determined error type.</returns>
        public static InstallationErrorType DetermineErrorType(string exceptionMessage)
        {
            if (string.IsNullOrEmpty(exceptionMessage))
                return InstallationErrorType.UnknownError;

            exceptionMessage = exceptionMessage.ToLowerInvariant();

            if (exceptionMessage.Contains("network") || 
                exceptionMessage.Contains("connection") || 
                exceptionMessage.Contains("internet") ||
                exceptionMessage.Contains("timeout") ||
                exceptionMessage.Contains("unreachable"))
            {
                return InstallationErrorType.NetworkError;
            }
            
            if (exceptionMessage.Contains("permission") || 
                exceptionMessage.Contains("access") || 
                exceptionMessage.Contains("denied") ||
                exceptionMessage.Contains("administrator") ||
                exceptionMessage.Contains("elevation"))
            {
                return InstallationErrorType.PermissionError;
            }
            
            if (exceptionMessage.Contains("not found") || 
                exceptionMessage.Contains("no package") ||
                exceptionMessage.Contains("no such package") ||
                exceptionMessage.Contains("could not find"))
            {
                return InstallationErrorType.PackageNotFoundError;
            }
            
            if (exceptionMessage.Contains("winget") && 
                (exceptionMessage.Contains("not installed") || 
                 exceptionMessage.Contains("could not be installed")))
            {
                return InstallationErrorType.WinGetNotInstalledError;
            }
            
            if (exceptionMessage.Contains("already installed") ||
                exceptionMessage.Contains("is installed"))
            {
                return InstallationErrorType.AlreadyInstalledError;
            }
            
            if (exceptionMessage.Contains("cancelled") || 
                exceptionMessage.Contains("canceled") ||
                exceptionMessage.Contains("aborted"))
            {
                return InstallationErrorType.CancelledByUserError;
            }
            
            if (exceptionMessage.Contains("system state") || 
                exceptionMessage.Contains("restart") ||
                exceptionMessage.Contains("reboot"))
            {
                return InstallationErrorType.SystemStateError;
            }
            
            if (exceptionMessage.Contains("corrupt") || 
                exceptionMessage.Contains("invalid") ||
                exceptionMessage.Contains("damaged"))
            {
                return InstallationErrorType.PackageCorruptedError;
            }
            
            if (exceptionMessage.Contains("dependency") || 
                exceptionMessage.Contains("dependencies") ||
                exceptionMessage.Contains("requires"))
            {
                return InstallationErrorType.DependencyResolutionError;
            }
            
            return InstallationErrorType.UnknownError;
        }
    }
}