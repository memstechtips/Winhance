using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Winhance.WPF.Features.Common.Views
{
    public partial class CustomDialog : Window
    {

        public CustomDialog()
        {
            InitializeComponent();
            DataContext = this;
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
            string headerText,
            string message,
            string footerText
        )
        {
            var dialog = new CustomDialog { Title = title };

            dialog.HeaderText.Text = headerText;
            dialog.MessageContent.Text = message;
            dialog.FooterText.Text = footerText;

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
            string message = items != null ? string.Join(Environment.NewLine, items) : string.Empty;
            return CreateConfirmationDialog(title, headerText, message, footerText);
        }

        public static CustomDialog CreateInformationDialog(
            string title,
            string headerText,
            string message,
            string footerText
        )
        {
            var dialog = new CustomDialog { Title = title };

            dialog.HeaderText.Text = headerText;
            dialog.MessageContent.Text = message;
            dialog.FooterText.Text = footerText;

            dialog.PrimaryButton.Content = "OK";
            dialog.SecondaryButton.Visibility = Visibility.Collapsed;

            return dialog;
        }

        public static CustomDialog CreateInformationDialog(
            string title,
            string headerText,
            IEnumerable<string> items,
            string footerText,
            bool useMultiColumnLayout = false
        )
        {
            string message = items != null ? string.Join(Environment.NewLine, items) : string.Empty;
            return CreateInformationDialog(title, headerText, message, footerText);
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
            var dialog = new CustomDialog { Title = title };

            dialog.HeaderText.Text = headerText;
            dialog.MessageContent.Text = message;
            dialog.FooterText.Text = footerText;

            dialog.PrimaryButton.Content = "Yes";
            dialog.SecondaryButton.Content = "No";
            dialog.TertiaryButton.Content = "Cancel";

            // Ensure the Cancel button is visible and properly styled
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
            string message = items != null ? string.Join(Environment.NewLine, items) : string.Empty;
            return CreateYesNoCancelDialog(title, headerText, message, footerText);
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
            string cancelButtonText = "Cancel")
        {
            var dialog = new CustomDialog { Title = title };

            dialog.HeaderText.Text = title;
            dialog.MessageContent.Text = message;
            dialog.FooterText.Text = "";

            // Configure checkbox if provided
            if (!string.IsNullOrEmpty(checkboxText))
            {
                dialog.OptionCheckbox.Content = checkboxText;
                dialog.OptionCheckbox.Visibility = Visibility.Visible;
                dialog.OptionCheckbox.IsChecked = false; // Default unchecked
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
    }
}
