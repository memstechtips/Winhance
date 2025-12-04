using System;
using System.Threading.Tasks;
using System.Windows;

namespace Winhance.WPF.Features.Common.Views
{
    public partial class DonationDialog : Window
    {
        public bool DontShowAgain => DontShowAgainCheckBox.IsChecked ?? false;

        public DonationDialog()
        {
            InitializeComponent();
            DataContext = this;

            var localization = Winhance.WPF.Features.Common.Services.LocalizationManager.Instance;
            Title = localization["Dialog_Donation_Title"];

            Loaded += (s, e) =>
            {
                if (Application.Current.MainWindow?.DataContext is ViewModels.MainViewModel mainViewModel)
                {
                    mainViewModel.IsDialogOverlayVisible = true;
                }
            };

            Closed += (s, e) =>
            {
                if (Application.Current.MainWindow?.DataContext is ViewModels.MainViewModel mainViewModel)
                {
                    mainViewModel.IsDialogOverlayVisible = false;
                }
            };
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

        public static async Task<DonationDialog> ShowDonationDialogAsync(string title = null, string supportMessage = null)
        {
            try
            {
                var localization = Winhance.WPF.Features.Common.Services.LocalizationManager.Instance;

                var dialog = new DonationDialog
                {
                    Title = title ?? localization["Dialog_Donation_Title"],
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ShowInTaskbar = false,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true
                };

                if (supportMessage != null)
                {
                    dialog.SupportMessageText.Text = supportMessage;
                }

                if (Application.Current.MainWindow != null && Application.Current.MainWindow != dialog)
                {
                    dialog.Owner = Application.Current.MainWindow;
                    dialog.Topmost = true;
                }
                else
                {
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

                dialog.Visibility = Visibility.Visible;
                dialog.Activate();
                dialog.Focus();
                dialog.ShowDialog();

                return dialog;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error showing donation dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                var errorDialog = new DonationDialog();
                errorDialog.DialogResult = false;
                return errorDialog;
            }
        }
    }
}