using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Views;
using Winhance.WPF.Features.SoftwareApps.Services;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels
{
    /// <summary>
    /// Base view model for installation operations.
    /// Provides common functionality for both WindowsAppsViewModel and ExternalAppsViewModel.
    /// </summary>
    /// <typeparam name="T">The type of app (WindowsApp or ExternalApp)</typeparam>
    public abstract class BaseInstallationViewModel<T> : SearchableViewModel<T>
        where T : class, ISearchable
    {
        protected readonly IAppInstallationService _appInstallationService;
        protected readonly IAppInstallationCoordinatorService _appInstallationCoordinatorService;
        protected readonly IInternetConnectivityService _connectivityService;
        protected readonly SoftwareAppsDialogService _dialogService;

        /// <summary>
        /// Gets or sets the reason for cancellation if an operation was cancelled.
        /// </summary>
        protected CancellationReason CurrentCancellationReason { get; set; } =
            CancellationReason.None;

        /// <summary>
        /// Gets or sets the status text.
        /// </summary>
        public string StatusText { get; set; } = "Ready";

        /// <summary>
        /// Gets or sets a value indicating whether the view model is initialized.
        /// </summary>
        public bool IsInitialized { get; set; } = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseInstallationViewModel{T}"/> class.
        /// </summary>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="searchService">The search service.</param>
        /// <param name="packageManager">The package manager.</param>
        /// <param name="appInstallationService">The app installation service.</param>
        /// <param name="appInstallationCoordinatorService">The app installation coordinator service.</param>
        /// <param name="connectivityService">The internet connectivity service.</param>
        /// <param name="dialogService">The specialized dialog service for software apps.</param>
        protected BaseInstallationViewModel(
            ITaskProgressService progressService,
            ISearchService searchService,
            IPackageManager packageManager,
            IAppInstallationService appInstallationService,
            IAppInstallationCoordinatorService appInstallationCoordinatorService,
            IInternetConnectivityService connectivityService,
            SoftwareAppsDialogService dialogService
        )
            : base(progressService, searchService, packageManager)
        {
            _appInstallationService =
                appInstallationService
                ?? throw new ArgumentNullException(nameof(appInstallationService));
            _appInstallationCoordinatorService =
                appInstallationCoordinatorService
                ?? throw new ArgumentNullException(nameof(appInstallationCoordinatorService));
            _connectivityService =
                connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
            _dialogService =
                dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        }

        /// <summary>
        /// Loads apps and checks their installation status.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task LoadAppsAndCheckInstallationStatusAsync()
        {
            if (IsInitialized)
            {
                return;
            }

            await LoadItemsAsync();
            await CheckInstallationStatusAsync();

            // Mark as initialized after loading is complete
            IsInitialized = true;
        }

        /// <summary>
        /// Checks if internet connectivity is available.
        /// </summary>
        /// <param name="showDialog">Whether to show a dialog if connectivity is not available.</param>
        /// <returns>True if internet is connected, false otherwise.</returns>
        protected async Task<bool> CheckInternetConnectivityAsync(bool showDialog = true)
        {
            bool isInternetConnected = await _connectivityService.IsInternetConnectedAsync(true);
            if (!isInternetConnected && showDialog)
            {
                StatusText = "No internet connection available. Installation cannot proceed.";

                // Show dialog informing the user about the connectivity issue
                await ShowNoInternetConnectionDialogAsync();
            }
            return isInternetConnected;
        }

        /// <summary>
        /// Shows a confirmation dialog for an operation.
        /// </summary>
        /// <param name="operationType">Type of operation (Install/Remove)</param>
        /// <param name="selectedApps">List of apps selected for the operation</param>
        /// <param name="skippedApps">List of apps that will be skipped (optional)</param>
        /// <returns>Dialog result (true if confirmed, false if canceled)</returns>
        protected async Task<bool> ShowOperationConfirmationDialogAsync(
            string operationType,
            IEnumerable<T> selectedApps,
            IEnumerable<T>? skippedApps = null
        )
        {
            string title = $"Confirm {operationType}";
            string headerText = $"The following items will be {GetPastTense(operationType)}:";

            // Create list of app names for the dialog
            var appNames = selectedApps.Select(a => GetAppName(a)).ToList();

            // Create footer text
            string footerText = "Do you want to continue?";

            // If there are skipped apps, add information about them
            if (skippedApps != null && skippedApps.Any())
            {
                var skippedNames = skippedApps.Select(a => GetAppName(a)).ToList();
                footerText =
                    $"Note: The following {skippedApps.Count()} item(s) cannot be {GetPastTense(operationType)} and will be skipped:\n";
                footerText += string.Join(", ", skippedNames);
                footerText +=
                    $"\n\nDo you want to continue with the remaining {selectedApps.Count()} item(s)?";
            }

            // Build the message
            string message = $"{headerText}\n";
            foreach (var name in appNames)
            {
                message += $"{name}\n";
            }
            message += $"\n{footerText}";

            // Show the confirmation dialog
            return await _dialogService.ShowConfirmationAsync(message, title);
        }

        /// <summary>
        /// Centralized method to handle the entire cancellation process.
        /// This method manages the cancellation state, logs the cancellation event,
        /// shows the appropriate dialog, and ensures proper cleanup.
        /// </summary>
        /// <param name="isConnectivityIssue">True if the cancellation was due to connectivity issues, false if user-initiated.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        protected async Task HandleCancellationAsync(bool isConnectivityIssue)
        {
            // Set the appropriate cancellation reason (state management)
            CurrentCancellationReason = isConnectivityIssue
                ? CancellationReason.InternetConnectivityLost
                : CancellationReason.UserCancelled;

            // Show the appropriate dialog (UI presentation - delegated to specialized method)
            await ShowCancellationDialogAsync(!isConnectivityIssue, isConnectivityIssue);

            // Reset cancellation reason after showing dialog (cleanup)
            CurrentCancellationReason = CancellationReason.None;
        }

        /// <summary>
        /// Shows an operation result dialog after operations complete.
        /// </summary>
        /// <param name="operationType">Type of operation (Install/Remove)</param>
        /// <param name="successCount">Number of successful operations</param>
        /// <param name="totalCount">Total number of operations attempted</param>
        /// <param name="successItems">List of successfully processed items</param>
        /// <param name="failedItems">List of failed items (optional)</param>
        /// <param name="skippedItems">List of skipped items (optional)</param>
        protected void ShowOperationResultDialog(
            string operationType,
            int successCount,
            int totalCount,
            IEnumerable<string> successItems,
            IEnumerable<string>? failedItems = null,
            IEnumerable<string>? skippedItems = null
        )
        {
            // Determine if this was a user-initiated cancellation or connectivity issue
            bool isUserCancelled = CurrentCancellationReason == CancellationReason.UserCancelled;
            bool isConnectivityIssue =
                CurrentCancellationReason == CancellationReason.InternetConnectivityLost;

            // If the operation was cancelled by the user, use CustomDialog for a simpler message
            if (isUserCancelled)
            {
                string title = "Installation Aborted by User";
                string headerText = "Installation aborted by user";
                string message = "The installation process was cancelled by the user.";
                string footerText =
                    successCount > 0
                        ? $"Some items were successfully {GetPastTense(operationType)} before cancellation."
                        : $"No items were {GetPastTense(operationType)} before cancellation.";

                // Use CustomDialog directly instead of SoftwareAppsDialog
                CustomDialog.ShowInformation(title, headerText, message, footerText);

                // Reset cancellation reason after showing dialog
                CurrentCancellationReason = CancellationReason.None;
                return;
            }
            // If the operation was cancelled due to connectivity issues
            else if (isConnectivityIssue)
            {
                // Use the dialog service with the connectivity issue flag
                _dialogService.ShowOperationResult(
                    operationType,
                    successCount,
                    totalCount,
                    successItems,
                    failedItems,
                    skippedItems,
                    true, // Connectivity issue flag
                    false // Not a user cancellation
                );

                // Reset cancellation reason after showing dialog
                CurrentCancellationReason = CancellationReason.None;
                return;
            }

            // For normal operation results (no cancellation)
            // Check if any failures are due to internet connectivity issues
            bool hasConnectivityIssues = false;
            if (failedItems != null)
            {
                hasConnectivityIssues = failedItems.Any(item =>
                    item.Contains("internet", StringComparison.OrdinalIgnoreCase)
                    || item.Contains("connection", StringComparison.OrdinalIgnoreCase)
                    || item.Contains("network", StringComparison.OrdinalIgnoreCase)
                    || item.Contains(
                        "pipeline has been stopped",
                        StringComparison.OrdinalIgnoreCase
                    )
                );
            }

            // For normal operation results, use the dialog service
            _dialogService.ShowOperationResult(
                operationType,
                successCount,
                totalCount,
                successItems,
                failedItems,
                skippedItems,
                hasConnectivityIssues,
                false // Not a user cancellation
            );
        }

        /// <summary>
        /// Shows a dialog informing the user that no internet connection is available.
        /// </summary>
        protected Task ShowNoInternetConnectionDialogAsync()
        {
            // Use CustomDialog directly instead of SoftwareAppsDialog
            CustomDialog.ShowInformation(
                "Internet Connection Required",
                "No internet connection available",
                "Internet connection is required to install apps.",
                "Please check your network connection and try again."
            );
            return Task.CompletedTask;
        }

        /// <summary>
        /// Shows a dialog informing the user that no items were selected for an operation.
        /// </summary>
        /// <param name="action">The action being performed (e.g., "installation", "removal")</param>
        protected Task ShowNoItemsSelectedDialogAsync(string action)
        {
            return _dialogService.ShowInformationAsync(
                $"No items were selected for {action}.",
                $"No Items Selected",
                $"Check the boxes next to the items you want to {action} and try again."
            );
        }

        /// <summary>
        /// Shows a confirmation dialog for an operation on multiple items.
        /// </summary>
        /// <param name="action">The action being performed (e.g., "install", "remove")</param>
        /// <param name="itemNames">The names of the items</param>
        /// <param name="count">The number of items</param>
        /// <returns>True if confirmed, false otherwise</returns>
        protected Task<bool> ShowConfirmItemsDialogAsync(
            string action,
            IEnumerable<string> itemNames,
            int count
        )
        {
            var formattedItemNames = itemNames.Select(name => $" {name}");

            return _dialogService.ShowConfirmationAsync(
                $"The following items will be {action}ed:\n"
                    + string.Join("\n", formattedItemNames)
                    + $"\n\nDo you want to {action} {count} item(s)?",
                $"Confirm {CapitalizeFirstLetter(action)}"
            );
        }

        /// <summary>
        /// Shows a dialog informing the user that items cannot be reinstalled.
        /// </summary>
        /// <param name="itemNames">The names of the items that cannot be reinstalled</param>
        /// <param name="isSingle">Whether this is for a single item or multiple items</param>
        protected Task ShowCannotReinstallDialogAsync(IEnumerable<string> itemNames, bool isSingle)
        {
            string title = isSingle ? "Cannot Install Item" : "Cannot Install Items";
            string message = isSingle
                ? $"{itemNames.First()} cannot be reinstalled."
                : "None of the selected items can be reinstalled.";

            return _dialogService.ShowInformationAsync(
                message,
                title,
                "These items are already installed and cannot be reinstalled."
            );
        }

        /// <summary>
        /// Shows a dialog informing the user about the operation results.
        /// </summary>
        /// <param name="action">The action that was performed (e.g., "install", "remove")</param>
        /// <param name="successCount">The number of successful operations</param>
        /// <param name="totalCount">The total number of operations attempted</param>
        /// <param name="successItems">The names of successfully processed items</param>
        /// <param name="failedItems">The names of failed items (optional)</param>
        protected Task ShowOperationResultDialogAsync(
            string action,
            int successCount,
            int totalCount,
            IEnumerable<string> successItems,
            IEnumerable<string>? failedItems = null
        )
        {
            string title = $"{CapitalizeFirstLetter(action)} Results";
            string message = $"{successCount} of {totalCount} items were successfully {action}ed.";

            return _dialogService.ShowInformationAsync(
                message,
                title,
                $"The operation completed with {successCount} successes and {totalCount - successCount} failures."
            );
        }

        /// <summary>
        /// Shows a dialog informing the user about installation cancellation.
        /// Uses CustomDialog directly to ensure proper text formatting for long messages.
        /// </summary>
        /// <param name="isUserCancelled">True if the cancellation was initiated by the user, false otherwise.</param>
        /// <param name="isConnectivityIssue">True if the cancellation was due to connectivity issues, false otherwise.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        protected Task ShowCancellationDialogAsync(bool isUserCancelled, bool isConnectivityIssue)
        {
            string title = isUserCancelled ? "Installation Aborted" : "Internet Connection Lost";

            string headerText = isUserCancelled
                ? "Installation aborted by user"
                : "Installation stopped due to internet connection loss";

            string message = isUserCancelled
                ? "The installation process was cancelled by the user."
                : "The installation process was stopped because the internet connection was lost.\nThis is required to ensure installations complete properly and prevent corrupted installations.";

            string footerText = isUserCancelled
                ? "You can restart the installation when you're ready."
                : "Please check your network connection and try again when your internet connection is stable.";

            // Use CustomDialog directly instead of SoftwareAppsDialog
            CustomDialog.ShowInformation(title, headerText, message, footerText);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the past tense form of an operation type
        /// </summary>
        /// <param name="operationType">The operation type (e.g., "Install", "Remove")</param>
        /// <returns>The past tense form of the operation type</returns>
        protected string GetPastTense(string operationType)
        {
            if (string.IsNullOrEmpty(operationType))
                return string.Empty;

            return operationType.Equals("Remove", StringComparison.OrdinalIgnoreCase)
                ? "removed"
                : $"{operationType.ToLower()}ed";
        }

        /// <summary>
        /// Gets the name of an app.
        /// </summary>
        /// <param name="app">The app.</param>
        /// <returns>The name of the app.</returns>
        protected abstract string GetAppName(T app);

        /// <summary>
        /// Converts an app to an AppInfo object.
        /// </summary>
        /// <param name="app">The app to convert.</param>
        /// <returns>The AppInfo object.</returns>
        protected abstract AppInfo ToAppInfo(T app);

        /// <summary>
        /// Gets the selected apps.
        /// </summary>
        /// <returns>The selected apps.</returns>
        protected abstract IEnumerable<T> GetSelectedApps();

        /// <summary>
        /// Sets the installation status of an app.
        /// </summary>
        /// <param name="app">The app.</param>
        /// <param name="isInstalled">Whether the app is installed.</param>
        protected abstract void SetInstallationStatus(T app, bool isInstalled);

        /// <summary>
        /// Capitalizes the first letter of a string.
        /// </summary>
        /// <param name="input">The input string</param>
        /// <returns>The string with the first letter capitalized</returns>
        protected string CapitalizeFirstLetter(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return char.ToUpper(input[0]) + input.Substring(1);
        }
    }
}
