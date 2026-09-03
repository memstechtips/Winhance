using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

internal sealed class FileCopyNative : IFileCopyNative
{
    private readonly ILogService _logService;

    public FileCopyNative(ILogService logService)
    {
        _logService = logService;
    }

    public unsafe void CopyWithProgress(
        string source,
        string destination,
        Action<long> onProgress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Returning PROGRESS_CANCEL from the routine is what stops a copy mid-file; the pbCancel
        // pointer only helps a caller cancelling from another thread, which is not this shape.
        LPPROGRESS_ROUTINE routine = (_, totalBytesTransferred, _, _, _, _, _, _, _) =>
        {
            onProgress(totalBytesTransferred);
            return cancellationToken.IsCancellationRequested ? PInvoke.PROGRESS_CANCEL : PInvoke.PROGRESS_CONTINUE;
        };

        var copied = PInvoke.CopyFileEx(source, destination, routine, null, null, 0);

        // The delegate is only reachable through the marshalled function pointer for the duration
        // of the call, so nothing else keeps it alive across a collection.
        GC.KeepAlive(routine);

        if (copied)
        {
            return;
        }

        var error = Marshal.GetLastWin32Error();
        if (error == (int)WIN32_ERROR.ERROR_REQUEST_ABORTED)
        {
            // CopyFileEx leaves the partial destination file behind when the routine cancels.
            TryDeletePartialCopy(destination);
            throw new OperationCanceledException(cancellationToken);
        }

        throw new Win32Exception(error, $"Could not copy '{source}' to '{destination}'.");
    }

    private void TryDeletePartialCopy(string destination)
    {
        try
        {
            if (File.Exists(destination))
            {
                File.SetAttributes(destination, FileAttributes.Normal);
                File.Delete(destination);
            }
        }
        catch (Exception ex)
        {
            // The caller is already cancelling; a leftover partial file is not worth failing over.
            _logService.LogDebug($"Best-effort cleanup of the cancelled copy failed: {ex.Message}");
        }
    }
}
