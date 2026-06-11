using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
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
    private readonly ISponsorsService _sponsorsService;
    private readonly SemaphoreSlim _dialogSemaphore = new(1, 1);

    /// <summary>
    /// The XamlRoot required for showing ContentDialogs.
    /// Must be set by MainWindow after content is loaded.
    /// </summary>
    public XamlRoot? XamlRoot { get; set; }

    public DialogService(ILocalizationService localization, ILogService logService, ITaskProgressService taskProgressService, ISponsorsService sponsorsService)
    {
        _localization = localization;
        _logService = logService;
        _taskProgressService = taskProgressService;
        _sponsorsService = sponsorsService;
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

    public async Task<(bool SupportClicked, bool DontShowAgain)> ShowSponsorsDialogAsync(SponsorsDialogMode mode)
    {
        return await ExecuteDialogAsync(async () =>
        {
            // GetSponsorsAsync never throws and may return null; the builder
            // handles a null document gracefully.
            var data = await _sponsorsService.GetSponsorsAsync();

            var builder = new Dialogs.SponsorsDialogBuilder(_localization, _sponsorsService);
            var dialog = builder.Build(data, mode, XamlRoot!);
            ConfigureDialog(dialog);
            await dialog.ShowAsync();
            return builder.ExtractResult();
        }, (false, false));
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

    public async Task<ConfirmationResponse> ShowConfirmationAsync(ConfirmationRequest confirmationRequest)
    {
        return await ExecuteDialogAsync(async () =>
        {
            var contentPanel = new StackPanel { Spacing = 12 };

            if (!string.IsNullOrEmpty(confirmationRequest.Message))
            {
                contentPanel.Children.Add(new TextBlock
                {
                    Text = confirmationRequest.Message,
                    TextWrapping = TextWrapping.Wrap
                });
            }

            if (confirmationRequest.Items is { Count: > 0 } items)
            {
                var itemContainerStyle = new Style(typeof(ListViewItem));
                itemContainerStyle.Setters.Add(new Setter(ListViewItem.PaddingProperty, new Thickness(0)));
                itemContainerStyle.Setters.Add(new Setter(ListViewItem.MinHeightProperty, 0d));

                var listView = new ListView
                {
                    ItemsSource = items.ToList(),
                    MaxHeight = 300,
                    SelectionMode = ListViewSelectionMode.None,
                    ItemContainerStyle = itemContainerStyle
                };

                var listContainer = new Border
                {
                    Child = listView,
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8),
                    Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        (XamlRoot?.Content as FrameworkElement)?.ActualTheme == ElementTheme.Dark
                            ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black),
                    BorderThickness = new Thickness(1)
                };

                contentPanel.Children.Add(listContainer);
            }

            CheckBox? checkBox = null;
            if (!string.IsNullOrEmpty(confirmationRequest.CheckboxText))
            {
                var checkboxText = confirmationRequest.CheckboxText;
                checkBox = new CheckBox
                {
                    Content = checkboxText,
                    IsChecked = confirmationRequest.CheckboxInitiallyChecked
                };

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
                Title = string.IsNullOrEmpty(confirmationRequest.Title)
                    ? _localization.GetString("Dialog_Confirmation")
                    : confirmationRequest.Title,
                Content = contentPanel,
                PrimaryButtonText = confirmationRequest.ConfirmButtonText,
                CloseButtonText = confirmationRequest.CancelButtonText,
                DefaultButton = ContentDialogButton.Primary
            };
            ConfigureDialog(dialog);

            var result = await dialog.ShowAsync();
            return new ConfirmationResponse
            {
                Confirmed = result == ContentDialogResult.Primary,
                CheckboxChecked = checkBox?.IsChecked == true
            };
        }, new ConfirmationResponse { Confirmed = false, CheckboxChecked = false });
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
