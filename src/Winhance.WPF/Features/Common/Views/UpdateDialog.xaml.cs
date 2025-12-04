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
    public partial class UpdateDialog : Window, System.ComponentModel.INotifyPropertyChanged
    {
        private readonly VersionInfo _currentVersion;
        private readonly VersionInfo _latestVersion;
        private readonly Func<Task> _downloadAndInstallAction;

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

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        private UpdateDialog(VersionInfo currentVersion, VersionInfo latestVersion, Func<Task> downloadAndInstallAction)
        {
            InitializeComponent();
            DataContext = this;

            _currentVersion = currentVersion;
            _latestVersion = latestVersion;
            _downloadAndInstallAction = downloadAndInstallAction;

            try
            {
                if (Application.Current.Resources.Contains("IsDarkTheme"))
                {
                    IsThemeDark = (bool)Application.Current.Resources["IsDarkTheme"];
                }
                else
                {
                    bool systemUsesLightTheme = false;

                    try
                    {
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
                    }

                    IsThemeDark = !systemUsesLightTheme;
                }
            }
            catch (Exception)
            {
                IsThemeDark = true;
            }

            this.Loaded += (sender, e) =>
            {
                if (Application.Current.MainWindow?.DataContext is ViewModels.MainViewModel mainViewModel)
                {
                    mainViewModel.IsDialogOverlayVisible = true;
                }

                if (Application.Current.Resources is System.Windows.ResourceDictionary resourceDictionary)
                {
                    var eventInfo = resourceDictionary.GetType().GetEvent("ResourceDictionaryChanged");
                    if (eventInfo != null)
                    {
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

                        eventInfo.AddEventHandler(resourceDictionary, resourceChangedHandler);
                    }
                }
            };

            Closed += (s, e) =>
            {
                if (Application.Current.MainWindow?.DataContext is ViewModels.MainViewModel mainViewModel)
                {
                    mainViewModel.IsDialogOverlayVisible = false;
                }
            };

            CurrentVersionText.Text = currentVersion.Version;
            LatestVersionText.Text = latestVersion.Version;
        }

        private async void PrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PrimaryButton.IsEnabled = false;
                SecondaryButton.IsEnabled = false;

                IsDownloading = true;
                OnPropertyChanged(nameof(IsDownloading));
                DownloadProgress.Visibility = Visibility.Visible;
                var localization = Winhance.WPF.Features.Common.Services.LocalizationManager.Instance;
                StatusText.Text = localization["Dialog_Update_Status_Downloading"];

                FooterText.Visibility = Visibility.Collapsed;

                await _downloadAndInstallAction();

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                PrimaryButton.IsEnabled = true;
                SecondaryButton.IsEnabled = true;

                IsDownloading = false;
                OnPropertyChanged(nameof(IsDownloading));
                DownloadProgress.Visibility = Visibility.Collapsed;

                var localization = Winhance.WPF.Features.Common.Services.LocalizationManager.Instance;
                string errorFormat = localization["Dialog_Update_Status_Error"];
                StatusText.Text = string.Format(errorFormat, ex.Message);

                FooterText.Visibility = Visibility.Visible;
            }
        }

        private void SecondaryButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public static async Task<bool> ShowAsync(
            VersionInfo currentVersion,
            VersionInfo latestVersion,
            Func<Task> downloadAndInstallAction)
        {
            try
            {
                var dialog = new UpdateDialog(currentVersion, latestVersion, downloadAndInstallAction)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ShowInTaskbar = false,
                    Topmost = true
                };

                if (Application.Current.MainWindow != null && Application.Current.MainWindow != dialog)
                {
                    dialog.Owner = Application.Current.MainWindow;
                }
                else
                {
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window != dialog && window.IsVisible)
                        {
                            dialog.Owner = window;
                            break;
                        }
                    }
                }

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
