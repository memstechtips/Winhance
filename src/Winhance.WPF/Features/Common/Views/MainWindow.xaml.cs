using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Resources.Theme;
using Winhance.WPF.Features.Common.Services;
using Winhance.WPF.Features.Common.Utilities;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Common.Views
{
    public partial class MainWindow : Window, IThemeAwareWindow
    {
        private WindowIconService _windowIconService;
        private readonly IApplicationCloseService _applicationCloseService;

        public MainWindow(IApplicationCloseService applicationCloseService)
        {
            InitializeComponent();
            _applicationCloseService = applicationCloseService;

            this.PreviewMouseWheel += MainWindow_PreviewMouseWheel;
            Loaded += (s, e) => UpdateThemeIcon();
            this.Closing += MainWindow_Closing;
        }

        private async void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            await _applicationCloseService.CheckOperationsAndCloseAsync();
        }

        public void OnThemeChanged(bool isDarkTheme)
        {
            UpdateThemeIcon();
        }

        private void MainWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = VisualTreeHelpers.FindVisualChild<ScrollViewer>(this);
            if (scrollViewer != null)
            {
                if (e.Delta < 0)
                    scrollViewer.LineDown();
                else
                    scrollViewer.LineUp();

                e.Handled = true;
            }
        }

        private void MoreMenuOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel mainViewModel)
                mainViewModel.CloseMoreMenuFlyout();
        }

        private void MoreMenuOverlay_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && DataContext is MainViewModel mainViewModel)
            {
                mainViewModel.CloseMoreMenuFlyout();
                e.Handled = true;
            }
        }

        private void AdvancedToolsOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel mainViewModel)
                mainViewModel.CloseAdvancedToolsFlyout();
        }

        private void AdvancedToolsOverlay_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && DataContext is MainViewModel mainViewModel)
            {
                mainViewModel.CloseAdvancedToolsFlyout();
                e.Handled = true;
            }
        }

        private void UpdateThemeIcon()
        {
            if (_windowIconService == null)
            {
                var app = Application.Current as App;
                var themeManager = app?.ServiceProvider.GetService(typeof(IThemeManager)) as IThemeManager;
                if (themeManager != null)
                    _windowIconService = new WindowIconService(themeManager);
            }

            _windowIconService?.UpdateTitleBarIcon(AppIconImage);
        }
    }
}
