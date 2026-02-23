using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class WindowsUIManagementService : IWindowsUIManagementService
    {
        private readonly ILogService _logService;
        private readonly IProcessExecutor _processExecutor;

        public WindowsUIManagementService(ILogService logService, IProcessExecutor processExecutor)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _processExecutor = processExecutor ?? throw new ArgumentNullException(nameof(processExecutor));
        }

        public bool IsProcessRunning(string processName)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                var isRunning = processes.Length > 0;
                foreach (var process in processes)
                {
                    process.Dispose();
                }
                return isRunning;
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
                    try
                    {
                        process.Kill();
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to kill process {processName}", ex);
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

                [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
                static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
                    uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

                const uint SMTO_ABORTIFHUNG = 0x0002;

                IntPtr result;
                SendMessageTimeout((IntPtr)HWND_BROADCAST, WM_SYSCOLORCHANGE, IntPtr.Zero, IntPtr.Zero,
                    SMTO_ABORTIFHUNG, 1000, out result);
                SendMessageTimeout((IntPtr)HWND_BROADCAST, WM_THEMECHANGE, IntPtr.Zero, IntPtr.Zero,
                    SMTO_ABORTIFHUNG, 1000, out result);

                if (killExplorer)
                {
                    await Task.Delay(500).ConfigureAwait(false);

                    bool explorerWasRunning = IsProcessRunning("explorer");

                    if (explorerWasRunning)
                    {
                        KillProcess("explorer");
                        await Task.Delay(1000).ConfigureAwait(false);

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
                                await Task.Delay(1000).ConfigureAwait(false);
                            }
                        }

                        if (!explorerRestarted)
                        {
                            try
                            {
                                await _processExecutor.ShellExecuteAsync("explorer.exe").ConfigureAwait(false);
                                await Task.Delay(2000).ConfigureAwait(false);
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
                    SendMessageTimeout((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, themeChangedPtr,
                        SMTO_ABORTIFHUNG, 1000, out result);

                    SendMessageTimeout((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, IntPtr.Zero,
                        SMTO_ABORTIFHUNG, 1000, out result);
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