using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Resources.Theme;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Common.Views
{
    /// <summary>
    /// Interaction logic for LoadingWindow.xaml
    /// </summary>
    public partial class LoadingWindow : Window
    {
        private readonly IThemeManager? _themeManager;
        private LoadingWindowViewModel? _viewModel;

        /// <summary>
        /// Default constructor for design-time
        /// </summary>
        public LoadingWindow()
        {
            // Ignore this error, it works fine in the designer
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with dependency injection
        /// </summary>
        /// <param name="themeManager">The theme manager</param>
        public LoadingWindow(IThemeManager themeManager)
        {
            _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            // Ignore this error, it works fine in the designer
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with dependency injection and progress service
        /// </summary>
        /// <param name="themeManager">The theme manager</param>
        /// <param name="progressService">The progress service</param>
        public LoadingWindow(IThemeManager themeManager, ITaskProgressService progressService)
        {
            _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            // Ignore this error, it works fine in the designer
            InitializeComponent();

            // Create and set the view model
            _viewModel = new LoadingWindowViewModel(progressService);
            DataContext = _viewModel;

            // Set the appropriate icon based on the theme
            UpdateThemeIcon();
        }

        /// <summary>
        /// Called when the window source is initialized
        /// </summary>
        /// <param name="e">Event arguments</param>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            try
            {
                var helper = new WindowInteropHelper(this);
                if (helper.Handle != IntPtr.Zero)
                {
                    EnableBlur();
                }
            }
            catch (Exception)
            {
                // Ignore blur errors - not critical for loading window
            }
        }

        /// <summary>
        /// Enables the blur effect on the window
        /// </summary>
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
                    Data = accentPtr
                };

                SetWindowCompositionAttribute(windowHelper.Handle, ref data);
            }
            finally
            {
                if (accentPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(accentPtr);
                }
            }
        }

        /// <summary>
        /// P/Invoke for SetWindowCompositionAttribute
        /// </summary>
        [DllImport("user32.dll")]
        internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        /// <summary>
        /// Accent policy structure
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        /// <summary>
        /// Window composition attribute data structure
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        /// <summary>
        /// Accent state enum
        /// </summary>
        internal enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
            ACCENT_INVALID_STATE = 5
        }

        /// <summary>
        /// Window composition attribute enum
        /// </summary>
        internal enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        /// <summary>
        /// Updates the window and image icons based on the current theme
        /// </summary>
        private void UpdateThemeIcon()
        {
            if (_themeManager == null)
            {
                Debug.WriteLine("Cannot update theme icon: ThemeManager is null");
                return;
            }

            try
            {
                Debug.WriteLine($"Updating theme icon. Current theme: {(_themeManager.IsDarkTheme ? "Dark" : "Light")}");

                // Get the appropriate icon based on the theme
                string iconPath = _themeManager.IsDarkTheme
                    ? "pack://application:,,,/Resources/AppIcons/winhance-rocket-white-transparent-bg.ico"
                    : "pack://application:,,,/Resources/AppIcons/winhance-rocket-black-transparent-bg.ico";

                Debug.WriteLine($"Selected icon path: {iconPath}");

                // Create a BitmapImage from the icon path
                var iconImage = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
                iconImage.Freeze(); // Freeze for better performance and thread safety

                // Set the window icon
                this.Icon = iconImage;
                Debug.WriteLine("Window icon updated");

                // Set the image control source
                if (AppIconImage != null)
                {
                    AppIconImage.Source = iconImage;
                    Debug.WriteLine("AppIconImage source updated");
                }
                else
                {
                    Debug.WriteLine("AppIconImage is null, cannot update source");
                }
            }
            catch (Exception ex)
            {
                // If there's an error, fall back to the default icon
                try
                {
                    var defaultIcon = new BitmapImage(new Uri("pack://application:,,,/Resources/AppIcons/winhance-rocket.ico", UriKind.Absolute));
                    this.Icon = defaultIcon;
                    if (AppIconImage != null)
                    {
                        AppIconImage.Source = defaultIcon;
                    }
                }
                catch
                {
                    // Ignore any errors with the fallback icon
                }
            }
        }
    }
}
