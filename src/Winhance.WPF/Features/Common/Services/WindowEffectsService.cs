using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Winhance.WPF.Features.Common.Services
{
    public class WindowEffectsService
    {
        [DllImport("user32.dll")]
        internal static extern int SetWindowCompositionAttribute(
            IntPtr hwnd,
            ref WindowCompositionAttributeData data
        );

        [DllImport("user32.dll")]
        internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        internal static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MONITORINFO
        {
            public int Size;
            public RECT Monitor;
            public RECT WorkArea;
            public int Flags;
        }

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

        public void EnableBlur(Window window)
        {
            try
            {
                var windowHelper = new WindowInteropHelper(window);
                var accent = new AccentPolicy
                {
                    AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND
                };

                var accentStructSize = Marshal.SizeOf(accent);
                var accentPtr = Marshal.AllocHGlobal(accentStructSize);
                Marshal.StructureToPtr(accent, accentPtr, false);

                var data = new WindowCompositionAttributeData
                {
                    Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    SizeOfData = accentStructSize,
                    Data = accentPtr
                };

                SetWindowCompositionAttribute(windowHelper.Handle, ref data);
                Marshal.FreeHGlobal(accentPtr);
            }
            catch
            {
                // Silently fail
            }
        }

        public RECT GetCurrentScreenWorkArea(Window window)
        {
            var windowHelper = new WindowInteropHelper(window);
            var monitor = MonitorFromWindow(windowHelper.Handle, 2);
            var monitorInfo = new MONITORINFO { Size = Marshal.SizeOf(typeof(MONITORINFO)) };

            if (GetMonitorInfo(monitor, ref monitorInfo))
                return monitorInfo.WorkArea;

            return new RECT
            {
                Left = 0,
                Top = 0,
                Right = (int)SystemParameters.PrimaryScreenWidth,
                Bottom = (int)SystemParameters.PrimaryScreenHeight
            };
        }

        public void SetDynamicWindowSize(Window window)
        {
            var workArea = GetCurrentScreenWorkArea(window);

            double dpiScaleX = 1.0;
            double dpiScaleY = 1.0;

            try
            {
                var presentationSource = PresentationSource.FromVisual(window);
                if (presentationSource?.CompositionTarget != null)
                {
                    dpiScaleX = presentationSource.CompositionTarget.TransformToDevice.M11;
                    dpiScaleY = presentationSource.CompositionTarget.TransformToDevice.M22;
                }
            }
            catch
            {
                // Use default DPI
            }

            double screenWidth = workArea.Right - workArea.Left;
            double screenHeight = workArea.Bottom - workArea.Top;
            double screenLeft = workArea.Left / dpiScaleX;
            double screenTop = workArea.Top / dpiScaleY;

            screenWidth /= dpiScaleX;
            screenHeight /= dpiScaleY;

            double windowWidth = Math.Min(1600, screenWidth * 0.75);
            double windowHeight = Math.Min(900, screenHeight * 0.75);

            windowWidth = Math.Max(windowWidth, 1024);
            windowHeight = Math.Max(windowHeight, 700);

            window.Width = windowWidth;
            window.Height = windowHeight;

            double windowLeft = screenLeft + (screenWidth - windowWidth) / 2;
            double windowTop = screenTop + (screenHeight - windowHeight) / 2;

            window.Left = Math.Max(0, windowLeft);
            window.Top = Math.Max(0, windowTop);
        }
    }
}
