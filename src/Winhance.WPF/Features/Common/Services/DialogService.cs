using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Views;

namespace Winhance.WPF.Features.Common.Services
{
    public class DialogService : IDialogService
    {
        public void ShowMessage(string message, string title = "")
        {
            // Use CustomDialog for all messages
            CustomDialog.ShowInformation(title, title, message, "");
        }

        public Task<bool> ShowConfirmationAsync(
            string message,
            string title = "",
            string okButtonText = "OK",
            string cancelButtonText = "Cancel"
        )
        {
            // For regular confirmation messages without app lists
            if (
                !message.Contains("following")
                || (!message.Contains("install") && !message.Contains("remove"))
            )
            {
                return Task.FromResult(
                    MessageBox.Show(
                        message,
                        title,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    ) == MessageBoxResult.Yes
                );
            }

            // Parse apps from the message
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

            var result = CustomDialog.ShowConfirmation(title, headerText, apps, footerText);
            return Task.FromResult(result ?? false);
        }

        public Task ShowErrorAsync(string message, string title = "Error", string buttonText = "OK")
        {
            // Use CustomDialog for all error messages
            CustomDialog.ShowInformation(title, title, message, "");
            return Task.CompletedTask;
        }

        public Task ShowInformationAsync(
            string message,
            string title = "Information",
            string buttonText = "OK"
        )
        {
            // For messages with app lists (special handling)
            if (
                message.Contains("following")
                && (message.Contains("installed") || message.Contains("removed"))
            )
            {
                // Parse apps from the message
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

                CustomDialog.ShowInformation(title, headerText, apps, footerText);
                return Task.CompletedTask;
            }

            // For all other information messages, use CustomDialog
            CustomDialog.ShowInformation(title, title, message, "");
            return Task.CompletedTask;
        }

        public Task ShowWarningAsync(
            string message,
            string title = "Warning",
            string buttonText = "OK"
        )
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return Task.CompletedTask;
        }

        public Task<string?> ShowInputAsync(
            string message,
            string title = "",
            string defaultValue = ""
        )
        {
            var result = MessageBox.Show(
                message,
                title,
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question
            );
            return Task.FromResult(result == MessageBoxResult.OK ? defaultValue : null);
        }

        public Task<bool?> ShowYesNoCancelAsync(string message, string title = "")
        {
            // For theme change messages (special case for "Choose Your Mode" combobox)
            if (message.Contains("theme wallpaper") || title.Contains("Theme"))
            {
                // Create a list with a single item for the message
                var messageList = new List<string> { message };

                // Use the CustomDialog.ShowConfirmation method (Yes/No only)
                var themeDialogResult = CustomDialog.ShowConfirmation(
                    title,
                    "Theme Change",
                    messageList,
                    ""
                );

                // Convert to bool? (Yes/No only, no Cancel)
                return Task.FromResult<bool?>(themeDialogResult);
            }
            // For regular messages without app lists
            else if (
                !message.Contains("following")
                || (!message.Contains("install") && !message.Contains("remove"))
            )
            {
                var result = MessageBox.Show(
                    message,
                    title,
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question
                );
                bool? boolResult = result switch
                {
                    MessageBoxResult.Yes => true,
                    MessageBoxResult.No => false,
                    _ => null,
                };
                return Task.FromResult(boolResult);
            }

            // Parse apps from the message
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

            // Use the CustomDialog.ShowYesNoCancel method
            var dialogResult = CustomDialog.ShowYesNoCancel(title, headerText, apps, footerText);
            return Task.FromResult(dialogResult);
        }

        public async Task<Dictionary<string, bool>> ShowUnifiedConfigurationSaveDialogAsync(
            string title,
            string description,
            Dictionary<string, (bool IsSelected, bool IsAvailable, int ItemCount)> sections
        )
        {
            // Create the dialog
            var dialog = new UnifiedConfigurationDialog(title, description, sections, true);

            // Only set the owner if the main window is visible
            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                try
                {
                    dialog.Owner = Application.Current.MainWindow;
                }
                catch (Exception ex)
                {
                    // Continue without setting the owner
                }
            }

            // Show the dialog
            var result = dialog.ShowDialog();

            // Return the result if the user clicked OK, otherwise return null
            if (result == true)
            {
                return dialog.GetResult();
            }

            return null;
        }

        public async Task<Dictionary<string, bool>> ShowUnifiedConfigurationImportDialogAsync(
            string title,
            string description,
            Dictionary<string, (bool IsSelected, bool IsAvailable, int ItemCount)> sections
        )
        {
            // Create the dialog
            var dialog = new UnifiedConfigurationDialog(title, description, sections, false);

            // Only set the owner if the main window is visible
            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                try
                {
                    dialog.Owner = Application.Current.MainWindow;
                }
                catch (Exception ex)
                {
                    // Continue without setting the owner
                }
            }

            // Show the dialog
            var result = dialog.ShowDialog();

            // Return the result if the user clicked OK, otherwise return null
            if (result == true)
            {
                return dialog.GetResult();
            }

            return null;
        }

        /// <summary>
        /// Displays a donation dialog.
        /// </summary>
        /// <param name="title">The title of the dialog box.</param>
        /// <param name="supportMessage">The support message to display.</param>
        /// <param name="footerText">The footer text.</param>
        /// <returns>A task representing the asynchronous operation, with a tuple containing the dialog result (whether the user clicked Yes or No) and whether the "Don't show again" checkbox was checked.</returns>
        public async Task<(bool? Result, bool DontShowAgain)> ShowDonationDialogAsync(
            string title,
            string supportMessage,
            string footerText
        )
        {
            try
            {
                // Use the DonationDialog.ShowDonationDialogAsync method
                var dialog = await DonationDialog.ShowDonationDialogAsync(
                    title,
                    supportMessage,
                    footerText
                );

                // Return the dialog result and the DontShowAgain value as a tuple
                return (dialog?.DialogResult, dialog?.DontShowAgain ?? false);
            }
            catch (Exception ex)
            {
                return (false, false);
            }
        }

        /// <summary>
        /// Shows the configuration import options dialog.
        /// </summary>
        /// <returns>The selected import option if the user clicked OK, or null if the user canceled.</returns>
        public async Task<ImportOption?> ShowConfigImportOptionsDialogAsync()
        {
            try
            {
                // Create the dialog
                var dialog = new ConfigImportOptionsDialog();

                // Only set the owner if the main window is visible
                if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
                {
                    try
                    {
                        dialog.Owner = Application.Current.MainWindow;
                    }
                    catch (Exception ex)
                    {
                        // Continue without setting the owner
                    }
                }

                // Show the dialog
                var result = dialog.ShowDialog();

                // Return the selected option if the user clicked OK, otherwise return null
                if (result == true)
                {
                    return dialog.SelectedOption;
                }

                return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
