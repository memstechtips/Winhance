using System;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Native;

namespace Winhance.Infrastructure.Features.Common.Services;

/// <summary>
/// Opens folders in Explorer, reusing an existing window if the folder is already open.
/// Uses Shell.Application COM interop to enumerate Explorer windows and User32 P/Invoke
/// to bring a matching window to the foreground.
/// </summary>
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
            Type? shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType != null)
            {
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic windows = shell.Windows();

                foreach (dynamic window in windows)
                {
                    try
                    {
                        string? locationUrl = window.LocationURL;
                        if (string.IsNullOrEmpty(locationUrl))
                            continue;

                        Uri uri = new Uri(locationUrl);
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
