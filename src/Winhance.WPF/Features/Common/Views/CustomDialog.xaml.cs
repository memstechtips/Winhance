using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Winhance.WPF.Features.Common.Views
{
    public partial class CustomDialog : Window
    {
        public int AppListColumns { get; set; } = 4;

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
            
            // Add debug logging
            System.Diagnostics.Debug.WriteLine("[DIALOG DEBUG] TertiaryButton (Cancel) clicked - DialogResult set to null");
            
            Close();
        }

        public static CustomDialog CreateConfirmationDialog(string title, string headerText, IEnumerable<string> apps, string footerText)
        {
            var dialog = new CustomDialog
            {
                Title = title
            };

            dialog.DialogIcon.Source = Application.Current.Resources["QuestionIcon"] as ImageSource;
            dialog.HeaderText.Text = headerText;
            dialog.AppList.ItemsSource = apps;
            dialog.FooterText.Text = footerText;

            dialog.PrimaryButton.Content = "Yes";
            dialog.SecondaryButton.Content = "No";

            return dialog;
        }

        public static CustomDialog CreateInformationDialog(string title, string headerText, IEnumerable<string> apps, string footerText, bool useMultiColumnLayout = false)
        {
            var dialog = new CustomDialog
            {
                Title = title
            };

            dialog.DialogIcon.Source = Application.Current.Resources["InfoIcon"] as ImageSource;
            dialog.HeaderText.Text = headerText;
            dialog.AppList.ItemsSource = apps;
            dialog.FooterText.Text = footerText;

            dialog.PrimaryButton.Content = "OK";
            dialog.SecondaryButton.Visibility = Visibility.Collapsed;

            return dialog;
        }

        public static bool? ShowConfirmation(string title, string headerText, IEnumerable<string> apps, string footerText)
        {
            var dialog = CreateConfirmationDialog(title, headerText, apps, footerText);
            return dialog.ShowDialog();
        }

        public static void ShowInformation(string title, string headerText, IEnumerable<string> apps, string footerText)
        {
            var dialog = CreateInformationDialog(title, headerText, apps, footerText);
            dialog.ShowDialog();
        }

        public static CustomDialog CreateYesNoCancelDialog(string title, string headerText, IEnumerable<string> apps, string footerText)
        {
            var dialog = new CustomDialog
            {
                Title = title
            };

            dialog.DialogIcon.Source = Application.Current.Resources["QuestionIcon"] as ImageSource;
            dialog.HeaderText.Text = headerText;
            dialog.AppList.ItemsSource = apps;
            dialog.FooterText.Text = footerText;

            dialog.PrimaryButton.Content = "Yes";
            dialog.SecondaryButton.Content = "No";
            dialog.TertiaryButton.Content = "Cancel";
            
            // Ensure the Cancel button is visible and properly styled
            dialog.TertiaryButton.Visibility = Visibility.Visible;
            dialog.TertiaryButton.IsCancel = true;
            
            // Add debug logging for button visibility
            System.Diagnostics.Debug.WriteLine($"[DIALOG DEBUG] TertiaryButton Visibility: {dialog.TertiaryButton.Visibility}");
            System.Diagnostics.Debug.WriteLine($"[DIALOG DEBUG] TertiaryButton IsCancel: {dialog.TertiaryButton.IsCancel}");

            return dialog;
        }

        public static bool? ShowYesNoCancel(string title, string headerText, IEnumerable<string> apps, string footerText)
        {
            var dialog = CreateYesNoCancelDialog(title, headerText, apps, footerText);
            
            // Add event handler for the Closing event to ensure DialogResult is set correctly
            dialog.Closing += (sender, e) => 
            {
                // If DialogResult is not explicitly set (e.g., if the dialog is closed by clicking outside or pressing Escape),
                // set it to null to indicate Cancel
                if (dialog.DialogResult == null)
                {
                    System.Diagnostics.Debug.WriteLine("[DIALOG DEBUG] Dialog closing without explicit DialogResult - setting to null (Cancel)");
                }
            };
            
            // Add event handler for the KeyDown event to handle Escape key
            dialog.KeyDown += (sender, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    System.Diagnostics.Debug.WriteLine("[DIALOG DEBUG] Escape key pressed - setting DialogResult to null (Cancel)");
                    dialog.DialogResult = null;
                    dialog.Close();
                }
            };
            
            // Show the dialog and get the result
            var result = dialog.ShowDialog();
            
            // Log the result
            System.Diagnostics.Debug.WriteLine($"[DIALOG DEBUG] ShowYesNoCancel result: {(result == true ? "Yes" : result == false ? "No" : "Cancel")}");
            
            return result;
        }
    }
}
