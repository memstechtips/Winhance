using System;
using System.Threading.Tasks;
using System.Windows;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Common.Views
{
    /// <summary>
    /// Update notification dialog that shows when a new version is available
    /// </summary>
    public partial class UpdateNotificationDialog : Window
    {
        // Default text content for the update dialog
        private static readonly string DefaultTitle = "Update Available";
        private static readonly string DefaultUpdateMessage = "A new version of Winhance is available.";
        
        public UpdateNotificationDialog(UpdateNotificationViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
        
        private void PrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            // Download and install
            if (DataContext is UpdateNotificationViewModel viewModel)
            {
                viewModel.DownloadAndInstallUpdateCommand.Execute(null);
            }
        }

        private void SecondaryButton_Click(object sender, RoutedEventArgs e)
        {
            // Remind later
            if (DataContext is UpdateNotificationViewModel viewModel)
            {
                viewModel.RemindLaterCommand.Execute(null);
            }
            DialogResult = false;
            Close();
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Close without action
            DialogResult = false;
            Close();
        }
        
        /// <summary>
        /// Creates and shows an update notification dialog with the specified parameters.
        /// This method ensures the dialog is properly modal and blocks the main window.
        /// </summary>
        /// <param name="viewModel">The view model containing update information</param>
        /// <param name="title">The dialog title (optional - uses default if empty)</param>
        /// <param name="updateMessage">The update message (optional - uses default if empty)</param>
        /// <returns>The dialog instance with DialogResult set</returns>
        public static async Task<UpdateNotificationDialog> ShowUpdateDialogAsync(
            UpdateNotificationViewModel viewModel,
            string title = "",
            string updateMessage = "")
        {
            try
            {
                var dialog = new UpdateNotificationDialog(viewModel)
                {
                    Title = string.IsNullOrEmpty(title) ? DefaultTitle : title,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ShowInTaskbar = false,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true
                };

                // Set the update message
                dialog.UpdateMessageText.Text = string.IsNullOrEmpty(updateMessage) ? DefaultUpdateMessage : updateMessage;
                
                // Set button content
                dialog.PrimaryButton.Content = "Download and Install";
                dialog.SecondaryButton.Content = "Remind Later";

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
                MessageBox.Show($"Error showing update dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // Create a dummy dialog with error result
                var errorDialog = new UpdateNotificationDialog(viewModel);
                errorDialog.DialogResult = false;
                return errorDialog;
            }
        }
    }
}
