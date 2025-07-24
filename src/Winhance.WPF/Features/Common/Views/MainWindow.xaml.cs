using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Messaging;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Resources.Theme;
using Winhance.WPF.Features.Common.Services;
using Winhance.WPF.Features.Common.Utilities;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Common.Views
{
    public partial class MainWindow : Window
    {
        private readonly Winhance.Core.Features.Common.Interfaces.INavigationService _navigationService =
            null!;
        private WindowSizeManager _windowSizeManager;
        private readonly UserPreferencesService _userPreferencesService;
        private readonly IApplicationCloseService _applicationCloseService;





        public MainWindow()
        {
            try
            {
                InitializeComponent();

                this.MinimizeButton.Click += (s, e) => this.WindowState = WindowState.Minimized;
                this.MaximizeRestoreButton.Click += (s, e) =>
                {
                    this.WindowState =
                        (this.WindowState == WindowState.Maximized)
                            ? WindowState.Normal
                            : WindowState.Maximized;
                };
                this.CloseButton.Click += CloseButton_Click;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public MainWindow(
            IThemeManager themeManager,
            IServiceProvider serviceProvider,
            IMessengerService messengerService,
            Winhance.Core.Features.Common.Interfaces.INavigationService navigationService,
            IVersionService versionService,
            UserPreferencesService userPreferencesService,
            IApplicationCloseService applicationCloseService
        )
        {
            try
            {

                if (themeManager == null)
                    throw new ArgumentNullException(nameof(themeManager));
                if (serviceProvider == null)
                    throw new ArgumentNullException(nameof(serviceProvider));
                if (messengerService == null)
                    throw new ArgumentNullException(nameof(messengerService));
                if (navigationService == null)
                    throw new ArgumentNullException(nameof(navigationService));
                if (versionService == null)
                    throw new ArgumentNullException(nameof(versionService));
                if (userPreferencesService == null)
                    throw new ArgumentNullException(nameof(userPreferencesService));

                InitializeComponent();

                _themeManager = themeManager;
                _serviceProvider = serviceProvider;
                _messengerService = messengerService;
                _navigationService = navigationService;
                _versionService = versionService;
                _userPreferencesService = userPreferencesService;
                _applicationCloseService = applicationCloseService;

                try
                {
                    var logService =
                        _serviceProvider.GetService(typeof(ILogService)) as ILogService;

                    if (userPreferencesService != null && logService != null)
                    {
                        _windowSizeManager = new WindowSizeManager(
                            this,
                            userPreferencesService,
                            logService
                        );
                    }
                }
                catch (Exception ex)
                {
                    throw;
                }

                this.PreviewMouseWheel += MainWindow_PreviewMouseWheel;

                this.Loaded += (sender, e) =>
                {
                    if (DataContext is MainViewModel mainViewModel)
                    {
                        try
                        {
                            _navigationService.NavigateTo("SoftwareApps");
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                _navigationService.NavigateTo("About");
                            }
                            catch (Exception fallbackEx)
                            {
                                throw;
                            }
                        }
                    }
                };

                this.StateChanged += (sender, e) =>
                {
                    if (DataContext is MainViewModel viewModel)
                    {
                        viewModel.MaximizeButtonContent =
                            (this.WindowState == WindowState.Maximized)
                                ? "WindowRestore"
                                : "WindowMaximize";
                    }
                };

                _messengerService.Register<WindowStateMessage>(this, HandleWindowStateMessage);

                this.Closed += (sender, e) =>
                {
                    _messengerService.Unregister(this);
                };
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private async void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _applicationCloseService.CloseApplicationWithSupportDialogAsync();
            }
            catch (Exception ex)
            {
                try
                {
                    this.Close();
                }
                catch
                {
                    Application.Current.Shutdown();
                }
            }
        }

        private readonly IMessengerService _messengerService = null!;
        private readonly IVersionService _versionService = null!;

        #region More Button Event Handlers

        private void MoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.SelectedNavigationItem = "More";
            }

            if (MoreMenuControl != null)
            {
                MoreMenuControl.ShowMenu(MoreButton);
            }
        }

        #endregion

        private void HandleWindowStateMessage(WindowStateMessage message)
        {
            switch (message.Action)
            {
                case WindowStateMessage.WindowStateAction.Minimize:
                    WindowState = WindowState.Minimized;
                    break;

                case WindowStateMessage.WindowStateAction.Maximize:
                    WindowState = WindowState.Maximized;
                    break;

                case WindowStateMessage.WindowStateAction.Restore:
                    WindowState = WindowState.Normal;
                    break;

                case WindowStateMessage.WindowStateAction.Close:
                    Close();
                    break;
            }
        }

        private readonly IServiceProvider _serviceProvider = null!;
        private readonly IThemeManager _themeManager = null!;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var helper = new WindowInteropHelper(this);
            if (helper.Handle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Window handle not available");
            }

            if (_windowSizeManager != null)
            {
                _windowSizeManager.Initialize();
            }
            else
            {
                SetDynamicWindowSize();
            }

            EnableBlur();
        }

        private void SetDynamicWindowSize()
        {
            var workArea = GetCurrentScreenWorkArea();

            double dpiScaleX = 1.0;
            double dpiScaleY = 1.0;

            try
            {
                var presentationSource = PresentationSource.FromVisual(this);
                if (presentationSource?.CompositionTarget != null)
                {
                    dpiScaleX = presentationSource.CompositionTarget.TransformToDevice.M11;
                    dpiScaleY = presentationSource.CompositionTarget.TransformToDevice.M22;
                }
            }
            catch
            {
            }

            double screenWidth = workArea.Width / dpiScaleX;
            double screenHeight = workArea.Height / dpiScaleY;
            double screenLeft = workArea.Left / dpiScaleX;
            double screenTop = workArea.Top / dpiScaleY;

            double windowWidth = Math.Min(1600, screenWidth * 0.75);
            double windowHeight = Math.Min(900, screenHeight * 0.75);

            windowWidth = Math.Max(windowWidth, 1024);
            windowHeight = Math.Max(windowHeight, 700);

            this.Width = windowWidth;
            this.Height = windowHeight;

            this.Left = screenLeft + (screenWidth - windowWidth) / 2;
            this.Top = screenTop + (screenHeight - windowHeight) / 2;
        }

        private Rect GetCurrentScreenWorkArea()
        {
            var windowHandle = new WindowInteropHelper(this).Handle;
            if (windowHandle != IntPtr.Zero)
            {
                var monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));

                if (
                    GetMonitorInfo(
                        MonitorFromWindow(windowHandle, MONITOR_DEFAULTTONEAREST),
                        ref monitorInfo
                    )
                )
                {
                    return new Rect(
                        monitorInfo.rcWork.left,
                        monitorInfo.rcWork.top,
                        monitorInfo.rcWork.right - monitorInfo.rcWork.left,
                        monitorInfo.rcWork.bottom - monitorInfo.rcWork.top
                    );
                }
            }

            return SystemParameters.WorkArea;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private void EnableBlur()
        {
            var windowHelper = new WindowInteropHelper(this);
            var accent = new AccentPolicy { AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND };
            var accentStructSize = Marshal.SizeOf(accent);

            var accentPtr = IntPtr.Zero;
            try
            {
                accentPtr = Marshal.AllocHGlobal(accentStructSize);
                Marshal.StructureToPtr(accent, accentPtr, false);

                var data = new WindowCompositionAttributeData
                {
                    Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    SizeOfData = accentStructSize,
                    Data = accentPtr,
                };

                int result = SetWindowCompositionAttribute(windowHelper.Handle, ref data);
                if (result == 0)
                {
                    throw new InvalidOperationException("SetWindowCompositionAttribute failed");
                }
            }
            catch
            {
            }
            finally
            {
                if (accentPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(accentPtr);
                }
            }
        }

        [DllImport("user32.dll")]
        internal static extern int SetWindowCompositionAttribute(
            IntPtr hwnd,
            ref WindowCompositionAttributeData data
        );

        [StructLayout(LayoutKind.Sequential)]
        internal struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        internal enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
            ACCENT_INVALID_STATE = 5,
        }

        internal enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19,
        }

        private void UpdateThemeIcon()
        {
            if (_themeManager == null)
            {
                return;
            }

            string iconPath = _themeManager.IsDarkTheme
                ? "pack://application:,,,/Resources/AppIcons/winhance-rocket-white-transparent-bg.ico"
                : "pack://application:,,,/Resources/AppIcons/winhance-rocket-black-transparent-bg.ico";

            try
            {
                var icon = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
                this.Icon = icon;
            }
            catch (Exception ex)
            {
                try
                {
                    var defaultIcon = new BitmapImage(
                        new Uri(
                            "pack://application:,,,/Resources/AppIcons/winhance-rocket.ico",
                            UriKind.Absolute
                        )
                    );
                    this.Icon = defaultIcon;
                }
                catch
                {
                }
            }

            if (AppIconImage != null)
            {
                try
                {
                    var icon = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
                    AppIconImage.Source = icon;
                }
                catch (Exception ex)
                {
                    try
                    {
                        var defaultIcon = new BitmapImage(
                            new Uri(
                                "pack://application:,,,/Resources/AppIcons/winhance-rocket.ico",
                                UriKind.Absolute
                            )
                        );
                        AppIconImage.Source = defaultIcon;
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void MainWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = FindVisualChild<ScrollViewer>(this);
            if (scrollViewer != null)
            {
                // Redirect the mouse wheel event to the ScrollViewer
                if (e.Delta < 0)
                {
                    scrollViewer.LineDown();
                }
                else
                {
                    scrollViewer.LineUp();
                }

                // Mark the event as handled to prevent it from bubbling up
                e.Handled = true;
            }
        }

        /// <summary>
        /// Finds a visual child of the specified type in the visual tree
        /// </summary>
        private static T FindVisualChild<T>(DependencyObject obj)
            where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);

                if (child != null && child is T)
                {
                    return (T)child;
                }

                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }

            return null;
        }
    }
}
