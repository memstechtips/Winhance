using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Views;

namespace Winhance.WPF.Features.Common.Services
{
    public class DialogService : IDialogService
    {
        private readonly ILocalizationService _localization;

        public DialogService(ILocalizationService localization)
        {
            _localization = localization;
        }

        public void ShowMessage(string message, string title = "")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CustomDialog.ShowInformation(title, title, message, "");
            });
        }

        public Task<bool> ShowConfirmationAsync(
            string message,
            string title = "",
            string okButtonText = "OK",
            string cancelButtonText = "Cancel"
        )
        {
            string finalOkText = okButtonText == "OK" ? _localization.GetString("Button_OK") : okButtonText;
            string finalCancelText = cancelButtonText == "Cancel" ? _localization.GetString("Button_Cancel") : cancelButtonText;
            
            if (string.IsNullOrEmpty(title))
            {
                title = _localization.GetString("Dialog_Confirmation");
            }

            return Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var parsedContent = ParseMessageContent(message);

                if (parsedContent.IsAppList)
                {
                    var result = CustomDialog.ShowConfirmation(title, parsedContent.HeaderText, parsedContent.Apps, parsedContent.FooterText, finalOkText, finalCancelText);
                    return result ?? false;
                }
                else
                {
                    var result = CustomDialog.ShowConfirmation(title, title, message, "", finalOkText, finalCancelText);
                    return result ?? false;
                }
            }).Task;
        }

        public Task<bool> ShowAppOperationConfirmationAsync(
            string operationType,
            IEnumerable<string> itemNames,
            int count)
        {
            return Application.Current.Dispatcher.InvokeAsync(() =>
            {
                bool isInstall = operationType.Equals("install", StringComparison.OrdinalIgnoreCase);
                bool isRemove = operationType.Equals("remove", StringComparison.OrdinalIgnoreCase);

                string title = isInstall ? _localization.GetString("Dialog_ConfirmInstallation") :
                              isRemove ? _localization.GetString("Dialog_ConfirmRemoval") :
                              _localization.GetString("Dialog_ConfirmOperation", operationType);

                string titleBarIcon = isInstall ? "Download" :
                                     isRemove ? "Delete" :
                                     "Information";

                string contextMessage = isInstall ? _localization.GetString("Dialog_ItemsWillBeInstalled") :
                                       isRemove ? _localization.GetString("Dialog_ItemsWillBeRemoved") :
                                       _localization.GetString("Dialog_ItemsWillBeProcessed", operationType.ToLower());

                var dialog = CustomDialog.CreateAppOperationConfirmationDialog(
                    title,
                    contextMessage,
                    itemNames,
                    DialogType.Question,
                    titleBarIcon,
                    _localization.GetString("Button_Yes"),
                    _localization.GetString("Button_No"));

                var result = dialog.ShowDialog();
                return result ?? false;
            }).Task;
        }

        public async Task ShowErrorAsync(string message, string title = "Error", string buttonText = "OK")
        {
            title = title == "Error" ? _localization.GetString("Dialog_Error") : title;
            string finalButtonText = buttonText == "OK" ? _localization.GetString("Button_OK") : buttonText;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                CustomDialog.ShowInformation(title, title, message, "", finalButtonText);
            });
        }

        public async Task ShowInformationAsync(
            string message,
            string title = "Information",
            string buttonText = "OK"
        )
        {
            title = title == "Information" ? _localization.GetString("Dialog_Information") : title;
            string finalButtonText = buttonText == "OK" ? _localization.GetString("Button_OK") : buttonText;

            var parsedContent = ParseMessageContent(message);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (parsedContent.IsAppList)
                {
                    CustomDialog.ShowInformation(title, parsedContent.HeaderText, parsedContent.Apps, parsedContent.FooterText, finalButtonText);
                }
                else
                {
                    CustomDialog.ShowInformation(title, title, message, "", finalButtonText);
                }
            });
        }

        public Task ShowWarningAsync(
            string message,
            string title = "Warning",
            string buttonText = "OK"
        )
        {
            title = title == "Warning" ? _localization.GetString("Dialog_Warning") : title;
            // MessageBox uses system language for buttons
            
            return Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }).Task;
        }

        public Task<string?> ShowInputAsync(
            string message,
            string title = "",
            string defaultValue = ""
        )
        {
            return Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var result = MessageBox.Show(
                    message,
                    title,
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question
                );
                return result == MessageBoxResult.OK ? defaultValue : null;
            }).Task;
        }

        public Task<bool?> ShowYesNoCancelAsync(string message, string title = "")
        {
            return Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (message.Contains("theme wallpaper") || title.Contains("Theme"))
                {
                    var messageList = new List<string> { message };
                    var themeDialogResult = CustomDialog.ShowConfirmation(
                        title,
                        "Theme Change",
                        messageList,
                        "",
                        _localization.GetString("Button_Yes"),
                        _localization.GetString("Button_No")
                    );
                    return themeDialogResult;
                }

                var parsedContent = ParseMessageContent(message);

                if (parsedContent.IsAppList)
                {
                    var dialogResult = CustomDialog.ShowYesNoCancel(
                        title, 
                        parsedContent.HeaderText, 
                        parsedContent.Apps, 
                        parsedContent.FooterText,
                        _localization.GetString("Button_Yes"),
                        _localization.GetString("Button_No"),
                        _localization.GetString("Button_Cancel"));
                    return dialogResult;
                }
                else
                {
                    var dialogResult = CustomDialog.ShowYesNoCancel(
                        title, 
                        title, 
                        message, 
                        "", 
                        _localization.GetString("Button_Yes"),
                        _localization.GetString("Button_No"),
                        _localization.GetString("Button_Cancel"));
                    return dialogResult;
                }
            }).Task;
        }

        public async Task<Dictionary<string, bool>> ShowUnifiedConfigurationSaveDialogAsync(
            string title,
            string description,
            Dictionary<string, (bool IsSelected, bool IsAvailable, int ItemCount)> sections
        )
        {
            var dialog = new UnifiedConfigurationDialog(title, description, sections, true, _localization);

            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                try
                {
                    dialog.Owner = Application.Current.MainWindow;
                }
                catch (Exception ex)
                {
                }
            }

            var result = dialog.ShowDialog();

            if (result == true)
            {
                var (sectionResults, _) = dialog.GetResult();
                return sectionResults;
            }

            return null;
        }

        public async Task<(Dictionary<string, bool> sections, ImportOptions options)?> ShowUnifiedConfigurationImportDialogAsync(
            string title,
            string description,
            Dictionary<string, (bool IsSelected, bool IsAvailable, int ItemCount)> sections
        )
        {
            var dialog = new UnifiedConfigurationDialog(title, description, sections, false, _localization);

            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                try
                {
                    dialog.Owner = Application.Current.MainWindow;
                }
                catch (Exception ex)
                {
                }
            }

            var result = dialog.ShowDialog();

            if (result == true)
            {
                return dialog.GetResult();
            }

            return null;
        }

        public async Task<(bool? Result, bool DontShowAgain)> ShowDonationDialogAsync(
            string title = null,
            string supportMessage = null
        )
        {
            try
            {
                var dialog = await DonationDialog.ShowDonationDialogAsync(title, supportMessage);
                return (dialog?.DialogResult, dialog?.DontShowAgain ?? false);
            }
            catch (Exception ex)
            {
                return (false, false);
            }
        }

        public async Task<ImportOption?> ShowConfigImportOptionsDialogAsync()
        {
            try
            {
                var dialog = new ConfigImportOptionsDialog();

                if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
                {
                    try
                    {
                        dialog.Owner = Application.Current.MainWindow;
                    }
                    catch (Exception ex)
                    {
                    }
                }

                var result = dialog.ShowDialog();

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

        public async Task<(bool Confirmed, bool CheckboxChecked)> ShowConfirmationWithCheckboxAsync(
            string message,
            string? checkboxText = null,
            string title = "Confirmation",
            string continueButtonText = "Continue",
            string cancelButtonText = "Cancel",
            string? titleBarIcon = null)
        {
            // Localize title if default
            if (title == "Confirmation") title = _localization.GetString("Dialog_Confirmation");
            
            // Localize buttons if default or keys
            string finalContinueText = continueButtonText == "Continue" ? _localization.GetString("Button_Continue") : continueButtonText;
            string finalCancelText = cancelButtonText == "Cancel" ? _localization.GetString("Button_Cancel") : cancelButtonText;

            try
            {
                var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    return CustomDialog.ShowConfirmationWithCheckbox(
                        title,
                        message,
                        checkboxText,
                        finalContinueText,
                        finalCancelText,
                        titleBarIcon
                    );
                });

                return result ?? (false, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dialog error: {ex.Message}");
                return (false, false);
            }
        }

        public async Task<ConfirmationResponse> ShowConfirmationAsync(
            ConfirmationRequest confirmationRequest,
            string continueButtonText = "Continue",
            string cancelButtonText = "Cancel")
        {
            try
            {
                var (confirmed, checkboxChecked) = await ShowConfirmationWithCheckboxAsync(
                    confirmationRequest.Message,
                    confirmationRequest.CheckboxText,
                    confirmationRequest.Title,
                    continueButtonText,
                    cancelButtonText,
                    null
                );

                return new ConfirmationResponse
                {
                    Confirmed = confirmed,
                    CheckboxChecked = checkboxChecked,
                    Context = confirmationRequest.Context
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dialog error: {ex.Message}");
                return new ConfirmationResponse
                {
                    Confirmed = false,
                    CheckboxChecked = false
                };
            }
        }

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
            Application.Current.Dispatcher.Invoke(() =>
            {
                string GetPastTense(string op)
                {
                    if (string.IsNullOrEmpty(op))
                        return string.Empty;

                    return op.Equals("Remove", StringComparison.OrdinalIgnoreCase)
                        ? "removed"
                        : $"{op.ToLower()}ed";
                }

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

                var resultItems = new List<string>();

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

                if (successItems != null && successItems.Any())
                {
                    if (!hasConnectivityIssues)
                    {
                        foreach (var item in successItems)
                        {
                            resultItems.Add(item);
                        }
                    }
                }
                else if (!hasConnectivityIssues)
                {
                    resultItems.Add($"No items were {GetPastTense(operationType)}.");
                }

                if (skippedItems != null && skippedItems.Any() && !hasConnectivityIssues)
                {
                    resultItems.Add($"Skipped items: {skippedItems.Count()}");
                    foreach (var item in skippedItems)
                    {
                        resultItems.Add($"  - {item}");
                    }
                }

                if (failedItems != null && failedItems.Any())
                {
                    if (!hasConnectivityIssues)
                    {
                        resultItems.Add($"Failed items: {failedItems.Count()}");
                    }

                    foreach (var item in failedItems)
                    {
                        resultItems.Add($"  - {item}");
                    }
                }

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

                CustomDialog.ShowInformation(title, headerText, resultItems, footerText);
            });
        }

        public Task ShowInformationAsync(
            string title,
            string headerText,
            IEnumerable<string> apps,
            string footerText
        )
        {
            return Application.Current.Dispatcher.InvokeAsync(() =>
            {
                CustomDialog.ShowInformation(title, headerText, apps, footerText);
            }).Task;
        }

        #region Localized Dialog Helpers

        /// <summary>
        /// Shows a localized information dialog using resource keys.
        /// </summary>
        /// <param name="titleKey">Resource key for the dialog title</param>
        /// <param name="messageKey">Resource key for the dialog message</param>
        /// <param name="args">Optional format arguments for the message</param>
        public void ShowLocalizedInformation(string titleKey, string messageKey, params object[] args)
        {
            var title = _localization.GetString(titleKey);
            var message = _localization.GetString(messageKey, args);
            var buttonText = _localization.GetString("Button_OK");

            Application.Current.Dispatcher.Invoke(() =>
            {
                CustomDialog.ShowInformation(title, title, message, "", buttonText);
            });
        }

        /// <summary>
        /// Shows a localized confirmation dialog using resource keys.
        /// </summary>
        /// <param name="titleKey">Resource key for the dialog title</param>
        /// <param name="messageKey">Resource key for the dialog message</param>
        /// <param name="args">Optional format arguments for the message</param>
        /// <returns>True if user confirmed, false otherwise</returns>
        public bool ShowLocalizedConfirmation(string titleKey, string messageKey, params object[] args)
        {
            var title = _localization.GetString(titleKey);
            var message = _localization.GetString(messageKey, args);
            var yesText = _localization.GetString("Button_Yes");
            var noText = _localization.GetString("Button_No");

            return Application.Current.Dispatcher.Invoke(() =>
            {
                var result = CustomDialog.ShowConfirmation(title, title, message, "", yesText, noText);
                return result ?? false;
            });
        }

        /// <summary>
        /// Shows a localized confirmation dialog with separate header and message keys.
        /// </summary>
        /// <param name="titleKey">Resource key for the dialog title</param>
        /// <param name="headerKey">Resource key for the dialog header</param>
        /// <param name="messageKey">Resource key for the dialog message</param>
        /// <param name="args">Optional format arguments for the message</param>
        /// <returns>True if user confirmed, false otherwise</returns>
        public bool ShowLocalizedConfirmation(string titleKey, string headerKey, string messageKey, params object[] args)
        {
            var title = _localization.GetString(titleKey);
            var header = _localization.GetString(headerKey);
            var message = _localization.GetString(messageKey, args);
            var yesText = _localization.GetString("Button_Yes");
            var noText = _localization.GetString("Button_No");

            return Application.Current.Dispatcher.Invoke(() =>
            {
                var result = CustomDialog.ShowConfirmation(title, header, message, "", yesText, noText);
                return result ?? false;
            });
        }

        /// <summary>
        /// Shows a localized error dialog using resource keys.
        /// </summary>
        /// <param name="titleKey">Resource key for the dialog title</param>
        /// <param name="messageKey">Resource key for the dialog message</param>
        /// <param name="args">Optional format arguments for the message</param>
        public void ShowLocalizedError(string titleKey, string messageKey, params object[] args)
        {
            var title = _localization.GetString(titleKey);
            var message = _localization.GetString(messageKey, args);
            var buttonText = _localization.GetString("Button_OK");

            Application.Current.Dispatcher.Invoke(() =>
            {
                CustomDialog.ShowInformation(title, title, message, "", buttonText);
            });
        }

        /// <summary>
        /// Shows a localized warning dialog using resource keys.
        /// </summary>
        /// <param name="titleKey">Resource key for the dialog title</param>
        /// <param name="messageKey">Resource key for the dialog message</param>
        /// <param name="args">Optional format arguments for the message</param>
        public void ShowLocalizedWarning(string titleKey, string messageKey, params object[] args)
        {
            var title = _localization.GetString(titleKey);
            var message = _localization.GetString(messageKey, args);

            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        public void ShowLocalizedDialog(string titleKey, string messageKey, DialogType dialogType, string icon, params object[] args)
        {
            var title = _localization.GetString(titleKey);
            var message = _localization.GetString(messageKey, args);
            var okButton = _localization.GetString("Dialog_Button_OK");

            Application.Current.Dispatcher.Invoke(() =>
            {
                var dialog = CustomDialog.CreateInformationDialog(title, message, "", dialogType, icon, okButton);
                dialog.ShowDialog();
            });
        }

        public bool ShowLocalizedConfirmationDialog(string titleKey, string messageKey, DialogType dialogType, string icon, params object[] args)
        {
            var title = _localization.GetString(titleKey);
            var message = _localization.GetString(messageKey, args);
            var yesButton = _localization.GetString("Dialog_Button_Yes");
            var noButton = _localization.GetString("Dialog_Button_No");

            return Application.Current.Dispatcher.Invoke(() =>
            {
                var dialog = CustomDialog.CreateConfirmationDialog(title, message, "", dialogType, icon, yesButton, noButton);
                var result = dialog.ShowDialog();
                return result ?? false;
            });
        }

        #endregion

        private MessageContent ParseMessageContent(string message)
        {
            if (message.Contains("following") &&
                (message.Contains("install") || message.Contains("remove")))
            {
                return ParseAppListMessage(message,
                    lines => lines
                        .Skip(1)
                        .Where(line => !string.IsNullOrWhiteSpace(line) && !line.Contains("Do you want to"))
                        .TakeWhile(line => !line.Contains("Do you want to"))
                        .Select(line => line.Trim()),
                    lines => lines
                        .Where(line => line.Contains("cannot be") || line.Contains("action cannot") || line.Contains("Some selected"))
                        .Select(line => line.Trim())
                );
            }

            if (message.Contains("following") &&
                (message.Contains("installed") || message.Contains("removed")))
            {
                return ParseAppListMessage(message,
                    lines => lines
                        .Where(line => line.StartsWith("+") || line.StartsWith("-"))
                        .Select(line => line.Trim()),
                    lines => lines
                        .Where(line => line.Contains("Failed") || line.Contains("startup task"))
                        .Select(line => line.Trim())
                );
            }

            return new MessageContent { IsAppList = false };
        }

        private MessageContent ParseAppListMessage(string message,
            Func<string[], IEnumerable<string>> appSelector,
            Func<string[], IEnumerable<string>> footerSelector)
        {
            var lines = message.Split('\n');
            return new MessageContent
            {
                IsAppList = true,
                HeaderText = lines[0],
                Apps = appSelector(lines),
                FooterText = string.Join("\n\n", footerSelector(lines))
            };
        }
    }

    public class MessageContent
    {
        public bool IsAppList { get; set; }
        public string HeaderText { get; set; } = "";
        public IEnumerable<string> Apps { get; set; } = [];
        public string FooterText { get; set; } = "";
    }
}