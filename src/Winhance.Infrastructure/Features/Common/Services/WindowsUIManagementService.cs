using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Native;

namespace Winhance.Infrastructure.Features.Common.Services;

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

    public async Task<OperationResult> RefreshWindowsGUI(bool killExplorer = true)
    {
        try
        {
            IntPtr result;
            User32Api.SendMessageTimeout(
                (IntPtr)User32Api.HWND_BROADCAST, User32Api.WM_SYSCOLORCHANGE,
                IntPtr.Zero, IntPtr.Zero, User32Api.SMTO_ABORTIFHUNG, 1000, out result);
            User32Api.SendMessageTimeout(
                (IntPtr)User32Api.HWND_BROADCAST, User32Api.WM_THEMECHANGE,
                IntPtr.Zero, IntPtr.Zero, User32Api.SMTO_ABORTIFHUNG, 1000, out result);

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
                            return OperationResult.Failed("Failed to start Explorer manually", ex);
                        }
                    }
                }
            }

            string themeChanged = "ImmersiveColorSet";
            IntPtr themeChangedPtr = Marshal.StringToHGlobalUni(themeChanged);

            try
            {
                User32Api.SendMessageTimeout(
                    (IntPtr)User32Api.HWND_BROADCAST, User32Api.WM_SETTINGCHANGE,
                    IntPtr.Zero, themeChangedPtr, User32Api.SMTO_ABORTIFHUNG, 1000, out result);

                User32Api.SendMessageTimeout(
                    (IntPtr)User32Api.HWND_BROADCAST, User32Api.WM_SETTINGCHANGE,
                    IntPtr.Zero, IntPtr.Zero, User32Api.SMTO_ABORTIFHUNG, 1000, out result);
            }
            finally
            {
                Marshal.FreeHGlobal(themeChangedPtr);
            }

            return OperationResult.Succeeded();
        }
        catch (Exception ex)
        {
            _logService.LogError("Error refreshing Windows GUI", ex);
            return OperationResult.Failed("Error refreshing Windows GUI", ex);
        }
    }
}
