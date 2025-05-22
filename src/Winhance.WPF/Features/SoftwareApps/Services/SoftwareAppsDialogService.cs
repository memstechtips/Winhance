using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Services;
using Winhance.WPF.Features.SoftwareApps.Views;

namespace Winhance.WPF.Features.SoftwareApps.Services
{
    /// <summary>
    /// Specialized dialog service for the SoftwareApps feature.
    /// This service uses the SoftwareAppsDialog for consistent UI in the SoftwareApps feature.
    /// </summary>
    public class SoftwareAppsDialogService
    {
        private readonly ILogService _logService;

        public SoftwareAppsDialogService(ILogService logService)
        {
            _logService = logService;
        }

        /// <summary>
        /// Shows a message to the user.
        /// </summary>
        /// <param name="message">The message to show.</param>
        /// <param name="title">The title of the dialog.</param>
        public Task ShowMessageAsync(string message, string title = "")
        {
            SoftwareAppsDialog.ShowInformationAsync(title, message, new[] { message }, "");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Shows a confirmation dialog to the user.
        /// </summary>
        /// <param name="message">The message to show.</param>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="okButtonText">The text for the OK button.</param>
        /// <param name="cancelButtonText">The text for the Cancel button.</param>
        /// <returns>A task that represents the asynchronous operation, with a boolean result indicating whether the user confirmed the action.</returns>
        public Task<bool> ShowConfirmationAsync(
            string message,
            string title = "",
            string okButtonText = "OK",
            string cancelButtonText = "Cancel"
        )
        {
            // Parse apps from the message if it contains a list
            if (
                message.Contains("following")
                && (message.Contains("install") || message.Contains("remove"))
            )
            {
                var lines = message.Split('\n');
                var apps = lines
                    .Skip(1) // Skip the header line
                    .Where(line =>
                        !string.IsNullOrWhiteSpace(line) && !line.Contains("Do you want to")
                    )
                    .TakeWhile(line => !line.Contains("Do you want to"))
                    .Select(line => line.Trim())
                    .ToList();

                var headerText = lines[0];
                var footerText = string.Join(
                    "\n\n",
                    lines
                        .Where(line =>
                            line.Contains("cannot be")
                            || line.Contains("action cannot")
                            || line.Contains("Some selected")
                        )
                        .Select(line => line.Trim())
                );

                var result = SoftwareAppsDialog.ShowConfirmationAsync(
                    title,
                    headerText,
                    apps,
                    footerText
                );
                return Task.FromResult(result ?? false);
            }
            else
            {
                // For simple confirmation messages
                var result = SoftwareAppsDialog.ShowConfirmationAsync(
                    title,
                    message,
                    new[] { message },
                    ""
                );
                return Task.FromResult(result ?? false);
            }
        }

        /// <summary>
        /// Shows an information dialog to the user.
        /// </summary>
        /// <param name="message">The message to show.</param>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="buttonText">The text for the button.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public Task ShowInformationAsync(
            string message,
            string title = "Information",
            string buttonText = "OK"
        )
        {
            // Parse apps from the message if it contains a list
            if (
                message.Contains("following")
                && (message.Contains("installed") || message.Contains("removed"))
            )
            {
                var lines = message.Split('\n');
                var apps = lines
                    .Where(line => line.StartsWith("+") || line.StartsWith("-"))
                    .Select(line => line.Trim())
                    .ToList();

                var headerText = lines[0];
                var footerText = string.Join(
                    "\n\n",
                    lines
                        .Where(line => line.Contains("Failed") || line.Contains("startup task"))
                        .Select(line => line.Trim())
                );

                SoftwareAppsDialog.ShowInformationAsync(title, headerText, apps, footerText);
            }
            else
            {
                // For simple information messages
                SoftwareAppsDialog.ShowInformationAsync(title, message, new[] { message }, "");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Shows an information dialog to the user with custom header, apps list, and footer.
        /// </summary>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="headerText">The header text to display.</param>
        /// <param name="apps">The collection of app names or messages to display.</param>
        /// <param name="footerText">The footer text to display.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public Task ShowInformationAsync(
            string title,
            string headerText,
            IEnumerable<string> apps,
            string footerText
        )
        {
            SoftwareAppsDialog.ShowInformationAsync(title, headerText, apps, footerText);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Shows a warning dialog to the user.
        /// </summary>
        /// <param name="message">The message to show.</param>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="buttonText">The text for the button.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public Task ShowWarningAsync(
            string message,
            string title = "Warning",
            string buttonText = "OK"
        )
        {
            SoftwareAppsDialog.ShowInformationAsync(title, message, new[] { message }, "");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Shows an input dialog to the user.
        /// </summary>
        /// <param name="message">The message to show.</param>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="defaultValue">The default value for the input.</param>
        /// <returns>A task that represents the asynchronous operation, with the user's input as the result.</returns>
        public Task<string?> ShowInputAsync(
            string message,
            string title = "",
            string defaultValue = ""
        )
        {
            // Input dialogs are not supported by SoftwareAppsDialog
            // Fallback to a confirmation dialog
            var result = SoftwareAppsDialog.ShowConfirmationAsync(
                title,
                message,
                new[] { message },
                ""
            );
            return Task.FromResult(result == true ? defaultValue : null);
        }

        /// <summary>
        /// Shows a Yes/No/Cancel dialog to the user.
        /// </summary>
        /// <param name="message">The message to show.</param>
        /// <param name="title">The title of the dialog.</param>
        /// <returns>A task that represents the asynchronous operation, with a boolean result indicating the user's choice (true for Yes, false for No, null for Cancel).</returns>
        public Task<bool?> ShowYesNoCancelAsync(string message, string title = "")
        {
            // Parse apps from the message if it contains a list
            if (
                message.Contains("following")
                && (message.Contains("install") || message.Contains("remove"))
            )
            {
                var lines = message.Split('\n');
                var apps = lines
                    .Where(line => line.StartsWith("+") || line.StartsWith("-"))
                    .Select(line => line.Trim())
                    .ToList();

                var headerText = lines[0];
                var footerText = string.Join(
                    "\n\n",
                    lines
                        .Where(line =>
                            line.Contains("cannot be")
                            || line.Contains("action cannot")
                            || line.Contains("Some selected")
                        )
                        .Select(line => line.Trim())
                );

                var result = SoftwareAppsDialog.ShowYesNoCancel(
                    title,
                    headerText,
                    apps,
                    footerText
                );
                return Task.FromResult(result);
            }
            else
            {
                // For simple messages
                var result = SoftwareAppsDialog.ShowYesNoCancel(
                    title,
                    message,
                    new[] { message },
                    ""
                );
                return Task.FromResult(result);
            }
        }

        /// <summary>
        /// Shows an operation result dialog to the user.
        /// </summary>
        /// <param name="operationType">The type of operation (e.g., "Install", "Remove").</param>
        /// <param name="successCount">The number of successful operations.</param>
        /// <param name="totalCount">The total number of operations.</param>
        /// <param name="successItems">The list of successfully processed items.</param>
        /// <param name="failedItems">The list of failed items.</param>
        /// <param name="skippedItems">The list of skipped items.</param>
        /// <param name="hasConnectivityIssues">Whether there were connectivity issues.</param>
        public void ShowOperationResult(
            string operationType,
            int successCount,
            int totalCount,
            IEnumerable<string> successItems,
            IEnumerable<string> failedItems = null,
            IEnumerable<string> skippedItems = null,
            bool hasConnectivityIssues = false,
            bool isUserCancelled = false
        )
        {
            // Get the past tense form of the operation type
            string GetPastTense(string op)
            {
                if (string.IsNullOrEmpty(op))
                    return string.Empty;

                return op.Equals("Remove", System.StringComparison.OrdinalIgnoreCase)
                    ? "removed"
                    : $"{op.ToLower()}ed";
            }

            // Check if any failures are due to internet connectivity issues or user cancellation
            bool isFailure = successCount < totalCount;

            string title;
            if (isUserCancelled)
            {
                title = "Installation Aborted";
            }
            else if (hasConnectivityIssues)
            {
                title = "Internet Connection Lost";
            }
            else
            {
                title = isFailure
                    ? $"{operationType} Operation Failed"
                    : $"{operationType} Results";
            }

            string headerText;
            if (isUserCancelled)
            {
                headerText = $"Installation aborted by user";
            }
            else if (hasConnectivityIssues)
            {
                headerText = "Installation stopped due to internet connection loss";
            }
            else
            {
                headerText =
                    successCount > 0 && successCount == totalCount
                        ? $"The following items were successfully {GetPastTense(operationType)}:"
                        : (
                            successCount > 0
                                ? $"Successfully {GetPastTense(operationType)} {successCount} of {totalCount} items."
                                : $"Unable to {operationType.ToLowerInvariant()} {totalCount} of {totalCount} items."
                        );
            }

            // Create list of items for the dialog
            var resultItems = new List<string>();

            // For connectivity issues or user cancellation, add a clear explanation
            if (isUserCancelled)
            {
                resultItems.Add("The installation process was cancelled by the user.");
                resultItems.Add("");
                if (successCount > 0)
                {
                    resultItems.Add("Successfully installed items:");
                }
            }
            else if (hasConnectivityIssues)
            {
                resultItems.Add(
                    "The installation process was stopped because the internet connection was lost."
                );
                resultItems.Add(
                    "This is required to ensure installations complete properly and prevent corrupted installations."
                );
                resultItems.Add("");
                resultItems.Add("Failed items:");
            }

            // Add successful items directly to the list
            if (successItems != null && successItems.Any())
            {
                if (!hasConnectivityIssues) // Only show success items if not a connectivity issue
                {
                    foreach (var item in successItems)
                    {
                        resultItems.Add(item);
                    }
                }
            }
            else if (!hasConnectivityIssues) // Only show this message if not a connectivity issue
            {
                resultItems.Add($"No items were {GetPastTense(operationType)}.");
            }

            // Add skipped items if any
            if (skippedItems != null && skippedItems.Any() && !hasConnectivityIssues) // Only show if not a connectivity issue
            {
                resultItems.Add($"Skipped items: {skippedItems.Count()}");
                foreach (var item in skippedItems.Take(5))
                {
                    resultItems.Add($"  - {item}");
                }
                if (skippedItems.Count() > 5)
                {
                    resultItems.Add($"  - ... and {skippedItems.Count() - 5} more");
                }
            }

            // Add failed items if any
            if (failedItems != null && failedItems.Any())
            {
                if (!hasConnectivityIssues) // Only show the header if not already shown for connectivity issues
                {
                    resultItems.Add($"Failed items: {failedItems.Count()}");
                }

                foreach (var item in failedItems.Take(5))
                {
                    resultItems.Add($"  - {item}");
                }
                if (failedItems.Count() > 5)
                {
                    resultItems.Add($"  - ... and {failedItems.Count() - 5} more");
                }
            }

            // Create footer text
            string footerText;
            if (isUserCancelled)
            {
                footerText =
                    successCount > 0
                        ? $"Some items were successfully {GetPastTense(operationType)} before cancellation."
                        : $"No items were {GetPastTense(operationType)} before cancellation.";
            }
            else if (hasConnectivityIssues)
            {
                footerText =
                    "Please check your network connection and try again when your internet connection is stable.";
            }
            else
            {
                // Check if we have any connectivity-related failures
                bool hasConnectivityFailures =
                    failedItems != null
                    && failedItems.Any(item =>
                        item.Contains("internet", StringComparison.OrdinalIgnoreCase)
                        || item.Contains("connection", StringComparison.OrdinalIgnoreCase)
                        || item.Contains("network", StringComparison.OrdinalIgnoreCase)
                        || item.Contains(
                            "pipeline has been stopped",
                            StringComparison.OrdinalIgnoreCase
                        )
                    );

                footerText =
                    successCount == totalCount
                        ? $"All items were successfully {GetPastTense(operationType)}."
                        : (
                            successCount > 0
                                ? (
                                    hasConnectivityFailures
                                        ? $"Some items could not be {GetPastTense(operationType)}. Please check your internet connection and try again."
                                        : $"Some items could not be {GetPastTense(operationType)}. Please try again later."
                                )
                                : (
                                    hasConnectivityFailures
                                        ? $"Installation failed. Please check your internet connection and try again."
                                        : $"Installation failed. Please try again later."
                                )
                        );
            }

            // Show the information dialog
            SoftwareAppsDialog.ShowInformationAsync(title, headerText, resultItems, footerText);
        }

        /// <summary>
        /// Gets the past tense form of an operation type
        /// </summary>
        /// <param name="operationType">The operation type (e.g., "Install", "Remove")</param>
        /// <returns>The past tense form of the operation type</returns>
        private string GetPastTense(string operationType)
        {
            if (string.IsNullOrEmpty(operationType))
                return string.Empty;

            return operationType.Equals("Remove", StringComparison.OrdinalIgnoreCase)
                ? "removed"
                : $"{operationType.ToLower()}ed";
        }
    }
}
