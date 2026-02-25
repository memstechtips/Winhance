using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Constants;
using Winhance.UI.Helpers;

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
    private readonly ITaskProgressService _taskProgressService;
    private readonly SemaphoreSlim _dialogSemaphore = new(1, 1);

    /// <summary>
    /// The XamlRoot required for showing ContentDialogs.
    /// Must be set by MainWindow after content is loaded.
    /// </summary>
    public XamlRoot? XamlRoot { get; set; }

    public DialogService(ILocalizationService localization, ILogService logService, ITaskProgressService taskProgressService)
    {
        _localization = localization;
        _logService = logService;
        _taskProgressService = taskProgressService;
    }

    /// <summary>
    /// Applies common dialog configuration: XamlRoot, theme, and a semi-transparent
    /// background so the window's Mica/Acrylic backdrop shows through the dialog.
    /// </summary>
    private void ConfigureDialog(ContentDialog dialog)
    {
        dialog.XamlRoot = XamlRoot;

        if (XamlRoot?.Content is FrameworkElement rootElement)
        {
            dialog.RequestedTheme = rootElement.ActualTheme == ElementTheme.Dark
                ? ElementTheme.Dark
                : ElementTheme.Light;
        }

        // The default ContentDialogBackground is a fully opaque SolidColorBrush that
        // blocks the window's Mica/Acrylic backdrop. Replace it with an in-app AcrylicBrush
        // which blurs the content behind the dialog (including the Mica effect visible
        // through the page) to create a frosted-glass look that harmonizes with the backdrop.
        // TintOpacity is kept low enough for the Mica wallpaper tinting to show through.
        var baseColor = dialog.RequestedTheme == ElementTheme.Dark
            ? Windows.UI.Color.FromArgb(255, 44, 44, 44)
            : Windows.UI.Color.FromArgb(255, 243, 243, 243);
        dialog.Background = new AcrylicBrush
        {
            TintColor = baseColor,
            TintOpacity = 0.65,
            TintLuminosityOpacity = 0.75,
            FallbackColor = baseColor
        };
    }

    #region Guard Helpers

    /// <summary>
    /// Acquires the dialog semaphore, checks XamlRoot, and executes the dialog action.
    /// Returns <paramref name="defaultValue"/> if XamlRoot is null.
    /// </summary>
    private async Task<T> ExecuteDialogAsync<T>(Func<Task<T>> dialogAction, T defaultValue)
    {
        await _dialogSemaphore.WaitAsync();
        try
        {
            if (XamlRoot == null)
            {
                _logService.LogWarning("[DialogService] XamlRoot is null");
                return defaultValue;
            }
            return await dialogAction();
        }
        finally
        {
            _dialogSemaphore.Release();
        }
    }

    /// <summary>
    /// Acquires the dialog semaphore, checks XamlRoot, and executes a void dialog action.
    /// </summary>
    private async Task ExecuteDialogAsync(Func<Task> dialogAction)
    {
        await ExecuteDialogAsync(async () => { await dialogAction(); return true; }, true);
    }

    #endregion

    #region Simple Dialogs

    /// <summary>
    /// Shared implementation for ShowInformationAsync, ShowWarningAsync, and ShowErrorAsync.
    /// </summary>
    private async Task ShowSimpleDialogAsync(string message, string title, string buttonText)
    {
        await ExecuteDialogAsync(async () =>
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = buttonText,
                DefaultButton = ContentDialogButton.Close
            };
            ConfigureDialog(dialog);
            await dialog.ShowAsync();
        });
    }

    public void ShowMessage(string message, string title = "")
    {
        // Fire-and-forget for non-async message display
        _ = ShowInformationAsync(message, title);
    }

    public async Task ShowInformationAsync(string message, string title = "Information", string buttonText = "OK")
        => await ShowSimpleDialogAsync(message, title, buttonText);

    public async Task ShowWarningAsync(string message, string title = "Warning", string buttonText = "OK")
        => await ShowSimpleDialogAsync(message, title, buttonText);

    public async Task ShowErrorAsync(string message, string title = "Error", string buttonText = "OK")
        => await ShowSimpleDialogAsync(message, title, buttonText);

    #endregion

    public async Task<bool> ShowConfirmationAsync(
        string message,
        string title = "",
        string okButtonText = "OK",
        string cancelButtonText = "Cancel")
    {
        return await ExecuteDialogAsync(async () =>
        {
            var dialog = new ContentDialog
            {
                Title = string.IsNullOrEmpty(title) ? StringKeys.Localized.Dialog_Confirmation : title,
                Content = message,
                PrimaryButtonText = okButtonText,
                CloseButtonText = cancelButtonText,
                DefaultButton = ContentDialogButton.Primary
            };
            ConfigureDialog(dialog);

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }, false);
    }

    public async Task<(bool? Result, bool DontShowAgain)> ShowDonationDialogAsync(string? title = null, string? supportMessage = null)
    {
        return await ExecuteDialogAsync(async () =>
        {
            // Red heart icon matching the WPF DonationDialog
            // Use Path inside Viewbox for proper scaling (PathIcon doesn't scale with Width/Height)
            var heartPath = new Microsoft.UI.Xaml.Shapes.Path
            {
                Data = (Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(
                    typeof(Geometry),
                    "M12,21.35L10.55,20.03C5.4,15.36 2,12.27 2,8.5C2,5.41 4.42,3 7.5,3C9.24,3 10.91,3.81 12,5.08C13.09,3.81 14.76,3 16.5,3C19.58,3 22,5.41 22,8.5C22,12.27 18.6,15.36 13.45,20.03L12,21.35Z"),
                Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xE8, 0x11, 0x23)),
                Stretch = Stretch.Uniform
            };
            var heartIcon = new Viewbox
            {
                Width = 30,
                Height = 30,
                Child = heartPath,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 20, 0, 0)
            };

            // Header: icon + title/message
            var headerTitle = new TextBlock
            {
                Text = _localization.GetString("Dialog_Donation_Header_Title"),
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };

            var headerMessage = new TextBlock
            {
                Text = _localization.GetString("Dialog_Donation_Header_Message"),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.8
            };

            var headerTextPanel = new StackPanel
            {
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { headerTitle, headerMessage }
            };

            // Use Grid instead of horizontal StackPanel so text wraps properly
            var headerPanel = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                },
                ColumnSpacing = 10
            };
            Grid.SetColumn(heartIcon, 0);
            Grid.SetColumn(headerTextPanel, 1);
            headerPanel.Children.Add(heartIcon);
            headerPanel.Children.Add(headerTextPanel);

            // Support message
            var supportText = new TextBlock
            {
                Text = supportMessage ?? _localization.GetString("Dialog_Donation_SupportMessage"),
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            };

            // Footer
            var footerText = new TextBlock
            {
                Text = _localization.GetString("Dialog_Donation_Footer"),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.7
            };

            // Don't show again checkbox
            var dontShowCheckBox = new CheckBox
            {
                Content = _localization.GetString("Dialog_Donation_Checkbox")
            };

            var contentPanel = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    headerPanel,
                    supportText,
                    footerText,
                    dontShowCheckBox
                }
            };

            var dialog = new ContentDialog
            {
                Title = title ?? _localization.GetString("Dialog_Donation_Title"),
                Content = contentPanel,
                PrimaryButtonText = StringKeys.Localized.Button_Yes,
                SecondaryButtonText = StringKeys.Localized.Button_No,
                DefaultButton = ContentDialogButton.Primary
            };
            ConfigureDialog(dialog);

            var result = await dialog.ShowAsync();
            return ((bool?)(result == ContentDialogResult.Primary), dontShowCheckBox.IsChecked == true);
        }, ((bool?)null, false));
    }

    public async Task<(ImportOption? Option, ImportOptions Options)> ShowConfigImportOptionsDialogAsync()
    {
        return await ExecuteDialogAsync(async () =>
        {
            var builder = new Dialogs.ConfigImportDialogBuilder(_localization);
            var dialog = builder.Build(XamlRoot!);
            ConfigureDialog(dialog);
            var result = await dialog.ShowAsync();
            return builder.ExtractResult(result);
        }, ((ImportOption?)null, new ImportOptions { ReviewBeforeApplying = true }));
    }

    public async Task<(bool Confirmed, bool CheckboxChecked)> ShowConfirmationWithCheckboxAsync(
        string message,
        string? checkboxText = null,
        string title = "Confirmation",
        string continueButtonText = "Continue",
        string cancelButtonText = "Cancel")
    {
        return await ExecuteDialogAsync(async () =>
        {

            var checkBox = new CheckBox { Content = checkboxText ?? "Don't show again", IsChecked = true };

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
                DefaultButton = ContentDialogButton.Primary
            };
            ConfigureDialog(dialog);

            var result = await dialog.ShowAsync();
            return (result == ContentDialogResult.Primary, checkBox.IsChecked == true);
        }, (false, false));
    }

    public async Task<(bool Confirmed, bool CheckboxChecked)> ShowAppOperationConfirmationAsync(
        string operationType,
        IEnumerable<string> itemNames,
        int count,
        string? checkboxText = null)
    {
        return await ExecuteDialogAsync(async () =>
        {
            bool isInstall = operationType.Equals("install", StringComparison.OrdinalIgnoreCase);
            bool isRemove = operationType.Equals("remove", StringComparison.OrdinalIgnoreCase);

            string title = isInstall ? _localization.GetString("Dialog_ConfirmInstallation") :
                          isRemove ? _localization.GetString("Dialog_ConfirmRemoval") :
                          _localization.GetString("Dialog_ConfirmOperation", operationType);

            string headerMessage = isInstall ? _localization.GetString("Dialog_ItemsWillBeInstalled") :
                                  isRemove ? _localization.GetString("Dialog_ItemsWillBeRemoved") :
                                  _localization.GetString("Dialog_ItemsWillBeProcessed", operationType.ToLower());

            var itemContainerStyle = new Style(typeof(ListViewItem));
            itemContainerStyle.Setters.Add(new Setter(ListViewItem.PaddingProperty, new Thickness(0)));
            itemContainerStyle.Setters.Add(new Setter(ListViewItem.MinHeightProperty, 0d));

            var listView = new ListView
            {
                ItemsSource = itemNames.ToList(),
                MaxHeight = 300,
                SelectionMode = ListViewSelectionMode.None,
                ItemContainerStyle = itemContainerStyle
            };

            var listContainer = new Border
            {
                Child = listView,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8),
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"]
            };

            var contentPanel = new StackPanel { Spacing = 12 };
            contentPanel.Children.Add(new TextBlock { Text = headerMessage, TextWrapping = TextWrapping.Wrap });
            contentPanel.Children.Add(listContainer);

            CheckBox? checkBox = null;
            if (!string.IsNullOrEmpty(checkboxText))
            {
                checkBox = new CheckBox { Content = checkboxText, IsChecked = true };

                // Announce checkbox state changes to Narrator
                checkBox.Checked += (_, _) => DialogAccessibilityHelper.AnnounceToNarrator(
                    checkBox,
                    $"{checkboxText}: {_localization.GetString("Accessibility_Checked") ?? "Checked"}",
                    "CheckboxStateChange");
                checkBox.Unchecked += (_, _) => DialogAccessibilityHelper.AnnounceToNarrator(
                    checkBox,
                    $"{checkboxText}: {_localization.GetString("Accessibility_Unchecked") ?? "Unchecked"}",
                    "CheckboxStateChange");

                contentPanel.Children.Add(checkBox);
            }

            var dialog = new ContentDialog
            {
                Title = title,
                Content = contentPanel,
                PrimaryButtonText = _localization.GetString("Button_Continue"),
                CloseButtonText = _localization.GetString("Button_Cancel"),
                DefaultButton = ContentDialogButton.Primary
            };
            ConfigureDialog(dialog);

            var result = await dialog.ShowAsync();
            return (result == ContentDialogResult.Primary, checkBox?.IsChecked == true);
        }, (false, false));
    }

    public async Task<ConfirmationResponse> ShowConfirmationAsync(
        ConfirmationRequest confirmationRequest,
        string continueButtonText = "Continue",
        string cancelButtonText = "Cancel")
    {
        return await ExecuteDialogAsync(async () =>
        {
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
                DefaultButton = ContentDialogButton.Primary
            };
            ConfigureDialog(dialog);

            var result = await dialog.ShowAsync();
            return new ConfirmationResponse
            {
                Confirmed = result == ContentDialogResult.Primary,
                CheckboxChecked = checkBox?.IsChecked == true
            };
        }, new ConfirmationResponse { Confirmed = false });
    }

    public async Task ShowTaskOutputDialogAsync(string title, IReadOnlyList<string> logMessages)
    {
        await ExecuteDialogAsync(async () =>
        {
            var builder = new Dialogs.TaskOutputDialogBuilder(_localization, _taskProgressService);
            var dialog = builder.Build(XamlRoot!, title, logMessages);
            ConfigureDialog(dialog);
            builder.StartLiveUpdates(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            try
            {
                await dialog.ShowAsync();
            }
            finally
            {
                builder.StopLiveUpdates();
            }
        });
    }

    public async Task ShowCustomContentDialogAsync(string title, object content, string closeButtonText = "Close")
    {
        await ExecuteDialogAsync(async () =>
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = closeButtonText,
                DefaultButton = ContentDialogButton.Close
            };
            ConfigureDialog(dialog);
            await dialog.ShowAsync();
        });
    }
}
