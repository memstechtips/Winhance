using System;
using System.Threading.Tasks;
using System.Windows;

namespace Winhance.WPF.Features.Common.Views
{
    /// <summary>
    /// Donation dialog that extends CustomDialog with a "Don't show this again" checkbox
    /// </summary>
    public partial class DonationDialog : Window
    {
        // Default text content for the donation dialog
        private static readonly string DefaultTitle = "Support Winhance";
        private static readonly string DefaultSupportMessage = "Your support helps keep this project going!";
        private static readonly string DefaultFooterText = "Click 'Yes' to show your support!";

        public bool DontShowAgain => DontShowAgainCheckBox.IsChecked ?? false;

        public DonationDialog()
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

        /// <summary>
        /// Creates and shows a donation dialog with the specified parameters.
        /// This method ensures the dialog is properly modal and blocks the main window.
        /// </summary>
        /// <param name="title">The dialog title (optional - uses default if null)</param>
        /// <param name="supportMessage">The support message (optional - uses default if null)</param>
        /// <param name="footerText">The footer text (optional - uses default if null)</param>
        /// <returns>The dialog instance with DialogResult set</returns>
        public static async Task<DonationDialog> ShowDonationDialogAsync(string title = null, string supportMessage = null, string footerText = null)
        {
            try
            {
                var dialog = new DonationDialog
                {
                    Title = title ?? DefaultTitle,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ShowInTaskbar = false,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true
                };

                // Set the support message and footer text
                dialog.SupportMessageText.Text = supportMessage ?? DefaultSupportMessage;
                dialog.FooterText.Text = footerText ?? DefaultFooterText;
                
                // Set button content
                dialog.PrimaryButton.Content = "Yes";
                dialog.SecondaryButton.Content = "No";

                // Set the owner to the main window to ensure it appears on top
                if (Application.Current.MainWindow != null && Application.Current.MainWindow != dialog)
                {
                    dialog.Owner = Application.Current.MainWindow;
                    dialog.Topmost = true; // Ensure it stays on top
                }
                else
                {
                    // Try to find the main window another way
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window != dialog && window.IsVisible)
                        {
                            dialog.Owner = window;
                            dialog.Topmost = true;
                            break;
                        }
                    }
                }

                // Make the dialog visible and focused
                dialog.Visibility = Visibility.Visible;
                dialog.Activate();
                dialog.Focus();
                
                // Show the dialog and wait for it to complete
                dialog.ShowDialog();
                
                // Return the dialog
                return dialog;
            }
            catch (Exception ex)
            {
                // Show error message
                MessageBox.Show($"Error showing donation dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // Create a dummy dialog with error result
                var errorDialog = new DonationDialog();
                errorDialog.DialogResult = false;
                return errorDialog;
            }
        }
    }
}