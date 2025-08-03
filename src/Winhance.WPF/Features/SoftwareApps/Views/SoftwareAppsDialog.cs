using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Winhance.WPF.Features.SoftwareApps.Views
{
    public partial class SoftwareAppsDialog : Window
    {
        public int AppListColumns { get; set; } = 4;

        public SoftwareAppsDialog()
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

        public static SoftwareAppsDialog CreateConfirmationDialog(
            string title,
            string headerText,
            IEnumerable<string> apps,
            string footerText
        )
        {
            var dialog = new SoftwareAppsDialog { Title = title };

            dialog.HeaderText.Text = headerText;
            dialog.AppList.ItemsSource = apps;
            dialog.FooterText.Text = footerText;

            dialog.PrimaryButton.Content = "Yes";
            dialog.SecondaryButton.Content = "No";

            return dialog;
        }

        public static SoftwareAppsDialog CreateInformationDialog(
            string title,
            string headerText,
            IEnumerable<string> apps,
            string footerText,
            bool useMultiColumnLayout = false
        )
        {
            var dialog = new SoftwareAppsDialog { Title = title };

            dialog.HeaderText.Text = headerText;
            dialog.AppList.ItemsSource = apps;
            dialog.FooterText.Text = footerText;

            dialog.PrimaryButton.Content = "OK";
            dialog.SecondaryButton.Visibility = Visibility.Collapsed;

            return dialog;
        }

        public static bool? ShowConfirmationAsync(
            string title,
            string headerText,
            IEnumerable<string> apps,
            string footerText
        )
        {
            var dialog = CreateConfirmationDialog(title, headerText, apps, footerText);
            return dialog.ShowDialog();
        }

        public static void ShowInformationAsync(
            string title,
            string headerText,
            IEnumerable<string> apps,
            string footerText
        )
        {
            var dialog = CreateInformationDialog(title, headerText, apps, footerText);
            dialog.ShowDialog();
        }

        public static SoftwareAppsDialog CreateYesNoCancelDialog(
            string title,
            string headerText,
            IEnumerable<string> apps,
            string footerText
        )
        {
            var dialog = new SoftwareAppsDialog { Title = title };

            dialog.HeaderText.Text = headerText;
            dialog.AppList.ItemsSource = apps;
            dialog.FooterText.Text = footerText;

            dialog.PrimaryButton.Content = "Yes";
            dialog.SecondaryButton.Content = "No";
            dialog.TertiaryButton.Content = "Cancel";

            // Ensure the Cancel button is visible and properly styled
            dialog.TertiaryButton.Visibility = Visibility.Visible;
            dialog.TertiaryButton.IsCancel = true;

            return dialog;
        }

        public static bool? ShowYesNoCancel(
            string title,
            string headerText,
            IEnumerable<string> apps,
            string footerText
        )
        {
            var dialog = CreateYesNoCancelDialog(title, headerText, apps, footerText);

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
    }
}
