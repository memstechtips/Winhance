using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class WindowsUIManagementService : IWindowsUIManagementService
    {
        private readonly ILogService _logService;

        public WindowsUIManagementService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public void RestartExplorer()
        {
            try
            {
                var explorerProcesses = Process.GetProcessesByName("explorer");
                foreach (var process in explorerProcesses)
                {
                    process.Kill();
                }

                Thread.Sleep(1000);
                Process.Start("explorer.exe");
            }
            catch (Exception ex)
            {
                _logService.LogError("Failed to restart Explorer", ex);
            }
        }

        public bool IsProcessRunning(string processName)
        {
            try
            {
                return Process.GetProcessesByName(processName).Length > 0;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error checking if process {processName} is running", ex);
                return false;
            }
        }

        public void KillProcess(string processName)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                foreach (var process in processes)
                {
                    process.Kill();
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to kill process {processName}", ex);
            }
        }

        public void RefreshDesktop()
        {
            try
            {
                [DllImport("user32.dll", SetLastError = true)]
                static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

                const uint SPI_SETDESKWALLPAPER = 0x0014;
                const uint SPIF_UPDATEINIFILE = 0x01;
                const uint SPIF_SENDCHANGE = 0x02;

                SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
            }
            catch (Exception ex)
            {
                _logService.LogError("Failed to refresh desktop", ex);
            }
        }

        public async Task<bool> RefreshWindowsGUI(bool killExplorer = true)
        {
            try
            {
                const int HWND_BROADCAST = 0xffff;
                const uint WM_SYSCOLORCHANGE = 0x0015;
                const uint WM_SETTINGCHANGE = 0x001A;
                const uint WM_THEMECHANGE = 0x031A;

                [DllImport("user32.dll", CharSet = CharSet.Auto)]
                static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

                [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
                static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
                    uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

                SendMessage((IntPtr)HWND_BROADCAST, WM_SYSCOLORCHANGE, IntPtr.Zero, IntPtr.Zero);
                SendMessage((IntPtr)HWND_BROADCAST, WM_THEMECHANGE, IntPtr.Zero, IntPtr.Zero);

                if (killExplorer)
                {
                    await Task.Delay(500);

                    bool explorerWasRunning = IsProcessRunning("explorer");

                    if (explorerWasRunning)
                    {
                        KillProcess("explorer");
                        await Task.Delay(1000);

                        int retryCount = 0;
                        const int maxRetries = 5;
                        bool explorerRestarted = false;

                        while (retryCount < maxRetries && !explorerRestarted)
                        {
                            if (IsProcessRunning("explorer"))
                            {
                                explorerRestarted = true;
                            }
                            else
                            {
                                retryCount++;
                                await Task.Delay(1000);
                            }
                        }

                        if (!explorerRestarted)
                        {
                            try
                            {
                                Process.Start("explorer.exe");
                                await Task.Delay(2000);
                            }
                            catch (Exception ex)
                            {
                                _logService.LogError("Failed to start Explorer manually", ex);
                                return false;
                            }
                        }
                    }
                }

                string themeChanged = "ImmersiveColorSet";
                IntPtr themeChangedPtr = Marshal.StringToHGlobalUni(themeChanged);

                try
                {
                    IntPtr result;
                    SendMessageTimeout((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, themeChangedPtr,
                        0x0000, 1000, out result);

                    SendMessage((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, IntPtr.Zero);
                }
                finally
                {
                    Marshal.FreeHGlobal(themeChangedPtr);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError("Error refreshing Windows GUI", ex);
                return false;
            }
        }
    }
}