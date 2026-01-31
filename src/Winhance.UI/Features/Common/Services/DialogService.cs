using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Constants;

namespace Winhance.UI.Features.Common.Services;

/// <summary>
/// WinUI 3 implementation of IDialogService using ContentDialog.
/// </summary>
/// <remarks>
/// This service uses a SemaphoreSlim to ensure only one ContentDialog is shown at a time,
/// as WinUI 3 throws an exception if multiple ContentDialogs are open simultaneously.
/// </remarks>
public class DialogService : IDialogService
{
    private readonly ILocalizationService _localization;
    private readonly ILogService _logService;
    private readonly SemaphoreSlim _dialogSemaphore = new(1, 1);

    /// <summary>
    /// The XamlRoot required for showing ContentDialogs.
    /// Must be set by MainWindow after content is loaded.
    /// </summary>
    public XamlRoot? XamlRoot { get; set; }

    public DialogService(ILocalizationService localization, ILogService logService)
    {
        _localization = localization;
        _logService = logService;
    }

    public void ShowMessage(string message, string title = "")
    {
        // Fire-and-forget for non-async message display
        _ = ShowInformationAsync(message, title);
    }

    public async Task<bool> ShowConfirmationAsync(
        string message,
        string title = "",
        string okButtonText = "OK",
        string cancelButtonText = "Cancel")
    {
        await _dialogSemaphore.WaitAsync();
        try
        {
            if (XamlRoot == null)
            {
                _logService.LogWarning("XamlRoot not set, cannot show dialog");
                return false;
            }

            var dialog = new ContentDialog
            {
                Title = string.IsNullOrEmpty(title) ? StringKeys.Localized.Dialog_Confirmation : title,
                Content = message,
                PrimaryButtonText = okButtonText,
                CloseButtonText = cancelButtonText,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }
        finally
        {
            _dialogSemaphore.Release();
        }
    }

    public async Task ShowInformationAsync(string message, string title = "Information", string buttonText = "OK")
    {
        await _dialogSemaphore.WaitAsync();
        try
        {
            if (XamlRoot == null)
            {
                _logService.LogWarning("XamlRoot not set, cannot show dialog");
                return;
            }

            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = buttonText,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            await dialog.ShowAsync();
        }
        finally
        {
            _dialogSemaphore.Release();
        }
    }

    public async Task ShowWarningAsync(string message, string title = "Warning", string buttonText = "OK")
    {
        await _dialogSemaphore.WaitAsync();
        try
        {
            if (XamlRoot == null)
            {
                _logService.LogWarning("XamlRoot not set, cannot show dialog");
                return;
            }

            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = buttonText,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            await dialog.ShowAsync();
        }
        finally
        {
            _dialogSemaphore.Release();
        }
    }

    public async Task ShowErrorAsync(string message, string title = "Error", string buttonText = "OK")
    {
        await _dialogSemaphore.WaitAsync();
        try
        {
            if (XamlRoot == null)
            {
                _logService.LogWarning("XamlRoot not set, cannot show dialog");
                return;
            }

            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = buttonText,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            await dialog.ShowAsync();
        }
        finally
        {
            _dialogSemaphore.Release();
        }
    }

    public async Task<string?> ShowInputAsync(string message, string title = "", string defaultValue = "")
    {
        await _dialogSemaphore.WaitAsync();
        try
        {
            if (XamlRoot == null)
            {
                _logService.LogWarning("XamlRoot not set, cannot show dialog");
                return null;
            }

            var inputBox = new TextBox
            {
                Text = defaultValue,
                PlaceholderText = message
            };

            var dialog = new ContentDialog
            {
                Title = string.IsNullOrEmpty(title) ? "Input" : title,
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                        inputBox
                    }
                },
                PrimaryButtonText = StringKeys.Localized.Button_OK,
                CloseButtonText = StringKeys.Localized.Button_Cancel,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary ? inputBox.Text : null;
        }
        finally
        {
            _dialogSemaphore.Release();
        }
    }

    public async Task<bool?> ShowYesNoCancelAsync(string message, string title = "")
    {
        await _dialogSemaphore.WaitAsync();
        try
        {
            if (XamlRoot == null)
            {
                _logService.LogWarning("XamlRoot not set, cannot show dialog");
                return null;
            }

            var dialog = new ContentDialog
            {
                Title = string.IsNullOrEmpty(title) ? StringKeys.Localized.Dialog_Confirmation : title,
                Content = message,
                PrimaryButtonText = StringKeys.Localized.Button_Yes,
                SecondaryButtonText = StringKeys.Localized.Button_No,
                CloseButtonText = StringKeys.Localized.Button_Cancel,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            return result switch
            {
                ContentDialogResult.Primary => true,
                ContentDialogResult.Secondary => false,
                _ => null
            };
        }
        finally
        {
            _dialogSemaphore.Release();
        }
    }

    public async Task<Dictionary<string, bool>> ShowUnifiedConfigurationSaveDialogAsync(
        string title,
        string description,
        Dictionary<string, (bool IsSelected, bool IsAvailable, int ItemCount)> sections)
    {
        // TODO: Implement as Page navigation or complex ContentDialog
        // For now, return all sections as selected
        _logService.LogWarning("ShowUnifiedConfigurationSaveDialogAsync not fully implemented");
        return sections.ToDictionary(s => s.Key, s => s.Value.IsSelected);
    }

    public async Task<(Dictionary<string, bool> sections, ImportOptions options)?> ShowUnifiedConfigurationImportDialogAsync(
        string title,
        string description,
        Dictionary<string, (bool IsSelected, bool IsAvailable, int ItemCount)> sections)
    {
        // TODO: Implement as Page navigation or complex ContentDialog
        // For now, return all sections as selected with default options
        _logService.LogWarning("ShowUnifiedConfigurationImportDialogAsync not fully implemented");
        return (sections.ToDictionary(s => s.Key, s => s.Value.IsSelected), new ImportOptions());
    }

    public async Task<(bool? Result, bool DontShowAgain)> ShowDonationDialogAsync(string? title = null, string? supportMessage = null)
    {
        await _dialogSemaphore.WaitAsync();
        try
        {
            if (XamlRoot == null)
            {
                _logService.LogWarning("XamlRoot not set, cannot show donation dialog");
                return (null, false);
            }

            var dontShowCheckBox = new CheckBox
            {
                Content = _localization.GetString("Dialog_DontShowAgain")
            };

            var dialog = new ContentDialog
            {
                Title = title ?? _localization.GetString("Dialog_Support_Title"),
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = supportMessage ?? _localization.GetString("Dialog_Support_Message"),
                            TextWrapping = TextWrapping.Wrap
                        },
                        dontShowCheckBox
                    }
                },
                PrimaryButtonText = StringKeys.Localized.Button_Yes,
                SecondaryButtonText = StringKeys.Localized.Button_No,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            return (result == ContentDialogResult.Primary, dontShowCheckBox.IsChecked == true);
        }
        finally
        {
            _dialogSemaphore.Release();
        }
    }

    public async Task<ImportOption?> ShowConfigImportOptionsDialogAsync()
    {
        await _dialogSemaphore.WaitAsync();
        try
        {
            if (XamlRoot == null)
            {
                _logService.LogWarning("XamlRoot not set, cannot show dialog");
                return null;
            }

            var radioImportOwn = new RadioButton { Content = _localization.GetString("ImportOption_Own"), IsChecked = true };
            var radioImportRecommended = new RadioButton { Content = _localization.GetString("ImportOption_Recommended") };

            var dialog = new ContentDialog
            {
                Title = _localization.GetString("Dialog_ImportOptions_Title"),
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = _localization.GetString("Dialog_ImportOptions_Message"), TextWrapping = TextWrapping.Wrap },
                        radioImportOwn,
                        radioImportRecommended
                    }
                },
                PrimaryButtonText = StringKeys.Localized.Button_Continue,
                CloseButtonText = StringKeys.Localized.Button_Cancel,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
                return null;

            return radioImportOwn.IsChecked == true ? ImportOption.ImportOwn : ImportOption.ImportRecommended;
        }
        finally
        {
            _dialogSemaphore.Release();
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
        await _dialogSemaphore.WaitAsync();
        try
        {
            if (XamlRoot == null)
            {
                _logService.LogWarning("XamlRoot not set, cannot show dialog");
                return (false, false);
            }

            var checkBox = new CheckBox { Content = checkboxText ?? "Don't show again" };

            var contentPanel = new StackPanel { Spacing = 12 };
            contentPanel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
            if (!string.IsNullOrEmpty(checkboxText))
            {
                contentPanel.Children.Add(checkBox);
            }

            var dialog = new ContentDialog
            {
                Title = title,
                Content = contentPanel,
                PrimaryButtonText = continueButtonText,
                CloseButtonText = cancelButtonText,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            return (result == ContentDialogResult.Primary, checkBox.IsChecked == true);
        }
        finally
        {
            _dialogSemaphore.Release();
        }
    }

    public void ShowOperationResult(
        string operationType,
        int successCount,
        int totalCount,
        IEnumerable<string> successItems,
        IEnumerable<string>? failedItems = null,
        IEnumerable<string>? skippedItems = null,
        bool hasConnectivityIssues = false,
        bool isUserCancelled = false)
    {
        // Build result message
        var message = $"{operationType} completed: {successCount}/{totalCount} items successful.";

        if (failedItems?.Any() == true)
        {
            message += $"\n\nFailed: {string.Join(", ", failedItems.Take(5))}";
            if (failedItems.Count() > 5)
                message += $" and {failedItems.Count() - 5} more...";
        }

        if (skippedItems?.Any() == true)
        {
            message += $"\n\nSkipped: {string.Join(", ", skippedItems.Take(5))}";
            if (skippedItems.Count() > 5)
                message += $" and {skippedItems.Count() - 5} more...";
        }

        if (hasConnectivityIssues)
            message += "\n\nNote: Some operations may have failed due to connectivity issues.";

        if (isUserCancelled)
            message += "\n\nOperation was cancelled by user.";

        // Fire-and-forget
        _ = ShowInformationAsync(message, $"{operationType} Result");
    }

    public async Task ShowInformationAsync(
        string title,
        string headerText,
        IEnumerable<string> apps,
        string footerText)
    {
        await _dialogSemaphore.WaitAsync();
        try
        {
            if (XamlRoot == null)
            {
                _logService.LogWarning("XamlRoot not set, cannot show dialog");
                return;
            }

            var listView = new ListView
            {
                ItemsSource = apps,
                MaxHeight = 300
            };

            var dialog = new ContentDialog
            {
                Title = title,
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock { Text = headerText, TextWrapping = TextWrapping.Wrap },
                        listView,
                        new TextBlock { Text = footerText, TextWrapping = TextWrapping.Wrap }
                    }
                },
                CloseButtonText = StringKeys.Localized.Button_OK,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            await dialog.ShowAsync();
        }
        finally
        {
            _dialogSemaphore.Release();
        }
    }

    public async Task<bool> ShowAppOperationConfirmationAsync(
        string operationType,
        IEnumerable<string> itemNames,
        int count)
    {
        var message = $"The following {count} item(s) will be {operationType.ToLower()}:\n\n" +
                      string.Join("\n", itemNames.Take(10));

        if (count > 10)
            message += $"\n... and {count - 10} more";

        return await ShowConfirmationAsync(message, $"Confirm {operationType}");
    }

    public async Task<ConfirmationResponse> ShowConfirmationAsync(
        ConfirmationRequest confirmationRequest,
        string continueButtonText = "Continue",
        string cancelButtonText = "Cancel")
    {
        await _dialogSemaphore.WaitAsync();
        try
        {
            if (XamlRoot == null)
            {
                _logService.LogWarning("XamlRoot not set, cannot show dialog");
                return new ConfirmationResponse { Confirmed = false };
            }

            var contentPanel = new StackPanel { Spacing = 8 };

            if (!string.IsNullOrEmpty(confirmationRequest.Message))
            {
                contentPanel.Children.Add(new TextBlock
                {
                    Text = confirmationRequest.Message,
                    TextWrapping = TextWrapping.Wrap
                });
            }

            CheckBox? checkBox = null;
            if (!string.IsNullOrEmpty(confirmationRequest.CheckboxText))
            {
                checkBox = new CheckBox { Content = confirmationRequest.CheckboxText };
                contentPanel.Children.Add(checkBox);
            }

            var dialog = new ContentDialog
            {
                Title = confirmationRequest.Title,
                Content = contentPanel,
                PrimaryButtonText = continueButtonText,
                CloseButtonText = cancelButtonText,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            return new ConfirmationResponse
            {
                Confirmed = result == ContentDialogResult.Primary,
                CheckboxChecked = checkBox?.IsChecked == true
            };
        }
        finally
        {
            _dialogSemaphore.Release();
        }
    }

    public void ShowLocalizedDialog(
        string titleKey,
        string messageKey,
        DialogType dialogType,
        string iconName,
        params object[] messageParams)
    {
        var title = _localization.GetString(titleKey);
        var message = messageParams.Length > 0
            ? _localization.GetString(messageKey, messageParams)
            : _localization.GetString(messageKey);

        // Fire-and-forget based on dialog type
        switch (dialogType)
        {
            case DialogType.Error:
                _ = ShowErrorAsync(message, title);
                break;
            case DialogType.Warning:
                _ = ShowWarningAsync(message, title);
                break;
            case DialogType.Information:
            default:
                _ = ShowInformationAsync(message, title);
                break;
        }
    }

    public bool ShowLocalizedConfirmationDialog(
        string titleKey,
        string messageKey,
        DialogType dialogType,
        string iconName,
        params object[] messageParams)
    {
        var title = _localization.GetString(titleKey);
        var message = messageParams.Length > 0
            ? _localization.GetString(messageKey, messageParams)
            : _localization.GetString(messageKey);

        // This is a synchronous method but we need async dialog
        // Return false as default - callers should use async methods instead
        _logService.LogWarning("ShowLocalizedConfirmationDialog called - use async methods instead");
        return false;
    }
}
