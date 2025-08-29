using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Resources.Theme;

namespace Winhance.WPF.Features.Common.Views
{
    /// <summary>
    /// Update dialog that shows when a new version is available
    /// </summary>
    public partial class UpdateDialog : Window, System.ComponentModel.INotifyPropertyChanged
    {
        private readonly VersionInfo _currentVersion;
        private readonly VersionInfo _latestVersion;
        private readonly Func<Task> _downloadAndInstallAction;

        // Add a property for theme binding that defaults to dark theme
        private bool _isThemeDark = true;
        public bool IsThemeDark
        {
            get => _isThemeDark;
            set
            {
                if (_isThemeDark != value)
                {
                    _isThemeDark = value;
                    OnPropertyChanged(nameof(IsThemeDark));
                }
            }
        }

        public bool IsDownloading { get; private set; }

        // Implement INotifyPropertyChanged
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        private UpdateDialog(string title, string message, VersionInfo currentVersion, VersionInfo latestVersion, Func<Task> downloadAndInstallAction)
        {
            InitializeComponent();
            DataContext = this;

            _currentVersion = currentVersion;
            _latestVersion = latestVersion;
            _downloadAndInstallAction = downloadAndInstallAction;

            // Try to determine the Winhance theme
            try
            {
                // First check if the theme is stored in Application.Current.Resources
                if (Application.Current.Resources.Contains("IsDarkTheme"))
                {
                    IsThemeDark = (bool)Application.Current.Resources["IsDarkTheme"];
                }
                else
                {
                    // Fall back to system theme if Winhance theme is not available
                    bool systemUsesLightTheme = false;

                    try
                    {
                        // Try to read the Windows registry to determine the system theme
                        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                        {
                            if (key != null)
                            {
                                var value = key.GetValue("AppsUseLightTheme");
                                if (value != null)
                                {
                                    systemUsesLightTheme = Convert.ToInt32(value) == 1;
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore errors reading the registry
                    }

                    // Set the IsThemeDark property based on the system theme
                    IsThemeDark = !systemUsesLightTheme;
                }
            }
            catch (Exception)
            {
                // Default to dark theme if we can't determine the theme
                IsThemeDark = true;
            }

            // Set up a handler to listen for theme changes
            this.Loaded += (sender, e) =>
            {
                // Listen for resource dictionary changes that might affect the theme
                if (Application.Current.Resources is System.Windows.ResourceDictionary resourceDictionary)
                {
                    // Use reflection to get the event
                    var eventInfo = resourceDictionary.GetType().GetEvent("ResourceDictionaryChanged");
                    if (eventInfo != null)
                    {
                        // Create a handler for the event
                        EventHandler resourceChangedHandler = (s, args) =>
                        {
                            if (Application.Current.Resources.Contains("IsDarkTheme"))
                            {
                                bool newIsDarkTheme = (bool)Application.Current.Resources["IsDarkTheme"];
                                if (IsThemeDark != newIsDarkTheme)
                                {
                                    IsThemeDark = newIsDarkTheme;
                                }
                            }
                        };

                        // Add the handler to the event
                        eventInfo.AddEventHandler(resourceDictionary, resourceChangedHandler);
                    }
                }
            };

            Title = title;
            HeaderText.Text = title;
            MessageText.Text = message;

            // Ensure version text is properly displayed
            CurrentVersionText.Text = currentVersion.Version;
            LatestVersionText.Text = latestVersion.Version;

            // Make sure the footer text is visible
            FooterText.Visibility = Visibility.Visible;
        }

        private async void PrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable buttons during download
                PrimaryButton.IsEnabled = false;
                SecondaryButton.IsEnabled = false;

                // Show progress indicator
                IsDownloading = true;
                OnPropertyChanged(nameof(IsDownloading));
                DownloadProgress.Visibility = Visibility.Visible;
                StatusText.Text = "Downloading update...";

                // Hide the footer text during download
                FooterText.Visibility = Visibility.Collapsed;

                // Execute the download and install action
                await _downloadAndInstallAction();

                // Set dialog result and close
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                // Re-enable buttons
                PrimaryButton.IsEnabled = true;
                SecondaryButton.IsEnabled = true;

                // Hide progress indicator
                IsDownloading = false;
                OnPropertyChanged(nameof(IsDownloading));
                DownloadProgress.Visibility = Visibility.Collapsed;

                // Show error message
                StatusText.Text = $"Error downloading update: {ex.Message}";

                // Show the footer text again
                FooterText.Visibility = Visibility.Visible;
            }
        }

        private void SecondaryButton_Click(object sender, RoutedEventArgs e)
        {
            // User chose to be reminded later
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Shows an update dialog with the specified parameters
        /// </summary>
        /// <param name="title">The title of the dialog</param>
        /// <param name="message">The message to display</param>
        /// <param name="currentVersion">The current version information</param>
        /// <param name="latestVersion">The latest version information</param>
        /// <param name="downloadAndInstallAction">The action to execute when the user clicks Download & Install</param>
        /// <returns>True if the user chose to download and install, false otherwise</returns>
        public static async Task<bool> ShowAsync(
            string title,
            string message,
            VersionInfo currentVersion,
            VersionInfo latestVersion,
            Func<Task> downloadAndInstallAction)
        {
            try
            {
                var dialog = new UpdateDialog(title, message, currentVersion, latestVersion, downloadAndInstallAction)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ShowInTaskbar = false,
                    Topmost = true
                };

                // Set the owner to the main window to ensure it appears on top
                if (Application.Current.MainWindow != null && Application.Current.MainWindow != dialog)
                {
                    dialog.Owner = Application.Current.MainWindow;
                }
                else
                {
                    // Try to find the main window another way
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window != dialog && window.IsVisible)
                        {
                            dialog.Owner = window;
                            break;
                        }
                    }
                }

                // Show the dialog and wait for the result
                return await Task.Run(() =>
                {
                    return Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            return dialog.ShowDialog() ?? false;
                        }
                        catch (Exception ex)
                        {
                            return false;
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
