using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MahApps.Metro.IconPacks;
using Winhance.Core.Features.Common.Enums;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Common.Views
{
    public partial class CustomDialog : Window
    {

        public CustomDialog()
        {
            InitializeComponent();
            DataContext = this;

            Loaded += (s, e) =>
            {
                if (Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
                {
                    mainViewModel.IsDialogOverlayVisible = true;
                }
            };

            Closed += (s, e) =>
            {
                if (Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
                {
                    mainViewModel.IsDialogOverlayVisible = false;
                }
            };
        }

        private static CustomDialog CreateBaseDialog(
            string windowTitle,
            string headerText,
            string footerText,
            DialogType dialogType = DialogType.None,
            string? titleBarIcon = null)
        {
            var dialog = new CustomDialog { Title = windowTitle };

            dialog.HeaderText.Text = windowTitle;
            dialog.HeaderText.Foreground = (SolidColorBrush)dialog.FindResource("SecondaryTextColor");
            dialog.HeaderText.FontWeight = FontWeights.Normal;

            if (string.IsNullOrEmpty(titleBarIcon) && dialogType != DialogType.None)
            {
                titleBarIcon = dialogType switch
                {
                    DialogType.Success => "CheckCircle",
                    DialogType.Information => "Information",
                    DialogType.Warning => "Alert",
                    DialogType.Error => "CloseCircle",
                    DialogType.Question => "HelpCircle",
                    _ => null
                };
            }

            if (!string.IsNullOrEmpty(titleBarIcon))
            {
                if (Enum.TryParse<PackIconMaterialKind>(titleBarIcon, out var iconKind))
                {
                    dialog.TitleBarIcon.Kind = iconKind;
                    dialog.TitleBarIcon.Visibility = Visibility.Visible;
                }
            }

            if (dialogType != DialogType.None)
            {
                var (iconKind, iconPack, iconColor) = GetDialogIconDetails(dialogType);
                var iconControl = CreateIconControl(iconKind, iconPack, iconColor, 48);
                dialog.ContentIconContainer.Content = iconControl;
                dialog.ContentIconContainer.Visibility = Visibility.Visible;
            }

            return dialog;
        }

        private static (string iconKind, string iconPack, Color iconColor) GetDialogIconDetails(DialogType type)
        {
            return type switch
            {
                DialogType.Information => ("Information", "Material", Color.FromRgb(0, 120, 215)),
                DialogType.Warning => ("Alert", "Material", Color.FromRgb(255, 185, 0)),
                DialogType.Error => ("CloseCircle", "Material", Color.FromRgb(232, 17, 35)),
                DialogType.Question => ("ChatQuestionOutline", "Material", Color.FromRgb(0, 120, 215)),
                DialogType.Success => ("CheckCircle", "Material", Color.FromRgb(16, 124, 16)),
                _ => ("Information", "Material", Colors.Gray)
            };
        }

        private static Control CreateIconControl(string iconKind, string iconPack, Color color, double size)
        {
            Control? icon = null;
            bool iconSet = false;

            if (iconPack == "Lucide")
            {
                if (Enum.TryParse<PackIconLucideKind>(iconKind, true, out var lucideKind))
                {
                    icon = new PackIconLucide
                    {
                        Kind = lucideKind,
                        Width = size,
                        Height = size,
                        Foreground = new SolidColorBrush(color)
                    };
                    iconSet = true;
                }
            }
            else if (iconPack == "MaterialDesign")
            {
                if (Enum.TryParse<PackIconMaterialDesignKind>(iconKind, out var mdKind))
                {
                    icon = new PackIconMaterialDesign
                    {
                        Kind = mdKind,
                        Width = size,
                        Height = size,
                        Foreground = new SolidColorBrush(color)
                    };
                    iconSet = true;
                }
            }
            else
            {
                if (Enum.TryParse<PackIconMaterialKind>(iconKind, out var materialKind))
                {
                    icon = new PackIconMaterial
                    {
                        Kind = materialKind,
                        Width = size,
                        Height = size,
                        Foreground = new SolidColorBrush(color)
                    };
                    iconSet = true;
                }
            }

            if (!iconSet || icon == null)
            {
                icon = new PackIconMaterial
                {
                    Kind = PackIconMaterialKind.HelpCircle,
                    Width = size,
                    Height = size,
                    Foreground = new SolidColorBrush(color)
                };
            }

            return icon;
        }

        private void SetupAppListDisplay(IEnumerable<string> items)
        {
            AppListControl.ItemsSource = items;
            AppListBorder.Visibility = Visibility.Visible;
            SimpleContentPanel.Visibility = Visibility.Collapsed;
        }

        private void SetupAppListDisplayWithMessage(string message, IEnumerable<string> items)
        {
            MessageContent.Text = message;
            MessageContent.Visibility = Visibility.Visible;
            MessageContent.Margin = new Thickness(0, 0, 0, 15);
            SimpleContentPanel.Visibility = Visibility.Visible;

            AppListControl.ItemsSource = items;
            AppListBorder.Visibility = Visibility.Visible;
            AppListBorder.Margin = new Thickness(0, 10, 0, 0);
        }

        private void SetupSimpleMessageDisplay(string message)
        {
            MessageContent.Text = message;
            MessageContent.Margin = new Thickness(0, 0, 0, 40);
            SimpleContentPanel.Visibility = Visibility.Visible;
            AppListBorder.Visibility = Visibility.Collapsed;
        }

        private void PrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void SecondaryButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TertiaryButton_Click(object sender, RoutedEventArgs e)
        {
            // Explicitly set DialogResult to null for Cancel
            DialogResult = null;

            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Set DialogResult to null for Cancel (same behavior as TertiaryButton)
            DialogResult = null;
            Close();
        }

        public static CustomDialog CreateConfirmationDialog(
            string title,
            string message,
            string footerText = "",
            DialogType dialogType = DialogType.Question,
            string? titleBarIcon = null)
        {
            var dialog = CreateBaseDialog(title, title, footerText, dialogType, titleBarIcon);
            dialog.SetupSimpleMessageDisplay(message);
            dialog.PrimaryButton.Content = "Yes";
            dialog.SecondaryButton.Content = "No";
            return dialog;
        }

        public static CustomDialog CreateConfirmationDialog(
            string title,
            string headerText,
            string message,
            string footerText
        )
        {
            var dialog = CreateBaseDialog(title, headerText, footerText, DialogType.Question);
            dialog.SetupSimpleMessageDisplay(message);
            dialog.PrimaryButton.Content = "Yes";
            dialog.SecondaryButton.Content = "No";
            return dialog;
        }

        public static CustomDialog CreateConfirmationDialog(
            string title,
            string headerText,
            IEnumerable<string> items,
            string footerText
        )
        {
            var dialog = CreateBaseDialog(title, headerText, footerText, DialogType.Question);
            dialog.SetupAppListDisplay(items);
            dialog.PrimaryButton.Content = "Yes";
            dialog.SecondaryButton.Content = "No";
            return dialog;
        }

        public static CustomDialog CreateInformationDialog(
            string title,
            string message,
            string footerText = "",
            DialogType dialogType = DialogType.Information,
            string? titleBarIcon = null)
        {
            var dialog = CreateBaseDialog(title, title, footerText, dialogType, titleBarIcon);
            dialog.SetupSimpleMessageDisplay(message);
            dialog.PrimaryButton.Content = "OK";
            dialog.SecondaryButton.Visibility = Visibility.Collapsed;
            return dialog;
        }

        public static CustomDialog CreateInformationDialog(
            string title,
            string headerText,
            string message,
            string footerText
        )
        {
            var dialog = CreateBaseDialog(title, headerText, footerText, DialogType.Information);
            dialog.SetupSimpleMessageDisplay(message);
            dialog.PrimaryButton.Content = "OK";
            dialog.SecondaryButton.Visibility = Visibility.Collapsed;
            return dialog;
        }

        public static CustomDialog CreateInformationDialog(
            string title,
            string headerText,
            IEnumerable<string> items,
            string footerText
        )
        {
            var dialog = CreateBaseDialog(title, headerText, footerText, DialogType.Information);
            dialog.SetupAppListDisplay(items);
            dialog.PrimaryButton.Content = "OK";
            dialog.SecondaryButton.Visibility = Visibility.Collapsed;
            return dialog;
        }

        public static bool? ShowConfirmation(
            string title,
            string headerText,
            string message,
            string footerText
        )
        {
            var dialog = CreateConfirmationDialog(title, headerText, message, footerText);
            return dialog.ShowDialog();
        }

        public static bool? ShowConfirmation(
            string title,
            string headerText,
            IEnumerable<string> items,
            string footerText
        )
        {
            var dialog = CreateConfirmationDialog(title, headerText, items, footerText);
            return dialog.ShowDialog();
        }

        public static void ShowInformation(
            string title,
            string headerText,
            string message,
            string footerText
        )
        {
            var dialog = CreateInformationDialog(title, headerText, message, footerText);
            dialog.ShowDialog();
        }

        public static void ShowInformation(
            string title,
            string headerText,
            IEnumerable<string> items,
            string footerText
        )
        {
            var dialog = CreateInformationDialog(title, headerText, items, footerText);
            dialog.ShowDialog();
        }

        public static CustomDialog CreateYesNoCancelDialog(
            string title,
            string headerText,
            string message,
            string footerText
        )
        {
            var dialog = CreateBaseDialog(title, headerText, footerText);
            dialog.SetupSimpleMessageDisplay(message);
            dialog.PrimaryButton.Content = "Yes";
            dialog.SecondaryButton.Content = "No";
            dialog.TertiaryButton.Content = "Cancel";
            dialog.TertiaryButton.Visibility = Visibility.Visible;
            dialog.TertiaryButton.IsCancel = true;
            return dialog;
        }

        public static CustomDialog CreateYesNoCancelDialog(
            string title,
            string headerText,
            IEnumerable<string> items,
            string footerText
        )
        {
            var dialog = CreateBaseDialog(title, headerText, footerText);
            dialog.SetupAppListDisplay(items);
            dialog.PrimaryButton.Content = "Yes";
            dialog.SecondaryButton.Content = "No";
            dialog.TertiaryButton.Content = "Cancel";
            dialog.TertiaryButton.Visibility = Visibility.Visible;
            dialog.TertiaryButton.IsCancel = true;
            return dialog;
        }

        public static bool? ShowYesNoCancel(
            string title,
            string headerText,
            string message,
            string footerText
        )
        {
            var dialog = CreateYesNoCancelDialog(title, headerText, message, footerText);

            // Add event handler for the Closing event to ensure DialogResult is set correctly
            dialog.Closing += (sender, e) =>
            {
                // If DialogResult is not explicitly set (e.g., if the dialog is closed by clicking outside or pressing Escape),
                // set it to null to indicate Cancel
                if (dialog.DialogResult == null) { }
            };

            // Add event handler for the KeyDown event to handle Escape key
            dialog.KeyDown += (sender, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    dialog.DialogResult = null;
                    dialog.Close();
                }
            };

            // Show the dialog and get the result
            var result = dialog.ShowDialog();

            return result;
        }

        public static bool? ShowYesNoCancel(
            string title,
            string headerText,
            IEnumerable<string> items,
            string footerText
        )
        {
            var dialog = CreateYesNoCancelDialog(title, headerText, items, footerText);

            // Add event handler for the Closing event to ensure DialogResult is set correctly
            dialog.Closing += (sender, e) =>
            {
                // If DialogResult is not explicitly set (e.g., if the dialog is closed by clicking outside or pressing Escape),
                // set it to null to indicate Cancel
                if (dialog.DialogResult == null) { }
            };

            // Add event handler for the KeyDown event to handle Escape key
            dialog.KeyDown += (sender, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    dialog.DialogResult = null;
                    dialog.Close();
                }
            };

            // Show the dialog and get the result
            var result = dialog.ShowDialog();

            return result;
        }

        public static (bool Confirmed, bool CheckboxChecked)? ShowConfirmationWithCheckbox(
            string title,
            string message,
            string? checkboxText = null,
            string continueButtonText = "Continue",
            string cancelButtonText = "Cancel",
            string? titleBarIcon = null)
        {
            var dialog = CreateBaseDialog(title, title, "", DialogType.Question, titleBarIcon);
            dialog.SetupSimpleMessageDisplay(message);

            if (!string.IsNullOrEmpty(checkboxText))
            {
                dialog.OptionCheckbox.Content = checkboxText;
                dialog.OptionCheckbox.Visibility = Visibility.Visible;
                dialog.OptionCheckbox.IsChecked = true;
            }
            else
            {
                dialog.OptionCheckbox.Visibility = Visibility.Collapsed;
            }

            dialog.PrimaryButton.Content = continueButtonText;
            dialog.SecondaryButton.Content = cancelButtonText;

            var result = dialog.ShowDialog();

            if (result == true)
            {
                return (true, dialog.OptionCheckbox.IsChecked == true);
            }
            else
            {
                return (false, false);
            }
        }

        public static bool ShowInformationWithCheckbox(
            string title,
            string headerText,
            string message,
            string checkboxText,
            string buttonText = "OK",
            DialogType dialogType = DialogType.Information,
            string? titleBarIcon = null)
        {
            var dialog = CreateBaseDialog(title, headerText, "", dialogType, titleBarIcon);
            dialog.SetupSimpleMessageDisplay(message);

            dialog.OptionCheckbox.Content = checkboxText;
            dialog.OptionCheckbox.Visibility = Visibility.Visible;
            dialog.OptionCheckbox.IsChecked = true;

            dialog.PrimaryButton.Content = buttonText;
            dialog.SecondaryButton.Visibility = Visibility.Collapsed;

            dialog.ShowDialog();

            return dialog.OptionCheckbox.IsChecked == true;
        }

        public static CustomDialog CreateAppOperationConfirmationDialog(
            string title,
            string contextMessage,
            IEnumerable<string> items,
            DialogType dialogType = DialogType.Question,
            string? titleBarIcon = null)
        {
            var dialog = CreateBaseDialog(title, title, "", dialogType, titleBarIcon);
            dialog.SetupAppListDisplayWithMessage(contextMessage, items);
            dialog.PrimaryButton.Content = "Yes";
            dialog.SecondaryButton.Content = "No";
            return dialog;
        }
    }
}
