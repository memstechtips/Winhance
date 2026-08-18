using System.Runtime.InteropServices;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Native;

namespace Winhance.Infrastructure.Features.Common.Services;

public class ExplorerWindowManager(
    IProcessExecutor processExecutor,
    ILogService logService) : IExplorerWindowManager
{
    public async Task OpenFolderAsync(string folderPath)
    {
        string normalizedPath = System.IO.Path.GetFullPath(folderPath)
            .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)
            .ToLowerInvariant();

        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType != null)
            {
                dynamic? shell = null;
                dynamic? windows = null;
                try
                {
                    shell = Activator.CreateInstance(shellType)!;
                    windows = shell.Windows();

                    foreach (dynamic window in windows)
                    {
                        try
                        {
                            string? locationUrl = window.LocationURL;
                            if (string.IsNullOrEmpty(locationUrl))
                                continue;

                            var uri = new Uri(locationUrl);
                            string windowPath = System.IO.Path.GetFullPath(uri.LocalPath)
                                .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)
                                .ToLowerInvariant();

                            if (windowPath == normalizedPath)
                            {
                                IntPtr handle = new IntPtr(window.HWND);
                                if (User32Api.IsIconic(handle))
                                {
                                    User32Api.ShowWindow(handle, User32Api.SW_RESTORE);
                                }
                                User32Api.SetForegroundWindow(handle);
                                return;
                            }
                        }
                        catch
                        {
                            // Skip windows that can't be inspected
                        }
                        finally
                        {
                            if (window != null)
                                try { Marshal.ReleaseComObject(window); } catch { }
                        }
                    }
                }
                finally
                {
                    if (windows != null)
                        try { Marshal.ReleaseComObject(windows); } catch { }
                    if (shell != null)
                        try { Marshal.ReleaseComObject(shell); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            logService.LogWarning($"Error checking for existing Explorer windows: {ex.Message}");
        }

        await processExecutor.ShellExecuteAsync("explorer.exe", folderPath).ConfigureAwait(false);
    }
}
