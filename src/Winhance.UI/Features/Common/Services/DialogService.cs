using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Helpers;
using Winhance.Core.Features.Common.Extensions;

namespace Winhance.UI.Features.Common.Services;

// A SemaphoreSlim serialises dialogs: WinUI 3 throws if two ContentDialogs are open at once.
public class DialogService : IDialogService
{
    private readonly ILocalizationService _localization;
    private readonly ILogService _logService;
    private readonly ITaskProgressService _taskProgressService;
    private readonly ISponsorsService _sponsorsService;
    private readonly IInstallOptionsService _installOptions;
    private readonly SemaphoreSlim _dialogSemaphore = new(1, 1);

    // Must be set by MainWindow after content is loaded.
    public XamlRoot? XamlRoot { get; set; }

    public DialogService(ILocalizationService localization, ILogService logService, ITaskProgressService taskProgressService, ISponsorsService sponsorsService,
        IInstallOptionsService installOptions)
    {
        _localization = localization;
        _logService = logService;
        _taskProgressService = taskProgressService;
        _sponsorsService = sponsorsService;
        _installOptions = installOptions;
    }

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

    private async Task<T> ExecuteDialogAsync<T>(Func<Task<T>> dialogAction, T defaultValue)
    {
        await _dialogSemaphore.WaitAsync();
        try
        {
            if (XamlRoot == null)
            {
                _logService.LogWarning("XamlRoot is null");
                return defaultValue;
            }
            return await dialogAction();
        }
        finally
        {
            _dialogSemaphore.Release();
        }
    }

    private async Task ExecuteDialogAsync(Func<Task> dialogAction)
    {
        await ExecuteDialogAsync(async () => { await dialogAction(); return true; }, true);
    }

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
        _ = ShowInformationAsync(message, title);
    }

    public async Task ShowInformationAsync(string message, string title = "", string buttonText = "")
        => await ShowSimpleDialogAsync(message,
            string.IsNullOrEmpty(title) ? _localization.GetString("Dialog_Information") : title,
            string.IsNullOrEmpty(buttonText) ? _localization.GetString("Button_OK") : buttonText);

    public async Task ShowWarningAsync(string message, string title = "", string buttonText = "")
        => await ShowSimpleDialogAsync(message,
            string.IsNullOrEmpty(title) ? _localization.GetString("Dialog_Warning") : title,
            string.IsNullOrEmpty(buttonText) ? _localization.GetString("Button_OK") : buttonText);

    public async Task ShowErrorAsync(string message, string title = "", string buttonText = "")
        => await ShowSimpleDialogAsync(message,
            string.IsNullOrEmpty(title) ? _localization.GetString("Dialog_Error") : title,
            string.IsNullOrEmpty(buttonText) ? _localization.GetString("Button_OK") : buttonText);

    public async Task ShowSponsorsDialogAsync(SponsorsDialogMode mode)
    {
        await ExecuteDialogAsync(async () =>
        {
            // GetSponsorsAsync never throws and may return null; the builder
            // handles a null document gracefully.
            var data = await _sponsorsService.GetSponsorsAsync();

            var builder = new Dialogs.SponsorsDialogBuilder(_localization, _sponsorsService);
            var dialog = builder.Build(data, mode);
            ConfigureDialog(dialog);
            await dialog.ShowAsync();
        });
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

    public async Task<BuilderSeed?> ShowBuilderSeedDialogAsync()
    {
        return await ExecuteDialogAsync(async () =>
        {
            var builder = new Dialogs.BuilderSeedDialogBuilder(_localization);
            var dialog = builder.Build();
            ConfigureDialog(dialog);
            var result = await dialog.ShowAsync();
            return builder.ExtractResult(result);
        }, (BuilderSeed?)null);
    }

    public async Task<InstallOptionsChoice?> ShowInstallOptionsDialogAsync(string? appName, InstallOptionsChoice current)
    {
        return await ExecuteDialogAsync(async () =>
        {
            var builder = new Dialogs.InstallOptionsDialogBuilder(_localization);
            var dialog = builder.Build(appName, current, _installOptions.Defaults);
            ConfigureDialog(dialog);
            var result = await dialog.ShowAsync();
            return builder.ExtractResult(result);
        }, (InstallOptionsChoice?)null);
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

                // Plain strings would clip at the dialog edge; each row is a wrapping TextBlock instead.
                var listView = new ListView
                {
                    ItemsSource = items.Select(item => new TextBlock { Text = item, TextWrapping = TextWrapping.Wrap }).ToList(),
                    MaxHeight = 300,
                    SelectionMode = ListViewSelectionMode.None,
                    ItemContainerStyle = itemContainerStyle
                };

                var listContainer = new Border
                {
                    Child = listView,
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8),
                    Style = CodeBuiltStyle.Named("CodeBuilt.CardSurface")
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

                checkBox.Checked += (_, _) => DialogAccessibilityHelper.AnnounceToNarrator(
                    checkBox,
                    $"{checkboxText}: {_localization.GetStringOrDefault("Common_Checked", "Checked")}",
                    "CheckboxStateChange");
                checkBox.Unchecked += (_, _) => DialogAccessibilityHelper.AnnounceToNarrator(
                    checkBox,
                    $"{checkboxText}: {_localization.GetStringOrDefault("Common_Unchecked", "Unchecked")}",
                    "CheckboxStateChange");

                contentPanel.Children.Add(checkBox);
            }

            var dialog = new ContentDialog
            {
                Title = string.IsNullOrEmpty(confirmationRequest.Title)
                    ? _localization.GetString("Dialog_Confirmation")
                    : confirmationRequest.Title,
                Content = contentPanel,
                PrimaryButtonText = string.IsNullOrEmpty(confirmationRequest.ConfirmButtonText)
                    ? _localization.GetString("Button_OK")
                    : confirmationRequest.ConfirmButtonText,
                SecondaryButtonText = confirmationRequest.SecondaryButtonText,
                CloseButtonText = string.IsNullOrEmpty(confirmationRequest.CancelButtonText)
                    ? _localization.GetString("Button_Cancel")
                    : confirmationRequest.CancelButtonText,
                // A three-button dialog defaults Enter to Cancel (the safe choice); the classic
                // two-button shape keeps Primary.
                DefaultButton = confirmationRequest.SecondaryButtonText is null
                    ? ContentDialogButton.Primary
                    : ContentDialogButton.Close
            };
            ConfigureDialog(dialog);

            var result = await dialog.ShowAsync();
            return new ConfirmationResponse
            {
                Confirmed = result == ContentDialogResult.Primary,
                SecondaryChosen = result == ContentDialogResult.Secondary,
                CheckboxChecked = checkBox?.IsChecked == true
            };
        }, new ConfirmationResponse { Confirmed = false, CheckboxChecked = false });
    }

    public async Task<bool> ShowSummaryAsync(SummaryRequest request) => (await AskSummaryAsync(request)).Confirmed;

    public async Task<ConfirmationResponse> AskSummaryAsync(SummaryRequest request)
    {
        return await ExecuteDialogAsync(async () =>
        {
            var builder = new Dialogs.SummaryDialogBuilder(_localization);
            var dialog = builder.Build(XamlRoot!, request);
            ConfigureDialog(dialog);
            var confirmed = await dialog.ShowAsync() == ContentDialogResult.Primary;
            return new ConfirmationResponse { Confirmed = confirmed, CheckboxChecked = builder.CheckboxChecked };
        }, new ConfirmationResponse());
    }

    public async Task<string?> AskTextAsync(TextInputRequest request)
    {
        return await ExecuteDialogAsync(async () =>
        {
            var input = new TextBox { Text = request.Text, PlaceholderText = request.Placeholder };
            var panel = new StackPanel { Spacing = 12 };
            if (!string.IsNullOrEmpty(request.Message))
                panel.Children.Add(new TextBlock { Text = request.Message, TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(input);

            var dialog = new ContentDialog
            {
                Title = request.Title,
                Content = panel,
                PrimaryButtonText = string.IsNullOrEmpty(request.ConfirmButtonText) ? _localization.GetString("Button_OK") : request.ConfirmButtonText,
                CloseButtonText = _localization.GetString("Button_Cancel"),
                DefaultButton = ContentDialogButton.Primary,
                IsPrimaryButtonEnabled = request.IsValid(input.Text),
            };
            input.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = request.IsValid(input.Text);
            input.Loaded += (_, _) => input.SelectAll();
            ConfigureDialog(dialog);
            return await dialog.ShowAsync() == ContentDialogResult.Primary ? input.Text.Trim() : null;
        }, (string?)null);
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

    public async Task ShowCustomContentDialogAsync(string title, object content, string closeButtonText = "")
    {
        await ExecuteDialogAsync(async () =>
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = string.IsNullOrEmpty(closeButtonText)
                    ? _localization.GetString("Button_Close")
                    : closeButtonText,
                DefaultButton = ContentDialogButton.Close
            };
            ConfigureDialog(dialog);
            await dialog.ShowAsync();
        });
    }
}
