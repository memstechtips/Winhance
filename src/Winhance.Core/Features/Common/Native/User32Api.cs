using System;
using System.Runtime.InteropServices;

namespace Winhance.Core.Features.Common.Native
{
    public static class User32Api
    {
        // Window message constants
        public const int HWND_BROADCAST = 0xffff;
        public const uint WM_SYSCOLORCHANGE = 0x0015;
        public const uint WM_SETTINGCHANGE = 0x001A;
        public const uint WM_THEMECHANGE = 0x031A;

        // SendMessageTimeout flags
        public const uint SMTO_ABORTIFHUNG = 0x0002;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint Msg,
            IntPtr wParam,
            IntPtr lParam,
            uint fuFlags,
            uint uTimeout,
            out IntPtr lpdwResult);
    }
}
