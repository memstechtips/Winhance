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
        public void ShowMessage(string message, string title = "")
        {
            CustomDialog.ShowInformation(title, title, message, "");
        }

        public Task<bool> ShowConfirmationAsync(
            string message,
            string title = "",
            string okButtonText = "OK",
            string cancelButtonText = "Cancel"
        )
        {
            var parsedContent = ParseMessageContent(message);

            if (parsedContent.IsAppList)
            {
                var result = CustomDialog.ShowConfirmation(title, parsedContent.HeaderText, parsedContent.Apps, parsedContent.FooterText);
                return Task.FromResult(result ?? false);
            }
            else
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
        }

        public Task ShowErrorAsync(string message, string title = "Error", string buttonText = "OK")
        {
            CustomDialog.ShowInformation(title, title, message, "");
            return Task.CompletedTask;
        }

        public Task ShowInformationAsync(
            string message,
            string title = "Information",
            string buttonText = "OK"
        )
        {
            var parsedContent = ParseMessageContent(message);

            if (parsedContent.IsAppList)
            {
                CustomDialog.ShowInformation(title, parsedContent.HeaderText, parsedContent.Apps, parsedContent.FooterText);
            }
            else
            {
                CustomDialog.ShowInformation(title, title, message, "");
            }

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
            if (message.Contains("theme wallpaper") || title.Contains("Theme"))
            {
                var messageList = new List<string> { message };
                var themeDialogResult = CustomDialog.ShowConfirmation(
                    title,
                    "Theme Change",
                    messageList,
                    ""
                );
                return Task.FromResult<bool?>(themeDialogResult);
            }

            var parsedContent = ParseMessageContent(message);

            if (parsedContent.IsAppList)
            {
                var dialogResult = CustomDialog.ShowYesNoCancel(title, parsedContent.HeaderText, parsedContent.Apps, parsedContent.FooterText);
                return Task.FromResult(dialogResult);
            }
            else
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
        }

        public async Task<Dictionary<string, bool>> ShowUnifiedConfigurationSaveDialogAsync(
            string title,
            string description,
            Dictionary<string, (bool IsSelected, bool IsAvailable, int ItemCount)> sections
        )
        {
            var dialog = new UnifiedConfigurationDialog(title, description, sections, true);

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

        public async Task<Dictionary<string, bool>> ShowUnifiedConfigurationImportDialogAsync(
            string title,
            string description,
            Dictionary<string, (bool IsSelected, bool IsAvailable, int ItemCount)> sections
        )
        {
            var dialog = new UnifiedConfigurationDialog(title, description, sections, false);

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
            string title,
            string supportMessage,
            string footerText
        )
        {
            try
            {
                var dialog = await DonationDialog.ShowDonationDialogAsync(
                    title,
                    supportMessage,
                    footerText
                );

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
            string cancelButtonText = "Cancel")
        {
            try
            {
                var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    return CustomDialog.ShowConfirmationWithCheckbox(
                        title,
                        message,
                        checkboxText,
                        continueButtonText,
                        cancelButtonText
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
                    cancelButtonText
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
        }

        public Task ShowInformationAsync(
            string title,
            string headerText,
            IEnumerable<string> apps,
            string footerText
        )
        {
            CustomDialog.ShowInformation(title, headerText, apps, footerText);
            return Task.CompletedTask;
        }

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