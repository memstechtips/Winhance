using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
                DefaultButton = ContentDialogButton.Primary
            };
            ConfigureDialog(dialog);

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
                DefaultButton = ContentDialogButton.Close
            };
            ConfigureDialog(dialog);

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
                DefaultButton = ContentDialogButton.Close
            };
            ConfigureDialog(dialog);

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
                DefaultButton = ContentDialogButton.Close
            };
            ConfigureDialog(dialog);

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
                DefaultButton = ContentDialogButton.Primary
            };
            ConfigureDialog(dialog);

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
                DefaultButton = ContentDialogButton.Primary
            };
            ConfigureDialog(dialog);

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
            return (result == ContentDialogResult.Primary, dontShowCheckBox.IsChecked == true);
        }
        finally
        {
            _dialogSemaphore.Release();
        }
    }

    public async Task<(ImportOption? Option, ImportOptions Options)> ShowConfigImportOptionsDialogAsync()
    {
        await _dialogSemaphore.WaitAsync();
        try
        {
            if (XamlRoot == null)
            {
                _logService.LogWarning("XamlRoot not set, cannot show dialog");
                return (null, new ImportOptions { ReviewBeforeApplying = true });
            }

            ImportOption? selectedOption = null;

            bool isDark = (XamlRoot?.Content as FrameworkElement)?.ActualTheme == ElementTheme.Dark;

            var dialog = new ContentDialog
            {
                Title = _localization.GetString("Dialog_ImportConfig_Title"),
                PrimaryButtonText = StringKeys.Localized.Button_Continue,
                IsPrimaryButtonEnabled = false,
                CloseButtonText = StringKeys.Localized.Button_Cancel,
                DefaultButton = ContentDialogButton.None,
                MinWidth = 500
            };
            ConfigureDialog(dialog);

            // Selection state for option cards
            Button? selectedCardButton = null;
            Border? selectedBgBorder = null;
            Border? selectedAccentBorder = null;

            // Helper to create option cards with select-then-continue behavior
            Button CreateOptionCard(UIElement icon, string titleKey, string descKey, ImportOption option, bool isLast = false)
            {
                var titleText = _localization.GetString(titleKey);
                var descText = _localization.GetString(descKey);

                var textPanel = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
                textPanel.Children.Add(new TextBlock
                {
                    Text = titleText,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontSize = 14
                });
                textPanel.Children.Add(new TextBlock
                {
                    Text = descText,
                    FontSize = 12,
                    Opacity = 0.7,
                    TextWrapping = TextWrapping.Wrap
                });

                var contentPanel2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, Margin = new Thickness(12, 8, 12, 8) };
                contentPanel2.Children.Add(icon);
                contentPanel2.Children.Add(textPanel);

                // Layer 0: Background fill (matches default Button rest state)
                var defaultBg = (Brush)Application.Current.Resources["ControlFillColorDefaultBrush"];
                var bgBorder = new Border
                {
                    Background = defaultBg,
                    CornerRadius = new CornerRadius(4)
                };

                // Layer 1: Accent border
                var accentBorder = new Border
                {
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(4)
                };

                var cardVisual = new Grid();
                cardVisual.Children.Add(bgBorder);
                cardVisual.Children.Add(accentBorder);
                cardVisual.Children.Add(contentPanel2);

                var cardButton = new Button
                {
                    Content = cardVisual,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    Margin = new Thickness(0, 0, 0, isLast ? 0 : 6)
                };
                AutomationProperties.SetName(cardButton, $"{titleText}. {descText}");

                // Override Button's built-in hover/press visual states so only our custom bgBorder shows
                cardButton.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                cardButton.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                cardButton.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                cardButton.Resources["ButtonBorderBrushPressed"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

                cardButton.Click += (_, _) =>
                {
                    // Re-click same card: no-op
                    if (selectedCardButton == cardButton) return;

                    // Deselect previous card
                    if (selectedBgBorder != null)
                        selectedBgBorder.Background = (Brush)Application.Current.Resources["ControlFillColorDefaultBrush"];
                    if (selectedAccentBorder != null)
                        selectedAccentBorder.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

                    // Select this card
                    bgBorder.Background = (Brush)Application.Current.Resources["SubtleFillColorTertiaryBrush"];
                    accentBorder.BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];

                    selectedCardButton = cardButton;
                    selectedBgBorder = bgBorder;
                    selectedAccentBorder = accentBorder;
                    selectedOption = option;
                    dialog.IsPrimaryButtonEnabled = true;

                    // Announce the selection to Narrator
                    var peer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(cardButton)
                               ?? Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(cardButton);
                    peer?.RaiseNotificationEvent(
                        Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationKind.ActionCompleted,
                        Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationProcessing.ImportantMostRecent,
                        $"{_localization.GetString("Accessibility_Selected") ?? "Selected"}: {titleText}",
                        "ConfigOptionSelected");
                };

                cardButton.PointerEntered += (_, _) =>
                {
                    if (selectedCardButton != cardButton)
                        bgBorder.Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"];
                };

                cardButton.PointerExited += (_, _) =>
                {
                    if (selectedCardButton != cardButton)
                        bgBorder.Background = (Brush)Application.Current.Resources["ControlFillColorDefaultBrush"];
                };

                // Show hover-like state on keyboard focus
                cardButton.GotFocus += (_, _) =>
                {
                    if (selectedCardButton != cardButton)
                        bgBorder.Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"];
                };
                cardButton.LostFocus += (_, _) =>
                {
                    if (selectedCardButton != cardButton)
                        bgBorder.Background = (Brush)Application.Current.Resources["ControlFillColorDefaultBrush"];
                };

                return cardButton;
            }

            // Card 1: Import own config - FolderOpen icon
            var ownIcon = new FluentIcons.WinUI.SymbolIcon { Symbol = FluentIcons.Common.Symbol.FolderOpen, IconVariant = FluentIcons.Common.IconVariant.Regular, FontSize = 24, VerticalAlignment = VerticalAlignment.Center };
            var ownCard = CreateOptionCard(ownIcon,
                "Dialog_ImportConfig_Option_Own_Title",
                "Dialog_ImportConfig_Option_Own_Description",
                ImportOption.ImportOwn);

            // Card 2: Import recommended config - Winhance logo
            var logoUri = isDark
                ? "ms-appx:///Assets/AppIcons/winhance-rocket-white-transparent-bg.png"
                : "ms-appx:///Assets/AppIcons/winhance-rocket-black-transparent-bg.png";
            var recIcon = new Image
            {
                Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new System.Uri(logoUri)),
                Width = 24,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Center
            };
            var recCard = CreateOptionCard(recIcon,
                "Dialog_ImportConfig_Option_Recommended_Title",
                "Dialog_ImportConfig_Option_Recommended_Description",
                ImportOption.ImportRecommended);

            // Card 3: Import backup config - History icon
            var backupIcon = new FluentIcons.WinUI.SymbolIcon { Symbol = FluentIcons.Common.Symbol.History, IconVariant = FluentIcons.Common.IconVariant.Regular, FontSize = 24, VerticalAlignment = VerticalAlignment.Center };
            var backupCard = CreateOptionCard(backupIcon,
                "Dialog_ImportConfig_Option_Backup_Title",
                "Dialog_ImportConfig_Option_Backup_Description",
                ImportOption.ImportBackup);

            // Card 4: Import Windows defaults - Refresh icon
            var defaultsIcon = new FluentIcons.WinUI.SymbolIcon { Symbol = FluentIcons.Common.Symbol.ArrowReset, IconVariant = FluentIcons.Common.IconVariant.Regular, FontSize = 24, VerticalAlignment = VerticalAlignment.Center };
            var defaultsCard = CreateOptionCard(defaultsIcon,
                "Dialog_ImportConfig_Option_Defaults_Title",
                "Dialog_ImportConfig_Option_Defaults_Description",
                ImportOption.ImportWindowsDefaults, isLast: true);

            // Helper to announce control state changes to Narrator
            void Announce(UIElement element, string message)
            {
                var peer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(element)
                           ?? Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(element);
                peer?.RaiseNotificationEvent(
                    Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationKind.ActionCompleted,
                    Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationProcessing.ImportantMostRecent,
                    message,
                    "ConfigDialogStateChange");
            }

            var skipReviewText = _localization.GetString("Review_Mode_Skip_Checkbox") ?? "Skip review and apply immediately";
            var skipReviewCheckbox = new CheckBox
            {
                Content = skipReviewText,
                IsChecked = false,
                Margin = new Thickness(0, 12, 0, 0)
            };

            // --- Import options panel (disabled unless skip review is checked) ---
            // Use a Grid so label and radio columns align across both rows
            var appsGrid = new Grid
            {
                Margin = new Thickness(0, 3, 0, 0),
                RowSpacing = 0,
                ColumnSpacing = 2
            };
            appsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            appsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            appsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            appsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            appsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            appsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var winAppsText = _localization.GetString("Config_Import_Options_WindowsApps") ?? "Windows Apps";
            var extAppsText = _localization.GetString("Config_Import_Options_ExternalApps") ?? "External Apps";
            var installText = _localization.GetString("Config_Import_Options_Install") ?? "Install";
            var uninstallText = _localization.GetString("Config_Import_Options_Uninstall") ?? "Uninstall";
            var selectOnlyText = _localization.GetString("Config_Import_Options_SelectOnly") ?? "Select Only";

            // Row 0: Windows Apps
            var winAppsLabel = new TextBlock
            {
                Text = winAppsText,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(winAppsLabel, 0);
            Grid.SetColumn(winAppsLabel, 0);

            var winAppsInstallRadio = new RadioButton
            {
                Content = new TextBlock { Text = installText, FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                GroupName = "WindowsApps",
                VerticalContentAlignment = VerticalAlignment.Center,
                IsEnabled = false,
                MinWidth = 0, MinHeight = 0,
                Padding = new Thickness(4, 0, 4, 0)
            };
            AutomationProperties.SetName(winAppsInstallRadio, $"{winAppsText}: {installText}");
            winAppsInstallRadio.Checked += (_, _) => Announce(winAppsInstallRadio, $"{winAppsText}: {installText}");
            Grid.SetRow(winAppsInstallRadio, 0);
            Grid.SetColumn(winAppsInstallRadio, 1);

            var winAppsUninstallRadio = new RadioButton
            {
                Content = new TextBlock { Text = uninstallText, FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                GroupName = "WindowsApps",
                VerticalContentAlignment = VerticalAlignment.Center,
                IsChecked = true,
                IsEnabled = false,
                MinWidth = 0, MinHeight = 0,
                Padding = new Thickness(4, 0, 4, 0)
            };
            AutomationProperties.SetName(winAppsUninstallRadio, $"{winAppsText}: {uninstallText}");
            winAppsUninstallRadio.Checked += (_, _) => Announce(winAppsUninstallRadio, $"{winAppsText}: {uninstallText}");
            Grid.SetRow(winAppsUninstallRadio, 0);
            Grid.SetColumn(winAppsUninstallRadio, 2);

            var winAppsSelectOnlyRadio = new RadioButton
            {
                Content = new TextBlock { Text = selectOnlyText, FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                GroupName = "WindowsApps",
                VerticalContentAlignment = VerticalAlignment.Center,
                IsEnabled = false,
                MinWidth = 0, MinHeight = 0,
                Padding = new Thickness(4, 0, 4, 0)
            };
            AutomationProperties.SetName(winAppsSelectOnlyRadio, $"{winAppsText}: {selectOnlyText}");
            winAppsSelectOnlyRadio.Checked += (_, _) => Announce(winAppsSelectOnlyRadio, $"{winAppsText}: {selectOnlyText}");
            Grid.SetRow(winAppsSelectOnlyRadio, 0);
            Grid.SetColumn(winAppsSelectOnlyRadio, 3);

            // Row 1: External Apps
            var extAppsLabel = new TextBlock
            {
                Text = extAppsText,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(extAppsLabel, 1);
            Grid.SetColumn(extAppsLabel, 0);

            var extAppsInstallRadio = new RadioButton
            {
                Content = new TextBlock { Text = installText, FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                GroupName = "ExternalApps",
                VerticalContentAlignment = VerticalAlignment.Center,
                IsChecked = true,
                IsEnabled = false,
                MinWidth = 0, MinHeight = 0,
                Padding = new Thickness(4, 0, 4, 0)
            };
            AutomationProperties.SetName(extAppsInstallRadio, $"{extAppsText}: {installText}");
            extAppsInstallRadio.Checked += (_, _) => Announce(extAppsInstallRadio, $"{extAppsText}: {installText}");
            Grid.SetRow(extAppsInstallRadio, 1);
            Grid.SetColumn(extAppsInstallRadio, 1);

            var extAppsUninstallRadio = new RadioButton
            {
                Content = new TextBlock { Text = uninstallText, FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                GroupName = "ExternalApps",
                VerticalContentAlignment = VerticalAlignment.Center,
                IsEnabled = false,
                MinWidth = 0, MinHeight = 0,
                Padding = new Thickness(4, 0, 4, 0)
            };
            AutomationProperties.SetName(extAppsUninstallRadio, $"{extAppsText}: {uninstallText}");
            extAppsUninstallRadio.Checked += (_, _) => Announce(extAppsUninstallRadio, $"{extAppsText}: {uninstallText}");
            Grid.SetRow(extAppsUninstallRadio, 1);
            Grid.SetColumn(extAppsUninstallRadio, 2);

            var extAppsSelectOnlyRadio = new RadioButton
            {
                Content = new TextBlock { Text = selectOnlyText, FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                GroupName = "ExternalApps",
                VerticalContentAlignment = VerticalAlignment.Center,
                IsEnabled = false,
                MinWidth = 0, MinHeight = 0,
                Padding = new Thickness(4, 0, 4, 0)
            };
            AutomationProperties.SetName(extAppsSelectOnlyRadio, $"{extAppsText}: {selectOnlyText}");
            extAppsSelectOnlyRadio.Checked += (_, _) => Announce(extAppsSelectOnlyRadio, $"{extAppsText}: {selectOnlyText}");
            Grid.SetRow(extAppsSelectOnlyRadio, 1);
            Grid.SetColumn(extAppsSelectOnlyRadio, 3);

            appsGrid.Children.Add(winAppsLabel);
            appsGrid.Children.Add(winAppsInstallRadio);
            appsGrid.Children.Add(winAppsUninstallRadio);
            appsGrid.Children.Add(winAppsSelectOnlyRadio);
            appsGrid.Children.Add(extAppsLabel);
            appsGrid.Children.Add(extAppsInstallRadio);
            appsGrid.Children.Add(extAppsUninstallRadio);
            appsGrid.Children.Add(extAppsSelectOnlyRadio);

            // Customize action checkboxes
            var themeWallpaperText = _localization.GetString("Config_Import_Options_ThemeWallpaper") ?? "Apply default wallpaper for theme";
            var themeWallpaperCheckbox = new CheckBox
            {
                Content = themeWallpaperText,
                IsChecked = true,
                IsEnabled = false,
                MinHeight = 0,
                Padding = new Thickness(4, 2, 4, 2),
                Margin = new Thickness(0, 2, 0, 0)
            };
            themeWallpaperCheckbox.Checked += (_, _) => Announce(themeWallpaperCheckbox, $"{themeWallpaperText}: {_localization.GetString("Accessibility_Checked") ?? "Checked"}");
            themeWallpaperCheckbox.Unchecked += (_, _) => Announce(themeWallpaperCheckbox, $"{themeWallpaperText}: {_localization.GetString("Accessibility_Unchecked") ?? "Unchecked"}");

            var cleanTaskbarText = _localization.GetString("Config_Import_Options_CleanTaskbar") ?? "Clean Taskbar";
            var cleanTaskbarCheckbox = new CheckBox
            {
                Content = cleanTaskbarText,
                IsChecked = true,
                IsEnabled = false,
                MinHeight = 0,
                Padding = new Thickness(4, 2, 4, 2)
            };
            cleanTaskbarCheckbox.Checked += (_, _) => Announce(cleanTaskbarCheckbox, $"{cleanTaskbarText}: {_localization.GetString("Accessibility_Checked") ?? "Checked"}");
            cleanTaskbarCheckbox.Unchecked += (_, _) => Announce(cleanTaskbarCheckbox, $"{cleanTaskbarText}: {_localization.GetString("Accessibility_Unchecked") ?? "Unchecked"}");

            var cleanStartMenuText = _localization.GetString("Config_Import_Options_CleanStartMenu") ?? "Clean Start Menu";
            var cleanStartMenuCheckbox = new CheckBox
            {
                Content = cleanStartMenuText,
                IsChecked = true,
                IsEnabled = false,
                MinHeight = 0,
                Padding = new Thickness(4, 2, 4, 2)
            };
            cleanStartMenuCheckbox.Checked += (_, _) => Announce(cleanStartMenuCheckbox, $"{cleanStartMenuText}: {_localization.GetString("Accessibility_Checked") ?? "Checked"}");
            cleanStartMenuCheckbox.Unchecked += (_, _) => Announce(cleanStartMenuCheckbox, $"{cleanStartMenuText}: {_localization.GetString("Accessibility_Unchecked") ?? "Unchecked"}");

            var optionsPanel = new StackPanel
            {
                Spacing = 0,
                Opacity = 0.4
            };
            optionsPanel.Children.Add(appsGrid);
            optionsPanel.Children.Add(themeWallpaperCheckbox);
            optionsPanel.Children.Add(cleanTaskbarCheckbox);
            optionsPanel.Children.Add(cleanStartMenuCheckbox);

            // Enable/disable options panel based on skip review checkbox
            skipReviewCheckbox.Checked += (_, _) =>
            {
                optionsPanel.Opacity = 1.0;
                winAppsInstallRadio.IsEnabled = true;
                winAppsUninstallRadio.IsEnabled = true;
                winAppsSelectOnlyRadio.IsEnabled = true;
                extAppsInstallRadio.IsEnabled = true;
                extAppsUninstallRadio.IsEnabled = true;
                extAppsSelectOnlyRadio.IsEnabled = true;
                themeWallpaperCheckbox.IsEnabled = true;
                cleanTaskbarCheckbox.IsEnabled = true;
                cleanStartMenuCheckbox.IsEnabled = true;
                Announce(skipReviewCheckbox, $"{skipReviewText}: {_localization.GetString("Accessibility_Checked") ?? "Checked"}");
            };
            skipReviewCheckbox.Unchecked += (_, _) =>
            {
                optionsPanel.Opacity = 0.4;
                winAppsInstallRadio.IsEnabled = false;
                winAppsUninstallRadio.IsEnabled = false;
                winAppsSelectOnlyRadio.IsEnabled = false;
                extAppsInstallRadio.IsEnabled = false;
                extAppsUninstallRadio.IsEnabled = false;
                extAppsSelectOnlyRadio.IsEnabled = false;
                themeWallpaperCheckbox.IsEnabled = false;
                cleanTaskbarCheckbox.IsEnabled = false;
                cleanStartMenuCheckbox.IsEnabled = false;
                Announce(skipReviewCheckbox, $"{skipReviewText}: {_localization.GetString("Accessibility_Unchecked") ?? "Unchecked"}");
            };

            var contentPanel = new StackPanel { Spacing = 0, Margin = new Thickness(0, 0, 14, 0) };
            contentPanel.Children.Add(new TextBlock
            {
                Text = _localization.GetString("Dialog_ImportOptions_Message"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            });
            contentPanel.Children.Add(ownCard);
            contentPanel.Children.Add(recCard);
            contentPanel.Children.Add(backupCard);
            contentPanel.Children.Add(defaultsCard);
            contentPanel.Children.Add(skipReviewCheckbox);
            contentPanel.Children.Add(optionsPanel);

            var scrollViewer = new ScrollViewer
            {
                Content = contentPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 480
            };

            dialog.Content = scrollViewer;

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                selectedOption = null; // Cancel was clicked, discard any selection
            }

            bool skipReview = skipReviewCheckbox.IsChecked == true;
            var importOptions = new ImportOptions
            {
                ReviewBeforeApplying = !skipReview,
                ProcessWindowsAppsInstallation = skipReview && winAppsInstallRadio.IsChecked == true,
                ProcessWindowsAppsRemoval = skipReview && winAppsUninstallRadio.IsChecked == true,
                // Select Only: neither Install nor Uninstall flag is set — apps get pre-selected only
                ProcessExternalAppsInstallation = skipReview && extAppsInstallRadio.IsChecked == true,
                ProcessExternalAppsRemoval = skipReview && extAppsUninstallRadio.IsChecked == true,
                ApplyThemeWallpaper = skipReview && themeWallpaperCheckbox.IsChecked == true,
                ApplyCleanTaskbar = skipReview && cleanTaskbarCheckbox.IsChecked == true,
                ApplyCleanStartMenu = skipReview && cleanStartMenuCheckbox.IsChecked == true,
            };
            return (selectedOption, importOptions);
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
                DefaultButton = ContentDialogButton.Close
            };
            ConfigureDialog(dialog);

            await dialog.ShowAsync();
        }
        finally
        {
            _dialogSemaphore.Release();
        }
    }

    public async Task<(bool Confirmed, bool CheckboxChecked)> ShowAppOperationConfirmationAsync(
        string operationType,
        IEnumerable<string> itemNames,
        int count,
        string? checkboxText = null)
    {
        await _dialogSemaphore.WaitAsync();
        try
        {
            if (XamlRoot == null)
            {
                _logService.LogWarning("XamlRoot not set, cannot show dialog");
                return (false, false);
            }

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
                void AnnounceCheckbox(UIElement element, string message)
                {
                    var peer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(element)
                               ?? Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(element);
                    peer?.RaiseNotificationEvent(
                        Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationKind.ActionCompleted,
                        Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationProcessing.ImportantMostRecent,
                        message,
                        "CheckboxStateChange");
                }
                checkBox.Checked += (_, _) => AnnounceCheckbox(checkBox, $"{checkboxText}: {_localization.GetString("Accessibility_Checked") ?? "Checked"}");
                checkBox.Unchecked += (_, _) => AnnounceCheckbox(checkBox, $"{checkboxText}: {_localization.GetString("Accessibility_Unchecked") ?? "Unchecked"}");

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
        }
        finally
        {
            _dialogSemaphore.Release();
        }
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
                DefaultButton = ContentDialogButton.Primary
            };
            ConfigureDialog(dialog);

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
