using System.Threading.Tasks;
using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Provides functionality for displaying dialog boxes to the user.
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Displays a message to the user.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="title">The title of the dialog box.</param>
        void ShowMessage(string message, string title = "");

        /// <summary>
        /// Displays a confirmation dialog.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="title">The title of the dialog box.</param>
        /// <param name="okButtonText">The text for the OK button.</param>
        /// <param name="cancelButtonText">The text for the Cancel button.</param>
        /// <returns>True if the user confirmed; otherwise, false.</returns>
        Task<bool> ShowConfirmationAsync(string message, string title = "", string okButtonText = "OK", string cancelButtonText = "Cancel");

        /// <summary>
        /// Displays an information dialog.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="title">The title of the dialog box.</param>
        /// <param name="buttonText">The text for the button.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ShowInformationAsync(string message, string title = "Information", string buttonText = "OK");

        /// <summary>
        /// Displays a warning dialog.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="title">The title of the dialog box.</param>
        /// <param name="buttonText">The text for the button.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ShowWarningAsync(string message, string title = "Warning", string buttonText = "OK");

        /// <summary>
        /// Displays an error dialog.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="title">The title of the dialog box.</param>
        /// <param name="buttonText">The text for the button.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ShowErrorAsync(string message, string title = "Error", string buttonText = "OK");

        /// <summary>
        /// Displays an input dialog.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="title">The title of the dialog box.</param>
        /// <param name="defaultValue">The default value for the input.</param>
        /// <returns>The value entered by the user, or null if the user canceled.</returns>
        Task<string?> ShowInputAsync(string message, string title = "", string defaultValue = "");

        /// <summary>
        /// Displays a Yes/No/Cancel dialog.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="title">The title of the dialog box.</param>
        /// <returns>True if the user clicked Yes, false if the user clicked No, null if the user clicked Cancel.</returns>
        Task<bool?> ShowYesNoCancelAsync(string message, string title = "");

        /// <summary>
        /// Displays a unified configuration save dialog.
        /// </summary>
        /// <param name="title">The title of the dialog box.</param>
        /// <param name="description">The description of the dialog.</param>
        /// <param name="sections">A dictionary of section names, their availability, and item counts.</param>
        /// <returns>A dictionary of section names and their final selection state, or null if the user canceled.</returns>
        Task<Dictionary<string, bool>> ShowUnifiedConfigurationSaveDialogAsync(string title, string description, Dictionary<string, (bool IsSelected, bool IsAvailable, int ItemCount)> sections);

        /// <summary>
        /// Displays a unified configuration import dialog.
        /// </summary>
        /// <param name="title">The title of the dialog box.</param>
        /// <param name="description">The description of the dialog.</param>
        /// <param name="sections">A dictionary of section names, their availability, and item counts.</param>
        /// <returns>A dictionary of section names and their final selection state, or null if the user canceled.</returns>
        Task<Dictionary<string, bool>> ShowUnifiedConfigurationImportDialogAsync(string title, string description, Dictionary<string, (bool IsSelected, bool IsAvailable, int ItemCount)> sections);

        /// <summary>
        /// Displays a donation dialog.
        /// </summary>
        /// <param name="title">The title of the dialog box.</param>
        /// <param name="supportMessage">The support message to display.</param>
        /// <param name="footerText">The footer text.</param>
        /// <returns>A task representing the asynchronous operation, with a tuple containing the dialog result (whether the user clicked Yes or No) and whether the "Don't show again" checkbox was checked.</returns>
        Task<(bool? Result, bool DontShowAgain)> ShowDonationDialogAsync(string title, string supportMessage, string footerText);

        /// <summary>
        /// Shows the configuration import options dialog.
        /// </summary>
        /// <returns>The selected import option if the user clicked OK, or null if the user canceled.</returns>
        Task<ImportOption?> ShowConfigImportOptionsDialogAsync();

        /// <summary>
        /// Displays a confirmation dialog with an optional checkbox.
        /// </summary>
        /// <param name="message">The confirmation message to display.</param>
        /// <param name="checkboxText">The text for the checkbox (null if no checkbox needed).</param>
        /// <param name="title">The title of the dialog box.</param>
        /// <param name="continueButtonText">The text for the continue button.</param>
        /// <param name="cancelButtonText">The text for the cancel button.</param>
        /// <returns>A tuple containing whether user confirmed and checkbox state (if applicable).</returns>
        Task<(bool Confirmed, bool CheckboxChecked)> ShowConfirmationWithCheckboxAsync(
            string message,
            string? checkboxText = null,
            string title = "Confirmation",
            string continueButtonText = "Continue",
            string cancelButtonText = "Cancel");

        void ShowOperationResult(
            string operationType,
            int successCount,
            int totalCount,
            IEnumerable<string> successItems,
            IEnumerable<string> failedItems = null,
            IEnumerable<string> skippedItems = null,
            bool hasConnectivityIssues = false,
            bool isUserCancelled = false);

        Task ShowInformationAsync(
            string title,
            string headerText,
            IEnumerable<string> apps,
            string footerText);

    }
}